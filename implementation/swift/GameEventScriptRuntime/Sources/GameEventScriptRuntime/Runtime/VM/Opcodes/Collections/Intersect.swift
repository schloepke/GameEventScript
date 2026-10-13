// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeIntersect(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            if slots[x].kind == .map {
                let map = slots[x].asMap!
                let keys: [String]
                if slots[y].kind == .map { keys = slots[y].asMap!.entries.map(\.key) } else if let list = slots[y].listValue, list.allSatisfy({ $0.textValue != nil }) { keys = list.map(\.asText) } else { return slots[target].setNothing() }
                return slots[target].setMap(map.entries.filter { entry in keys.contains { GesText.scalarEqual($0, entry.key) } })
            }
            guard let left = slots[x].listValue ?? slots[x].diceRolls?.map({ .integer(Int64($0)) }),
                let right = slots[y].listValue ?? slots[y].diceRolls?.map({ .integer(Int64($0)) })
            else { return slots[target].setNothing() }
            var used = [Bool](repeating: false, count: right.count)
            let result = left.filter { value in
                if let index = right.indices.first(where: { !used[$0] && right[$0] == value }) {
                    used[index] = true
                    return true
                }
                return false
            }
            return slots[x].kind == .dice && slots[y].kind == .dice ? slots[target].setDice(result.map { Int32($0.asInteger) }) : slots[target].setList(result)
        }
    }
}
