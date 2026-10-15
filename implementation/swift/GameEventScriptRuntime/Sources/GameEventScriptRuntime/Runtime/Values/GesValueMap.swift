// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// One canonical map entry; keys compare by exact Unicode scalar sequence. Storage only; the public map API
// exposes keys and values separately, as in C#.
struct GesMapEntry: Hashable {
    let key: String
    let value: GesValue

    init(key: String, value: GesValue) {
        self.key = key
        self.value = value
    }

    static func == (left: Self, right: Self) -> Bool { GesText.scalarEqual(left.key, right.key) && left.value == right.value }

    func hash(into hasher: inout Hasher) {
        GesText.hashScalars(key, into: &hasher)
        hasher.combine(value)
    }
}

/// An immutable map in ascending Unicode scalar key order with last-entry-wins duplicates.
public struct GesValueMap: Hashable {
    // Canonical entries in ascending Unicode scalar key order, stored contiguously.
    let entries: [GesMapEntry]

    // Sorts entries by scalar-exact keys and keeps the last input value for each duplicate key.
    init(_ entries: [GesMapEntry]) {
        let sorted = entries.enumerated().sorted {
            if GesText.scalarEqual($0.element.key, $1.element.key) { return $0.offset < $1.offset }
            return GesText.scalarLess($0.element.key, $1.element.key)
        }
        var normalized: [GesMapEntry] = []
        normalized.reserveCapacity(sorted.count)
        for item in sorted { if let last = normalized.last, GesText.scalarEqual(last.key, item.element.key) { normalized[normalized.count - 1] = item.element } else { normalized.append(item.element) } }
        self.entries = normalized
    }

    // Trusted entries already in canonical order without duplicate keys (precomputed map layouts).
    init(canonical entries: [GesMapEntry]) { self.entries = entries }

    /// Number of distinct keys.
    public var length: Int { entries.count }

    var keys: [GesValue] { entries.map { .text($0.key) } }

    var values: [GesValue] { entries.map(\.value) }

    /// Looks up an exact scalar key, distinguishing absence from a stored Nothing.
    public func get(_ key: String) -> GesValue? {
        var low = 0
        var high = entries.count
        while low < high {
            let middle = low + (high - low) / 2
            let entry = entries[middle]
            if GesText.scalarEqual(entry.key, key) { return entry.value }
            if GesText.scalarLess(entry.key, key) { low = middle + 1 } else { high = middle }
        }
        return nil
    }

    /// Tests whether a scalar-exact key is present, including keys storing Nothing.
    public func containsKey(_ key: String) -> Bool { get(key) != nil }

    subscript(index: Int) -> GesMapEntry { entries[index] }

    /// The key at a zero-based index in ascending Unicode scalar key order. Invalid indices are caller errors.
    public func key(at index: Int) -> String { entries[index].key }

    /// The value at a zero-based index, in the same order as `key(at:)`. Invalid indices are caller errors.
    public func value(at index: Int) -> GesValue { entries[index].value }
}
