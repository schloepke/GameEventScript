// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// An ordered map input or output entry. Keys compare by exact Unicode scalar sequence.
public struct GesMapEntry: Hashable {
    /// Text key compared by exact Unicode scalar sequence.
    public let key: String
    /// Value associated with the key, including Nothing.
    public let value: GesValue

    /// Creates an entry without changing its key or value.
    public init(key: String, value: GesValue) {
        self.key = key
        self.value = value
    }

    /// Compares scalar-exact keys and structurally equal values.
    public static func == (left: Self, right: Self) -> Bool { GesText.scalarEqual(left.key, right.key) && left.value == right.value }

    /// Hashes the scalar-exact key and value consistently with equality.
    public func hash(into hasher: inout Hasher) {
        GesText.hashScalars(key, into: &hasher)
        hasher.combine(value)
    }
}

/// An immutable map in ascending Unicode scalar key order with last-entry-wins duplicates.
public struct GesValueMap: Hashable {
    /// Canonical entries in ascending Unicode scalar key order.
    public let entries: [GesMapEntry]

    /// Sorts entries by scalar-exact keys and keeps the last input value for each duplicate key.
    public init(_ entries: [GesMapEntry]) {
        let sorted = entries.enumerated().sorted {
            if GesText.scalarEqual($0.element.key, $1.element.key) { return $0.offset < $1.offset }
            return GesText.scalarLess($0.element.key, $1.element.key)
        }
        var normalized: [GesMapEntry] = []
        normalized.reserveCapacity(sorted.count)
        for item in sorted { if let last = normalized.last, GesText.scalarEqual(last.key, item.element.key) { normalized[normalized.count - 1] = item.element } else { normalized.append(item.element) } }
        self.entries = normalized
    }

    /// Number of distinct keys.
    public var length: Int { entries.count }

    /// Text keys in canonical map order.
    public var keys: [GesValue] { entries.map { .text($0.key) } }

    /// Values in the same canonical order as `keys` and `entries`.
    public var values: [GesValue] { entries.map(\.value) }

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

    /// Reads an entry by its zero-based API index. Invalid indices are caller errors.
    public subscript(index: Int) -> GesMapEntry { entries[index] }
}
