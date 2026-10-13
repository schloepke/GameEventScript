// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesMath {
    @inline(__always)
    static func performRandomTakeRandomTakeFloat<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ a: GesValue, _ b: GesValue, sink: Output) throws -> Output.Result {
        if a.unit != b.unit { return sink.nothing }
        if i.opcode == .randomTake, let av = a.integerValue, let bv = b.integerValue { return sink.integer(c.random.nextInclusiveInteger(av, bv), unit: a.unit) }
        return a.asNumber.isFinite && b.asNumber.isFinite ? sink.float(c.random.nextFloat(a.asNumber, b.asNumber), unit: a.unit) : sink.nothing
    }
}
