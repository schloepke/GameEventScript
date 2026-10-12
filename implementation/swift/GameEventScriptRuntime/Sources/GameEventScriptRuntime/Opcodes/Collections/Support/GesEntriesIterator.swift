// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

final class GesEntriesIterator: GesIterator, GesComponentIterator {
    private let entries: [GesMapEntry]
    private let budget: GesRuntimeBudget
    private var position = -1
    private var closed = false

    init(_ entries: [GesMapEntry], budget: GesRuntimeBudget) {
        self.entries = entries
        self.budget = budget
        super.init(patternSequence: false)
    }

    func moveNext() -> Bool {
        if closed { return false }
        position += 1
        return position < entries.count
    }

    func component(_ index: Int) -> GesValue {
        if index == 0 { return .text(entries[position].key) }
        return index == 1 ? entries[position].value : .nothing
    }

    override func next<Output: GesValueOutput>(sink: Output) -> Output.Result? {
        guard moveNext(), budget.generated(2) else { return nil }
        return sink.map([.init(key: "key", value: component(0)), .init(key: "value", value: component(1))])
    }

    override func close() { closed = true }
}
