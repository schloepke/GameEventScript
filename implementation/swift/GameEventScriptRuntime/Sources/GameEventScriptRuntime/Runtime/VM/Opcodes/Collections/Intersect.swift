// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    @inline(never)
    func executeIntersect(_ target: Int, _ x: Int, _ y: Int) {
        if registers[x].kind == .map {
            let map = registers[x].asMap!
            let keys: [String]
            if registers[y].kind == .map {
                keys = registers[y].asMap!.entries.map(\.key)
            } else if let list = registers[y].listValue, list.allSatisfy({ $0.textValue != nil }) {
                keys = list.map(\.asText)
            } else {
                return registers[target].setNothing()
            }
            return registers[target].setMap(map.entries.filter { entry in keys.contains { GesText.scalarEqual($0, entry.key) } })
        }
        guard let left = registers[x].listValue ?? registers[x].diceRolls?.map({ .integer(Int64($0)) }),
            let right = registers[y].listValue ?? registers[y].diceRolls?.map({ .integer(Int64($0)) })
        else { return registers[target].setNothing() }
        var used = [Bool](repeating: false, count: right.count)
        let result = left.filter { value in
            if let index = right.indices.first(where: { !used[$0] && right[$0] == value }) {
                used[index] = true
                return true
            }
            return false
        }
        return registers[x].kind == .dice && registers[y].kind == .dice ? registers[target].setDice(result.map { Int32($0.asInteger) }) : registers[target].setList(result)
    }
}
