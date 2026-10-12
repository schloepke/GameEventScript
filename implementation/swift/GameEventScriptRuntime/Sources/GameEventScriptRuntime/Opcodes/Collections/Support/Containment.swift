// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCollectionOperators {
    @inline(__always)
    static func performContainsContainsValueContainsAnyContainsAll<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ slot: GesValue, _ a: GesValue, sink: Output) throws -> Output.Result {
        let op = i.opcode
        let right = s.slot(Int(i.word2))
        let b = right.registerValue
        if right.isRegisterData, b.isNothing { return sink.nothing }
        if op == .containsValue {
            if let entries = b.mapEntries { return sink.boolean(entries.contains { $0.value == a }) }
            return sink.boolean(b.spatialValue != nil && (1...3).contains { b.index(Int64($0)) == a })
        }
        if op == .contains { return sink.boolean(contains(a, right)) }
        let all = op == .containsAll
        let candidates = [.list, .dice, .text, .tag, .integerRange, .floatRange].contains(a.kind) ? GesIterator(a) : nil
        guard let candidates else { return sink.boolean(all) }
        defer { candidates.close() }
        let container: GesValue
        if let iterator = right.iteratorValue { container = .list(read(iterator)) } else { container = right }
        while let candidate = candidates.next() { if contains(candidate, container) != all { return sink.boolean(!all) } }
        return sink.boolean(all)
    }
}
