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
    func executeAngleBetween(_ target: Int, _ x: Int, _ y: Int) {
        guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
        guard registers[x].spatialValue != nil, registers[y].spatialValue != nil else { return registers[target].setNothing() }
        let ax = registers[x].x
        let ay = registers[x].y
        let az = registers[x].z
        let bx = registers[y].x
        let by = registers[y].y
        let bz = registers[y].z
        let al = (ax * ax + ay * ay + az * az).squareRoot()
        let bl = (bx * bx + by * by + bz * bz).squareRoot()
        guard al != 0, bl != 0, !al.isNaN, !bl.isNaN else { return registers[target].setNothing() }
        var ratio = (ax * bx + ay * by + az * bz) / (al * bl)
        if ratio > 1 { ratio = 1 } else if ratio < -1 { ratio = -1 }
        return registers[target].setFloat(acos(ratio))
    }
}
