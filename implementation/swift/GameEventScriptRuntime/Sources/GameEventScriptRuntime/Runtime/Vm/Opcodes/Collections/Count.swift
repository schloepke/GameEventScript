// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    @inline(never)
    func executeCount(_ destinationRegister: Int, _ sourceRegister: Int) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        if let iterator = registers[x].iteratorValue {
            defer { iterator.close() }
            var count: Int64 = 0
            while iterator.next() != nil { count += 1 }
            return setInteger(destination, count)
        }
        if registers[x].isNothing { return setInteger(destination, 0) }
        if registers[x].kind == .series || registers[x].kind == .external || GesIterator(registers[x].registerValue) == nil { return setNothing(destination) }
        return setInteger(destination, GesCollectionOperators.length(registers[x].registerValue))
    }
}
