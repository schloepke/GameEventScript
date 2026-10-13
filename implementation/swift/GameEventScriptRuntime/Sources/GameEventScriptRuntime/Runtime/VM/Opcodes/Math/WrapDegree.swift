// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeWrapDegree(_ target: Int, _ x: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            if !slots[x].isNumeric || !slots[x].asNumber.isFinite || (slots[x].unit != .none && slots[x].unit != .degree) { return slots[target].setNothing() }
            var value = slots[x].asNumber.truncatingRemainder(dividingBy: 360)
            if value < 0 { value += 360 }
            return slots[target].setFloat(value == 360 ? 0 : value, unit: .degree)
        }
    }
}
