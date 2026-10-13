// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeSubtract(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            switch slots[x].kind {
            case .integer where slots[y].kind == .integer:
                let av = slots[x].integerValue!
                let bv = slots[y].integerValue!
                guard slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit = slots[x].unit
                let (result, overflow) = av.subtractingReportingOverflow(bv)
                if overflow { return slots[target].setFloat(Double(av) - Double(bv), unit: unit) }
                return slots[target].setInteger(result, unit: unit)
            case .float where slots[y].kind == .float:
                guard slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit: GesUnit = slots[x].unit
                let left = slots[x].floatValue!
                let right = slots[y].floatValue!
                let result = left - right
                return slots[target].setFloat(result, unit: unit)
            case .integer where slots[y].kind == .float:
                guard slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit: GesUnit = slots[x].unit
                let left = Double(slots[x].integerValue!)
                let right = slots[y].floatValue!
                let result = left - right
                return slots[target].setFloat(result, unit: unit)
            case .float where slots[y].kind == .integer:
                guard slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit: GesUnit = slots[x].unit
                let left = slots[x].floatValue!
                let right = Double(slots[y].integerValue!)
                let result = left - right
                return slots[target].setFloat(result, unit: unit)
            case .percentage where slots[y].kind == .percentage:
                let result = slots[x].floatValue! - slots[y].floatValue!
                return slots[target].setPercentage(result)
            case .vector where slots[y].kind == .vector,
                .point where slots[y].kind == .vector || slots[y].kind == .point:
                let unit = slots[x].unit
                guard unit == slots[y].unit else { return slots[target].setNothing() }
                let vx = slots[x].x - slots[y].x
                let vy = slots[x].y - slots[y].y
                let vz = slots[x].z - slots[y].z
                if slots[x].kind == .point && slots[y].kind == .vector { return slots[target].setPoint(x: vx, y: vy, z: vz, unit: unit) }
                return slots[target].setVector(x: vx, y: vy, z: vz, unit: unit)
            case .nothing:
                return slots[target].setNothing()
            case .list where slots[y].kind == .map:
                return slots[target].setNothing()
            case .list:
                let list = slots[x].listValue!
                let removed = slots[y].listValue ?? slots[y].diceRolls?.map { .integer(Int64($0)) } ?? [slots[y].registerValue]
                return slots[target].setList(GesMath.removing(list, removed))
            case .dice where slots[y].kind == .list:
                let list = slots[x].diceRolls!.map { GesValue.integer(Int64($0)) }
                return slots[target].setList(GesMath.removing(list, slots[y].listValue!))
            case _ where slots[y].kind == .list && slots[x].kind != .map:
                return slots[target].setNothing()
            case .dice:
                let list = slots[x].diceRolls!.map { GesValue.integer(Int64($0)) }
                if let other = slots[y].diceRolls {
                    let result = GesMath.removing(list, other.map { .integer(Int64($0)) })
                    return slots[target].setDice(result.map { Int32($0.integerValue!) })
                }
                if GesMath.validFace(slots[y].registerValue) {
                    let result = GesMath.removing(list, [slots[y].registerValue])
                    return slots[target].setDice(result.map { Int32($0.integerValue!) })
                }
                return slots[target].setNothing()
            case .map:
                let keys: [String]
                switch slots[y].kind {
                case .map: keys = slots[y].asMap!.entries.map(\.key)
                case .text, .tag: keys = [slots[y].textValue!]
                case .list where slots[y].listValue!.allSatisfy({ $0.textValue != nil }):
                    keys = slots[y].listValue!.map(\.asText)
                default: return slots[target].setNothing()
                }
                let result = slots[x].mapEntries!.filter { entry in !keys.contains { GesText.scalarEqual($0, entry.key) } }
                return slots[target].setMap(result)
            case .vector, .point, .percentage:
                return slots[target].setNothing()
            default:
                guard slots[y].kind != .nothing && slots[y].kind != .vector && slots[y].kind != .point else { return slots[target].setNothing() }
                let left = slots[x].asNumber
                let right = slots[y].asNumber
                let unit = slots[x].unit
                if slots[y].kind == .percentage { return slots[target].setFloat(left - left * right, unit: unit) }
                guard unit == slots[y].unit else { return slots[target].setNothing() }
                return slots[target].setFloat(left - right, unit: unit)
            }
        }
    }

    @inline(never)
    func executeSubtractNumeric(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            guard slots[x].isNumeric, slots[y].isNumeric else { return slots[target].setNothing() }
            if let av = slots[x].numericIntegerValue, let bv = slots[y].numericIntegerValue {
                guard slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
                let unit = slots[x].unit
                let (result, overflow) = av.subtractingReportingOverflow(bv)
                if overflow { return slots[target].setFloat(Double(av) - Double(bv), unit: unit) }
                return slots[target].setInteger(result, unit: unit)
            }
            let left = slots[x].asNumber
            let right = slots[y].asNumber
            if slots[x].kind == .percentage {
                guard slots[y].kind == .percentage else { return slots[target].setNothing() }
                return slots[target].setPercentage(left - right)
            }
            if slots[y].kind == .percentage { return slots[target].setFloat(left - left * right, unit: slots[x].unit) }
            guard slots[x].unit == slots[y].unit else { return slots[target].setNothing() }
            return slots[target].setFloat(left - right, unit: slots[x].unit)
        }
    }
}
