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
    func executeSin(_ target: Int, _ x: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            guard slots[x].isNumeric, slots[x].unit == .none || slots[x].unit == .degree else { return slots[target].setNothing() }
            let n = slots[x].unit == .degree ? slots[x].asNumber / 180 * Double.pi : slots[x].asNumber
            return slots[target].setFloat(sin(n))
        }
    }
}
