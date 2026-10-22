// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeSubtract(_ target: Int, _ x: Int, _ y: Int) {
        switch registers[x].kind {
        case .integer where registers[y].kind == .integer:
            let av = registers[x].integerValue!
            let bv = registers[y].integerValue!
            guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit = registers[x].unit
            let (result, overflow) = av.subtractingReportingOverflow(bv)
            if overflow { return registers[target].setFloat(Double(av) - Double(bv), unit: unit) }
            return registers[target].setInteger(result, unit: unit)
        case .float where registers[y].kind == .float:
            guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit: GesQuantityUnit = registers[x].unit
            let left = registers[x].floatValue!
            let right = registers[y].floatValue!
            let result = left - right
            return registers[target].setFloat(result, unit: unit)
        case .integer where registers[y].kind == .float:
            guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit: GesQuantityUnit = registers[x].unit
            let left = Double(registers[x].integerValue!)
            let right = registers[y].floatValue!
            let result = left - right
            return registers[target].setFloat(result, unit: unit)
        case .float where registers[y].kind == .integer:
            guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit: GesQuantityUnit = registers[x].unit
            let left = registers[x].floatValue!
            let right = Double(registers[y].integerValue!)
            let result = left - right
            return registers[target].setFloat(result, unit: unit)
        case .percentage where registers[y].kind == .percentage:
            let result = registers[x].floatValue! - registers[y].floatValue!
            return registers[target].setPercentage(result)
        case .vector where registers[y].kind == .vector,
            .point where registers[y].kind == .vector || registers[y].kind == .point:
            let unit = registers[x].unit
            guard unit == registers[y].unit else { return registers[target].setNothing() }
            let vx = registers[x].x - registers[y].x
            let vy = registers[x].y - registers[y].y
            let vz = registers[x].z - registers[y].z
            if registers[x].kind == .point && registers[y].kind == .vector { return registers[target].setPoint(x: vx, y: vy, z: vz, unit: unit) }
            return registers[target].setVector(x: vx, y: vy, z: vz, unit: unit)
        case .nothing:
            return registers[target].setNothing()
        case .list where registers[y].kind == .map:
            return registers[target].setNothing()
        case .list:
            let list = registers[x].listValue!
            let removed = registers[y].listValue ?? registers[y].diceRolls?.map { .integer(Int64($0)) } ?? [registers[y].registerValue]
            return registers[target].setList(GesMath.removing(list, removed))
        case .dice where registers[y].kind == .list:
            let list = registers[x].diceRolls!.map { GesValue.integer(Int64($0)) }
            return registers[target].setList(GesMath.removing(list, registers[y].listValue!))
        case _ where registers[y].kind == .list && registers[x].kind != .map:
            return registers[target].setNothing()
        case .dice:
            let list = registers[x].diceRolls!.map { GesValue.integer(Int64($0)) }
            if let other = registers[y].diceRolls {
                let result = GesMath.removing(list, other.map { .integer(Int64($0)) })
                return registers[target].setDice(result.map { Int32($0.integerValue!) })
            }
            if GesMath.validFace(registers[y].registerValue) {
                let result = GesMath.removing(list, [registers[y].registerValue])
                return registers[target].setDice(result.map { Int32($0.integerValue!) })
            }
            return registers[target].setNothing()
        case .map:
            let keys: [String]
            switch registers[y].kind {
            case .map: keys = registers[y].asMap!.keyStorage
            case .text, .tag: keys = [registers[y].textValue!]
            case .list where registers[y].listValue!.allSatisfy({ $0.textValue != nil }):
                keys = GesValueMap.sortedKeys(registers[y].listValue!.map(\.asText))
            default: return registers[target].setNothing()
            }
            return registers[target].setMap(registers[x].asMap!.selecting(sortedKeys: keys, keep: false))
        case .vector, .point, .percentage:
            return registers[target].setNothing()
        default:
            guard registers[y].kind != .nothing && registers[y].kind != .vector && registers[y].kind != .point else { return registers[target].setNothing() }
            let left = registers[x].asNumber
            let right = registers[y].asNumber
            let unit = registers[x].unit
            if registers[y].kind == .percentage { return registers[target].setFloat(left - left * right, unit: unit) }
            guard unit == registers[y].unit else { return registers[target].setNothing() }
            return registers[target].setFloat(left - right, unit: unit)
        }
    }

    func executeSubtractNumeric(_ target: Int, _ x: Int, _ y: Int) {
        guard registers[x].isNumeric, registers[y].isNumeric else { return registers[target].setNothing() }
        if let av = registers[x].numericIntegerValue, let bv = registers[y].numericIntegerValue {
            guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
            let unit = registers[x].unit
            let (result, overflow) = av.subtractingReportingOverflow(bv)
            if overflow { return registers[target].setFloat(Double(av) - Double(bv), unit: unit) }
            return registers[target].setInteger(result, unit: unit)
        }
        let left = registers[x].asNumber
        let right = registers[y].asNumber
        if registers[x].kind == .percentage {
            guard registers[y].kind == .percentage else { return registers[target].setNothing() }
            return registers[target].setPercentage(left - right)
        }
        if registers[y].kind == .percentage { return registers[target].setFloat(left - left * right, unit: registers[x].unit) }
        guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
        return registers[target].setFloat(left - right, unit: registers[x].unit)
    }
}
