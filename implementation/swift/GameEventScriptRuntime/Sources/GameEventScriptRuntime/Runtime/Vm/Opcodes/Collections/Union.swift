// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeUnion(_ target: Int, _ x: Int, _ y: Int) {
        if registers[x].kind == .map {
            let map = registers[x].asMap!
            let keys: [String]
            if registers[y].kind == .map {
                keys = registers[y].asMap!.keyStorage
            } else if let list = registers[y].listValue, list.allSatisfy({ $0.textValue != nil }) {
                keys = list.map(\.asText)
            } else {
                return registers[target].setNothing()
            }
            if registers[y].kind == .map { return registers[target].setMap(map.merged(with: registers[y].asMap!)) }
            return registers[target].setMap(map.merged(with: GesValueMap(keys.filter { map.get($0) == nil }.map { .init(key: $0, value: .boolean(true)) })))
        }
        guard let left = registers[x].listValue ?? registers[x].diceRolls?.map({ .integer(Int64($0)) }),
            let right = registers[y].listValue ?? registers[y].diceRolls?.map({ .integer(Int64($0)) })
        else { return registers[target].setNothing() }
        let result = left + right
        return registers[x].kind == .dice && registers[y].kind == .dice ? registers[target].setDice(result.map { Int32($0.asInteger) }) : registers[target].setList(result)
    }
}
