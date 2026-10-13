// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeLengthSquared(_ target: Int, _ x: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            if slots[x].isNumeric { return slots[target].setFloat(slots[x].asNumber * slots[x].asNumber) }
            return slots[x].spatialValue == nil ? slots[target].setNothing() : slots[target].setFloat(slots[x].x * slots[x].x + slots[x].y * slots[x].y + slots[x].z * slots[x].z)
        }
    }
}
