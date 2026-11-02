// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

extension GesVirtualMachine {
    // A percentage scales a quantity by its own magnitude, which a logarithmic decibel level does not support.
    func percentageMismatch(_ x: Int, _ y: Int) -> Bool {
        registers[x].kind == .percentage && !registers[y].unit.acceptsPercentage || registers[y].kind == .percentage && !registers[x].unit.acceptsPercentage
    }
}

extension GesMath {
    // A percentage scales a quantity by its own magnitude, which a logarithmic decibel level does not support.
    static func percentageMismatch(_ a: GesValue, _ b: GesValue) -> Bool {
        a.kind == .percentage && !b.unit.acceptsPercentage || b.kind == .percentage && !a.unit.acceptsPercentage
    }
}
