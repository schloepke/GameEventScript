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
    func executeExp(_ target: Int, _ x: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            return slots[x].hasUnit ? slots[target].setNothing() : slots[target].setFloat(exp(slots[x].asNumber))
        }
    }
}
