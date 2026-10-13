// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCasts {
    static func spatial<Output: GesValueOutput>(_ value: GesValue, point: Bool, sink: Output) -> Output.Result {
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
