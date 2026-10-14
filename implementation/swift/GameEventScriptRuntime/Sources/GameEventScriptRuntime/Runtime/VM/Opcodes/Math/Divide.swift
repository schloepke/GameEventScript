// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    @inline(never)
    func executeDivide(_ target: Int, _ x: Int, _ y: Int) {
        if registers[x].isNothing || registers[y].isNothing { return registers[target].setNothing() }
        switch registers[x].kind {
        case .integer where registers[y].kind == .integer:
            let av = registers[x].integerValue!
            let bv = registers[y].integerValue!
            guard !registers[y].hasUnit || registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit: GesUnit = registers[y].hasUnit ? .none : registers[x].unit
            return registers[target].setFloat(Double(av) / Double(bv), unit: unit)
        case .float where registers[y].kind == .float:
            guard !registers[y].hasUnit || registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit: GesUnit = registers[y].hasUnit ? .none : registers[x].unit
            let left = registers[x].floatValue!
            let right = registers[y].floatValue!
            let result = left / right
            return registers[target].setFloat(result, unit: unit)
        case .integer where registers[y].kind == .float:
            guard !registers[y].hasUnit || registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit: GesUnit = registers[y].hasUnit ? .none : registers[x].unit
            let left = Double(registers[x].integerValue!)
            let right = registers[y].floatValue!
            let result = left / right
            return registers[target].setFloat(result, unit: unit)
        case .float where registers[y].kind == .integer:
            guard !registers[y].hasUnit || registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit: GesUnit = registers[y].hasUnit ? .none : registers[x].unit
            let left = registers[x].floatValue!
            let right = Double(registers[y].integerValue!)
            let result = left / right
            return registers[target].setFloat(result, unit: unit)
        default: break
        }
        guard !registers[y].hasUnit || registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
        let unit: GesUnit = registers[y].hasUnit ? .none : registers[x].unit
        if registers[x].spatialValue != nil || registers[y].spatialValue != nil {
            guard registers[x].kind == .vector, registers[y].asNumber.isFinite, registers[y].asNumber != 0 else { return registers[target].setNothing() }
            return registers[target].setVector(x: registers[x].x / registers[y].asNumber, y: registers[x].y / registers[y].asNumber, z: registers[x].z / registers[y].asNumber, unit: unit)
        }
        let left = registers[x].asNumber
        let right = registers[y].asNumber
        return registers[x].kind == .percentage && registers[y].kind != .percentage ? registers[target].setPercentage(left / right) : registers[target].setFloat(left / right, unit: unit)
    }

    @inline(never)
    func executeDivideNumeric(_ target: Int, _ x: Int, _ y: Int) {
        guard registers[x].isNumeric, registers[y].isNumeric else { return registers[target].setNothing() }
        if let av = registers[x].numericIntegerValue, let bv = registers[y].numericIntegerValue {
            guard !registers[y].hasUnit || registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit: GesUnit = registers[y].hasUnit ? .none : registers[x].unit
            return registers[target].setFloat(Double(av) / Double(bv), unit: unit)
        }
        guard !registers[y].hasUnit || registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
        let unit: GesUnit = registers[y].hasUnit ? .none : registers[x].unit
        let left = registers[x].asNumber
        let right = registers[y].asNumber
        return registers[x].kind == .percentage && registers[y].kind != .percentage ? registers[target].setPercentage(left / right) : registers[target].setFloat(left / right, unit: unit)
    }
}
