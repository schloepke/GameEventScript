// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

/// An immutable reference to an object outside the host: a handle type name, an id and, for a type with a root and a
/// part, a part, with an optional version or unresolved ref and the projection it reads through.
///
/// Equality, hashing, order and the text form use the type name, the id and the part; they ignore the version, the ref
/// and the projection. The id and part are an integer Number or a Text; the version is an integer Number for a
/// revision or 20 or 32 Bytes for a hash. A handle of a type with a root carries the root's id and its part, if the type
/// has one; an attached handle carries its root's version and ref, a keyed handle its own version.
public final class GesHandle: Hashable, Comparable, @unchecked Sendable, CustomStringConvertible {
    // Every stored value is an immutable integer, Text, Tag or Bytes value, so instances can cross isolation domains.

    /// The name of the handle type, without its `@`.
    public let typeName: String
    /// The id: an integer Number or a Text; that of a handle of a type with a root is its root's.
    public let id: GesValue
    /// The version: an integer Number, 20 or 32 Bytes, or Nothing; an attached handle's is its root's, a keyed handle's
    /// its own.
    public let version: GesValue
    /// An unresolved ref that names a version: a Tag or a Text, or Nothing.
    public let ref: GesValue
    /// The part of a handle of a type with a root and a part: an integer Number or a Text; Nothing for any other handle.
    public let part: GesValue
    /// Whether the handle is a keyed handle with a part, whose text form writes its own version after the part; equality
    /// and order ignore it.
    public let isKeyed: Bool
    /// The name of the [projection](https://gameeventscript.org/specs/Schemas.html#projections) the handle reads its
    /// state through, without the leading `:`; empty for the full view.
    public let projection: String

    private init(typeName: String, id: GesValue, version: GesValue, ref: GesValue, part: GesValue, isKeyed: Bool, projection: String = "") {
        self.projection = projection
        self.typeName = typeName
        self.id = id
        self.version = version
        self.ref = ref
        self.part = part
        self.isKeyed = isKeyed && !part.isNothing
    }

    /// Whether the handle carries a version.
    public var hasVersion: Bool { !version.isNothing }

    /// Whether the handle carries a part.
    public var hasPart: Bool { !part.isNothing }

    /// Creates a plain handle with an optional version.
    ///
    /// - Throws: `GesApiError.invalidArgument` for a type name that is not a type name, an id that is neither an
    ///   integer Number without unit nor a Text, or a version that is neither Nothing, an integer Number without unit
    ///   nor 20 or 32 Bytes.
    public convenience init(typeName: String, id: GesValue, version: GesValue = .nothing) throws(GesApiError) {
        self.init(typeName: try Self.checkTypeName(typeName), id: try Self.checkKey(id), version: try Self.checkVersion(version), ref: .nothing, part: .nothing, isKeyed: false)
    }

    /// Creates an attached handle (specs/Schemas.md#handles): an object that belongs to the object of a root handle, with
    /// the root's id, version and ref and optionally a part.
    ///
    /// - Parameters:
    ///   - typeName: The attached handle type name, without the leading `@`.
    ///   - root: The root handle, which carries no part.
    ///   - part: The part, an integer Number or a Text; Nothing for a type without a part.
    /// - Throws: `GesApiError.invalidArgument` for an invalid type name, a root that carries a part or a part that is
    ///   neither Nothing, an integer Number without unit nor a Text.
    public convenience init(typeName: String, attachedTo root: GesHandle, part: GesValue = .nothing) throws(GesApiError) {
        guard !root.hasPart else { throw .invalidArgument("A root carries no part.") }
        let name = try Self.checkTypeName(typeName)
        let checkedPart = part.isNothing ? GesValue.nothing : try Self.checkKey(part)
        self.init(typeName: name, id: root.id, version: root.version, ref: root.ref, part: checkedPart, isKeyed: false)
    }

    /// Creates a keyed handle (specs/Schemas.md#handles): an object of its own, identified by a root handle's id and
    /// optionally a part, with its own version; the root's version and ref are not carried.
    ///
    /// - Parameters:
    ///   - typeName: The keyed handle type name, without the leading `@`.
    ///   - root: The root handle, which carries no part.
    ///   - part: The part, an integer Number or a Text; Nothing for a type without a part.
    ///   - version: An integer Number for a revision or 20 or 32 Bytes for a hash; Nothing for none.
    /// - Throws: `GesApiError.invalidArgument` for an invalid type name, part or version or a root that carries a part.
    public convenience init(typeName: String, keyedBy root: GesHandle, part: GesValue = .nothing, version: GesValue = .nothing) throws(GesApiError) {
        guard !root.hasPart else { throw .invalidArgument("A root carries no part.") }
        let name = try Self.checkTypeName(typeName)
        let checkedPart = part.isNothing ? GesValue.nothing : try Self.checkKey(part)
        self.init(typeName: name, id: root.id, version: try Self.checkVersion(version), ref: .nothing, part: checkedPart, isKeyed: true)
    }

    /// Reads the text form `@Type:id[~version|~#tag|~'ref'][/part]`, refs included, such as `@Drone:7~120`, the attached
    /// `@UnitSlot:drone.gnabblo~3f2a…/1` or the keyed `@Stats:3/7~12`, whose version after the part marks it as keyed.
    ///
    /// - Throws: `GesApiError.invalidArgument` for text that is not a handle.
    public convenience init(parsing text: String) throws(GesApiError) {
        guard let handle = Self.read(text) else { throw .invalidArgument("'\(text)' is no handle.") }
        self.init(typeName: handle.typeName, id: handle.id, version: handle.version, ref: handle.ref, part: handle.part, isKeyed: handle.isKeyed)
    }

    /// Returns the handle with another version and without a ref; an attached handle takes it as its root's.
    ///
    /// - Throws: `GesApiError.invalidArgument` for a version that is neither Nothing, an integer Number without unit
    ///   nor 20 or 32 Bytes.
    public func withVersion(_ version: GesValue) throws(GesApiError) -> GesHandle {
        GesHandle(typeName: typeName, id: id, version: try Self.checkVersion(version), ref: .nothing, part: part, isKeyed: isKeyed, projection: projection)
    }

    /// Returns the handle reading its state through a projection, or through the full view for an empty name.
    ///
    /// - Parameter projection: The projection's schema name without the leading `:`, or empty for the full view.
    /// - Returns: The handle with the projection; its identity, version and ref are unchanged.
    /// - Throws: `GesApiError.invalidArgument` for a name that is no type name.
    public func withProjection(_ projection: String) throws(GesApiError) -> GesHandle {
        if !projection.isEmpty && !GesNames.type(projection) { throw .invalidArgument("'\(projection)' is no projection name.") }
        return projection.utf8.elementsEqual(self.projection.utf8) ? self : GesHandle(typeName: typeName, id: id, version: version, ref: ref, part: part, isKeyed: isKeyed, projection: projection)
    }

    // A handle from wire parts that its schema already checked.
    static func fromWire(typeName: String, id: GesValue, version: GesValue, part: GesValue, isKeyed: Bool) -> GesHandle {
        GesHandle(typeName: typeName, id: id, version: version, ref: .nothing, part: part, isKeyed: isKeyed)
    }

    func withoutVersion() -> GesHandle {
        version.isNothing && ref.isNothing ? self : GesHandle(typeName: typeName, id: id, version: .nothing, ref: .nothing, part: part, isKeyed: isKeyed, projection: projection)
    }

    // A checked version and a projection name at once, as narrowing needs them.
    func withView(_ version: GesValue, _ projection: String) -> GesHandle {
        GesHandle(typeName: typeName, id: id, version: version, ref: .nothing, part: part, isKeyed: isKeyed, projection: projection)
    }

    static func withRef(_ handle: GesHandle, _ reference: GesValue) -> GesHandle {
        GesHandle(typeName: handle.typeName, id: handle.id, version: .nothing, ref: reference, part: handle.part, isKeyed: handle.isKeyed, projection: handle.projection)
    }

    /// The canonical text form `@Type:id[~version|~#tag|~'ref'][/part]`; a keyed handle with a part writes
    /// `@Type:id/part[~version]`.
    public var description: String {
        var text = "@" + typeName + ":"
        Self.writeKey(id, isPart: false, to: &text)
        if isKeyed {
            text += "/"
            Self.writeKey(part, isPart: true, to: &text)
        }
        if !version.isNothing {
            text += "~"
            if version.valueKind == .integer { text += String(version.asInteger) } else { GesHashBytes.appendHex(version.bytesValue!, to: &text) }
        } else if !ref.isNothing {
            text += "~"
            if ref.valueKind == .tag { text += "#" + ref.textValue! } else { Self.appendQuoted(ref.textValue!, to: &text) }
        }
        if hasPart && !isKeyed {
            text += "/"
            Self.writeKey(part, isPart: true, to: &text)
        }
        return text
    }

    /// Handles are equal with the same type name, id and part; version, ref and projection are ignored.
    public static func == (left: GesHandle, right: GesHandle) -> Bool {
        left === right || GesText.scalarEqual(left.typeName, right.typeName) && keyEquals(left.id, right.id) && keyEquals(left.part, right.part)
    }

    /// Hashes the type name, id and part consistently with `==`.
    public func hash(into hasher: inout Hasher) {
        GesText.hashScalars(typeName, into: &hasher)
        Self.hashKey(id, into: &hasher)
        Self.hashKey(part, into: &hasher)
    }

    /// Orders by type name in scalar order, then id (integers before Texts), then part, none first.
    public static func < (left: GesHandle, right: GesHandle) -> Bool { compare(left, right) < 0 }

    static func compare(_ left: GesHandle, _ right: GesHandle) -> Int {
        if left === right { return 0 }
        if !GesText.scalarEqual(left.typeName, right.typeName) { return GesText.scalarLess(left.typeName, right.typeName) ? -1 : 1 }
        var comparison = compareKey(left.id, right.id)
        if comparison != 0 { return comparison }
        comparison = compareKey(left.part, right.part)
        return comparison
    }

    private static func checkTypeName(_ typeName: String) throws(GesApiError) -> String {
        guard GesText.isUpperName(typeName) else { throw .invalidArgument("'\(typeName)' is no handle type name.") }
        return typeName
    }

    private static func checkKey(_ key: GesValue) throws(GesApiError) -> GesValue {
        guard key.valueKind == .integer && key.unit == .none || key.valueKind == .text else { throw .invalidArgument("A handle id or part is an integer Number or a Text.") }
        return key
    }

    private static func checkVersion(_ version: GesValue) throws(GesApiError) -> GesValue {
        if version.isNothing { return .nothing }
        guard version.valueKind == .integer && version.unit == .none || version.valueKind == .bytes && (version.bytesValue!.count == 20 || version.bytesValue!.count == 32) else {
            throw .invalidArgument("A handle version is an integer Number or 20 or 32 Bytes.")
        }
        return version
    }

    private static func keyEquals(_ left: GesValue, _ right: GesValue) -> Bool {
        guard left.valueKind == right.valueKind else { return false }
        switch left.valueKind {
        case .integer: return left.asInteger == right.asInteger
        case .text: return GesText.scalarEqual(left.textValue!, right.textValue!)
        default: return true
        }
    }

    private static func hashKey(_ key: GesValue, into hasher: inout Hasher) {
        switch key.valueKind {
        case .integer: hasher.combine(key.asInteger)
        case .text: GesText.hashScalars(key.textValue!, into: &hasher)
        default: break
        }
    }

    private static func compareKey(_ left: GesValue, _ right: GesValue) -> Int {
        func rank(_ key: GesValue) -> Int { key.isNothing ? 0 : key.valueKind == .integer ? 1 : 2 }

        if left.valueKind != right.valueKind { return rank(left) < rank(right) ? -1 : rank(left) > rank(right) ? 1 : 0 }
        switch left.valueKind {
        case .integer:
            let a = left.asInteger
            let b = right.asInteger
            return a < b ? -1 : a > b ? 1 : 0
        case .text:
            let a = left.textValue!
            let b = right.textValue!
            return GesText.scalarEqual(a, b) ? 0 : GesText.scalarLess(a, b) ? -1 : 1
        default: return 0
        }
    }

    // Reads the text form through the scanner documents share; nil for anything else.
    static func read(_ text: String) -> GesHandle? {
        let bytes = Array(text.utf8)
        guard bytes.first == 64, let scanned = GesHandleLiteral.scan(bytes, 0), scanned.end == bytes.count else { return nil }
        let parts = scanned.parts
        var version = GesValue.nothing
        var reference = GesValue.nothing
        switch parts.version {
        case .none: break
        case .revision(let revision): version = .integer(revision)
        case .hash(let hex):
            guard let bytes = GesHashBytes.parseHex(hex, count: hex.utf8.count / 2) else { return nil }
            version = .bytes(bytes)
        case .tag(let tag):
            guard let value = try? GesValue.tag(tag) else { return nil }
            reference = value
        case .text(let value): reference = .text(value)
        }
        return GesHandle(
            typeName: parts.typeName,
            id: keyValue(parts.id),
            version: version,
            ref: reference,
            part: parts.part.map(keyValue) ?? .nothing,
            isKeyed: parts.versionAfterPart
        )
    }

    private static func keyValue(_ key: GesHandleLiteral.Key) -> GesValue {
        switch key {
        case .integer(let integer): .integer(integer)
        case .text(let text): .text(text)
        }
    }

    private static func writeKey(_ key: GesValue, isPart: Bool, to text: inout String) {
        if key.valueKind == .integer {
            text += String(key.asInteger)
            return
        }
        let value = key.textValue ?? ""
        if GesHandleLiteral.isBare(value, isPart: isPart) { text += value } else { appendQuoted(value, to: &text) }
    }

    private static func appendQuoted(_ value: String, to text: inout String) {
        text += "'"
        for scalar in value.unicodeScalars {
            if scalar == "'" { text += "''" } else { text.unicodeScalars.append(scalar) }
        }
        text += "'"
    }
}
