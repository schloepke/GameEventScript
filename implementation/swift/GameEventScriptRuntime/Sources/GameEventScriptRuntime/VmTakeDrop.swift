// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesTakeDrop {
    static func execute(
        _ op: GameEventScriptBytecodeOpCode, _ slot: GesVmState.Slot, count: Int, random: GameEventScriptRandomGenerator
    ) -> GesValue {
        let source = slot.value
        let count = max(0, count)
        if let series = source.seriesValue {
            if op == .takeFirst { return .list((0..<count).map { GesSeries.term(source, index: Int64($0)) }) }
            if op == .dropFirst {
                let (offset, overflow) = series.offset.addingReportingOverflow(Int64(count))
                return .series(.init(signatureID: series.signatureID, offset: overflow ? .max : offset))
            }
            return .nothing
        }
        if source.integerRangeValue != nil || source.floatRangeValue != nil {
            let length = GesCollectionOperators.length(source)
            if op == .oneRandom {
                return length == 0 ? .nothing : source.index(random.nextInclusiveInteger(0, length - 1) + 1)
            }
            if op == .takeRandom {
                let selectedCount = min(Int64(count), length)
                var indexes: [Int64] = []
                for _ in 0..<selectedCount {
                    if length > Int32.max {
                        var index: Int64
                        repeat { index = random.nextInclusiveInteger(0, length - 1) } while indexes.contains(index)
                        indexes.append(index)
                    } else {
                        // Selecting by remaining rank preserves the reference draw order without allocating the entire range.
                        var index = random.nextInclusiveInteger(0, length - Int64(indexes.count) - 1)
                        for previous in indexes.sorted() { if previous <= index { index += 1 } }
                        indexes.append(index)
                    }
                }
                return .list(indexes.map { source.index($0 + 1) })
            }
            return range(op, source, count: Int64(count))
        }
        if case .iterator(let iterator) = slot, op == .oneRandom {
            defer { iterator.close() }
            var chosen = GesValue.nothing
            var seen: Int64 = 0
            while let value = iterator.next() {
                seen += 1
                if random.nextInclusiveInteger(1, seen) == 1 { chosen = value }
            }
            return chosen
        }
        if case .iterator(let iterator) = slot, op == .takeFirst {
            defer { iterator.close() }
            var values: [GesValue] = []
            while values.count < count, let value = iterator.next() { values.append(value) }
            return .list(values)
        }
        guard let values = GesCollectionOperators.materialize(slot, ranges: false) else { return .nothing }
        if op == .oneRandom {
            return values.isEmpty ? .nothing : values[Int(random.nextInclusiveInteger(0, Int64(values.count - 1)))]
        }
        let number = min(count, values.count)
        let result: [GesValue]
        switch op {
        case .takeFirst: result = Array(values.prefix(number))
        case .takeLast: result = Array(values.suffix(number))
        case .dropFirst: result = Array(values.dropFirst(number))
        case .dropLast: result = Array(values.dropLast(number))
        case .takeRandom:
            var available = Array(values.indices)
            var selected: [GesValue] = []
            for _ in 0..<number {
                selected.append(
                    values[available.remove(at: Int(random.nextInclusiveInteger(0, Int64(available.count - 1))))])
            }
            result = selected
        case .takeHighest, .takeLowest, .dropHighest, .dropLowest:
            let highest = op == .takeHighest || op == .dropHighest
            let drop = op == .dropHighest || op == .dropLowest
            var selected = [Bool](repeating: false, count: values.count)
            var output: [GesValue] = []
            for _ in 0..<number {
                var best: Int?
                for index in values.indices where !selected[index] {
                    if let old = best {
                        let comparison = extremeOrder(values[index], values[old])
                        if highest ? comparison > 0 : comparison < 0 { best = index }
                    } else {
                        best = index
                    }
                }
                selected[best!] = true
                output.append(values[best!])
            }
            result = drop ? values.indices.filter { !selected[$0] }.map { values[$0] } : output
        default: return .nothing
        }
        return source.kind == .dice ? .dice(result.map { Int32($0.asInteger) }) : .list(result)
    }
    private static func range(_ op: GameEventScriptBytecodeOpCode, _ source: GesValue, count: Int64) -> GesValue {
        let length = GesCollectionOperators.length(source)
        let ascending = (source.integerRangeValue?.step ?? 0) > 0 || (source.floatRangeValue?.step ?? 0) > 0
        if op == .takeHighest || op == .takeLowest {
            let reverse = ascending == (op == .takeHighest)
            let ordered = reverse ? GesCollectionOperators.reverseRange(source) : source
            let n = min(count, length)
            if n <= 0 { return .integerRange(from: 0, to: 0, step: 0) }
            return sliced(ordered, first: 1, last: n, preserveEnd: false)
        }
        if op == .dropHighest || op == .dropLowest {
            let dropLast = ascending == (op == .dropHighest)
            return range(dropLast ? .dropLast : .dropFirst, source, count: count)
        }
        let drop = op == .dropFirst || op == .dropLast
        if drop && count == 0 || !drop && count >= length { return source }
        if drop && count >= length || !drop && count == 0 { return .integerRange(from: 0, to: 0, step: 0) }
        switch op {
        case .takeFirst: return sliced(source, first: 1, last: count, preserveEnd: false)
        case .takeLast: return sliced(source, first: length - count + 1, last: length, preserveEnd: true)
        case .dropFirst: return sliced(source, first: count + 1, last: length, preserveEnd: true)
        case .dropLast: return sliced(source, first: 1, last: length - count, preserveEnd: false)
        default: return .nothing
        }
    }
    private static func sliced(_ value: GesValue, first: Int64, last: Int64, preserveEnd: Bool) -> GesValue {
        if let range = value.integerRangeValue {
            guard let from = range.term(at: first), let to = preserveEnd ? range.to : range.term(at: last) else {
                return .nothing
            }
            return .integerRange(from: from, to: to, step: range.step)
        }
        guard let range = value.floatRangeValue, let from = range.term(at: first),
            let to = preserveEnd ? range.to : range.term(at: last)
        else { return .nothing }
        return .floatRange(from: from, to: to, step: range.step)
    }
    private static func extremeOrder(_ a: GesValue, _ b: GesValue) -> Int {
        func unit(_ value: GesUnit) -> Int {
            switch value {
            case .none: 0
            case .degree: 1
            case .meter: 2
            case .second: 3
            }
        }
        func rank(_ value: GesValue) -> Int {
            if value.isNumeric { return 1 }
            switch value.kind {
            case .nothing: return 0
            case .tag: return 1
            case .text: return 2
            case .vector: return 4
            case .point: return 5
            case .series: return 7
            case .message: return 9
            case .handler: return 10
            case .list: return 11
            case .map, .record, .external: return 12
            default: return 8
            }
        }
        if a.isNumeric && b.isNumeric {
            if a.unit != b.unit { return unit(a.unit) < unit(b.unit) ? -1 : 1 }
            return a.asNumber < b.asNumber ? -1 : a.asNumber > b.asNumber ? 1 : 0
        }
        if rank(a) != rank(b) { return rank(a) < rank(b) ? -1 : 1 }
        if a.spatialValue != nil && a.kind == b.kind && a.unit != b.unit { return unit(a.unit) < unit(b.unit) ? -1 : 1 }
        return GesComparison.order(a, b) ?? 0
    }
}
