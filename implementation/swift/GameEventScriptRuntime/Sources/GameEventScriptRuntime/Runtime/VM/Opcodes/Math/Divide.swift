// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeDivide(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            if slots[x].isNothing || slots[y].isNothing { return slots[target].setNothing() }
            switch slots[x].kind {
            case .integer where slots[y].kind == .integer:
                let av = slots[x].integerValue!
                let bv = slots[y].integerValue!
                guard !slots[y].hasUnit || slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit: GesUnit = slots[y].hasUnit ? .none : slots[x].unit
                return slots[target].setFloat(Double(av) / Double(bv), unit: unit)
            case .float where slots[y].kind == .float:
                guard !slots[y].hasUnit || slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit: GesUnit = slots[y].hasUnit ? .none : slots[x].unit
                let left = slots[x].floatValue!
                let right = slots[y].floatValue!
                let result = left / right
                return slots[target].setFloat(result, unit: unit)
            case .integer where slots[y].kind == .float:
                guard !slots[y].hasUnit || slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit: GesUnit = slots[y].hasUnit ? .none : slots[x].unit
                let left = Double(slots[x].integerValue!)
                let right = slots[y].floatValue!
                let result = left / right
                return slots[target].setFloat(result, unit: unit)
            case .float where slots[y].kind == .integer:
                guard !slots[y].hasUnit || slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit: GesUnit = slots[y].hasUnit ? .none : slots[x].unit
                let left = slots[x].floatValue!
                let right = Double(slots[y].integerValue!)
                let result = left / right
                return slots[target].setFloat(result, unit: unit)
            default: break
            }
            guard !slots[y].hasUnit || slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
            let unit: GesUnit = slots[y].hasUnit ? .none : slots[x].unit
            if slots[x].spatialValue != nil || slots[y].spatialValue != nil {
                guard slots[x].kind == .vector, slots[y].asNumber.isFinite, slots[y].asNumber != 0 else { return slots[target].setNothing() }
                return slots[target].setVector(x: slots[x].x / slots[y].asNumber, y: slots[x].y / slots[y].asNumber, z: slots[x].z / slots[y].asNumber, unit: unit)
            }
            let left = slots[x].asNumber
            let right = slots[y].asNumber
            return slots[x].kind == .percentage && slots[y].kind != .percentage ? slots[target].setPercentage(left / right) : slots[target].setFloat(left / right, unit: unit)
        }
    }

    @inline(never)
    func executeDivideNumeric(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            guard slots[x].isNumeric, slots[y].isNumeric else { return slots[target].setNothing() }
            if let av = slots[x].numericIntegerValue, let bv = slots[y].numericIntegerValue {
                guard !slots[y].hasUnit || slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit: GesUnit = slots[y].hasUnit ? .none : slots[x].unit
                return slots[target].setFloat(Double(av) / Double(bv), unit: unit)
            }
            guard !slots[y].hasUnit || slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
            let unit: GesUnit = slots[y].hasUnit ? .none : slots[x].unit
            let left = slots[x].asNumber
            let right = slots[y].asNumber
            return slots[x].kind == .percentage && slots[y].kind != .percentage ? slots[target].setPercentage(left / right) : slots[target].setFloat(left / right, unit: unit)
        }
    }
}
