// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeRoundHalfDown(_ target: Int, _ x: Int) {
        guard registers[x].isNumeric else { return registers[target].setNothing() }
        let n = registers[x].asNumber
        // Rounding keeps the quantity unit; percentages and Booleans have none.
        let unit = registers[x].unit
        let truncated = n.rounded(.towardZero)
        let result = Swift.abs(n - truncated) <= 0.5 ? truncated : n.rounded(.toNearestOrAwayFromZero)
        return registers[target].setInteger(GesNumber.saturatedInteger(result), unit: unit)
    }
}
