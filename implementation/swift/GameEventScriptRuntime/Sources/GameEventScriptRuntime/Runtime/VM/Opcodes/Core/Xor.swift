// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeXor(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            guard let av = slots[x].truth, let bv = slots[y].truth else { return slots[target].setNothing() }
            return slots[target].setBoolean(av != bv)
        }
    }
}
