// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesNavigation {
    @inline(__always)
    static func executeDistanceSquared<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, sink: Output) -> Output.Result {
        evaluate(.distanceSquared, i, s, sink: sink)
    }
}
