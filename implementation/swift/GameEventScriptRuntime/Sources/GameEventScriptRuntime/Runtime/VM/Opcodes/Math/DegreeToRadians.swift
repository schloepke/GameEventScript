// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeDegreeToRadians(_ target: Int, _ x: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            return slots[x].isNumeric && slots[x].asNumber.isFinite && (slots[x].unit == .none || slots[x].unit == .degree) ? slots[target].setFloat(slots[x].asNumber / 180 * Double.pi) : slots[target].setNothing()
        }
    }
}
