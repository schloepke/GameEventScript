// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

final class GesEntriesIterator: GesIterator, GesComponentIterator {
    private let keys: [String]
    private let values: [GesValue]
    private let budget: GesRuntimeBudget
    private var position = -1
    private var closed = false

    init(_ map: GesValueMap, budget: GesRuntimeBudget) {
        keys = map.keyStorage
        values = map.valueStorage
        self.budget = budget
        super.init(patternSequence: false)
    }

    func moveNext() -> Bool {
        if closed { return false }
        position += 1
        return position < keys.count
    }

    func component(_ index: Int) -> GesValue {
        if index == 0 { return .text(keys[position]) }
        return index == 1 ? values[position] : .nothing
    }

    override func next<Output: GesValueOutput>(sink: Output) -> Output.Result? {
        guard moveNext(), budget.generated(2) else { return nil }
        return sink.map([.init(key: "key", value: component(0)), .init(key: "value", value: component(1))])
    }

    override func close() { closed = true }
}
