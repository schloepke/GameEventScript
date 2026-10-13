// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeImplies(_ target: Int, _ x: Int, _ y: Int) {
        if registers[x].truth == false || registers[y].truth == true { return registers[target].setBoolean(true) }
        return registers[x].truth != nil && registers[y].truth != nil ? registers[target].setBoolean(false) : registers[target].setNothing()
    }
}
