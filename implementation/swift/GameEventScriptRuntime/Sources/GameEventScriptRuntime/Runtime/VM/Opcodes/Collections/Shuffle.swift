// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCollectionOperators {
    @inline(__always)
    static func executeShuffle<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ slot: GesValue, _ a: GesValue, sink: Output) throws -> Output.Result {
        guard var values = materialize(slot) else { return sink.nothing }
        if values.count > 1 { for index in stride(from: values.count - 1, through: 1, by: -1) { values.swapAt(index, Int(c.random.nextInclusiveInteger(0, Int64(index)))) } }
        return sink.list(values)
    }
}
