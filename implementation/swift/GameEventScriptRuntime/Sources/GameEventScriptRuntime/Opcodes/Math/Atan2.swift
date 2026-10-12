// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

#if canImport(Darwin)
    import Darwin
#elseif canImport(Glibc)
    import Glibc
#elseif canImport(Musl)
    import Musl
#endif

extension GesNavigation {
    @inline(__always)
    static func executeAtan2<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, sink: Output) -> Output.Result {
        let a = s.value(Int(i.word1))
        let b = s.value(Int(i.word2))
        if !a.isNumeric || !b.isNumeric || a.unit != b.unit { return sink.nothing }
        return sink.float(a.asNumber == 0 && b.asNumber == 0 ? 0 : atan2(a.asNumber, b.asNumber))
    }
}
