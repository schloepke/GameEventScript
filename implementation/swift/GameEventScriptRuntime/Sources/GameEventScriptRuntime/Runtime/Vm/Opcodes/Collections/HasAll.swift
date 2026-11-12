// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeHasAll(_ destinationRegister: Int, _ sourceRegister: Int) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        let all = true
        let iterator: GesIterator?
        if let source = registers[x].iteratorValue { iterator = source } else { iterator = GesIterator.make(registers[x].registerValue) }
        if registers[x].isNothing, registers[x].isRegisterData { return setNothing(destination) }
        // An integer Range decides without visiting items: it has a nonzero item unless it is just 0, and every item is
        // nonzero unless it contains 0.
        if let range = registers[x].integerRangeValue {
            iterator?.close()
            if range.count == 0 { return setBoolean(destination, all) }
            return setBoolean(destination, all ? !range.contains(0) : range.count > 1 || range.from != 0)
        }
        guard let iterator else { return setBoolean(destination, false) }
        defer { iterator.close() }
        let chargeItems = registers[x].floatRangeValue != nil
        while let value = iterator.next() {
            if chargeItems && !chargeRangeItem() { return setNothing(destination) }
            let truth = registers[x].spatialValue != nil ? value.asNumber.isFinite && value.asNumber != 0 : value.asBoolean
            if truth != all { return setBoolean(destination, !all) }
        }
        return setBoolean(destination, all)
    }
}
