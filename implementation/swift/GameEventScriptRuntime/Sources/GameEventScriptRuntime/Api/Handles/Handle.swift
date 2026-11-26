// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

/// An immutable reference to an object outside the host: a handle type name, an id and, for a composite, a part,
/// with an optional version or unresolved ref.
///
/// Equality, hashing and order use the type name, the id, whether the handle is composite and the part; they ignore
/// the version and the ref. The id and part are an integer Number or a Text; the version is an integer Number for a
/// revision or 20 or 32 Bytes for a hash. A composite's id, version and ref are its root's.
public final class GesHandle: Hashable, Comparable, @unchecked Sendable, CustomStringConvertible {
    // Every stored value is an immutable integer, Text, Tag or Bytes value, so instances can cross isolation domains.

    /// The name of the handle type, without its `@`.
    public let typeName: String
    /// The id: an integer Number or a Text.
    public let id: GesValue
    /// The version: an integer Number, 20 or 32 Bytes, or Nothing.
    public let version: GesValue
    /// An unresolved ref that names a version: a Tag or a Text, or Nothing.
    public let ref: GesValue
    /// A composite's part: an integer Number or a Text; Nothing for a plain handle.
    public let part: GesValue
    /// Whether the handle names a part of its root's object.
    public let isComposite: Bool

    private init(typeName: String, id: GesValue, version: GesValue, ref: GesValue, part: GesValue, isComposite: Bool) {
        self.typeName = typeName
        self.id = id
        self.version = version
        self.ref = ref
        self.part = part
        self.isComposite = isComposite
    }

    /// Whether the handle carries a version.
    public var hasVersion: Bool { !version.isNothing }

    /// Creates a plain handle with an optional version.
    ///
    /// - Throws: `GesApiError.invalidArgument` for a type name that is not a type name, an id that is neither an
    ///   integer Number without unit nor a Text, or a version that is neither Nothing, an integer Number without unit
    ///   nor 20 or 32 Bytes.
    public convenience init(typeName: String, id: GesValue, version: GesValue = .nothing) throws(GesApiError) {
        self.init(typeName: try Self.checkTypeName(typeName), id: try Self.checkKey(id), version: try Self.checkVersion(version), ref: .nothing, part: .nothing, isComposite: false)
    }

    /// Creates a composite handle that takes its id, version and ref from a plain root handle.
    ///
    /// - Throws: `GesApiError.invalidArgument` for an invalid type name, a composite root or a part that is neither an
    ///   integer Number without unit nor a Text.
    public convenience init(typeName: String, root: GesHandle, part: GesValue) throws(GesApiError) {
        guard !root.isComposite else { throw .invalidArgument("A composite's root is no composite.") }
        self.init(typeName: try Self.checkTypeName(typeName), id: root.id, version: root.version, ref: root.ref, part: try Self.checkKey(part), isComposite: true)
    }

    /// Reads the text form `@Type:id[~version|~#tag|~'ref'][/part]`, refs included.
    ///
    /// - Throws: `GesApiError.invalidArgument` for text that is not a handle.
    public convenience init(parsing text: String) throws(GesApiError) {
        guard let handle = Self.read(text) else { throw .invalidArgument("'\(text)' is no handle.") }
        self.init(typeName: handle.typeName, id: handle.id, version: handle.version, ref: handle.ref, part: handle.part, isComposite: handle.isComposite)
    }

    /// Returns the handle with another version and without a ref.
    ///
    /// - Throws: `GesApiError.invalidArgument` for a version that is neither Nothing, an integer Number without unit
    ///   nor 20 or 32 Bytes.
    public func withVersion(_ version: GesValue) throws(GesApiError) -> GesHandle {
        GesHandle(typeName: typeName, id: id, version: try Self.checkVersion(version), ref: .nothing, part: part, isComposite: isComposite)
    }

    // A handle from wire parts that its schema already checked.
    static func fromWire(typeName: String, id: GesValue, version: GesValue, part: GesValue, isComposite: Bool) -> GesHandle {
        GesHandle(typeName: typeName, id: id, version: version, ref: .nothing, part: part, isComposite: isComposite)
    }

    func withoutVersion() -> GesHandle {
        version.isNothing && ref.isNothing ? self : GesHandle(typeName: typeName, id: id, version: .nothing, ref: .nothing, part: part, isComposite: isComposite)
    }

    static func withRef(_ handle: GesHandle, _ reference: GesValue) -> GesHandle {
        GesHandle(typeName: handle.typeName, id: handle.id, version: .nothing, ref: reference, part: handle.part, isComposite: handle.isComposite)
    }

    /// The canonical text form `@Type:id[~version|~#tag|~'ref'][/part]`.
    public var description: String {
        var text = "@" + typeName + ":"
        Self.writeKey(id, isPart: false, to: &text)
        if !version.isNothing {
            text += "~"
            if version.valueKind == .integer { text += String(version.asInteger) } else { GesHashBytes.appendHex(version.bytesValue!, to: &text) }
        } else if !ref.isNothing {
            text += "~"
            if ref.valueKind == .tag { text += "#" + ref.textValue! } else { Self.appendQuoted(ref.textValue!, to: &text) }
        }
        if isComposite {
            text += "/"
            Self.writeKey(part, isPart: true, to: &text)
        }
        return text
    }

    /// Handles are equal with the same type name, id, composite flag and part; version and ref are ignored.
    public static func == (left: GesHandle, right: GesHandle) -> Bool {
        left === right
            || GesText.scalarEqual(left.typeName, right.typeName) && left.isComposite == right.isComposite && keyEquals(left.id, right.id) && keyEquals(left.part, right.part)
    }

    /// Hashes the type name, id and part consistently with `==`.
    public func hash(into hasher: inout Hasher) {
        GesText.hashScalars(typeName, into: &hasher)
        Self.hashKey(id, into: &hasher)
        Self.hashKey(part, into: &hasher)
    }

    /// Orders by type name in scalar order, then id (integers before Texts), then plain before composite, then part.
    public static func < (left: GesHandle, right: GesHandle) -> Bool { compare(left, right) < 0 }

    static func compare(_ left: GesHandle, _ right: GesHandle) -> Int {
        if left === right { return 0 }
        if !GesText.scalarEqual(left.typeName, right.typeName) { return GesText.scalarLess(left.typeName, right.typeName) ? -1 : 1 }
        var comparison = compareKey(left.id, right.id)
        if comparison != 0 { return comparison }
        if left.isComposite != right.isComposite { return left.isComposite ? 1 : -1 }
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
            isComposite: parts.part != nil
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
