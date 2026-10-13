// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeFirst(_ destinationRegister: Int, _ sourceRegister: Int) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        if let iterator = registers[x].iteratorValue {
            defer { iterator.close() }
            guard let first = iterator.next() else { return setNothing(destination) }
            return setValue(destination, first)
        }
        guard let iterator = GesIterator(registers[x].registerValue) else { return setNothing(destination) }
        return iterator.next(sink: output(destination)) ?? setNothing(destination)
    }
}
