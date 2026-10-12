// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesNavigation {
    @inline(__always)
    static func executeCross<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, sink: Output) -> Output.Result {
        evaluate(.cross, i, s, sink: sink)
    }
}
