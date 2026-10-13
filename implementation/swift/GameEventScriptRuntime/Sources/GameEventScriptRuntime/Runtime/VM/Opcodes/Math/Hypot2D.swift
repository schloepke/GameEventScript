// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeHypot2D(_ target: Int, _ x: Int, _ otherRegister: Int) {
        let component0 = x
        let component1 = otherRegister
        registers.withUnsafeMutableBufferPointer { slots in
            guard slots[component0].isNumeric, slots[component0].unit == slots[x].unit else { return slots[target].setNothing() }
            let ax = slots[component0].asNumber
            guard slots[component1].isNumeric, slots[component1].unit == slots[x].unit else { return slots[target].setNothing() }
            let ay = slots[component1].asNumber
            let az = 0.0
            let squared = ax * ax + ay * ay + az * az
            return slots[target].setFloat(squared.squareRoot(), unit: slots[x].unit)
        }
    }
}
