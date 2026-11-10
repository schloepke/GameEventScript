// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime

enum ApiObservations {
    static func run(_ test: Input) throws -> [String: Any] {
        let input = try test.required("api")
        switch test["kind"]?.stringValue {
        case "valueApi":
            if let literal = input["literal"] { return try self.literal(literal) }
            if let access = input["access"] { return try self.access(access) }
            let value = try RuntimeValues.decode(input.required("value"), mutateSource: input["mutateSourceAfterCreate"]?.boolValue == true)
            let other = try input["equalTo"].flatMap { $0 == .null ? nil : try RuntimeValues.decode($0) }
            let different = try input["notEqualTo"].flatMap { $0 == .null ? nil : try RuntimeValues.decode($0) }
            return [
                "status": "ok",
                "value": [
                    "normalized": try RuntimeValues.encode(value), "isNumeric": value.isNumeric, "hasValue": value.hasValue, "isNothing": value.isNothing,
                    "hasUnit": value.hasUnit, "asBoolean": value.asBoolean, "length": value.length, "customTypeName": nullable(value.customTypeName),
                    "equal": nullable(other.map { value == $0 }), "equalHash": nullable(other.map { value.hashValue == $0.hashValue }), "notEqual": nullable(different.map { value != $0 }),
                ],
            ]
        case "messageApi": return try message(input)
        case "documentApi": return try DocumentApiObservations.run(input)
        case "schemaApi": return try SchemaApiObservations.run(input)
        case "externalTypeApi":
            let definitions = try input.values("typeNames").map { try GesExternalTypeDefinition(name: $0.stringValue!, fields: [], constructors: []) }
            do {
                let catalog = try GesDeclaredExternalTypeCatalog(definitions)
                return ["status": "ok", "externalType": ["typeCount": catalog.types.count]]
            } catch let error as GesApiError {
                guard Set(definitions.map(\.name)).count != definitions.count else { throw error }
                return ["status": "ok", "externalType": ["error": "duplicateTypeName"]]
            }
        default:
            let host = try HostScenario.makeHost(input, observer: nil, publishSink: nil)
            do {
                _ = try host.load(CompilerObservations.compile(test))
                return ["status": "loaded", "diagnostics": []]
            } catch let error as GesDynamicLinkError { return ["status": "loadError", "diagnostics": error.diagnostics.map(CompilerObservations.diagnostic)] }
        }
    }

    // Member reads and map traversal; `normalized` and `traversal` stay null for an external value, whose fields only
    // its getters could produce.
    static func access(_ access: Input) throws -> [String: Any] {
        let mode = access["external"]?.stringValue
        let external = mode != nil
        let probe = try ExternalProbe(mode ?? "counting")
        let value: GesValue = try external ? .external(probe) : access["fromEntries"]?.boolValue == true ? fromEntries(access.required("value")) : RuntimeValues.decode(access.required("value"))
        let length = value.length
        let members = try access.values("members").map { path in
            try RuntimeValues.encode(path.arrayValue!.reduce(value) { $0[$1.stringValue!] })
        }
        let traversal = try entries(value.asMap)
        let readFields: [Any] = try access.values("readFields").map { name in try observed { try nullable(value.readField(name.stringValue!).map { try RuntimeValues.encode($0) }) } }
        let materialized: [Any] = try (0..<(access["materialize"]?.integerValue ?? 0)).map { _ in try observed { try entries(value.materializedMap()) } }
        let json: Any = access["toJson"]?.boolValue == true ? try observed { try value.json() } : NSNull()
        let messageJson: Any = access["toMessageJson"]?.boolValue == true ? try observed { try GameEventScriptMessage(name: "Done", arguments: [.init(name: "value", value: value)]).json() } : NSNull()
        return [
            "status": "ok",
            "access": [
                "length": length, "json": json, "messageJson": messageJson, "normalized": external ? NSNull() : try RuntimeValues.encode(value), "members": members, "traversal": traversal, "asMapPresent": value.asMap != nil,
                "hostObject": value.externalValue === probe, "readFields": readFields, "materialized": materialized, "externalFieldReads": probe.reads,
            ],
        ]
    }

    private static func observed(_ read: () throws -> Any) throws -> Any {
        do { return try read() } catch let error as any GesFatalRuntimeError { return ["failure": error.diagnostic.code] }
    }

    private static func entries(_ map: GesValueMap?) throws -> Any {
        guard let map else { return NSNull() }
        var entries: [Any] = []
        for (key, entry) in map { entries.append(["key": key, "value": try RuntimeValues.encode(entry)]) }
        return entries
    }

    private static func fromEntries(_ input: Input) throws -> GesValue {
        let type = try input.text("type")
        let entries = try input.values("entries").map { (key: try $0.text("key"), value: try RuntimeValues.decode($0.required("value"))) }
        return type == ":Map" ? .map(entries) : .record(typeName: String(type.dropFirst()), entries)
    }

    // Exposes the fields of a Record as an external value of the same type name; a custom field type keeps each value,
    // and one getter can raise a platform failure or, with a code, a deliberate fault.
    final class ExternalRecord: GesExternalValue {
        let definition: GesExternalTypeDefinition
        private let record: GesValue
        private let failingField: String?
        private let failingCode: String?

        init(_ record: GesValue, failingField: String? = nil, failingCode: String? = nil) throws {
            self.record = record
            self.failingField = failingField
            self.failingCode = failingCode
            definition = try GesExternalTypeDefinition(name: record.customTypeName!, fields: record.asMap!.map { try GesExternalTypeFieldDefinition(name: $0.key, typeName: "Field") }, constructors: [])
        }

        func field(_ name: String) throws -> GesValue? {
            guard name == failingField else { return record.asMap!.get(name) }
            if let failingCode { throw try GesExtensionFaultError(code: failingCode, message: "Configured field fault") }
            throw ConformanceExecutionError.invalidInput("Configured field failure")
        }
    }

    // `counting` returns the number of reads, `typed` an integer that its Float<Watt> declaration turns into watts,
    // `failing` raises an unanticipated platform failure, `faulting` a deliberate fault, `ordered` a deliberate fault
    // per field and `sparse` leaves one field without a value.
    private final class ExternalProbe: GesExternalValue {
        let definition: GesExternalTypeDefinition
        private let mode: String
        var reads = 0

        init(_ mode: String) throws {
            self.mode = mode
            let fields: [GesExternalTypeFieldDefinition]
            switch mode {
            case "typed": fields = [try .init(name: "power", kind: .float, unit: .watt)]
            case "ordered": fields = try ["z", "a"].map { try .init(name: $0, typeName: "Number") }
            case "sparse": fields = try ["c", "b", "a"].map { try .init(name: $0, typeName: "Number") }
            default: fields = [try .init(name: "x", typeName: "Number")]
            }
            definition = try GesExternalTypeDefinition(name: "AccessProbe", fields: fields, constructors: [])
        }

        func field(_ name: String) throws -> GesValue? {
            reads += 1
            switch mode {
            case "failing": throw ConformanceExecutionError.invalidInput("Configured field failure")
            case "faulting": throw try GesExtensionFaultError(code: "probe.fault", message: "Configured field fault")
            case "typed": return .integer(3)
            case "ordered": throw try GesExtensionFaultError(code: "probe." + name, message: "Configured field fault")
            case "sparse": return name == "b" ? nil : .integer(name == "c" ? 1 : 3)
            default: return .integer(Int64(reads))
            }
        }
    }

    // Literal parsing through the public data API; a limit becomes an observation, not an adapter error.
    static func literal(_ literal: Input) throws -> [String: Any] {
        let text = try literal.text("text")
        do {
            let value = try literal.text("mode") == "strict" ? GesValue(strictLiteral: text) : GesValue(literal: text)
            return ["status": "ok", "literal": ["value": nullable(try value.map { try RuntimeValues.encode($0) }), "limit": NSNull()]]
        } catch let error as GesLiteralLimitError {
            return ["status": "ok", "literal": ["value": NSNull(), "limit": ["name": error.limitName, "limit": error.limit]]]
        }
    }

    static func message(_ input: Input) throws -> [String: Any] {
        func failure(_ code: String) -> [String: Any] { ["status": "ok", "message": ["error": code]] }

        if input["message"]?["args"]?.objectValue != nil { return failure("invalidArgumentsShape") }
        do {
            let signature = try RuntimeValues.signature(input.required("signature"))
            var message = try input["json"]?.stringValue.map { try GameEventScriptMessage(json: $0) } ?? RuntimeValues.decodeMessage(input.required("message"))
            if input["roundTripJson"]?.boolValue == true { message = try GameEventScriptMessage(json: message.json()) }
            var result: [String: Any] = [
                "name": message.name, "signatureId": signature.signatureId, "messageSignatureId": message.signatureId,
                "matches": signature.matches(message), "argumentCount": message.arguments.count,
            ]
            let json = input["json"] != nil || input["roundTripJson"] != nil
            if json { result["json"] = try message.json() }
            if !json || input["includeNormalized"]?.boolValue == true { result["normalized"] = try RuntimeValues.encodeMessage(message) }
            if let node = input["compareSignature"] {
                let other = try RuntimeValues.signature(node)
                result["signatureEquals"] = signature == other
                result["signatureHashEquals"] = signature.hashValue == other.hashValue
            }
            if let node = input["compareMessage"] {
                let other = try RuntimeValues.decodeMessage(node)
                result["messageEquals"] = message == other
                result["messageHashEquals"] = message.hashValue == other.hashValue
            }
            if let node = input["compareHandler"] {
                let left = GesValue.handler(signature)
                let right = try GesValue.handler(RuntimeValues.signature(node))
                result["handlerEquals"] = left == right
                result["handlerHashEquals"] = left.hashValue == right.hashValue
            }
            if input["createArguments"] != nil { result["createdMessageSignatureId"] = try signature.makeMessage(input.values("createArguments").map { try RuntimeValues.decode($0) })?.signatureId ?? "<null>" }
            return ["status": "ok", "message": result]
        } catch let error as GesMessageFormatError { return failure(error.code) } catch let error as GesMessageError {
            switch error {
            case .duplicateArgumentName: return failure("duplicateArgumentName")
            case .argumentCountMismatch, .missingArgument: return failure("invalidArgument")
            default: return failure("invalidMessage")
            }
        } catch is GesValueError { return failure("invalidMessage") }
    }
}
