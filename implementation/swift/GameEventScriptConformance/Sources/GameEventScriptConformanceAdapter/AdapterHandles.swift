// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptCompiler
import GameEventScriptRuntime

// The per-case handle setup: the handle types the compiler and the host know, and the scripted resolver answers
// (specs/Conformance/MarkdownFormat.md#handle-setup).
struct AdapterHandles {
    private let store: Input

    private init(_ store: Input) { self.store = store }

    static func from(_ test: Input) -> AdapterHandles? { test["handles"].map(AdapterHandles.init) }

    static func validate(_ store: Input) throws {
        try Validation.fields(store, ["schemas", "hostSchemas", "resolvers", "answers", "refAnswers", "resolveRefs", "stores", "lists", "enumerable"])
        _ = try parse(store.required("schemas"))
        _ = try parse(store.required("hostSchemas"))
        let resolvers = try store.required("resolvers")
        if resolvers != .null { _ = try Validation.strings(resolvers) }
        for answer in try Validation.array(store.required("answers")) {
            try Validation.fields(answer, ["handle", "answer", "text", "version"])
            _ = try GesHandle(parsing: answer.text("handle"))
            try Validation.require(["document", "record", "missing", "notVisible", "fail"].contains(try answer.text("answer")))
        }
        for answer in try Validation.array(store.required("refAnswers")) {
            try Validation.fields(answer, ["ref", "answer", "version"])
            try Validation.require(!(try GesHandle(parsing: answer.text("ref"))).ref.isNothing)
            try Validation.require(["version", "missing", "fail"].contains(try answer.text("answer")))
        }
        for reference in try Validation.strings(store.required("resolveRefs")) { _ = try GesHandle(parsing: reference) }
        for listing in try Validation.array(store.required("lists")) {
            try Validation.fields(listing, ["type", "root", "handles"])
            if try listing.required("root") != .null { _ = try GesHandle(parsing: listing.text("root")) }
            for handle in try Validation.strings(listing.required("handles")) { _ = try GesHandle(parsing: handle) }
        }
        let enumerable = try store.required("enumerable")
        if enumerable != .null { _ = try Validation.strings(enumerable) }
        let stores = try store.required("stores")
        try Validation.require(stores == .null || stores.objectValue != nil)
    }

    // Gives the compiler the handle types the sources may name and, when the case says so, the types the host enumerates.
    func configure(_ builder: GesProgramBuilder) throws {
        builder.withHandleTypes(try Self.parse(store.required("schemas")).handleTypes)
        let enumerable = try store.required("enumerable")
        if enumerable != .null { builder.withEnumerableHandleTypes(try Validation.strings(enumerable)) }
    }

    // Registers a fresh scripted resolver for every selected type of the host store.
    func register(_ builder: GesHostBuilder) throws {
        let resolvers = try store.required("resolvers")
        let selected = resolvers == .null ? nil : Set(try Validation.strings(resolvers))
        var answers: [GesHandle: [Input]] = [:]
        for answer in try Validation.array(store.required("answers")) { answers[try GesHandle(parsing: answer.text("handle")), default: []].append(answer) }
        // A ref is part of the text form but not of handle equality, so ref answers are found by text.
        var refAnswers: [String: Input] = [:]
        for answer in try Validation.array(store.required("refAnswers")) { refAnswers[String(describing: try GesHandle(parsing: answer.text("ref")))] = answer }
        var lists: [StoreListKey: [GesHandle]] = [:]
        for listing in try Validation.array(store.required("lists")) {
            let root = try listing.required("root") == .null ? nil : try GesHandle(parsing: listing.text("root"))
            lists[StoreListKey(type: try listing.text("type"), root: root)] = try Validation.strings(listing.required("handles")).map { try GesHandle(parsing: $0) }
        }
        // A store read from the case's files answers for its own type in place of the scripted resolver.
        let set = try Self.parse(store.required("hostSchemas"))
        let storesInput = try store.required("stores")
        var stores: [String: GesStore] = [:]
        if storesInput != .null { for read in try SchemaApiObservations.readStores(set, storesInput) { stores[read.type.name] = read } }
        let shared = ScriptedAnswers(answers, refAnswers, lists)
        for type in set.handleTypes {
            if let read = stores[type.name] {
                builder.withHandleResolver(read, for: type)
            } else if selected?.contains(type.name) ?? true {
                builder.withHandleResolver(ScriptedResolver(type.name, shared), for: type)
            }
        }
    }

    // Resolves each scripted ref through the started host, in order: the returned handle or Nothing, or the failure.
    func resolveRefs(_ host: GameEventScriptHost) throws -> [[String: Any]] {
        try Validation.strings(store.required("resolveRefs")).map { text in
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

private struct StoreListKey: Hashable {
    let type: String
    let root: GesHandle?
}

// The case's answers, listings and read counts, which the resolvers of all types share.
private final class ScriptedAnswers {
    let answers: [GesHandle: [Input]]
    let refAnswers: [String: Input]
    let lists: [StoreListKey: [GesHandle]]
    var calls: [GesHandle: Int] = [:]

    init(_ answers: [GesHandle: [Input]], _ refAnswers: [String: Input], _ lists: [StoreListKey: [GesHandle]]) {
        self.answers = answers
        self.refAnswers = refAnswers
        self.lists = lists
    }
}

// Answers each object's reads in the authored order; the last answer repeats, and an object without one fails. Each
// resolver lists its own type's scripted handles.
private final class ScriptedResolver: GesHandleResolver {
    private let type: String
    private let shared: ScriptedAnswers

    init(_ type: String, _ shared: ScriptedAnswers) {
        self.type = type
        self.shared = shared
    }

    // Lists the case's scripted handles of this type, or of one root's parts; without a listing the type does not enumerate.
    func handles(root: GesHandle?) throws -> [GesHandle]? { shared.lists[StoreListKey(type: type, root: root)] }

    func resolve(_ call: GesHandleResolveCall) throws {
        let handle = call.handle
        guard let list = shared.answers[handle] else { throw ScriptedResolverError(description: "The case has no answer for \(handle).") }
        let count = shared.calls[handle] ?? 0
        shared.calls[handle] = count + 1
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
        case "notVisible":
            try call.setNotVisible()
        default:
            throw ScriptedResolverError(description: "The case's resolver fails for \(handle).")
        }
    }

    // Answers a ref from the case's scripted ref answers; a ref without one fails.
    func resolveRef(_ handle: GesHandle) throws -> GesValue {
        guard let answer = shared.refAnswers[String(describing: handle)], try answer.text("answer") != "fail" else { throw ScriptedResolverError(description: "The case's resolver fails for \(handle).") }
        return try answer.text("answer") == "missing" ? .nothing : RuntimeValues.decode(answer.required("version"))
    }
}
