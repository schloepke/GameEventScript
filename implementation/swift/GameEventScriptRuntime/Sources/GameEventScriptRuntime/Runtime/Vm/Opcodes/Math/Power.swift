// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

extension GesVirtualMachine {
    func executePower(_ target: Int, _ x: Int, _ y: Int) {
        if registers[x].isNothing || registers[y].isNothing { return registers[target].setNothing() }
        let left = registers[x].asNumber
        let right = registers[y].asNumber
        // Quantity powers follow the unit derivations, for example `m` squared is `m²`; others are invalid.
        guard !registers[y].hasUnit, !left.isNaN, !right.isNaN, let unit = GesQuantityUnit.power(registers[x].unit, right) else { return registers[target].setNothing() }
        return registers[target].setFloat(GesNumber.power(left, right), unit: unit)
    }

    func executePowerNumeric(_ target: Int, _ x: Int, _ y: Int) {
        guard registers[x].isNumeric, registers[y].isNumeric else { return registers[target].setNothing() }
        let left = registers[x].asNumber
        let right = registers[y].asNumber
        // Quantity powers follow the unit derivations, for example `m` squared is `m²`; others are invalid.
        guard !registers[y].hasUnit, !left.isNaN, !right.isNaN, let unit = GesQuantityUnit.power(registers[x].unit, right) else { return registers[target].setNothing() }
        return registers[target].setFloat(GesNumber.power(left, right), unit: unit)
    }
}
