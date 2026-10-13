// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeCross3D(_ target: Int, _ x: Int, _ otherRegister: Int, _ operandA: Int, _ operandB: Int, _ operandC: Int, _ operandD: Int) {
        let component0 = x
        let component1 = otherRegister
        let component2 = operandA
        let component3 = operandB
        let component4 = operandC
        let component5 = operandD
        registers.withUnsafeMutableBufferPointer { slots in
            guard slots[component0].isNumeric, slots[component0].unit == slots[x].unit else { return slots[target].setNothing() }
            let ax = slots[component0].asNumber
            guard slots[component1].isNumeric, slots[component1].unit == slots[x].unit else { return slots[target].setNothing() }
            let ay = slots[component1].asNumber
            guard slots[component2].isNumeric, slots[component2].unit == slots[x].unit else { return slots[target].setNothing() }
            let az = slots[component2].asNumber
            guard slots[component3].isNumeric, slots[component3].unit == slots[x].unit else { return slots[target].setNothing() }
            let bx = slots[component3].asNumber
            guard slots[component4].isNumeric, slots[component4].unit == slots[x].unit else { return slots[target].setNothing() }
            let by = slots[component4].asNumber
            guard slots[component5].isNumeric, slots[component5].unit == slots[x].unit else { return slots[target].setNothing() }
            let bz = slots[component5].asNumber
            return slots[target].setVector(x: ay * bz - az * by, y: az * bx - ax * bz, z: ax * by - ay * bx, unit: .none)
        }
    }
}
