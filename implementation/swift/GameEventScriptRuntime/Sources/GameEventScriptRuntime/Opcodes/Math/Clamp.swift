// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesMath {
    @inline(__always)
    static func executeClamp<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ a: GesValue, _ b: GesValue, sink: Output) throws -> Output.Result {
        var upper = s.value(Int(i.a))
        if i.unitAndFlags & 0x40 != 0 && (upper.kind == .boolean || upper.kind == .dice) { upper = numericOperand(upper) }
        guard a.unit == b.unit, b.unit == upper.unit, a.isNumeric, b.isNumeric, upper.isNumeric else { return sink.nothing }
        if let av = a.integerValue, let bv = b.integerValue, let cv = upper.integerValue { return sink.integer(Swift.min(Swift.max(av, Swift.min(bv, cv)), Swift.max(bv, cv)), unit: a.unit) }
        return sink.float(Swift.min(Swift.max(a.asNumber, Swift.min(b.asNumber, upper.asNumber)), Swift.max(b.asNumber, upper.asNumber)), unit: a.unit)
    }
}
