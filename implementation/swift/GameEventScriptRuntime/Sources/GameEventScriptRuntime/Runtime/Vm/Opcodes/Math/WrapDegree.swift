// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeWrapDegree(_ target: Int, _ x: Int) {
        if !registers[x].isNumeric || !registers[x].asNumber.isFinite || (registers[x].unit != .none && registers[x].unit != .degree) { return registers[target].setNothing() }
        var value = registers[x].asNumber.truncatingRemainder(dividingBy: 360)
        if value < 0 { value += 360 }
        return registers[target].setFloat(value == 360 ? 0 : value, unit: .degree)
    }
}
