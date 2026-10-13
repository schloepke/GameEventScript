// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeMultiply(_ target: Int, _ x: Int, _ y: Int) {
        let handled = registers.withUnsafeMutableBufferPointer { slots in
            switch slots[x].kind {
            case .integer where slots[y].kind == .integer:
                let av = slots[x].integerValue!
                let bv = slots[y].integerValue!
                guard !slots[x].hasUnit || !slots[y].hasUnit else {
                    slots[target].setNothing()
                    return true
                }
                let unit = slots[x].hasUnit ? slots[x].unit : slots[y].unit
                let (result, overflow) = av.multipliedReportingOverflow(by: bv)
                if overflow {
                    slots[target].setFloat(Double(av) * Double(bv), unit: unit)
                    return true
                }
                slots[target].setInteger(result, unit: unit)
                return true
            default: return false
            }
        }
        if handled { return }
        registers.withUnsafeMutableBufferPointer { slots in
            if slots[x].isNothing || slots[y].isNothing { return slots[target].setNothing() }
            switch slots[x].kind {
            case .float where slots[y].kind == .float:
                guard !slots[x].hasUnit || !slots[y].hasUnit else { return slots[target].setNothing() }
                let unit: GesUnit = slots[x].hasUnit ? slots[x].unit : slots[y].unit
                let left = slots[x].floatValue!
                let right = slots[y].floatValue!
                let result = left * right
                return slots[target].setFloat(result, unit: unit)
            case .integer where slots[y].kind == .float:
                guard !slots[x].hasUnit || !slots[y].hasUnit else { return slots[target].setNothing() }
                let unit: GesUnit = slots[x].hasUnit ? slots[x].unit : slots[y].unit
                let left = Double(slots[x].integerValue!)
                let right = slots[y].floatValue!
                let result = left * right
                return slots[target].setFloat(result, unit: unit)
            case .float where slots[y].kind == .integer:
                guard !slots[x].hasUnit || !slots[y].hasUnit else { return slots[target].setNothing() }
                let unit: GesUnit = slots[x].hasUnit ? slots[x].unit : slots[y].unit
                let left = slots[x].floatValue!
                let right = Double(slots[y].integerValue!)
                let result = left * right
                return slots[target].setFloat(result, unit: unit)
            default: break
            }
            guard !slots[x].hasUnit || !slots[y].hasUnit else { return slots[target].setNothing() }
            let unit = slots[x].hasUnit ? slots[x].unit : slots[y].unit
            if slots[x].spatialValue != nil || slots[y].spatialValue != nil {
                if slots[x].kind == .vector && slots[y].asNumber.isFinite {
                    return slots[target].setVector(x: slots[x].x * slots[y].asNumber, y: slots[x].y * slots[y].asNumber, z: slots[x].z * slots[y].asNumber, unit: unit)
                }
                if slots[y].kind == .vector && slots[x].asNumber.isFinite {
                    return slots[target].setVector(x: slots[y].x * slots[x].asNumber, y: slots[y].y * slots[x].asNumber, z: slots[y].z * slots[x].asNumber, unit: unit)
                }
                return slots[target].setNothing()
            }
            let left = slots[x].asNumber
            let right = slots[y].asNumber
            return slots[x].kind == .percentage && slots[y].kind == .percentage ? slots[target].setPercentage(left * right) : slots[target].setFloat(left * right, unit: unit)
        }
    }

    @inline(never)
    func executeMultiplyNumeric(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            guard slots[x].isNumeric, slots[y].isNumeric else { return slots[target].setNothing() }
            if let av = slots[x].numericIntegerValue, let bv = slots[y].numericIntegerValue {
                guard !slots[x].hasUnit || !slots[y].hasUnit else {
                    slots[target].setNothing()
                    return
                }
                let unit = slots[x].hasUnit ? slots[x].unit : slots[y].unit
                let (result, overflow) = av.multipliedReportingOverflow(by: bv)
                if overflow {
                    slots[target].setFloat(Double(av) * Double(bv), unit: unit)
                    return
                }
                slots[target].setInteger(result, unit: unit)
                return
            }
            guard !slots[x].hasUnit || !slots[y].hasUnit else { return slots[target].setNothing() }
            let unit = slots[x].hasUnit ? slots[x].unit : slots[y].unit
            let left = slots[x].asNumber
            let right = slots[y].asNumber
            return slots[x].kind == .percentage && slots[y].kind == .percentage ? slots[target].setPercentage(left * right) : slots[target].setFloat(left * right, unit: unit)
        }
    }
}
