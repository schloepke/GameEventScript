// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeDot(_ target: Int, _ x: Int, _ y: Int) {
        guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
        guard registers[x].spatialValue != nil, registers[y].spatialValue != nil else { return registers[target].setNothing() }
        let ax = registers[x].x
        let ay = registers[x].y
        let az = registers[x].z
        let bx = registers[y].x
        let by = registers[y].y
        let bz = registers[y].z
        return registers[target].setFloat(ax * bx + ay * by + az * bz)
    }
}
