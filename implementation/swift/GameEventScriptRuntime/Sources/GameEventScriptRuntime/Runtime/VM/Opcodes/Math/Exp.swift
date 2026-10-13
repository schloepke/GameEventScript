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
        return registers[x].hasUnit ? registers[target].setNothing() : registers[target].setFloat(exp(registers[x].asNumber))
    }
}
