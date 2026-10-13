// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCollectionOperators {
    @inline(__always)
    static func performTakeFirstTakeLastTakeHighestTakeLowestDropFirstDropLastDropHighestDropLowestOneRandomTakeRandom<Output: GesValueOutput>(
        _ i: GameEventScriptBytecodeInstruction,
        _ s: GesVmState,
        _ c: GameEventScriptContext,
        _ slot: GesValue,
        _ a: GesValue,
        sink: Output
    ) throws -> Output.Result {
        let op = i.opcode
        return GesTakeDrop.execute(op, slot, count: Int(i.signedWord2), random: c.random, sink: sink)
    }
}
