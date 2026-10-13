// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeContainsValue(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            if slots[y].isRegisterData, slots[y].isNothing { return slots[target].setNothing() }
            if let entries = slots[y].mapEntries { return slots[target].setBoolean(entries.contains { $0.value == slots[x].registerValue }) }
            return slots[target].setBoolean(slots[y].spatialValue != nil && (1...3).contains { slots[y].index(Int64($0)) == slots[x].registerValue })
        }
    }
}
