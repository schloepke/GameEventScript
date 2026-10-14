// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    // Integer operands are the hot case and run inline in dispatch; every other
    // combination, including overflow, takes the complete out-of-line body.
    @inline(__always)
    func executeMultiply(_ target: Int, _ x: Int, _ y: Int) {
        guard registers[x].kind == .integer, registers[y].kind == .integer, !registers[x].hasUnit || !registers[y].hasUnit else { return executeMultiplyGeneral(target, x, y) }
        let unit = registers[x].hasUnit ? registers[x].unit : registers[y].unit
        let (result, overflow) = registers[x].integerValue!.multipliedReportingOverflow(by: registers[y].integerValue!)
        if overflow { return executeMultiplyGeneral(target, x, y) }
        registers[target].setInteger(result, unit: unit)
    }

    @inline(never)
    func executeMultiplyGeneral(_ target: Int, _ x: Int, _ y: Int) {
        if registers[x].kind == .integer && registers[y].kind == .integer {
            let av = registers[x].integerValue!
            let bv = registers[y].integerValue!
            guard !registers[x].hasUnit || !registers[y].hasUnit else {
                registers[target].setNothing()
                return
            }
            let unit = registers[x].hasUnit ? registers[x].unit : registers[y].unit
            let (result, overflow) = av.multipliedReportingOverflow(by: bv)
            if overflow {
                registers[target].setFloat(Double(av) * Double(bv), unit: unit)
                return
            }
            registers[target].setInteger(result, unit: unit)
            return
        }
        if registers[x].isNothing || registers[y].isNothing { return registers[target].setNothing() }
        switch registers[x].kind {
        case .float where registers[y].kind == .float:
            guard !registers[x].hasUnit || !registers[y].hasUnit else { return registers[target].setNothing() }
            let unit: GesUnit = registers[x].hasUnit ? registers[x].unit : registers[y].unit
            let left = registers[x].floatValue!
            let right = registers[y].floatValue!
            let result = left * right
            return registers[target].setFloat(result, unit: unit)
        case .integer where registers[y].kind == .float:
            guard !registers[x].hasUnit || !registers[y].hasUnit else { return registers[target].setNothing() }
            let unit: GesUnit = registers[x].hasUnit ? registers[x].unit : registers[y].unit
            let left = Double(registers[x].integerValue!)
            let right = registers[y].floatValue!
            let result = left * right
            return registers[target].setFloat(result, unit: unit)
        case .float where registers[y].kind == .integer:
            guard !registers[x].hasUnit || !registers[y].hasUnit else { return registers[target].setNothing() }
            let unit: GesUnit = registers[x].hasUnit ? registers[x].unit : registers[y].unit
            let left = registers[x].floatValue!
            let right = Double(registers[y].integerValue!)
            let result = left * right
            return registers[target].setFloat(result, unit: unit)
        default: break
        }
        guard !registers[x].hasUnit || !registers[y].hasUnit else { return registers[target].setNothing() }
        let unit = registers[x].hasUnit ? registers[x].unit : registers[y].unit
        if registers[x].spatialValue != nil || registers[y].spatialValue != nil {
            if registers[x].kind == .vector && registers[y].asNumber.isFinite {
                return registers[target].setVector(x: registers[x].x * registers[y].asNumber, y: registers[x].y * registers[y].asNumber, z: registers[x].z * registers[y].asNumber, unit: unit)
            }
            if registers[y].kind == .vector && registers[x].asNumber.isFinite {
                return registers[target].setVector(x: registers[y].x * registers[x].asNumber, y: registers[y].y * registers[x].asNumber, z: registers[y].z * registers[x].asNumber, unit: unit)
            }
            return registers[target].setNothing()
        }
        let left = registers[x].asNumber
        let right = registers[y].asNumber
        return registers[x].kind == .percentage && registers[y].kind == .percentage ? registers[target].setPercentage(left * right) : registers[target].setFloat(left * right, unit: unit)
    }

    @inline(never)
    func executeMultiplyNumeric(_ target: Int, _ x: Int, _ y: Int) {
        guard registers[x].isNumeric, registers[y].isNumeric else { return registers[target].setNothing() }
        if let av = registers[x].numericIntegerValue, let bv = registers[y].numericIntegerValue {
            guard !registers[x].hasUnit || !registers[y].hasUnit else {
                registers[target].setNothing()
                return
            }
            let unit = registers[x].hasUnit ? registers[x].unit : registers[y].unit
            let (result, overflow) = av.multipliedReportingOverflow(by: bv)
            if overflow {
                registers[target].setFloat(Double(av) * Double(bv), unit: unit)
                return
            }
            registers[target].setInteger(result, unit: unit)
            return
        }
        guard !registers[x].hasUnit || !registers[y].hasUnit else { return registers[target].setNothing() }
        let unit = registers[x].hasUnit ? registers[x].unit : registers[y].unit
        let left = registers[x].asNumber
        let right = registers[y].asNumber
        return registers[x].kind == .percentage && registers[y].kind == .percentage ? registers[target].setPercentage(left * right) : registers[target].setFloat(left * right, unit: unit)
    }
}
