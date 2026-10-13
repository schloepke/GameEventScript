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
    func executeAcos(_ target: Int, _ x: Int) {
        guard registers[x].isNumeric, !registers[x].hasUnit else { return registers[target].setNothing() }
        return registers[target].setFloat(acos(registers[x].asNumber))
    }
}
