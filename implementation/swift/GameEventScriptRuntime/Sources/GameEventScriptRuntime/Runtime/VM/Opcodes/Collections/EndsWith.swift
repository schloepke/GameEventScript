// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeEndsWith(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            if slots[x].isNothing { return slots[target].setNothing() }
            if let at = slots[x].textValue, let bt = slots[y].textValue { return slots[target].setBoolean(at.unicodeScalars.reversed().starts(with: bt.unicodeScalars.reversed())) }
            guard GesCollectionOperators.sequence(slots[x].registerValue), GesCollectionOperators.sequence(slots[y].registerValue) else { return slots[target].setBoolean(false) }
            let ac = GesCollectionOperators.length(slots[x].registerValue)
            let bc = GesCollectionOperators.length(slots[y].registerValue)
            if bc > ac { return slots[target].setBoolean(false) }
            let start = ac - bc
            for index in 0..<bc { if slots[x].index(start + index + 1) != slots[y].index(index + 1) { return slots[target].setBoolean(false) } }
            return slots[target].setBoolean(true)
        }
    }
}
