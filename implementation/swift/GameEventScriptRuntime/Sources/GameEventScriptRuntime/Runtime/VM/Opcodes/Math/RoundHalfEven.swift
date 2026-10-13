// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeRoundHalfEven(_ target: Int, _ x: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            guard slots[x].isNumeric else { return slots[target].setNothing() }
            let n = slots[x].asNumber
            let result = n.rounded(.toNearestOrEven)
            return slots[target].setInteger(GesNumber.saturatedInteger(result))
        }
    }
}
