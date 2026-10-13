// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeDistanceSquared(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            guard slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
            if slots[x].isNumeric && slots[y].isNumeric {
                let delta = slots[x].asNumber - slots[y].asNumber
                return slots[target].setFloat(delta * delta)
            }
            guard slots[x].spatialValue != nil, slots[y].spatialValue != nil else { return slots[target].setNothing() }
            let ax = slots[x].x
            let ay = slots[x].y
            let az = slots[x].z
            let bx = slots[y].x
            let by = slots[y].y
            let bz = slots[y].z
            let dx = ax - bx
            let dy = ay - by
            let dz = az - bz
            let squared = dx * dx + dy * dy + dz * dz
            return slots[target].setFloat(squared)
        }
    }
}
