// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeChance(_ destinationRegister: Int, _ sourceRegister: Int, _ context: GesContext) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        if !registers[x].numericOnly || registers[x].hasUnit || !registers[x].asNumber.isFinite { return setNothing(destination) }
        let ratio = registers[x].kind == .integer || (registers[x].kind == .float && Swift.abs(registers[x].asNumber) > 1) ? registers[x].asNumber / 100 : registers[x].asNumber
        return setBoolean(destination, ratio <= 0 ? false : ratio >= 1 ? true : context.random.nextFloat(0, 1) < ratio)
    }
}
