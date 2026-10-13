// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeDistance3D(_ target: Int, _ x: Int, _ otherRegister: Int, _ operandA: Int, _ operandB: Int, _ operandC: Int, _ operandD: Int) {
        let component0 = x
        let component1 = otherRegister
        let component2 = operandA
        let component3 = operandB
        let component4 = operandC
        let component5 = operandD
        guard registers[component0].isNumeric, registers[component0].unit == registers[x].unit else { return registers[target].setNothing() }
        let ax = registers[component0].asNumber
        guard registers[component1].isNumeric, registers[component1].unit == registers[x].unit else { return registers[target].setNothing() }
        let ay = registers[component1].asNumber
        guard registers[component2].isNumeric, registers[component2].unit == registers[x].unit else { return registers[target].setNothing() }
        let az = registers[component2].asNumber
        guard registers[component3].isNumeric, registers[component3].unit == registers[x].unit else { return registers[target].setNothing() }
        let bx = registers[component3].asNumber
        guard registers[component4].isNumeric, registers[component4].unit == registers[x].unit else { return registers[target].setNothing() }
        let by = registers[component4].asNumber
        guard registers[component5].isNumeric, registers[component5].unit == registers[x].unit else { return registers[target].setNothing() }
        let bz = registers[component5].asNumber
        let dx = ax - bx
        let dy = ay - by
        let dz = az - bz
        let squared = dx * dx + dy * dy + dz * dz
        return registers[target].setFloat(squared.squareRoot(), unit: registers[x].unit)
    }
}
