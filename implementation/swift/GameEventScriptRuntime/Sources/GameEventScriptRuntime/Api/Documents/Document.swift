// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// An immutable GES document: a Map or Record root with an optional schema reference, read from and printed to
/// human-editable `.gesd` text. Comments and layout are not part of a document; equality compares the root value
/// and the schema reference.
public struct GesDocument: Hashable {
    /// The Map or Record root.
    public let value: GesValue

    /// The schema reference, or nil when the document names none.
    public let schema: GesDocumentSchema?

    /// Wraps a Map or Record root. A Record's type name becomes the record type schema; a Map takes an optional file
    /// schema. Throws `GesApiError.invalidArgument` for any other root, a Record type name that is not a valid,
    /// non-built-in type name, a Record root key that is not a field name, a schema that does not match the root, or a value inside the root without readable
    /// literal text: an External value, a Dice roll below 1 or a nested Record whose type name is not a type name.
    public init(_ value: GesValue, schema: GesDocumentSchema? = nil) throws {
        switch value.kind {
        case .map:
            if schema?.kind == .type { throw GesApiError.invalidArgument("A record type schema requires a Record root.") }
            self.schema = schema
        case .record:
            let name = value.customTypeName!
            guard GesDocumentSchema.isRecordTypeName(name) else { throw GesApiError.invalidArgument("A document Record root needs a non-built-in type name.") }
            guard value.asMap!.keyStorage.allSatisfy(GesNames.plain) else { throw GesApiError.invalidArgument("A document Record root needs field names as keys.") }
            let own = try GesDocumentSchema(kind: .type, name: name)
            if let schema, schema != own { throw GesApiError.invalidArgument("A Record root carries the record type schema of its own type name.") }
            self.schema = own
        default:
            throw GesApiError.invalidArgument("A document root must be a Map or a Record.")
        }
        guard GesDocumentFormatter.isPrintable(value) else { throw GesApiError.invalidArgument("A document cannot contain External values, Dice rolls below 1 or Records without a type name.") }
        self.value = value
    }

    /// Reads GES document text, for example the contents of a `.gesd` file. Throws `GesDocumentError` with a stable
    /// code and the line and column of the first error when the text is not a valid document or exceeds a literal
    /// parsing limit.
    public init(parsing text: String) throws {
        (value, schema) = try GesLiteralParser.parseDocument(text)
    }

    /// The root entries: the Map entries or the Record fields, in Map order.
    public var entries: GesValueMap { value.asMap! }

    /// Prints the document as LF-terminated text that `init(parsing:)` reads back to an equal document, with the
    /// schema header first; an empty document without a schema prints empty text. The layout follows `format` as a
    /// recommendation only.
    public func text(format: GesDocumentFormat = .init()) -> String { GesDocumentFormatter.print(self, format) }
}

/// The kind of schema a GES document references.
public enum GesDocumentSchemaKind: Hashable, Sendable {
    /// A record type, written `:Schema(:TypeName)`; the document root is a Record of that type.
    case type
    /// A schema file, written `:Schema('path.gess')`; the host interprets the path and the root is a Map.
    case file
}

/// The schema reference of a GES document. It is a reference only: GES neither loads schema files nor validates
/// documents against record definitions.
public struct GesDocumentSchema: Hashable, Sendable {
    /// Whether `name` is a record type name or a schema file path.
    public let kind: GesDocumentSchemaKind

    /// The record type name or the schema file path.
    public let name: String

    /// Creates a schema reference. Throws `GesApiError.invalidArgument` when a record type name does not follow the
    /// source type name grammar or names a built-in type, or when a file path is empty.
    public init(kind: GesDocumentSchemaKind, name: String) throws {
        switch kind {
        case .type where !Self.isRecordTypeName(name): throw GesApiError.invalidArgument("A schema record type must be a non-built-in type name.")
        case .file where name.isEmpty: throw GesApiError.invalidArgument("A schema file path must not be empty.")
        default: break
        }
        self.kind = kind
        self.name = name
    }

    /// A record type schema reference; throws like `init(kind:name:)`.
    public static func type(_ name: String) throws -> Self { try Self(kind: .type, name: name) }

    /// A schema file reference; throws like `init(kind:name:)`.
    public static func file(_ path: String) throws -> Self { try Self(kind: .file, name: path) }

    /// Schema names compare by Unicode scalars, like other GES Text.
    public static func == (left: Self, right: Self) -> Bool { left.kind == right.kind && GesText.scalarEqual(left.name, right.name) }

    /// Hashes the kind and the Unicode scalars of the name.
    public func hash(into hasher: inout Hasher) {
        hasher.combine(kind)
        GesText.hashScalars(name, into: &hasher)
    }

    static func isRecordTypeName(_ name: String) -> Bool {
        guard GesNames.type(name) else { return false }
        let type = name.lowercased()
        return !(GesDataConstructionShape.types.contains(type) && name == type.prefix(1).uppercased() + type.dropFirst())
    }
}

/// Layout options for printing GES documents and values. Layout is a recommendation: every printed document reads
/// back to an equal document, while indentation, line breaks and numeric spelling may differ between implementations.
public struct GesDocumentFormat: Hashable, Sendable {
    /// The largest supported `indentation`.
    public static let maximumIndentation = 100

    /// Spaces per nesting level of a block container; 2 by default. Values below 0 print as 0 and values above
    /// `maximumIndentation` as `maximumIndentation`.
    public var indentation: Int

    /// Maximum line width in Unicode scalars up to which a container is printed inline; 100 by default. Values
    /// below 1 print as 1.
    public var lineWidth: Int

    /// Whether scalar quantities use exact prefixed unit spellings such as `21.4kN` or `250ms`; on by default. When
    /// off, every quantity uses its base unit symbol.
    public var prefixedUnits: Bool

    /// Creates layout options with the given or default values.
    public init(indentation: Int = 2, lineWidth: Int = 100, prefixedUnits: Bool = true) {
        self.indentation = indentation
        self.lineWidth = lineWidth
        self.prefixedUnits = prefixedUnits
    }
}

/// GES document text is invalid or exceeds a fixed literal parsing limit.
public struct GesDocumentError: Error, Hashable, Sendable {
    /// The stable error code, such as `document.unquotedText`; see the GES document specification.
    public let code: String

    /// The 1-based line of the offending position.
    public let line: Int

    /// The 1-based column of the offending position, counted in Unicode scalars.
    public let column: Int

    /// The stable name of the exceeded limit for `document.limitExceeded`, such as `MaxLiteralDepth`; otherwise nil.
    public let limitName: String?

    /// The exceeded limit value for `document.limitExceeded`; otherwise nil.
    public let limit: Int?

    /// Creates a document error at a source position.
    public init(code: String, line: Int, column: Int, limitName: String? = nil, limit: Int? = nil) {
        self.code = code
        self.line = line
        self.column = column
        self.limitName = limitName
        self.limit = limit
    }

    /// A diagnostic message that is not part of the contract.
    public var message: String {
        if let limitName { return "GES document exceeds \(limitName) (\(limit ?? 0)) at line \(line), column \(column)." }
        return "Invalid GES document (\(code)) at line \(line), column \(column)."
    }
}

// Stable document error codes owned by specs/Documents.md.
enum GesDocumentErrorCodes {
    static let invalidKey = "document.invalidKey"
    static let missingColon = "document.missingColon"
    static let duplicateKey = "document.duplicateKey"
    static let unquotedText = "document.unquotedText"
    static let unknownUnit = "document.unknownUnit"
    static let invalidLiteral = "document.invalidLiteral"
    static let unknownType = "document.unknownType"
    static let unexpectedToken = "document.unexpectedToken"
    static let unclosedBracket = "document.unclosedBracket"
    static let unclosedText = "document.unclosedText"
    static let invalidSchemaHeader = "document.invalidSchemaHeader"
    static let misplacedSchemaHeader = "document.misplacedSchemaHeader"
    static let limitExceeded = "document.limitExceeded"
}
