// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    // Indices are absolute register offsets decoded by the dispatcher.
    @inline(__always)
    func executeAdd(_ destination: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            let leftKind = slots[x].kind
            let rightKind = slots[y].kind
            switch leftKind {
            case .integer where rightKind == .integer:
                let left = slots[x].integerValue!
                let right = slots[y].integerValue!
                let unit = slots[x].unit
                guard unit == slots[y].unit else { return slots[destination].setNothing() }
                let (result, overflow) = left.addingReportingOverflow(right)
                if overflow { return slots[destination].setFloat(Double(left) + Double(right), unit: unit) }
                slots[destination].setInteger(result, unit: unit)
            case .float where rightKind == .float:
                let unit = slots[x].unit
                guard unit == slots[y].unit else { return slots[destination].setNothing() }
                let result = slots[x].floatValue! + slots[y].floatValue!
                slots[destination].setFloat(result, unit: unit)
            case .integer where rightKind == .float:
                let unit = slots[x].unit
                guard unit == slots[y].unit else { return slots[destination].setNothing() }
                let result = Double(slots[x].integerValue!) + slots[y].floatValue!
                slots[destination].setFloat(result, unit: unit)
            case .float where rightKind == .integer:
                let unit = slots[x].unit
                guard unit == slots[y].unit else { return slots[destination].setNothing() }
                let result = slots[x].floatValue! + Double(slots[y].integerValue!)
                slots[destination].setFloat(result, unit: unit)
            case .percentage where rightKind == .percentage:
                let result = slots[x].asNumber + slots[y].asNumber
                slots[destination].setPercentage(result)
            case .vector where rightKind == .vector, .point where rightKind == .vector:
                let unit = slots[x].unit
                guard unit == slots[y].unit else { return slots[destination].setNothing() }
                let vx = slots[x].x + slots[y].x
                let vy = slots[x].y + slots[y].y
                let vz = slots[x].z + slots[y].z
                if leftKind == .point { return slots[destination].setPoint(x: vx, y: vy, z: vz, unit: unit) }
                slots[destination].setVector(x: vx, y: vy, z: vz, unit: unit)
            case .nothing,
                _ where rightKind == .nothing:
                slots[destination].setNothing()
            case .text,
                _ where rightKind == .text:
                let result = slots[x].toText + slots[y].toText
                slots[destination].setText(result)
            case .list:
                let result = slots[x].listValue! + [slots[y].registerValue]
                slots[destination].setList(result)
            case _ where rightKind == .list:
                let result = [slots[x].registerValue] + slots[y].listValue!
                slots[destination].setList(result)
            case .dice,
                _ where rightKind == .dice:
                if let dice = slots[x].diceRolls, GesMath.validFace(slots[y].registerValue) {
                    let result = dice + [Int32(slots[y].asInteger)]
                    return slots[destination].setDice(result)
                }
                if let dice = slots[y].diceRolls, GesMath.validFace(slots[x].registerValue) {
                    let result = [Int32(slots[x].asInteger)] + dice
                    return slots[destination].setDice(result)
                }
                slots[destination].setNothing()
            case .vector, .point,
                _ where rightKind == .vector || rightKind == .point:
                slots[destination].setNothing()
            case .percentage:
                slots[destination].setNothing()
            case _ where rightKind == .percentage:
                let left = slots[x].asNumber
                let result = left + left * slots[y].asNumber
                let unit = slots[x].unit
                slots[destination].setFloat(result, unit: unit)
            default:
                let unit = slots[x].unit
                guard unit == slots[y].unit else { return slots[destination].setNothing() }
                let result = slots[x].asNumber + slots[y].asNumber
                slots[destination].setFloat(result, unit: unit)
            }
        }
    }

    @inline(never)
    func executeAddNumeric(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            guard slots[x].isNumeric, slots[y].isNumeric else { return slots[target].setNothing() }
            if let av = slots[x].numericIntegerValue, let bv = slots[y].numericIntegerValue {
                guard slots[x].unit == slots[y].unit else {
                    slots[target].setNothing()
                    return
                }
                let unit = slots[x].unit
                let (result, overflow) = av.addingReportingOverflow(bv)
                if overflow {
                    slots[target].setFloat(Double(av) + Double(bv), unit: unit)
                    return
                }
                slots[target].setInteger(result, unit: unit)
                return
            }
            let left = slots[x].asNumber
            let right = slots[y].asNumber
            if slots[x].kind == .percentage {
                guard slots[y].kind == .percentage else { return slots[target].setNothing() }
                return slots[target].setPercentage(left + right)
            }
            if slots[y].kind == .percentage { return slots[target].setFloat(left + left * right, unit: slots[x].unit) }
            guard slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
            return slots[target].setFloat(left + right, unit: slots[x].unit)
        }
    }
}
