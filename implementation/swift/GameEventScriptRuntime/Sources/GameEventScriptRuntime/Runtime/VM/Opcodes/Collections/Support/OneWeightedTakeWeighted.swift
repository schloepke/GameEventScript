// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCollectionOperators {
    @inline(__always)
    static func performOneWeightedTakeWeighted<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ slot: GesValue, _ a: GesValue, sink: Output) throws -> Output.Result {
        let op = i.opcode
        return weighted(a, s.value(Int(op == .oneWeighted ? i.word2 : i.a)), count: op == .oneWeighted ? 1 : Int(i.signedWord2), single: op == .oneWeighted, random: c.random, sink: sink)
    }
}
