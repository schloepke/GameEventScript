// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeOr(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            if slots[x].truth == true || slots[y].truth == true { return slots[target].setBoolean(true) }
            return slots[x].truth != nil && slots[y].truth != nil ? slots[target].setBoolean(false) : slots[target].setNothing()
        }
    }
}
