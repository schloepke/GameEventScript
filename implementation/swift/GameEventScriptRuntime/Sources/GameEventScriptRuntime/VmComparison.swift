// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesComparison {
    static func near(_ a: Double, _ b: Double) -> Bool {
        if a.isNaN || b.isNaN { return false }
        if a == b { return true }
        if !a.isFinite || !b.isFinite { return false }
        func ordered(_ x: Double) -> UInt64 {
            let bits = x.bitPattern
            return bits & (1 << 63) != 0 ? ~bits &+ 1 : bits | (1 << 63)
        }
        let left = ordered(a)
        let right = ordered(b)
        return (left > right ? left - right : right - left) <= 2
    }
    static func equal(_ a: GesValue, _ b: GesValue) -> Bool {
        if a.isNothing || b.isNothing { return false }
        if a.kind == .integer && b.kind == .integer { return a.unit == b.unit && a.integerValue == b.integerValue }
        if a.numericOnly && b.numericOnly { return a.unit == b.unit && near(a.asNumber, b.asNumber) }
        if let av = a.spatialValue, let bv = b.spatialValue {
            return a.kind == b.kind && a.unit == b.unit && near(av.x, bv.x) && near(av.y, bv.y) && near(av.z, bv.z)
        }
        if a.kind == .dice && b.kind == .dice || a.kind == .dice && b.kind == .integer
            || b.kind == .dice && a.kind == .integer
        {
            return !a.hasUnit && !b.hasUnit && a.asInteger == b.asInteger
        }
        if a.kind == .dice && b.isNumeric || b.kind == .dice && a.isNumeric {
            return !a.hasUnit && !b.hasUnit && near(a.asNumber, b.asNumber)
        }
        if let av = a.floatRangeValue, let bv = b.floatRangeValue {
            return near(av.from, bv.from) && near(av.to, bv.to) && near(av.step, bv.step)
        }
        if a.kind == .external || b.kind == .external { return false }
        if a.isNumeric && b.isNumeric && a.kind != b.kind { return near(a.asNumber, b.asNumber) }
        return a == b
    }
    static func order(_ a: GesValue, _ b: GesValue) -> Int? {
        if a.isNothing || b.isNothing { return nil }
        if a.isNumeric && b.isNumeric {
            if a.unit != b.unit { return nil }
            return a.asNumber < b.asNumber ? -1 : a.asNumber > b.asNumber ? 1 : 0
        }
        let left = rank(a.kind)
        let right = rank(b.kind)
        if left != right { return left < right ? -1 : 1 }
        if let av = a.textValue, let bv = b.textValue {
            return GesText.scalarEqual(av, bv) ? 0 : GesText.scalarLess(av, bv) ? -1 : 1
        }
        if let av = a.spatialValue, let bv = b.spatialValue {
            if a.unit != b.unit { return nil }
            for (x, y) in [(av.x, bv.x), (av.y, bv.y), (av.z, bv.z)] { if x != y { return x < y ? -1 : 1 } }
        }
        return 0
    }
    static func extreme(_ a: GesValue, _ b: GesValue, maximum: Bool) -> GesValue {
        if a.isNothing || b.isNothing { return .nothing }
        let comparison: Int
        if (a.numericOnly || a.kind == .boolean) && (b.numericOnly || b.kind == .boolean) {
            if a.unit != b.unit { return .nothing }
            if let av = a.integerValue, let bv = b.integerValue {
                comparison = av < bv ? -1 : av > bv ? 1 : 0
            } else {
                comparison = a.asNumber < b.asNumber ? -1 : a.asNumber > b.asNumber ? 1 : 0
            }
        } else if rank(a.kind) != rank(b.kind) {
            comparison = rank(a.kind) < rank(b.kind) ? -1 : 1
        } else if a.spatialValue != nil && a.kind == b.kind && a.unit != b.unit {
            func unitRank(_ unit: GesUnit) -> Int {
                switch unit {
                case .none: 0
                case .degree: 1
                case .meter: 2
                case .second: 3
                }
            }
            comparison = unitRank(a.unit) < unitRank(b.unit) ? -1 : 1
        } else if a.kind == .dice && b.kind == .dice {
            comparison = 0
        } else if a.kind != b.kind {
            func kindRank(_ value: GesValue) -> Int {
                switch value.kind {
                case .integerRange, .floatRange: 10
                case .series: 49
                case .record, .external: 255
                default: 0
                }
            }
            comparison = kindRank(a) < kindRank(b) ? -1 : kindRank(a) > kindRank(b) ? 1 : 0
        } else {
            comparison = order(a, b) ?? 0
        }
        return (maximum ? comparison < 0 : comparison > 0) ? b : a
    }
    static func sorted(_ values: [GesValue], keys: [GesValue]? = nil, descending: Bool = false) -> [GesValue]? {
        var result = values
        var keys = keys ?? values
        // Stable insertion ordering matches the portable incomparable-key rules.
        for index in result.indices.dropFirst() {
            let value = result[index]
            let key = keys[index]
            var cursor = index
            while cursor > 0 {
                guard let comparison = order(keys[cursor - 1], key) else { return nil }
                if descending ? comparison >= 0 : comparison <= 0 { break }
                result[cursor] = result[cursor - 1]
                keys[cursor] = keys[cursor - 1]
                cursor -= 1
            }
            result[cursor] = value
            keys[cursor] = key
        }
        return result
    }
    private static func rank(_ kind: GesValueKind) -> Int {
        switch kind {
        case .integer, .float, .percentage: 1
        case .text: 2
        case .tag: 3
        case .vector: 4
        case .point: 5
        case .boolean: 6
        case .message: 9
        case .handler: 10
        case .list: 11
        case .map: 12
        case .dice: 13
        default: 8
        }
    }
}
