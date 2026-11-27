// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// A value of a read document. Scalars carry their recognized value; Runtime builds the values of forms, messages
/// and handlers.
@_spi(Syntax) public indirect enum GesDocumentNode: Sendable, Hashable {
    case text(String)
    case boolean(Bool)
    case nothing
    case tag(String)
    case number(GesNumberLiteral)
    /// A List with the UTF-8 offset of each item, which schema checks report.
    case list([GesDocumentNode], offsets: [Int])
    /// A Map with the UTF-8 offsets of its keys and values, which schema checks report. `handleKeys` marks the keys
    /// written as handles, whose text form is the key; it is empty when no key is a handle.
    case map(keys: [String], values: [GesDocumentNode], keyOffsets: [Int], valueOffsets: [Int], handleKeys: [Bool])
    /// A handle such as `@Drone:7~120` in its text form as written; Runtime parses it. In a store's document, `pinned`
    /// marks a trailing `~`, which the text leaves out and which pins the handle to the named entry's hash. `offset` is
    /// the UTF-8 offset of its `@`.
    case handle(String, pinned: Bool = false, offset: Int = 0)
    /// A store reference such as `@hull.light~` by its name, whether a `~` pins it, and the UTF-8 offset of its `@`; a
    /// store replaces it by a handle (specs/Documents.md#references).
    case reference(String, pinned: Bool, offset: Int)
    case dice([Int32])
    case bytes([UInt8])
    case spatial(point: Bool, x: Double, y: Double, z: Double, unit: GesQuantityUnit)
    /// An explicit form such as `:Range(1, 5)` with its lowercase type, labels (`_` when unlabeled), arguments and
    /// the UTF-8 offset of its `:`, where construction errors point.
    case form(type: String, labels: [String], arguments: [GesDocumentNode], offset: Int)
    /// The message of `:Message(Done(v: 1))` before its tags.
    case message(name: String, labels: [String], arguments: [GesDocumentNode], offset: Int)
    case handler(name: String, labels: [String])
}

/// The schema reference of a read document.
@_spi(Syntax) public enum GesDocumentSchemaSyntax: Sendable, Equatable {
    case none
    /// `:Schema(:DriveUnit)`: the root is a Record of that type.
    case type(String)
    /// `:Schema('DriveUnit.gess')`: the root is a Map carrying the path.
    case file(String)
}

/// The error of an unreadable document: its stable code and its UTF-8 range, which starts at the position the code
/// reports. A limit error also names the limit and its value.
@_spi(Syntax) public struct GesDocumentDiagnostic: Sendable, Equatable {
    @_spi(Syntax) public let code: String
    @_spi(Syntax) public let start: Int
    @_spi(Syntax) public let end: Int
    @_spi(Syntax) public let limitName: String?
    @_spi(Syntax) public let limitValue: Int

    init(_ code: String, _ start: Int, _ end: Int, limitName: String? = nil, limitValue: Int = 0) {
        self.code = code
        self.start = start
        self.end = end
        self.limitName = limitName
        self.limitValue = limitValue
    }

    @_spi(Syntax) public static let invalidKey = "document.invalidKey"
    @_spi(Syntax) public static let missingColon = "document.missingColon"
    @_spi(Syntax) public static let duplicateKey = "document.duplicateKey"
    @_spi(Syntax) public static let unquotedText = "document.unquotedText"
    @_spi(Syntax) public static let unknownUnit = "document.unknownUnit"
    @_spi(Syntax) public static let invalidLiteral = "document.invalidLiteral"
    @_spi(Syntax) public static let unknownType = "document.unknownType"
    @_spi(Syntax) public static let unexpectedToken = "document.unexpectedToken"
    @_spi(Syntax) public static let unclosedBracket = "document.unclosedBracket"
    @_spi(Syntax) public static let unclosedText = "document.unclosedText"
    @_spi(Syntax) public static let invalidSchemaHeader = "document.invalidSchemaHeader"
    @_spi(Syntax) public static let misplacedSchemaHeader = "document.misplacedSchemaHeader"
    @_spi(Syntax) public static let limitExceeded = "document.limitExceeded"
    @_spi(Syntax) public static let invalidHandle = "document.invalidHandle"
    @_spi(Syntax) public static let unexpectedModule = "document.unexpectedModule"

    // The English explanation of an error code for highlighters and tools; it is not part of the contract.
    @_spi(Syntax) public static func explain(_ code: String, limitName: String?, limitValue: Int) -> String {
        switch code {
        case invalidKey: return "An entry must start with a key; a record field must be a field name."
        case missingColon: return "A key must be followed by ':' on its line."
        case duplicateKey: return "This key is already used in its container."
        case unquotedText: return "Text values must be quoted."
        case unknownUnit: return "The number has an unknown unit."
        case invalidLiteral: return "This is not a valid literal."
        case unknownType: return "Documents know only the built-in types."
        case unexpectedToken: return "An entry, separator or closing bracket is expected here."
        case unclosedBracket: return "This bracket is never closed."
        case unclosedText: return "This Text is never closed."
        case invalidSchemaHeader: return "A schema header names one record type or a schema file; a store header one handle type."
        case misplacedSchemaHeader: return "The schema header must come before the first entry."
        case invalidHandle: return "This is not a handle, or a handle key has a version."
        case unexpectedModule: return "Module blocks belong only at the root of a store file."
        case limitExceeded: return "The document exceeds \(limitName ?? "a limit") (\(limitValue))."
        default: return "This is not a valid document."
        }
    }
}

/// A read document: the Map root of its entries and its schema reference, or the first error that stops reading.
/// Construction of explicit forms and the Range materialization total are left to Runtime.
@_spi(Syntax) public struct GesDocumentSyntax: Sendable {
    /// The UTF-8 bytes of the text, which diagnostic and form offsets index.
    @_spi(Syntax) public let bytes: [UInt8]
    /// The entries as a Map node, absent when reading failed.
    @_spi(Syntax) public let root: GesDocumentNode?
    @_spi(Syntax) public let schema: GesDocumentSchemaSyntax
    @_spi(Syntax) public let diagnostic: GesDocumentDiagnostic?

    /// The 1-based line and column of a UTF-8 offset, in Unicode scalars; CRLF is one line break and a leading byte
    /// order mark is not counted.
    @_spi(Syntax) public func location(_ offset: Int) -> (line: Int, column: Int) { Self.location(bytes, offset) }

    init(bytes: [UInt8], root: GesDocumentNode?, schema: GesDocumentSchemaSyntax, diagnostic: GesDocumentDiagnostic?) {
        self.bytes = bytes
        self.root = root
        self.schema = schema
        self.diagnostic = diagnostic
    }

    /// A read document of a root in a text's UTF-8 bytes, such as a store entry's value whose references became
    /// handles; it has no schema reference and no error.
    @_spi(Syntax) public init(bytes: [UInt8], root: GesDocumentNode) { self.init(bytes: bytes, root: root, schema: .none, diagnostic: nil) }

    /// The 1-based line and column of a UTF-8 offset in any text, by the same rules.
    @_spi(Syntax) public static func location(_ bytes: [UInt8], _ offset: Int) -> (line: Int, column: Int) {
        var line = 1
        var column = 1
        var index = bytes.starts(with: [0xEF, 0xBB, 0xBF]) ? 3 : 0
        while index < offset && index < bytes.count {
            let byte = bytes[index]
            index += 1
            if byte == 10 || byte == 13 {
                if byte == 13 && index < bytes.count && bytes[index] == 10 && index < offset { index += 1 }
                line += 1
                column = 1
            } else if byte & 0xC0 != 0x80 {
                column += 1
            }
        }
        return (line, column)
    }
}

/// What one item of a store file holds.
@_spi(Syntax) public enum GesStoreItemKind: Sendable {
    /// A root `import 'path'` of a further store file.
    case `import`
    /// `key: value` at the root or in a module block.
    case entry
    /// `key: import 'path'`, whose state comes from a state document.
    case entryImport
}

/// One item of a store file: an entry with its module, key and value, or an import with its path.
@_spi(Syntax) public struct GesStoreItem: Sendable {
    @_spi(Syntax) public let kind: GesStoreItemKind
    /// The module name of the block holding the entry, empty at the root.
    @_spi(Syntax) public let module: String
    @_spi(Syntax) public let key: String
    /// The UTF-8 offset of the key, or of the `import` word of a root import.
    @_spi(Syntax) public let keyOffset: Int
    /// The entry's Map value, which may contain references.
    @_spi(Syntax) public let value: GesDocumentNode?
    /// The UTF-8 offset of the entry's value.
    @_spi(Syntax) public let valueOffset: Int
    /// The import path as written.
    @_spi(Syntax) public let path: String
    /// The UTF-8 offset of the import path's opening quote, or -1.
    @_spi(Syntax) public let pathOffset: Int
}

/// A read store file (specs/Documents.md#store-files): its handle type and items, or the first error. A text
/// without a `:Store` header is no store file and has no error.
@_spi(Syntax) public struct GesStoreSyntax: Sendable {
    /// The UTF-8 bytes of the text, which item and diagnostic offsets index.
    @_spi(Syntax) public let bytes: [UInt8]
    @_spi(Syntax) public let isStore: Bool
    /// The handle type name of the header, without `@`.
    @_spi(Syntax) public let typeName: String
    /// The UTF-8 offset of the header's `:`.
    @_spi(Syntax) public let headerOffset: Int
    @_spi(Syntax) public let items: [GesStoreItem]
    @_spi(Syntax) public let diagnostic: GesDocumentDiagnostic?
}
