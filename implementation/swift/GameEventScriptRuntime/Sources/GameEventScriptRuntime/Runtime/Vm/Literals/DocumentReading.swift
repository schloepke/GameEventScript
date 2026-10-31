// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

/// Builds the value of a document that the Syntax reader has read; explicit forms construct here.
struct GesDocumentReading {
    private var nodes: [GesLiteralNode] = []

    /// Throws GesDocumentError for invalid text or an exceeded literal parsing limit.
    static func read(_ text: String) throws -> (GesValue, GesDocumentSchema?) {
        let syntax = GesDocumentReader.read(text)
        if let diagnostic = syntax.diagnostic {
            throw error(syntax, diagnostic.code, diagnostic.start, limitName: diagnostic.limitName, limit: diagnostic.limitName == nil ? nil : diagnostic.limitValue)
        }
        let schema: GesDocumentSchema?
        switch syntax.schema {
        case .none: schema = nil
        case .type(let name): schema = try GesDocumentSchema(kind: .type, name: name)
        case .file(let path): schema = try GesDocumentSchema(kind: .file, name: path)
        }
        var reading = Self()
        let root = reading.value(syntax.root!)
        let map: GesValue
        do {
            let evaluated = try GesLiteralEvaluation.evaluateDocument(root, nodes: reading.nodes)
            guard let value = evaluated.value else { throw error(syntax, GesDocumentDiagnostic.invalidLiteral, evaluated.invalidOffset) }
            map = value
        } catch let limit as GesLiteralLimitError {
            throw error(syntax, GesDocumentDiagnostic.limitExceeded, limit.offset, limitName: limit.limitName, limit: limit.limit)
        }
        guard let schema, schema.kind == .type else { return (map, schema) }
        return (.record(typeName: schema.name, fields: map.asMap!), schema)
    }

    private static func error(_ syntax: GesDocumentSyntax, _ code: String, _ offset: Int, limitName: String? = nil, limit: Int? = nil) -> GesDocumentError {
        let (line, column) = syntax.location(offset)
        return GesDocumentError(code: code, line: line, column: column, limitName: limitName, limit: limit)
    }

    // Scalars become values; Maps, forms and messages become literal nodes for document evaluation.
    private mutating func value(_ node: GesDocumentNode) -> GesValue {
        switch node {
        case .text(let text): return .text(text)
        case .boolean(let value): return .boolean(value)
        case .nothing: return .nothing
        case .tag(let name): return (try? .tag(name)) ?? .nothing
        case .number(let number): return GesTextNumberCast.value(number, sink: GesValueFactory())!
        case .spatial(let point, let x, let y, let z, let unit): return point ? .point(x: x, y: y, z: z, unit: unit) : .vector(x: x, y: y, z: z, unit: unit)
        case .dice(let rolls): return .dice(rolls)
        case .list(let items): return .list(items.map { value($0) })
        case .map(let keys, let values): return literal("@map", keys, values, offset: 0)
        case .form(let type, let labels, let arguments, let offset): return literal(type, labels, arguments, offset: offset)
        case .message(let name, let labels, let arguments, let offset): return literal("@message:" + name, labels, arguments, offset: offset)
        case .handler(let name, let labels): return (try? .handler(GesMessageSignature(name: name, parameters: labels))) ?? .nothing
        }
    }

    private mutating func literal(_ type: String, _ labels: [String], _ arguments: [GesDocumentNode], offset: Int) -> GesValue {
        let values = arguments.map { value($0) }
        let index = nodes.count
        nodes.append(.init(type: type, labels: labels, arguments: values, constructor: nil, offset: offset))
        return .record(typeName: "@literal", entries: [.init(key: "id", value: .integer(Int64(index)))])
    }
}
