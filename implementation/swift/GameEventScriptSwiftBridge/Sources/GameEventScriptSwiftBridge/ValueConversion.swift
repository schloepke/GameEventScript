// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// A native adapter conversion failed; these errors are not source-language casts.
public enum GesBridgeConversionError: Error, Equatable {
    /// The value kind or unit differs from the required native representation.
    case typeMismatch(expected: String, actual: String)
    /// Exact conversion would overflow or lose numeric precision.
    case outOfRange(String)
    /// Distinct scalar-exact GES keys would collide under Swift String equality.
    case dictionaryKeyCollision(String)
}

/// Explicit native conversion. Implementations must not silently discard kind, units or numeric precision.
public protocol GesBridgeValueConvertible {
    /// Encodes a native value without silently discarding units, kind or precision; conversion failures may throw.
    func toGesValue() throws -> GesValue

    /// Decodes a GES value into the native type with strict kind, unit and precision checks; conversion failures may
    /// throw.
    static func fromGesValue(_ value: GesValue) throws -> Self
}

/// Entry points for typed Swift conversion, independently of any Host or Compiler.
public enum GesBridgeValueConversion {
    /// Invokes the native value's strict GES conversion, propagating conversion errors.
    public static func encode<T: GesBridgeValueConvertible>(_ value: T) throws -> GesValue { try value.toGesValue() }

    /// Encodes a native numeric magnitude with an explicitly declared quantity unit.
    /// Nothing passes through for optional/NaN values. Existing units and nonnumeric values are rejected;
    /// `.none` delegates to normal conversion. No unit scaling or numeric truncation is performed.
    public static func encode<T: GesBridgeValueConvertible>(_ value: T, unit: GesQuantityUnit) throws -> GesValue {
        let encoded = try value.toGesValue()
        if unit == .none || encoded.isNothing { return encoded }
        try requireKind(encoded, .integer, .float)
        if let integer = encoded.integerValue { return .integer(integer, unit: unit) }
        return .float(encoded.floatValue!, unit: unit)
    }

    /// Decodes a numeric magnitude only when its unit exactly matches the declared binding unit.
    /// Nothing delegates to the native decoder for Optional support. `.none` uses normal strict conversion.
    /// The unit is removed only after validation; the native decoder still rejects precision loss and overflow.
    public static func decode<T: GesBridgeValueConvertible>(_ value: GesValue, as type: T.Type = T.self, unit: GesQuantityUnit) throws -> T {
        if unit == .none || value.isNothing { return try T.fromGesValue(value) }
        guard value.unit == unit, value.valueKind == .integer || value.valueKind == .float else {
            throw GesBridgeConversionError.typeMismatch(expected: "Quantity(" + unit.suffix + ")", actual: String(describing: value.valueKind) + value.unit.suffix)
        }
        let magnitude = value.integerValue.map { GesValue.integer($0) } ?? .float(value.floatValue!)
        return try T.fromGesValue(magnitude)
    }

    /// Invokes the requested native type's strict decoder, propagating kind, unit and precision errors.
    public static func decode<T: GesBridgeValueConvertible>(_ value: GesValue, as type: T.Type = T.self) throws -> T { try T.fromGesValue(value) }
}

func requireKind(_ value: GesValue, _ first: GesBytecodeTypeKind, _ second: GesBytecodeTypeKind? = nil) throws {
    guard value.unit == .none, value.valueKind == first || value.valueKind == second else {
        throw GesBridgeConversionError.typeMismatch(expected: String(describing: first) + (second.map { "/" + String(describing: $0) } ?? ""), actual: String(describing: value.valueKind) + value.unit.suffix)
    }
}

extension GesValue: GesBridgeValueConvertible {
    /// Returns this immutable GES value unchanged.
    public func toGesValue() -> GesValue { self }

    /// Returns the supplied immutable GES value unchanged.
    public static func fromGesValue(_ value: GesValue) -> Self { value }
}

extension Bool: GesBridgeValueConvertible {
    /// Encodes this value as GES Boolean.
    public func toGesValue() -> GesValue { .boolean(self) }

    /// Decodes a unitless Boolean.
    ///
    /// - Throws: A conversion type mismatch for all other kinds or units.
    public static func fromGesValue(_ value: GesValue) throws -> Self {
        try requireKind(value, .boolean)
        return value.asBoolean
    }
}

extension String: GesBridgeValueConvertible {
    /// Encodes verbatim GES Text without Unicode normalization.
    public func toGesValue() -> GesValue { .text(self) }

    /// Decodes unitless Text verbatim; Tags are not implicitly converted.
    ///
    /// - Throws: A conversion type mismatch for other kinds or units.
    public static func fromGesValue(_ value: GesValue) throws -> Self {
        try requireKind(value, .text)
        return value.textValue!
    }
}

extension Double: GesBridgeValueConvertible {
    /// NaN follows the portable factory and becomes Nothing; infinities remain numeric.
    public func toGesValue() -> GesValue { .float(self) }

    /// Decodes a unitless Number exactly as binary64.
    ///
    /// - Throws: A conversion error for incompatible kinds/units or an Int64 value not exactly representable as Double.
    public static func fromGesValue(_ value: GesValue) throws -> Self {
        try requireKind(value, .integer, .float)
        if let integer = value.integerValue {
            guard let result = Double(exactly: integer) else { throw GesBridgeConversionError.outOfRange("Double") }
            return result
        }
        return value.floatValue!
    }
}

extension Float: GesBridgeValueConvertible {
    /// Encodes this binary32 value through the portable binary64 factory; NaN becomes Nothing.
    public func toGesValue() -> GesValue { .float(Double(self)) }

    /// Decodes a unitless Number only if exactly representable as Float.
    ///
    /// - Throws: A conversion error for incompatible kinds/units, overflow or lost precision.
    public static func fromGesValue(_ value: GesValue) throws -> Self {
        let number = try Double.fromGesValue(value)
        guard let result = Float(exactly: number) else { throw GesBridgeConversionError.outOfRange("Float") }
        return result
    }
}

extension Optional: GesBridgeValueConvertible where Wrapped: GesBridgeValueConvertible {
    /// Encodes nil as Nothing, otherwise delegates to the wrapped value's conversion and propagates errors.
    public func toGesValue() throws -> GesValue { try self?.toGesValue() ?? .nothing }

    /// Decodes Nothing as nil, otherwise uses the wrapped type's strict decoder and propagates errors.
    public static func fromGesValue(_ value: GesValue) throws -> Self { value.isNothing ? nil : try Wrapped.fromGesValue(value) }
}

extension Array: GesBridgeValueConvertible where Element: GesBridgeValueConvertible {
    /// Encodes each element in order as a GES List, propagating element conversion errors.
    public func toGesValue() throws -> GesValue { .list(try map { try $0.toGesValue() }) }

    /// Decodes a unitless List in order using strict element conversion.
    ///
    /// - Throws: A type mismatch for non-Lists, or any element conversion error.
    public static func fromGesValue(_ value: GesValue) throws -> Self {
        try requireKind(value, .list)
        return try value.listValue!.map(Element.fromGesValue)
    }
}

extension Dictionary: GesBridgeValueConvertible where Key == String, Value: GesBridgeValueConvertible {
    /// Copies the dictionary into the Runtime's canonical scalar-key order.
    public func toGesValue() throws -> GesValue {
        let pairs = Array(self)
        return .map(keys: pairs.map(\.key), values: try pairs.map { try $0.value.toGesValue() })
    }

    /// Rejects scalar-distinct GES keys that Swift String equality would merge.
    public static func fromGesValue(_ value: GesValue) throws -> Self {
        try requireKind(value, .map)
        var result: Self = [:]
        let map = value.asMap!
        for index in 0..<map.length {
            let key = map.key(at: index)
            guard result.index(forKey: key) == nil else { throw GesBridgeConversionError.dictionaryKeyCollision(key) }
            result.updateValue(try Value.fromGesValue(map.value(at: index)), forKey: key)
        }
        return result
    }
}

func encodeInteger<T: FixedWidthInteger>(_ value: T) throws -> GesValue {
    guard let integer = Int64(exactly: value) else { throw GesBridgeConversionError.outOfRange("Int64") }
    return .integer(integer)
}

func decodeInteger<T: FixedWidthInteger>(_ value: GesValue, as type: T.Type) throws -> T {
    try requireKind(value, .integer, .float)
    let integer = value.integerValue ?? value.floatValue.flatMap(Int64.init(exactly:))
    guard let integer, let result = T(exactly: integer) else { throw GesBridgeConversionError.outOfRange(String(describing: T.self)) }
    return result
}
