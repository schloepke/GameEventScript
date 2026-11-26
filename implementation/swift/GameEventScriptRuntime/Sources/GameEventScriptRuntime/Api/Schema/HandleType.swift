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
    /// The name of the state schema, without the leading `:`.
    public let stateName: String
    /// The form of the id; nil for a composite, whose identity is its root's id and its part.
    public let id: GesHandleKey?
    /// The form of a composite's part; nil for any other type.
    public let part: GesHandleKey?
    /// The kind of version; a composite has its root's.
    public let version: GesHandleVersionKind
    /// The root type of a composite; nil for any other type.
    public internal(set) var root: GesHandleType?
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
        stateName = handle.state.name
        let key = Self.key(handle.id)
        id = handle.root == nil ? key : nil
        part = handle.root == nil ? nil : key
        version = handle.versionKind
        canonicalForm = GesSchemaCanonical.handleForm(handle)
        hash = GesSchemaCanonical.hash(canonicalForm)
    }

    // Called once the state schema has its hash, after every schema of the set is complete.
    func completeLinkHash() { linkHash = GesSchemaCanonical.hash(canonicalForm + "state " + GesSchemaCanonical.hex(resolved.state.schema!.hash) + "\n") }

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
    var state: GesResolvedDeclaration!
    /// The root of a composite, which is not composite itself.
    var root: GesResolvedHandle?
    /// The id, or a composite's part.
    var id: GesResolvedType!
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

    /// The declared version, or for a composite its root's.
    var versionKind: GesHandleVersionKind { root?.versionKind ?? syntax.version }

    var versioned: Bool { versionKind != .none }

    func writeCanonical(_ output: inout String) {
        output += "handle " + name
        if let root {
            output += " composite " + root.name + "\npart "
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
