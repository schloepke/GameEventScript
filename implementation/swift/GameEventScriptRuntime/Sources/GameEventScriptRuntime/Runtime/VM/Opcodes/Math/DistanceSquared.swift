// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeDistanceSquared(_ target: Int, _ x: Int, _ y: Int) {
        guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
        if registers[x].isNumeric && registers[y].isNumeric {
            let delta = registers[x].asNumber - registers[y].asNumber
            return registers[target].setFloat(delta * delta)
        }
        guard registers[x].spatialValue != nil, registers[y].spatialValue != nil else { return registers[target].setNothing() }
        let ax = registers[x].x
        let ay = registers[x].y
        let az = registers[x].z
        let bx = registers[y].x
        let by = registers[y].y
        let bz = registers[y].z
        let dx = ax - bx
        let dy = ay - by
        let dz = az - bz
        let squared = dx * dx + dy * dy + dz * dz
        return registers[target].setFloat(squared)
    }
}
