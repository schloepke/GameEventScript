// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeTruncate(_ target: Int, _ x: Int) {
        guard registers[x].isNumeric else { return registers[target].setNothing() }
        let n = registers[x].asNumber
        let result = n.rounded(.towardZero)
        return registers[target].setInteger(GesNumber.saturatedInteger(result))
    }
}
