// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesCompoundSources {
    static func create(_ sources: [GesValue], mode: UInt8, budget: GesRuntimeBudget) -> GesIterator? {
        if mode == 4 || mode == 5 {
            var iterators: [GesIterator] = []
            for source in sources {
                guard let iterator = GesIterator(source) else {
                    for previous in iterators { previous.close() }
                    return nil
                }
                iterators.append(iterator)
            }
            return GesProductIterator(sources, iterators: iterators, cartesian: mode == 5, budget: budget)
        }
        var value = sources[0]
        if value.kind == .map {
            for right in sources.dropFirst() {
                value = combineMap(value, right, mode: mode, budget: budget)
                if value.kind != .map || budget.isExhausted { return nil }
            }
            return GesIterator(value)
        }
        guard value.kind == .list || value.kind == .dice, var current = GesIterator(value) else { return nil }
        var kind = value.kind
        for right in sources.dropFirst() {
            let valid = right.kind == .list || right.kind == .dice || mode == 3 && (kind == .list && right.kind != .map || kind == .dice && right.kind == .integer && !right.hasUnit && right.asInteger > 0 && right.asInteger <= Int32.max)
            if !valid {
                current.close()
                return nil
            }
            if mode == 1 {
                if kind == .dice && right.kind == .dice {
                    var collected: [Int32] = []
                    while let item = current.next() {
                        guard budget.generated(collected.count + 1) else {
                            current.close()
                            return nil
                        }
                        collected.append(Int32(item.asInteger))
                    }
                    current.close()
                    let dice = right.diceRolls!
                    guard budget.generated(collected.count + dice.count) else { return nil }
                    collected.append(contentsOf: dice)
                    collected.sort(by: >)
                    current = GesIterator(.dice(collected))!
                } else if let union = current as? GesUnionIterator {
                    union.append(GesIterator(right)!)
                } else {
                    current = GesUnionIterator(current, GesIterator(right)!)
                }
            } else {
                let count = right.listValue?.count ?? right.diceRolls?.count ?? 1
                guard budget.generated(count) else {
                    current.close()
                    return nil
                }
                let values = right.listValue ?? right.diceRolls?.map { GesValue.integer(Int64($0)) } ?? [right]
                if let multiset = current as? GesMultisetIterator {
                    multiset.append(values)
                } else {
                    current = GesMultisetIterator(current, values, intersect: mode == 2, budget: budget)
                }
            }
            if right.kind == .list { kind = .list }
        }
        return current
    }

    private static func combineMap(_ left: GesValue, _ right: GesValue, mode: UInt8, budget: GesRuntimeBudget) -> GesValue {
        let map = left.mapEntries!
        let other = right.kind == .map ? right.mapEntries : nil
        let list = right.kind == .list ? right.listValue : nil
        if other == nil && list == nil && !(mode == 3 && (right.kind == .text || right.kind == .tag)) { return .nothing }
        if let list {
            for key in list {
                guard budget.loop(), key.kind == .text || key.kind == .tag else { return .nothing }
            }
        }
        var result: [GesMapEntry] = []
        for entry in map {
            guard budget.loop() else { return .nothing }
            let found = find(entry.key)
            if budget.isExhausted { return .nothing }
            if mode == 2 && found < 0 || mode == 3 && found >= 0 { continue }
            guard budget.generated(result.count + 1) else { return .nothing }
            let value = mode == 1 && other != nil && found >= 0 ? other![found].value : entry.value
            result.append(.init(key: entry.key, value: value))
        }
        if mode == 1 {
            for index in 0..<(other?.count ?? list!.count) {
                let key = other != nil ? other![index].key : list![index].textValue!
                var found = false
                for existing in result {
                    guard budget.loop() else { return .nothing }
                    if GesText.scalarEqual(key, existing.key) {
                        found = true
                        break
                    }
                }
                if found { continue }
                guard budget.generated(result.count + 1) else { return .nothing }
                result.append(.init(key: key, value: other != nil ? other![index].value : .boolean(true)))
            }
        }
        return .map(result)

        func find(_ key: String) -> Int {
            for index in 0..<(other?.count ?? list?.count ?? 1) {
                guard budget.loop() else { return -1 }
                let candidate = other != nil ? other![index].key : list != nil ? list![index].textValue! : right.textValue!
                if GesText.scalarEqual(key, candidate) { return index }
            }
            return -1
        }
    }
}
