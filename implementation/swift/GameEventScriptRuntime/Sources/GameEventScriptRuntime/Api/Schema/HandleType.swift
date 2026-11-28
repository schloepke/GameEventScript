// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

/// The form of a handle type's id or part: an integer with a closed range or a Text with a maximum.
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
    /// The name of the state schema, without the leading `:`; empty for a type without state.
    public let stateName: String
    /// Whether the type has a state schema; a type without one only names objects and their versions.
    public var hasState: Bool { !stateName.isEmpty }
    /// The form of the id; for a type with a root and without a part its root's, since its identity is the root's id; nil
    /// for a type with a part, whose identity is its root's id and its part.
    public let id: GesHandleKey?
    /// The form of the part of a type attached to or keyed by a root; nil when it has none.
    public let part: GesHandleKey?
    /// The kind of version: a type attached to a root has its root's, a keyed type its own.
    public let version: GesHandleVersionKind
    /// The root a type is attached to or keyed by; nil for a type without a root.
    public internal(set) var root: GesHandleType?
    /// Whether the type is keyed by its root (`keyed by @Root`): its objects have their own version, lifetime and source;
    /// otherwise a type with a root is attached to it (specs/Schemas.md#handles).
    public let isKeyed: Bool
    /// The projections of the state schema in the schema set, the projections a handle of this type can read through, in
    /// ascending name order; empty for a type without state (specs/Schemas.md#projections).
    public internal(set) var projections: [GesSchema] = []
    /// The handle hash: FNV-1a 64 over the canonical form.
    public let hash: UInt64
    /// The canonical form, which the hash runs over: the declaration and, for a type with a root, its root's.
    public let canonicalForm: String
    /// The link hash a Program records for the type: FNV-1a 64 over the canonical form followed by the line `state` and
    /// the state schema's hash, so a changed state schema fails to link as well (specs/Schemas.md#canonical-form).
    public internal(set) var linkHash: UInt64 = 0

    init(_ handle: GesResolvedHandle, _ declarations: [String: GesResolvedDeclaration]) {
        resolved = handle
        self.declarations = declarations
        name = handle.name
        stateName = handle.state?.name ?? ""
        isKeyed = handle.isKeyed
        let key = Self.key(handle.id)
        id = handle.hasPart ? nil : key
        part = handle.hasPart ? key : nil
        version = handle.versionKind
        canonicalForm = GesSchemaCanonical.handleForm(handle)
        hash = GesSchemaCanonical.hash(canonicalForm)
    }

    // Called once the state schema has its hash, after every schema of the set is complete. A type without state has its
    // handle hash as its link hash.
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
    /// The state schema; nil for a type without state.
    var state: GesResolvedDeclaration?
    /// The root a type is attached to or keyed by, which has no root itself.
    var root: GesResolvedHandle?
    /// The id, or the part of a type with a root; a type with a root and without a part has its root's id.
    var id: GesResolvedType! {
        get { ownId ?? root!.id }
        set { ownId = newValue }
    }
    private var ownId: GesResolvedType?
    /// The type of a revision, an integer Number with an optional range.
    var revision: GesResolvedType?
    /// The public description, which the schema set holds. It holds this handle, so this side is weak.
    weak var publicType: GesHandleType?

    init(name: String, offset: Int, scope: GesFileScope, syntax: GesSchemaDeclarationSyntax) {
        self.name = name
        self.offset = offset
        self.scope = scope
        self.syntax = syntax
    }

    /// Whether a type with a root names a part of it; one without a part is one per root and keyed by the root's id.
    var hasPart: Bool { root != nil && syntax.id != nil }

    var isKeyed: Bool { syntax.form == .keyed }

    var attached: Bool { syntax.form == .attached }

    /// The declared version, or for a type attached to a root its root's.
    var versionKind: GesHandleVersionKind { syntax.form == .attached ? root!.versionKind : syntax.version }

    /// The type whose version a handle carries: an attached type's root, otherwise the type itself.
    var versionOwner: GesResolvedHandle { syntax.form == .attached ? root! : self }

    var versioned: Bool { versionKind != .none }

    /// `handle X` with its form and root, the id or part, the version of a type that is not attached and `stateless` for a
    /// type without state; the state's own hash joins only the link hash.
    func writeCanonical(_ output: inout String) {
        output += "handle " + name
        switch syntax.form {
        case .attached: output += " attached " + root!.name
        case .keyed: output += " keyed " + root!.name
        case .plain: break
        }
        output += "\n"
        if state == nil { output += "stateless\n" }
        if root == nil || hasPart {
            output += root == nil ? "id " : "part "
            GesSchemaCanonical.writeHandleType(id, &output)
            output += "\n"
        }
        if syntax.form == .attached { return }
        output += "version "
        switch syntax.version {
        case .none: output += "none"
        case .gesHash: output += "hash(#ges)"
        case .sha1: output += "hash(#sha1)"
        case .sha256: output += "hash(#sha256)"
        case .revision:
            output += "revision "
            GesSchemaCanonical.writeHandleType(revision!, &output)
        }
        output += "\n"
    }
}
