// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeEndsWith(_ target: Int, _ x: Int, _ y: Int) {
        if registers[x].isNothing { return registers[target].setNothing() }
        if let at = registers[x].textValue, let bt = registers[y].textValue { return registers[target].setBoolean(at.unicodeScalars.reversed().starts(with: bt.unicodeScalars.reversed())) }
        guard GesCollectionOperators.sequence(registers[x].registerValue), GesCollectionOperators.sequence(registers[y].registerValue) else { return registers[target].setBoolean(false) }
        let ac = GesCollectionOperators.length(registers[x].registerValue)
        let bc = GesCollectionOperators.length(registers[y].registerValue)
        if bc > ac { return registers[target].setBoolean(false) }
        if let left = registers[x].integerRangeValue, let right = registers[y].integerRangeValue {
            return registers[target].setBoolean(GesCollectionOperators.integerRangeAffix(left, ac, right, bc, suffix: true))
        }
        // Between two Ranges every compared pair charges the loop budget (specs/HostRuntime.md#iterator-and-generated-collection-limits).
        let chargeItems = GesCollectionOperators.isRange(registers[x]) && GesCollectionOperators.isRange(registers[y])
        let start = ac - bc
        for index in 0..<bc {
            if chargeItems && !chargeRangeItem() { return registers[target].setNothing() }
            if registers[x].index(start + index + 1) != registers[y].index(index + 1) { return registers[target].setBoolean(false) }
        }
        return registers[target].setBoolean(true)
    }
}
