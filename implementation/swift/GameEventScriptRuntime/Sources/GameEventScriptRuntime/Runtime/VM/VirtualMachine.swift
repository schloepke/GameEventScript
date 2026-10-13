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
    @inline(__always)
    private static func executeSlice<Instrumentation: VmInstrumentation>(_ state: GesVmState, context: GameEventScriptContext, budget: Int, instrumentation: Instrumentation) -> Int {
        var executed = 0
        let reserved = context.budget.reserve(budget)
        do {
            while true {
                instrumentation.phaseStarting(.stateCheck)
                if !state.processing { break }
                instrumentation.phaseStarting(.budgetCheck)
                if context.budget.isExhausted { break }
                instrumentation.phaseStarting(.sliceCheck)
                if executed >= reserved { break }
                instrumentation.phaseStarting(.fetch)
                if state.ip < 0 || state.ip >= state.program.code.count {
                    state.fail("runtime.illegalInstructionPointer")
                    break
                }
                let instruction = state.program.code[state.ip]
                state.ip += 1
                instrumentation.instructionStarting(state.ip - 1)
                // Keep dispatch in the loop, matching the C# hot path. Case exits still
                // pass through Advance and the completed-opcode counter below.
                let d = Int(instruction.word0)
                let x = Int(instruction.word1)
                let y = Int(instruction.word2)
                switch instruction.opcode {
                case .add, .subtract, .multiply, .divide, .integerDivide, .modulo, .remainder:
                    if instruction.unitAndFlags & 0x40 != 0 { try state.setNumericArithmeticResult(instruction, context) } else { try state.setArithmeticResult(instruction, context) }
                case .power, .min, .max, .negate, .abs, .clamp:
                    if instruction.unitAndFlags & 0x40 != 0 { try state.setNumericArithmeticResult(instruction, context) } else { try GesMath.execute(instruction, state, context, sink: state.output(d)) }
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
                case .castUnit: GesCasts.unit(state.value(x), instruction.unit, sink: state.output(d))
                case .castNumeric: GesCasts.number(state.value(x), sink: state.output(d))
                case .constructData: try GesDataConstruction.create(state.text(instruction.word1), state.list(instruction.a).map(state.text), state.values(instruction.word2), context, sink: state.output(d))
                case .splitText: GesTextSplit.split(state.value(x), state.value(y), whitespace: instruction.a == 1, sink: state.output(d))
                case .parseLiteral: try GesLiteralParser.evaluate(state.value(x), context: context, state: state, destination: d)
                case .checkType: state.setBoolean(d, GesCasts.check(state.value(x), GameEventScriptBytecodeTypeKind(rawValue: instruction.word2)!))
                case .checkCustomType: state.setBoolean(d, state.value(x).customTypeName == state.text(instruction.word2))
                case .checkUnit:
                    let value = state.value(x)
                    state.setBoolean(d, ((value.kind == .integer || value.kind == .float) || value.spatialValue != nil) && value.unit == instruction.unit)
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
                case .loadInteger: state.setInteger(d, instruction.integer, unit: instruction.unit)
                case .loadFloat: state.setFloat(d, instruction.float, unit: instruction.unit)
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
                case .stageInteger: state.stageInteger(instruction.integer, unit: instruction.unit)
                case .stageFloat: state.stageFloat(instruction.float, unit: instruction.unit)
                case .stagePercentage: state.stagePercentage(instruction.float)
                case .stageText: state.stageTextConstant(instruction.word1)
                case .stageTag: state.stageTextConstant(instruction.word1, tag: true)
                case .createDice:
                    let count = Int(instruction.signedWord1)
                    let sides = Int(instruction.signedWord2)
                    if count <= 0 || sides <= 0 || !context.budget.dice(count: count, sides: sides) {
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
                    let names = state.list(instruction.word1)
                    if names.count == state.stageLength { state.setMap(d, names.enumerated().map { .init(key: state.text($0.element), value: state.staged($0.offset)) }) } else { state.setNothing(d) }
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
                    if !context.random.push(seed: seed.kind == .integer && !seed.hasUnit ? seed.asInteger : nil) { context.budget.exhaust("MaxRandomScopeDepth", context.runtimeLimits.maxRandomScopeDepth) }
                case .randomPushConstant: if !context.random.push(seed: instruction.integer) { context.budget.exhaust("MaxRandomScopeDepth", context.runtimeLimits.maxRandomScopeDepth) }
                case .randomPop: if !context.random.pop() && !context.budget.isExhausted { state.fail("runtime.randomStackUnderflow") }
                case .iteratorCreate, .iteratorCreateOrJump:
                    try compoundIterator(state, instruction, context)
                    if instruction.opcode == .iteratorCreateOrJump, state.slot(d).isRegisterData { state.ip = y }
                case .cartesian:
                    cartesian(state, d, state.value(x), state.value(y), context.budget)
                case .iteratorNext where instruction.unitAndFlags & 32 != 0:
                    if nextComponents(state, instruction) { _ = context.budget.loop() }
                case .iteratorNext:
                    if let iterator = state.slot(x).iteratorValue, iterator.next(sink: state.output(d)) != nil {
                        _ = context.budget.loop()
                    } else {
                        state.setNothing(d)
                        state.ip = y
                    }
                case .iteratorClose:
                    if let iterator = state.slot(x).iteratorValue { iterator.close() }
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
                    if let builder = state.slot(x).builderValue {
                        if instruction.opcode == .listBuilderAdd { builder.add(value: state.value(y), budget: context.budget) } else { builder.add(key: state.value(y), value: state.value(Int(instruction.a)), budget: context.budget) }
                    }
                case .listBuilderFinish, .mapBuilderFinish, .distinctBuilderFinish, .groupBuilderFinish, .orderBuilderFinishAscending, .orderBuilderFinishDescending:
                    if let builder = state.slot(x).builderValue { builder.finish(descending: instruction.opcode == .orderBuilderFinishDescending, sink: state.output(d)) } else { state.setNothing(d) }
                default:
                    if instruction.opcode.rawValue >= 0x50 && instruction.opcode.rawValue <= 0x97 {
                        try GesMath.execute(instruction, state, context, sink: state.output(d))
                    } else {
                        try GesCollectionOperators.execute(instruction, state, context, sink: state.output(d))
                    }
                }
                instrumentation.phaseStarting(.advance)
                executed += 1
            }
        } catch let fault as GameEventScriptExtensionFault { state.fail(fault.diagnostic) } catch let fault as GesRuntimeError { state.fail(fault.diagnostic) } catch { state.fail("runtime.unhandledFailure", error: error) }
        instrumentation.finishSlice()
        context.budget.complete(executed: executed, reserved: reserved, processing: state.processing)
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
