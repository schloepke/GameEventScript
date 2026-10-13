// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

class GesIterator {
    private var source: GesValue
    // Integer ranges are the hot loop form; iterate unboxed state without touching `source`.
    // For every valid index the next term is the previous one plus `rangeStep`, which equals
    // GesIntegerRange.term(at:) without its per-step multiplication and bound checks.
    private let integerRange: GesIntegerRange?
    private let rangeCount: Int64
    private let rangeStep: Int64
    private var rangeTerm: Int64
    private var index: Int64 = 0
    private var textIndex: String.UnicodeScalarView.Index?
    private var closed = false
    let isPatternSequence: Bool

    init(patternSequence: Bool) {
        source = .nothing
        integerRange = nil
        rangeCount = 0
        rangeStep = 0
        rangeTerm = 0
        isSourceIterator = false
        isPatternSequence = patternSequence
    }

    init?(_ source: GesValue) {
        switch source.kind {
        case .integerRange, .floatRange, .list, .dice, .map, .record, .vector, .point, .text, .tag: break
        default: return nil
        }
        self.source = source
        integerRange = source.integerRangeValue
        rangeCount = integerRange?.count ?? 0
        rangeStep = integerRange?.step ?? 0
        rangeTerm = integerRange?.from ?? 0
        isSourceIterator = true
        isPatternSequence = [.list, .dice, .map, .record].contains(source.kind)
        textIndex = source.textValue?.unicodeScalars.startIndex
    }

    func next() -> GesValue? { next(sink: GesValueFactory()) }

    /// Whether `nextIntegerTerm()` applies; subclasses never iterate a plain integer range.
    final var isIntegerRange: Bool { integerRange != nil }
    /// Whether this iterator walks a source value; subclasses never do, so `nextFromSource` applies.
    final let isSourceIterator: Bool

    // Statically dispatched step for the hot integer-range loop; same semantics as next(sink:).
    @inline(__always)
    final func nextIntegerTerm() -> Int64? {
        guard !closed, index < rangeCount else {
            closed = true
            return nil
        }
        let term = rangeTerm
        if index == .max - 1 {
            closed = true
        } else {
            index += 1
            // Only advance to a term that exists, so the addition cannot overflow.
            if index < rangeCount { rangeTerm &+= rangeStep }
        }
        return term
    }

    func next<Output: GesValueOutput>(sink: Output) -> Output.Result? { nextFromSource(sink: sink) }

    // Statically dispatched, so callers with a concrete sink get a specialized copy; kept out
    // of line because inlining it into VM dispatch regressed the compute benchmark.
    @inline(never)
    final func nextFromSource<Output: GesValueOutput>(sink: Output) -> Output.Result? {
        if closed { return nil }
        if integerRange != nil { return nextIntegerTerm().map { sink.integer($0) } }
        let result: Output.Result?
        switch source.kind {
        case .integerRange: result = source.integerRangeValue!.term(at: index + 1).map { sink.integer($0) }
        case .floatRange: result = source.floatRangeValue!.term(at: index + 1).map { sink.float($0) }
        case .list:
            let list = source.listValue!
            result = index < list.count ? sink.copy(list[Int(index)]) : nil
        case .dice:
            let dice = source.diceRolls!
            result = index < dice.count ? sink.integer(Int64(dice[Int(index)])) : nil
        case .map, .record:
            let entries = source.mapEntries!
            result = index < entries.count ? sink.copy(entries[Int(index)].value) : nil
        case .vector, .point: result = index < 3 ? sink.float(index == 0 ? source.x : index == 1 ? source.y : source.z) : nil
        case .text, .tag:
            let scalars = source.textValue!.unicodeScalars
            if let current = textIndex, current < scalars.endIndex {
                result = sink.text(String(scalars[current]))
                textIndex = scalars.index(after: current)
            } else {
                result = nil
            }
        default: result = nil
        }
        if result == nil || index == .max - 1 { closed = true } else { index += 1 }
        return result
    }

    func close() {
        closed = true
        source = .nothing
        textIndex = nil
    }
}
