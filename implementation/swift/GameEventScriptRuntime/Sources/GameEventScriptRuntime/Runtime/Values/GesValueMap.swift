// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

// One canonical map entry; keys compare by exact Unicode scalar sequence. Storage only; the public map API
// exposes keys and values separately, as in C#.
struct GesMapEntry: Hashable {
    let key: String
    let value: GesValue

    static func == (left: Self, right: Self) -> Bool { GesText.scalarEqual(left.key, right.key) && left.value == right.value }

    func hash(into hasher: inout Hasher) {
        GesText.hashScalars(key, into: &hasher)
        hasher.combine(value)
    }
}

/// An immutable map in ascending Unicode scalar key order with last-entry-wins duplicates. Handle keys follow every
/// Text key, in handle order.
public struct GesValueMap: Hashable {
    // Canonical keys in ascending Unicode scalar order and their values at the same positions. Maps built from
    // one constant key layout share its key array. Handle keys follow the Text keys; keyStorage holds their text forms.
    let keyStorage: [String]
    let valueStorage: [GesValue]
    // The handle keys after the first textCount Text keys; empty without handle keys.
    let handleStorage: [GesHandle]
    let textCount: Int

    // Sorts entries by scalar-exact keys and keeps the last input value for each duplicate key.
    init(_ entries: [GesMapEntry]) {
        let sorted = entries.enumerated().sorted {
            if GesText.scalarEqual($0.element.key, $1.element.key) { return $0.offset < $1.offset }
            return GesText.scalarLess($0.element.key, $1.element.key)
        }
        var keys: [String] = []
        var values: [GesValue] = []
        keys.reserveCapacity(sorted.count)
        values.reserveCapacity(sorted.count)
        for item in sorted {
            if let last = keys.last, GesText.scalarEqual(last, item.element.key) {
                values[values.count - 1] = item.element.value
            } else {
                keys.append(item.element.key)
                values.append(item.element.value)
            }
        }
        keyStorage = keys
        valueStorage = values
        handleStorage = []
        textCount = keys.count
    }

    // Trusted entries already in canonical order without duplicate keys.
    init(canonical entries: [GesMapEntry]) {
        keyStorage = entries.map(\.key)
        valueStorage = entries.map(\.value)
        handleStorage = []
        textCount = keyStorage.count
    }

    // Trusted canonical keys (shared layout) and values at the same positions.
    init(canonicalKeys: [String], values: [GesValue]) {
        keyStorage = canonicalKeys
        valueStorage = values
        handleStorage = []
        textCount = canonicalKeys.count
    }

    // Trusted canonical keys with handle keys: unique Text keys in scalar order, then unique handles in handle order,
    // whose values follow those of the Text keys.
    init(canonicalKeys: [String], handles: [GesHandle], values: [GesValue]) {
        keyStorage = handles.isEmpty ? canonicalKeys : canonicalKeys + handles.map(\.description)
        valueStorage = values
        handleStorage = handles
        textCount = canonicalKeys.count
    }

    // A map of Text and handle keys in any order; the last value of an equal key wins. Nil when a key is neither a
    // Text nor a handle.
    static func fromKeyValues(_ keys: [GesValue], _ values: [GesValue]) -> GesValueMap? {
        var texts: [GesMapEntry] = []
        var handles: [GesHandle: (key: GesHandle, value: GesValue)] = [:]
        for index in keys.indices {
            let key = keys[index]
            if key.valueKind == .text {
                texts.append(GesMapEntry(key: key.textValue!, value: values[index]))
            } else if let handle = key.handleValue {
                handles[handle] = (handle, values[index])
            } else {
                return nil
            }
        }
        let textMap = GesValueMap(texts)
        let handleKeys = handles.keys.sorted()
        return GesValueMap(
            canonicalKeys: textMap.keyStorage,
            handles: handleKeys.map { handles[$0]!.key },
            values: textMap.valueStorage + handleKeys.map { handles[$0]!.value }
        )
    }

    /// Maps are equal when their keys match by exact Unicode scalar sequence and their values are equal in order.
    /// Handle keys are equal by type and identity.
    public static func == (left: Self, right: Self) -> Bool {
        left.keyStorage.count == right.keyStorage.count && left.textCount == right.textCount && left.valueStorage == right.valueStorage
            && zip(left.keyStorage[..<left.textCount], right.keyStorage[..<right.textCount]).allSatisfy { GesText.scalarEqual($0, $1) }
            && left.handleStorage == right.handleStorage
    }

    /// Hashes keys by Unicode scalar sequence or handle identity and values in key order, consistent with `==`.
    public func hash(into hasher: inout Hasher) {
        for key in keyStorage[..<textCount] { GesText.hashScalars(key, into: &hasher) }
        for handle in handleStorage { hasher.combine(handle) }
        hasher.combine(valueStorage)
    }

    /// Number of distinct keys.
    public var length: Int { keyStorage.count }

    var entries: [GesMapEntry] { zip(keyStorage, valueStorage).map { GesMapEntry(key: $0, value: $1) } }

    var keys: [GesValue] { keyStorage.indices.map(keyValue(at:)) }

    // The key at a position as a value: a Text or a handle.
    func keyValue(at index: Int) -> GesValue { index < textCount ? .text(keyStorage[index]) : .handle(handleStorage[index - textCount]) }

    var values: [GesValue] { valueStorage }

    /// Looks up an exact scalar Text key, distinguishing absence from a stored Nothing.
    public func get(_ key: String) -> GesValue? {
        var low = 0
        var high = textCount
        while low < high {
            let middle = low + (high - low) / 2
            let candidate = keyStorage[middle]
            if GesText.scalarEqual(candidate, key) { return valueStorage[middle] }
            if GesText.scalarLess(candidate, key) { low = middle + 1 } else { high = middle }
        }
        return nil
    }

    /// Tests whether a scalar-exact key is present, including keys storing Nothing.
    public func containsKey(_ key: String) -> Bool { get(key) != nil }

    /// Looks up a handle key by type and identity, ignoring its version; nil when absent.
    public func get(_ key: GesHandle) -> GesValue? {
        var low = 0
        var high = handleStorage.count
        while low < high {
            let middle = low + (high - low) / 2
            let comparison = GesHandle.compare(handleStorage[middle], key)
            if comparison == 0 { return valueStorage[textCount + middle] }
            if comparison < 0 { low = middle + 1 } else { high = middle }
        }
        return nil
    }

    /// Tests whether a handle key is present by type and identity, including keys storing Nothing.
    public func containsKey(_ key: GesHandle) -> Bool { get(key) != nil }

    /// The handle key at a zero-based index, or nil for a Text key. Invalid indices are caller errors.
    public func handleKey(at index: Int) -> GesHandle? {
        precondition(index >= 0 && index < keyStorage.count, "Map index out of range.")
        return index < textCount ? nil : handleStorage[index - textCount]
    }

    subscript(index: Int) -> GesMapEntry { GesMapEntry(key: keyStorage[index], value: valueStorage[index]) }

    /// The key at a zero-based index in canonical key order; a handle key is its text form. Invalid indices are caller
    /// errors.
    public func key(at index: Int) -> String { keyStorage[index] }

    /// The value at a zero-based index, in the same order as `key(at:)`. Invalid indices are caller errors.
    public func value(at index: Int) -> GesValue { valueStorage[index] }

    // The entries whose key is (`keep`) or is not (`!keep`) among `sortedKeys`, found by merging both scalar-sorted key
    // sequences in one pass as C# does; the result stays in canonical order, so nothing is sorted again.
    func selecting(sortedKeys: [String], keep: Bool) -> GesValueMap {
        var keys: [String] = []
        var values: [GesValue] = []
        var other = 0
        for index in keyStorage.indices {
            let key = keyStorage[index]
            while other < sortedKeys.count && GesText.scalarLess(sortedKeys[other], key) { other += 1 }
            if (other < sortedKeys.count && GesText.scalarEqual(sortedKeys[other], key)) == keep {
                keys.append(key)
                values.append(valueStorage[index])
            }
        }
        return .init(canonicalKeys: keys, values: values)
    }

    // Key operands in scalar order for `selecting`; a map's own keys already are.
    static func sortedKeys(_ keys: [String]) -> [String] { keys.sorted(by: GesText.scalarLess) }

    // Merges two canonical maps in one pass; a key in both takes the right value, as last-entry-wins construction does.
    func merged(with right: GesValueMap) -> GesValueMap {
        var keys: [String] = []
        var values: [GesValue] = []
        keys.reserveCapacity(length + right.length)
        values.reserveCapacity(length + right.length)
        var left = 0
        var other = 0
        while left < length || other < right.length {
            if other == right.length || left < length && GesText.scalarLess(keyStorage[left], right.keyStorage[other]) {
                keys.append(keyStorage[left])
                values.append(valueStorage[left])
                left += 1
            } else {
                if left < length && GesText.scalarEqual(keyStorage[left], right.keyStorage[other]) { left += 1 }
                keys.append(right.keyStorage[other])
                values.append(right.valueStorage[other])
                other += 1
            }
        }
        return .init(canonicalKeys: keys, values: values)
    }
}

extension GesValueMap: Sequence {
    /// Iterates the entries of a map in ascending Unicode scalar key order.
    public struct Iterator: IteratorProtocol {
        private let map: GesValueMap
        private var index = 0

        init(_ map: GesValueMap) { self.map = map }

        /// The next entry, or nil after the last one.
        public mutating func next() -> (key: String, value: GesValue)? {
            guard index < map.keyStorage.count else { return nil }
            defer { index += 1 }
            return (map.keyStorage[index], map.valueStorage[index])
        }
    }

    /// Returns an iterator over the entries in ascending Unicode scalar key order.
    public func makeIterator() -> Iterator { Iterator(self) }

    /// The exact number of entries.
    public var underestimatedCount: Int { length }
}
