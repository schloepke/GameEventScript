// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

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
