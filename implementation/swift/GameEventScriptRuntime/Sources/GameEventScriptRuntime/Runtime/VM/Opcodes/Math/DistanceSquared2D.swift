// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeDistanceSquared2D(_ target: Int, _ x: Int, _ otherRegister: Int, _ operandA: Int, _ operandB: Int) {
        let component0 = x
        let component1 = otherRegister
        let component2 = operandA
        let component3 = operandB
        guard registers[component0].isNumeric, registers[component0].unit == registers[x].unit else { return registers[target].setNothing() }
        let ax = registers[component0].asNumber
        guard registers[component1].isNumeric, registers[component1].unit == registers[x].unit else { return registers[target].setNothing() }
        let ay = registers[component1].asNumber
        guard registers[component2].isNumeric, registers[component2].unit == registers[x].unit else { return registers[target].setNothing() }
        let bx = registers[component2].asNumber
        guard registers[component3].isNumeric, registers[component3].unit == registers[x].unit else { return registers[target].setNothing() }
        let by = registers[component3].asNumber
        let az = 0.0
        let bz = 0.0
        let dx = ax - bx
        let dy = ay - by
        let dz = az - bz
        let squared = dx * dx + dy * dy + dz * dz
        return registers[target].setFloat(squared)
    }
}
