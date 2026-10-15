// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeRemainder(_ target: Int, _ x: Int, _ y: Int) {
        if registers[x].isNothing || registers[y].isNothing { return registers[target].setNothing() }
        switch registers[x].kind {
        case .integer where registers[y].kind == .integer:
            let av = registers[x].integerValue!
            let bv = registers[y].integerValue!
            guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit = registers[x].unit
            guard bv != 0 else { return registers[target].setNothing() }
            let result = av == .min && bv == -1 ? 0 : av % bv
            return registers[target].setInteger(result, unit: unit)
        case .float where registers[y].kind == .float:
            guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit: GesQuantityUnit = registers[x].unit
            let left = registers[x].floatValue!
            let right = registers[y].floatValue!
            guard left.isFinite, !right.isNaN, right != 0 else { return registers[target].setNothing() }
            let result = right.isInfinite ? left : left.truncatingRemainder(dividingBy: right)
            return registers[target].setFloat(result, unit: unit)
        case .integer where registers[y].kind == .float:
            guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit: GesQuantityUnit = registers[x].unit
            let left = Double(registers[x].integerValue!)
            let right = registers[y].floatValue!
            guard left.isFinite, !right.isNaN, right != 0 else { return registers[target].setNothing() }
            let result = right.isInfinite ? left : left.truncatingRemainder(dividingBy: right)
            return registers[target].setFloat(result, unit: unit)
        case .float where registers[y].kind == .integer:
            guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit: GesQuantityUnit = registers[x].unit
            let left = registers[x].floatValue!
            let right = Double(registers[y].integerValue!)
            guard left.isFinite, !right.isNaN, right != 0 else { return registers[target].setNothing() }
            let result = right.isInfinite ? left : left.truncatingRemainder(dividingBy: right)
            return registers[target].setFloat(result, unit: unit)
        default: break
        }
        guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
        let unit = registers[x].unit
        if registers[x].spatialValue != nil || registers[y].spatialValue != nil { return registers[target].setNothing() }
        let left = registers[x].asNumber
        let right = registers[y].asNumber
        guard left.isFinite, !right.isNaN, right != 0 else { return registers[target].setNothing() }
        let result = right.isInfinite ? left : left.truncatingRemainder(dividingBy: right)
        return registers[target].setFloat(result, unit: unit)
    }

    func executeRemainderNumeric(_ target: Int, _ x: Int, _ y: Int) {
        guard registers[x].isNumeric, registers[y].isNumeric else { return registers[target].setNothing() }
        if let av = registers[x].numericIntegerValue, let bv = registers[y].numericIntegerValue {
            guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit = registers[x].unit
            guard bv != 0 else { return registers[target].setNothing() }
            let result = av == .min && bv == -1 ? 0 : av % bv
            return registers[target].setInteger(result, unit: unit)
        }
        guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
        let unit = registers[x].unit
        let left = registers[x].asNumber
        let right = registers[y].asNumber
        guard left.isFinite, !right.isNaN, right != 0 else { return registers[target].setNothing() }
        let result = right.isInfinite ? left : left.truncatingRemainder(dividingBy: right)
        return registers[target].setFloat(result, unit: unit)
    }
}
