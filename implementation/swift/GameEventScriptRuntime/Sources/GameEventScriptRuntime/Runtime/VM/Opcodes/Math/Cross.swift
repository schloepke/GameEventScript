// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeCross(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            guard slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
            guard slots[x].spatialValue != nil, slots[y].spatialValue != nil else { return slots[target].setNothing() }
            let ax = slots[x].x
            let ay = slots[x].y
            let az = slots[x].z
            let bx = slots[y].x
            let by = slots[y].y
            let bz = slots[y].z
            return slots[target].setVector(x: ay * bz - az * by, y: az * bx - ax * bz, z: ax * by - ay * bx, unit: .none)
        }
    }
}
