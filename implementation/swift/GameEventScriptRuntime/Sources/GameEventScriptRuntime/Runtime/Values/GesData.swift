// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Data holds no external value, whose fields may change between handlers, anywhere inside: not in List items, Map
// entries, Record fields or Message arguments.
func gesIsData(_ value: GesValue) -> Bool {
    if value.externalValue != nil { return false }
    if let items = value.listValue { return items.allSatisfy(gesIsData) }
    if let fields = value.asMap { return fields.valueStorage.allSatisfy(gesIsData) }
    if let message = value.messageValue { return message.arguments.values.allSatisfy(gesIsData) }
    return true
}

/// An immutable plain Map that holds only data: no external value anywhere inside, whose fields could change between
/// handlers. Keys are unique in ascending Unicode scalar order; equality and hashing follow GES Map semantics.
///
/// A data map is `Sendable`: its values are immutable and the only mutable references a value may hold, external host
/// objects, are rejected when the map is created.
public struct GesDataMap: Hashable, @unchecked Sendable, Sequence, CustomStringConvertible {
    /// The entries, shared without copying.
    public let asMap: GesValueMap

    /// Creates the map without entries.
    public init() { asMap = GesValueMap(canonicalKeys: [], values: []) }

    /// Creates a data map that shares the entries of `map`; the stored fields of a Record become plain entries.
    ///
    /// - Throws: `GesApiError.invalidArgument` when an entry holds an external value anywhere inside.
    public init(_ map: GesValueMap) throws(GesApiError) {
        guard map.valueStorage.allSatisfy(gesIsData) else { throw GesApiError.invalidArgument("A data map holds no external value") }
        asMap = map
    }

    /// Reads a plain Map value that holds only data, sharing its entries; a Record, an external value, every other kind
    /// and a Map holding an external value anywhere inside give nil.
    public init?(value: GesValue) {
        guard value.kind == .map, let map = value.asMap, gesIsData(value) else { return nil }
        asMap = map
    }

    /// The map without entries.
    public static var empty: Self { Self() }

    /// The number of entries.
    public var length: Int { asMap.length }

    /// Looks up an exact scalar key, distinguishing absence from a stored Nothing.
    public func get(_ key: String) -> GesValue? { asMap.get(key) }

    /// Tests whether a scalar-exact key is present, including keys storing Nothing.
    public func containsKey(_ key: String) -> Bool { asMap.containsKey(key) }

    /// The key at a zero-based index in ascending Unicode scalar key order. Invalid indices are caller errors.
    public func key(at index: Int) -> String { asMap.key(at: index) }

    /// The value at a zero-based index, in the same order as `key(at:)`. Invalid indices are caller errors.
    public func value(at index: Int) -> GesValue { asMap.value(at: index) }

    /// The Map value that holds the entries, without copying them.
    public func toValue() -> GesValue { .map(asMap) }

    /// Returns an iterator over the entries in ascending Unicode scalar key order.
    public func makeIterator() -> GesValueMap.Iterator { asMap.makeIterator() }

    /// The exact number of entries.
    public var underestimatedCount: Int { asMap.length }

    /// The GES text of the Map.
    public var description: String { toValue().description }
}

/// An immutable List that holds only data: no external value anywhere inside, whose fields could change between
/// handlers. Equality and hashing follow GES List semantics.
///
/// A data list is `Sendable`: its values are immutable and the only mutable references a value may hold, external host
/// objects, are rejected when the list is created.
public struct GesDataList: Hashable, @unchecked Sendable, RandomAccessCollection, CustomStringConvertible {
    /// The elements in order, shared without copying.
    public let asList: [GesValue]

    /// Creates the list without elements.
    public init() { asList = [] }

    /// Creates a data list from elements in order.
    ///
    /// - Throws: `GesApiError.invalidArgument` when an element holds an external value anywhere inside.
    public init(_ items: [GesValue]) throws(GesApiError) {
        guard items.allSatisfy(gesIsData) else { throw GesApiError.invalidArgument("A data list holds no external value") }
        asList = items
    }

    /// Reads a List value that holds only data, sharing its elements; every other kind and a List holding an external
    /// value anywhere inside give nil.
    public init?(value: GesValue) {
        guard let items = value.listValue, gesIsData(value) else { return nil }
        asList = items
    }

    /// The list without elements.
    public static var empty: Self { Self() }

    /// The number of elements.
    public var length: Int { asList.count }

    /// The position of the first element, always 0.
    public var startIndex: Int { 0 }

    /// The position after the last element, equal to `length`.
    public var endIndex: Int { asList.count }

    /// The element at a zero-based position. Invalid positions are caller errors.
    public subscript(position: Int) -> GesValue { asList[position] }

    /// The List value that holds the elements, without copying them.
    public func toValue() -> GesValue { .list(asList) }

    /// The GES text of the List.
    public var description: String { toValue().description }
}

/// Codable as the Map value object of the V1 value JSON, as `GesValue` writes a Map.
extension GesDataMap: Codable {
    /// Writes the map as the Map value object of the V1 value JSON.
    public func encode(to encoder: any Encoder) throws { try toValue().encode(to: encoder) }

    /// Reads a Map value object; any other value is `DecodingError.typeMismatch`.
    public init(from decoder: any Decoder) throws {
        guard let map = GesDataMap(value: try GesValue(from: decoder)) else {
            throw DecodingError.typeMismatch(GesDataMap.self, .init(codingPath: decoder.codingPath, debugDescription: "The JSON value is no Map."))
        }
        self = map
    }
}

/// Codable as the List value object of the V1 value JSON, as `GesValue` writes a List.
extension GesDataList: Codable {
    /// Writes the list as the List value object of the V1 value JSON.
    public func encode(to encoder: any Encoder) throws { try toValue().encode(to: encoder) }

    /// Reads a List value object; any other value is `DecodingError.typeMismatch`.
    public init(from decoder: any Decoder) throws {
        guard let list = GesDataList(value: try GesValue(from: decoder)) else {
            throw DecodingError.typeMismatch(GesDataList.self, .init(codingPath: decoder.codingPath, debugDescription: "The JSON value is no List."))
        }
        self = list
    }
}
