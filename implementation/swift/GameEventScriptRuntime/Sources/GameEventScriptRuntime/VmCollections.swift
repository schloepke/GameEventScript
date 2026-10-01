// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

final class GesIterator {
    private var source: GesValue
    private var index: Int64 = 0
    private var textIndex: String.UnicodeScalarView.Index?
    private var closed = false
    let isPatternSequence: Bool

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

final class GesCollectionBuilder {
    enum Kind { case list, map, distinct, group, order }

    let kind: Kind
    var values: [GesValue] = []
    private var keys: [GesValue] = []
    private var textKeys: [String] = []
    private var groups: [[GesValue]] = []

    init(_ kind: Kind) { self.kind = kind }

    func add(key: GesValue = .nothing, value: GesValue, budget: GesRuntimeBudget) {
        switch kind {
        case .list: if budget.generated(values.count + 1) { values.append(value) }
        case .map:
            let name = key.isNothing ? "" : key.textValue ?? key.toText
            if name.isEmpty { return }
            if let index = textKeys.firstIndex(where: { GesText.scalarEqual($0, name) }) {
                values[index] = value
                return
            }
            if budget.generated(values.count + 1) {
                textKeys.append(name)
                values.append(value)
            }
        case .distinct:
            if keys.contains(key) { return }
            if budget.generated(values.count + 1) {
                keys.append(key)
                values.append(value)
            }
        case .group:
            let name = key.textValue ?? key.toText
            if let index = textKeys.firstIndex(where: { GesText.scalarEqual($0, name) }) {
                if budget.generated(groups[index].count + 1) { groups[index].append(value) }
            } else if budget.generated(groups.count + 1) && budget.generated(1) {
                textKeys.append(name)
                groups.append([value])
            }
        case .order:
            if budget.generated(values.count + 1) {
                keys.append(key)
                values.append(value)
            }
        }
    }

    func finish<Output: GesValueOutput>(descending: Bool = false, sink: Output) -> Output.Result {
        switch kind {
        case .list, .distinct: return sink.list(values)
        case .map: return sink.map(zip(textKeys, values).map { .init(key: $0.0, value: $0.1) })
        case .group: return sink.map(zip(textKeys, groups).map { .init(key: $0.0, value: .list($0.1)) })
        case .order:
            guard let sorted = GesComparison.sorted(values, keys: keys, descending: descending) else { return sink.nothing }
            return sink.list(sorted)
        }
    }
}

extension GesValue {
    var truth: Bool? {
        switch kind {
        case .boolean, .integer, .float, .percentage, .text, .tag, .vector, .point: asBoolean
        default: nil
        }
    }
    var numericOnly: Bool { [.integer, .float, .percentage].contains(kind) }

    func index(_ oneBased: Int64) -> GesValue {
        index(oneBased, sink: GesValueFactory())
    }

    func index<Output: GesValueOutput>(_ oneBased: Int64, sink: Output) -> Output.Result {
        if oneBased <= 0 { return sink.nothing }
        let index = oneBased - 1
        switch kind {
        case .list:
            let values = listValue!
            return index < values.count ? sink.copy(values[Int(index)]) : sink.nothing
        case .dice:
            let rolls = diceRolls!
            return index < rolls.count ? sink.integer(Int64(rolls[Int(index)])) : sink.nothing
        case .vector, .point: return index < 3 ? sink.float(index == 0 ? x : index == 1 ? y : z, unit: unit) : sink.nothing
        case .integerRange:
            guard let term = integerRangeValue!.term(at: oneBased) else { return sink.nothing }
            return sink.integer(term)
        case .floatRange:
            guard let term = floatRangeValue!.term(at: oneBased) else { return sink.nothing }
            return sink.float(term)
        case .text, .tag:
            let text = textValue!.unicodeScalars
            guard index < text.count else { return sink.nothing }
            return sink.text(String(text[text.index(text.startIndex, offsetBy: Int(index))]))
        default: return sink.nothing
        }
    }

    func member(_ key: String) throws -> GesValue {
        try member(key, sink: GesValueFactory())
    }

    func member<Output: GesValueOutput>(_ key: String, sink: Output) throws -> Output.Result {
        if kind == .external { return try sink.copy(externalField(key) ?? .nothing) }
        if let map = asMap { return sink.copy(map.get(key) ?? .nothing) }
        if spatialValue != nil {
            switch key {
            case "x": return sink.float(x, unit: unit)
            case "y": return sink.float(y, unit: unit)
            case "z": return sink.float(z, unit: unit)
            default: return sink.nothing
            }
        }
        if let message = messageValue {
            switch key {
            case "name": return sink.text(message.name)
            case "signature": return sink.text(message.signatureId)
            case "arguments": return sink.map((0..<message.arguments.count).map { .init(key: message.arguments.nameAt($0), value: message.arguments[$0]) })
            case "tags": return sink.list(try message.tags.map(GesValue.tag))
            default: return sink.nothing
            }
        }
        if let signature = signatureValue {
            switch key {
            case "name": return sink.text(signature.name)
            case "signature": return sink.text(signature.signatureId)
            case "parameters": return sink.list(signature.parameters.map(GesValue.text))
            default: return sink.nothing
            }
        }
        return sink.nothing
    }
}
