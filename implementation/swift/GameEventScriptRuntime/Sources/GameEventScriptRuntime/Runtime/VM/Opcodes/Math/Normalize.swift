// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeNormalize(_ target: Int, _ x: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            guard slots[x].spatialValue != nil else { return slots[target].setNothing() }
            let ax = slots[x].x
            let ay = slots[x].y
            let az = slots[x].z
            let length = (ax * ax + ay * ay + az * az).squareRoot()
            guard length != 0, !length.isNaN else { return slots[target].setNothing() }
            return slots[target].setVector(x: ax / length, y: ay / length, z: az / length, unit: .none)
        }
    }
}
