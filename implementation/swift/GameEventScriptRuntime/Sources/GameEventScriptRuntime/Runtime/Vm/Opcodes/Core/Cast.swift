// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCasts {
    static func cast(_ value: GesValue, _ kind: GesBytecodeTypeKind, _ context: GesContext) throws -> GesValue {
        try cast(value, kind, context, sink: GesValueFactory())
    }

    static func cast<Output: GesValueOutput>(_ value: GesValue, _ kind: GesBytecodeTypeKind, _ context: GesContext, sink: Output) throws -> Output.Result {
        switch kind {
        case .nothing: return sink.nothing
        case .boolean: return sink.boolean(value.asBoolean)
        case .integer:
            guard let integer = GesNumber.exactInteger(value.asNumber) else { return sink.nothing }
            return sink.integer(integer, unit: value.kind == .integer || value.kind == .float ? value.unit : .none)
        case .float: return sink.float(value.asNumber, unit: value.kind == .integer || value.kind == .float ? value.unit : .none)
        case .percentage: return GesTextNumberCast.percentage(value, sink: sink)
        case .text: return value.kind == .text ? sink.copy(value) : sink.text(value.toText)
        case .tag:
            if value.kind == .tag { return sink.copy(value) }
            if value.kind == .boolean { return try sink.tag(value.asBoolean ? "true" : "false") }
            guard value.kind == .text else { return sink.nothing }
            let name = value.asText == "True" ? "true" : value.asText == "False" ? "false" : value.asText
            guard GesText.isLowerName(name) else { return sink.nothing }
            return try sink.tag(name)
        case .vector, .point: return spatial(value, point: kind == .point, sink: sink)
        case .dice:
            if value.kind == .dice { return sink.copy(value) }
            guard let values = value.listValue else { return sink.dice([]) }
            var rolls: [Int32] = []
            rolls.reserveCapacity(values.count)
            for value in values {
                guard let number = value.integerValue, number > 0, number <= Int32.max else { return sink.nothing }
                rolls.append(Int32(number))
            }
            return sink.dice(rolls)
        case .list:
            if value.kind == .list { return sink.copy(value) }
            if let text = value.textValue { return sink.list(text.unicodeScalars.map { .text(String($0)) }) }
            if value.spatialValue != nil { return sink.list([.float(value.x, unit: value.unit), .float(value.y, unit: value.unit), .float(value.z, unit: value.unit)]) }
            if let dice = value.diceRolls { return sink.list(dice.map { .integer(Int64($0)) }) }
            if value.integerRangeValue != nil || value.floatRangeValue != nil {
                let count = value.integerRangeValue?.count ?? value.floatRangeValue!.count
                if count > Int32.max { return sink.nothing }
                if !context.budget.range(count) { return sink.list([]) }
                let iterator = GesIterator(value)!
                var result: [GesValue] = []
                result.reserveCapacity(Int(count))
                while let item = iterator.next() { result.append(item) }
                return sink.list(result)
            }
            return sink.list([])
        case .map:
            if let map = try value.asMap ?? value.externalMap() { return sink.map(map.entries) }
            if value.spatialValue != nil { return sink.map([.init(key: "x", value: .float(value.x, unit: value.unit)), .init(key: "y", value: .float(value.y, unit: value.unit)), .init(key: "z", value: .float(value.z, unit: value.unit))]) }
            return sink.map([])
        default: return check(value, kind) ? sink.copy(value) : sink.nothing
        }
    }
}
