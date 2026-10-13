// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeRemainder(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            if slots[x].isNothing || slots[y].isNothing { return slots[target].setNothing() }
            switch slots[x].kind {
            case .integer where slots[y].kind == .integer:
                let av = slots[x].integerValue!
                let bv = slots[y].integerValue!
                guard slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit = slots[x].unit
                guard bv != 0 else { return slots[target].setNothing() }
                let result = av == .min && bv == -1 ? 0 : av % bv
                return slots[target].setInteger(result, unit: unit)
            case .float where slots[y].kind == .float:
                guard slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit: GesUnit = slots[x].unit
                let left = slots[x].floatValue!
                let right = slots[y].floatValue!
                guard left.isFinite, !right.isNaN, right != 0 else { return slots[target].setNothing() }
                let result = right.isInfinite ? left : left.truncatingRemainder(dividingBy: right)
                return slots[target].setFloat(result, unit: unit)
            case .integer where slots[y].kind == .float:
                guard slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit: GesUnit = slots[x].unit
                let left = Double(slots[x].integerValue!)
                let right = slots[y].floatValue!
                guard left.isFinite, !right.isNaN, right != 0 else { return slots[target].setNothing() }
                let result = right.isInfinite ? left : left.truncatingRemainder(dividingBy: right)
                return slots[target].setFloat(result, unit: unit)
            case .float where slots[y].kind == .integer:
                guard slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit: GesUnit = slots[x].unit
                let left = slots[x].floatValue!
                let right = Double(slots[y].integerValue!)
                guard left.isFinite, !right.isNaN, right != 0 else { return slots[target].setNothing() }
                let result = right.isInfinite ? left : left.truncatingRemainder(dividingBy: right)
                return slots[target].setFloat(result, unit: unit)
            default: break
            }
            guard slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
            let unit = slots[x].unit
            if slots[x].spatialValue != nil || slots[y].spatialValue != nil { return slots[target].setNothing() }
            let left = slots[x].asNumber
            let right = slots[y].asNumber
            guard left.isFinite, !right.isNaN, right != 0 else { return slots[target].setNothing() }
            let result = right.isInfinite ? left : left.truncatingRemainder(dividingBy: right)
            return slots[target].setFloat(result, unit: unit)
        }
    }

    @inline(never)
    func executeRemainderNumeric(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            guard slots[x].isNumeric, slots[y].isNumeric else { return slots[target].setNothing() }
            if let av = slots[x].numericIntegerValue, let bv = slots[y].numericIntegerValue {
                guard slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit = slots[x].unit
                guard bv != 0 else { return slots[target].setNothing() }
                let result = av == .min && bv == -1 ? 0 : av % bv
                return slots[target].setInteger(result, unit: unit)
            }
            guard slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
            let unit = slots[x].unit
            let left = slots[x].asNumber
            let right = slots[y].asNumber
            guard left.isFinite, !right.isNaN, right != 0 else { return slots[target].setNothing() }
            let result = right.isInfinite ? left : left.truncatingRemainder(dividingBy: right)
            return slots[target].setFloat(result, unit: unit)
        }
    }
}
