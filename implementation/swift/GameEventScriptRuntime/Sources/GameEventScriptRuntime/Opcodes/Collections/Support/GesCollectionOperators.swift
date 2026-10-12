// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesCollectionOperators {
    static func execute<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, sink: Output) throws -> Output.Result {
        let op = i.opcode
        let slot = s.slot(Int(i.word1))
        let a = slot.registerValue
        switch op {
        case .takeFirst: return try executeTakeFirst(i, s, c, slot, a, sink: sink)
        case .takeLast: return try executeTakeLast(i, s, c, slot, a, sink: sink)
        case .takeHighest: return try executeTakeHighest(i, s, c, slot, a, sink: sink)
        case .takeLowest: return try executeTakeLowest(i, s, c, slot, a, sink: sink)
        case .dropFirst: return try executeDropFirst(i, s, c, slot, a, sink: sink)
        case .dropLast: return try executeDropLast(i, s, c, slot, a, sink: sink)
        case .dropHighest: return try executeDropHighest(i, s, c, slot, a, sink: sink)
        case .dropLowest: return try executeDropLowest(i, s, c, slot, a, sink: sink)
        case .oneRandom: return try executeOneRandom(i, s, c, slot, a, sink: sink)
        case .takeRandom: return try executeTakeRandom(i, s, c, slot, a, sink: sink)
        case .oneWeighted: return try executeOneWeighted(i, s, c, slot, a, sink: sink)
        case .takeWeighted: return try executeTakeWeighted(i, s, c, slot, a, sink: sink)
        case .hasPattern: return try executeHasPattern(i, s, c, slot, a, sink: sink)
        case .takePattern: return try executeTakePattern(i, s, c, slot, a, sink: sink)
        case .count: return try executeCount(i, s, c, slot, a, sink: sink)
        case .first: return try executeFirst(i, s, c, slot, a, sink: sink)
        case .last: return try executeLast(i, s, c, slot, a, sink: sink)
        case .single: return try executeSingle(i, s, c, slot, a, sink: sink)
        case .keysOfMap: return try executeKeysOfMap(i, s, c, slot, a, sink: sink)
        case .valuesOfMap: return try executeValuesOfMap(i, s, c, slot, a, sink: sink)
        case .entriesOfMap: return try executeEntriesOfMap(i, s, c, slot, a, sink: sink)
        case .hasAny: return try executeHasAny(i, s, c, slot, a, sink: sink)
        case .hasAll: return try executeHasAll(i, s, c, slot, a, sink: sink)
        case .contains: return try executeContains(i, s, c, slot, a, sink: sink)
        case .containsValue: return try executeContainsValue(i, s, c, slot, a, sink: sink)
        case .containsAny: return try executeContainsAny(i, s, c, slot, a, sink: sink)
        case .containsAll: return try executeContainsAll(i, s, c, slot, a, sink: sink)
        case .startsWith: return try executeStartsWith(i, s, c, slot, a, sink: sink)
        case .endsWith: return try executeEndsWith(i, s, c, slot, a, sink: sink)
        case .union: return try executeUnion(i, s, c, slot, a, sink: sink)
        case .intersect: return try executeIntersect(i, s, c, slot, a, sink: sink)
        case .zip: return try executeZip(i, s, c, slot, a, sink: sink)
        case .distinct: return try executeDistinct(i, s, c, slot, a, sink: sink)
        case .sortAscending: return try executeSortAscending(i, s, c, slot, a, sink: sink)
        case .sortDescending: return try executeSortDescending(i, s, c, slot, a, sink: sink)
        case .reverse: return try executeReverse(i, s, c, slot, a, sink: sink)
        case .shuffle: return try executeShuffle(i, s, c, slot, a, sink: sink)
        default:
            s.fail("runtime.illegalOpcode")
            return sink.nothing
        }
    }

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

    static func combine<Output: GesValueOutput>(_ a: GesValue, _ b: GesValue, intersect: Bool, sink: Output) -> Output.Result {
        if a.kind == .map {
            let map = a.asMap!
            let keys: [String]
            if b.kind == .map { keys = b.asMap!.entries.map(\.key) } else if let list = b.listValue, list.allSatisfy({ $0.textValue != nil }) { keys = list.map(\.asText) } else { return sink.nothing }
            if intersect { return sink.map(map.entries.filter { entry in keys.contains { GesText.scalarEqual($0, entry.key) } }) }
            if b.kind == .map { return sink.map(map.entries + b.mapEntries!) }
            return sink.map(map.entries + keys.filter { map.get($0) == nil }.map { .init(key: $0, value: .boolean(true)) })
        }
        guard let left = a.listValue ?? a.diceRolls?.map({ .integer(Int64($0)) }), let right = b.listValue ?? b.diceRolls?.map({ .integer(Int64($0)) }) else { return sink.nothing }
        var result: [GesValue]
        if intersect {
            var used = [Bool](repeating: false, count: right.count)
            result = left.filter { value in
                if let index = right.indices.first(where: { !used[$0] && right[$0] == value }) {
                    used[index] = true
                    return true
                }
                return false
            }
        } else {
            result = left + right
        }
        return a.kind == .dice && b.kind == .dice ? sink.dice(result.map { Int32($0.asInteger) }) : sink.list(result)
    }

    static func weighted<Output: GesValueOutput>(_ source: GesValue, _ weightsValue: GesValue, count: Int, single: Bool, random: GameEventScriptRandomGenerator, sink: Output) -> Output.Result {
        guard var items = source.listValue, let weights = weightsValue.listValue, weights.count >= items.count else { return sink.nothing }
        if items.isEmpty || count <= 0 { return single ? sink.nothing : sink.list([]) }
        var weightsBuffer = weights.prefix(items.count).map(\.asNumber)
        if weightsBuffer.contains(where: { !$0.isFinite || $0 <= 0 }) { return single ? sink.nothing : sink.list([]) }
        var total = weightsBuffer.reduce(0, +)
        var result = [GesValue](repeating: .nothing, count: min(count, items.count))
        for target in result.indices {
            if total <= 0 { break }
            let threshold = random.nextFloat(0, total)
            var cumulative = 0.0
            var selected = items.count - 1
            for index in items.indices {
                cumulative += weightsBuffer[index]
                if threshold < cumulative {
                    selected = index
                    break
                }
            }
            result[target] = items.remove(at: selected)
            total -= weightsBuffer.remove(at: selected)
        }
        return single ? sink.copy(result[0]) : sink.list(result)
    }
}
