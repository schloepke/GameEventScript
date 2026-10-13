// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesMath {
    @inline(__always)
    static func executeWrapDegree<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ a: GesValue, _ b: GesValue, sink: Output) throws -> Output.Result {
        if !a.isNumeric || !a.asNumber.isFinite || (a.unit != .none && a.unit != .degree) { return sink.nothing }
        var value = a.asNumber.truncatingRemainder(dividingBy: 360)
        if value < 0 { value += 360 }
        return sink.float(value == 360 ? 0 : value, unit: .degree)
    }
}
