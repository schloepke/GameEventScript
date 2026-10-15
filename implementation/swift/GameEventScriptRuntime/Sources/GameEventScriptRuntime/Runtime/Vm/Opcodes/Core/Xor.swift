// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    @inline(never)
    func executeXor(_ target: Int, _ x: Int, _ y: Int) {
        guard let av = registers[x].truth, let bv = registers[y].truth else { return registers[target].setNothing() }
        return registers[target].setBoolean(av != bv)
    }
}
