// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesMath {
    @inline(__always)
    static func executeDegreeToRadians<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ a: GesValue, _ b: GesValue, sink: Output) throws -> Output.Result {
        return a.isNumeric && a.asNumber.isFinite && (a.unit == .none || a.unit == .degree) ? sink.float(a.asNumber / 180 * Double.pi) : sink.nothing
    }
}
