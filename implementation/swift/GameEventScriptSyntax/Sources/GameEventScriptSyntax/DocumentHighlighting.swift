// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Categorizes GES document text token by token, also after an error: keys and labels before their colon, the schema
// header, typed forms with their message and parameter names, scalars and comments, and in a store file its header,
// imports, module blocks and references. The diagnostic is the reader's.
enum GesDocumentHighlighting {
    private enum Context {
        case container, form, messageForm, handlerForm, seriesForm, message, handler
    }

    static func highlight(_ text: String, _ spans: inout GesSpanBuilder) -> [GesSpanBuilder.Diagnostic] {
        let bytes = spans.bytes
        let store = GesDocumentReader.readStore(text)
        let isStore = store.isStore
        var contexts: [Context] = []
        // Whether a module name follows, until its `{`.
        var inModuleName = false
        // The form a `:Type` names until its `(` opens, and whether the next token is the first inside a context.
        var pending: Context?
        var first = false
        var afterSeriesKind = false
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

            if isStore && contexts.isEmpty, let storeKind = storeSpan(bytes, &position, &inModuleName) {
                spans.add(start, position, storeKind)
                pending = nil
                first = false
                afterSeriesKind = false
                continue
            }
            let opening = pending
            let firstInContext = first
            let seriesKindFollows = afterSeriesKind
            if c != 58 { afterSeriesKind = false }
            pending = nil
            first = false
            var kind = GesSyntaxKind.plain
            if c == 39 || c == 34 {
                position = quotedEnd(bytes, position)
                kind = .string
            } else if c == 58 && position + 1 < bytes.count && isLetter(bytes[position + 1]) {
                position = nameEnd(bytes, position + 1)
                let name = String(decoding: bytes[(start + 1)..<position], as: UTF8.self)
                kind = name == "Schema" || (isStore && name == "Store") ? .keyword : .type
                switch name {
                case "Message": pending = .messageForm
                case "Handler": pending = .handlerForm
                case "Series": pending = .seriesForm
                default: pending = .form
                }
            } else if c == 64 && position + 1 < bytes.count && GesNames.upper(bytes[position + 1]) {
                position = handleSpans(bytes, position, &spans)
                continue
            } else if c == 64 && position + 1 < bytes.count && isKeyByte(bytes[position + 1]) {
                position = referenceSpans(bytes, position, &spans)
                continue
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
                if (context == .messageForm || context == .handlerForm) && firstInContext && (65...90).contains(c) {
                    kind = .message
                    pending = context == .handlerForm ? .handler : .message
                } else if followedByColon(bytes, position) || context == .handler {
                    kind = .identifier
                    afterSeriesKind = word == "kind" && context == .seriesForm
                } else if word == "true" || word == "false" || word == "nothing" {
                    kind = .keyword
                } else if word == "with" && context == .messageForm {
                    kind = .keyword
                } else if (word == "fibonacci" || word == "factorial") && context == .seriesForm && (firstInContext || seriesKindFollows) {
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

        guard let diagnostic = isStore ? store.diagnostic : GesDocumentReader.read(text, references: true).diagnostic else { return [] }
        return [GesSpanBuilder.Diagnostic(code: diagnostic.code, message: GesDocumentDiagnostic.explain(diagnostic.code, limitName: diagnostic.limitName, limitValue: diagnostic.limitValue), start: diagnostic.start, end: diagnostic.end)]
    }

    // A token of a store file outside every bracket that the store grammar owns: braces, `import` and `module`
    // without a key colon, a module name and a key. Returns nil for every other token, which documents categorize.
    private static func storeSpan(_ bytes: [UInt8], _ position: inout Int, _ inModuleName: inout Bool) -> GesSyntaxKind? {
        let c = bytes[position]
        if c == 123 || c == 125 {
            inModuleName = false
            position += 1
            return .symbol
        }
        if inModuleName && c == 46 {
            position += 1
            return .symbol
        }
        if !isKeyByte(c) { return nil }
        var end = position
        while end < bytes.count && isKeyByte(bytes[end]) { end += 1 }
        let word = String(decoding: bytes[position..<end], as: UTF8.self)
        if inModuleName || followedByColon(bytes, end) {
            position = end
            return .identifier
        }
        if word == "import" || word == "module" {
            inModuleName = word == "module"
            position = end
            return .keyword
        }
        return nil
    }

    // A reference: `@`, `.` and `~` are Symbol and each key Identifier. Returns the end.
    private static func referenceSpans(_ bytes: [UInt8], _ start: Int, _ spans: inout GesSpanBuilder) -> Int {
        spans.add(start, start + 1, .symbol)
        var position = start + 1
        while true {
            let keyStart = position
            while position < bytes.count && isKeyByte(bytes[position]) { position += 1 }
            spans.add(keyStart, position, .identifier)
            if position + 1 < bytes.count && bytes[position] == 46 && isKeyByte(bytes[position + 1]) {
                spans.add(position, position + 1, .symbol)
                position += 1
                continue
            }
            if position < bytes.count && bytes[position] == 126 {
                spans.add(position, position + 1, .symbol)
                position += 1
            }
            return position
        }
    }

    private static func isKeyByte(_ c: UInt8) -> Bool { isLetter(c) || isDigit(c) || c == 95 || c == 45 }

    // A handle: `@Type` is Type; `:`, `~` and `/` are Symbol; a quoted id, part or ref is String, a ref `#name` Tag, an
    // integer id, part or version and a hash Number, and other bare ids and parts Plain. Without a well-formed handle
    // only `@Type` is a token. Returns the end.
    private static func handleSpans(_ bytes: [UInt8], _ start: Int, _ spans: inout GesSpanBuilder) -> Int {
        let typeEnd = nameEnd(bytes, start + 1)
        spans.add(start, typeEnd, .type)
        guard let end = GesHandleLiteral.read(bytes, start, allowPin: true)?.end else { return typeEnd }
        var position = typeEnd
        var isPart = false
        while position < end {
            let c = bytes[position]
            let tokenStart = position
            let kind: GesSyntaxKind
            if c == 58 || c == 126 || (c == 47 && !isPart) {
                isPart = isPart || c == 47
                position += 1
                kind = .symbol
            } else if c == 39 {
                position = quotedEnd(bytes, position)
                kind = .string
            } else if c == 35 {
                position = nameEnd(bytes, position + 1)
                kind = .tag
            } else {
                while position < end && bytes[position] != 58 && bytes[position] != 126 && bytes[position] != 39 && (isPart || bytes[position] != 47) { position += 1 }
                let afterTilde = tokenStart > 0 && bytes[tokenStart - 1] == 126
                kind = GesHandleLiteral.readsAsInteger(bytes[tokenStart..<position]) || afterTilde ? .number : .plain
            }
            spans.add(tokenStart, position, kind)
        }
        return end
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
        let end = GesNumberLiteral.tokenEnd(bytes, from: start, brackets: true)
        var position = start
        while position < end && !isComment(bytes, position) { position += 1 }
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
}
