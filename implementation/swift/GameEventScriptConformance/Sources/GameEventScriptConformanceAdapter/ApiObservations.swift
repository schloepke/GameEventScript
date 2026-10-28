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
            } catch let error as GesDynamicLinkError { return ["status": "loadError", "diagnostics": [CompilerObservations.diagnostic(error.diagnostic)]] }
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
