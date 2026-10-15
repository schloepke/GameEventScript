// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

final class GesMultisetIterator: GesIterator {
    private let left: GesIterator
    private var stages: [(values: [GesValue], used: [Bool])]
    private let intersect: Bool
    private let budget: GesRuntimeBudget
    private var closed = false

    init(_ left: GesIterator, _ right: [GesValue], intersect: Bool, budget: GesRuntimeBudget) {
        self.left = left
        self.intersect = intersect
        self.budget = budget
        stages = [(right, .init(repeating: false, count: right.count))]
        super.init(patternSequence: false)
    }

    func append(_ values: [GesValue]) { stages.append((values, .init(repeating: false, count: values.count))) }

    override func next<Output: GesValueOutput>(sink: Output) -> Output.Result? {
        while !closed && !budget.isExhausted {
            guard let candidate = left.next() else { return nil }
            var accepted = true
            for stage in stages.indices {
                guard budget.loop() else { return nil }
                var found = false
                for index in stages[stage].values.indices {
                    guard budget.loop() else { return nil }
                    if stages[stage].used[index] || candidate != stages[stage].values[index] { continue }
                    stages[stage].used[index] = true
                    found = true
                    break
                }
                if found == intersect { continue }
                accepted = false
                break
            }
            if accepted { return sink.copy(candidate) }
        }
        return nil
    }

    override func close() {
        if closed { return }
        closed = true
        left.close()
    }
}
