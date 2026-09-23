// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesDataConstruction {
    static let types = ["nothing", "number", "percentage", "boolean", "text", "tag", "list", "map", "dice", "vector", "point", "range", "series", "message", "handler", "record"]

    static func positions(_ type: String, _ labels: [String]) -> [Int]? {
        let parameters: [String]
        switch type {
        case "number": parameters = ["value", "unit"]
        case "range": parameters = ["from", "to", "step"]
        case "series": parameters = ["kind", "offset"]
        case "record": parameters = ["type", "fields"]
        case "message": parameters = ["value", "tags"]
        case "vector", "point": parameters = ["x", "y", "z"]
        default: parameters = ["value"]
        }
        if labels.count > parameters.count || labels.isEmpty && !["nothing", "vector", "point"].contains(type) { return nil }
        var positions: [Int] = []
        for (i, label) in labels.enumerated() {
            guard let position = label == "_" ? i : parameters.firstIndex(of: label), !positions.contains(position) else { return nil }
            positions.append(position)
        }
        if (type == "record" || type == "range" && labels.count > 1) && (!positions.contains(0) || !positions.contains(1)) { return nil }
        if !["nothing", "vector", "point"].contains(type) && !positions.contains(0) { return nil }
        return positions
    }

    static func create(_ type: String, _ labels: [String], _ arguments: [GesValue], _ context: GameEventScriptContext) throws -> GesValue {
        guard let positions = positions(type, labels), arguments.count == labels.count else { return .nothing }
        var values = [GesValue](repeating: .nothing, count: max(3, arguments.count))
        for (i, position) in positions.enumerated() { values[position] = arguments[i] }
        let first = values[0]
        switch type {
        case "nothing": return .nothing
        case "number":
            let number = GesCasts.number(first)
            if arguments.count == 1 { return number }
            guard values[1].kind == .text, let name = values[1].textValue else { return .nothing }
            let units: [String: GesUnit] = ["": .none, "none": .none, "s": .second, "second": .second, "m": .meter, "meter": .meter, "°": .degree, "degree": .degree]
            guard let unit = units[name], number.numericOnly, !number.hasUnit || number.unit == unit else { return .nothing }
            return number.integerValue.map { .integer($0, unit: unit) } ?? .float(number.asNumber, unit: unit)
        case "record":
            guard first.kind == .text, let name = first.textValue, GesNames.type(name), values[1].kind == .map, let fields = values[1].mapEntries else { return .nothing }
            return .record(typeName: name, entries: fields)
        case "range" where arguments.count > 1:
            let step = arguments.count > 2 ? values[2] : .integer(1)
            guard first.numericOnly, values[1].numericOnly, step.numericOnly, !first.hasUnit, !values[1].hasUnit, !step.hasUnit else { return .nothing }
            if let from = first.integerValue, let to = values[1].integerValue, let stride = step.integerValue { return .integerRange(from: from, to: to, step: stride) }
            return .floatRange(from: first.asNumber, to: values[1].asNumber, step: step.asNumber)
        case "series" where first.kind == .text:
            guard let kind = first.textValue, ["fibonacci", "factorial"].contains(kind) else { return .nothing }
            if arguments.count > 1 && (values[1].integerValue == nil || values[1].hasUnit || values[1].asInteger < 0) { return .nothing }
            return .series(.init(signatureID: kind, offset: arguments.count > 1 ? values[1].asInteger : 0))
        case "message" where arguments.count == 2:
            guard let message = first.messageValue else { return .nothing }
            var tags: [String] = []

            func add(_ value: GesValue) {
                if let list = value.listValue { for item in list { add(item) } } else { tags.append(value.asText) }
            }

            add(values[1])
            return (try? message.withTags(tags)).map(GesValue.message) ?? .nothing
        default:
            let kinds: [String: GameEventScriptBytecodeTypeKind] = [
                "percentage": .percentage, "boolean": .boolean, "text": .text, "tag": .tag, "list": .list, "map": .map, "dice": .dice, "vector": .vector, "point": .point, "range": .range, "series": .series, "message": .message, "handler": .handler,
            ]
            return try GesCasts.cast(first, kinds[type] ?? .nothing, context)
        }
    }

    static func whitespace(_ scalar: Unicode.Scalar) -> Bool {
        let v = scalar.value
        return (9...13).contains(v) || [0x20, 0x85, 0xA0, 0x1680, 0x2028, 0x2029, 0x202F, 0x205F, 0x3000].contains(v) || (0x2000...0x200A).contains(v)
    }

    static func split(_ input: GesValue, _ delimiter: GesValue, whitespace: Bool) -> GesValue {
        guard input.kind == .text, let text = input.textValue else { return .nothing }
        if whitespace {
            return .list(text.unicodeScalars.split(whereSeparator: Self.whitespace).map { .text(String(String.UnicodeScalarView($0))) })
        }
        guard delimiter.kind == .text, let separator = delimiter.textValue, !separator.isEmpty else { return .nothing }
        // Scalar arrays avoid Swift's canonical-equivalence and grapheme-cluster matching.
        let scalars = Array(text.unicodeScalars)
        let needle = Array(separator.unicodeScalars)
        var start = 0
        var index = 0
        var result: [GesValue] = []

        func segment(_ end: Int) -> GesValue { end == start ? .nothing : .text(String(String.UnicodeScalarView(scalars[start..<end]))) }

        while index + needle.count <= scalars.count {
            if scalars[index..<(index + needle.count)].elementsEqual(needle) {
                result.append(segment(index))
                index += needle.count
                start = index
            } else {
                index += 1
            }
        }
        result.append(segment(scalars.count))
        return .list(result)
    }
}
