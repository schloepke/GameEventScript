// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Base of all iterators. Source iterators are dedicated subclasses per source kind, as in C#, so a step never
/// re-dispatches on the source kind or re-reads its payload. Compound iterators override `next(sink:)`.
class GesIterator {
    fileprivate var exhausted = false
    let isPatternSequence: Bool
    /// Whether this is a `GesIntegerRangeIterator`, whose statically dispatched `nextIntegerTerm()` VM dispatch uses.
    final let isIntegerRange: Bool

    init(patternSequence: Bool) {
        isPatternSequence = patternSequence
        isIntegerRange = false
    }

    fileprivate init(patternSequence: Bool, integerRange: Bool) {
        isPatternSequence = patternSequence
        isIntegerRange = integerRange
    }

    /// Creates the dedicated iterator for an iterable source, or nil for a non-iterable value.
    static func make(_ source: GesValue) -> GesIterator? {
        switch source.kind {
        case .integerRange: return GesIntegerRangeIterator(source.integerRangeValue!)
        case .floatRange: return GesFloatRangeIterator(source.floatRangeValue!)
        case .list: return GesListIterator(source.listValue!)
        case .dice: return GesIntIterator(source.diceRolls!)
        case .map, .record: return GesMapValueIterator(source.asMap!)
        case .vector, .point: return GesTripletIterator(x: source.x, y: source.y, z: source.z)
        case .text, .tag: return GesStringIterator(source.textValue!)
        default: return nil
        }
    }

    /// The next value, or nil once exhausted. Source iterators override this; compound iterators override
    /// `next(sink:)`, which this default forwards to.
    func next() -> GesValue? { next(sink: GesValueFactory()) }

    /// The next value written through `sink`, or nil once exhausted.
    func next<Output: GesValueOutput>(sink: Output) -> Output.Result? { next().map { sink.copy($0) } }

    /// Writes the next value directly into a VM register and reports whether one existed. Source iterators override
    /// this non-generic step so VM dispatch avoids an intermediate value and generic dispatch.
    func next(into output: GesRegisterOutput) -> Bool { next(sink: output) != nil }

    func close() { exhausted = true }
}

final class GesIntegerRangeIterator: GesIterator {
    // For every valid index the next term is the previous one plus `step`, which equals
    // GesIntegerRange.term(at:) without its per-step multiplication and bound checks.
    private let count: Int64
    private let step: Int64
    private var term: Int64
    private var index: Int64 = 0

    init(_ range: GesIntegerRange) {
        count = range.count
        step = range.step
        term = range.from
        super.init(patternSequence: false, integerRange: true)
    }

    // Statically dispatched step for the hot integer-range loop; same sequence as next().
    @inline(__always)
    func nextIntegerTerm() -> Int64? {
        guard !exhausted, index < count else {
            exhausted = true
            return nil
        }
        let current = term
        if index == .max - 1 {
            exhausted = true
        } else {
            index += 1
            // Only advance to a term that exists, so the addition cannot overflow.
            if index < count { term &+= step }
        }
        return current
    }

    override func next() -> GesValue? { nextIntegerTerm().map { .integer($0) } }

    override func next(into output: GesRegisterOutput) -> Bool {
        guard let term = nextIntegerTerm() else { return false }
        output.integer(term, unit: .none)
        return true
    }
}

final class GesFloatRangeIterator: GesIterator {
    private let range: GesFloatRange
    private var index: Int64 = 0

    init(_ range: GesFloatRange) {
        self.range = range
        super.init(patternSequence: false, integerRange: false)
    }

    override func next() -> GesValue? {
        if exhausted { return nil }
        guard let term = range.term(at: index + 1) else {
            exhausted = true
            return nil
        }
        if index == .max - 1 { exhausted = true } else { index += 1 }
        return .float(term)
    }

    override func next(into output: GesRegisterOutput) -> Bool {
        guard let value = next() else { return false }
        output.float(value.asNumber, unit: .none)
        return true
    }
}

final class GesListIterator: GesIterator {
    private var values: [GesValue]
    private var index = 0

    init(_ values: [GesValue]) {
        self.values = values
        super.init(patternSequence: true, integerRange: false)
    }

    override func next() -> GesValue? {
        guard !exhausted, index < values.count else {
            exhausted = true
            return nil
        }
        defer { index += 1 }
        return values[index]
    }

    override func next(into output: GesRegisterOutput) -> Bool {
        guard !exhausted, index < values.count else {
            exhausted = true
            return false
        }
        output.copy(values[index])
        index += 1
        return true
    }

    override func close() {
        super.close()
        values = []
    }
}

// Iterates map and record values in key order over the map's existing entries. Sharing the entry array avoids
// materializing a value array at iterator creation, so early exits stay O(1) to set up.
final class GesMapValueIterator: GesIterator {
    private var entries: [GesMapEntry]
    private var index = 0

    init(_ map: GesValueMap) {
        entries = map.entries
        super.init(patternSequence: true, integerRange: false)
    }

    override func next() -> GesValue? {
        guard !exhausted, index < entries.count else {
            exhausted = true
            return nil
        }
        defer { index += 1 }
        return entries[index].value
    }

    override func next(into output: GesRegisterOutput) -> Bool {
        guard !exhausted, index < entries.count else {
            exhausted = true
            return false
        }
        output.copy(entries[index].value)
        index += 1
        return true
    }

    override func close() {
        super.close()
        entries = []
    }
}

final class GesIntIterator: GesIterator {
    private var values: [Int32]
    private var index = 0

    init(_ values: [Int32]) {
        self.values = values
        super.init(patternSequence: true, integerRange: false)
    }

    override func next() -> GesValue? {
        guard !exhausted, index < values.count else {
            exhausted = true
            return nil
        }
        defer { index += 1 }
        return .integer(Int64(values[index]))
    }

    override func next(into output: GesRegisterOutput) -> Bool {
        guard !exhausted, index < values.count else {
            exhausted = true
            return false
        }
        output.integer(Int64(values[index]), unit: .none)
        index += 1
        return true
    }

    override func close() {
        super.close()
        values = []
    }
}

final class GesStringIterator: GesIterator {
    private var scalars: String.UnicodeScalarView
    private var position: String.UnicodeScalarView.Index

    init(_ text: String) {
        scalars = text.unicodeScalars
        position = scalars.startIndex
        super.init(patternSequence: false, integerRange: false)
    }

    override func next() -> GesValue? {
        guard !exhausted, position < scalars.endIndex else {
            exhausted = true
            return nil
        }
        let scalar = scalars[position]
        position = scalars.index(after: position)
        return .text(String(scalar))
    }

    override func close() {
        super.close()
        scalars = "".unicodeScalars
        position = scalars.startIndex
    }
}

final class GesTripletIterator: GesIterator {
    private let components: (Double, Double, Double)
    private var index = 0

    init(x: Double, y: Double, z: Double) {
        components = (x, y, z)
        super.init(patternSequence: false, integerRange: false)
    }

    override func next() -> GesValue? {
        guard !exhausted, index < 3 else {
            exhausted = true
            return nil
        }
        defer { index += 1 }
        return .float(index == 0 ? components.0 : index == 1 ? components.1 : components.2)
    }
}
