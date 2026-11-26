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

    // Discards every entry. Each is reported in queue order before the queue changes, so a throwing observer cannot
    // leave it inconsistent.
    mutating func clear(reportingTo observer: (any GesRuntimeObserver)?) {
        if let observer { for index in 0..<count { if let item = items[(head + index) % items.count] { observer.messageDiscarded(id: item.id) } } }
        while dequeue() != nil {}
    }

    // Removes the failed initialization's outputs and entries it left without recipients, reporting each in queue
    // order first.
    mutating func removeFailedRecipients(discardingOutputsFrom id: Int64, reportingTo observer: (any GesRuntimeObserver)?) {
        if let observer {
            for index in 0..<count {
                if let item = items[(head + index) % items.count], !Self.keeps(item, discardingOutputsFrom: id) { observer.messageDiscarded(id: item.id) }
            }
        }
        let length = count
        for _ in 0..<length { if let item = dequeue(), Self.keeps(item, discardingOutputsFrom: id) { enqueue(item) } }
    }

    // Removes the entry with the send ID, if present, keeping the order of the others.
    mutating func remove(id: Int64) -> Bool {
        var found = false
        let length = count
        for _ in 0..<length {
            guard let item = dequeue() else { break }
            if !found && item.id == id { found = true } else { enqueue(item) }
        }
        return found
    }

    // Discards every entry without reporting it.
    mutating func reset() {
        for index in items.indices { items[index] = nil }
        head = 0
        count = 0
    }

    private static func keeps(_ item: GesPendingMessage, discardingOutputsFrom id: Int64) -> Bool { item.initializationOutputID != id && item.hasRecipients }

    mutating func dequeue() -> GesPendingMessage? {
        if count == 0 { return nil }
        let item = items[head]
        items[head] = nil
        head = (head + 1) % items.count
        count -= 1
        return item
    }
}
