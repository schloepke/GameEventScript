// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    @inline(never)
    func executeRandomTakeFloat(_ destinationRegister: Int, _ sourceRegister: Int, _ otherRegister: Int, _ context: GesContext) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        let y = frameStart + otherRegister
        if registers[x].unit != registers[y].unit { return setNothing(destination) }

        return registers[x].asNumber.isFinite && registers[y].asNumber.isFinite ? setFloat(destination, context.random.nextFloat(registers[x].asNumber, registers[y].asNumber), unit: registers[x].unit) : setNothing(destination)
    }
}
