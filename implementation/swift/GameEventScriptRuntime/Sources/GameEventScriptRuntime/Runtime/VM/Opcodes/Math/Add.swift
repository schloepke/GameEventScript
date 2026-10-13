// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    // Indices are absolute register offsets decoded by the dispatcher. Integer operands with
    // matching units are the hot case and run inline; everything else, including overflow,
    // takes the complete out-of-line body.
    @inline(__always)
    func executeAdd(_ destination: Int, _ x: Int, _ y: Int) {
        guard registers[x].kind == .integer, registers[y].kind == .integer, registers[x].unit == registers[y].unit else { return executeAddGeneral(destination, x, y) }
        let unit = registers[x].unit
        let (result, overflow) = registers[x].integerValue!.addingReportingOverflow(registers[y].integerValue!)
        if overflow { return executeAddGeneral(destination, x, y) }
        registers[destination].setInteger(result, unit: unit)
    }

    @inline(never)
    func executeAddGeneral(_ destination: Int, _ x: Int, _ y: Int) {
        let leftKind = registers[x].kind
        let rightKind = registers[y].kind
        switch leftKind {
        case .integer where rightKind == .integer:
            let left = registers[x].integerValue!
            let right = registers[y].integerValue!
            let unit = registers[x].unit
            guard unit == registers[y].unit else { return registers[destination].setNothing() }
            let (result, overflow) = left.addingReportingOverflow(right)
            if overflow { return registers[destination].setFloat(Double(left) + Double(right), unit: unit) }
            registers[destination].setInteger(result, unit: unit)
        case .float where rightKind == .float:
            let unit = registers[x].unit
            guard unit == registers[y].unit else { return registers[destination].setNothing() }
            let result = registers[x].floatValue! + registers[y].floatValue!
            registers[destination].setFloat(result, unit: unit)
        case .integer where rightKind == .float:
            let unit = registers[x].unit
            guard unit == registers[y].unit else { return registers[destination].setNothing() }
            let result = Double(registers[x].integerValue!) + registers[y].floatValue!
            registers[destination].setFloat(result, unit: unit)
        case .float where rightKind == .integer:
            let unit = registers[x].unit
            guard unit == registers[y].unit else { return registers[destination].setNothing() }
            let result = registers[x].floatValue! + Double(registers[y].integerValue!)
            registers[destination].setFloat(result, unit: unit)
        case .percentage where rightKind == .percentage:
            let result = registers[x].asNumber + registers[y].asNumber
            registers[destination].setPercentage(result)
        case .vector where rightKind == .vector, .point where rightKind == .vector:
            let unit = registers[x].unit
            guard unit == registers[y].unit else { return registers[destination].setNothing() }
            let vx = registers[x].x + registers[y].x
            let vy = registers[x].y + registers[y].y
            let vz = registers[x].z + registers[y].z
            if leftKind == .point { return registers[destination].setPoint(x: vx, y: vy, z: vz, unit: unit) }
            registers[destination].setVector(x: vx, y: vy, z: vz, unit: unit)
        case .nothing,
            _ where rightKind == .nothing:
            registers[destination].setNothing()
        case .text,
            _ where rightKind == .text:
            let result = registers[x].toText + registers[y].toText
            registers[destination].setText(result)
        case .list:
            let result = registers[x].listValue! + [registers[y].registerValue]
            registers[destination].setList(result)
        case _ where rightKind == .list:
            let result = [registers[x].registerValue] + registers[y].listValue!
            registers[destination].setList(result)
        case .dice,
            _ where rightKind == .dice:
            if let dice = registers[x].diceRolls, GesMath.validFace(registers[y].registerValue) {
                let result = dice + [Int32(registers[y].asInteger)]
                return registers[destination].setDice(result)
            }
            if let dice = registers[y].diceRolls, GesMath.validFace(registers[x].registerValue) {
                let result = [Int32(registers[x].asInteger)] + dice
                return registers[destination].setDice(result)
            }
            registers[destination].setNothing()
        case .vector, .point,
            _ where rightKind == .vector || rightKind == .point:
            registers[destination].setNothing()
        case .percentage:
            registers[destination].setNothing()
        case _ where rightKind == .percentage:
            let left = registers[x].asNumber
            let result = left + left * registers[y].asNumber
            let unit = registers[x].unit
            registers[destination].setFloat(result, unit: unit)
        default:
            let unit = registers[x].unit
            guard unit == registers[y].unit else { return registers[destination].setNothing() }
            let result = registers[x].asNumber + registers[y].asNumber
            registers[destination].setFloat(result, unit: unit)
        }
    }

    @inline(never)
    func executeAddNumeric(_ target: Int, _ x: Int, _ y: Int) {
        guard registers[x].isNumeric, registers[y].isNumeric else { return registers[target].setNothing() }
        if let av = registers[x].numericIntegerValue, let bv = registers[y].numericIntegerValue {
            guard registers[x].unit == registers[y].unit else {
                registers[target].setNothing()
                return
            }
            let unit = registers[x].unit
            let (result, overflow) = av.addingReportingOverflow(bv)
            if overflow {
                registers[target].setFloat(Double(av) + Double(bv), unit: unit)
                return
            }
            registers[target].setInteger(result, unit: unit)
            return
        }
        let left = registers[x].asNumber
        let right = registers[y].asNumber
        if registers[x].kind == .percentage {
            guard registers[y].kind == .percentage else { return registers[target].setNothing() }
            return registers[target].setPercentage(left + right)
        }
        if registers[y].kind == .percentage { return registers[target].setFloat(left + left * right, unit: registers[x].unit) }
        guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
        return registers[target].setFloat(left + right, unit: registers[x].unit)
    }
}
