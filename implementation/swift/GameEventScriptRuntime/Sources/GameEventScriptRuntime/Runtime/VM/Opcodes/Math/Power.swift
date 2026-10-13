// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

#if canImport(Darwin)
    import Darwin
#elseif canImport(Glibc)
    import Glibc
#elseif canImport(Musl)
    import Musl
#endif

extension GesVmState {
    @inline(never)
    func executePower(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            if slots[x].isNothing || slots[y].isNothing { return slots[target].setNothing() }
            let left = slots[x].asNumber
            let right = slots[y].asNumber
            if slots[y].hasUnit || left.isNaN || right.isNaN || (slots[x].hasUnit && right != 0 && right != 1) { return slots[target].setNothing() }
            return slots[target].setFloat(pow(left, right), unit: right == 1 ? slots[x].unit : .none)
        }
    }

    @inline(never)
    func executePowerNumeric(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            guard slots[x].isNumeric, slots[y].isNumeric else { return slots[target].setNothing() }
            let left = slots[x].asNumber
            let right = slots[y].asNumber
            if slots[y].hasUnit || left.isNaN || right.isNaN || (slots[x].hasUnit && right != 0 && right != 1) { return slots[target].setNothing() }
            return slots[target].setFloat(pow(left, right), unit: right == 1 ? slots[x].unit : .none)
        }
    }
}
