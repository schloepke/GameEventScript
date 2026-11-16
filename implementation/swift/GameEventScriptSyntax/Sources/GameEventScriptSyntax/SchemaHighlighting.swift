// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Categorizes schema source token by token, also after an error: keywords, type names, tags, constants, numbers and
// comments. The diagnostic is the reader's.
enum GesSchemaHighlighting {
    private static let keywords: Set<String> = [
        "schema", "message", "messages", "constant", "parameter", "as", "be", "between", "and", "wrapping", "step", "delta", "within", "integer", "optional", "key", "max", "depth", "tags", "wire", "model", "let", "default", "true", "false",
        "abstract", "packed",
        "with", "all", "without", "computed", "by", "when", "otherwise", "or", "not", "of", "min", "clamp", "abs", "has",
        "sqrt", "cbrt", "sin", "cos", "tan", "asin", "acos", "atan", "atan2", "hypot", "exp", "ln", "floor", "ceil", "truncate", "round", "rad", "deg",
    ]

    static func highlight(_ text: String, _ spans: inout GesSpanBuilder) -> [GesSpanBuilder.Diagnostic] {
        let bytes = spans.bytes
        var position = 0
        var previous = ""
        if bytes.starts(with: [0xEF, 0xBB, 0xBF]) {
            spans.add(0, 3, .plain)
            position = 3
        }
        while position < bytes.count {
            let start = position
            let byte = bytes[position]
            let next = position + 1 < bytes.count ? bytes[position + 1] : 0
            var kind = GesSyntaxKind.plain
            if byte == 47 && next == 47 {
                while position < bytes.count && bytes[position] != 13 && bytes[position] != 10 { position += 1 }
                kind = .comment
            } else if byte == 58 && GesNames.upper(next) {
                position = GesNames.upperNameEnd(bytes, wordEnd(bytes, position + 1))
                kind = .type
            } else if byte == 35 && (GesNames.lower(next) || GesNames.upper(next)) {
                position = wordEnd(bytes, position + 1)
                kind = .tag
            } else if byte == 36 && (GesNames.lower(next) || GesNames.upper(next)) {
                position = wordEnd(bytes, position + 1)
                kind = .constant
            } else if GesNames.digit(byte) || (byte == 45 && GesNames.digit(next)) {
                position += 1
                while position < bytes.count && !isNumberEnd(bytes, position) { position += 1 }
                kind = .number
            } else if byte == 96, let quoted = quotedEnd(bytes, position) {
                // A name in backticks is a name, also where its word is a keyword.
                position = quoted
                kind = .identifier
            } else if GesNames.lower(byte) || GesNames.upper(byte) {
                position = wordEnd(bytes, position)
                if GesNames.upper(byte) { position = GesNames.upperNameEnd(bytes, position) }
                let word = String(decoding: bytes[start..<position], as: UTF8.self)
                // The words of `round half even`, `round half up` and `round half down` are keywords only there.
                let mode = previous == "round" && word == "half" || previous == "half" && (word == "even" || word == "up" || word == "down")
                kind = keywords.contains(word) || mode ? .keyword : GesNames.upper(byte) ? .message : .identifier
                previous = word
                spans.add(start, position, kind)
                continue
            } else {
                position += 1
                while position < bytes.count && bytes[position] & 0xC0 == 0x80 { position += 1 }
            }
            if kind != .plain || (byte != 32 && byte != 9 && byte != 13 && byte != 10) { previous = "" }
            spans.add(start, position, kind)
        }
        guard let error = GesSchemaSourceReader.read(text).error else { return [] }
        var end = error.offset + 1
        while end < bytes.count && bytes[end] & 0xC0 == 0x80 { end += 1 }
        return [.init(code: error.code, message: error.message, start: error.offset, end: min(end, bytes.count))]
    }

    /// The end of a name in backticks on one line, or nil where the backtick closes none.
    private static func quotedEnd(_ bytes: [UInt8], _ start: Int) -> Int? {
        let end = wordEnd(bytes, start + 1)
        return end > start + 1 && end < bytes.count && bytes[end] == 96 ? end + 1 : nil
    }

    private static func wordEnd(_ bytes: [UInt8], _ start: Int) -> Int {
        var position = start
        while position < bytes.count && GesNames.alnum(bytes[position]) { position += 1 }
        return position
    }

    private static func isNumberEnd(_ bytes: [UInt8], _ index: Int) -> Bool {
        switch bytes[index] {
        case 32, 9, 13, 10, 44, 40, 41, 123, 125, 91, 93, 124, 35: return true
        case 47: return index + 1 < bytes.count && bytes[index + 1] == 47
        default: return false
        }
    }
}
