// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesDataConstruction {
    static func create(_ type: String, _ labels: [String], _ arguments: [GesValue], _ context: GameEventScriptContext) throws -> GesValue {
        try create(type, labels, arguments, context, sink: GesValueFactory())
    }

    static func create<Output: GesValueOutput>(_ type: String, _ labels: [String], _ arguments: [GesValue], _ context: GameEventScriptContext, sink: Output) throws -> Output.Result {
        guard let positions = GesDataConstructionShape.positions(type, labels), arguments.count == labels.count else { return sink.nothing }
        var values = [GesValue](repeating: .nothing, count: max(3, arguments.count))
        for (i, position) in positions.enumerated() { values[position] = arguments[i] }
        let first = values[0]
        switch type {
        case "nothing": return sink.nothing
        case "number":
            if arguments.count == 1 { return GesCasts.number(first, sink: sink) }
            let number = GesCasts.number(first)
            guard values[1].kind == .text, let name = values[1].textValue else { return sink.nothing }
            let units: [String: GesUnit] = ["": .none, "none": .none, "s": .second, "second": .second, "m": .meter, "meter": .meter, "°": .degree, "degree": .degree]
            guard let unit = units[name], number.numericOnly, !number.hasUnit || number.unit == unit else { return sink.nothing }
            if let integer = number.integerValue { return sink.integer(integer, unit: unit) }
            return sink.float(number.asNumber, unit: unit)
        case "record":
            guard first.kind == .text, let name = first.textValue, GesNames.type(name), values[1].kind == .map, let fields = values[1].asMap else { return sink.nothing }
            return sink.record(typeName: name, fields: fields)
        case "range" where arguments.count > 1:
            let step = arguments.count > 2 ? values[2] : .integer(1)
            guard first.numericOnly, values[1].numericOnly, step.numericOnly, !first.hasUnit, !values[1].hasUnit, !step.hasUnit else { return sink.nothing }
            if let from = first.integerValue, let to = values[1].integerValue, let stride = step.integerValue { return sink.integerRange(from: from, to: to, step: stride) }
            return sink.floatRange(from: first.asNumber, to: values[1].asNumber, step: step.asNumber)
        case "series" where first.kind == .text:
            guard let kind = first.textValue, ["fibonacci", "factorial"].contains(kind) else { return sink.nothing }
            if arguments.count > 1 && (values[1].integerValue == nil || values[1].hasUnit || values[1].asInteger < 0) { return sink.nothing }
            return sink.series(.init(signatureID: kind, offset: arguments.count > 1 ? values[1].asInteger : 0))
        case "message" where arguments.count == 2:
            guard let message = first.messageValue else { return sink.nothing }
            var tags: [String] = []

            func add(_ value: GesValue) {
                if let list = value.listValue { for item in list { add(item) } } else { tags.append(value.asText) }
            }

            add(values[1])
            guard let tagged = try? message.withTags(tags) else { return sink.nothing }
            return sink.message(tagged)
        default:
            let kinds: [String: GameEventScriptBytecodeTypeKind] = [
                "percentage": .percentage, "boolean": .boolean, "text": .text, "tag": .tag, "list": .list, "map": .map, "dice": .dice, "vector": .vector, "point": .point, "range": .range, "series": .series, "message": .message, "handler": .handler,
            ]
            return try GesCasts.cast(first, kinds[type] ?? .nothing, context, sink: sink)
        }
    }
}
