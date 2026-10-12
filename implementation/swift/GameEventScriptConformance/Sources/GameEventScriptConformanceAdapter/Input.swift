// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// A decoded JSON request. Protocol numeric values travel as strings to preserve exact bits.
indirect enum Input: Equatable, Sendable {
    /// An explicit null scalar.
    case null
    /// A Boolean scalar.
    case bool(Bool)
    /// A text scalar.
    case string(String)
    /// A numeric scalar retaining its authored decimal text.
    case number(String)
    /// An ordered sequence of schema values.
    case array([Input])
    /// Schema object fields; their order has no protocol meaning.
    case object([InputEntry])

    /// Compares kinds and ordered contents exactly, preserving numeric spelling and scalar-exact text.
    static func == (lhs: Self, rhs: Self) -> Bool {
        switch (lhs, rhs) {
        case (.null, .null): return true
        case (.bool(let a), .bool(let b)): return a == b
        case (.string(let a), .string(let b)), (.number(let a), .number(let b)): return a.utf8.elementsEqual(b.utf8)
        case (.array(let a), .array(let b)): return a == b
        case (.object(let a), .object(let b)): return a == b
        default: return false
        }
    }

    /// Looks up a scalar-exact object key; returns nil for absence or a non-object value.
    subscript(_ key: String) -> Input? { objectValue?.first(where: { $0.key.utf8.elementsEqual(key.utf8) })?.value }

    /// Text payload, or nil for other kinds.
    var stringValue: String? {
        if case .string(let value) = self { return value }
        return nil
    }
    /// Authored numeric text, or nil for other kinds.
    var numberValue: String? {
        if case .number(let value) = self { return value }
        return nil
    }
    /// Boolean payload, or nil for other kinds.
    var boolValue: Bool? {
        if case .bool(let value) = self { return value }
        return nil
    }
    /// Array elements, or nil for other kinds.
    var arrayValue: [Input]? {
        if case .array(let value) = self { return value }
        return nil
    }
    /// Ordered object entries, or nil for other kinds.
    var objectValue: [InputEntry]? {
        if case .object(let value) = self { return value }
        return nil
    }
    /// Numeric text parsed as a platform Int, or nil when unavailable or not representable.
    var integerValue: Int? { numberValue.flatMap(Int.init) }

    internal func replacing(_ key: String, with value: Input) -> Input {
        var entries = objectValue ?? []
        if let index = entries.firstIndex(where: { $0.key.utf8.elementsEqual(key.utf8) }) { entries[index] = .init(key: key, value: value) } else { entries.append(.init(key: key, value: value)) }
        return .object(entries)
    }
}

/// A decoded schema object field. Ordered GES maps use explicit entry arrays.
struct InputEntry: Equatable, Sendable {
    /// Schema object key.
    let key: String
    /// Schema value associated with the key.
    let value: Input

    /// Compares scalar-exact keys and ordered schema values.
    static func == (lhs: Self, rhs: Self) -> Bool { lhs.key.utf8.elementsEqual(rhs.key.utf8) && lhs.value == rhs.value }
}

extension Input: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.singleValueContainer()
        if container.decodeNil() {
            self = .null
        } else if let value = try? container.decode(Bool.self) {
            self = .bool(value)
        } else if let value = try? container.decode(String.self) {
            self = .string(value)
        } else if let value = try? container.decode(Int64.self) {
            self = .number(String(value))
        } else if let value = try? container.decode(UInt64.self) {
            self = .number(String(value))
        } else if let value = try? container.decode([Input].self) {
            self = .array(value)
        } else {
            self = .object(try container.decode([String: Input].self).map { .init(key: $0.key, value: $0.value) })
        }
    }
}
