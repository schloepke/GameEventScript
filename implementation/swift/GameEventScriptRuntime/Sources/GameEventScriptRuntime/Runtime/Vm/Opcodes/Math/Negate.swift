// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeNegate(_ target: Int, _ x: Int) {
        let unit = registers[x].unit
        switch registers[x].kind {
        case .integer:
            let integer = registers[x].integerValue!
            return integer == .min ? registers[target].setFloat(-Double(integer), unit: unit) : registers[target].setInteger(-integer, unit: unit)
        case .float:
            let value = registers[x].floatValue!
            return registers[target].setFloat(-value, unit: unit)
        case .percentage:
            let value = registers[x].floatValue!
            return registers[target].setPercentage(-value)
        case .vector:
            return registers[target].setVector(x: -registers[x].x, y: -registers[x].y, z: -registers[x].z, unit: unit)
        case .point:
            return registers[target].setNothing()
        default:
            let value = registers[x].asNumber
            return registers[target].setFloat(-value, unit: unit)
        }
    }

    func executeNegateNumeric(_ target: Int, _ x: Int) {
        guard registers[x].isNumeric else { return registers[target].setNothing() }
        if let integer = registers[x].numericIntegerValue { return integer == .min ? registers[target].setFloat(-Double(integer), unit: registers[x].unit) : registers[target].setInteger(-integer, unit: registers[x].unit) }
        let number = -registers[x].asNumber
        return registers[x].kind == .percentage ? registers[target].setPercentage(number) : registers[target].setFloat(number, unit: registers[x].unit)
    }
}
