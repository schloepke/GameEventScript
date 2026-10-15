// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func begin(linked: GesLinkedProgram, message: GameEventScriptMessage, matchArguments: Bool, entry: Int, signature: String) throws {
        if processing { throw GesRuntimeError(diagnostic: .init(phase: .runtime, code: "runtime.vmStateConflict")) }
        reset()
        self.linked = linked
        handlerName = signature
        ip = Int(entry)
        let count = matchArguments ? message.arguments.count : 1
        guard ensureRegisterCapacity(count) else { return }
        frameLength = count
        if matchArguments { for index in 0..<count { setValue(index, message.arguments[index]) } } else { setMessage(0, message) }
        processing = true
    }

    func runSlice(context: GesContext, budget: Int) -> Int {
        if let profiler = linked?.profiler {
            return executeSlice(context: context, budget: budget, instrumentation: ActiveInstrumentation(profiler: profiler))
        }
        return executeSlice(context: context, budget: budget, instrumentation: NoInstrumentation())
    }

    // Specialize the shared loop so the disabled path has no existential call or optional check per opcode.
    // Opcode extensions normally stay out of line: broad inlining regressed the uninstrumented
    // compute benchmark. Keep exceptions such as Add measurement-driven, independently of this loop specialization.
    @inline(__always)
    private func executeSlice<Instrumentation: VmInstrumentation>(context: GesContext, budget: Int, instrumentation: Instrumentation) -> Int {
        var executed = 0
        // The linked Program and the lazily created budget are fixed for the whole slice.
        let runtimeBudget = context.budget
        let code = linked?.program.code.instructions ?? []
        let reserved = runtimeBudget.reserve(budget)
        do {
            while true {
                instrumentation.phaseStarting(.stateCheck)
                if !processing { break }
                instrumentation.phaseStarting(.budgetCheck)
                if runtimeBudget.isExhausted { break }
                instrumentation.phaseStarting(.sliceCheck)
                if executed >= reserved { break }
                instrumentation.phaseStarting(.fetch)
                if ip < 0 || ip >= code.count {
                    raiseError("runtime.illegalInstructionPointer")
                    break
                }
                let instruction = code[ip]
                ip += 1
                instrumentation.instructionStarting(ip - 1)
                // Keep dispatch in the loop, matching the C# hot path. Case exits still
                // pass through Advance and the completed-opcode counter below.
                let d = Int(instruction.word0)
                let x = Int(instruction.word1)
                let y = Int(instruction.word2)
                switch instruction.opcode {
                case .nop: break
                case .registerLocals: modifyLocals(Int(instruction.signedWord1))
                case .jump: ip = y
                case .jumpIfTrue: if truth(x) == true { ip = y }
                case .jumpIfFalse: if truth(x) == false { ip = y }
                case .jumpIfNotTrue: if truth(x) != true { ip = y }
                case .jumpIfNothing: if isNothing(x) { ip = y }
                case .call: call(y, destination: d, predicate: instruction.normalizeResultAsPredicate)
                case .returnVoid: try returnValue()
                case .returnValue: try returnRegister(x)
                case .createSeries: setSeries(d, .init(signatureID: instruction.word2 == 1 ? "fibonacci" : "factorial"))
                case .callExternal: try GesCallbacks.extensionCall(instruction, self, context)
                case .emitInstant, .emitAfter, .publishInstant, .publishAfter: try send(instruction, context)
                case .emitMessage, .emitMessageWithTags, .publishMessage, .publishMessageWithTags, .emitMessageValue, .emitMessageValueWithTags, .publishMessageValue, .publishMessageValueWithTags: try emitMessage(instruction, context)
                case .cast: try GesCasts.cast(value(x), GesBytecodeTypeKind(rawValue: instruction.word2)!, context, sink: output(d))
                case .castCustom:
                    let value = self.value(x)
                    let type = text(instruction.word2)
                    if value.customTypeName == type {
                        setValue(d, value)
                    } else if value.kind == .map, let binding = linked!.recordsByName[type] {
                        clearStage()
                        for name in binding.argumentNames { stageValue(value.asMap!.get(text(name)) ?? .nothing) }
                        if processing { call(Int(binding.entryAddress), destination: d) }
                    } else {
                        setNothing(d)
                    }
                case .castUnit: GesCasts.unit(value(x), instruction.decodedUnit, sink: output(d))
                case .castNumeric: GesCasts.number(value(x), sink: output(d))
                case .constructData:
                    if let positions = linked!.dataConstructionPositions(type: instruction.word1, labels: instruction.a) {
                        try GesDataConstruction.create(text(instruction.word1), positions: positions, registers: instruction.word2, self, context, sink: output(d))
                    } else {
                        setNothing(d)
                    }
                case .splitText: GesTextSplit.split(value(x), value(y), whitespace: instruction.a == 1, sink: output(d))
                case .parseLiteral: try GesLiteralParser.evaluate(value(x), context: context, state: self, destination: d)
                case .checkType: setBoolean(d, GesCasts.check(value(x), GesBytecodeTypeKind(rawValue: instruction.word2)!))
                case .checkCustomType: setBoolean(d, value(x).customTypeName == text(instruction.word2))
                case .checkUnit:
                    let value = self.value(x)
                    setBoolean(d, ((value.kind == .integer || value.kind == .float) || value.spatialValue != nil) && value.unit == instruction.decodedUnit)
                case .checkNumeric: setBoolean(d, value(x).isNumeric)
                case .checkInteger:
                    let v = value(x)
                    setBoolean(d, v.isNumeric && GesNumber.exactInteger(v.asNumber) != nil)
                case .checkFractional:
                    let v = value(x)
                    let n = v.asNumber
                    setBoolean(d, v.isNumeric && n.isFinite && n != n.rounded(.towardZero))
                case .move:
                    if instruction.unitAndFlags & 0x40 != 0 { numericCopy(d, x) } else { move(d, x) }
                case .memberAccess: try value(y).member(text(instruction.word1), sink: output(d))
                case .indexAccess: value(y).index(Int64(instruction.word1), sink: output(d))
                case .propertyAccess:
                    let key = value(x)
                    let value = self.value(y)
                    if let integer = key.integerValue { value.index(integer, sink: output(d)) } else if let text = key.textValue { try value.member(text, sink: output(d)) } else { setNothing(d) }
                case .bindHandler: if let message = value(x).signatureValue?.makeMessage(values(instruction.word2)) { setMessage(d, message) } else { setNothing(d) }
                case .loadNothing: setNothing(d)
                case .loadTrue: setBoolean(d, true)
                case .loadFalse: setBoolean(d, false)
                case .loadInteger: setInteger(d, instruction.integer, unit: instruction.decodedUnit)
                case .loadFloat: setFloat(d, instruction.float, unit: instruction.decodedUnit)
                case .loadPercentage: setPercentage(d, instruction.float)
                case .loadText: setTextConstant(d, constant: instruction.word1)
                case .loadTag: setTagConstant(d, constant: instruction.word1)
                case .loadHandler, .loadMessage:
                    let shape = list(instruction.opcode == .loadHandler ? instruction.word2 : instruction.word1)
                    if shape.isEmpty || (instruction.opcode == .loadMessage && list(instruction.word2).count != shape.count - 1) {
                        raiseError("runtime.invalidMessageShape")
                        break
                    }
                    let signature = try GesMessageSignature(name: text(shape[0]), parameters: shape.dropFirst().map(text))
                    if instruction.opcode == .loadHandler { setHandler(d, signature) } else if let message = signature.makeMessage(values(instruction.word2)) { setMessage(d, message) } else { setNothing(d) }
                case .stageRegister: stageRegister(x)
                case .stageNothing: stageNothing()
                case .stageTrue: stageBoolean(true)
                case .stageFalse: stageBoolean(false)
                case .stageInteger: stageInteger(instruction.integer, unit: instruction.decodedUnit)
                case .stageFloat: stageFloat(instruction.float, unit: instruction.decodedUnit)
                case .stagePercentage: stagePercentage(instruction.float)
                case .stageText: stageTextConstant(instruction.word1)
                case .stageTag: stageTagConstant(instruction.word1)
                case .createDice:
                    let count = Int(instruction.signedWord1)
                    let sides = Int(instruction.signedWord2)
                    if count <= 0 || sides <= 0 || !runtimeBudget.dice(count: count, sides: sides) {
                        setDice(d, [])
                        break
                    }
                    setDice(d, (0..<count).map { _ in Int32(context.random.nextInclusiveInteger(1, Int64(sides))) })
                case .createVector, .createPoint:
                    createSpatial(destination: d, start: Int(instruction.signedWord1), point: instruction.opcode == .createPoint)
                    clearStage()
                case .createList:
                    setList(d, (0..<stageLength).map(staged))
                    clearStage()
                case .createMap:
                    let layout = linked!.mapLayout(instruction.word1)
                    if layout.inputCount == stageLength {
                        var entries: [GesMapEntry] = []
                        entries.reserveCapacity(layout.keys.count)
                        for index in layout.keys.indices { entries.append(.init(key: layout.keys[index], value: staged(layout.sources[index]))) }
                        setMap(d, GesValueMap(canonical: entries))
                    } else {
                        setNothing(d)
                    }
                    clearStage()
                case .createRange, .createRangeWithStep:
                    Self.createRange(value(x), value(y), instruction.opcode == .createRangeWithStep ? value(Int(instruction.a)) : .integer(1), sink: output(d))
                case .createRangeIterator, .createRangeIteratorWithStep, .createRangeIteratorShort:
                    let value: GesValue
                    if instruction.opcode == .createRangeIteratorShort {
                        value = .integerRange(from: Int64(instruction.signedWord1), to: Int64(instruction.signedWord2), step: Int64(Int16(bitPattern: instruction.a)))
                    } else {
                        value = Self.createRange(self.value(x), self.value(y), instruction.opcode == .createRangeIteratorWithStep ? self.value(Int(instruction.a)) : .integer(1), sink: GesValueFactory())
                    }
                    createIterator(d, value, context, checkRangeLimit: true)
                case .createRecord:
                    guard let binding = linked!.records[instruction.word1] else {
                        raiseError("runtime.invalidRecordBinding")
                        break
                    }
                    call(Int(binding.entryAddress), destination: d)
                case .createRecordValue:
                    let value = self.value(x)
                    if let fields = value.asMap, value.kind == .map { setRecord(d, typeName: text(instruction.word2), fields: fields) } else { setNothing(d) }
                case .createExternalType:
                    try GesCallbacks.constructor(instruction, self, context)
                    clearStage()
                case .hasValue: setBoolean(d, value(x).hasValue)
                case .isEmpty: setBoolean(d, !value(x).hasValue)
                case .default:
                    let value = self.value(x)
                    setValue(d, value.hasValue ? value : self.value(y))
                case .randomPush:
                    let seed = value(x)
                    if !context.random.push(seed: seed.kind == .integer && !seed.hasUnit ? seed.asInteger : nil) { runtimeBudget.exhaust("MaxRandomScopeDepth", context.runtimeLimits.maxRandomScopeDepth) }
                case .randomPushConstant: if !context.random.push(seed: instruction.integer) { runtimeBudget.exhaust("MaxRandomScopeDepth", context.runtimeLimits.maxRandomScopeDepth) }
                case .randomPop: if !context.random.pop() && !runtimeBudget.isExhausted { raiseError("runtime.randomStackUnderflow") }
                case .iteratorCreate, .iteratorCreateOrJump:
                    try compoundIterator(instruction, context)
                    if instruction.opcode == .iteratorCreateOrJump, isRegisterData(d) { ip = y }
                case .cartesian:
                    cartesian(d, value(x), value(y), runtimeBudget)
                case .iteratorNext where instruction.unitAndFlags & 32 != 0:
                    if nextComponents(instruction) { _ = runtimeBudget.loop() }
                case .iteratorNext:
                    if let iterator = self.iterator(x), iterator.isIntegerRange {
                        if let term = unsafeDowncast(iterator, to: GesIntegerRangeIterator.self).nextIntegerTerm() {
                            setInteger(d, term)
                            _ = runtimeBudget.loop()
                        } else {
                            setNothing(d)
                            ip = y
                        }
                    } else if let iterator = self.iterator(x), iterator.next(into: output(d)) {
                        _ = runtimeBudget.loop()
                    } else {
                        setNothing(d)
                        ip = y
                    }
                case .iteratorClose:
                    if let iterator = self.iterator(x) { iterator.close() }
                    setNothing(x)
                case .listBuilderCreate, .mapBuilderCreate, .distinctBuilderCreate, .groupBuilderCreate, .orderBuilderCreate:
                    let kind: GesCollectionBuilder.Kind
                    switch instruction.opcode {
                    case .listBuilderCreate: kind = .list
                    case .mapBuilderCreate: kind = .map
                    case .distinctBuilderCreate: kind = .distinct
                    case .groupBuilderCreate: kind = .group
                    default: kind = .order
                    }
                    createBuilder(d, kind)
                case .listBuilderAdd, .mapBuilderAdd, .distinctBuilderAdd, .groupBuilderAdd, .orderBuilderAdd:
                    if let builder = self.builder(x) {
                        if instruction.opcode == .listBuilderAdd { builder.add(value: value(y), budget: runtimeBudget) } else { builder.add(key: value(y), value: value(Int(instruction.a)), budget: runtimeBudget) }
                    }
                case .listBuilderFinish, .mapBuilderFinish, .distinctBuilderFinish, .groupBuilderFinish, .orderBuilderFinishAscending, .orderBuilderFinishDescending:
                    if let builder = self.builder(x) { builder.finish(descending: instruction.opcode == .orderBuilderFinishDescending, sink: output(d)) } else { setNothing(d) }
                case .or: executeOr(frameStart + d, frameStart + x, frameStart + y)
                case .and: executeAnd(frameStart + d, frameStart + x, frameStart + y)
                case .xor: executeXor(frameStart + d, frameStart + x, frameStart + y)
                case .implies: executeImplies(frameStart + d, frameStart + x, frameStart + y)
                case .not: executeNot(frameStart + d, frameStart + x)
                case .equal: executeEqual(frameStart + d, frameStart + x, frameStart + y)
                case .notEqual: executeNotEqual(frameStart + d, frameStart + x, frameStart + y)
                case .less: executeLess(frameStart + d, frameStart + x, frameStart + y)
                case .greater: executeGreater(frameStart + d, frameStart + x, frameStart + y)
                case .lessOrEqual: executeLessOrEqual(frameStart + d, frameStart + x, frameStart + y)
                case .greaterOrEqual: executeGreaterOrEqual(frameStart + d, frameStart + x, frameStart + y)
                case .add:
                    if instruction.unitAndFlags & 0x40 != 0 { executeAddNumeric(frameStart + d, frameStart + x, frameStart + y) } else { executeAdd(frameStart + d, frameStart + x, frameStart + y) }
                case .subtract:
                    if instruction.unitAndFlags & 0x40 != 0 {
                        executeSubtractNumeric(frameStart + d, frameStart + x, frameStart + y)
                    } else {
                        executeSubtract(frameStart + d, frameStart + x, frameStart + y)
                    }
                case .multiply:
                    if instruction.unitAndFlags & 0x40 != 0 {
                        executeMultiplyNumeric(frameStart + d, frameStart + x, frameStart + y)
                    } else {
                        executeMultiply(frameStart + d, frameStart + x, frameStart + y)
                    }
                case .divide:
                    if instruction.unitAndFlags & 0x40 != 0 {
                        executeDivideNumeric(frameStart + d, frameStart + x, frameStart + y)
                    } else {
                        executeDivide(frameStart + d, frameStart + x, frameStart + y)
                    }
                case .power:
                    if instruction.unitAndFlags & 0x40 != 0 { executePowerNumeric(frameStart + d, frameStart + x, frameStart + y) } else { executePower(frameStart + d, frameStart + x, frameStart + y) }
                case .integerDivide:
                    if instruction.unitAndFlags & 0x40 != 0 {
                        executeIntegerDivideNumeric(frameStart + d, frameStart + x, frameStart + y)
                    } else {
                        executeIntegerDivide(frameStart + d, frameStart + x, frameStart + y)
                    }
                case .modulo:
                    if instruction.unitAndFlags & 0x40 != 0 {
                        executeModuloNumeric(frameStart + d, frameStart + x, frameStart + y)
                    } else {
                        executeModulo(frameStart + d, frameStart + x, frameStart + y)
                    }
                case .remainder:
                    if instruction.unitAndFlags & 0x40 != 0 {
                        executeRemainderNumeric(frameStart + d, frameStart + x, frameStart + y)
                    } else {
                        executeRemainder(frameStart + d, frameStart + x, frameStart + y)
                    }
                case .min: if instruction.unitAndFlags & 0x40 != 0 { executeMinNumeric(d, x, y) } else { executeMin(d, x, y) }
                case .max: if instruction.unitAndFlags & 0x40 != 0 { executeMaxNumeric(d, x, y) } else { executeMax(d, x, y) }
                case .negate: if instruction.unitAndFlags & 0x40 != 0 { executeNegateNumeric(frameStart + d, frameStart + x) } else { executeNegate(frameStart + d, frameStart + x) }
                case .abs: if instruction.unitAndFlags & 0x40 != 0 { executeAbsNumeric(frameStart + d, frameStart + x) } else { executeAbs(frameStart + d, frameStart + x) }
                case .logN: executeLogN(frameStart + d, frameStart + x)
                case .chance: executeChance(d, x, context)
                case .clamp:
                    if instruction.unitAndFlags & 0x40 != 0 {
                        executeClampNumeric(frameStart + d, frameStart + x, frameStart + y, frameStart + Int(instruction.a))
                    } else {
                        executeClamp(frameStart + d, frameStart + x, frameStart + y, frameStart + Int(instruction.a))
                    }
                case .randomTake: executeRandomTake(d, x, y, context)
                case .randomTakeFloat: executeRandomTakeFloat(d, x, y, context)
                case .term: executeTerm(d, x, y)
                case .exp: executeExp(frameStart + d, frameStart + x)
                case .floor: executeFloor(frameStart + d, frameStart + x)
                case .ceil: executeCeil(frameStart + d, frameStart + x)
                case .truncate: executeTruncate(frameStart + d, frameStart + x)
                case .roundHalfEven: executeRoundHalfEven(frameStart + d, frameStart + x)
                case .roundHalfUp: executeRoundHalfUp(frameStart + d, frameStart + x)
                case .roundHalfDown: executeRoundHalfDown(frameStart + d, frameStart + x)
                case .degreeToRadians: executeDegreeToRadians(frameStart + d, frameStart + x)
                case .degreeFromRadians: executeDegreeFromRadians(frameStart + d, frameStart + x)
                case .wrapDegree: executeWrapDegree(frameStart + d, frameStart + x)
                case .sin: executeSin(frameStart + d, frameStart + x)
                case .cos: executeCos(frameStart + d, frameStart + x)
                case .tan: executeTan(frameStart + d, frameStart + x)
                case .asin: executeAsin(frameStart + d, frameStart + x)
                case .acos: executeAcos(frameStart + d, frameStart + x)
                case .atan: executeAtan(frameStart + d, frameStart + x)
                case .atan2: executeAtan2(frameStart + d, frameStart + x, frameStart + y)
                case .hypot2D: executeHypot2D(frameStart + d, frameStart + x, frameStart + y)
                case .hypot3D: executeHypot3D(frameStart + d, frameStart + x, frameStart + y, frameStart + Int(instruction.a))
                case .distance: executeDistance(frameStart + d, frameStart + x, frameStart + y)
                case .distance2D: executeDistance2D(frameStart + d, frameStart + x, frameStart + y, frameStart + Int(instruction.a), frameStart + Int(instruction.b))
                case .distance3D:
                    executeDistance3D(
                        frameStart + d,
                        frameStart + x,
                        frameStart + y,
                        frameStart + Int(instruction.a),
                        frameStart + Int(instruction.b),
                        frameStart + Int(instruction.c),
                        frameStart + Int(instruction.d)
                    )
                case .distanceSquared: executeDistanceSquared(frameStart + d, frameStart + x, frameStart + y)
                case .distanceSquared2D: executeDistanceSquared2D(frameStart + d, frameStart + x, frameStart + y, frameStart + Int(instruction.a), frameStart + Int(instruction.b))
                case .distanceSquared3D:
                    executeDistanceSquared3D(
                        frameStart + d,
                        frameStart + x,
                        frameStart + y,
                        frameStart + Int(instruction.a),
                        frameStart + Int(instruction.b),
                        frameStart + Int(instruction.c),
                        frameStart + Int(instruction.d)
                    )
                case .lengthSquared: executeLengthSquared(frameStart + d, frameStart + x)
                case .lengthSquared2D: executeLengthSquared2D(frameStart + d, frameStart + x, frameStart + y)
                case .lengthSquared3D: executeLengthSquared3D(frameStart + d, frameStart + x, frameStart + y, frameStart + Int(instruction.a))
                case .normalize: executeNormalize(frameStart + d, frameStart + x)
                case .normalize2D: executeNormalize2D(frameStart + d, frameStart + x, frameStart + y)
                case .normalize3D: executeNormalize3D(frameStart + d, frameStart + x, frameStart + y, frameStart + Int(instruction.a))
                case .dot: executeDot(frameStart + d, frameStart + x, frameStart + y)
                case .dot2D: executeDot2D(frameStart + d, frameStart + x, frameStart + y, frameStart + Int(instruction.a), frameStart + Int(instruction.b))
                case .dot3D:
                    executeDot3D(
                        frameStart + d,
                        frameStart + x,
                        frameStart + y,
                        frameStart + Int(instruction.a),
                        frameStart + Int(instruction.b),
                        frameStart + Int(instruction.c),
                        frameStart + Int(instruction.d)
                    )
                case .cross: executeCross(frameStart + d, frameStart + x, frameStart + y)
                case .cross2D: executeCross2D(frameStart + d, frameStart + x, frameStart + y, frameStart + Int(instruction.a), frameStart + Int(instruction.b))
                case .cross3D:
                    executeCross3D(
                        frameStart + d,
                        frameStart + x,
                        frameStart + y,
                        frameStart + Int(instruction.a),
                        frameStart + Int(instruction.b),
                        frameStart + Int(instruction.c),
                        frameStart + Int(instruction.d)
                    )
                case .angleBetween: executeAngleBetween(frameStart + d, frameStart + x, frameStart + y)
                case .angleBetween2D: executeAngleBetween2D(frameStart + d, frameStart + x, frameStart + y, frameStart + Int(instruction.a), frameStart + Int(instruction.b))
                case .angleBetween3D:
                    executeAngleBetween3D(
                        frameStart + d,
                        frameStart + x,
                        frameStart + y,
                        frameStart + Int(instruction.a),
                        frameStart + Int(instruction.b),
                        frameStart + Int(instruction.c),
                        frameStart + Int(instruction.d)
                    )
                case .takeFirst: executeTakeFirst(d, x, Int(instruction.signedWord2))
                case .dropFirst: executeDropFirst(d, x, Int(instruction.signedWord2))
                case .takeLast: executeTakeLast(d, x, Int(instruction.signedWord2))
                case .dropLast: executeDropLast(d, x, Int(instruction.signedWord2))
                case .takeHighest: executeTakeHighest(d, x, Int(instruction.signedWord2))
                case .takeLowest: executeTakeLowest(d, x, Int(instruction.signedWord2))
                case .dropHighest: executeDropHighest(d, x, Int(instruction.signedWord2))
                case .dropLowest: executeDropLowest(d, x, Int(instruction.signedWord2))
                case .oneRandom: executeOneRandom(d, x, context)
                case .takeRandom: executeTakeRandom(d, x, Int(instruction.signedWord2), context)
                case .oneWeighted: executeOneWeighted(d, x, y, context)
                case .takeWeighted: executeTakeWeighted(d, x, Int(instruction.signedWord2), Int(instruction.a), context)
                case .count: executeCount(d, x)
                case .startsWith: executeStartsWith(frameStart + d, frameStart + x, frameStart + y)
                case .endsWith: executeEndsWith(frameStart + d, frameStart + x, frameStart + y)
                case .contains: executeContains(frameStart + d, frameStart + x, frameStart + y)
                case .containsAny: executeContainsAny(d, x, y)
                case .containsAll: executeContainsAll(d, x, y)
                case .hasAny: executeHasAny(d, x)
                case .hasAll: executeHasAll(d, x)
                case .containsValue: executeContainsValue(frameStart + d, frameStart + x, frameStart + y)
                case .union: executeUnion(frameStart + d, frameStart + x, frameStart + y)
                case .intersect: executeIntersect(frameStart + d, frameStart + x, frameStart + y)
                case .zip: executeZip(frameStart + d, frameStart + x, frameStart + y)
                case .keysOfMap: try executeKeysOfMap(d, x)
                case .valuesOfMap: try executeValuesOfMap(d, x)
                case .entriesOfMap: try executeEntriesOfMap(d, x)
                case .first: executeFirst(d, x)
                case .last: executeLast(d, x)
                case .single: executeSingle(d, x)
                case .distinct: executeDistinct(d, x)
                case .sortAscending: executeSortAscending(d, x)
                case .sortDescending: executeSortDescending(d, x)
                case .reverse: executeReverse(d, x)
                case .shuffle: executeShuffle(d, x, context)
                case .hasPattern: executeHasPattern(d, x, Int(instruction.signedWord2), instruction.a, Int(instruction.b))
                case .takePattern: executeTakePattern(d, x, Int(instruction.signedWord2), instruction.a, Int(instruction.b))
                }
                instrumentation.phaseStarting(.advance)
                executed += 1
            }
        } catch let fault as GesExtensionFaultError { raiseError(fault.diagnostic) } catch let fault as GesRuntimeError { raiseError(fault.diagnostic) } catch { raiseError("runtime.unhandledFailure", error: error) }
        instrumentation.finishSlice()
        runtimeBudget.complete(executed: executed, reserved: reserved, processing: processing)
        return executed
    }
}

private protocol VmInstrumentation {
    func phaseStarting(_ phase: GesProfilePhase)

    func instructionStarting(_ address: Int)

    func finishSlice()
}

private struct NoInstrumentation: VmInstrumentation {
    @inline(__always) func phaseStarting(_ phase: GesProfilePhase) {}

    @inline(__always) func instructionStarting(_ address: Int) {}

    @inline(__always) func finishSlice() {}
}

private struct ActiveInstrumentation: VmInstrumentation {
    let profiler: any GesProgramProfiler

    @inline(__always) func phaseStarting(_ phase: GesProfilePhase) { profiler.phaseStarting(phase) }

    @inline(__always) func instructionStarting(_ address: Int) { profiler.instructionStarting(address) }

    @inline(__always) func finishSlice() { profiler.finishSlice() }
}
