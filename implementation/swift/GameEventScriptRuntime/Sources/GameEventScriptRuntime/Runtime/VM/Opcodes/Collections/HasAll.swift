// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeHasAll(_ destinationRegister: Int, _ sourceRegister: Int) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        let all = true
        let iterator: GesIterator?
        if let source = registers[x].iteratorValue { iterator = source } else { iterator = GesIterator(registers[x].registerValue) }
        if registers[x].isNothing, registers[x].isRegisterData { return setNothing(destination) }
        guard let iterator else { return setBoolean(destination, false) }
        defer { iterator.close() }
        while let value = iterator.next() {
            let truth = registers[x].spatialValue != nil ? value.asNumber.isFinite && value.asNumber != 0 : value.asBoolean
            if truth != all { return setBoolean(destination, !all) }
        }
        return setBoolean(destination, all)
    }
}
