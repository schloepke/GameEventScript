// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCollectionOperators {
    @inline(__always)
    static func executeZip<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ slot: GesValue, _ a: GesValue, sink: Output) throws -> Output.Result {
        guard let a = a.listValue, let b = s.value(Int(i.word2)).listValue else { return sink.nothing }
        return sink.list(zip(a, b).map { .map([.init(key: "left", value: $0.0), .init(key: "right", value: $0.1)]) })
    }
}
