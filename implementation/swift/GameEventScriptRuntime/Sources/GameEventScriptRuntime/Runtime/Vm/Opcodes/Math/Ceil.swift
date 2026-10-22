// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeCeil(_ target: Int, _ x: Int) {
        guard registers[x].isNumeric else { return registers[target].setNothing() }
        let n = registers[x].asNumber
        let result = n.rounded(.up)
        return registers[target].setInteger(GesNumber.saturatedInteger(result))
    }
}
