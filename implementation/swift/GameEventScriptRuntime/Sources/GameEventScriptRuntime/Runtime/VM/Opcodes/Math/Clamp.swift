// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    @inline(never)
    func executeClamp(_ target: Int, _ x: Int, _ y: Int, _ operandA: Int) {
        let upper = operandA
        guard registers[x].unit == registers[y].unit, registers[y].unit == registers[upper].unit, registers[x].isNumeric, registers[y].isNumeric, registers[upper].isNumeric else { return registers[target].setNothing() }
        if let av = registers[x].integerValue, let bv = registers[y].integerValue, let cv = registers[upper].integerValue {
            return registers[target].setInteger(Swift.min(Swift.max(av, Swift.min(bv, cv)), Swift.max(bv, cv)), unit: registers[x].unit)
        }
        return registers[target].setFloat(Swift.min(Swift.max(registers[x].asNumber, Swift.min(registers[y].asNumber, registers[upper].asNumber)), Swift.max(registers[y].asNumber, registers[upper].asNumber)), unit: registers[x].unit)
    }

    @inline(never)
    func executeClampNumeric(_ target: Int, _ x: Int, _ y: Int, _ operandA: Int) {
        let upper = operandA
        guard registers[x].unit == registers[y].unit, registers[y].unit == registers[upper].unit, registers[x].isNumeric, registers[y].isNumeric, registers[upper].isNumeric else { return registers[target].setNothing() }
        if let av = registers[x].numericIntegerValue, let bv = registers[y].numericIntegerValue, let cv = registers[upper].numericIntegerValue {
            return registers[target].setInteger(Swift.min(Swift.max(av, Swift.min(bv, cv)), Swift.max(bv, cv)), unit: registers[x].unit)
        }
        return registers[target].setFloat(Swift.min(Swift.max(registers[x].asNumber, Swift.min(registers[y].asNumber, registers[upper].asNumber)), Swift.max(registers[y].asNumber, registers[upper].asNumber)), unit: registers[x].unit)
    }
}
