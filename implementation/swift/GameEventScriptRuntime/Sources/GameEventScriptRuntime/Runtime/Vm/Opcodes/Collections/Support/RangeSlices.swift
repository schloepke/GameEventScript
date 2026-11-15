// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    // Items [start, start + count) of a non-empty floating Range, last first when reversed, as FloatRangeSlice of C#.
    // The result stays a Range when one has exactly these items; otherwise it materializes them as a List, which obeys
    // MaxRangeItems like the List cast (specs/Semantics/Determinism.md#iteration-and-ranges).
    func setFloatRangeSlice(_ destination: Int, _ range: GesFloatRange, start: Int64, count: Int64, reversed: Bool) {
        let length = range.count
        guard let first = range.term(at: start + 1), let last = range.term(at: start + count) else { return setNothing(destination) }
        let from = reversed ? last : start == 0 ? range.from : first
        let to = reversed ? (start == 0 ? range.from : first) : (start + count == length ? range.to : last)
        let step = reversed ? -range.step : range.step
        if range.isExactSlice(start: start, count: count, reversed: reversed, GesFloatRange(from: from, to: to, step: step)) {
            return setFloatRange(destination, from: from, to: to, step: step)
        }
        if let budget = valueBudget, !budget.range(count) { return setList(destination, []) }
        if count > Int64(Int32.max) { return setNothing(destination) }
        var items: [GesValue] = []
        items.reserveCapacity(Int(count))
        for offset in 0..<count { items.append(.float(range.term(at: reversed ? start + count - offset : start + offset + 1)!)) }
        setList(destination, items)
    }
}
