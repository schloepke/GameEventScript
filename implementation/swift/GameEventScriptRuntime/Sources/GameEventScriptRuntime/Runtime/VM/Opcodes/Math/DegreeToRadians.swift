// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    @inline(never)
    func executeDegreeToRadians(_ target: Int, _ x: Int) {
        return registers[x].isNumeric && registers[x].asNumber.isFinite && (registers[x].unit == .none || registers[x].unit == .degree) ? registers[target].setFloat(registers[x].asNumber / 180 * Double.pi) : registers[target].setNothing()
    }
}
