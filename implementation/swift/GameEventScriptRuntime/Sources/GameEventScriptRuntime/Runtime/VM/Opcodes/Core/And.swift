// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesMath {
    @inline(__always)
    static func executeAnd<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ a: GesValue, _ b: GesValue, sink: Output) throws -> Output.Result {
        if a.truth == false || b.truth == false { return sink.boolean(false) }
        return a.truth != nil && b.truth != nil ? sink.boolean(true) : sink.nothing
    }
}
