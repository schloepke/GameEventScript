// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// The delayed messages of one host as a binary min-heap ordered by deadline, then by send ID. Send IDs grow in scheduling
/// order, so equal deadlines keep their enqueue order (specs/HostRuntime.md#delayed-dispatch). Scheduling and promoting one
/// message cost O(log n) whatever order the deadlines arrive in; the rare discards sort a copy to report in deadline order.
struct GesDelayedQueue {
    private var items: [GesDelayedMessage] = []

    var count: Int { items.count }

    var isEmpty: Bool { items.isEmpty }

    var first: GesDelayedMessage? { items.first }

    mutating func add(_ item: GesDelayedMessage) {
        items.append(item)
        var index = items.count - 1
        while index > 0 {
            let parent = (index - 1) / 2
            if !Self.before(item, items[parent]) { break }
            items[index] = items[parent]
            index = parent
        }
        items[index] = item
    }

    mutating func removeFirst() -> GesDelayedMessage {
        let first = items[0]
        let last = items.removeLast()
        if !items.isEmpty { siftDown(0, last) }
        return first
    }

    /// The entries in deadline order, as a sorted copy.
    var inOrder: [GesDelayedMessage] { items.sorted(by: Self.before) }

    mutating func removeAll(where match: (GesDelayedMessage) -> Bool) {
        items.removeAll(where: match)
        var index = items.count / 2 - 1
        while index >= 0 {
            siftDown(index, items[index])
            index -= 1
        }
    }

    mutating func removeAll() { items.removeAll() }

    private mutating func siftDown(_ start: Int, _ item: GesDelayedMessage) {
        var index = start
        while true {
            var child = index * 2 + 1
            if child >= items.count { break }
            if child + 1 < items.count && Self.before(items[child + 1], items[child]) { child += 1 }
            if !Self.before(items[child], item) { break }
            items[index] = items[child]
            index = child
        }
        items[index] = item
    }

    private static func before(_ left: GesDelayedMessage, _ right: GesDelayedMessage) -> Bool {
        left.deadline < right.deadline || left.deadline == right.deadline && left.id < right.id
    }
}
