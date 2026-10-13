// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeAnd(_ target: Int, _ x: Int, _ y: Int) {
        if registers[x].truth == false || registers[y].truth == false { return registers[target].setBoolean(false) }
        return registers[x].truth != nil && registers[y].truth != nil ? registers[target].setBoolean(true) : registers[target].setNothing()
    }
}
