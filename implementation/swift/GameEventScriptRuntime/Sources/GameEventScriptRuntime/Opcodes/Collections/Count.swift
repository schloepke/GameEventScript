// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCollectionOperators {
    @inline(__always)
    static func executeCount<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ slot: GesValue, _ a: GesValue, sink: Output) throws -> Output.Result {
        if let iterator = slot.iteratorValue {
            defer { iterator.close() }
            var count: Int64 = 0
            while iterator.next() != nil { count += 1 }
            return sink.integer(count)
        }
        if a.isNothing { return sink.integer(0) }
        if a.kind == .series || a.kind == .external || GesIterator(a) == nil { return sink.nothing }
        return sink.integer(length(a))
    }
}
