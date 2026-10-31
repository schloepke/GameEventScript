// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Categorizes GES document text token by token, also after an error: keys and labels before their colon, the schema
// header, typed forms with their message and parameter names, scalars and comments. The diagnostic is the reader's.
enum GesDocumentHighlighting {
    private enum Context {
        case container, form, messageForm, handlerForm, seriesForm, message, handler
    }

    static func highlight(_ text: String, _ spans: inout GesSpanBuilder) -> [GesSpanBuilder.Diagnostic] {
        let bytes = spans.bytes
        var contexts: [Context] = []
        // The form a `:Type` names until its `(` opens, and whether the next token is the first inside a context.
        var pending: Context?
        var first = false
        var position = 0
        if bytes.starts(with: [0xEF, 0xBB, 0xBF]) {
            spans.add(0, 3, .plain)
            position = 3
        }
        while position < bytes.count {
            let c = bytes[position]
            let start = position
            if isSpace(c) {
                while position < bytes.count && isSpace(bytes[position]) { position += 1 }
                spans.add(start, position, .plain)
                continue
            }
            if isComment(bytes, position) {
                while position < bytes.count && bytes[position] != 10 && bytes[position] != 13 { position += 1 }
                spans.add(start, position, .comment)
                continue
            }

            let opening = pending
            let firstInContext = first
            pending = nil
            first = false
            var kind = GesSyntaxKind.plain
            if c == 39 || c == 34 {
                position = quotedEnd(bytes, position)
                kind = .string
            } else if c == 58 && position + 1 < bytes.count && isLetter(bytes[position + 1]) {
                position = nameEnd(bytes, position + 1)
                let name = String(decoding: bytes[(start + 1)..<position], as: UTF8.self)
                kind = name == "Schema" ? .keyword : .type
                switch name {
                case "Message": pending = .messageForm
                case "Handler": pending = .handlerForm
                case "Series": pending = .seriesForm
                default: pending = .form
                }
            } else if c == 91 || c == 40 {
                position += 1
                kind = .symbol
                contexts.append(c == 91 ? .container : opening ?? .container)
                first = true
            } else if c == 93 || c == 41 {
                position += 1
                kind = .symbol
                if !contexts.isEmpty { contexts.removeLast() }
            } else if c == 44 || c == 58 {
                position += 1
                kind = .symbol
            } else if c == 35 {
                position = nameEnd(bytes, position + 1)
                kind = .tag
            } else if startsNumber(bytes, position) {
                position = valueEnd(bytes, position)
                kind = .number
            } else if isLetter(c) || c == 95 {
                position = nameEnd(bytes, position)
                let word = String(decoding: bytes[start..<position], as: UTF8.self)
                let context = contexts.last ?? .container
                if (context == .messageForm || context == .handlerForm) && firstInContext && ((65...90).contains(c) || word == "initialization" || word == "undeliverable") {
                    kind = .message
                    pending = context == .handlerForm ? .handler : .message
                } else if followedByColon(bytes, position) || context == .handler {
                    kind = .identifier
                } else if word == "true" || word == "false" || word == "nothing" {
                    kind = .keyword
                } else if word == "with" && context == .messageForm {
                    kind = .keyword
                } else if (word == "fibonacci" || word == "factorial") && context == .seriesForm && firstInContext {
                    kind = .builtin
                } else if word == "Infinity" || word == "NaN" {
                    kind = .number
                }
            } else {
                position += 1
                while position < bytes.count && bytes[position] & 0xC0 == 0x80 { position += 1 }
            }
            spans.add(start, position, kind)
        }

        guard let diagnostic = GesDocumentReader.read(text).diagnostic else { return [] }
        return [GesSpanBuilder.Diagnostic(code: diagnostic.code, message: message(diagnostic), start: diagnostic.start, end: diagnostic.end)]
    }

    private static func isSpace(_ c: UInt8) -> Bool { c == 32 || c == 9 || c == 10 || c == 13 }

    private static func isLetter(_ c: UInt8) -> Bool { c >= 0x41 && c <= 0x5A || c >= 0x61 && c <= 0x7A }

    private static func isDigit(_ c: UInt8) -> Bool { c >= 0x30 && c <= 0x39 }

    private static func isComment(_ bytes: [UInt8], _ position: Int) -> Bool { position + 1 < bytes.count && bytes[position] == 47 && bytes[position + 1] == 47 }

    // The end of a quoted Text with doubled delimiters, or the end of the text when it stays open.
    private static func quotedEnd(_ bytes: [UInt8], _ start: Int) -> Int {
        let quote = bytes[start]
        var position = start + 1
        while position < bytes.count {
            position += 1
            if bytes[position - 1] != quote { continue }
            if position < bytes.count && bytes[position] == quote { position += 1 } else { return position }
        }
        return position
    }

    private static func nameEnd(_ bytes: [UInt8], _ start: Int) -> Int {
        var position = start
        while position < bytes.count && (isLetter(bytes[position]) || isDigit(bytes[position]) || bytes[position] == 95) { position += 1 }
        return position
    }

    // A number starts with a digit, or a sign or `.` before a digit, or a sign before `Infinity`.
    private static func startsNumber(_ bytes: [UInt8], _ position: Int) -> Bool {
        let c = bytes[position]
        if isDigit(c) { return true }
        guard c == 43 || c == 45 || c == 46, position + 1 < bytes.count else { return false }
        let next = bytes[position + 1]
        if isDigit(next) { return true }
        if c != 46 && next == 46 && position + 2 < bytes.count && isDigit(bytes[position + 2]) { return true }
        return c != 46 && bytes[(position + 1)...].starts(with: "Infinity".utf8)
    }

    // An unquoted value token, such as `21.4kN` or `90%`, ends at whitespace, a separator, a bracket or a comment.
    private static func valueEnd(_ bytes: [UInt8], _ start: Int) -> Int {
        var position = start
        while position < bytes.count && !isSpace(bytes[position]) && ![44, 91, 93, 41].contains(bytes[position]) && !isComment(bytes, position) { position += 1 }
        return position
    }

    // A word is a key or label when a colon follows on its line.
    private static func followedByColon(_ bytes: [UInt8], _ start: Int) -> Bool {
        var position = start
        while position < bytes.count {
            if bytes[position] == 32 || bytes[position] == 9 {
                position += 1
            } else {
                return !isComment(bytes, position) && bytes[position] == 58
            }
        }
        return false
    }

    private static func message(_ diagnostic: GesDocumentDiagnostic) -> String {
        switch diagnostic.code {
        case GesDocumentDiagnostic.invalidKey: return "An entry must start with a key; a record field must be a field name."
        case GesDocumentDiagnostic.missingColon: return "A key must be followed by ':' on its line."
        case GesDocumentDiagnostic.duplicateKey: return "This key is already used in its container."
        case GesDocumentDiagnostic.unquotedText: return "Text values must be quoted."
        case GesDocumentDiagnostic.unknownUnit: return "The number has an unknown unit."
        case GesDocumentDiagnostic.invalidLiteral: return "This is not a valid literal."
        case GesDocumentDiagnostic.unknownType: return "Documents know only the built-in types."
        case GesDocumentDiagnostic.unexpectedToken: return "An entry, separator or closing bracket is expected here."
        case GesDocumentDiagnostic.unclosedBracket: return "This bracket is never closed."
        case GesDocumentDiagnostic.unclosedText: return "This Text is never closed."
        case GesDocumentDiagnostic.invalidSchemaHeader: return "A schema header names one record type or a schema file."
        case GesDocumentDiagnostic.misplacedSchemaHeader: return "The schema header must come before the first entry."
        default: return "The document exceeds \(diagnostic.limitName ?? "a limit") (\(diagnostic.limitValue))."
        }
    }
}
