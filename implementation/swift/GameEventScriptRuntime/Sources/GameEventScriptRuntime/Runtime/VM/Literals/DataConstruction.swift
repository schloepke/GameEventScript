// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesDataConstruction {
    static func create(_ type: String, _ labels: [String], _ arguments: [GesValue], _ context: GameEventScriptContext) throws -> GesValue {
        try create(type, labels, arguments, context, sink: GesValueFactory())
    }

    static func create<Output: GesValueOutput>(_ type: String, _ labels: [String], _ arguments: [GesValue], _ context: GameEventScriptContext, sink: Output) throws -> Output.Result {
        guard let positions = GesDataConstructionShape.positions(type, labels) else { return sink.nothing }
        return try create(type, positions: positions, arguments, context, sink: sink)
    }

    // Positions come from GesDataConstructionShape, resolved once per constant type/label list.
    static func create<Output: GesValueOutput>(_ type: String, positions: [Int], _ arguments: [GesValue], _ context: GameEventScriptContext, sink: Output) throws -> Output.Result {
        guard arguments.count == positions.count else { return sink.nothing }
        var slots = (GesValue.nothing, GesValue.nothing, GesValue.nothing)
        for (index, position) in positions.enumerated() { place(arguments[index], at: position, in: &slots) }
        return try create(type, count: arguments.count, slots, context, sink: sink)
    }

    // VM entry: reads the argument registers directly instead of collecting them into arrays.
    static func create<Output: GesValueOutput>(_ type: String, positions: [Int], registers list: UInt16, _ state: GesVmState, _ context: GameEventScriptContext, sink: Output) throws -> Output.Result {
        let registers = state.list(list)
        guard registers.count == positions.count else { return sink.nothing }
        var slots = (GesValue.nothing, GesValue.nothing, GesValue.nothing)
        for (index, position) in positions.enumerated() { place(state.value(Int(registers[index])), at: position, in: &slots) }
        return try create(type, count: registers.count, slots, context, sink: sink)
    }

    // Every shape has at most three parameters, so positions are 0...2.
    @inline(__always)
    private static func place(_ value: GesValue, at position: Int, in slots: inout (GesValue, GesValue, GesValue)) {
        switch position {
        case 0: slots.0 = value
        case 1: slots.1 = value
        default: slots.2 = value
        }
    }

    private static func create<Output: GesValueOutput>(_ type: String, count: Int, _ slots: (GesValue, GesValue, GesValue), _ context: GameEventScriptContext, sink: Output) throws -> Output.Result {
        let first = slots.0
        switch type {
        case "nothing": return sink.nothing
        case "number":
            if count == 1 { return GesCasts.number(first, sink: sink) }
            let number = GesCasts.number(first)
            guard slots.1.kind == .text, let name = slots.1.textValue else { return sink.nothing }
            guard let unit = unit(named: name), number.numericOnly, !number.hasUnit || number.unit == unit else { return sink.nothing }
            if let integer = number.integerValue { return sink.integer(integer, unit: unit) }
            return sink.float(number.asNumber, unit: unit)
        case "record":
            guard first.kind == .text, let name = first.textValue, GesNames.type(name), slots.1.kind == .map, let fields = slots.1.asMap else { return sink.nothing }
            return sink.record(typeName: name, fields: fields)
        case "range" where count > 1:
            let step = count > 2 ? slots.2 : .integer(1)
            guard first.numericOnly, slots.1.numericOnly, step.numericOnly, !first.hasUnit, !slots.1.hasUnit, !step.hasUnit else { return sink.nothing }
            if let from = first.integerValue, let to = slots.1.integerValue, let stride = step.integerValue { return sink.integerRange(from: from, to: to, step: stride) }
            return sink.floatRange(from: first.asNumber, to: slots.1.asNumber, step: step.asNumber)
        case "series" where first.kind == .text:
            guard let kind = first.textValue, ["fibonacci", "factorial"].contains(kind) else { return sink.nothing }
            if count > 1 && (slots.1.integerValue == nil || slots.1.hasUnit || slots.1.asInteger < 0) { return sink.nothing }
            return sink.series(.init(signatureID: kind, offset: count > 1 ? slots.1.asInteger : 0))
        case "message" where count == 2:
            guard let message = first.messageValue else { return sink.nothing }
            var tags: [String] = []

            func add(_ value: GesValue) {
                if let list = value.listValue { for item in list { add(item) } } else { tags.append(value.asText) }
            }

            add(slots.1)
            guard let tagged = try? message.withTags(tags) else { return sink.nothing }
            return sink.message(tagged)
        default:
            return try GesCasts.cast(first, castKind(type), context, sink: sink)
        }
    }

    // Switches instead of per-call dictionary literals; same exact-name mapping as before.
    private static func unit(named name: String) -> GesUnit? {
        switch name {
        case "", "none": .none
        case "s", "second": .second
        case "m", "meter": .meter
        case "°", "degree": .degree
        default: nil
        }
    }

    private static func castKind(_ type: String) -> GameEventScriptBytecodeTypeKind {
        switch type {
        case "percentage": .percentage
        case "boolean": .boolean
        case "text": .text
        case "tag": .tag
        case "list": .list
        case "map": .map
        case "dice": .dice
        case "vector": .vector
        case "point": .point
        case "range": .range
        case "series": .series
        case "message": .message
        case "handler": .handler
        default: .nothing
        }
    }
}
