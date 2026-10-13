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
    func executeAtan2(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            if !slots[x].isNumeric || !slots[y].isNumeric || slots[x].unit != slots[y].unit { return slots[target].setNothing() }
            return slots[target].setFloat(slots[x].asNumber == 0 && slots[y].asNumber == 0 ? 0 : atan2(slots[x].asNumber, slots[y].asNumber))
        }
    }
}
