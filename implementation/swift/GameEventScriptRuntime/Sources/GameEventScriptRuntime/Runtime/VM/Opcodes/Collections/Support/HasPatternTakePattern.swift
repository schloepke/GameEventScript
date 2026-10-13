// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCollectionOperators {
    @inline(__always)
    static func performHasPatternTakePattern<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ slot: GesValue, _ a: GesValue, sink: Output) throws -> Output.Result {
        let op = i.opcode
        return GesPatterns.execute(slot, pattern: GameEventScriptBytecodePatternKind(rawValue: i.a)!, count: Int(i.signedWord2), face: i.a == 1 ? s.value(Int(i.b)) : .nothing, take: op == .takePattern, sink: sink)
    }
}
