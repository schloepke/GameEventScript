// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeContainsValue(_ target: Int, _ x: Int, _ y: Int) {
        if registers[y].isRegisterData, registers[y].isNothing { return registers[target].setNothing() }
        if let entries = registers[y].mapEntries { return registers[target].setBoolean(entries.contains { $0.value == registers[x].registerValue }) }
        return registers[target].setBoolean(registers[y].spatialValue != nil && (1...3).contains { registers[y].index(Int64($0)) == registers[x].registerValue })
    }
}
