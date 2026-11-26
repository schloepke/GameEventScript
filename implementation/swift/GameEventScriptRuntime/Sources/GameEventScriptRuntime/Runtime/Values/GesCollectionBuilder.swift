// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

final class GesCollectionBuilder {
    enum Kind { case list, map, distinct, group, order }

    let kind: Kind
    var values: [GesValue] = []
    private var keys: [GesValue] = []
    private var textKeys: [String] = []
    private var groups: [[GesValue]] = []
    // The handle key of each Map or group slot, empty until the first handle key; a slot with a handle has no Text key.
    private var handleKeys: [GesHandle?] = []

    init(_ kind: Kind) { self.kind = kind }

    func add(key: GesValue = .nothing, value: GesValue, budget: GesRuntimeBudget) {
        switch kind {
        case .list: if budget.generated(values.count + 1) { values.append(value) }
        case .map:
            // The last value and key of an equal handle win, as in GesValueMap.fromKeyValues.
            if let handle = key.handleValue {
                if let index = handleIndex(handle) {
                    handleKeys[index] = handle
                    values[index] = value
                } else if budget.generated(values.count + 1) {
                    appendHandle(handle)
                    values.append(value)
                }
                return
            }
            let name = key.isNothing ? "" : key.textValue ?? key.toText
            if name.isEmpty { return }
            if let index = textIndex(name) {
                values[index] = value
                return
            }
            if budget.generated(values.count + 1) {
                appendText(name)
                values.append(value)
            }
        case .distinct:
            if keys.contains(key) { return }
            if budget.generated(values.count + 1) {
                keys.append(key)
                values.append(value)
            }
        case .group:
            if let handle = key.handleValue {
                if let index = handleIndex(handle) {
                    if budget.generated(groups[index].count + 1) { groups[index].append(value) }
                } else if budget.generated(groups.count + 1) && budget.generated(1) {
                    appendHandle(handle)
                    groups.append([value])
                }
                return
            }
            let name = key.textValue ?? key.toText
            if let index = textIndex(name) {
                if budget.generated(groups[index].count + 1) { groups[index].append(value) }
            } else if budget.generated(groups.count + 1) && budget.generated(1) {
                appendText(name)
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
        case .map:
            if !handleKeys.isEmpty { return sink.map(mapWithHandles(values)) }
            return sink.map(zip(textKeys, values).map { .init(key: $0.0, value: $0.1) })
        case .group:
            if !handleKeys.isEmpty { return sink.map(mapWithHandles(groups.map { .list($0) })) }
            return sink.map(zip(textKeys, groups).map { .init(key: $0.0, value: .list($0.1)) })
        case .order:
            guard let sorted = GesComparison.sorted(values, keys: keys, descending: descending) else { return sink.nothing }
            return sink.list(sorted)
        }
    }

    private func textIndex(_ name: String) -> Int? {
        if handleKeys.isEmpty { return textKeys.firstIndex { GesText.scalarEqual($0, name) } }
        return textKeys.indices.first { handleKeys[$0] == nil && GesText.scalarEqual(textKeys[$0], name) }
    }

    private func handleIndex(_ handle: GesHandle) -> Int? { handleKeys.firstIndex { $0 == handle } }

    private func appendText(_ name: String) {
        textKeys.append(name)
        if !handleKeys.isEmpty { handleKeys.append(nil) }
    }

    private func appendHandle(_ handle: GesHandle) {
        if handleKeys.isEmpty { handleKeys = Array(repeating: nil, count: textKeys.count) }
        textKeys.append("")
        handleKeys.append(handle)
    }

    // A Map of the Text keys and the handle keys of the slots.
    private func mapWithHandles(_ slotValues: [GesValue]) -> GesValueMap {
        let keys = textKeys.indices.map { handleKeys[$0].map(GesValue.handle) ?? .text(textKeys[$0]) }
        return GesValueMap.fromKeyValues(keys, slotValues)!
    }
}
