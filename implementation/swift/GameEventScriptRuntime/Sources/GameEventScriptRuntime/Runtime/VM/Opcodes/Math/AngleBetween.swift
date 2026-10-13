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
    func executeAngleBetween(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            guard slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
            guard slots[x].spatialValue != nil, slots[y].spatialValue != nil else { return slots[target].setNothing() }
            let ax = slots[x].x
            let ay = slots[x].y
            let az = slots[x].z
            let bx = slots[y].x
            let by = slots[y].y
            let bz = slots[y].z
            let al = (ax * ax + ay * ay + az * az).squareRoot()
            let bl = (bx * bx + by * by + bz * bz).squareRoot()
            guard al != 0, bl != 0, !al.isNaN, !bl.isNaN else { return slots[target].setNothing() }
            var ratio = (ax * bx + ay * by + az * bz) / (al * bl)
            if ratio > 1 { ratio = 1 } else if ratio < -1 { ratio = -1 }
            return slots[target].setFloat(acos(ratio))
        }
    }
}
