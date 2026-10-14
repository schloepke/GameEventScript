// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

#if canImport(Darwin)
    import Darwin
#elseif canImport(Glibc)
    import Glibc
#elseif canImport(Musl)
    import Musl
#endif

extension GesVirtualMachine {
    @inline(never)
    func executeAtan2(_ target: Int, _ x: Int, _ y: Int) {
        if !registers[x].isNumeric || !registers[y].isNumeric || registers[x].unit != registers[y].unit { return registers[target].setNothing() }
        return registers[target].setFloat(registers[x].asNumber == 0 && registers[y].asNumber == 0 ? 0 : atan2(registers[x].asNumber, registers[y].asNumber))
    }
}
