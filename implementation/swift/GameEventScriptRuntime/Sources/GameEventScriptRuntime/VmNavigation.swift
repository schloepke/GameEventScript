// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

#if canImport(Darwin)
    import Darwin
#elseif canImport(Glibc)
    import Glibc
#elseif canImport(Musl)
    import Musl
#endif

enum GesNavigation {
    static func execute(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState) -> GesValue {
        let op = i.opcode
        let a = s.value(Int(i.word1))
        if [.sin, .cos, .tan].contains(op) {
            guard a.isNumeric, a.unit == .none || a.unit == .degree else { return .nothing }
            let n = a.unit == .degree ? a.asNumber / 180 * Double.pi : a.asNumber
            return .float(op == .sin ? sin(n) : op == .cos ? cos(n) : tan(n))
        }
        if [.asin, .acos, .atan].contains(op) {
            guard a.isNumeric, !a.hasUnit else { return .nothing }
            return .float(op == .asin ? asin(a.asNumber) : op == .acos ? acos(a.asNumber) : atan(a.asNumber))
        }
        if op == .lengthSquared {
            if a.isNumeric { return .float(a.asNumber * a.asNumber) }
            return a.spatialValue == nil ? .nothing : .float(a.x * a.x + a.y * a.y + a.z * a.z)
        }
        if op == .normalize { return a.spatialValue == nil ? .nothing : normalize(a.x, a.y, a.z) }
        let b = s.value(Int(i.word2))
        if op == .atan2 {
            if !a.isNumeric || !b.isNumeric || a.unit != b.unit { return .nothing }
            return .float(a.asNumber == 0 && b.asNumber == 0 ? 0 : atan2(a.asNumber, b.asNumber))
        }
        let scalarPair = (op == .distance || op == .distanceSquared) && a.isNumeric && b.isNumeric
        if [.distance, .distanceSquared, .dot, .cross, .angleBetween].contains(op) {
            guard a.unit == b.unit else { return .nothing }
            if scalarPair {
                let delta = a.asNumber - b.asNumber
                return op == .distance ? .float(Swift.abs(delta), unit: a.unit) : .float(delta * delta)
            }
            guard a.spatialValue != nil, b.spatialValue != nil else { return .nothing }
            return pair(op, a.x, a.y, a.z, b.x, b.y, b.z, unit: a.unit)
        }
        let dimension = [.hypot3D, .lengthSquared3D, .normalize3D, .distance3D, .distanceSquared3D, .dot3D, .cross3D, .angleBetween3D].contains(op) ? 3 : 2
        let single = [.hypot2D, .hypot3D, .lengthSquared2D, .lengthSquared3D, .normalize2D, .normalize3D].contains(op)
        let count = single ? dimension : dimension * 2
        let registers = [i.word1, i.word2, i.a, i.b, i.c, i.d]
        var values = [Double](repeating: 0, count: 6)
        for index in 0..<count {
            let value = s.value(Int(registers[index]))
            if !value.isNumeric || value.unit != a.unit { return .nothing }
            values[index] = value.asNumber
        }
        if single {
            let z = dimension == 3 ? values[2] : 0
            if op == .normalize2D || op == .normalize3D { return normalize(values[0], values[1], z) }
            let squared = values[0] * values[0] + values[1] * values[1] + z * z
            return op == .hypot2D || op == .hypot3D ? .float(squared.squareRoot(), unit: a.unit) : .float(squared)
        }
        return dimension == 3 ? pair(op, values[0], values[1], values[2], values[3], values[4], values[5], unit: a.unit) : pair(op, values[0], values[1], 0, values[2], values[3], 0, unit: a.unit)
    }

    private static func normalize(_ x: Double, _ y: Double, _ z: Double) -> GesValue {
        let length = (x * x + y * y + z * z).squareRoot()
        if length == 0 || length.isNaN { return .nothing }
        return .vector(x: x / length, y: y / length, z: z / length)
    }

    private static func pair(_ op: GameEventScriptBytecodeOpCode, _ ax: Double, _ ay: Double, _ az: Double, _ bx: Double, _ by: Double, _ bz: Double, unit: GesUnit) -> GesValue {
        switch op {
        case .distance, .distance2D, .distance3D, .distanceSquared, .distanceSquared2D, .distanceSquared3D:
            let dx = ax - bx
            let dy = ay - by
            let dz = az - bz
            let squared = dx * dx + dy * dy + dz * dz
            return [.distance, .distance2D, .distance3D].contains(op) ? .float(squared.squareRoot(), unit: unit) : .float(squared)
        case .dot, .dot2D, .dot3D: return .float(ax * bx + ay * by + az * bz)
        case .cross2D: return .float(ax * by - ay * bx)
        case .cross, .cross3D: return .vector(x: ay * bz - az * by, y: az * bx - ax * bz, z: ax * by - ay * bx)
        case .angleBetween, .angleBetween2D, .angleBetween3D:
            let al = (ax * ax + ay * ay + az * az).squareRoot()
            let bl = (bx * bx + by * by + bz * bz).squareRoot()
            if al == 0 || bl == 0 || al.isNaN || bl.isNaN { return .nothing }
            var ratio = (ax * bx + ay * by + az * bz) / (al * bl)
            if ratio > 1 { ratio = 1 } else if ratio < -1 { ratio = -1 }
            return .float(acos(ratio))
        default: return .nothing
        }
    }
}
