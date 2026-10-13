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
    func executeCos(_ target: Int, _ x: Int) {
        guard registers[x].isNumeric, registers[x].unit == .none || registers[x].unit == .degree else { return registers[target].setNothing() }
        let n = registers[x].unit == .degree ? registers[x].asNumber / 180 * Double.pi : registers[x].asNumber
        return registers[target].setFloat(cos(n))
    }
}
