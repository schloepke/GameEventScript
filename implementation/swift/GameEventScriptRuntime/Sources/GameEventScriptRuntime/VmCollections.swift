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
    func next() -> GesValue? {
        if closed { return nil }
        let result: GesValue?
        switch source.kind {
        case .integerRange: result = source.integerRangeValue!.term(at: index + 1).map { .integer($0) }
        case .floatRange: result = source.floatRangeValue!.term(at: index + 1).map { .float($0) }
        case .list:
            let list = source.listValue!
            result = index < list.count ? list[Int(index)] : nil
        case .dice:
            let dice = source.diceRolls!
            result = index < dice.count ? .integer(Int64(dice[Int(index)])) : nil
        case .map, .record:
            let entries = source.mapEntries!
            result = index < entries.count ? entries[Int(index)].value : nil
        case .vector, .point:
            result = index < 3 ? .float(index == 0 ? source.x : index == 1 ? source.y : source.z) : nil
        case .text, .tag:
            let scalars = source.textValue!.unicodeScalars
            if let current = textIndex, current < scalars.endIndex {
                result = .text(String(scalars[current]))
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
        case .list:
            if budget.generated(values.count + 1) { values.append(value) }
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
    func finish(descending: Bool = false) -> GesValue {
        switch kind {
        case .list, .distinct: return .list(values)
        case .map: return .map(zip(textKeys, values).map { .init(key: $0.0, value: $0.1) })
        case .group: return .map(zip(textKeys, groups).map { .init(key: $0.0, value: .list($0.1)) })
        case .order:
            return GesComparison.sorted(values, keys: keys, descending: descending).map(GesValue.list) ?? .nothing
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
        if oneBased <= 0 { return .nothing }
        let index = oneBased - 1
        switch kind {
        case .list:
            let values = listValue!
            return index < values.count ? values[Int(index)] : .nothing
        case .dice:
            let rolls = diceRolls!
            return index < rolls.count ? .integer(Int64(rolls[Int(index)])) : .nothing
        case .vector, .point: return index < 3 ? .float(index == 0 ? x : index == 1 ? y : z, unit: unit) : .nothing
        case .integerRange: return integerRangeValue!.term(at: oneBased).map { .integer($0) } ?? .nothing
        case .floatRange: return floatRangeValue!.term(at: oneBased).map { .float($0) } ?? .nothing
        case .text, .tag:
            let text = textValue!.unicodeScalars
            guard index < text.count else { return .nothing }
            return .text(String(text[text.index(text.startIndex, offsetBy: Int(index))]))
        default: return .nothing
        }
    }
    func member(_ key: String) throws -> GesValue {
        if kind == .external { return try externalField(key) ?? .nothing }
        if let map = asMap { return map.get(key) ?? .nothing }
        if spatialValue != nil {
            switch key {
            case "x": return .float(x, unit: unit)
            case "y": return .float(y, unit: unit)
            case "z": return .float(z, unit: unit)
            default: return .nothing
            }
        }
        if let message = messageValue {
            switch key {
            case "name": return .text(message.name)
            case "signature": return .text(message.signatureId)
            case "arguments":
                return .map(
                    (0..<message.arguments.count).map {
                        .init(key: message.arguments.nameAt($0), value: message.arguments[$0])
                    })
            case "tags": return .list(try message.tags.map(GesValue.tag))
            default: return .nothing
            }
        }
        if let signature = signatureValue {
            switch key {
            case "name": return .text(signature.name)
            case "signature": return .text(signature.signatureId)
            case "parameters": return .list(signature.parameters.map(GesValue.text))
            default: return .nothing
            }
        }
        return .nothing
    }
}
