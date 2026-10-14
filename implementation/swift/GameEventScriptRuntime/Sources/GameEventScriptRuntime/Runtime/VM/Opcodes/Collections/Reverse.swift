// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    @inline(never)
    func executeReverse(_ destinationRegister: Int, _ sourceRegister: Int) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        if registers[x].integerRangeValue != nil || registers[x].floatRangeValue != nil {

            return GesCollectionOperators.reverseRange(registers[x].registerValue, sink: output(destination))
        }
        guard let values = GesCollectionOperators.materialize(registers[x], ranges: false) else { return setNothing(destination) }
        return setList(destination, values.reversed())
    }
}
