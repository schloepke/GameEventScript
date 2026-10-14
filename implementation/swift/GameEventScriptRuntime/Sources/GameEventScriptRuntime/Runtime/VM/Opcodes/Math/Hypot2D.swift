// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    @inline(never)
    func executeHypot2D(_ target: Int, _ x: Int, _ otherRegister: Int) {
        let component0 = x
        let component1 = otherRegister
        guard registers[component0].isNumeric, registers[component0].unit == registers[x].unit else { return registers[target].setNothing() }
        let ax = registers[component0].asNumber
        guard registers[component1].isNumeric, registers[component1].unit == registers[x].unit else { return registers[target].setNothing() }
        let ay = registers[component1].asNumber
        let az = 0.0
        let squared = ax * ax + ay * ay + az * az
        return registers[target].setFloat(squared.squareRoot(), unit: registers[x].unit)
    }
}
