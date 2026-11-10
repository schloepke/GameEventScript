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
            if let document = input["document"]?.stringValue { result["document"] = check(set, document, root, parameters) }
            if input["bind"]?.boolValue == true, let root {
                let bound = try set.schema(root).bind(parameters: parameters)
                result["bindingHash"] = hex(bound.bindingHash)
                result["maxByteCount"] = bound.maxByteCount
                if let encode = input["encode"], encode != .null { result["encoded"] = try self.encode(bound, encode, input) }
                if let decode = input["decode"]?.stringValue { result["decoded"] = self.decode(bound, decode, input) }
                if let key = input["key"]?.stringValue { result["key"] = try GesSchemaValues.readKey(bound, bytes(key)) }
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
        if !schema.fields.isEmpty {
            result["fields"] = schema.fields.map { field -> [String: Any] in
                var entry: [String: Any] = ["name": field.name, "alternatives": field.alternatives.map(describe)]
                if field.isUnlabeled { entry["unlabeled"] = true }
                if field.isOptional { entry["optional"] = true }
                if field.isKey { entry["key"] = true }
                return entry
            }
        }
        if !schema.tags.isEmpty { result["tags"] = schema.tags }
        if !schema.members.isEmpty { result["members"] = schema.members.map(\.name) }
        return result
    }

    private static func describe(_ type: GesSchemaType) -> [String: Any] {
        var result: [String: Any] = ["kind": type.kind.rawValue]
        if type.unit != .none { result["unit"] = type.unit.rawValue }
        limits(&result, type.isInteger, type.isWrapping, type.lower, type.upper, type.step, type.delta)
        if !type.tags.isEmpty { result["tags"] = type.tags }
        if let max = type.max { result["max"] = max }
        if !type.element.isEmpty { result["element"] = type.element.map(describe) }
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
