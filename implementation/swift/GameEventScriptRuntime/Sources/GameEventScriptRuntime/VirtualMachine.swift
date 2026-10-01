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
        if matchArguments { for index in 0..<count { state.set(index, message.arguments[index]) } } else { state.set(0, .message(message)) }
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
                    if !state.setIntegerArithmeticResult(instruction) { state.set(d, try GesMath.execute(instruction, state, context)) }
                case .nop: break
                case .registerLocals: state.modifyLocals(Int(instruction.signedWord1))
                case .jump: state.ip = y
                case .jumpIfTrue: if state.value(x).truth == true { state.ip = y }
                case .jumpIfFalse: if state.value(x).truth == false { state.ip = y }
                case .jumpIfNotTrue: if state.value(x).truth != true { state.ip = y }
                case .jumpIfNothing: if state.value(x).isNothing { state.ip = y }
                case .call: state.call(y, destination: d, predicate: instruction.normalizeResultAsPredicate)
                case .returnVoid: try state.returnValue()
                case .returnValue: try state.returnValue(state.value(x))
                case .createSeries: state.set(d, .series(.init(signatureID: instruction.word2 == 1 ? "fibonacci" : "factorial")))
                case .callExternal: try GesCallbacks.extensionCall(instruction, state, context)
                case .emitInstant, .emitAfter, .publishInstant, .publishAfter: try send(instruction, state, context)
                case .emitMessage, .emitMessageWithTags, .publishMessage, .publishMessageWithTags, .emitMessageValue, .emitMessageValueWithTags, .publishMessageValue, .publishMessageValueWithTags: try message(instruction, state, context)
                case .cast: state.set(d, try GesCasts.cast(state.value(x), GameEventScriptBytecodeTypeKind(rawValue: instruction.word2)!, context))
                case .castCustom:
                    let value = state.value(x)
                    let type = state.text(instruction.word2)
                    if value.customTypeName == type {
                        state.set(d, value)
                    } else if value.kind == .map, let binding = state.linked!.recordsByName[type] {
                        state.clearStage()
                        for name in binding.argumentNames { state.stage(value.asMap!.get(state.text(name)) ?? .nothing) }
                        if state.processing { state.call(Int(binding.entryAddress), destination: d) }
                    } else {
                        state.set(d, .nothing)
                    }
                case .castUnit: state.set(d, GesCasts.unit(state.value(x), instruction.unit))
                case .castNumeric: state.set(d, GesCasts.number(state.value(x)))
                case .constructData: state.set(d, try GesDataConstruction.create(state.text(instruction.word1), state.list(instruction.a).map(state.text), state.values(instruction.word2), context))
                case .splitText: state.set(d, GesDataConstruction.split(state.value(x), state.value(y), whitespace: instruction.a == 1))
                case .parseLiteral: try GesLiteralParser.evaluate(state.value(x), context: context, state: state, destination: d)
                case .checkType: state.set(d, .boolean(GesCasts.check(state.value(x), GameEventScriptBytecodeTypeKind(rawValue: instruction.word2)!)))
                case .checkCustomType: state.set(d, .boolean(state.value(x).customTypeName == state.text(instruction.word2)))
                case .checkUnit:
                    let value = state.value(x)
                    state.set(d, .boolean(((value.kind == .integer || value.kind == .float) || value.spatialValue != nil) && value.unit == instruction.unit))
                case .checkNumeric: state.set(d, .boolean(state.value(x).isNumeric))
                case .checkInteger:
                    let v = state.value(x)
                    state.set(d, .boolean(v.isNumeric && GesNumber.exactInteger(v.asNumber) != nil))
                case .checkFractional:
                    let v = state.value(x)
                    let n = v.asNumber
                    state.set(d, .boolean(v.isNumeric && n.isFinite && n != n.rounded(.towardZero)))
                case .move: state.set(d, state.value(x))
                case .memberAccess: state.set(d, try state.value(y).member(state.text(instruction.word1)))
                case .indexAccess: state.set(d, state.value(y).index(Int64(instruction.word1)))
                case .propertyAccess:
                    let key = state.value(x)
                    let value = state.value(y)
                    if let integer = key.integerValue { state.set(d, value.index(integer)) } else if let text = key.textValue { state.set(d, try value.member(text)) } else { state.set(d, .nothing) }
                case .bindHandler: state.set(d, state.value(x).signatureValue?.createMessage(state.values(instruction.word2)).map(GesValue.message) ?? .nothing)
                case .loadNothing: state.set(d, .nothing)
                case .loadTrue: state.set(d, .boolean(true))
                case .loadFalse: state.set(d, .boolean(false))
                case .loadInteger: state.set(d, .integer(instruction.integer, unit: instruction.unit))
                case .loadFloat: state.set(d, .float(instruction.float, unit: instruction.unit))
                case .loadPercentage: state.set(d, .percentage(instruction.float))
                case .loadText: state.set(d, .text(state.text(instruction.word1)))
                case .loadTag: state.set(d, try .tag(state.text(instruction.word1)))
                case .loadHandler, .loadMessage:
                    let shape = state.list(instruction.opcode == .loadHandler ? instruction.word2 : instruction.word1)
                    if shape.isEmpty || (instruction.opcode == .loadMessage && state.list(instruction.word2).count != shape.count - 1) {
                        state.fail("runtime.invalidMessageShape")
                        break
                    }
                    let signature = try GameEventScriptMessageSignature(name: state.text(shape[0]), parameters: shape.dropFirst().map(state.text))
                    state.set(d, instruction.opcode == .loadHandler ? .handler(signature) : signature.createMessage(state.values(instruction.word2)).map(GesValue.message) ?? .nothing)
                case .stageRegister: state.stage(state.value(x))
                case .stageNothing: state.stage(.nothing)
                case .stageTrue: state.stage(.boolean(true))
                case .stageFalse: state.stage(.boolean(false))
                case .stageInteger: state.stage(.integer(instruction.integer, unit: instruction.unit))
                case .stageFloat: state.stage(.float(instruction.float, unit: instruction.unit))
                case .stagePercentage: state.stage(.percentage(instruction.float))
                case .stageText: state.stage(.text(state.text(instruction.word1)))
                case .stageTag: state.stage(try .tag(state.text(instruction.word1)))
                case .createDice:
                    let count = Int(instruction.signedWord1)
                    let sides = Int(instruction.signedWord2)
                    if count <= 0 || sides <= 0 || !context.budget.dice(count: count, sides: sides) {
                        state.set(d, .dice([]))
                        break
                    }
                    state.set(d, .dice((0..<count).map { _ in Int32(context.random.nextInclusiveInteger(1, Int64(sides))) }))
                case .createVector, .createPoint:
                    state.set(d, spatial(state, start: Int(instruction.signedWord1), point: instruction.opcode == .createPoint))
                    state.clearStage()
                case .createList:
                    state.set(d, .list((0..<state.stageLength).map(state.staged)))
                    state.clearStage()
                case .createMap:
                    let names = state.list(instruction.word1)
                    if names.count == state.stageLength { state.set(d, .map(names.enumerated().map { .init(key: state.text($0.element), value: state.staged($0.offset)) })) } else { state.set(d, .nothing) }
                    state.clearStage()
                case .createRange, .createRangeWithStep, .createRangeIterator, .createRangeIteratorWithStep, .createRangeIteratorShort:
                    let value: GesValue
                    if instruction.opcode == .createRangeIteratorShort {
                        value = .integerRange(from: Int64(instruction.signedWord1), to: Int64(instruction.signedWord2), step: Int64(Int16(bitPattern: instruction.a)))
                    } else {
                        value = range(state.value(x), state.value(y), [.createRangeWithStep, .createRangeIteratorWithStep].contains(instruction.opcode) ? state.value(Int(instruction.a)) : .integer(1))
                    }
                    if instruction.opcode == .createRange || instruction.opcode == .createRangeWithStep { state.set(d, value) } else { iterator(state, d, value, context, checkRangeLimit: true) }
                case .createRecord:
                    guard let binding = state.linked!.records[instruction.word1] else {
                        state.fail("runtime.invalidRecordBinding")
                        break
                    }
                    state.call(Int(binding.entryAddress), destination: d)
                case .createRecordValue:
                    let value = state.value(x)
                    state.set(d, value.kind == .map ? .record(typeName: state.text(instruction.word2), entries: value.mapEntries!) : .nothing)
                case .createExternalType:
                    try GesCallbacks.constructor(instruction, state, context)
                    state.clearStage()
                case .hasValue: state.set(d, .boolean(state.value(x).hasValue))
                case .isEmpty: state.set(d, .boolean(!state.value(x).hasValue))
                case .default:
                    let value = state.value(x)
                    state.set(d, value.hasValue ? value : state.value(y))
                case .randomPush:
                    let seed = state.value(x)
                    if !context.random.push(seed: seed.kind == .integer && !seed.hasUnit ? seed.asInteger : nil) { context.budget.exhaust("MaxRandomScopeDepth", context.runtimeLimits.maxRandomScopeDepth) }
                case .randomPushConstant: if !context.random.push(seed: instruction.integer) { context.budget.exhaust("MaxRandomScopeDepth", context.runtimeLimits.maxRandomScopeDepth) }
                case .randomPop: if !context.random.pop() && !context.budget.isExhausted { state.fail("runtime.randomStackUnderflow") }
                case .iteratorCreate, .iteratorCreateOrJump:
                    iterator(state, d, state.value(x), context)
                    if instruction.opcode == .iteratorCreateOrJump, case .value = state.slot(d) { state.ip = y }
                case .iteratorNext:
                    if case .iterator(let iterator) = state.slot(x), let value = iterator.next() {
                        state.set(d, value)
                        _ = context.budget.loop()
                    } else {
                        state.set(d, .nothing)
                        state.ip = y
                    }
                case .iteratorClose:
                    if case .iterator(let iterator) = state.slot(x) { iterator.close() }
                    state.set(x, .nothing)
                case .listBuilderCreate, .mapBuilderCreate, .distinctBuilderCreate, .groupBuilderCreate, .orderBuilderCreate:
                    let kind: GesCollectionBuilder.Kind
                    switch instruction.opcode {
                    case .listBuilderCreate: kind = .list
                    case .mapBuilderCreate: kind = .map
                    case .distinctBuilderCreate: kind = .distinct
                    case .groupBuilderCreate: kind = .group
                    default: kind = .order
                    }
                    state.setSlot(d, .builder(GesCollectionBuilder(kind)))
                case .listBuilderAdd, .mapBuilderAdd, .distinctBuilderAdd, .groupBuilderAdd, .orderBuilderAdd:
                    if case .builder(let builder) = state.slot(x) {
                        if instruction.opcode == .listBuilderAdd { builder.add(value: state.value(y), budget: context.budget) } else { builder.add(key: state.value(y), value: state.value(Int(instruction.a)), budget: context.budget) }
                    }
                case .listBuilderFinish, .mapBuilderFinish, .distinctBuilderFinish, .groupBuilderFinish, .orderBuilderFinishAscending, .orderBuilderFinishDescending:
                    if case .builder(let builder) = state.slot(x) { state.set(d, builder.finish(descending: instruction.opcode == .orderBuilderFinishDescending)) } else { state.set(d, .nothing) }
                default:
                    if instruction.opcode.rawValue >= 0x50 && instruction.opcode.rawValue <= 0x97 {
                        state.set(d, try GesMath.execute(instruction, state, context))
                    } else {
                        state.set(d, try GesCollectionOperators.execute(instruction, state, context))
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

    static func iterator(_ s: GesVmState, _ destination: Int, _ value: GesValue, _ c: GameEventScriptContext, checkRangeLimit: Bool = false) {
        var value = value
        if checkRangeLimit, let count = value.integerRangeValue?.count ?? value.floatRangeValue?.count, !c.budget.range(count) { value = .integerRange(from: 0, to: 0, step: 0) }
        if let iterator = GesIterator(value) { s.setSlot(destination, .iterator(iterator)) } else { s.set(destination, .nothing) }
    }

    private static func range(_ from: GesValue, _ to: GesValue, _ step: GesValue) -> GesValue {
        if let from = from.integerValue, let to = to.integerValue, let step = step.integerValue { return .integerRange(from: from, to: to, step: step) }
        if from.isNumeric && to.isNumeric && step.isNumeric { return .floatRange(from: from.asNumber, to: to.asNumber, step: step.asNumber) }
        return .nothing
    }

    private static func spatial(_ s: GesVmState, start: Int, point: Bool) -> GesValue {
        var xyz = [0.0, 0.0, 0.0]
        var unit: GesUnit?
        if start == 0 && s.stageLength > 0 && s.staged(0).spatialValue != nil {
            if s.stageLength > 2 { return .nothing }
            let first = s.staged(0)
            xyz = [first.x, first.y, first.z]
            unit = first.unit
            if s.stageLength == 2 {
                let value = s.staged(1)
                if value.unit != unit { return .nothing }
                xyz[2] = value.asNumber
            }
        } else {
            for index in 0..<min(s.stageLength, max(0, 3 - start)) {
                let value = s.staged(index)
                if let unit, value.unit != unit { return .nothing }
                unit = value.unit
                xyz[start + index] = value.asNumber
            }
        }
        return point ? .point(x: xyz[0], y: xyz[1], z: xyz[2], unit: unit ?? .none) : .vector(x: xyz[0], y: xyz[1], z: xyz[2], unit: unit ?? .none)
    }

    private static func tagged(_ message: GameEventScriptMessage, _ values: [GesValue]) throws -> GameEventScriptMessage {
        var tags: [String] = []

        func append(_ value: GesValue) {
            if let list = value.listValue { for item in list { append(item) } } else { tags.append(value.asText) }
        }

        for value in values { append(value) }
        let untagged = GameEventScriptMessage(normalizedName: message.name, arguments: message.arguments, signatureId: message.signatureId, normalizedTags: [])
        return try untagged.withTags(tags)
    }

    private static func send(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext) throws {
        var micros: Int64 = 0
        if i.opcode == .emitAfter || i.opcode == .publishAfter {
            let delay = s.value(Int(i.b))
            guard delay.unit == .second && (delay.kind == .integer || delay.kind == .float) else {
                s.set(Int(i.word0), .boolean(false))
                return
            }
            if let value = delay.integerValue {
                guard value >= 0 && value <= Int64.max / 1_000_000 else {
                    s.set(Int(i.word0), .boolean(false))
                    return
                }
                micros = value * 1_000_000
            } else {
                let rounded = (delay.asNumber * 1_000_000).rounded(.up)
                guard delay.asNumber >= 0 && rounded.isFinite && rounded >= 0 && rounded < 9223372036854775808.0 else {
                    s.set(Int(i.word0), .boolean(false))
                    return
                }
                micros = Int64(rounded)
            }
        }
        let message: GameEventScriptMessage?
        if i.unitAndFlags & 0x80 != 0 { message = s.value(Int(i.word1)).messageValue } else { message = s.linked!.outbound[i.word1]?.createMessage(s.values(i.word2)) }
        guard var message else {
            s.set(Int(i.word0), .boolean(false))
            return
        }
        if i.unitAndFlags & 0x40 != 0 {
            guard let taggedMessage = try? tagged(message, s.values(i.a)) else {
                s.set(Int(i.word0), .boolean(false))
                return
            }
            message = taggedMessage
        }
        let accepted = c.send(message, publish: i.opcode == .publishInstant || i.opcode == .publishAfter, microseconds: micros)
        s.set(Int(i.word0), .boolean(accepted))
    }

    private static func message(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext) throws {
        let valueMessage = [.emitMessageValue, .emitMessageValueWithTags, .publishMessageValue, .publishMessageValueWithTags].contains(i.opcode)
        var message: GameEventScriptMessage?
        if valueMessage {
            let value = s.value(Int(i.word1))
            message = value.messageValue ?? value.signatureValue?.createMessage([])
        } else {
            guard let signature = s.linked!.outbound[i.word0] else {
                s.fail("runtime.invalidMessageBinding")
                return
            }
            message = signature.createMessage(s.values(i.word2))
        }
        guard var message else { return }
        if [.emitMessageWithTags, .emitMessageValueWithTags, .publishMessageWithTags, .publishMessageValueWithTags].contains(i.opcode) {
            do { message = try tagged(message, s.values(valueMessage ? i.word2 : i.word1)) } catch {
                if valueMessage { throw error }
                return
            }
        }
        if [.publishMessage, .publishMessageWithTags, .publishMessageValue, .publishMessageValueWithTags].contains(i.opcode) { c.publish(message) } else { c.emit(message) }
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
