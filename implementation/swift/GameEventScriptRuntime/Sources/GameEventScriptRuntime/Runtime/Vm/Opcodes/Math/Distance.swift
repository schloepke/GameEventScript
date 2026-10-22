// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeDistance(_ target: Int, _ x: Int, _ y: Int) {
        guard registers[x].unit == registers[y].unit else { return registers[target].setNothing() }
        if registers[x].isNumeric && registers[y].isNumeric {
            let delta = registers[x].asNumber - registers[y].asNumber
            return registers[target].setFloat(Swift.abs(delta), unit: registers[x].unit)
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
        return registers[target].setFloat(squared.squareRoot(), unit: registers[x].unit)
    }
}
