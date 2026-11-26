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

    // Reads the text form; nil for anything else.
    static func read(_ text: String) -> GesHandle? {
        let scalars = Array(text.unicodeScalars)
        guard scalars.count >= 4, scalars[0] == "@" else { return nil }
        var position = 1
        while position < scalars.count && (isAsciiLetter(scalars[position]) || isDigit(scalars[position]) || scalars[position] == "_") { position += 1 }
        let typeName = String(String.UnicodeScalarView(scalars[1..<position]))
        guard GesText.isUpperName(typeName), position < scalars.count, scalars[position] == ":" else { return nil }
        position += 1
        guard let id = readKey(scalars, &position, isPart: false) else { return nil }
        var version = GesValue.nothing
        var reference = GesValue.nothing
        if position < scalars.count && scalars[position] == "~" {
            position += 1
            if position < scalars.count && scalars[position] == "#" {
                position += 1
                let start = position
                while position < scalars.count && scalars[position] != "/" { position += 1 }
                guard let tag = try? GesValue.tag(String(String.UnicodeScalarView(scalars[start..<position]))) else { return nil }
                reference = tag
            } else if position < scalars.count && scalars[position] == "'" {
                guard let quoted = readQuoted(scalars, &position) else { return nil }
                reference = .text(quoted)
            } else {
                let start = position
                while position < scalars.count && scalars[position] != "/" { position += 1 }
                guard let read = readVersion(Array(scalars[start..<position])) else { return nil }
                version = read
            }
        }
        var isComposite = false
        var part = GesValue.nothing
        if position < scalars.count {
            guard scalars[position] == "/" else { return nil }
            position += 1
            guard let read = readKey(scalars, &position, isPart: true) else { return nil }
            part = read
            isComposite = true
        }
        guard position == scalars.count else { return nil }
        return GesHandle(typeName: typeName, id: id, version: version, ref: reference, part: part, isComposite: isComposite)
    }

    // A key: quoted, or a bare run of ASCII letters, digits, '.', '_' and '-' (and '/' for a part) that is an integer
    // when it reads as one.
    private static func readKey(_ scalars: [Unicode.Scalar], _ position: inout Int, isPart: Bool) -> GesValue? {
        guard position < scalars.count else { return nil }
        if scalars[position] == "'" {
            guard let quoted = readQuoted(scalars, &position) else { return nil }
            return .text(quoted)
        }
        let start = position
        while position < scalars.count && isBare(scalars[position], isPart: isPart) { position += 1 }
        guard position > start else { return nil }
        let bare = Array(scalars[start..<position])
        if readsAsInteger(bare) {
            guard let integer = Int64(String(String.UnicodeScalarView(bare))) else { return nil }
            return .integer(integer)
        }
        return .text(String(String.UnicodeScalarView(bare)))
    }

    private static func readQuoted(_ scalars: [Unicode.Scalar], _ position: inout Int) -> String? {
        var value = String.UnicodeScalarView()
        position += 1
        while position < scalars.count {
            let scalar = scalars[position]
            position += 1
            if scalar != "'" {
                value.append(scalar)
                continue
            }
            if position < scalars.count && scalars[position] == "'" {
                value.append("'")
                position += 1
                continue
            }
            return String(value)
        }
        return nil
    }

    private static func readVersion(_ scalars: [Unicode.Scalar]) -> GesValue? {
        if scalars.count == 40 || scalars.count == 64, scalars.allSatisfy({ isDigit($0) || ("a"..."f").contains($0) }) {
            guard let bytes = GesHashBytes.parseHex(String(String.UnicodeScalarView(scalars)), count: scalars.count / 2) else { return nil }
            return .bytes(bytes)
        }
        guard readsAsInteger(scalars), let revision = Int64(String(String.UnicodeScalarView(scalars))) else { return nil }
        return .integer(revision)
    }

    // An integer is a decimal with an optional '-' and no leading zero.
    private static func readsAsInteger(_ scalars: [Unicode.Scalar]) -> Bool {
        let start = scalars.first == "-" ? 1 : 0
        guard start < scalars.count else { return false }
        if scalars[start] == "0" && scalars.count - start > 1 { return false }
        return scalars[start...].allSatisfy(isDigit)
    }

    private static func isAsciiLetter(_ scalar: Unicode.Scalar) -> Bool { ("a"..."z").contains(scalar) || ("A"..."Z").contains(scalar) }

    private static func isDigit(_ scalar: Unicode.Scalar) -> Bool { ("0"..."9").contains(scalar) }

    private static func isBare(_ scalar: Unicode.Scalar, isPart: Bool) -> Bool {
        isAsciiLetter(scalar) || isDigit(scalar) || scalar == "." || scalar == "_" || scalar == "-" || isPart && scalar == "/"
    }

    private static func writeKey(_ key: GesValue, isPart: Bool, to text: inout String) {
        if key.valueKind == .integer {
            text += String(key.asInteger)
            return
        }
        let value = key.textValue ?? ""
        let scalars = Array(value.unicodeScalars)
        let bare = !scalars.isEmpty && !readsAsInteger(scalars) && scalars.allSatisfy { isBare($0, isPart: isPart) }
        if bare { text += value } else { appendQuoted(value, to: &text) }
    }

    private static func appendQuoted(_ value: String, to text: inout String) {
        text += "'"
        for scalar in value.unicodeScalars {
            if scalar == "'" { text += "''" } else { text.unicodeScalars.append(scalar) }
        }
        text += "'"
    }
}
