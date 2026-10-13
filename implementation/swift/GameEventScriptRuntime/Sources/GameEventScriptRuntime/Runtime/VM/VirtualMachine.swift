// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Stateless executor. Every mutable execution resource belongs to the host's reusable state.
enum GameEventScriptVirtualMachine {
    static func begin(_ state: GesVmState, linked: GesLinkedProgram, message: GameEventScriptMessage, matchArguments: Bool, entry: Int, signature: String) throws {
        if state.processing { throw GesRuntimeError(diagnostic: .init(phase: .runtime, code: "runtime.vmStateConflict")) }
        state.reset()
        state.linked = linked
        state.handlerName = signature
        state.ip = Int(entry)
        let count = matchArguments ? message.arguments.count : 1
        guard state.ensure(count) else { return }
        state.frameLength = count
        if matchArguments { for index in 0..<count { state.setValue(index, message.arguments[index]) } } else { state.setMessage(0, message) }
        state.processing = true
    }

    static func runSlice(_ state: GesVmState, context: GameEventScriptContext, budget: Int) -> Int {
        if let profiler = state.linked?.profiler {
            return executeSlice(state, context: context, budget: budget, instrumentation: ActiveInstrumentation(profiler: profiler))
        }
        return executeSlice(state, context: context, budget: budget, instrumentation: NoInstrumentation())
    }

    // Specialize the shared loop so the disabled path has no existential call or optional check per opcode.
    // Opcode extensions normally stay out of line: broad inlining regressed the uninstrumented
    // compute benchmark. Keep exceptions such as Add measurement-driven, independently of this loop specialization.
    @inline(__always)
    private static func executeSlice<Instrumentation: VmInstrumentation>(_ state: GesVmState, context: GameEventScriptContext, budget: Int, instrumentation: Instrumentation) -> Int {
        var executed = 0
        // The linked Program and the lazily created budget are fixed for the whole slice.
        let runtimeBudget = context.budget
        let code = state.linked?.program.code ?? []
        let reserved = runtimeBudget.reserve(budget)
        do {
            while true {
                instrumentation.phaseStarting(.stateCheck)
                if !state.processing { break }
                instrumentation.phaseStarting(.budgetCheck)
                if runtimeBudget.isExhausted { break }
                instrumentation.phaseStarting(.sliceCheck)
                if executed >= reserved { break }
                instrumentation.phaseStarting(.fetch)
                if state.ip < 0 || state.ip >= code.count {
                    state.fail("runtime.illegalInstructionPointer")
                    break
                }
                let instruction = code[state.ip]
                state.ip += 1
                instrumentation.instructionStarting(state.ip - 1)
                // Keep dispatch in the loop, matching the C# hot path. Case exits still
                // pass through Advance and the completed-opcode counter below.
                let d = Int(instruction.word0)
                let x = Int(instruction.word1)
                let y = Int(instruction.word2)
                switch instruction.opcode {
                case .nop: break
                case .registerLocals: state.modifyLocals(Int(instruction.signedWord1))
                case .jump: state.ip = y
                case .jumpIfTrue: if state.truth(x) == true { state.ip = y }
                case .jumpIfFalse: if state.truth(x) == false { state.ip = y }
                case .jumpIfNotTrue: if state.truth(x) != true { state.ip = y }
                case .jumpIfNothing: if state.isNothing(x) { state.ip = y }
                case .call: state.call(y, destination: d, predicate: instruction.normalizeResultAsPredicate)
                case .returnVoid: try state.returnValue()
                case .returnValue: try state.returnRegister(x)
                case .createSeries: state.setSeries(d, .init(signatureID: instruction.word2 == 1 ? "fibonacci" : "factorial"))
                case .callExternal: try GesCallbacks.extensionCall(instruction, state, context)
                case .emitInstant, .emitAfter, .publishInstant, .publishAfter: try send(instruction, state, context)
                case .emitMessage, .emitMessageWithTags, .publishMessage, .publishMessageWithTags, .emitMessageValue, .emitMessageValueWithTags, .publishMessageValue, .publishMessageValueWithTags: try message(instruction, state, context)
                case .cast: try GesCasts.cast(state.value(x), GameEventScriptBytecodeTypeKind(rawValue: instruction.word2)!, context, sink: state.output(d))
                case .castCustom:
                    let value = state.value(x)
                    let type = state.text(instruction.word2)
                    if value.customTypeName == type {
                        state.setValue(d, value)
                    } else if value.kind == .map, let binding = state.linked!.recordsByName[type] {
                        state.clearStage()
                        for name in binding.argumentNames { state.stageValue(value.asMap!.get(state.text(name)) ?? .nothing) }
                        if state.processing { state.call(Int(binding.entryAddress), destination: d) }
                    } else {
                        state.setNothing(d)
                    }
                case .castUnit: GesCasts.unit(state.value(x), instruction.decodedUnit, sink: state.output(d))
                case .castNumeric: GesCasts.number(state.value(x), sink: state.output(d))
                case .constructData:
                    if let positions = state.linked!.dataConstructionPositions(type: instruction.word1, labels: instruction.a) {
                        try GesDataConstruction.create(state.text(instruction.word1), positions: positions, state.values(instruction.word2), context, sink: state.output(d))
                    } else {
                        state.setNothing(d)
                    }
                case .splitText: GesTextSplit.split(state.value(x), state.value(y), whitespace: instruction.a == 1, sink: state.output(d))
                case .parseLiteral: try GesLiteralParser.evaluate(state.value(x), context: context, state: state, destination: d)
                case .checkType: state.setBoolean(d, GesCasts.check(state.value(x), GameEventScriptBytecodeTypeKind(rawValue: instruction.word2)!))
                case .checkCustomType: state.setBoolean(d, state.value(x).customTypeName == state.text(instruction.word2))
                case .checkUnit:
                    let value = state.value(x)
                    state.setBoolean(d, ((value.kind == .integer || value.kind == .float) || value.spatialValue != nil) && value.unit == instruction.decodedUnit)
                case .checkNumeric: state.setBoolean(d, state.value(x).isNumeric)
                case .checkInteger:
                    let v = state.value(x)
                    state.setBoolean(d, v.isNumeric && GesNumber.exactInteger(v.asNumber) != nil)
                case .checkFractional:
                    let v = state.value(x)
                    let n = v.asNumber
                    state.setBoolean(d, v.isNumeric && n.isFinite && n != n.rounded(.towardZero))
                case .move:
                    if instruction.unitAndFlags & 0x40 != 0 { state.numericCopy(d, x) } else { state.move(d, x) }
                case .memberAccess: try state.value(y).member(state.text(instruction.word1), sink: state.output(d))
                case .indexAccess: state.value(y).index(Int64(instruction.word1), sink: state.output(d))
                case .propertyAccess:
                    let key = state.value(x)
                    let value = state.value(y)
                    if let integer = key.integerValue { value.index(integer, sink: state.output(d)) } else if let text = key.textValue { try value.member(text, sink: state.output(d)) } else { state.setNothing(d) }
                case .bindHandler: if let message = state.value(x).signatureValue?.createMessage(state.values(instruction.word2)) { state.setMessage(d, message) } else { state.setNothing(d) }
                case .loadNothing: state.setNothing(d)
                case .loadTrue: state.setBoolean(d, true)
                case .loadFalse: state.setBoolean(d, false)
                case .loadInteger: state.setInteger(d, instruction.integer, unit: instruction.decodedUnit)
                case .loadFloat: state.setFloat(d, instruction.float, unit: instruction.decodedUnit)
                case .loadPercentage: state.setPercentage(d, instruction.float)
                case .loadText: state.loadText(d, constant: instruction.word1)
                case .loadTag: state.loadText(d, constant: instruction.word1, tag: true)
                case .loadHandler, .loadMessage:
                    let shape = state.list(instruction.opcode == .loadHandler ? instruction.word2 : instruction.word1)
                    if shape.isEmpty || (instruction.opcode == .loadMessage && state.list(instruction.word2).count != shape.count - 1) {
                        state.fail("runtime.invalidMessageShape")
                        break
                    }
                    let signature = try GameEventScriptMessageSignature(name: state.text(shape[0]), parameters: shape.dropFirst().map(state.text))
                    if instruction.opcode == .loadHandler { state.setHandler(d, signature) } else if let message = signature.createMessage(state.values(instruction.word2)) { state.setMessage(d, message) } else { state.setNothing(d) }
                case .stageRegister: state.stageRegister(x)
                case .stageNothing: state.stageNothing()
                case .stageTrue: state.stageBoolean(true)
                case .stageFalse: state.stageBoolean(false)
                case .stageInteger: state.stageInteger(instruction.integer, unit: instruction.decodedUnit)
                case .stageFloat: state.stageFloat(instruction.float, unit: instruction.decodedUnit)
                case .stagePercentage: state.stagePercentage(instruction.float)
                case .stageText: state.stageTextConstant(instruction.word1)
                case .stageTag: state.stageTextConstant(instruction.word1, tag: true)
                case .createDice:
                    let count = Int(instruction.signedWord1)
                    let sides = Int(instruction.signedWord2)
                    if count <= 0 || sides <= 0 || !runtimeBudget.dice(count: count, sides: sides) {
                        state.setDice(d, [])
                        break
                    }
                    state.setDice(d, (0..<count).map { _ in Int32(context.random.nextInclusiveInteger(1, Int64(sides))) })
                case .createVector, .createPoint:
                    spatial(state, destination: d, start: Int(instruction.signedWord1), point: instruction.opcode == .createPoint)
                    state.clearStage()
                case .createList:
                    state.setList(d, (0..<state.stageLength).map(state.staged))
                    state.clearStage()
                case .createMap:
                    let layout = state.linked!.mapLayout(instruction.word1)
                    if layout.inputCount == state.stageLength {
                        var entries: [GesMapEntry] = []
                        entries.reserveCapacity(layout.keys.count)
                        for index in layout.keys.indices { entries.append(.init(key: layout.keys[index], value: state.staged(layout.sources[index]))) }
                        state.setMap(d, GesValueMap(canonical: entries))
                    } else {
                        state.setNothing(d)
                    }
                    state.clearStage()
                case .createRange, .createRangeWithStep:
                    range(state.value(x), state.value(y), instruction.opcode == .createRangeWithStep ? state.value(Int(instruction.a)) : .integer(1), sink: state.output(d))
                case .createRangeIterator, .createRangeIteratorWithStep, .createRangeIteratorShort:
                    let value: GesValue
                    if instruction.opcode == .createRangeIteratorShort {
                        value = .integerRange(from: Int64(instruction.signedWord1), to: Int64(instruction.signedWord2), step: Int64(Int16(bitPattern: instruction.a)))
                    } else {
                        value = range(state.value(x), state.value(y), instruction.opcode == .createRangeIteratorWithStep ? state.value(Int(instruction.a)) : .integer(1), sink: GesValueFactory())
                    }
                    iterator(state, d, value, context, checkRangeLimit: true)
                case .createRecord:
                    guard let binding = state.linked!.records[instruction.word1] else {
                        state.fail("runtime.invalidRecordBinding")
                        break
                    }
                    state.call(Int(binding.entryAddress), destination: d)
                case .createRecordValue:
                    let value = state.value(x)
                    if let fields = value.asMap, value.kind == .map { state.setRecord(d, typeName: state.text(instruction.word2), fields: fields) } else { state.setNothing(d) }
                case .createExternalType:
                    try GesCallbacks.constructor(instruction, state, context)
                    state.clearStage()
                case .hasValue: state.setBoolean(d, state.value(x).hasValue)
                case .isEmpty: state.setBoolean(d, !state.value(x).hasValue)
                case .default:
                    let value = state.value(x)
                    state.setValue(d, value.hasValue ? value : state.value(y))
                case .randomPush:
                    let seed = state.value(x)
                    if !context.random.push(seed: seed.kind == .integer && !seed.hasUnit ? seed.asInteger : nil) { runtimeBudget.exhaust("MaxRandomScopeDepth", context.runtimeLimits.maxRandomScopeDepth) }
                case .randomPushConstant: if !context.random.push(seed: instruction.integer) { runtimeBudget.exhaust("MaxRandomScopeDepth", context.runtimeLimits.maxRandomScopeDepth) }
                case .randomPop: if !context.random.pop() && !runtimeBudget.isExhausted { state.fail("runtime.randomStackUnderflow") }
                case .iteratorCreate, .iteratorCreateOrJump:
                    try compoundIterator(state, instruction, context)
                    if instruction.opcode == .iteratorCreateOrJump, state.isRegisterData(d) { state.ip = y }
                case .cartesian:
                    cartesian(state, d, state.value(x), state.value(y), runtimeBudget)
                case .iteratorNext where instruction.unitAndFlags & 32 != 0:
                    if nextComponents(state, instruction) { _ = runtimeBudget.loop() }
                case .iteratorNext:
                    if let iterator = state.iterator(x), iterator.isIntegerRange {
                        if let term = iterator.nextIntegerTerm() {
                            state.setInteger(d, term)
                            _ = runtimeBudget.loop()
                        } else {
                            state.setNothing(d)
                            state.ip = y
                        }
                    } else if let iterator = state.iterator(x), (iterator.isSourceIterator ? iterator.nextFromSource(sink: state.output(d)) : iterator.next(sink: state.output(d))) != nil {
                        _ = runtimeBudget.loop()
                    } else {
                        state.setNothing(d)
                        state.ip = y
                    }
                case .iteratorClose:
                    if let iterator = state.iterator(x) { iterator.close() }
                    state.setNothing(x)
                case .listBuilderCreate, .mapBuilderCreate, .distinctBuilderCreate, .groupBuilderCreate, .orderBuilderCreate:
                    let kind: GesCollectionBuilder.Kind
                    switch instruction.opcode {
                    case .listBuilderCreate: kind = .list
                    case .mapBuilderCreate: kind = .map
                    case .distinctBuilderCreate: kind = .distinct
                    case .groupBuilderCreate: kind = .group
                    default: kind = .order
                    }
                    state.createBuilder(d, kind)
                case .listBuilderAdd, .mapBuilderAdd, .distinctBuilderAdd, .groupBuilderAdd, .orderBuilderAdd:
                    if let builder = state.builder(x) {
                        if instruction.opcode == .listBuilderAdd { builder.add(value: state.value(y), budget: runtimeBudget) } else { builder.add(key: state.value(y), value: state.value(Int(instruction.a)), budget: runtimeBudget) }
                    }
                case .listBuilderFinish, .mapBuilderFinish, .distinctBuilderFinish, .groupBuilderFinish, .orderBuilderFinishAscending, .orderBuilderFinishDescending:
                    if let builder = state.builder(x) { builder.finish(descending: instruction.opcode == .orderBuilderFinishDescending, sink: state.output(d)) } else { state.setNothing(d) }
                case .or: state.executeOr(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .and: state.executeAnd(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .xor: state.executeXor(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .implies: state.executeImplies(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .not: state.executeNot(state.frameStart + d, state.frameStart + x)
                case .equal: state.executeEqual(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .notEqual: state.executeNotEqual(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .less: state.executeLess(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .greater: state.executeGreater(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .lessOrEqual: state.executeLessOrEqual(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .greaterOrEqual: state.executeGreaterOrEqual(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .add:
                    if instruction.unitAndFlags & 0x40 != 0 { state.executeAddNumeric(state.frameStart + d, state.frameStart + x, state.frameStart + y) } else { state.executeAdd(state.frameStart + d, state.frameStart + x, state.frameStart + y) }
                case .subtract:
                    if instruction.unitAndFlags & 0x40 != 0 {
                        state.executeSubtractNumeric(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                    } else {
                        state.executeSubtract(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                    }
                case .multiply:
                    if instruction.unitAndFlags & 0x40 != 0 {
                        state.executeMultiplyNumeric(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                    } else {
                        state.executeMultiply(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                    }
                case .divide:
                    if instruction.unitAndFlags & 0x40 != 0 {
                        state.executeDivideNumeric(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                    } else {
                        state.executeDivide(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                    }
                case .power:
                    if instruction.unitAndFlags & 0x40 != 0 { state.executePowerNumeric(state.frameStart + d, state.frameStart + x, state.frameStart + y) } else { state.executePower(state.frameStart + d, state.frameStart + x, state.frameStart + y) }
                case .integerDivide:
                    if instruction.unitAndFlags & 0x40 != 0 {
                        state.executeIntegerDivideNumeric(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                    } else {
                        state.executeIntegerDivide(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                    }
                case .modulo:
                    if instruction.unitAndFlags & 0x40 != 0 {
                        state.executeModuloNumeric(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                    } else {
                        state.executeModulo(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                    }
                case .remainder:
                    if instruction.unitAndFlags & 0x40 != 0 {
                        state.executeRemainderNumeric(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                    } else {
                        state.executeRemainder(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                    }
                case .min: if instruction.unitAndFlags & 0x40 != 0 { state.executeMinNumeric(d, x, y) } else { state.executeMin(d, x, y) }
                case .max: if instruction.unitAndFlags & 0x40 != 0 { state.executeMaxNumeric(d, x, y) } else { state.executeMax(d, x, y) }
                case .negate: if instruction.unitAndFlags & 0x40 != 0 { state.executeNegateNumeric(state.frameStart + d, state.frameStart + x) } else { state.executeNegate(state.frameStart + d, state.frameStart + x) }
                case .abs: if instruction.unitAndFlags & 0x40 != 0 { state.executeAbsNumeric(state.frameStart + d, state.frameStart + x) } else { state.executeAbs(state.frameStart + d, state.frameStart + x) }
                case .logN: state.executeLogN(state.frameStart + d, state.frameStart + x)
                case .chance: state.executeChance(d, x, context)
                case .clamp:
                    if instruction.unitAndFlags & 0x40 != 0 {
                        state.executeClampNumeric(state.frameStart + d, state.frameStart + x, state.frameStart + y, state.frameStart + Int(instruction.a))
                    } else {
                        state.executeClamp(state.frameStart + d, state.frameStart + x, state.frameStart + y, state.frameStart + Int(instruction.a))
                    }
                case .randomTake: state.executeRandomTake(d, x, y, context)
                case .randomTakeFloat: state.executeRandomTakeFloat(d, x, y, context)
                case .term: state.executeTerm(d, x, y)
                case .exp: state.executeExp(state.frameStart + d, state.frameStart + x)
                case .floor: state.executeFloor(state.frameStart + d, state.frameStart + x)
                case .ceil: state.executeCeil(state.frameStart + d, state.frameStart + x)
                case .truncate: state.executeTruncate(state.frameStart + d, state.frameStart + x)
                case .roundHalfEven: state.executeRoundHalfEven(state.frameStart + d, state.frameStart + x)
                case .roundHalfUp: state.executeRoundHalfUp(state.frameStart + d, state.frameStart + x)
                case .roundHalfDown: state.executeRoundHalfDown(state.frameStart + d, state.frameStart + x)
                case .degreeToRadians: state.executeDegreeToRadians(state.frameStart + d, state.frameStart + x)
                case .degreeFromRadians: state.executeDegreeFromRadians(state.frameStart + d, state.frameStart + x)
                case .wrapDegree: state.executeWrapDegree(state.frameStart + d, state.frameStart + x)
                case .sin: state.executeSin(state.frameStart + d, state.frameStart + x)
                case .cos: state.executeCos(state.frameStart + d, state.frameStart + x)
                case .tan: state.executeTan(state.frameStart + d, state.frameStart + x)
                case .asin: state.executeAsin(state.frameStart + d, state.frameStart + x)
                case .acos: state.executeAcos(state.frameStart + d, state.frameStart + x)
                case .atan: state.executeAtan(state.frameStart + d, state.frameStart + x)
                case .atan2: state.executeAtan2(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .hypot2D: state.executeHypot2D(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .hypot3D: state.executeHypot3D(state.frameStart + d, state.frameStart + x, state.frameStart + y, state.frameStart + Int(instruction.a))
                case .distance: state.executeDistance(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .distance2D: state.executeDistance2D(state.frameStart + d, state.frameStart + x, state.frameStart + y, state.frameStart + Int(instruction.a), state.frameStart + Int(instruction.b))
                case .distance3D:
                    state.executeDistance3D(
                        state.frameStart + d,
                        state.frameStart + x,
                        state.frameStart + y,
                        state.frameStart + Int(instruction.a),
                        state.frameStart + Int(instruction.b),
                        state.frameStart + Int(instruction.c),
                        state.frameStart + Int(instruction.d)
                    )
                case .distanceSquared: state.executeDistanceSquared(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .distanceSquared2D: state.executeDistanceSquared2D(state.frameStart + d, state.frameStart + x, state.frameStart + y, state.frameStart + Int(instruction.a), state.frameStart + Int(instruction.b))
                case .distanceSquared3D:
                    state.executeDistanceSquared3D(
                        state.frameStart + d,
                        state.frameStart + x,
                        state.frameStart + y,
                        state.frameStart + Int(instruction.a),
                        state.frameStart + Int(instruction.b),
                        state.frameStart + Int(instruction.c),
                        state.frameStart + Int(instruction.d)
                    )
                case .lengthSquared: state.executeLengthSquared(state.frameStart + d, state.frameStart + x)
                case .lengthSquared2D: state.executeLengthSquared2D(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .lengthSquared3D: state.executeLengthSquared3D(state.frameStart + d, state.frameStart + x, state.frameStart + y, state.frameStart + Int(instruction.a))
                case .normalize: state.executeNormalize(state.frameStart + d, state.frameStart + x)
                case .normalize2D: state.executeNormalize2D(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .normalize3D: state.executeNormalize3D(state.frameStart + d, state.frameStart + x, state.frameStart + y, state.frameStart + Int(instruction.a))
                case .dot: state.executeDot(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .dot2D: state.executeDot2D(state.frameStart + d, state.frameStart + x, state.frameStart + y, state.frameStart + Int(instruction.a), state.frameStart + Int(instruction.b))
                case .dot3D:
                    state.executeDot3D(
                        state.frameStart + d,
                        state.frameStart + x,
                        state.frameStart + y,
                        state.frameStart + Int(instruction.a),
                        state.frameStart + Int(instruction.b),
                        state.frameStart + Int(instruction.c),
                        state.frameStart + Int(instruction.d)
                    )
                case .cross: state.executeCross(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .cross2D: state.executeCross2D(state.frameStart + d, state.frameStart + x, state.frameStart + y, state.frameStart + Int(instruction.a), state.frameStart + Int(instruction.b))
                case .cross3D:
                    state.executeCross3D(
                        state.frameStart + d,
                        state.frameStart + x,
                        state.frameStart + y,
                        state.frameStart + Int(instruction.a),
                        state.frameStart + Int(instruction.b),
                        state.frameStart + Int(instruction.c),
                        state.frameStart + Int(instruction.d)
                    )
                case .angleBetween: state.executeAngleBetween(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .angleBetween2D: state.executeAngleBetween2D(state.frameStart + d, state.frameStart + x, state.frameStart + y, state.frameStart + Int(instruction.a), state.frameStart + Int(instruction.b))
                case .angleBetween3D:
                    state.executeAngleBetween3D(
                        state.frameStart + d,
                        state.frameStart + x,
                        state.frameStart + y,
                        state.frameStart + Int(instruction.a),
                        state.frameStart + Int(instruction.b),
                        state.frameStart + Int(instruction.c),
                        state.frameStart + Int(instruction.d)
                    )
                case .takeFirst: state.executeTakeFirst(d, x, Int(instruction.signedWord2))
                case .dropFirst: state.executeDropFirst(d, x, Int(instruction.signedWord2))
                case .takeLast: state.executeTakeLast(d, x, Int(instruction.signedWord2))
                case .dropLast: state.executeDropLast(d, x, Int(instruction.signedWord2))
                case .takeHighest: state.executeTakeHighest(d, x, Int(instruction.signedWord2))
                case .takeLowest: state.executeTakeLowest(d, x, Int(instruction.signedWord2))
                case .dropHighest: state.executeDropHighest(d, x, Int(instruction.signedWord2))
                case .dropLowest: state.executeDropLowest(d, x, Int(instruction.signedWord2))
                case .oneRandom: state.executeOneRandom(d, x, context)
                case .takeRandom: state.executeTakeRandom(d, x, Int(instruction.signedWord2), context)
                case .oneWeighted: state.executeOneWeighted(d, x, y, context)
                case .takeWeighted: state.executeTakeWeighted(d, x, Int(instruction.signedWord2), Int(instruction.a), context)
                case .count: state.executeCount(d, x)
                case .startsWith: state.executeStartsWith(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .endsWith: state.executeEndsWith(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .contains: state.executeContains(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .containsAny: state.executeContainsAny(d, x, y)
                case .containsAll: state.executeContainsAll(d, x, y)
                case .hasAny: state.executeHasAny(d, x)
                case .hasAll: state.executeHasAll(d, x)
                case .containsValue: state.executeContainsValue(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .union: state.executeUnion(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .intersect: state.executeIntersect(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .zip: state.executeZip(state.frameStart + d, state.frameStart + x, state.frameStart + y)
                case .keysOfMap: try state.executeKeysOfMap(d, x)
                case .valuesOfMap: try state.executeValuesOfMap(d, x)
                case .entriesOfMap: try state.executeEntriesOfMap(d, x)
                case .first: state.executeFirst(d, x)
                case .last: state.executeLast(d, x)
                case .single: state.executeSingle(d, x)
                case .distinct: state.executeDistinct(d, x)
                case .sortAscending: state.executeSortAscending(d, x)
                case .sortDescending: state.executeSortDescending(d, x)
                case .reverse: state.executeReverse(d, x)
                case .shuffle: state.executeShuffle(d, x, context)
                case .hasPattern: state.executeHasPattern(d, x, Int(instruction.signedWord2), instruction.a, Int(instruction.b))
                case .takePattern: state.executeTakePattern(d, x, Int(instruction.signedWord2), instruction.a, Int(instruction.b))
                }
                instrumentation.phaseStarting(.advance)
                executed += 1
            }
        } catch let fault as GameEventScriptExtensionFault { state.fail(fault.diagnostic) } catch let fault as GesRuntimeError { state.fail(fault.diagnostic) } catch { state.fail("runtime.unhandledFailure", error: error) }
        instrumentation.finishSlice()
        runtimeBudget.complete(executed: executed, reserved: reserved, processing: state.processing)
        return executed
    }
}

private protocol VmInstrumentation {
    func phaseStarting(_ phase: GameEventScriptProfilePhase)

    func instructionStarting(_ address: Int)

    func finishSlice()
}

private struct NoInstrumentation: VmInstrumentation {
    @inline(__always) func phaseStarting(_ phase: GameEventScriptProfilePhase) {}

    @inline(__always) func instructionStarting(_ address: Int) {}

    @inline(__always) func finishSlice() {}
}

private struct ActiveInstrumentation: VmInstrumentation {
    let profiler: any GameEventScriptProgramProfiler

    @inline(__always) func phaseStarting(_ phase: GameEventScriptProfilePhase) { profiler.phaseStarting(phase) }

    @inline(__always) func instructionStarting(_ address: Int) { profiler.instructionStarting(address) }

    @inline(__always) func finishSlice() { profiler.finishSlice() }
}
