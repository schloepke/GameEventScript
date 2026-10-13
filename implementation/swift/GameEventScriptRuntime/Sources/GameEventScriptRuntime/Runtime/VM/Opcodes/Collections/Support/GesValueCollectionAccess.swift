// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesValue {
    var numericOnly: Bool { [.integer, .float, .percentage].contains(kind) }

    func index(_ oneBased: Int64) -> GesValue {
        index(oneBased, sink: GesValueFactory())
    }

    func index<Output: GesValueOutput>(_ oneBased: Int64, sink: Output) -> Output.Result {
        if oneBased <= 0 { return sink.nothing }
        let index = oneBased - 1
        switch kind {
        case .list:
            let values = listValue!
            return index < values.count ? sink.copy(values[Int(index)]) : sink.nothing
        case .dice:
            let rolls = diceRolls!
            return index < rolls.count ? sink.integer(Int64(rolls[Int(index)])) : sink.nothing
        case .vector, .point: return index < 3 ? sink.float(index == 0 ? x : index == 1 ? y : z, unit: unit) : sink.nothing
        case .integerRange:
            guard let term = integerRangeValue!.term(at: oneBased) else { return sink.nothing }
            return sink.integer(term)
        case .floatRange:
            guard let term = floatRangeValue!.term(at: oneBased) else { return sink.nothing }
            return sink.float(term)
        case .text, .tag:
            let text = textValue!.unicodeScalars
            guard index < text.count else { return sink.nothing }
            return sink.text(String(text[text.index(text.startIndex, offsetBy: Int(index))]))
        default: return sink.nothing
        }
    }

    func member(_ key: String) throws -> GesValue {
        try member(key, sink: GesValueFactory())
    }

    func member<Output: GesValueOutput>(_ key: String, sink: Output) throws -> Output.Result {
        if kind == .external { return try sink.copy(externalField(key) ?? .nothing) }
        if let map = asMap { return sink.copy(map.get(key) ?? .nothing) }
        if spatialValue != nil {
            switch key {
            case "x": return sink.float(x, unit: unit)
            case "y": return sink.float(y, unit: unit)
            case "z": return sink.float(z, unit: unit)
            default: return sink.nothing
            }
        }
        if let message = messageValue {
            switch key {
            case "name": return sink.text(message.name)
            case "signature": return sink.text(message.signatureId)
            case "arguments": return sink.map((0..<message.arguments.count).map { .init(key: message.arguments.nameAt($0), value: message.arguments[$0]) })
            case "tags": return sink.list(try message.tags.map(GesValue.tag))
            default: return sink.nothing
            }
        }
        if let signature = signatureValue {
            switch key {
            case "name": return sink.text(signature.name)
            case "signature": return sink.text(signature.signatureId)
            case "parameters": return sink.list(signature.parameters.map(GesValue.text))
            default: return sink.nothing
            }
        }
        return sink.nothing
    }
}
