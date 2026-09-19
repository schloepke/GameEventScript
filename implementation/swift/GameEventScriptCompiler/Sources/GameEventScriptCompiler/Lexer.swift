// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

struct GesLexer {
    let scalars: [Unicode.Scalar]
    var index = 0, line = 1, column = 1, byteOffset = 0
    var previousCR = false
    init(_ text: String) { scalars = Array(text.unicodeScalars) }
    static func lower(_ c: Unicode.Scalar) -> Bool { (97...122).contains(c.value) }
    static func upper(_ c: Unicode.Scalar) -> Bool { (65...90).contains(c.value) }
    static func letter(_ c: Unicode.Scalar) -> Bool { lower(c) || upper(c) }
    static func digit(_ c: Unicode.Scalar) -> Bool { (48...57).contains(c.value) }
    static func space(_ c: Unicode.Scalar) -> Bool { [9, 10, 11, 12, 13, 32].contains(c.value) }
    static func validSuffix(_ text: String) -> Bool {
        guard let separator = text.firstIndex(of: "_") else { return true }
        let digits = text[text.index(after: separator)...]
        return !digits.isEmpty && (digits.count == 1 || digits.first != "0")
    }
    static let boundaries = Set("(){}[],;.:#+-*/!~&|^=<>·×÷−∞∏ℇτφ√∛°∧∨∈∉⊕⋅≤≥¬≠≈≅→⇒↦²³".unicodeScalars)
    var current: Unicode.Scalar { peek(0) }
    func peek(_ offset: Int = 1) -> Unicode.Scalar { index + offset < scalars.count ? scalars[index + offset] : "\0" }
    mutating func advance() {
        guard index < scalars.count else { return }
        let c = current
        byteOffset += String(c).utf8.count
        if c == "\r" {
            line += 1
            column = 1
            previousCR = true
        } else if c == "\n" {
            if !previousCR { line += 1 }
            column = 1
            previousCR = false
        } else {
            column += 1
            previousCR = false
        }
        index += 1
    }
    func slice(_ start: Int) -> String { String(String.UnicodeScalarView(scalars[start..<index])) }
    mutating func read() -> GesToken {
        while index < scalars.count {
            if [9, 11, 12, 32].contains(current.value) {
                advance()
                continue
            }
            if current == "/" && peek() == "/" {
                while index < scalars.count && current != "\n" && current != "\r" { advance() }
                continue
            }
            break
        }
        let start = index
        let firstLine = line
        let firstColumn = column
        let startByte = byteOffset
        func token(_ kind: String, _ text: String) -> GesToken {
            .init(
                kind: kind, text: text, line: firstLine, column: firstColumn,
                endLine: line, endColumn: column, start: startByte, end: byteOffset)
        }
        if index == scalars.count { return token("eof", "") }
        if current == "\r" || current == "\n" {
            let cr = current == "\r"
            advance()
            if cr && current == "\n" { advance() }
            return token("newline", "\n")
        }
        let first = current
        if Self.letter(first) || ((first == ":" || first == "#" || first == "$") && Self.letter(peek())) {
            if !Self.letter(first) {
                if (first == "#" || first == "$") && !Self.lower(peek()) {
                    advance()
                    return token("illegal", slice(start))
                }
                advance()
            }
            while Self.letter(current) || Self.digit(current) { advance() }
            if Self.lower(first) && current == "_" {
                advance()
                while Self.digit(current) { advance() }
            }
            let attachedIllegal =
                current == "&" && peek() == "&" || current == "|" && peek() == "|"
                || current == "^" && ["&", "|", "^"].contains(peek())
            if !Self.validSuffix(slice(start)) || attachedIllegal
                || (index < scalars.count && !Self.space(current) && !Self.boundaries.contains(current))
            {
                while index < scalars.count && !Self.space(current) { advance() }
                return token("illegal", slice(start))
            }
            let kind =
                first == "#"
                ? "tag"
                : first == "$"
                    ? "constant"
                    : first == ":"
                        ? (Self.upper(scalars[start + 1]) ? "type" : "selector")
                        : Self.upper(first) ? "message" : "word"
            return token(kind, slice(start))
        }
        if Self.digit(first) {
            while Self.digit(current) || current == "_" && Self.digit(peek()) { advance() }
            if current == "." && Self.digit(peek()) {
                advance()
                while Self.digit(current) || current == "_" && Self.digit(peek()) { advance() }
            }
            if current == "%" || current == "°" {
                advance()
            } else if (current == "m" || current == "s")
                && (peek() == "\0" || Self.space(peek()) || Self.boundaries.contains(peek()))
            {
                advance()
            }
            if index < scalars.count && !Self.letter(current) && !Self.space(current)
                && !Self.boundaries.contains(current)
            {
                while index < scalars.count && !Self.space(current) { advance() }
                return token("illegal", slice(start))
            }
            return token("number", slice(start))
        }
        if first == "\"" || first == "'" {
            advance()
            var escaped = false
            while index < scalars.count {
                let c = current
                advance()
                if escaped {
                    escaped = false
                    continue
                }
                if c == "\\" {
                    escaped = true
                    continue
                }
                if c == first {
                    if current == first {
                        advance()
                        continue
                    }
                    break
                }
            }
            if let value = GameEventScriptCompilerSupport.quotedText(slice(start)) { return token("text", value) }
            return token("illegal", slice(start))
        }
        let pair = String(first) + String(peek())
        if ["<>", "<=", ">=", "->", "=>"].contains(pair) {
            advance()
            advance()
            return token("symbol", pair)
        }
        advance()
        let aliases: [Unicode.Scalar: String] = [
            "·": "*", "×": "*", "÷": "/", "−": "-", "∞": "infinity", "π": "pi", "∏": "pi", "ℇ": "e", "τ": "tau",
            "√": "sqrt", "∛": "cbrt", "°": "degree", "∧": "and", "∨": "or", "∈": "in", "∉": "not in", "⊕": "xor",
            "⋅": "*", "≤": "<=", "≥": ">=", "¬": "!", "≠": "<>", "→": "->", "⇒": "->", "↦": "=>", "~": "!", "²": "2",
            "³": "3",
        ]
        if first == "²" || first == "³" { return token("superscript", aliases[first]!) }
        if let alias = aliases[first] { return token("symbol", alias) }
        return token("(){}[],;.:_+=<>|^&-*/!".unicodeScalars.contains(first) ? "symbol" : "illegal", String(first))
    }
}
