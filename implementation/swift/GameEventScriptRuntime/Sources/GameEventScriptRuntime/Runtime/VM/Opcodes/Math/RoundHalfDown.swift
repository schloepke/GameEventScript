// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeRoundHalfDown(_ target: Int, _ x: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            guard slots[x].isNumeric else { return slots[target].setNothing() }
            let n = slots[x].asNumber
            let truncated = n.rounded(.towardZero)
            let result = Swift.abs(n - truncated) <= 0.5 ? truncated : n.rounded(.toNearestOrAwayFromZero)
            return slots[target].setInteger(GesNumber.saturatedInteger(result))
        }
    }
}
