// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

extension Int: GameEventScriptSwiftValueConvertible {
    /// Converts this integer to exact Int64 GES storage.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError.outOfRange` when it cannot fit Int64.
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }

    /// Decodes a unitless Number exactly as Int. Fractional values, overflow and lost precision are rejected.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError` for an incompatible kind, unit or range.
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}

extension Int8: GameEventScriptSwiftValueConvertible {
    /// Converts this integer to exact Int64 GES storage.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError.outOfRange` when it cannot fit Int64.
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }

    /// Decodes a unitless Number exactly as Int8. Fractional values, overflow and lost precision are rejected.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError` for an incompatible kind, unit or range.
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}

extension Int16: GameEventScriptSwiftValueConvertible {
    /// Converts this integer to exact Int64 GES storage.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError.outOfRange` when it cannot fit Int64.
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }

    /// Decodes a unitless Number exactly as Int16. Fractional values, overflow and lost precision are rejected.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError` for an incompatible kind, unit or range.
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}

extension Int32: GameEventScriptSwiftValueConvertible {
    /// Converts this integer to exact Int64 GES storage.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError.outOfRange` when it cannot fit Int64.
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }

    /// Decodes a unitless Number exactly as Int32. Fractional values, overflow and lost precision are rejected.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError` for an incompatible kind, unit or range.
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}

extension Int64: GameEventScriptSwiftValueConvertible {
    /// Converts this integer to exact Int64 GES storage.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError.outOfRange` when it cannot fit Int64.
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }

    /// Decodes a unitless Number exactly as Int64. Fractional values, overflow and lost precision are rejected.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError` for an incompatible kind, unit or range.
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}

extension UInt: GameEventScriptSwiftValueConvertible {
    /// Converts this integer to exact Int64 GES storage.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError.outOfRange` when it cannot fit Int64.
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }

    /// Decodes a unitless Number exactly as UInt. Fractional values, overflow and lost precision are rejected.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError` for an incompatible kind, unit or range.
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}

extension UInt8: GameEventScriptSwiftValueConvertible {
    /// Converts this integer to exact Int64 GES storage.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError.outOfRange` when it cannot fit Int64.
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }

    /// Decodes a unitless Number exactly as UInt8. Fractional values, overflow and lost precision are rejected.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError` for an incompatible kind, unit or range.
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}

extension UInt16: GameEventScriptSwiftValueConvertible {
    /// Converts this integer to exact Int64 GES storage.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError.outOfRange` when it cannot fit Int64.
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }

    /// Decodes a unitless Number exactly as UInt16. Fractional values, overflow and lost precision are rejected.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError` for an incompatible kind, unit or range.
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}

extension UInt32: GameEventScriptSwiftValueConvertible {
    /// Converts this integer to exact Int64 GES storage.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError.outOfRange` when it cannot fit Int64.
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }

    /// Decodes a unitless Number exactly as UInt32. Fractional values, overflow and lost precision are rejected.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError` for an incompatible kind, unit or range.
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}

extension UInt64: GameEventScriptSwiftValueConvertible {
    /// Converts this integer to exact Int64 GES storage.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError.outOfRange` when it cannot fit Int64.
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }

    /// Decodes a unitless Number exactly as UInt64. Fractional values, overflow and lost precision are rejected.
    ///
    /// - Throws: `GameEventScriptSwiftConversionError` for an incompatible kind, unit or range.
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}
