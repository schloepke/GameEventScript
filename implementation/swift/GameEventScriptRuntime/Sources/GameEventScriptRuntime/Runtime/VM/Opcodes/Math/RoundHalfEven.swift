// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeRoundHalfEven(_ target: Int, _ x: Int) {
        guard registers[x].isNumeric else { return registers[target].setNothing() }
        let n = registers[x].asNumber
        let result = n.rounded(.toNearestOrEven)
        return registers[target].setInteger(GesNumber.saturatedInteger(result))
    }
}
