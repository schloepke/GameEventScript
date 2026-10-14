// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    @inline(never)
    func executeLengthSquared(_ target: Int, _ x: Int) {
        if registers[x].isNumeric { return registers[target].setFloat(registers[x].asNumber * registers[x].asNumber) }
        return registers[x].spatialValue == nil ? registers[target].setNothing() : registers[target].setFloat(registers[x].x * registers[x].x + registers[x].y * registers[x].y + registers[x].z * registers[x].z)
    }
}
