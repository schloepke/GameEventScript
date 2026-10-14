// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    @inline(never)
    func executeDegreeFromRadians(_ target: Int, _ x: Int) {
        return registers[x].isNumeric && registers[x].asNumber.isFinite && !registers[x].hasUnit ? registers[target].setFloat(registers[x].asNumber / Double.pi * 180, unit: .degree) : registers[target].setNothing()
    }
}
