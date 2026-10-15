// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    @inline(never)
    func executeSortDescending(_ destinationRegister: Int, _ sourceRegister: Int) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        if registers[x].integerRangeValue != nil || registers[x].floatRangeValue != nil {
            let descending = (registers[x].integerRangeValue?.step ?? 0) < 0 || (registers[x].floatRangeValue?.step ?? 0) < 0
            if descending == (true) { return setValue(destination, registers[x].registerValue) }
            return GesCollectionOperators.reverseRange(registers[x].registerValue, sink: output(destination))
        }
        guard let values = GesCollectionOperators.materialize(registers[x], ranges: false) else { return setNothing(destination) }

        guard let sorted = GesComparison.sorted(values, descending: true) else { return setNothing(destination) }
        return setList(destination, sorted)
    }
}
