// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

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
