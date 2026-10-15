// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    @inline(never)
    func executeContainsAny(_ destinationRegister: Int, _ sourceRegister: Int, _ otherRegister: Int) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        let y = frameStart + otherRegister
        let right = registers[y]
        if right.isRegisterData, registers[y].isNothing { return setNothing(destination) }

        let all = false
        let candidates = [.list, .dice, .text, .tag, .integerRange, .floatRange].contains(registers[x].kind) ? GesIterator.make(registers[x].registerValue) : nil
        guard let candidates else { return setBoolean(destination, all) }
        defer { candidates.close() }
        let container: GesValue
        if let iterator = right.iteratorValue { container = .list(GesCollectionOperators.read(iterator)) } else { container = right }
        while let candidate = candidates.next() { if GesCollectionOperators.contains(candidate, container) != all { return setBoolean(destination, !all) } }
        return setBoolean(destination, all)
    }
}
