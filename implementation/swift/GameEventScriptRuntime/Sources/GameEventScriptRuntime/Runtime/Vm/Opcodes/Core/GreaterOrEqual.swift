// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeGreaterOrEqual(_ target: Int, _ x: Int, _ y: Int) {
        // Handles have no numeric view, so ordering them gives nothing (specs/Language.md#handles).
        if registers[x].isNothing || registers[y].isNothing || registers[x].kind == .handle || registers[y].kind == .handle { return registers[target].setNothing() }
        if !registers[x].isNumeric || !registers[y].isNumeric || registers[x].unit != registers[y].unit { return registers[target].setBoolean(false) }
        switch registers[x].kind {
        case .integer where registers[y].kind == .integer:
            let av = registers[x].integerValue!
            let bv = registers[y].integerValue!
            return registers[target].setBoolean(av >= bv)
        default: break
        }
        return registers[target].setBoolean(registers[x].asNumber >= registers[y].asNumber)
    }
}
