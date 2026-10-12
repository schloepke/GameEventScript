// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesMath {
    @inline(__always)
    static func executeMin<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ a: GesValue, _ b: GesValue, sink: Output) throws -> Output.Result {
        return sink.copy(GesComparison.extreme(a, b, maximum: i.opcode == .max))
    }
}
