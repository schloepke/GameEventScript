// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime

// Reads schema sources and binds them, then encodes, decodes and checks documents as a case requests.
enum SchemaApiObservations {
    static func run(_ input: Input) throws -> [String: Any] {
        var result: [String: Any] = [:]
        do {
            let sources = (input["sources"]?.arrayValue ?? []).enumerated().map { GesSchemaSource(name: "schema\($0.offset).gess", text: $0.element.stringValue ?? "") }
            let set = try GesSchemaSet(sources: sources)
            let parameters = GesSchemaParameters()
            for parameter in input["parameters"]?.arrayValue ?? [] {
                let name = parameter["name"]?.stringValue ?? ""
                if let literal = parameter["literal"]?.stringValue {
                    try parameters.with(name, literal: literal)
                } else if let raw = parameter["value"] {
                    let value = try RuntimeValues.decode(raw)
                    if let integer = value.integerValue {
                        parameters.with(name, integer, unit: value.unit)
                    } else {
                        try parameters.with(name, value.floatValue ?? value.asNumber, unit: value.unit)
                    }
                }
            }
            let root = input["root"]?.stringValue
            if let root {
                let schema = try set.schema(root)
                result["canonical"] = schema.canonicalForm
                result["hash"] = hex(schema.hash)
                if input["describe"]?.boolValue == true { result["description"] = describe(schema) }
            }
            if let name = input["network"]?.stringValue {
                let network = try set.network(name).bind(parameters: parameters)
                result["network"] = ["canonical": network.canonicalForm, "hash": hex(network.hash)]
            }
            if let name = input["handle"]?.stringValue {
                let handle = try set.handleType(name)
                result["handle"] = ["canonical": handle.canonicalForm, "hash": hex(handle.hash)]
                if let state = input["contentHash"], state != .null { result["contentHash"] = try contentHash(handle, state) }
            }
            if let document = input["document"]?.stringValue { result["document"] = check(set, document, root, parameters) }
            if let read = input["readDocument"], read != .null { result["readDocument"] = try readDocument(set, read) }
            if let store = input["store"], store.objectValue != nil { result["store"] = try stores(set, store) }
            if input["bind"]?.boolValue == true, let root {
                let bound = try set.schema(root).bind(parameters: parameters)
                result["bindingHash"] = hex(bound.bindingHash)
                result["maxByteCount"] = bound.maxByteCount
                if let encode = input["encode"], encode != .null { result["encoded"] = try self.encode(bound, encode, input) }
                if let decode = input["decode"]?.stringValue { result["decoded"] = self.decode(bound, decode, input) }
                if let key = input["key"]?.stringValue { result["key"] = try GesSchemaValues.readKey(bound, bytes(key)) }
                if let message = input["complete"], message != .null { result["completed"] = try complete(bound, message) }
                if let delta = input["deltaEncode"], delta != .null { result["deltaEncoded"] = deltaEncode(bound, delta) }
                if let delta = input["deltaDecode"], delta != .null { result["deltaDecoded"] = deltaDecode(bound, delta) }
                if let dump = input["dump"]?.stringValue {
                    let read = input["dumpBaseline"]?.stringValue.map { GesSchemaDump.read(bound, data: bytes(dump), baseline: bytes($0)) } ?? GesSchemaDump.read(bound, data: bytes(dump))
                    result["dump"] = ["text": read.text, "valid": read.isValid]
                }
            }
        } catch let error as GesSchemaError {
            return ["status": "ok", "schema": ["error": ["code": error.code, "line": error.line, "column": error.column]]]
        }
        return ["status": "ok", "schema": result]
    }

    // Only what differs from the default, so that expectations stay readable.
    private static func describe(_ schema: GesSchema) -> [String: Any] {
        var result: [String: Any] = ["name": schema.name]
        switch schema.kind {
        case .schema: result["kind"] = "Schema"
        case .message: result["kind"] = "Message"
        case .messages: result["kind"] = "Messages"
        }
        if let wire = schema.wireGroup { result["wireGroup"] = wire }
        if let model = schema.modelGroup { result["modelGroup"] = model }
        if !schema.parameters.isEmpty {
            result["parameters"] = schema.parameters.map { parameter -> [String: Any] in
                var entry: [String: Any] = ["name": parameter.name, "kind": parameter.kind.rawValue]
                if parameter.unit != .none { entry["unit"] = parameter.unit.rawValue }
                return entry
            }
        }
        if schema.isAbstract { result["abstract"] = true }
        if schema.packedWidth > 0 {
            result["packedWidth"] = schema.packedWidth
            result["packedBits"] = schema.packedBits
        }
        if !schema.takes.isEmpty {
            result["takes"] = schema.takes.map { take -> [String: Any] in
                var entry: [String: Any] = ["schema": take.schema.name]
                if take.isAllOptional { entry["allOptional"] = true }
                if !take.without.isEmpty { entry["without"] = take.without }
                return entry
            }
        }
        if !schema.fields.isEmpty {
            result["fields"] = schema.fields.map { field -> [String: Any] in
                var entry: [String: Any] = ["name": field.name, "alternatives": field.alternatives.map(describe)]
                if field.isUnlabeled { entry["unlabeled"] = true }
                if field.isOptional { entry["optional"] = true }
                if field.isKey { entry["key"] = true }
                if let fallback = field.defaultValue { entry["default"] = describeDefault(fallback) }
                return entry
            }
        }
        if !schema.computedFields.isEmpty {
            result["computed"] = schema.computedFields.map { field -> [String: Any] in
                var entry: [String: Any] = ["name": field.name, "kind": field.kind.rawValue]
                if field.unit != .none { entry["unit"] = field.unit.rawValue }
                if field.isOptional { entry["optional"] = true }
                entry["expression"] = field.expression
                return entry
            }
        }
        if !schema.constants.isEmpty {
            result["constants"] = schema.constants.map { constant -> [String: Any] in
                var entry: [String: Any] = ["name": constant.name, "kind": constant.kind.rawValue]
                if constant.unit != .none { entry["unit"] = constant.unit.rawValue }
                if constant.isInteger { entry["integer"] = true }
                entry["value"] = text(constant.value)
                return entry
            }
        }
        if !schema.tags.isEmpty { result["tags"] = schema.tags }
        if !schema.members.isEmpty { result["members"] = schema.members.map(\.name) }
        return result
    }

    private static func describeDefault(_ fallback: GesSchemaDefaultInfo) -> [String: Any] {
        let kinds: [GesSchemaDefaultKind: String] = [.number: "Number", .boolean: "Boolean", .tag: "Tag", .field: "Field", .record: "Record", .empty: "Empty"]
        var described: [String: Any] = ["kind": kinds[fallback.kind]!]
        if let number = fallback.number {
            described["value"] = text(number)
        } else if fallback.kind == .boolean {
            described["value"] = fallback.boolean
        } else if fallback.kind == .record {
            described["entries"] = fallback.entries.map { ["name": $0.name, "value": describeDefault($0.value)] as [String: Any] }
        } else if fallback.kind != .empty {
            described["name"] = fallback.name
        }
        return described
    }

    private static func describe(_ type: GesSchemaType) -> [String: Any] {
        var result: [String: Any] = ["kind": type.kind.rawValue]
        if type.unit != .none { result["unit"] = type.unit.rawValue }
        limits(&result, type.isInteger, type.isWrapping, type.lower, type.upper, type.step, type.delta)
        if !type.tags.isEmpty { result["tags"] = type.tags }
        if let max = type.max { result["max"] = max }
        if !type.element.isEmpty { result["element"] = type.element.map(describe) }
        if type.depth > 0 { result["depth"] = type.depth }
        if !type.axes.isEmpty {
            result["axes"] = type.axes.map { axis -> [String: Any] in
                var entry: [String: Any] = ["name": axis.name]
                limits(&entry, axis.isInteger, axis.isWrapping, axis.lower, axis.upper, axis.step, axis.delta)
                return entry
            }
        }
        if let schema = type.schema { result["schema"] = schema.name }
        return result
    }

    private static func limits(_ result: inout [String: Any], _ integer: Bool, _ wrapping: Bool, _ lower: GesSchemaBound?, _ upper: GesSchemaBound?, _ step: GesSchemaBound?, _ delta: GesSchemaBound?) {
        if integer { result["integer"] = true }
        if wrapping { result["wrapping"] = true }
        if let lower { result["lower"] = text(lower) }
        if let upper { result["upper"] = text(upper) }
        if let step { result["step"] = text(step) }
        if let delta { result["delta"] = text(delta) }
    }

    private static func text(_ bound: GesSchemaBound) -> String { bound.isParameter ? "parameter \(bound.parameter)" : "\(bound.significand)e\(bound.exponent)" }

    // Reads a document as generated models do: the header names an expected schema, and the entries are checked against it.
    private static func readDocument(_ set: GesSchemaSet, _ input: Input) throws -> [String: Any] {
        do {
            let document = try GesSchemaDocument(try input.text("text"), schemas: set, expected: input.values("expected").map { $0.stringValue ?? "" })
            return ["schema": document.schema, "value": try RuntimeValues.encode(document.value)]
        } catch let error as GesDocumentError {
            return ["error": ["code": error.code, "line": error.line, "column": error.column, "path": error.path]]
        }
    }

    // Reads the stores in order, each with every earlier one as a dependency, from named roots or from root handles: the
    // entries of the last with its sources, the errors of the first that fails, or the code of a resolver failure.
    private static func stores(_ set: GesSchemaSet, _ input: Input) throws -> [String: Any] {
        try Validation.fields(input, ["files", "stores", "refs", "latest", "index"])
        let table = try input.values("files").map { file in
            StoreFile(root: try file.text("root"), path: try file.text("path"), text: try file.text("text"), version: try optionalInt64(file["version"]))
        }
        let files = StoreFiles(files: table)
        var refs: [String: Int64] = [:]
        for entry in input["refs"]?.objectValue ?? [] { refs[entry.key] = try optionalInt64(entry.value) }
        let resolver = StoreResolver(files: table, refs: refs, latest: try optionalInt64(input["latest"]))
        var read: [GesStore] = []
        for store in input.values("stores") {
            let type = try set.handleType(try store.text("type"))
            do {
                if let handles = store["handles"]?.arrayValue {
                    let roots = try handles.map { try GesHandle(parsing: $0.stringValue ?? "") }
                    read.append(try GesStore.read(type, roots: roots, resolver: resolver, dependencies: read))
                } else {
                    let roots = try store.values("roots").map { GesStoreRoot(name: try $0.text("name"), path: try $0.text("path")) }
                    read.append(try GesStore.read(type, roots: roots, files: files, dependencies: read))
                }
            } catch let error as GesStoreError {
                return ["errors": error.diagnostics.map(storeError)]
            } catch let error as GesHandleError {
                return ["failure": error.code]
            }
        }
        return [
            "entries": try (read.last?.entries ?? []).map { entry -> [String: Any] in
                ["id": entry.id, "handle": entry.handle.description, "origin": location(entry.origin), "value": try RuntimeValues.encode(entry.value)]
            },
            "sources": (read.last?.sources ?? []).map(\.description),
            "indexes": try read.last.map { try indexes($0, input) } ?? [:],
        ]
    }

    // The handles of the last store through each requested projection, by projection name.
    private static func indexes(_ store: GesStore, _ input: Input) throws -> [String: Any] {
        var result: [String: Any] = [:]
        for name in input["index"]?.arrayValue ?? [] {
            let text = name.stringValue ?? ""
            guard let projection = store.type.projections.first(where: { $0.name == text }) else {
                throw ConformanceExecutionError.invalidInput("':\(text)' is no projection of '@\(store.type.name)'.")
            }
            result[projection.name] = try store.index(projection).map(\.description)
        }
        return result
    }

    private static func optionalInt64(_ input: Input?) throws -> Int64? {
        guard let input, input != .null else { return nil }
        guard let text = input.numberValue, let value = Int64(text) else { throw ConformanceExecutionError.invalidInput("Expected an integer") }
        return value
    }

    private static func storeError(_ diagnostic: GesStoreDiagnostic) -> [String: Any] {
        var error: [String: Any] = ["code": diagnostic.code, "location": location(diagnostic.location)]
        if !diagnostic.fieldPath.isEmpty { error["fieldPath"] = diagnostic.fieldPath }
        if let other = diagnostic.other { error["other"] = location(other) }
        if let suggestion = diagnostic.suggestion { error["suggestion"] = suggestion }
        return error
    }

    private static func location(_ location: GesSourceLocation) -> [String: Any] {
        var result: [String: Any] = ["root": location.moduleName, "path": location.sourceName]
        if let line = location.line { result["line"] = line }
        if let column = location.column { result["column"] = column }
        return result
    }

    private struct StoreFile {
        let root: String
        let path: String
        let text: String
        let version: Int64?
    }

    private struct StoreFiles: GesStoreFiles {
        let files: [StoreFile]

        func read(root: String, path: String) -> String? { Self.find(files, root, path)?.text }

        static func find(_ files: [StoreFile], _ root: String, _ path: String) -> StoreFile? {
            files.first { $0.root.utf8.elementsEqual(root.utf8) && $0.path.utf8.elementsEqual(path.utf8) }
        }
    }

    // Answers a file handle from the table: its root is the root handle's Text id, its path the part. A present file
    // answers with its own version, else the requested one, else the latest; refs name revisions by their text form.
    private struct StoreResolver: GesHandleResolver {
        let files: [StoreFile]
        let refs: [String: Int64]
        let latest: Int64?

        func resolve(_ call: GesHandleResolveCall) throws {
            let handle = call.handle
            let root = handle.id.valueKind == .text ? handle.id.asText : String(handle.id.asInteger)
            guard let file = StoreFiles.find(files, root, handle.part.asText) else {
                try call.setMissing()
                return
            }
            if let own = file.version {
                try call.setDocument(file.text, version: .integer(own))
            } else if handle.hasVersion {
                try call.setDocument(file.text, version: handle.version)
            } else if let latest {
                try call.setDocument(file.text, version: .integer(latest))
            } else {
                try call.setDocument(file.text)
            }
        }

        func resolveRef(_ handle: GesHandle) throws -> GesValue {
            let name = handle.ref.valueKind == .tag ? "#" + handle.ref.asText : handle.ref.asText
            return refs[name].map { .integer($0) } ?? .nothing
        }
    }

    private static func check(_ set: GesSchemaSet, _ document: String, _ root: String?, _ parameters: GesSchemaParameters) -> [String: Any] {
        do {
            if let root {
                try set.schema(root).checkDocument(document, parameters: parameters)
            } else {
                try set.checkDocument(document, parameters: parameters)
            }
            return ["ok": true]
        } catch let error as GesSchemaError {
            return ["error": ["code": error.code, "line": error.line, "column": error.column]]
        } catch {
            return ["error": ["code": "\(error)", "line": 0, "column": 0]]
        }
    }

    private static func contentHash(_ handle: GesHandleType, _ state: Input) throws -> Any {
        do {
            return try GesSchemaValues.contentHash(handle, state: RuntimeValues.decode(state)).description
        } catch let error as GesWireError {
            return ["error": ["code": error.code, "path": error.path]]
        }
    }

    private static func encode(_ bound: GesBoundSchema, _ value: Input, _ input: Input) throws -> [String: Any] {
        do {
            let size = integer(input["encodeBuffer"])
            var subject = try RuntimeValues.decode(value)
            let externalFields = input.values("externalFields").compactMap(\.stringValue)
            if !externalFields.isEmpty {
                let fields: [(key: String, value: GesValue)] = try subject.asMap!.map { entry in
                    (key: entry.key, value: externalFields.contains(entry.key) ? .external(try ApiObservations.ExternalRecord(entry.value)) : entry.value)
                }
                subject = .record(typeName: subject.customTypeName!, fields)
            }
            if input["encodeExternal"]?.boolValue == true { subject = .external(try ApiObservations.ExternalRecord(subject, failingField: input["failingField"]?.stringValue, failingCode: input["failingCode"]?.stringValue)) }
            if let baseline = input["baseline"], baseline != .null {
                return ["bytes": hexadecimal(try GesSchemaValues.encode(bound, subject, baseline: try self.baseline(baseline, input)))]
            }
            return ["bytes": hexadecimal(try GesSchemaValues.encode(bound, subject, bufferSize: size))]
        } catch let error as GesWireError {
            return ["error": ["code": error.code, "path": error.path]]
        } catch let error as any GesFatalRuntimeError {
            return ["failure": error.diagnostic.code]
        }
    }

    private static func baseline(_ baseline: Input, _ input: Input) throws -> GesValue {
        let value = try RuntimeValues.decode(baseline)
        return input["baselineExternal"]?.boolValue == true ? .external(try ApiObservations.ExternalRecord(value, failingField: input["baselineFailingField"]?.stringValue, failingCode: input["failingCode"]?.stringValue)) : value
    }

    private static func deltaEncode(_ bound: GesBoundSchema, _ input: Input) -> [String: Any] {
        let record = bytes(input["record"]!.stringValue!)
        let baseline = bytes(input["baseline"]!.stringValue!)
        var buffer = [UInt8](repeating: 0, count: bound.maxByteCount)
        do {
            let length = try buffer.withUnsafeMutableBytes { target in
                try record.withUnsafeBytes { first in
                    try baseline.withUnsafeBytes { second in
                        try GesSchemaDelta.encode(bound, record: first, baseline: second, into: target)
                    }
                }
            }
            return ["bytes": hexadecimal(Array(buffer[0..<length]))]
        } catch let error as GesWireError {
            return ["error": ["code": error.code, "path": error.path]]
        } catch {
            return ["error": ["code": "\(error)", "path": ""]]
        }
    }

    private static func deltaDecode(_ bound: GesBoundSchema, _ input: Input) -> [String: Any] {
        let delta = bytes(input["delta"]!.stringValue!)
        let baseline = bytes(input["baseline"]!.stringValue!)
        var buffer = [UInt8](repeating: 0, count: bound.maxByteCount)
        do {
            let decoded = try buffer.withUnsafeMutableBytes { target in
                try delta.withUnsafeBytes { first in
                    try baseline.withUnsafeBytes { second in
                        try GesSchemaDelta.decode(bound, delta: first, baseline: second, into: target)
                    }
                }
            }
            return ["record": hexadecimal(Array(buffer[0..<decoded.byteCount])), "deltaByteCount": decoded.deltaByteCount]
        } catch let error as GesWireError {
            return ["error": ["code": error.code, "path": error.path]]
        } catch {
            return ["error": ["code": "\(error)", "path": ""]]
        }
    }

    // The lenient read of a message, then the strict encoding of the completed message.
    private static func complete(_ bound: GesBoundSchema, _ input: Input) throws -> [String: Any] {
        guard let message = try RuntimeValues.decode(input).messageValue else { throw ConformanceExecutionError.invalidInput("complete requires a message") }
        do {
            let completed = try GesSchemaValues.completeMessage(bound, message)
            return ["value": try RuntimeValues.encode(.message(completed)), "bytes": hexadecimal(try GesSchemaValues.encode(bound, .message(completed)))]
        } catch let error as GesWireError {
            return ["error": ["code": error.code, "path": error.path]]
        }
    }

    private static func decode(_ bound: GesBoundSchema, _ hex: String, _ input: Input) -> [String: Any] {
        do {
            let limit = integer(input["decodeLimit"]) ?? .max
            let decoded: GesSchemaDecoded
            if let baseline = input["baseline"], baseline != .null {
                decoded = try GesSchemaValues.decode(bound, bytes(hex), baseline: try self.baseline(baseline, input), byteLimit: limit)
            } else {
                decoded = try GesSchemaValues.decode(bound, bytes(hex), byteLimit: limit)
            }
            return ["value": try RuntimeValues.encode(decoded.value), "byteCount": decoded.byteCount]
        } catch let error as GesWireError {
            return ["error": ["code": error.code, "path": error.path]]
        } catch let error as any GesFatalRuntimeError {
            return ["failure": error.diagnostic.code]
        } catch {
            return ["error": ["code": "\(error)", "path": ""]]
        }
    }

    private static func integer(_ input: Input?) -> Int? {
        if case .number(let text)? = input { return Int(text) }
        return nil
    }

    private static func hex(_ value: UInt64) -> String {
        let digits = String(value, radix: 16)
        return String(repeating: "0", count: 16 - digits.count) + digits
    }

    private static func hexadecimal(_ bytes: [UInt8]) -> String {
        bytes.map { $0 < 16 ? "0" + String($0, radix: 16) : String($0, radix: 16) }.joined()
    }

    private static func bytes(_ hex: String) -> [UInt8] {
        let digits = Array(hex.utf8)
        return stride(from: 0, to: digits.count, by: 2).map { UInt8(String(decoding: digits[$0..<($0 + 2)], as: UTF8.self), radix: 16) ?? 0 }
    }
}
