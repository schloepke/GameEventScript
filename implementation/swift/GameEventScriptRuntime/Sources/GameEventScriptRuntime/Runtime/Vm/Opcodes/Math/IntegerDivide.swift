// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeIntegerDivide(_ target: Int, _ x: Int, _ y: Int) {
        if registers[x].isNothing || registers[y].isNothing { return registers[target].setNothing() }
        switch registers[x].kind {
        case .integer where registers[y].kind == .integer:
            let av = registers[x].integerValue!
            let bv = registers[y].integerValue!
            guard !registers[y].hasUnit || registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit: GesQuantityUnit = registers[y].hasUnit ? .none : registers[x].unit
            if bv != 0 && !(av == .min && bv == -1) {
                var result = av / bv
                if av % bv != 0 && (av < 0) != (bv < 0) { result -= 1 }
                return registers[target].setInteger(result, unit: unit)
            }
            return registers[target].setFloat((Double(av) / Double(bv)).rounded(.down), unit: unit)
        case .float where registers[y].kind == .float:
            guard !registers[y].hasUnit || registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit: GesQuantityUnit = registers[y].hasUnit ? .none : registers[x].unit
            let left = registers[x].floatValue!
            let right = registers[y].floatValue!
            let result = (left / right).rounded(.down)
            return registers[target].setFloat(result, unit: unit)
        case .integer where registers[y].kind == .float:
            guard !registers[y].hasUnit || registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit: GesQuantityUnit = registers[y].hasUnit ? .none : registers[x].unit
            let left = Double(registers[x].integerValue!)
            let right = registers[y].floatValue!
            let result = (left / right).rounded(.down)
            return registers[target].setFloat(result, unit: unit)
        case .float where registers[y].kind == .integer:
            guard !registers[y].hasUnit || registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit: GesQuantityUnit = registers[y].hasUnit ? .none : registers[x].unit
            let left = registers[x].floatValue!
            let right = Double(registers[y].integerValue!)
            let result = (left / right).rounded(.down)
            return registers[target].setFloat(result, unit: unit)
        default: break
        }
        guard !registers[y].hasUnit || registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
        let unit: GesQuantityUnit = registers[y].hasUnit ? .none : registers[x].unit
        if registers[x].spatialValue != nil || registers[y].spatialValue != nil { return registers[target].setNothing() }
        let left = registers[x].asNumber
        let right = registers[y].asNumber
        return registers[target].setFloat((left / right).rounded(.down), unit: unit)
    }

    func executeIntegerDivideNumeric(_ target: Int, _ x: Int, _ y: Int) {
        guard registers[x].isNumeric, registers[y].isNumeric else { return registers[target].setNothing() }
        if let av = registers[x].numericIntegerValue, let bv = registers[y].numericIntegerValue {
            guard !registers[y].hasUnit || registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit: GesQuantityUnit = registers[y].hasUnit ? .none : registers[x].unit
            if bv != 0 && !(av == .min && bv == -1) {
                var result = av / bv
                if av % bv != 0 && (av < 0) != (bv < 0) { result -= 1 }
                return registers[target].setInteger(result, unit: unit)
            }
            return registers[target].setFloat((Double(av) / Double(bv)).rounded(.down), unit: unit)
        }
        guard !registers[y].hasUnit || registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
        let unit: GesQuantityUnit = registers[y].hasUnit ? .none : registers[x].unit
        let left = registers[x].asNumber
        let right = registers[y].asNumber
        return registers[target].setFloat((left / right).rounded(.down), unit: unit)
    }
}
