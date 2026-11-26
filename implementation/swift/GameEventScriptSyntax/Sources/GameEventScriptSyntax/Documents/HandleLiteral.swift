// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Recognizes the text form of a handle in a document (specs/Documents.md#handles): `@Type:id` with an optional
/// `~version` or ref and an optional `/part`. Runtime builds the handle from the recognized text.
enum GesHandleLiteral {
    /// Reads a handle at `start`, which holds `@`. Returns nil when the text there is no well-formed handle that a
    /// document delimiter follows; otherwise its end, its identity (type, id and part, which compare keys) and whether
    /// a version or a ref follows the id.
    static func read(_ bytes: [UInt8], _ start: Int) -> (end: Int, identity: [Unicode.Scalar], versioned: Bool)? {
        var position = start + 1
        guard position < bytes.count, GesNames.upper(bytes[position]) else { return nil }
        while position < bytes.count && (GesNames.alnum(bytes[position]) || bytes[position] == 95) { position += 1 }
        let typeName = String(decoding: bytes[(start + 1)..<position], as: UTF8.self)
        guard GesNames.type(typeName), position < bytes.count, bytes[position] == 58 else { return nil }
        position += 1
        var identity = Array(typeName.unicodeScalars)
        identity.append(":")
        guard key(bytes, &position, isPart: false, &identity) else { return nil }
        var versioned = false
        if position < bytes.count && bytes[position] == 126 {
            versioned = true
            position += 1
            if position < bytes.count && bytes[position] == 35 {
                position += 1
                let tagStart = position
                while position < bytes.count && GesNames.alnum(bytes[position]) { position += 1 }
                guard GesNames.plain(String(decoding: bytes[tagStart..<position], as: UTF8.self)) else { return nil }
            } else if position < bytes.count && bytes[position] == 39 {
                guard quoted(bytes, &position) != nil else { return nil }
            } else {
                let versionStart = position
                while position < bytes.count && (GesNames.alnum(bytes[position]) || bytes[position] == 45) { position += 1 }
                guard isVersion(bytes[versionStart..<position]) else { return nil }
            }
        }
        if position < bytes.count && bytes[position] == 47 && !isComment(bytes, position) {
            position += 1
            identity.append("/")
            guard key(bytes, &position, isPart: true, &identity) else { return nil }
        }
        if position < bytes.count && !isDelimiter(bytes, position) { return nil }
        return (position, identity, versioned)
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
    private static func isDelimiter(_ bytes: [UInt8], _ position: Int) -> Bool {
        switch bytes[position] {
        case 32, 9, 13, 10, 44, 93, 41, 58: return true
        default: return isComment(bytes, position)
        }
    }

    private static func isComment(_ bytes: [UInt8], _ position: Int) -> Bool { bytes[position] == 47 && position + 1 < bytes.count && bytes[position + 1] == 47 }

    // An id or part, quoted or bare, appended to the identity as `i<integer>;` or `t<scalar count>:<text>`.
    private static func key(_ bytes: [UInt8], _ position: inout Int, isPart: Bool, _ identity: inout [Unicode.Scalar]) -> Bool {
        guard position < bytes.count else { return false }
        if bytes[position] == 39 {
            guard let text = quoted(bytes, &position) else { return false }
            append(text, to: &identity)
            return true
        }
        let start = position
        while position < bytes.count && isBareByte(bytes[position], isPart) && !(isPart && isComment(bytes, position)) { position += 1 }
        if position == start { return false }
        let bare = bytes[start..<position]
        if readsAsInteger(bare) {
            guard let integer = Int64(String(decoding: bare, as: UTF8.self)) else { return false }
            identity.append("i")
            identity.append(contentsOf: String(integer).unicodeScalars)
            identity.append(";")
            return true
        }
        append(String(decoding: bare, as: UTF8.self), to: &identity)
        return true
    }

    private static func append(_ text: String, to identity: inout [Unicode.Scalar]) {
        let scalars = Array(text.unicodeScalars)
        identity.append("t")
        identity.append(contentsOf: String(scalars.count).unicodeScalars)
        identity.append(":")
        identity.append(contentsOf: scalars)
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

    // A version is 40 or 64 lowercase hexadecimal digits or a decimal revision within Int64.
    private static func isVersion(_ text: ArraySlice<UInt8>) -> Bool {
        if (text.count == 40 || text.count == 64) && text.allSatisfy({ GesNames.digit($0) || (97...102).contains($0) }) { return true }
        return readsAsInteger(text) && Int64(String(decoding: text, as: UTF8.self)) != nil
    }

    private static func isBareByte(_ byte: UInt8, _ isPart: Bool) -> Bool {
        GesNames.alnum(byte) || byte == 46 || byte == 95 || byte == 45 || (isPart && byte == 47)
    }
}
