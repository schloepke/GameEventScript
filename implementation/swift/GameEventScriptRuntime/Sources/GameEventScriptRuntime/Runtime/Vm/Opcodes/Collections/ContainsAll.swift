// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeContainsAll(_ destinationRegister: Int, _ sourceRegister: Int, _ otherRegister: Int) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        let y = frameStart + otherRegister
        let right = registers[y]
        if right.isRegisterData, registers[y].isNothing { return setNothing(destination) }

        let all = true
        let candidates = [.list, .dice, .bytes, .text, .tag, .integerRange, .floatRange].contains(registers[x].kind) ? GesIterator.make(registers[x].registerValue) : nil
        guard let candidates else { return setBoolean(destination, all) }
        defer { candidates.close() }
        let container: GesValue
        if let iterator = right.iteratorValue { container = .list(GesCollectionOperators.read(iterator)) } else { container = right }
        var chargeItems = GesCollectionOperators.isRange(registers[x])
        // Against an iterator every Range candidate is visited before the search, as the iterator is read only once.
        if chargeItems, right.iteratorValue != nil {
            for _ in 0..<GesCollectionOperators.length(registers[x]) where !chargeRangeItem() { return setNothing(destination) }
            chargeItems = false
        }
        while let candidate = candidates.next() {
            if chargeItems && !chargeRangeItem() { return setNothing(destination) }
            if GesCollectionOperators.contains(candidate, container) != all { return setBoolean(destination, !all) }
        }
        return setBoolean(destination, all)
    }
}
