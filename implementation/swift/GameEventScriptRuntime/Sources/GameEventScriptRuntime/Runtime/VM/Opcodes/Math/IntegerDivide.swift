// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeIntegerDivide(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            if slots[x].isNothing || slots[y].isNothing { return slots[target].setNothing() }
            switch slots[x].kind {
            case .integer where slots[y].kind == .integer:
                let av = slots[x].integerValue!
                let bv = slots[y].integerValue!
                guard !slots[y].hasUnit || slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit: GesUnit = slots[y].hasUnit ? .none : slots[x].unit
                if bv != 0 && !(av == .min && bv == -1) {
                    var result = av / bv
                    if av % bv != 0 && (av < 0) != (bv < 0) { result -= 1 }
                    return slots[target].setInteger(result, unit: unit)
                }
                return slots[target].setFloat((Double(av) / Double(bv)).rounded(.down), unit: unit)
            case .float where slots[y].kind == .float:
                guard !slots[y].hasUnit || slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit: GesUnit = slots[y].hasUnit ? .none : slots[x].unit
                let left = slots[x].floatValue!
                let right = slots[y].floatValue!
                let result = (left / right).rounded(.down)
                return slots[target].setFloat(result, unit: unit)
            case .integer where slots[y].kind == .float:
                guard !slots[y].hasUnit || slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit: GesUnit = slots[y].hasUnit ? .none : slots[x].unit
                let left = Double(slots[x].integerValue!)
                let right = slots[y].floatValue!
                let result = (left / right).rounded(.down)
                return slots[target].setFloat(result, unit: unit)
            case .float where slots[y].kind == .integer:
                guard !slots[y].hasUnit || slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit: GesUnit = slots[y].hasUnit ? .none : slots[x].unit
                let left = slots[x].floatValue!
                let right = Double(slots[y].integerValue!)
                let result = (left / right).rounded(.down)
                return slots[target].setFloat(result, unit: unit)
            default: break
            }
            guard !slots[y].hasUnit || slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
            let unit: GesUnit = slots[y].hasUnit ? .none : slots[x].unit
            if slots[x].spatialValue != nil || slots[y].spatialValue != nil { return slots[target].setNothing() }
            let left = slots[x].asNumber
            let right = slots[y].asNumber
            return slots[target].setFloat((left / right).rounded(.down), unit: unit)
        }
    }

    @inline(never)
    func executeIntegerDivideNumeric(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            guard slots[x].isNumeric, slots[y].isNumeric else { return slots[target].setNothing() }
            if let av = slots[x].numericIntegerValue, let bv = slots[y].numericIntegerValue {
                guard !slots[y].hasUnit || slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit: GesUnit = slots[y].hasUnit ? .none : slots[x].unit
                if bv != 0 && !(av == .min && bv == -1) {
                    var result = av / bv
                    if av % bv != 0 && (av < 0) != (bv < 0) { result -= 1 }
                    return slots[target].setInteger(result, unit: unit)
                }
                return slots[target].setFloat((Double(av) / Double(bv)).rounded(.down), unit: unit)
            }
            guard !slots[y].hasUnit || slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
            let unit: GesUnit = slots[y].hasUnit ? .none : slots[x].unit
            let left = slots[x].asNumber
            let right = slots[y].asNumber
            return slots[target].setFloat((left / right).rounded(.down), unit: unit)
        }
    }
}
