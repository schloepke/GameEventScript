// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeUnion(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            if slots[x].kind == .map {
                let map = slots[x].asMap!
                let keys: [String]
                if slots[y].kind == .map { keys = slots[y].asMap!.entries.map(\.key) } else if let list = slots[y].listValue, list.allSatisfy({ $0.textValue != nil }) { keys = list.map(\.asText) } else { return slots[target].setNothing() }
                if slots[y].kind == .map { return slots[target].setMap(map.entries + slots[y].mapEntries!) }
                return slots[target].setMap(map.entries + keys.filter { map.get($0) == nil }.map { .init(key: $0, value: .boolean(true)) })
            }
            guard let left = slots[x].listValue ?? slots[x].diceRolls?.map({ .integer(Int64($0)) }),
                let right = slots[y].listValue ?? slots[y].diceRolls?.map({ .integer(Int64($0)) })
            else { return slots[target].setNothing() }
            let result = left + right
            return slots[x].kind == .dice && slots[y].kind == .dice ? slots[target].setDice(result.map { Int32($0.asInteger) }) : slots[target].setList(result)
        }
    }
}
