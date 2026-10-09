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

extension GameEventScriptVirtualMachine {
    static func compoundIterator(_ state: GesVmState, _ instruction: GameEventScriptBytecodeInstruction, _ context: GameEventScriptContext) throws {
        let mode = instruction.unitAndFlags >> 5
        let destination = Int(instruction.word0)
        if mode == 0 {
            iterator(state, destination, state.value(Int(instruction.word1)), context)
            return
        }
        let result: GesIterator?
        if mode == 6 {
            if let map = try state.value(Int(instruction.word1)).materializedMap() { result = GesEntriesIterator(map.entries, budget: context.budget) } else { result = nil }
        } else {
            let sources = state.linked!.program.uint16IndexLists[Int(instruction.word1)].map { state.value(Int($0)) }
            result = GesCompoundSources.create(sources, mode: mode, budget: context.budget)
        }
        if let result { state.setIterator(destination, result) } else { state.setNothing(destination) }
    }

    static func nextComponents(_ state: GesVmState, _ instruction: GameEventScriptBytecodeInstruction) -> Bool {
        let iterator = state.slot(Int(instruction.word1)).iteratorValue
        let components = iterator as? any GesComponentIterator
        let item = components == nil ? iterator?.next() : nil
        let hasValue = components?.moveNext() ?? (item != nil)
        for (index, target) in state.linked!.program.uint16IndexLists[Int(instruction.word0)].enumerated() {
            var value = GesValue.nothing
            if hasValue {
                if let components {
                    value = components.component(index)
                } else if let item {
                    if let list = item.listValue {
                        value = index < list.count ? list[index] : .nothing
                    } else if item.kind == .map, let map = item.mapEntries {
                        value = index < map.count ? map[index].value : .nothing
                    } else if index == 0 {
                        value = item
                    }
                }
            }
            state.setValue(Int(target), value)
        }
        if !hasValue { state.ip = Int(instruction.word2) }
        return hasValue
    }

    static func cartesian(_ state: GesVmState, _ destination: Int, _ left: GesValue, _ right: GesValue, _ budget: GesRuntimeBudget) {
        guard let a = left.listValue, let b = right.listValue else {
            state.setNothing(destination)
            return
        }
        let (count, overflow) = a.count.multipliedReportingOverflow(by: b.count)
        guard !overflow, budget.generated(count), count <= Int32.max, count == 0 || budget.generated(2) else {
            state.setNothing(destination)
            return
        }
        var result: [GesValue] = []
        result.reserveCapacity(count)
        for first in a { for second in b { result.append(.list([first, second])) } }
        state.setValue(destination, .list(result))
    }
}
