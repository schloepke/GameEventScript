// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeLast(_ destinationRegister: Int, _ sourceRegister: Int) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        if let iterator = registers[x].iteratorValue {
            defer { iterator.close() }
            guard let first = iterator.next() else { return setNothing(destination) }

            var last = first
            while let value = iterator.next() { last = value }
            return setValue(destination, last)
        }
        guard GesIterator(registers[x].registerValue) != nil else { return setNothing(destination) }

        if let values = registers[x].mapEntries { return setValue(destination, values.last?.value ?? .nothing) }
        if registers[x].spatialValue != nil { return setFloat(destination, registers[x].z) }
        return registers[x].index(GesCollectionOperators.length(registers[x].registerValue), sink: output(destination))
    }
}
