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
        try Validation.fields(catalog, ["schemas", "hostSchemas", "resolvers", "answers", "refAnswers", "resolveRefs"])
        _ = try parse(catalog.required("schemas"))
        _ = try parse(catalog.required("hostSchemas"))
        let resolvers = try catalog.required("resolvers")
        if resolvers != .null { _ = try Validation.strings(resolvers) }
        for answer in try Validation.array(catalog.required("answers")) {
            try Validation.fields(answer, ["handle", "answer", "text", "version"])
            _ = try GesHandle(parsing: answer.text("handle"))
            try Validation.require(["document", "record", "missing", "fail"].contains(try answer.text("answer")))
        }
        for answer in try Validation.array(catalog.required("refAnswers")) {
            try Validation.fields(answer, ["ref", "answer", "version"])
            try Validation.require(!(try GesHandle(parsing: answer.text("ref"))).ref.isNothing)
            try Validation.require(["version", "missing", "fail"].contains(try answer.text("answer")))
        }
        for reference in try Validation.strings(catalog.required("resolveRefs")) { _ = try GesHandle(parsing: reference) }
    }

    // The handle types the sources may name.
    func compilerTypes() throws -> [GesHandleType] { try Self.parse(catalog.required("schemas")).handleTypes }

    // Registers a fresh scripted resolver for every selected type of the host catalog.
    func register(_ builder: GesHostBuilder) throws {
        let resolvers = try catalog.required("resolvers")
        let selected = resolvers == .null ? nil : Set(try Validation.strings(resolvers))
        var answers: [GesHandle: [Input]] = [:]
        for answer in try Validation.array(catalog.required("answers")) { answers[try GesHandle(parsing: answer.text("handle")), default: []].append(answer) }
        // A ref is part of the text form but not of handle equality, so ref answers are found by text.
        var refAnswers: [String: Input] = [:]
        for answer in try Validation.array(catalog.required("refAnswers")) { refAnswers[String(describing: try GesHandle(parsing: answer.text("ref")))] = answer }
        let resolver = ScriptedResolver(answers, refAnswers)
        for type in try Self.parse(catalog.required("hostSchemas")).handleTypes where selected?.contains(type.name) ?? true { builder.withHandleResolver(resolver, for: type) }
    }

    // Resolves each scripted ref through the started host, in order: the returned handle or Nothing, or the failure.
    func resolveRefs(_ host: GameEventScriptHost) throws -> [[String: Any]] {
        try Validation.strings(catalog.required("resolveRefs")).map { text in
            do {
                let resolved = try host.resolveRef(GesHandle(parsing: text))
                return ["ref": text, "value": try RuntimeValues.encode(resolved.map { .handle($0) } ?? .nothing)]
            } catch let error as GesHandleError {
                return ["ref": text, "error": ["phase": "runtime", "code": error.code, "symbol": error.symbol]]
            }
        }
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
    private let refAnswers: [String: Input]
    private var calls: [GesHandle: Int] = [:]

    init(_ answers: [GesHandle: [Input]], _ refAnswers: [String: Input]) {
        self.answers = answers
        self.refAnswers = refAnswers
    }

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

    // Answers a ref from the case's scripted ref answers; a ref without one fails.
    func resolveRef(_ handle: GesHandle) throws -> GesValue {
        guard let answer = refAnswers[String(describing: handle)], try answer.text("answer") != "fail" else { throw ScriptedResolverError(description: "The case's resolver fails for \(handle).") }
        return try answer.text("answer") == "missing" ? .nothing : RuntimeValues.decode(answer.required("version"))
    }
}
