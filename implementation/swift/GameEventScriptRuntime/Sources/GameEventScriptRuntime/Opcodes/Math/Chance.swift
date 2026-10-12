// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesMath {
    @inline(__always)
    static func executeChance<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ a: GesValue, _ b: GesValue, sink: Output) throws -> Output.Result {
        if !a.numericOnly || a.hasUnit || !a.asNumber.isFinite { return sink.nothing }
        let ratio = a.kind == .integer || (a.kind == .float && Swift.abs(a.asNumber) > 1) ? a.asNumber / 100 : a.asNumber
        return sink.boolean(ratio <= 0 ? false : ratio >= 1 ? true : c.random.nextFloat(0, 1) < ratio)
    }
}
