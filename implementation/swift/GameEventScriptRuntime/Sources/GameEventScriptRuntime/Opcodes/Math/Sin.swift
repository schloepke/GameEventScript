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
    static func executeSin<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, sink: Output) -> Output.Result {
        let a = s.value(Int(i.word1))
        guard a.isNumeric, a.unit == .none || a.unit == .degree else { return sink.nothing }
        let n = a.unit == .degree ? a.asNumber / 180 * Double.pi : a.asNumber
        return sink.float(sin(n))
    }
}
