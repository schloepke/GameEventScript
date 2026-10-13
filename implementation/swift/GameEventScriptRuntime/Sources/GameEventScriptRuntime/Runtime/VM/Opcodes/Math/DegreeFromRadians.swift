// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeDegreeFromRadians(_ target: Int, _ x: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            return slots[x].isNumeric && slots[x].asNumber.isFinite && !slots[x].hasUnit ? slots[target].setFloat(slots[x].asNumber / Double.pi * 180, unit: .degree) : slots[target].setNothing()
        }
    }
}
