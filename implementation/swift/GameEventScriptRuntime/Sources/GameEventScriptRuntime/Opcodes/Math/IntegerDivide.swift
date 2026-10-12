// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesMath {
    @inline(__always)
    static func executeIntegerDivide<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ a: GesValue, _ b: GesValue, sink: Output) throws -> Output.Result {
        return arithmetic(i.opcode, a, b, sink: sink)
    }
}
