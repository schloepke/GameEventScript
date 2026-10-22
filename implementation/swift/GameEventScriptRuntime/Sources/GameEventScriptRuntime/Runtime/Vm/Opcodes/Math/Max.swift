// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeMax(_ destinationRegister: Int, _ sourceRegister: Int, _ otherRegister: Int) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        let y = frameStart + otherRegister
        return setValue(destination, GesComparison.extreme(registers[x].registerValue, registers[y].registerValue, maximum: true))
    }

    func executeMaxNumeric(_ destinationRegister: Int, _ sourceRegister: Int, _ otherRegister: Int) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        let y = frameStart + otherRegister
        guard registers[x].isNumeric, registers[y].isNumeric, registers[x].unit == registers[y].unit else { return setNothing(destination) }
        let chooseLeft: Bool
        if let left = registers[x].numericIntegerValue, let right = registers[y].numericIntegerValue {
            chooseLeft = left >= right
        } else {
            chooseLeft = registers[x].asNumber >= registers[y].asNumber
        }
        numericCopy(destinationRegister, chooseLeft ? sourceRegister : otherRegister)
    }
}
