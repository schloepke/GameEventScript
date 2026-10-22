// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

struct GesMessageQueue {
    private var items: [GesPendingMessage?]

    init(capacity: Int = 16) { items = .init(repeating: nil, count: capacity) }

    private var head = 0
    private(set) var count = 0

    mutating func enqueue(_ value: GesPendingMessage) {
        if count == items.count {
            var expanded = [GesPendingMessage?](repeating: nil, count: max(1, items.count * 2))
            for index in 0..<count { expanded[index] = items[(head + index) % items.count] }
            items = expanded
            head = 0
        }
        items[(head + count) % items.count] = value
        count += 1
    }

    mutating func clear() { while dequeue() != nil {} }

    mutating func removeFailedRecipients(discardingOutputsFrom id: Int64) {
        let length = count
        for _ in 0..<length { if let item = dequeue(), item.initializationOutputID != id, item.hasRecipients { enqueue(item) } }
    }

    mutating func dequeue() -> GesPendingMessage? {
        if count == 0 { return nil }
        let item = items[head]
        items[head] = nil
        head = (head + 1) % items.count
        count -= 1
        return item
    }
}
