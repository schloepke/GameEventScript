// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeEqual(_ target: Int, _ x: Int, _ y: Int) {
        if registers[x].isNothing || registers[y].isNothing { return registers[target].setNothing() }
        return registers[target].setBoolean(GesComparison.equal(registers[x].registerValue, registers[y].registerValue) == true)
    }
}
