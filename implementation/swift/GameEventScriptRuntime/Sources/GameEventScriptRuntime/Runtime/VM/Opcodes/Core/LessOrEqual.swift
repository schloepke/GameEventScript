// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeLessOrEqual(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            if slots[x].isNothing || slots[y].isNothing { return slots[target].setNothing() }
            if !slots[x].isNumeric || !slots[y].isNumeric || slots[x].unit != slots[y].unit { return slots[target].setBoolean(false) }
            switch slots[x].kind {
            case .integer where slots[y].kind == .integer:
                let av = slots[x].integerValue!
                let bv = slots[y].integerValue!
                return slots[target].setBoolean(av <= bv)
            default: break
            }
            return slots[target].setBoolean(slots[x].asNumber <= slots[y].asNumber)
        }
    }
}
