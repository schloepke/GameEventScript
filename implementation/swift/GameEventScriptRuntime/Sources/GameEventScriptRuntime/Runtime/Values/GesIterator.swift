// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

class GesIterator {
    private var source: GesValue
    private var index: Int64 = 0
    private var textIndex: String.UnicodeScalarView.Index?
    private var closed = false
    let isPatternSequence: Bool

    init(patternSequence: Bool) {
        source = .nothing
        isPatternSequence = patternSequence
    }

    init?(_ source: GesValue) {
        switch source.kind {
        case .integerRange, .floatRange, .list, .dice, .map, .record, .vector, .point, .text, .tag: break
        default: return nil
        }
        self.source = source
        isPatternSequence = [.list, .dice, .map, .record].contains(source.kind)
        textIndex = source.textValue?.unicodeScalars.startIndex
    }

    func next() -> GesValue? { next(sink: GesValueFactory()) }

    func next<Output: GesValueOutput>(sink: Output) -> Output.Result? {
        if closed { return nil }
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
