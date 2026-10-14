// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

#if canImport(Darwin)
    import Darwin
#elseif canImport(Glibc)
    import Glibc
#elseif canImport(Musl)
    import Musl
#endif

extension GesVirtualMachine {
    @inline(never)
    func executePower(_ target: Int, _ x: Int, _ y: Int) {
        if registers[x].isNothing || registers[y].isNothing { return registers[target].setNothing() }
        let left = registers[x].asNumber
        let right = registers[y].asNumber
        if registers[y].hasUnit || left.isNaN || right.isNaN || (registers[x].hasUnit && right != 0 && right != 1) { return registers[target].setNothing() }
        return registers[target].setFloat(pow(left, right), unit: right == 1 ? registers[x].unit : .none)
    }

    @inline(never)
    func executePowerNumeric(_ target: Int, _ x: Int, _ y: Int) {
        guard registers[x].isNumeric, registers[y].isNumeric else { return registers[target].setNothing() }
        let left = registers[x].asNumber
        let right = registers[y].asNumber
        if registers[y].hasUnit || left.isNaN || right.isNaN || (registers[x].hasUnit && right != 0 && right != 1) { return registers[target].setNothing() }
        return registers[target].setFloat(pow(left, right), unit: right == 1 ? registers[x].unit : .none)
    }
}
