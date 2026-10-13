// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeLengthSquared3D(_ target: Int, _ x: Int, _ otherRegister: Int, _ operandA: Int) {
        let component0 = x
        let component1 = otherRegister
        let component2 = operandA
        guard registers[component0].isNumeric, registers[component0].unit == registers[x].unit else { return registers[target].setNothing() }
        let ax = registers[component0].asNumber
        guard registers[component1].isNumeric, registers[component1].unit == registers[x].unit else { return registers[target].setNothing() }
        let ay = registers[component1].asNumber
        guard registers[component2].isNumeric, registers[component2].unit == registers[x].unit else { return registers[target].setNothing() }
        let az = registers[component2].asNumber
        let squared = ax * ax + ay * ay + az * az
        return registers[target].setFloat(squared)
    }
}
