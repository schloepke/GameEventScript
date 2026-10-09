// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

protocol GesComponentIterator: AnyObject {
    func moveNext() -> Bool

    func component(_ index: Int) -> GesValue
}

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

final class GesProductIterator: GesIterator, GesComponentIterator {
    private let sources: [GesValue]
    private var iterators: [GesIterator]
    private var components: [GesValue]
    private let cartesian: Bool
    private let budget: GesRuntimeBudget
    private var started = false
    private var closed = false

    init(_ sources: [GesValue], iterators: [GesIterator], cartesian: Bool, budget: GesRuntimeBudget) {
        self.sources = sources
        self.iterators = iterators
        self.cartesian = cartesian
        self.budget = budget
        components = .init(repeating: .nothing, count: sources.count)
        super.init(patternSequence: false)
    }

    func component(_ index: Int) -> GesValue { index < components.count ? components[index] : .nothing }

    func moveNext() -> Bool {
        if closed { return false }
        if !started || !cartesian {
            started = true
            for index in iterators.indices {
                if !advance(index) {
                    close()
                    return false
                }
            }
            return true
        }
        for index in iterators.indices.reversed() {
            if !advance(index) { continue }
            for reset in (index + 1)..<iterators.count {
                iterators[reset].close()
                iterators[reset] = GesIterator(sources[reset])!
                if !advance(reset) {
                    close()
                    return false
                }
            }
            return true
        }
        close()
        return false
    }

    private func advance(_ index: Int) -> Bool {
        guard let value = iterators[index].next() else { return false }
        components[index] = value
        return true
    }

    override func next<Output: GesValueOutput>(sink: Output) -> Output.Result? {
        guard moveNext(), budget.generated(components.count) else { return nil }
        return sink.list(components)
    }

    override func close() {
        if closed { return }
        closed = true
        for iterator in iterators { iterator.close() }
    }
}

final class GesUnionIterator: GesIterator {
    private var sources: [GesIterator]
    private var position = 0
    private var closed = false

    init(_ left: GesIterator, _ right: GesIterator) {
        sources = [left, right]
        super.init(patternSequence: false)
    }

    func append(_ source: GesIterator) { sources.append(source) }

    override func next<Output: GesValueOutput>(sink: Output) -> Output.Result? {
        while !closed && position < sources.count {
            if let value = sources[position].next(sink: sink) { return value }
            position += 1
        }
        return nil
    }

    override func close() {
        if closed { return }
        closed = true
        for source in sources { source.close() }
    }
}

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
