// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// An original UTF-8 byte range (excluding the initial BOM), with scalar positions.
public struct ConformanceSourceRange: Equatable, Sendable {
    public let byteOffset: Int
    public let byteLength: Int
    public let line: Int
    public let column: Int
    public let endLine: Int
    public let endColumn: Int
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
    public let code: String
    public let message: String
    public let range: ConformanceSourceRange
}

/// A malformed Markdown/YAML/schema document; no partial document is returned.
public struct ConformanceParseError: Error, Sendable, CustomStringConvertible {
    public let diagnostic: ConformanceDiagnostic
    public var diagnostics: [ConformanceDiagnostic] { [diagnostic] }
    public var description: String {
        "\(diagnostic.code) at \(diagnostic.range.line):\(diagnostic.range.column): \(diagnostic.message)"
    }
    internal init(_ code: String, _ message: String, _ range: ConformanceSourceRange = .init()) {
        diagnostic = .init(code: code, message: message, range: range)
    }
}

/// Bounded resource consumption for the synchronous authoring parser.
public struct ConformanceParserLimits: Sendable {
    public static let `default` = ConformanceParserLimits()
    public var maxDocumentBytes = 16 * 1024 * 1024
    public var maxYamlDepth = 32
    public var maxTests = 4096
    public var maxSourcesPerTest = 256
    public var maxSourceBytesPerTest = 4 * 1024 * 1024
    public var maxStepsPerTest = 65535
    public var maxHostsPerTest = 256
    public var maxScalarBytes = 1024 * 1024
    public var maxYamlNodes = 262144
    public init() {}
}
