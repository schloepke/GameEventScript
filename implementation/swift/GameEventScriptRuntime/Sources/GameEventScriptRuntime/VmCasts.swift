// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesCasts {
    static func number(_ value: GesValue) -> GesValue {
        if value.kind == .integer { return value }
        if value.kind == .text { return TextNumberCast.read(value.asText) ?? .nothing }
        if value.kind == .series { return number(GesSeries.term(value, index: 0)) }
        return .float(value.asNumber, unit: value.kind == .float ? value.unit : .none)
    }

    static func unit(_ value: GesValue, _ unit: GesUnit) -> GesValue {
        guard unit == .none || value.unit == .none || value.unit == unit else { return .nothing }
        switch value.kind {
        case .integer: return .integer(value.asInteger, unit: unit)
        case .float: return value.asNumber.isFinite ? .float(value.asNumber, unit: unit) : .nothing
        case .vector: return .vector(x: value.x, y: value.y, z: value.z, unit: unit)
        case .point: return .point(x: value.x, y: value.y, z: value.z, unit: unit)
        default: return .nothing
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
        switch kind {
        case .nothing: return .nothing
        case .boolean: return .boolean(value.asBoolean)
        case .integer: return GesNumber.exactInteger(value.asNumber).map { .integer($0, unit: value.kind == .integer || value.kind == .float ? value.unit : .none) } ?? .nothing
        case .float: return .float(value.asNumber, unit: value.kind == .integer || value.kind == .float ? value.unit : .none)
        case .percentage: return TextNumberCast.percentage(value)
        case .text: return .text(value.toText)
        case .tag:
            if value.kind == .tag { return value }
            if value.kind == .boolean { return try .tag(value.asBoolean ? "true" : "false") }
            guard value.kind == .text else { return .nothing }
            let name = value.asText == "True" ? "true" : value.asText == "False" ? "false" : value.asText
            return (try? .tag(name)) ?? .nothing
        case .vector, .point: return spatial(value, point: kind == .point)
        case .dice:
            if value.kind == .dice { return value }
            guard let values = value.listValue else { return .dice([]) }
            var rolls: [Int32] = []
            rolls.reserveCapacity(values.count)
            for value in values {
                guard let number = value.integerValue, number > 0, number <= Int32.max else { return .nothing }
                rolls.append(Int32(number))
            }
            return .dice(rolls)
        case .list:
            if value.kind == .list { return value }
            if let text = value.textValue { return .list(text.unicodeScalars.map { .text(String($0)) }) }
            if value.spatialValue != nil { return .list([.float(value.x, unit: value.unit), .float(value.y, unit: value.unit), .float(value.z, unit: value.unit)]) }
            if let dice = value.diceRolls { return .list(dice.map { .integer(Int64($0)) }) }
            if value.integerRangeValue != nil || value.floatRangeValue != nil {
                let count = value.integerRangeValue?.count ?? value.floatRangeValue!.count
                if count > Int32.max { return .nothing }
                if !context.budget.range(count) { return .list([]) }
                let iterator = GesIterator(value)!
                var result: [GesValue] = []
                result.reserveCapacity(Int(count))
                while let item = iterator.next() { result.append(item) }
                return .list(result)
            }
            return .list([])
        case .map:
            if let map = try value.asMap ?? value.externalMap() { return .map(map.entries) }
            if value.spatialValue != nil { return .map([.init(key: "x", value: .float(value.x, unit: value.unit)), .init(key: "y", value: .float(value.y, unit: value.unit)), .init(key: "z", value: .float(value.z, unit: value.unit))]) }
            return .map([])
        default: return check(value, kind) ? value : .nothing
        }
    }

    private static func spatial(_ value: GesValue, point: Bool) -> GesValue {
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
            if numbers.contains(where: { !$0.isFinite }) { return .nothing }
            x = numbers.count > 0 ? numbers[0] : 0
            y = numbers.count > 1 ? numbers[1] : 0
            z = numbers.count > 2 ? numbers[2] : 0
        } else if let map = value.asMap {
            x = map.get("x")?.asNumber ?? 0
            y = map.get("y")?.asNumber ?? 0
            z = map.get("z")?.asNumber ?? 0
            if value.kind == .map && (!x.isFinite || !y.isFinite || !z.isFinite) { return .nothing }
        } else if value.integerRangeValue != nil || value.floatRangeValue != nil {
            let iterator = GesIterator(value)!
            x = iterator.next()?.asNumber ?? 0
            y = iterator.next()?.asNumber ?? 0
            z = iterator.next()?.asNumber ?? 0
        } else if [.integer, .float, .percentage, .tag, .boolean].contains(value.kind) {
            x = value.asNumber
            if !x.isFinite { return .nothing }
            if value.kind == .integer || value.kind == .float { unit = value.unit }
        } else {
            return .nothing
        }
        return point ? .point(x: x, y: y, z: z, unit: unit) : .vector(x: x, y: y, z: z, unit: unit)
    }
}

enum GesSeries {
    static func term(_ value: GesValue, index: Int64) -> GesValue {
        guard let series = value.seriesValue, index >= 0 else { return .nothing }
        let (index, overflow) = index.addingReportingOverflow(series.offset)
        if overflow || index < 0 { return .nothing }
        switch series.signatureID {
        case "fibonacci":
            if index > 1476 { return .float(.infinity) }
            var a = 0.0
            var b = 1.0
            for _ in 0..<index {
                let next = a + b
                a = b
                b = next
            }
            return .float(a)
        case "factorial":
            if index > 170 { return .float(.infinity) }
            var result = 1.0
            if index > 1 { for factor in 2...index { result *= Double(factor) } }
            return .float(result)
        default: return .nothing
        }
    }
}
