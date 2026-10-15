// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    // Integer operands are the hot case and run inline in dispatch; every other
    // combination, including a zero divisor, takes the general body.
    // Inlining this fast path is measured: without it the performance.core/case-0005 run is about 2% slower.
    @inline(__always)
    func executeModulo(_ target: Int, _ x: Int, _ y: Int) {
        guard registers[x].kind == .integer, registers[y].kind == .integer, registers[x].unit == registers[y].unit, let bv = registers[y].integerValue, bv != 0 else { return executeModuloGeneral(target, x, y) }
        let av = registers[x].integerValue!
        let unit = registers[x].unit
        var result = av == .min && bv == -1 ? 0 : av % bv
        if result != 0 && (result < 0) != (bv < 0) { result += bv }
        registers[target].setInteger(result, unit: unit)
    }

    func executeModuloGeneral(_ target: Int, _ x: Int, _ y: Int) {
        if registers[x].kind == .integer && registers[y].kind == .integer {
            let av = registers[x].integerValue!
            let bv = registers[y].integerValue!
            guard registers[x].unit == registers[y].unit else {
                registers[target].setNothing()
                return
            }
            let unit = registers[x].unit
            guard bv != 0 else {
                registers[target].setNothing()
                return
            }
            var result = av == .min && bv == -1 ? 0 : av % bv
            if result != 0 && (result < 0) != (bv < 0) { result += bv }
            registers[target].setInteger(result, unit: unit)
            return
        }
        if registers[x].isNothing || registers[y].isNothing { return registers[target].setNothing() }
        switch registers[x].kind {
        case .float where registers[y].kind == .float:
            guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit: GesQuantityUnit = registers[x].unit
            let left = registers[x].floatValue!
            let right = registers[y].floatValue!
            guard left.isFinite, !right.isNaN, right != 0 else { return registers[target].setNothing() }
            var result = right.isInfinite ? left : left.truncatingRemainder(dividingBy: right)
            if right.isFinite && result != 0 && (result < 0) != (right < 0) { result += right }
            return registers[target].setFloat(result, unit: unit)
        case .integer where registers[y].kind == .float:
            guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit: GesQuantityUnit = registers[x].unit
            let left = Double(registers[x].integerValue!)
            let right = registers[y].floatValue!
            guard left.isFinite, !right.isNaN, right != 0 else { return registers[target].setNothing() }
            var result = right.isInfinite ? left : left.truncatingRemainder(dividingBy: right)
            if right.isFinite && result != 0 && (result < 0) != (right < 0) { result += right }
            return registers[target].setFloat(result, unit: unit)
        case .float where registers[y].kind == .integer:
            guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit: GesQuantityUnit = registers[x].unit
            let left = registers[x].floatValue!
            let right = Double(registers[y].integerValue!)
            guard left.isFinite, !right.isNaN, right != 0 else { return registers[target].setNothing() }
            var result = right.isInfinite ? left : left.truncatingRemainder(dividingBy: right)
            if right.isFinite && result != 0 && (result < 0) != (right < 0) { result += right }
            return registers[target].setFloat(result, unit: unit)
        default: break
        }
        guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
        let unit = registers[x].unit
        if registers[x].spatialValue != nil || registers[y].spatialValue != nil { return registers[target].setNothing() }
        let left = registers[x].asNumber
        let right = registers[y].asNumber
        guard left.isFinite, !right.isNaN, right != 0 else { return registers[target].setNothing() }
        var result = right.isInfinite ? left : left.truncatingRemainder(dividingBy: right)
        if right.isFinite && result != 0 && (result < 0) != (right < 0) { result += right }
        return registers[target].setFloat(result, unit: unit)
    }

    func executeModuloNumeric(_ target: Int, _ x: Int, _ y: Int) {
        guard registers[x].isNumeric, registers[y].isNumeric else { return registers[target].setNothing() }
        if let av = registers[x].numericIntegerValue, let bv = registers[y].numericIntegerValue {
            guard registers[x].unit == registers[y].unit else {
                registers[target].setNothing()
                return
            }
            let unit = registers[x].unit
            guard bv != 0 else {
                registers[target].setNothing()
                return
            }
            var result = av == .min && bv == -1 ? 0 : av % bv
            if result != 0 && (result < 0) != (bv < 0) { result += bv }
            registers[target].setInteger(result, unit: unit)
            return
        }
        guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
        let unit = registers[x].unit
        let left = registers[x].asNumber
        let right = registers[y].asNumber
        guard left.isFinite, !right.isNaN, right != 0 else { return registers[target].setNothing() }
        var result = right.isInfinite ? left : left.truncatingRemainder(dividingBy: right)
        if right.isFinite && result != 0 && (result < 0) != (right < 0) { result += right }
        return registers[target].setFloat(result, unit: unit)
    }
}
