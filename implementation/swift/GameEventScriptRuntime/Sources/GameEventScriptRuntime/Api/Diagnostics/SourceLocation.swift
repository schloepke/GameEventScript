// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Optional source span; lines and columns are one-based, source IDs identify compiler inputs.
public struct GameEventScriptSourceLocation: Sendable {
    /// Display name of the compiler input.
    public let sourceName: String
    /// One-based start line when known.
    public let line: Int?
    /// One-based start column when known.
    public let column: Int?
    /// One-based end line when known.
    public let endLine: Int?
    /// One-based end column when known.
    public let endColumn: Int?
    /// Declaring module, or UnknownModule when unavailable.
    public let moduleName: String
    /// Compiler-assigned document identifier when available.
    public let sourceID: UInt32?

    /// Creates source context; unknown coordinates remain nil rather than becoming zero.
    public init(sourceName: String, line: Int? = nil, column: Int? = nil, endLine: Int? = nil, endColumn: Int? = nil, moduleName: String = "UnknownModule", sourceID: UInt32? = nil) {
        self.sourceName = sourceName
        self.line = line
        self.column = column
        self.endLine = endLine
        self.endColumn = endColumn
        self.moduleName = moduleName
        self.sourceID = sourceID
    }
}
