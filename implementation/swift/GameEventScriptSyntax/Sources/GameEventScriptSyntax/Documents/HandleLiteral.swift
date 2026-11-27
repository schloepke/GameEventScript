// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Recognizes the text form of a handle (specs/Language.md#handle-text-form): `@Type:id` with an optional `~version` or
/// ref and an optional `/part`. Documents and Runtime's `GesHandle(parsing:)` share this one scanner; Runtime builds
/// the handle from the recognized parts.
@_spi(Syntax) public enum GesHandleLiteral {
    /// An id or part: an integer or a Text.
    @_spi(Syntax) public enum Key: Equatable, Sendable {
        /// A bare decimal integer.
        case integer(Int64)
        /// A quoted or bare Text.
        case text(String)
    }

    /// What follows the `~` of a handle.
    @_spi(Syntax) public enum Version: Equatable, Sendable {
        /// No `~`.
        case none
        /// A decimal revision.
        case revision(Int64)
        /// The 40 or 64 lowercase hexadecimal digits of a hash.
        case hash(String)
        /// A tag ref, without its `#`.
        case tag(String)
        /// A quoted Text ref.
        case text(String)
    }

    /// The recognized parts of a handle.
    @_spi(Syntax) public struct Parts: Sendable {
        /// The type name, without its `@`.
        @_spi(Syntax) public let typeName: String
        /// The id.
        @_spi(Syntax) public let id: Key
        /// The version or ref.
        @_spi(Syntax) public let version: Version
        /// The part of a composite, or nil.
        @_spi(Syntax) public let part: Key?
    }

    /// Reads a handle at `start`, which holds `@`, without looking at what follows it. Returns nil when the text there is
    /// no well-formed handle; otherwise its end and parts. A `/` that begins a `//` comment never starts or continues a
    /// part.
    @_spi(Syntax) public static func scan(_ bytes: [UInt8], _ start: Int) -> (end: Int, parts: Parts)? {
        guard let scanned = scan(bytes, start, allowPin: false) else { return nil }
        return (scanned.end, scanned.parts)
    }

    /// Reads a handle like `scan(_:_:)`; with `allowPin`, a `~` that no version follows pins the handle of a catalog
    /// (specs/Documents.md#references) and `pin` is its offset, otherwise nil.
    static func scan(_ bytes: [UInt8], _ start: Int, allowPin: Bool) -> (end: Int, parts: Parts, pin: Int?)? {
        var pin: Int?
        var position = start + 1
        guard position < bytes.count, GesNames.upper(bytes[position]) else { return nil }
        while position < bytes.count && (GesNames.alnum(bytes[position]) || bytes[position] == 95) { position += 1 }
        let typeName = String(decoding: bytes[(start + 1)..<position], as: UTF8.self)
        guard GesNames.type(typeName), position < bytes.count, bytes[position] == 58 else { return nil }
        position += 1
        guard let id = key(bytes, &position, isPart: false) else { return nil }
        var version = Version.none
        if position < bytes.count && bytes[position] == 126 {
            position += 1
            if allowPin && (position == bytes.count || isDelimiter(bytes, position)) {
                pin = position - 1
            } else if position < bytes.count && bytes[position] == 35 {
                position += 1
                let tagStart = position
                while position < bytes.count && GesNames.alnum(bytes[position]) { position += 1 }
                let tag = String(decoding: bytes[tagStart..<position], as: UTF8.self)
                guard GesNames.plain(tag) else { return nil }
                version = .tag(tag)
            } else if position < bytes.count && bytes[position] == 39 {
                guard let text = quoted(bytes, &position) else { return nil }
                version = .text(text)
            } else {
                let versionStart = position
                while position < bytes.count && (GesNames.alnum(bytes[position]) || bytes[position] == 45) { position += 1 }
                let text = bytes[versionStart..<position]
                if (text.count == 40 || text.count == 64) && text.allSatisfy({ GesNames.digit($0) || (97...102).contains($0) }) {
                    version = .hash(String(decoding: text, as: UTF8.self))
                } else if readsAsInteger(text), let revision = Int64(String(decoding: text, as: UTF8.self)) {
                    version = .revision(revision)
                } else {
                    return nil
                }
            }
        }
        var part: Key?
        if position < bytes.count && bytes[position] == 47 && !isComment(bytes, position) {
            position += 1
            guard let read = key(bytes, &position, isPart: true) else { return nil }
            part = read
        }
        return (position, Parts(typeName: typeName, id: id, version: version, part: part), pin)
    }

    /// Reads a handle in a document at `start`, which holds `@`. Returns nil when the text there is no well-formed
    /// handle that a document delimiter follows; otherwise its end, its identity (type, id and part, which compare keys)
    /// and whether a version or a ref follows the id. With `allowPin`, a catalog handle may end with a pinning `~` at
    /// offset `pin`, otherwise nil.
    static func read(_ bytes: [UInt8], _ start: Int, allowPin: Bool = false) -> (end: Int, identity: [Unicode.Scalar], versioned: Bool, pin: Int?)? {
        guard let scanned = scan(bytes, start, allowPin: allowPin), scanned.end == bytes.count || isDelimiter(bytes, scanned.end) else { return nil }
        let parts = scanned.parts
        var identity = Array(parts.typeName.unicodeScalars)
        identity.append(":")
        append(parts.id, to: &identity)
        if let part = parts.part {
            identity.append("/")
            append(part, to: &identity)
        }
        return (scanned.end, identity, parts.version != .none, scanned.pin)
    }

    /// Whether a Text id or part may be written without quotes: a nonempty bare run that does not read as an integer
    /// and, for a part, neither starts with `/` nor contains `//`.
    @_spi(Syntax) public static func isBare(_ text: String, isPart: Bool) -> Bool {
        let bytes = Array(text.utf8)
        guard !bytes.isEmpty, !readsAsInteger(bytes[...]), bytes.allSatisfy({ isBareByte($0, isPart) }) else { return false }
        guard isPart else { return true }
        return bytes[0] != 47 && !zip(bytes, bytes.dropFirst()).contains { $0 == 47 && $1 == 47 }
    }

    /// Whether an unquoted handle word reads as an integer: a decimal with an optional `-` and no leading zero.
    static func readsAsInteger(_ text: ArraySlice<UInt8>) -> Bool {
        var index = text.startIndex
        if index < text.endIndex && text[index] == 45 { index += 1 }
        if index == text.endIndex { return false }
        if text[index] == 48 && text.endIndex - index > 1 { return false }
        return text[index...].allSatisfy(GesNames.digit)
    }

    // A document delimiter ends a handle: whitespace, a separator, a closing bracket, a key colon or a comment.
    static func isDelimiter(_ bytes: [UInt8], _ position: Int) -> Bool {
        switch bytes[position] {
        case 32, 9, 13, 10, 44, 93, 41, 58: return true
        default: return isComment(bytes, position)
        }
    }

    private static func isComment(_ bytes: [UInt8], _ position: Int) -> Bool { bytes[position] == 47 && position + 1 < bytes.count && bytes[position + 1] == 47 }

    // An id or part, quoted or bare; a bare one is an integer when it reads as one.
    private static func key(_ bytes: [UInt8], _ position: inout Int, isPart: Bool) -> Key? {
        guard position < bytes.count else { return nil }
        if bytes[position] == 39 {
            guard let text = quoted(bytes, &position) else { return nil }
            return .text(text)
        }
        let start = position
        while position < bytes.count && isBareByte(bytes[position], isPart) && !(isPart && isComment(bytes, position)) { position += 1 }
        if position == start { return nil }
        let bare = bytes[start..<position]
        if readsAsInteger(bare) {
            guard let integer = Int64(String(decoding: bare, as: UTF8.self)) else { return nil }
            return .integer(integer)
        }
        return .text(String(decoding: bare, as: UTF8.self))
    }

    // An identity key: `i<integer>;` or `t<scalar count>:<text>`.
    private static func append(_ key: Key, to identity: inout [Unicode.Scalar]) {
        switch key {
        case .integer(let integer):
            identity.append("i")
            identity.append(contentsOf: String(integer).unicodeScalars)
            identity.append(";")
        case .text(let text):
            let scalars = Array(text.unicodeScalars)
            identity.append("t")
            identity.append(contentsOf: String(scalars.count).unicodeScalars)
            identity.append(":")
            identity.append(contentsOf: scalars)
        }
    }

    // A `'`-quoted text with doubled quotes, from the opening quote at `position`.
    private static func quoted(_ bytes: [UInt8], _ position: inout Int) -> String? {
        position += 1
        var output: [UInt8] = []
        while position < bytes.count {
            let byte = bytes[position]
            position += 1
            if byte != 39 {
                output.append(byte)
            } else if position < bytes.count && bytes[position] == 39 {
                output.append(39)
                position += 1
            } else {
                return String(decoding: output, as: UTF8.self)
            }
        }
        return nil
    }

    // Whether the byte may start a bare or quoted id.
    static func isIdStart(_ byte: UInt8) -> Bool { byte == 39 || isBareByte(byte, false) }

    private static func isBareByte(_ byte: UInt8, _ isPart: Bool) -> Bool {
        GesNames.alnum(byte) || byte == 46 || byte == 95 || byte == 45 || (isPart && byte == 47)
    }
}
