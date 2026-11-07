// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime

// Reads documents through GesDocument(parsing:) or builds them from a value, then optionally prints and rereads them.
enum DocumentApiObservations {
    static func run(_ input: Input) throws -> [String: Any] {
        var result: [String: Any] = [:]
        let document: GesDocument?
        if let text = input["text"]?.stringValue {
            let (read, observation) = try self.read(text)
            document = read
            result["read"] = observation
        } else {
            let schema = input["schema"] == .null ? nil : try self.schema(input.required("schema"))
            do {
                document = try GesDocument(RuntimeValues.decode(input.required("value")), schema: schema)
            } catch is GesApiError {
                return ["status": "ok", "document": ["rejected": true]]
            }
        }
        if let document, let print = input["print"], print != .null {
            let printed = document.text(format: try format(print))
            result["printed"] = printed
            result["reread"] = try read(printed).1
        }
        return ["status": "ok", "document": result]
    }

    // Only document errors are observations; any other failure is an adapter error, like an exception in C#.
    private static func read(_ text: String) throws -> (GesDocument?, [String: Any]) {
        do {
            let document = try GesDocument(parsing: text)
            let value = try RuntimeValues.encode(document.value)
            return (document, ["value": value, "schema": encode(document.schema)])
        } catch let error as GesDocumentError {
            var observed: [String: Any] = ["code": String(error.code.dropFirst("document.".count)), "line": error.line, "column": error.column]
            if let name = error.limitName, let limit = error.limit {
                observed["limitName"] = name
                observed["limit"] = limit
            }
            return (nil, ["error": observed])
        }
    }

    private static func encode(_ schema: GesDocumentSchema?) -> Any {
        guard let schema else { return NSNull() }
        return ["kind": schema.kind == .type ? "type" : "file", "name": schema.name]
    }

    static func schema(_ input: Input) throws -> GesDocumentSchema {
        try Validation.fields(input, ["kind", "name"])
        let name = try input.text("name")
        switch try input.text("kind") {
        case "type": return try .type(name)
        case "file": return try .file(name)
        default: throw ConformanceExecutionError.invalidInput("Invalid schema kind")
        }
    }

    static func format(_ input: Input) throws -> GesDocumentFormat {
        try Validation.fields(input, [], optional: ["indentation", "lineWidth", "prefixedUnits"])
        var format = GesDocumentFormat()
        if let indentation = input["indentation"] {
            guard let value = indentation.integerValue else { throw ConformanceExecutionError.invalidInput("Invalid indentation") }
            format.indentation = value
        }
        if let width = input["lineWidth"] {
            guard let value = width.integerValue else { throw ConformanceExecutionError.invalidInput("Invalid line width") }
            format.lineWidth = value
        }
        if let prefixed = input["prefixedUnits"] {
            guard let value = prefixed.boolValue else { throw ConformanceExecutionError.invalidInput("Invalid prefixed units") }
            format.prefixedUnits = value
        }
        return format
    }
}
