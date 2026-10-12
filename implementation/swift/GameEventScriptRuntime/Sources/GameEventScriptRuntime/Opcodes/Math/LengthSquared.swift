// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesNavigation {
    @inline(__always)
    static func executeLengthSquared<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, sink: Output) -> Output.Result {
        let a = s.value(Int(i.word1))
        if a.isNumeric { return sink.float(a.asNumber * a.asNumber) }
        return a.spatialValue == nil ? sink.nothing : sink.float(a.x * a.x + a.y * a.y + a.z * a.z)
    }
}
