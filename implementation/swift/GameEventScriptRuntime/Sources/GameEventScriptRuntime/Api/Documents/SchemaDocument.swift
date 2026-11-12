// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

/// A GES document read against the schema its header names, as generated models read their documents.
///
/// The header `:Schema(:Name)` names one of the expected schemas, the document passes the schema's check, and its entries
/// are the plain value that a model converts. Errors carry their position and field path.
public struct GesSchemaDocument {
    /// The code of a document whose header names no expected schema.
    public static let schemaMismatch = "document.schemaMismatch"

    /// The schema the header names.
    public let schema: String

    /// The document's entries as a Record of ``schema``, which a model of it converts.
    public let value: GesValue

    private let syntax: GesDocumentSyntax

    /// Reads document text whose header names one of the expected schemas and checks it against that schema.
    /// - Parameters:
    ///   - text: The document text.
    ///   - schemas: The schema set that declares the expected schemas.
    ///   - expected: The schemas the header may name.
    /// - Throws: `GesDocumentError` when the text is no document, its header names no expected schema (``schemaMismatch``,
    ///   naming both), or it breaks the schema, with the `document.*` code, its position and the field path.
    public init(_ text: String, schemas: GesSchemaSet, expected: [String]) throws(GesDocumentError) {
        let value: GesValue
        do {
            value = try GesDocumentReading.read(text).0
        } catch let error as GesDocumentError {
            throw error
        } catch {
            throw GesDocumentError(code: "document.invalidLiteral", line: 1, column: 1)
        }
        let syntax = GesDocumentReader.read(text)
        var named: String?
        if case .type(let name) = syntax.schema { named = name }
        guard let name = named, expected.contains(name) else {
            let wanted = expected.map { ":" + $0 }.joined(separator: " or ")
            throw GesDocumentError(code: Self.schemaMismatch, line: 1, column: 1, message: "The document names \(named.map { ":" + $0 } ?? "no schema") where \(wanted) is expected.")
        }
        let failure: GesDocumentError?
        let declaration: GesResolvedDeclaration
        do {
            declaration = try schemas.declaration(name)
            failure = try GesSchemaDocumentCheck.failing(syntax, declaration, nil)
        } catch {
            throw GesDocumentError(code: Self.schemaMismatch, line: 1, column: 1, message: "The schema set cannot check :\(name): \(error).")
        }
        if let failure { throw failure }
        schema = name
        self.value = GesSchemaValues.documentShapes(declaration, value)
        self.syntax = syntax
    }

    /// Places an error of converting ``value`` into a model at the value of its path in the document: the deepest entry the
    /// path names, and a List for an element of it, since paths name no index.
    /// - Parameter error: The conversion error.
    /// - Returns: A document error with the error's code and path.
    public func error(for error: GesWireError) -> GesDocumentError {
        var node = syntax.root
        var offset = 0
        for segment in error.path.split(separator: ".", omittingEmptySubsequences: false) {
            let name = segment.prefix { $0 != "[" && $0 != ":" }
            guard let current = node, case .map(let keys, let values, _, let valueOffsets) = Self.entries(current), let index = keys.firstIndex(of: String(name)) else { break }
            node = values[index]
            offset = valueOffsets[index]
            if segment.contains("[") { break }
        }
        let (line, column) = syntax.location(offset)
        return GesDocumentError(code: error.code, line: line, column: column, path: error.path, message: "\(error) at line \(line), column \(column).")
    }

    // The entries of a Map or of a Record form.
    private static func entries(_ node: GesDocumentNode) -> GesDocumentNode? {
        if case .map = node { return node }
        guard case .form("record", let labels, let arguments, _) = node, let positions = GesLiteralForms.positions("record", labels), let fields = positions.firstIndex(of: 1) else { return nil }
        if case .map = arguments[fields] { return arguments[fields] }
        return nil
    }
}
