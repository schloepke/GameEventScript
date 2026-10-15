// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesCollectionOperators {
    static func length(_ value: GesValue) -> Int64 { value.integerRangeValue?.count ?? value.floatRangeValue?.count ?? (value.spatialValue != nil ? 3 : Int64(value.length)) }

    static func sequence(_ value: GesValue) -> Bool { [.list, .dice, .integerRange, .floatRange].contains(value.kind) }

    static func materialize(_ slot: GesValue, ranges: Bool = true) -> [GesValue]? {
        if let iterator = slot.iteratorValue { return read(iterator) }
        let value = slot.registerValue
        if let list = value.listValue { return list }
        if let dice = value.diceRolls { return dice.map { .integer(Int64($0)) } }
        if ranges && (value.integerRangeValue != nil || value.floatRangeValue != nil) && length(value) <= Int32.max { return read(GesIterator(value)!) }
        return nil
    }

    static func read(_ iterator: GesIterator) -> [GesValue] {
        defer { iterator.close() }
        var values: [GesValue] = []
        while let value = iterator.next() { values.append(value) }
        return values
    }

    static func contains(_ needle: GesValue, _ slot: GesValue) -> Bool {
        if let iterator = slot.iteratorValue {
            defer { iterator.close() }
            while let value = iterator.next() { if value == needle { return true } }
            return false
        }
        let value = slot.registerValue
        if let text = value.textValue {
            guard let part = needle.textValue else { return false }
            let a = Array(text.unicodeScalars)
            let b = Array(part.unicodeScalars)
            if b.isEmpty { return true }
            if b.count > a.count { return false }
            return (0...(a.count - b.count)).contains { a[$0..<($0 + b.count)].elementsEqual(b) }
        }
        if let list = value.listValue { return list.contains(needle) }
        if let dice = value.diceRolls { return needle.kind == .integer && !needle.hasUnit && dice.contains { Int64($0) == needle.asInteger } }
        if value.kind == .map { return needle.textValue.flatMap { value.asMap!.get($0) } != nil }
        if value.spatialValue != nil { return needle.isNumeric && (1...3).contains { value.index(Int64($0)) == needle } }
        if let range = value.integerRangeValue { return needle.kind == .integer && !needle.hasUnit && range.contains(needle.asInteger) }
        if let range = value.floatRangeValue {
            if needle.hasUnit || (needle.kind != .integer && needle.kind != .float) { return false }
            if let integer = needle.integerValue, GesNumber.exactInteger(Double(integer)) != integer { return false }
            return range.contains(needle.asNumber)
        }
        return false
    }

    static func reverseRange(_ value: GesValue) -> GesValue {
        reverseRange(value, sink: GesValueFactory())
    }

    static func reverseRange<Output: GesValueOutput>(_ value: GesValue, sink: Output) -> Output.Result {
        if let range = value.integerRangeValue {
            guard let last = range.term(at: range.count) else { return sink.integerRange(from: 0, to: 0, step: 0) }
            return sink.integerRange(from: last, to: range.from, step: 0 &- range.step)
        }
        let range = value.floatRangeValue!
        guard let last = range.term(at: range.count) else { return sink.integerRange(from: 0, to: 0, step: 0) }
        return sink.floatRange(from: last, to: range.from, step: -range.step)
    }
}
