// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeNormalize3D(_ target: Int, _ x: Int, _ otherRegister: Int, _ operandA: Int) {
        let component0 = x
        let component1 = otherRegister
        let component2 = operandA
        registers.withUnsafeMutableBufferPointer { slots in
            guard slots[component0].isNumeric, slots[component0].unit == slots[x].unit else { return slots[target].setNothing() }
            let ax = slots[component0].asNumber
            guard slots[component1].isNumeric, slots[component1].unit == slots[x].unit else { return slots[target].setNothing() }
            let ay = slots[component1].asNumber
            guard slots[component2].isNumeric, slots[component2].unit == slots[x].unit else { return slots[target].setNothing() }
            let az = slots[component2].asNumber
            let length = (ax * ax + ay * ay + az * az).squareRoot()
            guard length != 0, !length.isNaN else { return slots[target].setNothing() }
            return slots[target].setVector(x: ax / length, y: ay / length, z: az / length, unit: .none)
        }
    }
}
