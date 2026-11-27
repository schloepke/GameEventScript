// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

/// The form of a handle type's id or a composite's part: an integer with a closed range or a Text with a maximum.
public struct GesHandleKey: Sendable, Equatable {
    /// Whether the key is a Text; otherwise it is an integer Number.
    public let isText: Bool
    /// The smallest integer key; 0 for a Text.
    public let minimum: Int64
    /// The largest integer key; 0 for a Text.
    public let maximum: Int64
    /// The maximum of a Text key in UTF-8 bytes; 0 for an integer.
    public let maxLength: Int64
}

/// The declarative description of one `handle` declaration of a schema set: a reference type to objects that live outside
/// the host, the schema of their state and the form of their identity and version.
public final class GesHandleType: @unchecked Sendable {
    let resolved: GesResolvedHandle
    /// The declarations of the schema set, by name, so that a nested record of the state finds its schema.
    let declarations: [String: GesResolvedDeclaration]
    /// The name, without the leading `@`.
    public let name: String
    /// The name of the state schema, without the leading `:`; empty for a store file type.
    public let stateName: String
    /// The form of the id; for a part-less composite its root's, since its identity is the root's id; nil for a composite
    /// with a part, whose identity is its root's id and its part.
    public let id: GesHandleKey?
    /// The form of a composite's part; nil for a part-less composite and any other type.
    public let part: GesHandleKey?
    /// The kind of version; a composite has its root's.
    public let version: GesHandleVersionKind
    /// The root type of a composite, with or without a part; nil for any other type.
    public internal(set) var root: GesHandleType?
    /// The handle type of the store whose files this store file type names, `:Store(@Type)`; nil for any other type
    /// (specs/Schemas.md#handles).
    public internal(set) var storeOf: GesHandleType?
    /// The projections of the state schema in the schema set, the projections a handle of this type can read through, in
    /// ascending name order; empty for a store file type (specs/Schemas.md#projections).
    public internal(set) var projections: [GesSchema] = []
    /// The handle hash: FNV-1a 64 over the canonical form.
    public let hash: UInt64
    /// The canonical form, which the hash runs over: the declaration and, for a composite, its root's.
    public let canonicalForm: String
    /// The link hash a Program records for the type: FNV-1a 64 over the canonical form followed by the line `state` and
    /// the state schema's hash, so a changed state schema fails to link as well (specs/Schemas.md#canonical-form).
    public internal(set) var linkHash: UInt64 = 0

    init(_ handle: GesResolvedHandle, _ declarations: [String: GesResolvedDeclaration]) {
        resolved = handle
        self.declarations = declarations
        name = handle.name
        stateName = handle.state?.name ?? ""
        let key = Self.key(handle.id)
        id = handle.hasPart ? nil : key
        part = handle.hasPart ? key : nil
        version = handle.versionKind
        canonicalForm = GesSchemaCanonical.handleForm(handle)
        hash = GesSchemaCanonical.hash(canonicalForm)
    }

    // The store file type of this type's store with a name, or nil when none of that name names it. A file type holds
    // its store's type, so this side does not hold the file types: once the schema set and the file type are released,
    // an equal description is built again.
    func fileType(_ name: String) -> GesHandleType? {
        for file in resolved.files where file.name == name {
            if let type = file.publicType { return type }
            let type = Self.rebuilt(file, declarations)
            type.storeOf = self
            return type
        }
        return nil
    }

    // The public description of a resolved handle, built again with the same link hash when the schema set released it.
    private static func rebuilt(_ handle: GesResolvedHandle, _ declarations: [String: GesResolvedDeclaration]) -> GesHandleType {
        if let type = handle.publicType { return type }
        let type = GesHandleType(handle, declarations)
        type.root = handle.root.map { rebuilt($0, declarations) }
        type.linkHash = handle.linkHash
        if let state = handle.state { type.projections = projections(of: state, declarations) }
        return type
    }

    // Called once the state schema has its hash, after every schema of the set is complete. A store file type has no
    // state, so its link hash is its handle hash.
    // Each projection of the state adds its name and schema hash, so a Program and a host agree on what it reads.
    func completeLinkHash() {
        if let state = resolved.state {
            projections = Self.projections(of: state, declarations)
            var text = canonicalForm + "state " + GesSchemaCanonical.hex(state.schema!.hash) + "\n"
            for projection in projections { text += "projection " + projection.name + " " + GesSchemaCanonical.hex(projection.hash) + "\n" }
            linkHash = GesSchemaCanonical.hash(text)
        } else {
            linkHash = hash
        }
        resolved.linkHash = linkHash
    }

    // The projections that narrow a state, in ascending name order; type names are ASCII, so bytes order them like C#.
    private static func projections(of state: GesResolvedDeclaration, _ declarations: [String: GesResolvedDeclaration]) -> [GesSchema] {
        declarations.values.filter { $0.narrows(state) }.compactMap(\.schema).sorted { Array($0.name.utf8).lexicographicallyPrecedes(Array($1.name.utf8)) }
    }

    /// The projection of this type's state with a name, or nil when none of that name narrows it.
    @_spi(Compiler) public func findProjection(_ name: String) -> GesSchema? { projections.first { $0.name == name } }

    /// The nodes of the type on its own, with its version when it has one, which schema code generation lays out.
    @_spi(Generator) public func bindNodes() throws -> (nodes: [GesSchemaNode], infos: [GesSchemaNodeInfo]) { try GesSchemaBinder.bindHandle(resolved) }

    private static func key(_ type: GesResolvedType) -> GesHandleKey {
        if type.kind == .text { return GesHandleKey(isText: true, minimum: 0, maximum: 0, maxLength: type.max ?? 0) }
        return GesHandleKey(isText: false, minimum: integer(type.constraints.lower!), maximum: integer(type.constraints.upper!), maxLength: 0)
    }

    private static func integer(_ value: GesResolvedValue) -> Int64 {
        var result = value.value.significand
        for _ in 0..<max(value.value.exponent, 0) { result *= 10 }
        return result
    }
}

/// A resolved `handle` declaration.
final class GesResolvedHandle {
    let name: String
    let offset: Int
    let scope: GesFileScope
    let syntax: GesSchemaDeclarationSyntax
    /// The state schema; nil for a store file type, which has none.
    var state: GesResolvedDeclaration!
    /// The handle type of the store whose files a store file type names; nil for any other type.
    var store: GesResolvedHandle?
    /// The store file types that name this type's store, in declaration order.
    var files: [GesResolvedHandle] = []
    /// The root of a composite, which is not composite itself.
    var root: GesResolvedHandle?
    /// The id, or a composite's part; a part-less composite has its root's id.
    var id: GesResolvedType! {
        get { ownId ?? root!.id }
        set { ownId = newValue }
    }
    private var ownId: GesResolvedType?
    /// The type of a revision, an integer Number with an optional range.
    var revision: GesResolvedType?
    /// The link hash of the public description, kept to build it again.
    var linkHash: UInt64 = 0
    /// The public description, which the schema set holds. It holds this handle, so this side is weak.
    weak var publicType: GesHandleType?

    init(name: String, offset: Int, scope: GesFileScope, syntax: GesSchemaDeclarationSyntax) {
        self.name = name
        self.offset = offset
        self.scope = scope
        self.syntax = syntax
    }

    /// Whether a composite names a part of its root; a part-less composite is one per root and keyed by the root's id.
    var hasPart: Bool { root != nil && syntax.id != nil }

    /// The declared version, or for a composite its root's.
    var versionKind: GesHandleVersionKind { root?.versionKind ?? syntax.version }

    var versioned: Bool { versionKind != .none }

    func writeCanonical(_ output: inout String) {
        output += "handle " + name
        if let root {
            output += " composite " + root.name + "\n"
            if !hasPart { return }
            if let store { output += "store " + store.name + "\n" }
            output += "part "
            GesSchemaCanonical.writeHandleType(id, &output)
            output += "\n"
            return
        }
        output += "\nid "
        GesSchemaCanonical.writeHandleType(id, &output)
        output += "\nversion "
        switch syntax.version {
        case .none: output += "Nothing"
        case .contentHash: output += "ContentHash"
        case .sha1: output += "Hash(#sha1)"
        case .sha256: output += "Hash(#sha256)"
        case .revision: GesSchemaCanonical.writeHandleType(revision!, &output)
        }
        output += "\n"
    }
}
