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

    // Keyword words of the C# lexer; every other lowercase word is an identifier.
    static let keywords: [String: GesTokenKind] = [
        "on": .on,
        "record": .record,
        "predicate": .predicate,
        "function": .function,
        "emit": .emit,
        "publish": .publish,
        "matching": .matching,
        "without": .without,
        "let": .`let`,
        "as": .`as`,
        "be": .be,
        "when": .when,
        "otherwise": .otherwise,
        "has": .has,
        "parse": .keywordParse,
        "empty": .empty,
        "if": .`if`,
        "else": .`else`,
        "for": .`for`,
        "in": .`in`,
        "of": .of,
        "starts": .starts,
        "ends": .ends,
        "with": .with,
        "is": .`is`,
        "numeric": .numeric,
        "or": .operatorOr,
        "xor": .operatorXor,
        "and": .operatorAnd,
        "div": .operatorIntegerDivide,
        "mod": .operatorModulo,
        "rem": .operatorRemainder,
        "not": .operatorNot,
        "to": .to,
        "true": .`true`,
        "false": .`false`,
        "nothing": .nothing,
        "pi": .mathConstantPi,
        "e": .mathConstantE,
        "tau": .mathConstantTau,
        "infinity": .mathConstantInfinity,
        "abs": .intrinsicAbs,
        "ln": .intrinsicLn,
        "exp": .intrinsicExp,
        "sqrt": .intrinsicSqrt,
        "cbrt": .intrinsicCbrt,
        "chance": .intrinsicChance,
        "floor": .intrinsicFloor,
        "ceil": .intrinsicCeil,
        "truncate": .intrinsicTruncate,
        "rad": .intrinsicRad,
        "deg": .intrinsicDeg,
        "wrap": .intrinsicWrap,
        "round": .intrinsicRound,
        "sin": .intrinsicSin,
        "cos": .intrinsicCos,
        "tan": .intrinsicTan,
        "asin": .intrinsicAsin,
        "acos": .intrinsicAcos,
        "atan": .intrinsicAtan,
        "atan2": .intrinsicAtan2,
        "hypot": .intrinsicHypot,
        "distance": .intrinsicDistance,
        "squared": .intrinsicSquared,
        "length": .intrinsicLength,
        "normalize": .intrinsicNormalize,
        "dot": .intrinsicDot,
        "cross": .intrinsicCross,
        "angle": .intrinsicAngle,
        "random": .keywordRandom,
        "series": .series,
        "fibonacci": .fibonacci,
        "factorial": .factorial,
        "clamp": .clamp,
        "min": .min,
        "max": .max,
        "zip": .zip,
        "roll": .roll,
        "default": .`default`,
        "module": .module,
        "constant": .constant,
    ]

    // Selector keywords; another `:word` is a selector name.
    static let selectors: [String: GesTokenKind] = [
        ":any": .selectorAny,
        ":all": .selectorAll,
        ":filter": .selectorFilter,
        ":has": .selectorHas,
        ":take": .selectorTake,
        ":drop": .selectorDrop,
        ":count": .selectorCount,
        ":choose": .selectorChoose,
        ":draw": .selectorDraw,
        ":shuffle": .selectorShuffle,
        ":reverse": .selectorReverse,
        ":sum": .selectorSum,
        ":average": .selectorAverage,
        ":select": .selectorSelect,
        ":contains": .selectorContains,
        ":sort": .selectorSort,
    ]

    static let pairs: [String: GesTokenKind] = ["<>": .operatorNotEqual, "<=": .operatorLessOrEqual, ">=": .operatorGreaterOrEqual, "->": .operatorImplication, "=>": .projectionArrow]

    // Punctuation and operator characters with the C# kind and the spelling the parser and lowering read.
    static let characters: [Unicode.Scalar: (GesTokenKind, String)] = [
        "(": (.leftParen, "("), ")": (.rightParen, ")"), "{": (.leftBrace, "{"), "}": (.rightBrace, "}"), "[": (.leftBracket, "["), "]": (.rightBracket, "]"),
        ",": (.comma, ","), ";": (.semicolon, ";"), ".": (.dot, "."), ":": (.colon, ":"), "_": (.underscore, "_"),
        "=": (.operatorEqual, "="), "<": (.operatorLess, "<"), ">": (.operatorGreater, ">"), "|": (.operatorCollectionUnion, "|"), "^": (.operatorPower, "^"),
        "&": (.operatorCollectionIntersect, "&"), "+": (.operatorPlus, "+"), "-": (.operatorMinus, "-"), "*": (.operatorMultiply, "*"), "/": (.operatorDivide, "/"), "!": (.operatorNot, "!"),
        "·": (.operatorMultiply, "*"), "×": (.operatorMultiply, "*"), "⋅": (.operatorMultiply, "*"), "÷": (.operatorDivide, "/"), "−": (.operatorMinus, "-"),
        "∞": (.mathConstantInfinity, "infinity"), "π": (.mathConstantPi, "pi"), "∏": (.mathConstantPi, "pi"), "ℇ": (.mathConstantE, "e"), "τ": (.mathConstantTau, "tau"),
        "√": (.intrinsicSqrt, "sqrt"), "∛": (.intrinsicCbrt, "cbrt"), "°": (.identifier, "degree"), "∧": (.operatorAnd, "and"), "∨": (.operatorOr, "or"),
        "∈": (.in, "in"), "∉": (.notIn, "not in"), "⊕": (.operatorXor, "xor"), "≤": (.operatorLessOrEqual, "<="), "≥": (.operatorGreaterOrEqual, ">="),
        "¬": (.operatorNot, "!"), "~": (.operatorNot, "!"), "≠": (.operatorNotEqual, "<>"), "→": (.operatorImplication, "->"), "⇒": (.operatorImplication, "->"), "↦": (.projectionArrow, "=>"),
        "²": (.superscriptInteger, "2"), "³": (.superscriptInteger, "3"),
    ]

    static let unitStarts = Set("msdkMWHNJVAK°".unicodeScalars)
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

        func token(_ kind: GesTokenKind, _ text: String, word: Bool = false) -> GesToken {
            .init(kind: kind, text: text, isWord: word, line: firstLine, column: firstColumn, endLine: line, endColumn: column, start: startByte, end: byteOffset)
        }

        if index == scalars.count { return token(.endOfFile, "") }
        if current == "\r" || current == "\n" {
            let cr = current == "\r"
            advance()
            if cr && current == "\n" { advance() }
            return token(.newLine, "\n")
        }
        let first = current
        if Self.letter(first) || ((first == ":" || first == "#" || first == "$") && Self.letter(peek())) {
            if !Self.letter(first) {
                if (first == "#" || first == "$") && !Self.lower(peek()) {
                    advance()
                    return token(.illegal, slice(start))
                }
                advance()
            }
            while Self.letter(current) || Self.digit(current) { advance() }
            if Self.lower(first) && current == "_" {
                advance()
                while Self.digit(current) { advance() }
            }
            let attachedIllegal = current == "&" && peek() == "&" || current == "|" && peek() == "|" || current == "^" && ["&", "|", "^"].contains(peek())
            if !Self.validSuffix(slice(start)) || attachedIllegal || (index < scalars.count && !Self.space(current) && !Self.boundaries.contains(current)) {
                while index < scalars.count && !Self.space(current) { advance() }
                return token(.illegal, slice(start))
            }
            let text = slice(start)
            if first == "#" { return token(.tag, text) }
            if first == "$" { return token(.constantReference, text) }
            if first == ":" { return token(Self.upper(scalars[start + 1]) ? .typeName : Self.selectors[text] ?? .selector, text) }
            if Self.upper(first) { return token(.message, text) }
            return token(Self.keywords[text] ?? .identifier, text, word: true)
        }
        if Self.digit(first) {
            while Self.digit(current) || current == "_" && Self.digit(peek()) { advance() }
            if current == "." && Self.digit(peek()) {
                advance()
                while Self.digit(current) || current == "_" && Self.digit(peek()) { advance() }
            }
            // The longest unit spelling directly followed by a boundary forms one quantity literal, so `10m/s` is a speed
            // and `10m / s` a division. The token text keeps the spelling, whose decoding applies prefix scaling.
            var quantity = false
            // Only these characters start a unit spelling, so unitless numbers skip the table.
            let suffixLength = Self.unitStarts.contains(current) ? GesCompilerSupport.unitSuffixLength(scalars, at: index) { Self.space($0) || Self.boundaries.contains($0) } : 0
            if current == "%" {
                advance()
            } else if suffixLength > 0 {
                for _ in 0..<suffixLength { advance() }
                quantity = true
            } else if current == "°" {
                advance()
                quantity = true
            }
            if index < scalars.count && !Self.letter(current) && !Self.space(current) && !Self.boundaries.contains(current) {
                while index < scalars.count && !Self.space(current) { advance() }
                return token(.illegal, slice(start))
            }
            let text = slice(start)
            return token(quantity ? .unitNumber : text.unicodeScalars.last == "%" ? .percentage : .float, text)
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
            if let value = GesCompilerSupport.quotedText(slice(start)) { return token(.text, value) }
            return token(.illegal, slice(start))
        }
        let pair = String(first) + String(peek())
        if let kind = Self.pairs[pair] {
            advance()
            advance()
            return token(kind, pair)
        }
        advance()
        if let (kind, text) = Self.characters[first] { return token(kind, text) }
        return token(.illegal, String(first))
    }
}
