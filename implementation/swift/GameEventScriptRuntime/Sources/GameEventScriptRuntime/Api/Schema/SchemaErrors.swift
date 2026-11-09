// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// A schema source or a binding that breaks a rule. Callers branch on `code`, a stable `schema.*` code; a document check
/// reports a `document.*` code. The message is informative text.
public struct GesSchemaError: Error, Sendable, Equatable {
    /// The stable code.
    public let code: String
    /// The name of the source the error is in.
    public let sourceName: String
    /// The one-based line, or 0 when the error has no position.
    public let line: Int
    /// The one-based Unicode-scalar column, or 0 when the error has no position.
    public let column: Int
    /// Informative text, not a portable comparison value.
    public let message: String
}

/// A value that cannot be encoded, or data that is not a valid encoding. The error names the field path and holds no value
/// from the data, so it can be logged safely.
public struct GesWireError: Error, Sendable, Equatable {
    /// The stable `wire.*` code.
    public let code: String
    /// The path of the field, such as `position.x`; empty for the record as a whole.
    public let path: String

    /// The code `wire.truncated`: the data ends before the record does.
    public static let truncated = "wire.truncated"
    /// The code `wire.invalidIndex`: an index is at least the size of its range, tag set, union or message set.
    public static let invalidIndex = "wire.invalidIndex"
    /// The code `wire.invalidCount`: a Text, Tag, List or Dice count exceeds its `max`.
    public static let invalidCount = "wire.invalidCount"
    /// The code `wire.invalidText`: Text is not valid UTF-8.
    public static let invalidText = "wire.invalidText"
    /// The code `wire.invalidTag`: a `:Tag max n` holds or receives no valid tag name.
    public static let invalidTag = "wire.invalidTag"
    /// The code `wire.invalidNumber`: data holds NaN or negative zero, or an infinity with a range.
    public static let invalidNumber = "wire.invalidNumber"
    /// The code `wire.invalidPadding`: a padding bit is not zero.
    public static let invalidPadding = "wire.invalidPadding"
    /// The code `wire.limitExceeded`: the record exceeds the caller's byte limit.
    public static let limitExceeded = "wire.limitExceeded"
    /// The code `wire.outOfRange`: a value is outside its range, not finite in a quantized field, or more than 2^50 steps from zero.
    public static let outOfRange = "wire.outOfRange"
    /// The code `wire.notRepresentable`: an integer field receives a fractional value, or a value does not fit its stored form.
    public static let notRepresentable = "wire.notRepresentable"
    /// The code `wire.missingField`: a field that is neither `optional` nor has a `:Nothing` alternative has no value.
    public static let missingField = "wire.missingField"
    /// The code `wire.nothingValue`: a field, argument or list element is `nothing` without allowing it.
    public static let nothingValue = "wire.nothingValue"
    /// The code `wire.keyMismatch`: a record and its baseline have different keys.
    public static let keyMismatch = "wire.keyMismatch"
    /// The code `wire.invalidDelta`: a delta is not canonical, leaves its range, or its key differs from the baseline's.
    public static let invalidDelta = "wire.invalidDelta"
    /// The code `wire.overlappingBuffers`: the target buffer of a delta overlaps an input.
    public static let overlappingBuffers = "wire.overlappingBuffers"
    /// The code `wire.typeMismatch`: a value has another kind or unit than its field.
    public static let typeMismatch = "wire.typeMismatch"
    /// The code `wire.unknownTag`: a tag is not in its set.
    public static let unknownTag = "wire.unknownTag"
    /// The code `wire.tooLong`: a Text, Tag, List or Dice exceeds its `max`.
    public static let tooLong = "wire.tooLong"
    /// The code `wire.bufferTooSmall`: the caller's buffer is too small.
    public static let bufferTooSmall = "wire.bufferTooSmall"
    /// The code `wire.fieldOrder`: a typed writer or reader uses a field out of order.
    public static let fieldOrder = "wire.fieldOrder"

    /// Creates an error with a stable code and a field path.
    /// - Parameters:
    ///   - code: The stable `wire.*` code, one of the constants of this type.
    ///   - path: The path of the field; empty for the record as a whole.
    public init(code: String, path: String = "") {
        self.code = code
        self.path = path
    }

    init(_ code: String, _ path: String = "") {
        self.code = code
        self.path = path
    }
}
