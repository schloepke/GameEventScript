// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeTerm(_ destinationRegister: Int, _ sourceRegister: Int, _ otherRegister: Int) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        let y = frameStart + otherRegister
        return registers[y].isNumeric ? GesSeries.term(registers[x].registerValue, index: registers[y].asInteger, sink: output(destination)) : setNothing(destination)
    }
}
