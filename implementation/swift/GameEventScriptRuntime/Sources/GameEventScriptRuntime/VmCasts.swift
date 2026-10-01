// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesCasts {
    static func number(_ value: GesValue) -> GesValue {
        number(value, sink: GesValueFactory())
    }

    static func number<Output: GesValueOutput>(_ value: GesValue, sink: Output) -> Output.Result {
        if value.kind == .integer { return sink.copy(value) }
        if value.kind == .text { return TextNumberCast.read(value.asText, sink: sink) ?? sink.nothing }
        if value.kind == .series { return GesSeries.term(value, index: 0, sink: sink) }
        return sink.float(value.asNumber, unit: value.kind == .float ? value.unit : .none)
    }

    static func unit(_ value: GesValue, _ unit: GesUnit) -> GesValue {
        Self.unit(value, unit, sink: GesValueFactory())
    }

    static func unit<Output: GesValueOutput>(_ value: GesValue, _ unit: GesUnit, sink: Output) -> Output.Result {
        guard unit == .none || value.unit == .none || value.unit == unit else { return sink.nothing }
        switch value.kind {
        case .integer: return sink.integer(value.asInteger, unit: unit)
        case .float: return value.asNumber.isFinite ? sink.float(value.asNumber, unit: unit) : sink.nothing
        case .vector: return sink.vector(x: value.x, y: value.y, z: value.z, unit: unit)
        case .point: return sink.point(x: value.x, y: value.y, z: value.z, unit: unit)
        default: return sink.nothing
        }
    }

    static func check(_ value: GesValue, _ kind: GameEventScriptBytecodeTypeKind) -> Bool {
        switch kind {
        case .nothing: value.isNothing
        case .boolean: value.kind == .boolean
        case .integer: value.kind == .integer
        case .float: value.kind == .float
        case .percentage: value.kind == .percentage
        case .text: value.kind == .text
        case .tag: value.kind == .tag
        case .vector: value.kind == .vector
        case .point: value.kind == .point
        case .range: value.kind == .integerRange || value.kind == .floatRange
        case .handler: value.kind == .handler
        case .message: value.kind == .message
        case .list: value.kind == .list
        case .dice: value.kind == .dice
        case .map: value.kind == .map || value.kind == .record || value.kind == .external
        case .series: value.kind == .series
        default: false
        }
    }

    static func cast(_ value: GesValue, _ kind: GameEventScriptBytecodeTypeKind, _ context: GameEventScriptContext) throws -> GesValue {
        try cast(value, kind, context, sink: GesValueFactory())
    }

    static func cast<Output: GesValueOutput>(_ value: GesValue, _ kind: GameEventScriptBytecodeTypeKind, _ context: GameEventScriptContext, sink: Output) throws -> Output.Result {
        switch kind {
        case .nothing: return sink.nothing
        case .boolean: return sink.boolean(value.asBoolean)
        case .integer:
            guard let integer = GesNumber.exactInteger(value.asNumber) else { return sink.nothing }
            return sink.integer(integer, unit: value.kind == .integer || value.kind == .float ? value.unit : .none)
        case .float: return sink.float(value.asNumber, unit: value.kind == .integer || value.kind == .float ? value.unit : .none)
        case .percentage: return TextNumberCast.percentage(value, sink: sink)
        case .text: return sink.text(value.toText)
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

    private static func spatial<Output: GesValueOutput>(_ value: GesValue, point: Bool, sink: Output) -> Output.Result {
        var x = 0.0
        var y = 0.0
        var z = 0.0
        var unit = GesUnit.none
        if value.spatialValue != nil {
            x = value.x
            y = value.y
            z = value.z
            unit = value.unit
        } else if let dice = value.diceRolls {
            x = dice.count > 0 ? Double(dice[0]) : 0
            y = dice.count > 1 ? Double(dice[1]) : 0
            z = dice.count > 2 ? Double(dice[2]) : 0
        } else if let list = value.listValue {
            let numbers = list.prefix(3).map(\.asNumber)
            if numbers.contains(where: { !$0.isFinite }) { return sink.nothing }
            x = numbers.count > 0 ? numbers[0] : 0
            y = numbers.count > 1 ? numbers[1] : 0
            z = numbers.count > 2 ? numbers[2] : 0
        } else if let map = value.asMap {
            x = map.get("x")?.asNumber ?? 0
            y = map.get("y")?.asNumber ?? 0
            z = map.get("z")?.asNumber ?? 0
            if value.kind == .map && (!x.isFinite || !y.isFinite || !z.isFinite) { return sink.nothing }
        } else if value.integerRangeValue != nil || value.floatRangeValue != nil {
            let iterator = GesIterator(value)!
            x = iterator.next()?.asNumber ?? 0
            y = iterator.next()?.asNumber ?? 0
            z = iterator.next()?.asNumber ?? 0
        } else if [.integer, .float, .percentage, .tag, .boolean].contains(value.kind) {
            x = value.asNumber
            if !x.isFinite { return sink.nothing }
            if value.kind == .integer || value.kind == .float { unit = value.unit }
        } else {
            return sink.nothing
        }
        return point ? sink.point(x: x, y: y, z: z, unit: unit) : sink.vector(x: x, y: y, z: z, unit: unit)
    }
}

enum GesSeries {
    static func term(_ value: GesValue, index: Int64) -> GesValue {
        term(value, index: index, sink: GesValueFactory())
    }

    static func term<Output: GesValueOutput>(_ value: GesValue, index: Int64, sink: Output) -> Output.Result {
        guard let series = value.seriesValue, index >= 0 else { return sink.nothing }
        let (index, overflow) = index.addingReportingOverflow(series.offset)
        if overflow || index < 0 { return sink.nothing }
        switch series.signatureID {
        case "fibonacci":
            if index > 1476 { return sink.float(.infinity) }
            var a = 0.0
            var b = 1.0
            for _ in 0..<index {
                let next = a + b
                a = b
                b = next
            }
            return sink.float(a)
        case "factorial":
            if index > 170 { return sink.float(.infinity) }
            var result = 1.0
            if index > 1 { for factor in 2...index { result *= Double(factor) } }
            return sink.float(result)
        default: return sink.nothing
        }
    }
}
