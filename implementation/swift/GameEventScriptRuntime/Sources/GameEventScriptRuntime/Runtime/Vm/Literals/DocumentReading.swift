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

    // The value an explicit form of a readable document constructs, as the node a schema check reads. Forms a check accepts
    // by their type stay forms; every position inside the result is that of the form.
    static func resolve(_ form: GesDocumentNode, start: Int) -> GesDocumentNode {
        var reading = Self()
        let root = reading.value(.map(keys: ["v"], values: [form], keyOffsets: [0], valueOffsets: [0]))
        guard let evaluated = try? GesLiteralEvaluation.evaluateDocument(root, nodes: reading.nodes), let fields = evaluated.value?.asMap else { return form }
        return fields.length == 0 ? .nothing : node(fields.value(at: 0), start)
    }

    private static func node(_ value: GesValue, _ start: Int) -> GesDocumentNode {
        switch value.valueKind {
        case .nothing: return .nothing
        case .boolean: return .boolean(value.asBoolean)
        case .integer: return .number(.integer(value.integerValue!, unit: value.unit))
        case .float: return .number(.float(value.floatValue!, unit: value.unit))
        case .percentage: return .number(.percentage(value.floatValue!))
        case .text: return .text(value.asText)
        case .tag: return .tag(value.asText)
        case .vector: return .spatial(point: false, x: value.x, y: value.y, z: value.z, unit: value.unit)
        case .point: return .spatial(point: true, x: value.x, y: value.y, z: value.z, unit: value.unit)
        case .dice: return .dice(value.asDice)
        case .bytes: return .bytes(value.asBytes)
        case .list:
            let items = value.asList
            return .list(items.map { node($0, start) }, offsets: [Int](repeating: start, count: items.count))
        case .map: return mapNode(value.asMap!, start)
        case .custom where value.customTypeName != nil:
            return .form(type: "record", labels: ["_", "_"], arguments: [.text(value.customTypeName!), mapNode(value.asMap!, start)], offset: start)
        default: return .form(type: "\(value.valueKind)", labels: [], arguments: [], offset: start)
        }
    }

    private static func mapNode(_ fields: GesValueMap, _ start: Int) -> GesDocumentNode {
        var keys: [String] = []
        var values: [GesDocumentNode] = []
        for index in 0..<fields.length {
            keys.append(fields.key(at: index))
            values.append(node(fields.value(at: index), start))
        }
        let offsets = [Int](repeating: start, count: keys.count)
        return .map(keys: keys, values: values, keyOffsets: offsets, valueOffsets: offsets)
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
        case .bytes(let values): return .bytes(values)
        case .list(let items, _): return .list(items.map { value($0) })
        case .map(let keys, let values, _, _): return literal("@map", keys, values, offset: 0)
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
