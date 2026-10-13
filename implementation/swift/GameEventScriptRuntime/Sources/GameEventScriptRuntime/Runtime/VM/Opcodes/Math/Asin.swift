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
    static func executeAsin<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, sink: Output) -> Output.Result {
        let a = s.value(Int(i.word1))
        guard a.isNumeric, !a.hasUnit else { return sink.nothing }
        return sink.float(asin(a.asNumber))
    }
}
