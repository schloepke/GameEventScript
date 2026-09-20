// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// A native adapter conversion failed; these errors are not source-language casts.
public enum GameEventScriptSwiftConversionError: Error, Equatable {
    case typeMismatch(expected: String, actual: String)
    case outOfRange(String)
    case dictionaryKeyCollision(String)
}

/// Explicit native conversion. Implementations must not silently discard kind, units or numeric precision.
public protocol GameEventScriptSwiftValueConvertible {
    func toGesValue() throws -> GesValue
    static func fromGesValue(_ value: GesValue) throws -> Self
}

/// Entry points for typed Swift conversion, independently of any Host or Compiler.
public enum GameEventScriptSwiftValue {
    public static func encode<T: GameEventScriptSwiftValueConvertible>(_ value: T) throws -> GesValue {
        try value.toGesValue()
    }
    public static func decode<T: GameEventScriptSwiftValueConvertible>(_ value: GesValue, as type: T.Type = T.self)
        throws -> T
    {
        try T.fromGesValue(value)
    }
}

func requireKind(_ value: GesValue, _ first: GesValueKind, _ second: GesValueKind? = nil) throws {
    guard value.unit == .none, value.kind == first || value.kind == second else {
        throw GameEventScriptSwiftConversionError.typeMismatch(
            expected: String(describing: first) + (second.map { "/" + String(describing: $0) } ?? ""),
            actual: String(describing: value.kind) + value.unit.suffix)
    }
}

extension GesValue: GameEventScriptSwiftValueConvertible {
    public func toGesValue() -> GesValue { self }
    public static func fromGesValue(_ value: GesValue) -> Self { value }
}
extension Bool: GameEventScriptSwiftValueConvertible {
    public func toGesValue() -> GesValue { .boolean(self) }
    public static func fromGesValue(_ value: GesValue) throws -> Self {
        try requireKind(value, .boolean)
        return value.asBoolean
    }
}
extension String: GameEventScriptSwiftValueConvertible {
    public func toGesValue() -> GesValue { .text(self) }
    public static func fromGesValue(_ value: GesValue) throws -> Self {
        try requireKind(value, .text)
        return value.textValue!
    }
}
extension Double: GameEventScriptSwiftValueConvertible {
    /// NaN follows the portable factory and becomes Nothing; infinities remain numeric.
    public func toGesValue() -> GesValue { .float(self) }
    public static func fromGesValue(_ value: GesValue) throws -> Self {
        try requireKind(value, .integer, .float)
        if let integer = value.integerValue {
            guard let result = Double(exactly: integer) else {
                throw GameEventScriptSwiftConversionError.outOfRange("Double")
            }
            return result
        }
        return value.floatValue!
    }
}
extension Float: GameEventScriptSwiftValueConvertible {
    public func toGesValue() -> GesValue { .float(Double(self)) }
    public static func fromGesValue(_ value: GesValue) throws -> Self {
        let number = try Double.fromGesValue(value)
        guard let result = Float(exactly: number) else {
            throw GameEventScriptSwiftConversionError.outOfRange("Float")
        }
        return result
    }
}

extension Optional: GameEventScriptSwiftValueConvertible where Wrapped: GameEventScriptSwiftValueConvertible {
    public func toGesValue() throws -> GesValue { try self?.toGesValue() ?? .nothing }
    public static func fromGesValue(_ value: GesValue) throws -> Self {
        value.isNothing ? nil : try Wrapped.fromGesValue(value)
    }
}
extension Array: GameEventScriptSwiftValueConvertible where Element: GameEventScriptSwiftValueConvertible {
    public func toGesValue() throws -> GesValue { .list(try map { try $0.toGesValue() }) }
    public static func fromGesValue(_ value: GesValue) throws -> Self {
        try requireKind(value, .list)
        return try value.listValue!.map(Element.fromGesValue)
    }
}
extension Dictionary: GameEventScriptSwiftValueConvertible
where Key == String, Value: GameEventScriptSwiftValueConvertible {
    /// Copies the dictionary into the Runtime's canonical scalar-key order.
    public func toGesValue() throws -> GesValue {
        .map(try map { try GesMapEntry(key: $0.key, value: $0.value.toGesValue()) })
    }
    /// Rejects scalar-distinct GES keys that Swift String equality would merge.
    public static func fromGesValue(_ value: GesValue) throws -> Self {
        try requireKind(value, .map)
        var result: Self = [:]
        for entry in value.mapEntries! {
            guard result.index(forKey: entry.key) == nil else {
                throw GameEventScriptSwiftConversionError.dictionaryKeyCollision(entry.key)
            }
            result.updateValue(try Value.fromGesValue(entry.value), forKey: entry.key)
        }
        return result
    }
}

func encodeInteger<T: FixedWidthInteger>(_ value: T) throws -> GesValue {
    guard let integer = Int64(exactly: value) else {
        throw GameEventScriptSwiftConversionError.outOfRange("Int64")
    }
    return .integer(integer)
}
func decodeInteger<T: FixedWidthInteger>(_ value: GesValue, as type: T.Type) throws -> T {
    try requireKind(value, .integer, .float)
    let integer = value.integerValue ?? value.floatValue.flatMap(Int64.init(exactly:))
    guard let integer, let result = T(exactly: integer) else {
        throw GameEventScriptSwiftConversionError.outOfRange(String(describing: T.self))
    }
    return result
}
