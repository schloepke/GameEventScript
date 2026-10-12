// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCollectionOperators {
    @inline(__always)
    static func performKeysOfMapValuesOfMapEntriesOfMap<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ slot: GesValue, _ a: GesValue, sink: Output) throws -> Output.Result {
        let op = i.opcode
        guard let map = try a.asMap ?? a.externalMap() else { return sink.nothing }
        if op == .keysOfMap { return sink.list(map.keys) }
        if op == .valuesOfMap { return sink.list(map.values) }
        return sink.list(map.entries.map { .map([.init(key: "key", value: .text($0.key)), .init(key: "value", value: $0.value)]) })
    }
}
