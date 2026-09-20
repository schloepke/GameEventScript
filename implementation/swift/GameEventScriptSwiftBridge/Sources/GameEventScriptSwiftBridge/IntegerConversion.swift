// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

extension Int: GameEventScriptSwiftValueConvertible {
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}
extension Int8: GameEventScriptSwiftValueConvertible {
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}
extension Int16: GameEventScriptSwiftValueConvertible {
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}
extension Int32: GameEventScriptSwiftValueConvertible {
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}
extension Int64: GameEventScriptSwiftValueConvertible {
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}
extension UInt: GameEventScriptSwiftValueConvertible {
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}
extension UInt8: GameEventScriptSwiftValueConvertible {
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}
extension UInt16: GameEventScriptSwiftValueConvertible {
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}
extension UInt32: GameEventScriptSwiftValueConvertible {
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}
extension UInt64: GameEventScriptSwiftValueConvertible {
    public func toGesValue() throws -> GesValue { try encodeInteger(self) }
    public static func fromGesValue(_ value: GesValue) throws -> Self { try decodeInteger(value, as: Self.self) }
}
