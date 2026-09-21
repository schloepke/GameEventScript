// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// An original UTF-8 byte range (excluding the initial BOM), with scalar positions.
public struct ConformanceSourceRange: Equatable, Sendable {
    /// Zero-based UTF-8 start offset, excluding an initial BOM.
    public let byteOffset: Int
    /// UTF-8 byte length of the span.
    public let byteLength: Int
    /// One-based start line.
    public let line: Int
    /// One-based start column measured in Unicode scalars.
    public let column: Int
    /// One-based end line.
    public let endLine: Int
    /// One-based end column measured in Unicode scalars.
    public let endColumn: Int
    /// Creates source coordinates; defaults to an empty span at the start of the document.
    public init(
        byteOffset: Int = 0, byteLength: Int = 0, line: Int = 1, column: Int = 1, endLine: Int = 1, endColumn: Int = 1
    ) {
        self.byteOffset = byteOffset
        self.byteLength = byteLength
        self.line = line
        self.column = column
        self.endLine = endLine
        self.endColumn = endColumn
    }
}

/// A stable authoring diagnostic independent of a test framework or host language.
public struct ConformanceDiagnostic: Equatable, Sendable {
    /// Stable authoring error classification.
    public let code: String
    /// Human-readable explanation, not an assertion key.
    public let message: String
    /// Original Markdown source span associated with the diagnostic.
    public let range: ConformanceSourceRange
}

/// A malformed Markdown/YAML/schema document; no partial document is returned.
public struct ConformanceParseError: Error, Sendable, CustomStringConvertible {
    /// The authoring diagnostic that prevented parsing.
    public let diagnostic: ConformanceDiagnostic
    /// Single-element diagnostic collection containing this parse failure.
    public var diagnostics: [ConformanceDiagnostic] { [diagnostic] }
    /// Human-readable diagnostic code, line, column and message for display.
    public var description: String {
        "\(diagnostic.code) at \(diagnostic.range.line):\(diagnostic.range.column): \(diagnostic.message)"
    }
    internal init(_ code: String, _ message: String, _ range: ConformanceSourceRange = .init()) {
        diagnostic = .init(code: code, message: message, range: range)
    }
}

/// Bounded resource consumption for the synchronous authoring parser.
public struct ConformanceParserLimits: Sendable {
    /// Standard bounded parser configuration.
    public static let `default` = ConformanceParserLimits()
    /// Maximum Markdown input length in UTF-8 bytes.
    public var maxDocumentBytes = 16 * 1024 * 1024
    /// Maximum nested YAML collection depth.
    public var maxYamlDepth = 32
    /// Maximum cases in one document.
    public var maxTests = 4096
    /// Maximum source inputs in one case.
    public var maxSourcesPerTest = 256
    /// Maximum combined source bytes in one case.
    public var maxSourceBytesPerTest = 4 * 1024 * 1024
    /// Maximum execution steps declared by one case.
    public var maxStepsPerTest = 65535
    /// Maximum host instances requested by one case.
    public var maxHostsPerTest = 256
    /// Maximum UTF-8 bytes in one YAML scalar.
    public var maxScalarBytes = 1024 * 1024
    /// Maximum parsed YAML node count.
    public var maxYamlNodes = 262144
    /// Creates the standard parser limits.
    public init() {}
}
