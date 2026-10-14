// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    @inline(never)
    func executeNot(_ target: Int, _ x: Int) {
        guard let truth = registers[x].truth else { return registers[target].setNothing() }
        return registers[target].setBoolean(!truth)
    }
}
