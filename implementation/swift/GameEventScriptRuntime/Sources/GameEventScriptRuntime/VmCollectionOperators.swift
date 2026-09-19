// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesCollectionOperators {
    static func execute(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext) throws
        -> GesValue
    {
        let op = i.opcode
        let slot = s.slot(Int(i.word1))
        let a = slot.value
        switch op {
        case .takeFirst, .takeLast, .takeHighest, .takeLowest, .dropFirst, .dropLast, .dropHighest, .dropLowest,
            .oneRandom, .takeRandom:
            return GesTakeDrop.execute(op, slot, count: Int(i.signedWord2), random: c.random)
        case .oneWeighted, .takeWeighted:
            return weighted(
                a, s.value(Int(op == .oneWeighted ? i.word2 : i.a)), count: op == .oneWeighted ? 1 : Int(i.signedWord2),
                single: op == .oneWeighted, random: c.random)
        case .hasPattern, .takePattern:
            return GesPatterns.execute(
                slot, pattern: GameEventScriptBytecodePatternKind(rawValue: i.a)!, count: Int(i.signedWord2),
                face: i.a == 1 ? s.value(Int(i.b)) : .nothing, take: op == .takePattern)
        case .count:
            if case .iterator(let iterator) = slot {
                defer { iterator.close() }
                var count: Int64 = 0
                while iterator.next() != nil { count += 1 }
                return .integer(count)
            }
            if a.isNothing { return .integer(0) }
            if a.kind == .series || a.kind == .external || GesIterator(a) == nil { return .nothing }
            return .integer(length(a))
        case .first, .last, .single:
            if case .iterator(let iterator) = slot {
                defer { iterator.close() }
                guard let first = iterator.next() else { return .nothing }
                if op == .first { return first }
                if op == .single { return iterator.next() == nil ? first : .nothing }
                var last = first
                while let value = iterator.next() { last = value }
                return last
            }
            guard let iterator = GesIterator(a) else { return .nothing }
            if op == .first { return iterator.next() ?? .nothing }
            if op == .single { return length(a) == 1 ? iterator.next() ?? .nothing : .nothing }
            if let values = a.mapEntries { return values.last?.value ?? .nothing }
            if a.spatialValue != nil { return .float(a.z) }
            return a.index(length(a))
        case .keysOfMap, .valuesOfMap, .entriesOfMap:
            guard let map = try a.asMap ?? a.externalMap() else { return .nothing }
            if op == .keysOfMap { return .list(map.keys) }
            if op == .valuesOfMap { return .list(map.values) }
            return .list(
                map.entries.map {
                    .map([.init(key: "key", value: .text($0.key)), .init(key: "value", value: $0.value)])
                })
        case .hasAny, .hasAll:
            let all = op == .hasAll
            let iterator: GesIterator?
            if case .iterator(let source) = slot { iterator = source } else { iterator = GesIterator(a) }
            if a.isNothing, case .value = slot { return .nothing }
            guard let iterator else { return .boolean(false) }
            defer { iterator.close() }
            while let value = iterator.next() {
                let truth = a.spatialValue != nil ? value.asNumber.isFinite && value.asNumber != 0 : value.asBoolean
                if truth != all { return .boolean(!all) }
            }
            return .boolean(all)
        case .contains, .containsValue, .containsAny, .containsAll:
            let right = s.slot(Int(i.word2))
            let b = right.value
            if case .value = right, b.isNothing { return .nothing }
            if op == .containsValue {
                if let entries = b.mapEntries { return .boolean(entries.contains { $0.value == a }) }
                return .boolean(b.spatialValue != nil && (1...3).contains { b.index(Int64($0)) == a })
            }
            if op == .contains { return .boolean(contains(a, right)) }
            let all = op == .containsAll
            let candidates =
                [.list, .dice, .text, .tag, .integerRange, .floatRange].contains(a.kind) ? GesIterator(a) : nil
            guard let candidates else { return .boolean(all) }
            defer { candidates.close() }
            let container: GesVmState.Slot
            if case .iterator(let iterator) = right {
                container = .value(.list(read(iterator)))
            } else {
                container = right
            }
            while let candidate = candidates.next() {
                if contains(candidate, container) != all { return .boolean(!all) }
            }
            return .boolean(all)
        case .startsWith, .endsWith:
            let b = s.value(Int(i.word2))
            if a.isNothing { return .nothing }
            if let at = a.textValue, let bt = b.textValue {
                return .boolean(
                    op == .startsWith
                        ? at.unicodeScalars.starts(with: bt.unicodeScalars)
                        : at.unicodeScalars.reversed().starts(with: bt.unicodeScalars.reversed()))
            }
            guard sequence(a), sequence(b) else { return .boolean(false) }
            let ac = length(a)
            let bc = length(b)
            if bc > ac { return .boolean(false) }
            let start = op == .startsWith ? 0 : ac - bc
            for index in 0..<bc { if a.index(start + index + 1) != b.index(index + 1) { return .boolean(false) } }
            return .boolean(true)
        case .union, .intersect: return combine(a, s.value(Int(i.word2)), intersect: op == .intersect)
        case .zip:
            guard let a = a.listValue, let b = s.value(Int(i.word2)).listValue else { return .nothing }
            return .list(zip(a, b).map { .map([.init(key: "left", value: $0.0), .init(key: "right", value: $0.1)]) })
        case .distinct:
            guard let values = materialize(slot, ranges: false) else { return .nothing }
            var seen = Set<GesValue>()
            var result: [GesValue] = []
            for value in values where seen.insert(value).inserted { result.append(value) }
            return a.kind == .dice ? .dice(result.map { Int32($0.asInteger) }) : .list(result)
        case .sortAscending, .sortDescending, .reverse:
            if a.integerRangeValue != nil || a.floatRangeValue != nil {
                let descending = (a.integerRangeValue?.step ?? 0) < 0 || (a.floatRangeValue?.step ?? 0) < 0
                if op != .reverse && descending == (op == .sortDescending) { return a }
                return reverseRange(a)
            }
            guard let values = materialize(slot, ranges: false) else { return .nothing }
            if op == .reverse { return .list(values.reversed()) }
            return GesComparison.sorted(values, descending: op == .sortDescending).map(GesValue.list) ?? .nothing
        case .shuffle:
            guard var values = materialize(slot) else { return .nothing }
            if values.count > 1 {
                for index in stride(from: values.count - 1, through: 1, by: -1) {
                    values.swapAt(index, Int(c.random.nextInclusiveInteger(0, Int64(index))))
                }
            }
            return .list(values)
        default:
            s.fail("runtime.illegalOpcode")
            return .nothing
        }
    }
    static func length(_ value: GesValue) -> Int64 {
        value.integerRangeValue?.count ?? value.floatRangeValue?.count
            ?? (value.spatialValue != nil ? 3 : Int64(value.length))
    }
    static func sequence(_ value: GesValue) -> Bool { [.list, .dice, .integerRange, .floatRange].contains(value.kind) }
    static func materialize(_ slot: GesVmState.Slot, ranges: Bool = true) -> [GesValue]? {
        if case .iterator(let iterator) = slot { return read(iterator) }
        let value = slot.value
        if let list = value.listValue { return list }
        if let dice = value.diceRolls { return dice.map { .integer(Int64($0)) } }
        if ranges && (value.integerRangeValue != nil || value.floatRangeValue != nil) && length(value) <= Int32.max {
            return read(GesIterator(value)!)
        }
        return nil
    }
    static func read(_ iterator: GesIterator) -> [GesValue] {
        defer { iterator.close() }
        var values: [GesValue] = []
        while let value = iterator.next() { values.append(value) }
        return values
    }
    static func contains(_ needle: GesValue, _ slot: GesVmState.Slot) -> Bool {
        if case .iterator(let iterator) = slot {
            defer { iterator.close() }
            while let value = iterator.next() { if value == needle { return true } }
            return false
        }
        let value = slot.value
        if let text = value.textValue {
            guard let part = needle.textValue else { return false }
            let a = Array(text.unicodeScalars)
            let b = Array(part.unicodeScalars)
            if b.isEmpty { return true }
            if b.count > a.count { return false }
            return (0...(a.count - b.count)).contains { a[$0..<($0 + b.count)].elementsEqual(b) }
        }
        if let list = value.listValue { return list.contains(needle) }
        if let dice = value.diceRolls {
            return needle.kind == .integer && !needle.hasUnit && dice.contains { Int64($0) == needle.asInteger }
        }
        if value.kind == .map { return needle.textValue.flatMap { value.asMap!.get($0) } != nil }
        if value.spatialValue != nil {
            return needle.isNumeric && (1...3).contains { value.index(Int64($0)) == needle }
        }
        if let range = value.integerRangeValue {
            return needle.kind == .integer && !needle.hasUnit && range.contains(needle.asInteger)
        }
        if let range = value.floatRangeValue {
            if needle.hasUnit || (needle.kind != .integer && needle.kind != .float) { return false }
            if let integer = needle.integerValue, GesNumber.exactInteger(Double(integer)) != integer { return false }
            return range.contains(needle.asNumber)
        }
        return false
    }
    static func reverseRange(_ value: GesValue) -> GesValue {
        if let range = value.integerRangeValue {
            guard let last = range.term(at: range.count) else { return .integerRange(from: 0, to: 0, step: 0) }
            return .integerRange(from: last, to: range.from, step: 0 &- range.step)
        }
        let range = value.floatRangeValue!
        guard let last = range.term(at: range.count) else { return .integerRange(from: 0, to: 0, step: 0) }
        return .floatRange(from: last, to: range.from, step: -range.step)
    }
    private static func combine(_ a: GesValue, _ b: GesValue, intersect: Bool) -> GesValue {
        if a.kind == .map {
            let map = a.asMap!
            let keys: [String]
            if b.kind == .map {
                keys = b.asMap!.entries.map(\.key)
            } else if let list = b.listValue, list.allSatisfy({ $0.textValue != nil }) {
                keys = list.map(\.asText)
            } else {
                return .nothing
            }
            if intersect {
                return .map(map.entries.filter { entry in keys.contains { GesText.scalarEqual($0, entry.key) } })
            }
            if b.kind == .map { return .map(map.entries + b.mapEntries!) }
            return .map(map.entries + keys.filter { map.get($0) == nil }.map { .init(key: $0, value: .boolean(true)) })
        }
        guard let left = a.listValue ?? a.diceRolls?.map({ .integer(Int64($0)) }),
            let right = b.listValue ?? b.diceRolls?.map({ .integer(Int64($0)) })
        else { return .nothing }
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
        return a.kind == .dice && b.kind == .dice ? .dice(result.map { Int32($0.asInteger) }) : .list(result)
    }
    private static func weighted(
        _ source: GesValue, _ weightsValue: GesValue, count: Int, single: Bool, random: GameEventScriptRandomGenerator
    ) -> GesValue {
        guard var items = source.listValue, let weights = weightsValue.listValue, weights.count >= items.count else {
            return .nothing
        }
        if items.isEmpty || count <= 0 { return single ? .nothing : .list([]) }
        var weightsBuffer = weights.prefix(items.count).map(\.asNumber)
        if weightsBuffer.contains(where: { !$0.isFinite || $0 <= 0 }) { return single ? .nothing : .list([]) }
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
        return single ? result[0] : .list(result)
    }
}
