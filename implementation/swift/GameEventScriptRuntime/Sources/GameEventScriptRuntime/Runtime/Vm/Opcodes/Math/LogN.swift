// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

#if canImport(Darwin)
    import Darwin
#elseif canImport(Glibc)
    import Glibc
#elseif canImport(Musl)
    import Musl
#elseif canImport(Android)
    import Android
#elseif canImport(WASILibc)
    import WASILibc
#endif

extension GesVirtualMachine {
    func executeLogN(_ target: Int, _ x: Int) {
        return registers[x].hasUnit ? registers[target].setNothing() : registers[target].setFloat(log(registers[x].asNumber))
    }
}
