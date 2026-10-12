// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCollectionOperators {
    @inline(__always)
    static func performSortAscendingSortDescendingReverse<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ slot: GesValue, _ a: GesValue, sink: Output) throws -> Output.Result {
        let op = i.opcode
        if a.integerRangeValue != nil || a.floatRangeValue != nil {
            let descending = (a.integerRangeValue?.step ?? 0) < 0 || (a.floatRangeValue?.step ?? 0) < 0
            if op != .reverse && descending == (op == .sortDescending) { return sink.copy(a) }
            return reverseRange(a, sink: sink)
        }
        guard let values = materialize(slot, ranges: false) else { return sink.nothing }
        if op == .reverse { return sink.list(values.reversed()) }
        guard let sorted = GesComparison.sorted(values, descending: op == .sortDescending) else { return sink.nothing }
        return sink.list(sorted)
    }
}
