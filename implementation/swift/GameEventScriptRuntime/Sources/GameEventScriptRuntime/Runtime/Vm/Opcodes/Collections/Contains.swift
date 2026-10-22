// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeContains(_ target: Int, _ x: Int, _ y: Int) {
        if registers[y].isRegisterData, registers[y].isNothing { return registers[target].setNothing() }
        return registers[target].setBoolean(GesCollectionOperators.contains(registers[x].registerValue, registers[y]))
    }
}
