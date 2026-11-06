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

    static let truncated = "wire.truncated"
    static let invalidIndex = "wire.invalidIndex"
    static let invalidCount = "wire.invalidCount"
    static let invalidText = "wire.invalidText"
    static let invalidTag = "wire.invalidTag"
    static let invalidNumber = "wire.invalidNumber"
    static let invalidPadding = "wire.invalidPadding"
    static let limitExceeded = "wire.limitExceeded"
    static let outOfRange = "wire.outOfRange"
    static let notRepresentable = "wire.notRepresentable"
    static let missingField = "wire.missingField"
    static let nothingValue = "wire.nothingValue"
    static let keyMismatch = "wire.keyMismatch"
    static let invalidDelta = "wire.invalidDelta"
    static let overlappingBuffers = "wire.overlappingBuffers"
    static let typeMismatch = "wire.typeMismatch"
    static let unknownTag = "wire.unknownTag"
    static let tooLong = "wire.tooLong"
    static let bufferTooSmall = "wire.bufferTooSmall"
    static let fieldOrder = "wire.fieldOrder"

    init(_ code: String, _ path: String = "") {
        self.code = code
        self.path = path
    }

    static func make(_ code: String, _ path: String) -> GesWireError { GesWireError(code, path) }
}
