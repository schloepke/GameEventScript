// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCollectionOperators {
    @inline(__always)
    static func performFirstLastSingle<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ slot: GesValue, _ a: GesValue, sink: Output) throws -> Output.Result {
        let op = i.opcode
        if let iterator = slot.iteratorValue {
            defer { iterator.close() }
            guard let first = iterator.next() else { return sink.nothing }
            if op == .first { return sink.copy(first) }
            if op == .single { return iterator.next() == nil ? sink.copy(first) : sink.nothing }
            var last = first
            while let value = iterator.next() { last = value }
            return sink.copy(last)
        }
        guard let iterator = GesIterator(a) else { return sink.nothing }
        if op == .first { return iterator.next(sink: sink) ?? sink.nothing }
        if op == .single { return length(a) == 1 ? iterator.next(sink: sink) ?? sink.nothing : sink.nothing }
        if let values = a.mapEntries { return sink.copy(values.last?.value ?? .nothing) }
        if a.spatialValue != nil { return sink.float(a.z) }
        return a.index(length(a), sink: sink)
    }
}
