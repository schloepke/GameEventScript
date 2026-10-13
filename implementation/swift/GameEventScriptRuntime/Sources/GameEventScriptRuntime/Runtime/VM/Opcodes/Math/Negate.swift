// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeNegate(_ target: Int, _ x: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            let unit = slots[x].unit
            switch slots[x].kind {
            case .integer:
                let integer = slots[x].integerValue!
                return integer == .min ? slots[target].setFloat(-Double(integer), unit: unit) : slots[target].setInteger(-integer, unit: unit)
            case .float:
                let value = slots[x].floatValue!
                return slots[target].setFloat(-value, unit: unit)
            case .percentage:
                let value = slots[x].floatValue!
                return slots[target].setPercentage(-value)
            case .vector:
                return slots[target].setVector(x: -slots[x].x, y: -slots[x].y, z: -slots[x].z, unit: unit)
            case .point:
                return slots[target].setNothing()
            default:
                let value = slots[x].asNumber
                return slots[target].setFloat(-value, unit: unit)
            }
        }
    }

    @inline(never)
    func executeNegateNumeric(_ target: Int, _ x: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            guard slots[x].isNumeric else { return slots[target].setNothing() }
            if let integer = slots[x].numericIntegerValue { return integer == .min ? slots[target].setFloat(-Double(integer), unit: slots[x].unit) : slots[target].setInteger(-integer, unit: slots[x].unit) }
            let number = -slots[x].asNumber
            return slots[x].kind == .percentage ? slots[target].setPercentage(number) : slots[target].setFloat(number, unit: slots[x].unit)
        }
    }
}
