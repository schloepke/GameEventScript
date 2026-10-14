// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    @inline(never)
    func executeNormalize(_ target: Int, _ x: Int) {
        guard registers[x].spatialValue != nil else { return registers[target].setNothing() }
        let ax = registers[x].x
        let ay = registers[x].y
        let az = registers[x].z
        let length = (ax * ax + ay * ay + az * az).squareRoot()
        guard length != 0, !length.isNaN else { return registers[target].setNothing() }
        return registers[target].setVector(x: ax / length, y: ay / length, z: az / length, unit: .none)
    }
}
