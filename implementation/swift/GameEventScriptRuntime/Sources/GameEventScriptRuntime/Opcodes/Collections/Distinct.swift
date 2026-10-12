// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCollectionOperators {
    @inline(__always)
    static func executeDistinct<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ slot: GesValue, _ a: GesValue, sink: Output) throws -> Output.Result {
        guard let values = materialize(slot, ranges: false) else { return sink.nothing }
        var seen = Set<GesValue>()
        var result: [GesValue] = []
        for value in values where seen.insert(value).inserted { result.append(value) }
        return a.kind == .dice ? sink.dice(result.map { Int32($0.asInteger) }) : sink.list(result)
    }
}
