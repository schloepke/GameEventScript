// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

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
