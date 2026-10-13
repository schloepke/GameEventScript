// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeNot(_ target: Int, _ x: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            guard let truth = slots[x].truth else { return slots[target].setNothing() }
            return slots[target].setBoolean(!truth)
        }
    }
}
