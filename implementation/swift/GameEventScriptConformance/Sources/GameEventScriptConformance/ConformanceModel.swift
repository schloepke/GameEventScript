// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// An immutable, ordered value from the portable Conformance YAML subset.
/// Numeric tokens retain their spelling until a schema or runtime codec consumes them.
public indirect enum ConformanceData: Equatable, Sendable {
    case null
    case bool(Bool)
    case string(String)
    case number(String)
    case array([ConformanceData])
    case object([ConformanceEntry])

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

    public subscript(_ key: String) -> ConformanceData? {
        objectValue?.first(where: { $0.key.utf8.elementsEqual(key.utf8) })?.value
    }
    public var stringValue: String? {
        if case .string(let value) = self { return value }
        return nil
    }
    public var numberValue: String? {
        if case .number(let value) = self { return value }
        return nil
    }
    public var boolValue: Bool? {
        if case .bool(let value) = self { return value }
        return nil
    }
    public var arrayValue: [ConformanceData]? {
        if case .array(let value) = self { return value }
        return nil
    }
    public var objectValue: [ConformanceEntry]? {
        if case .object(let value) = self { return value }
        return nil
    }
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
    public let key: String
    public let value: ConformanceData
    public init(key: String, value: ConformanceData) {
        self.key = key
        self.value = value
    }
    public static func == (lhs: Self, rhs: Self) -> Bool {
        lhs.key.utf8.elementsEqual(rhs.key.utf8) && lhs.value == rhs.value
    }
}

/// The validated suite and exact original bytes supplied by the embedding.
public struct ConformanceDocument: Sendable {
    public let formatVersion: Int
    public let suiteID: String
    public let title: String
    public let cases: [ConformanceCase]
    public let sourceBytes: [UInt8]
    public let frontmatter: ConformanceData
    /// The complete frontmatter including both delimiter lines, excluding the final line ending.
    public let frontmatterRange: ConformanceSourceRange
    /// Whether the original document begins with the optional UTF-8 byte order mark.
    public var hasByteOrderMark: Bool { sourceBytes.starts(with: [0xef, 0xbb, 0xbf]) }
}

/// A validated executable case, including resolved suite defaults.
public struct ConformanceCase: Sendable {
    public let id: String
    public let fullID: String
    public let title: String
    public let kind: String
    public let level: String
    public let requiredCore: [String]
    public let requiredOptional: [String]
    public let metadata: ConformanceData
    public let expectation: ConformanceData
    public let sourceBlocks: [String]
    /// Ordered named compiler inputs, with both normalized text and original byte ranges.
    public let sources: [ConformanceSourceInput]
    public let assembler: String?
    public let steps: [ConformanceStep]
    /// The test heading through its final content line, excluding that line's ending.
    public let testRange: ConformanceSourceRange
    /// The complete semantic case fence, including its delimiters.
    public let caseMetadataRange: ConformanceSourceRange
    /// The complete semantic expectation fence when present.
    public let expectationRange: ConformanceSourceRange?
    /// The Steps heading through the last executable table row.
    public let stepsTableRange: ConformanceSourceRange?
    public let assemblerBlockRange: ConformanceSourceRange?
    public let assemblerPayloadRange: ConformanceSourceRange?
}

/// A named GES source supplied entirely by a semantic fence; names are never opened as files.
public struct ConformanceSourceInput: Sendable {
    public let name: String
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
    public let id: String
    public let receive: ConformanceData?
    public let pump: String
    public let budget: Int?
    public let range: ConformanceSourceRange
}
