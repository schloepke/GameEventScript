// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeClamp(_ target: Int, _ x: Int, _ y: Int, _ operandA: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            let upper = operandA
            guard slots[x].unit == slots[y].unit, slots[y].unit == slots[upper].unit, slots[x].isNumeric, slots[y].isNumeric, slots[upper].isNumeric else { return slots[target].setNothing() }
            if let av = slots[x].integerValue, let bv = slots[y].integerValue, let cv = slots[upper].integerValue {
                return slots[target].setInteger(Swift.min(Swift.max(av, Swift.min(bv, cv)), Swift.max(bv, cv)), unit: slots[x].unit)
            }
            return slots[target].setFloat(Swift.min(Swift.max(slots[x].asNumber, Swift.min(slots[y].asNumber, slots[upper].asNumber)), Swift.max(slots[y].asNumber, slots[upper].asNumber)), unit: slots[x].unit)
        }
    }

    @inline(never)
    func executeClampNumeric(_ target: Int, _ x: Int, _ y: Int, _ operandA: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            let upper = operandA
            guard slots[x].unit == slots[y].unit, slots[y].unit == slots[upper].unit, slots[x].isNumeric, slots[y].isNumeric, slots[upper].isNumeric else { return slots[target].setNothing() }
            if let av = slots[x].numericIntegerValue, let bv = slots[y].numericIntegerValue, let cv = slots[upper].numericIntegerValue {
                return slots[target].setInteger(Swift.min(Swift.max(av, Swift.min(bv, cv)), Swift.max(bv, cv)), unit: slots[x].unit)
            }
            return slots[target].setFloat(Swift.min(Swift.max(slots[x].asNumber, Swift.min(slots[y].asNumber, slots[upper].asNumber)), Swift.max(slots[y].asNumber, slots[upper].asNumber)), unit: slots[x].unit)
        }
    }
}
