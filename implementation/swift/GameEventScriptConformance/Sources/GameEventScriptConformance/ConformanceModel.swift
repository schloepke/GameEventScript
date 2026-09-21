// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// An immutable, ordered value from the portable Conformance YAML subset.
/// Numeric tokens retain their spelling until a schema or runtime codec consumes them.
public indirect enum ConformanceData: Equatable, Sendable {
    /// An explicit null scalar.
    case null
    /// A Boolean scalar.
    case bool(Bool)
    /// A text scalar.
    case string(String)
    /// A numeric scalar retaining its authored decimal text.
    case number(String)
    /// An ordered sequence of schema values.
    case array([ConformanceData])
    /// An ordered sequence of key/value entries.
    case object([ConformanceEntry])

    /// Compares kinds and ordered contents exactly, preserving numeric spelling and scalar-exact text.
    public static func == (lhs: Self, rhs: Self) -> Bool {
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
    public subscript(_ key: String) -> ConformanceData? {
        objectValue?.first(where: { $0.key.utf8.elementsEqual(key.utf8) })?.value
    }
    /// Text payload, or nil for other kinds.
    public var stringValue: String? {
        if case .string(let value) = self { return value }
        return nil
    }
    /// Authored numeric text, or nil for other kinds.
    public var numberValue: String? {
        if case .number(let value) = self { return value }
        return nil
    }
    /// Boolean payload, or nil for other kinds.
    public var boolValue: Bool? {
        if case .bool(let value) = self { return value }
        return nil
    }
    /// Array elements, or nil for other kinds.
    public var arrayValue: [ConformanceData]? {
        if case .array(let value) = self { return value }
        return nil
    }
    /// Ordered object entries, or nil for other kinds.
    public var objectValue: [ConformanceEntry]? {
        if case .object(let value) = self { return value }
        return nil
    }
    /// Numeric text parsed as a platform Int, or nil when unavailable or not representable.
    public var integerValue: Int? { numberValue.flatMap(Int.init) }

    internal func replacing(_ key: String, with value: ConformanceData) -> ConformanceData {
        var entries = objectValue ?? []
        if let index = entries.firstIndex(where: { $0.key.utf8.elementsEqual(key.utf8) }) {
            entries[index] = .init(key: key, value: value)
        } else {
            entries.append(.init(key: key, value: value))
        }
        return .object(entries)
    }
}

/// A decoded mapping entry. Order follows the authoring document.
public struct ConformanceEntry: Equatable, Sendable {
    /// Schema object key.
    public let key: String
    /// Schema value associated with the key.
    public let value: ConformanceData
    /// Creates an ordered schema key/value entry.
    public init(key: String, value: ConformanceData) {
        self.key = key
        self.value = value
    }
    /// Compares scalar-exact keys and ordered schema values.
    public static func == (lhs: Self, rhs: Self) -> Bool {
        lhs.key.utf8.elementsEqual(rhs.key.utf8) && lhs.value == rhs.value
    }
}

/// The validated suite and exact original bytes supplied by the embedding.
public struct ConformanceDocument: Sendable {
    /// Markdown conformance format version.
    public let formatVersion: Int
    /// Stable unique suite identifier.
    public let suiteID: String
    /// Human-readable suite title.
    public let title: String
    /// Validated cases in document order.
    public let cases: [ConformanceCase]
    /// Original document bytes used for corpus identity.
    public let sourceBytes: [UInt8]
    /// Validated document-level metadata.
    public let frontmatter: ConformanceData
    /// The complete frontmatter including both delimiter lines, excluding the final line ending.
    public let frontmatterRange: ConformanceSourceRange
    /// Whether the original document begins with the optional UTF-8 byte order mark.
    public var hasByteOrderMark: Bool { sourceBytes.starts(with: [0xef, 0xbb, 0xbf]) }
}

/// A validated executable case, including resolved suite defaults.
public struct ConformanceCase: Sendable {
    /// Stable case identifier within its suite.
    public let id: String
    /// Globally qualified suite/case identifier.
    public let fullID: String
    /// Human-readable case title.
    public let title: String
    /// Schema-defined execution kind, such as scriptApi or programBinary.
    public let kind: String
    /// Schema-defined conformance level.
    public let level: String
    /// Mandatory capabilities whose absence is an error.
    public let requiredCore: [String]
    /// Optional capabilities whose absence skips the case.
    public let requiredOptional: [String]
    /// Validated case configuration.
    public let metadata: ConformanceData
    /// Validated behavioral and optional performance expectations.
    public let expectation: ConformanceData
    /// Source block texts in authored order.
    public let sourceBlocks: [String]
    /// Ordered named compiler inputs, with both normalized text and original byte ranges.
    public let sources: [ConformanceSourceInput]
    /// Optional authored GESA expectation.
    public let assembler: String?
    /// Ordered host input and pump instructions.
    public let steps: [ConformanceStep]
    /// The test heading through its final content line, excluding that line's ending.
    public let testRange: ConformanceSourceRange
    /// The complete semantic case fence, including its delimiters.
    public let caseMetadataRange: ConformanceSourceRange
    /// The complete semantic expectation fence when present.
    public let expectationRange: ConformanceSourceRange?
    /// The Steps heading through the last executable table row.
    public let stepsTableRange: ConformanceSourceRange?
    /// Original Markdown span of the assembler fence, when present.
    public let assemblerBlockRange: ConformanceSourceRange?
    /// Original span of the assembler payload, excluding its fence.
    public let assemblerPayloadRange: ConformanceSourceRange?
}

/// A named GES source supplied entirely by a semantic fence; names are never opened as files.
public struct ConformanceSourceInput: Sendable {
    /// Display name of this source input.
    public let name: String
    /// Logical Program group compiled from this input.
    public let programID: String
    /// Source payload with logical LF line endings.
    public let text: String
    /// Original UTF-8 bytes from the opening fence through the closing fence.
    public let blockRange: ConformanceSourceRange
    /// Original payload bytes excluding the structural line ending before the closing fence.
    public let payloadRange: ConformanceSourceRange
}

/// One chronological input row. Its receive value is the exact message name.
public struct ConformanceStep: Sendable {
    /// Stable step identifier within the case.
    public let id: String
    /// Optional structured input message.
    public let receive: ConformanceData?
    /// Requested pump mode, such as frame or completion.
    public let pump: String
    /// Optional frame opcode budget.
    public let budget: Int?
    /// Source span of the authored step.
    public let range: ConformanceSourceRange
}
