// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

// The per-case handle catalog: the handle types the compiler and the host know, and the scripted resolver answers
// (specs/Conformance/MarkdownFormat.md#handle-catalogs).
struct AdapterHandles {
    private let catalog: Input

    private init(_ catalog: Input) { self.catalog = catalog }

    static func from(_ test: Input) -> AdapterHandles? { test["handles"].map(AdapterHandles.init) }

    static func validate(_ catalog: Input) throws {
        try Validation.fields(catalog, ["schemas", "hostSchemas", "resolvers", "answers"])
        _ = try parse(catalog.required("schemas"))
        _ = try parse(catalog.required("hostSchemas"))
        let resolvers = try catalog.required("resolvers")
        if resolvers != .null { _ = try Validation.strings(resolvers) }
        for answer in try Validation.array(catalog.required("answers")) {
            try Validation.fields(answer, ["handle", "answer", "text", "version"])
            _ = try GesHandle(parsing: answer.text("handle"))
            try Validation.require(["document", "record", "missing", "fail"].contains(try answer.text("answer")))
        }
    }

    // The handle types the sources may name.
    func compilerTypes() throws -> [GesHandleType] { try Self.parse(catalog.required("schemas")).handleTypes }

    // Registers a fresh scripted resolver for every selected type of the host catalog.
    func register(_ builder: GesHostBuilder) throws {
        let resolvers = try catalog.required("resolvers")
        let selected = resolvers == .null ? nil : Set(try Validation.strings(resolvers))
        var answers: [GesHandle: [Input]] = [:]
        for answer in try Validation.array(catalog.required("answers")) { answers[try GesHandle(parsing: answer.text("handle")), default: []].append(answer) }
        let resolver = ScriptedResolver(answers)
        for type in try Self.parse(catalog.required("hostSchemas")).handleTypes where selected?.contains(type.name) ?? true { builder.withHandleResolver(resolver, for: type) }
    }

    private static func parse(_ schemas: Input) throws -> GesSchemaSet {
        try GesSchemaSet(sources: Validation.strings(schemas).enumerated().map { GesSchemaSource(name: "handles-\($0.offset + 1).gess", text: $0.element) })
    }
}

private struct ScriptedResolverError: Error, CustomStringConvertible {
    let description: String
}

// Answers each object's reads in the authored order; the last answer repeats, and an object without one fails.
private final class ScriptedResolver: GesHandleResolver {
    private let answers: [GesHandle: [Input]]
    private var calls: [GesHandle: Int] = [:]

    init(_ answers: [GesHandle: [Input]]) { self.answers = answers }

    func resolve(_ call: GesHandleResolveCall) throws {
        let handle = call.handle
        guard let list = answers[handle] else { throw ScriptedResolverError(description: "The case has no answer for \(handle).") }
        let count = calls[handle] ?? 0
        calls[handle] = count + 1
        let answer = list[min(count, list.count - 1)]
        let version = try answer.required("version")
        let passed = version == .null ? GesValue.nothing : try RuntimeValues.decode(version)
        switch try answer.text("answer") {
        case "document":
            try call.setDocument(answer.text("text"), version: passed)
        case "record":
            guard let record = try GesValue(strictLiteral: answer.text("text")) else { throw ConformanceExecutionError.invalidInput("The record answer is no data literal.") }
            try call.setState(record, version: passed)
        case "missing":
            try call.setMissing()
        default:
            throw ScriptedResolverError(description: "The case's resolver fails for \(handle).")
        }
    }

    // Refs are not part of the scripted answers.
    func resolveRef(_ handle: GesHandle) throws -> GesValue { throw ScriptedResolverError(description: "The case resolves no refs.") }
}
