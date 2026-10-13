// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeAnd(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            if slots[x].truth == false || slots[y].truth == false { return slots[target].setBoolean(false) }
            return slots[x].truth != nil && slots[y].truth != nil ? slots[target].setBoolean(true) : slots[target].setNothing()
        }
    }
}
