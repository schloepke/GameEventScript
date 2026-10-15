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
    func executeAngleBetween2D(_ target: Int, _ x: Int, _ otherRegister: Int, _ operandA: Int, _ operandB: Int) {
        let component0 = x
        let component1 = otherRegister
        let component2 = operandA
        let component3 = operandB
        guard registers[component0].isNumeric, registers[component0].unit == registers[x].unit else { return registers[target].setNothing() }
        let ax = registers[component0].asNumber
        guard registers[component1].isNumeric, registers[component1].unit == registers[x].unit else { return registers[target].setNothing() }
        let ay = registers[component1].asNumber
        guard registers[component2].isNumeric, registers[component2].unit == registers[x].unit else { return registers[target].setNothing() }
        let bx = registers[component2].asNumber
        guard registers[component3].isNumeric, registers[component3].unit == registers[x].unit else { return registers[target].setNothing() }
        let by = registers[component3].asNumber
        let az = 0.0
        let bz = 0.0
        let al = (ax * ax + ay * ay + az * az).squareRoot()
        let bl = (bx * bx + by * by + bz * bz).squareRoot()
        guard al != 0, bl != 0, !al.isNaN, !bl.isNaN else { return registers[target].setNothing() }
        var ratio = (ax * bx + ay * by + az * bz) / (al * bl)
        if ratio > 1 { ratio = 1 } else if ratio < -1 { ratio = -1 }
        return registers[target].setFloat(acos(ratio))
    }
}
