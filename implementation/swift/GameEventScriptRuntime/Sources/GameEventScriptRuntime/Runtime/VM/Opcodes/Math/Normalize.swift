// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesNavigation {
    @inline(__always)
    static func executeNormalize<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, sink: Output) -> Output.Result {
        let a = s.value(Int(i.word1))
        return a.spatialValue == nil ? sink.nothing : normalize(a.x, a.y, a.z, sink: sink)
    }
}
