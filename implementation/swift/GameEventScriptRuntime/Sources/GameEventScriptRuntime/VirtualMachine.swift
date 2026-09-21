// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Stateless executor. Every mutable execution resource belongs to the host's reusable state.
enum GameEventScriptVirtualMachine {
    static func begin(
        _ state: GesVmState, linked: GesLinkedProgram, message: GameEventScriptMessage,
        matchArguments: Bool, entry: Int, signature: String
    ) throws {
        if state.processing {
            throw GesRuntimeError(diagnostic: .init(phase: .runtime, code: "runtime.vmStateConflict"))
        }
        state.reset()
        state.linked = linked
        state.handlerName = signature
        state.ip = Int(entry)
        let count = matchArguments ? message.arguments.count : 1
        guard state.ensure(count) else { return }
        state.frameLength = count
        if matchArguments {
            for index in 0..<count { state.set(index, message.arguments[index]) }
        } else {
            state.set(0, .message(message))
        }
        state.processing = true
    }
    static func runSlice(_ state: GesVmState, context: GameEventScriptContext, budget: Int) -> Int {
        var executed = 0
        let reserved = context.budget.reserve(budget)
        do {
            while state.processing && !context.budget.isExhausted && executed < reserved {
                if state.ip < 0 || state.ip >= state.program.code.count {
                    state.fail("runtime.illegalInstructionPointer")
                    break
                }
                let instruction = state.program.code[state.ip]
                state.ip += 1
                try execute(instruction, state, context)
                executed += 1
            }
        } catch let fault as GameEventScriptExtensionFault { state.fail(fault.diagnostic) } catch let fault
            as GesRuntimeError
        { state.fail(fault.diagnostic) } catch { state.fail("runtime.unhandledFailure", error: error) }
        context.budget.complete(executed: executed, reserved: reserved, processing: state.processing)
        return executed
    }
    private static func execute(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext)
        throws
    {
        let d = Int(i.word0)
        let x = Int(i.word1)
        let y = Int(i.word2)
        switch i.opcode {
        case .nop: break
        case .registerLocals: s.modifyLocals(Int(i.signedWord1))
        case .jump: s.ip = y
        case .jumpIfTrue: if s.value(x).truth == true { s.ip = y }
        case .jumpIfFalse: if s.value(x).truth == false { s.ip = y }
        case .jumpIfNotTrue: if s.value(x).truth != true { s.ip = y }
        case .jumpIfNothing: if s.value(x).isNothing { s.ip = y }
        case .call: s.call(y, destination: d, predicate: i.normalizeResultAsPredicate)
        case .returnVoid: s.returnValue()
        case .returnValue: s.returnValue(s.value(x))
        case .createSeries: s.set(d, .series(.init(signatureID: i.word2 == 1 ? "fibonacci" : "factorial")))
        case .callExternal: try GesCallbacks.extensionCall(i, s, c)
        case .emitMessage, .emitMessageWithTags, .publishMessage, .publishMessageWithTags,
            .emitMessageValue, .emitMessageValueWithTags, .publishMessageValue, .publishMessageValueWithTags:
            try message(i, s, c)
        case .cast: s.set(d, try GesCasts.cast(s.value(x), GameEventScriptBytecodeTypeKind(rawValue: i.word2)!, c))
        case .castCustom:
            let value = s.value(x)
            let type = s.text(i.word2)
            if value.customTypeName == type {
                s.set(d, value)
            } else if value.kind == .map, let binding = s.linked!.recordsByName[type] {
                s.clearStage()
                for name in binding.argumentNames { s.stage(value.asMap!.get(s.text(name)) ?? .nothing) }
                if s.processing { s.call(Int(binding.entryAddress), destination: d) }
            } else {
                s.set(d, .nothing)
            }
        case .castUnit: s.set(d, GesCasts.unit(s.value(x), i.unit))
        case .castNumeric: s.set(d, GesCasts.number(s.value(x)))
        case .parseLiteral: s.set(d, GesLiteralParser.parse(s.value(x), context: c))
        case .checkType:
            s.set(d, .boolean(GesCasts.check(s.value(x), GameEventScriptBytecodeTypeKind(rawValue: i.word2)!)))
        case .checkCustomType: s.set(d, .boolean(s.value(x).customTypeName == s.text(i.word2)))
        case .checkUnit:
            let value = s.value(x)
            s.set(
                d,
                .boolean(
                    ((value.kind == .integer || value.kind == .float) || value.spatialValue != nil)
                        && value.unit == i.unit))
        case .checkNumeric: s.set(d, .boolean(s.value(x).isNumeric))
        case .checkInteger:
            let v = s.value(x)
            s.set(d, .boolean(v.isNumeric && GesNumber.exactInteger(v.asNumber) != nil))
        case .checkFractional:
            let v = s.value(x)
            let n = v.asNumber
            s.set(d, .boolean(v.isNumeric && n.isFinite && n != n.rounded(.towardZero)))
        case .move: s.set(d, s.value(x))
        case .memberAccess: s.set(d, try s.value(y).member(s.text(i.word1)))
        case .indexAccess: s.set(d, s.value(y).index(Int64(i.word1)))
        case .propertyAccess:
            let key = s.value(x)
            let value = s.value(y)
            if let integer = key.integerValue {
                s.set(d, value.index(integer))
            } else if let text = key.textValue {
                s.set(d, try value.member(text))
            } else {
                s.set(d, .nothing)
            }
        case .bindHandler:
            s.set(d, s.value(x).signatureValue?.createMessage(s.values(i.word2)).map(GesValue.message) ?? .nothing)
        case .loadNothing: s.set(d, .nothing)
        case .loadTrue: s.set(d, .boolean(true))
        case .loadFalse: s.set(d, .boolean(false))
        case .loadInteger: s.set(d, .integer(i.integer, unit: i.unit))
        case .loadFloat: s.set(d, .float(i.float, unit: i.unit))
        case .loadPercentage: s.set(d, .percentage(i.float))
        case .loadText: s.set(d, .text(s.text(i.word1)))
        case .loadTag: s.set(d, try .tag(s.text(i.word1)))
        case .loadHandler, .loadMessage:
            let shape = s.list(i.opcode == .loadHandler ? i.word2 : i.word1)
            if shape.isEmpty || (i.opcode == .loadMessage && s.list(i.word2).count != shape.count - 1) {
                s.fail("runtime.invalidMessageShape")
                return
            }
            let signature = try GameEventScriptMessageSignature(
                name: s.text(shape[0]), parameters: shape.dropFirst().map(s.text))
            s.set(
                d,
                i.opcode == .loadHandler
                    ? .handler(signature) : signature.createMessage(s.values(i.word2)).map(GesValue.message) ?? .nothing
            )
        case .stageRegister: s.stage(s.value(x))
        case .stageNothing: s.stage(.nothing)
        case .stageTrue: s.stage(.boolean(true))
        case .stageFalse: s.stage(.boolean(false))
        case .stageInteger: s.stage(.integer(i.integer, unit: i.unit))
        case .stageFloat: s.stage(.float(i.float, unit: i.unit))
        case .stagePercentage: s.stage(.percentage(i.float))
        case .stageText: s.stage(.text(s.text(i.word1)))
        case .stageTag: s.stage(try .tag(s.text(i.word1)))
        case .createDice:
            let count = Int(i.signedWord1)
            let sides = Int(i.signedWord2)
            if count <= 0 || sides <= 0 || !c.budget.dice(count: count, sides: sides) {
                s.set(d, .dice([]))
                return
            }
            s.set(d, .dice((0..<count).map { _ in Int32(c.random.nextInclusiveInteger(1, Int64(sides))) }))
        case .createVector, .createPoint:
            s.set(d, spatial(s, start: Int(i.signedWord1), point: i.opcode == .createPoint))
            s.clearStage()
        case .createList:
            s.set(d, .list((0..<s.stageLength).map(s.staged)))
            s.clearStage()
        case .createMap:
            let names = s.list(i.word1)
            if names.count == s.stageLength {
                s.set(d, .map(names.enumerated().map { .init(key: s.text($0.element), value: s.staged($0.offset)) }))
            } else {
                s.set(d, .nothing)
            }
            s.clearStage()
        case .createRange, .createRangeWithStep, .createRangeIterator, .createRangeIteratorWithStep,
            .createRangeIteratorShort:
            let value: GesValue
            if i.opcode == .createRangeIteratorShort {
                value = .integerRange(
                    from: Int64(i.signedWord1), to: Int64(i.signedWord2), step: Int64(Int16(bitPattern: i.a)))
            } else {
                value = range(
                    s.value(x), s.value(y),
                    [.createRangeWithStep, .createRangeIteratorWithStep].contains(i.opcode)
                        ? s.value(Int(i.a)) : .integer(1))
            }
            if i.opcode == .createRange || i.opcode == .createRangeWithStep {
                s.set(d, value)
            } else {
                iterator(s, d, value, c, checkRangeLimit: true)
            }
        case .createRecord:
            guard let binding = s.linked!.records[i.word1] else {
                s.fail("runtime.invalidRecordBinding")
                return
            }
            s.call(Int(binding.entryAddress), destination: d)
        case .createRecordValue:
            let value = s.value(x)
            s.set(d, value.kind == .map ? .record(typeName: s.text(i.word2), entries: value.mapEntries!) : .nothing)
        case .createExternalType:
            try GesCallbacks.constructor(i, s, c)
            s.clearStage()
        case .hasValue: s.set(d, .boolean(s.value(x).hasValue))
        case .isEmpty: s.set(d, .boolean(!s.value(x).hasValue))
        case .default:
            let value = s.value(x)
            s.set(d, value.hasValue ? value : s.value(y))
        case .randomPush:
            let seed = s.value(x)
            if !c.random.push(seed: seed.kind == .integer && !seed.hasUnit ? seed.asInteger : nil) {
                c.budget.exhaust("MaxRandomScopeDepth", c.runtimeLimits.maxRandomScopeDepth)
            }
        case .randomPushConstant:
            if !c.random.push(seed: i.integer) {
                c.budget.exhaust("MaxRandomScopeDepth", c.runtimeLimits.maxRandomScopeDepth)
            }
        case .randomPop:
            if !c.random.pop() && !c.budget.isExhausted { s.fail("runtime.randomStackUnderflow") }
        case .iteratorCreate, .iteratorCreateOrJump:
            iterator(s, d, s.value(x), c)
            if i.opcode == .iteratorCreateOrJump, case .value = s.slot(d) { s.ip = y }
        case .iteratorNext:
            if case .iterator(let iterator) = s.slot(x), let value = iterator.next() {
                s.set(d, value)
                _ = c.budget.loop()
            } else {
                s.set(d, .nothing)
                s.ip = y
            }
        case .iteratorClose:
            if case .iterator(let iterator) = s.slot(x) { iterator.close() }
            s.set(x, .nothing)
        case .listBuilderCreate, .mapBuilderCreate, .distinctBuilderCreate, .groupBuilderCreate, .orderBuilderCreate:
            let kind: GesCollectionBuilder.Kind
            switch i.opcode {
            case .listBuilderCreate: kind = .list
            case .mapBuilderCreate: kind = .map
            case .distinctBuilderCreate: kind = .distinct
            case .groupBuilderCreate: kind = .group
            default: kind = .order
            }
            s.setSlot(d, .builder(GesCollectionBuilder(kind)))
        case .listBuilderAdd, .mapBuilderAdd, .distinctBuilderAdd, .groupBuilderAdd, .orderBuilderAdd:
            if case .builder(let builder) = s.slot(x) {
                if i.opcode == .listBuilderAdd {
                    builder.add(value: s.value(y), budget: c.budget)
                } else {
                    builder.add(key: s.value(y), value: s.value(Int(i.a)), budget: c.budget)
                }
            }
        case .listBuilderFinish, .mapBuilderFinish, .distinctBuilderFinish, .groupBuilderFinish,
            .orderBuilderFinishAscending, .orderBuilderFinishDescending:
            if case .builder(let builder) = s.slot(x) {
                s.set(d, builder.finish(descending: i.opcode == .orderBuilderFinishDescending))
            } else {
                s.set(d, .nothing)
            }
        default:
            if i.opcode.rawValue >= 0x50 && i.opcode.rawValue <= 0x97 {
                s.set(d, try GesMath.execute(i, s, c))
            } else {
                s.set(d, try GesCollectionOperators.execute(i, s, c))
            }
        }
    }
    static func iterator(
        _ s: GesVmState, _ destination: Int, _ value: GesValue, _ c: GameEventScriptContext,
        checkRangeLimit: Bool = false
    ) {
        var value = value
        if checkRangeLimit, let count = value.integerRangeValue?.count ?? value.floatRangeValue?.count,
            !c.budget.range(count)
        {
            value = .integerRange(from: 0, to: 0, step: 0)
        }
        if let iterator = GesIterator(value) {
            s.setSlot(destination, .iterator(iterator))
        } else {
            s.set(destination, .nothing)
        }
    }
    private static func range(_ from: GesValue, _ to: GesValue, _ step: GesValue) -> GesValue {
        if let from = from.integerValue, let to = to.integerValue, let step = step.integerValue {
            return .integerRange(from: from, to: to, step: step)
        }
        if from.isNumeric && to.isNumeric && step.isNumeric {
            return .floatRange(from: from.asNumber, to: to.asNumber, step: step.asNumber)
        }
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
        return point
            ? .point(x: xyz[0], y: xyz[1], z: xyz[2], unit: unit ?? .none)
            : .vector(x: xyz[0], y: xyz[1], z: xyz[2], unit: unit ?? .none)
    }
    private static func message(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext)
        throws
    {
        let valueMessage = [
            .emitMessageValue, .emitMessageValueWithTags, .publishMessageValue, .publishMessageValueWithTags,
        ].contains(i.opcode)
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
        if [.emitMessageWithTags, .emitMessageValueWithTags, .publishMessageWithTags, .publishMessageValueWithTags]
            .contains(i.opcode)
        {
            var tags: [String] = []
            for value in s.values(valueMessage ? i.word2 : i.word1) {
                if value.kind == .tag {
                    tags.append(value.asText)
                } else if let list = value.listValue {
                    for item in list where item.kind == .tag { tags.append(item.asText) }
                }
            }
            message = try message.withTags(tags)
        }
        if [.publishMessage, .publishMessageWithTags, .publishMessageValue, .publishMessageValueWithTags].contains(
            i.opcode)
        {
            c.publish(message)
        } else {
            c.emit(message)
        }
    }
}
