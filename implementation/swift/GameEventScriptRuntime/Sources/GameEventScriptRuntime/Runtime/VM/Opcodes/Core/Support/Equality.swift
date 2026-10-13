// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesMath {
    @inline(__always)
    static func performEqualNotEqual<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ a: GesValue, _ b: GesValue, sink: Output) throws -> Output.Result {
        if a.isNothing || b.isNothing { return sink.nothing }
        return sink.boolean(GesComparison.equal(a, b) == (i.opcode == .equal))
    }
}
