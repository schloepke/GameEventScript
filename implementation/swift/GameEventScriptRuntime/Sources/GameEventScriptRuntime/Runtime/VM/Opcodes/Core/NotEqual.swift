// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeNotEqual(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            if slots[x].isNothing || slots[y].isNothing { return slots[target].setNothing() }
            return slots[target].setBoolean(GesComparison.equal(slots[x].registerValue, slots[y].registerValue) == false)
        }
    }
}
