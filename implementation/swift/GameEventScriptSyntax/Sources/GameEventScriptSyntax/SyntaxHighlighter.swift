// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Reusable GES, GESA and GES document highlighting, without a Compiler, Runtime, UI or terminal dependency.
public final class GesSyntaxHighlighter: Sendable {
    /// The language this highlighter reads.
    public let language: GesSyntaxLanguage
    /// Maximum UTF-16 input length per call; larger inputs return an incomplete result.
    public let maxInputLength: Int

    /// Creates a highlighter for one language.
    /// - Throws: `invalidInputLimit` if the bound is not positive.
    public init(language: GesSyntaxLanguage = .ges, maxInputLength: Int = 262_144) throws {
        guard maxInputLength > 0 else { throw GesHighlightingError.invalidInputLimit }
        self.language = language
        self.maxInputLength = maxInputLength
    }

    /// Highlights a complete document, preserving CR, LF, CRLF and all UTF-16 offsets. Invalid source remains
    /// highlightable; GES diagnostics report its syntax errors or, without those, its violated single-source rules, and
    /// GES document diagnostics its first document error.
    public func highlight(_ text: String) -> GesHighlightResult {
        guard text.utf16.count <= maxInputLength else { return .init(spans: [], diagnostics: [], isComplete: false) }
        var spans = GesSpanBuilder(Array(text.utf8))
        if language == .ges {
            var diagnostics: [GesSpanBuilder.Diagnostic] = []
            GesSourceHighlighting.highlight(text, offset: 0, &spans, diagnostics: &diagnostics)
            return spans.finish(diagnostics)
        }
        if language == .gesd {
            let diagnostics = GesDocumentHighlighting.highlight(text, &spans)
            return spans.finish(diagnostics)
        }
        GesAssemblyHighlighting.highlight(&spans)
        return spans.finish([])
    }
}

// Collects the categorized pieces of a text in order as UTF-8 ranges, then converts them to UTF-16, gives each its
// diagnostics and merges equal neighbours.
struct GesSpanBuilder {
    // A diagnostic over the UTF-8 range start..<end.
    struct Diagnostic {
        let code: String
        let message: String
        let start: Int
        let end: Int
    }

    let bytes: [UInt8]
    private var pieces: [(start: Int, end: Int, kind: GesSyntaxKind)] = []

    init(_ bytes: [UInt8]) { self.bytes = bytes }

    mutating func add(_ start: Int, _ end: Int, _ kind: GesSyntaxKind) {
        if end > start { pieces.append((start, end, kind)) }
    }

    // Converts monotonically increasing UTF-8 offsets to UTF-16 offsets.
    private struct Utf16Cursor {
        let bytes: [UInt8]
        var utf8 = 0
        var utf16 = 0

        mutating func offset(_ target: Int) -> Int {
            while utf8 < target {
                let byte = bytes[utf8]
                if byte & 0xC0 != 0x80 { utf16 += byte >= 0xF0 ? 2 : 1 }
                utf8 += 1
            }
            return utf16
        }
    }

    // Sorts the diagnostics into source order and attaches each to the pieces it overlaps; one without length overlaps the
    // piece at its position, or the last piece at the end of the text.
    func finish(_ found: [Diagnostic]) -> GesHighlightResult {
        var textLength = Utf16Cursor(bytes: bytes)
        let length = textLength.offset(bytes.count)
        var starts = Utf16Cursor(bytes: bytes)
        var ends = Utf16Cursor(bytes: bytes)
        let byStart = found.indices.sorted { found[$0].start != found[$1].start ? found[$0].start < found[$1].start : $0 < $1 }
        var startOffsets = [Int](repeating: 0, count: found.count)
        for index in byStart { startOffsets[index] = starts.offset(found[index].start) }
        var endOffsets = [Int](repeating: 0, count: found.count)
        for index in found.indices.sorted(by: { found[$0].end < found[$1].end }) { endOffsets[index] = ends.offset(found[index].end) }
        let diagnostics = byStart.map { GesHighlightDiagnostic(code: found[$0].code, message: found[$0].message, start: startOffsets[$0], length: endOffsets[$0] - startOffsets[$0]) }
        let ordered = diagnostics.map { diagnostic -> (diagnostic: GesHighlightDiagnostic, start: Int, end: Int) in
            let start = min(diagnostic.start, max(length - 1, 0))
            return (diagnostic, start, max(diagnostic.start + diagnostic.length, start + 1))
        }

        var spans: [GesHighlightSpan] = []
        spans.reserveCapacity(pieces.count)
        var active: [(diagnostic: GesHighlightDiagnostic, start: Int, end: Int)] = []
        var next = 0
        var cursor = Utf16Cursor(bytes: bytes)
        for piece in pieces {
            let start = cursor.offset(piece.start)
            let end = cursor.offset(piece.end)
            active.removeAll { $0.end <= start }
            while next < ordered.count && ordered[next].start < end {
                active.append(ordered[next])
                next += 1
            }
            let covering = active.filter { $0.end > start }.map(\.diagnostic)
            if let previous = spans.last, previous.kind == piece.kind, previous.start + previous.length == start, previous.diagnostics == covering {
                spans[spans.count - 1] = GesHighlightSpan(start: previous.start, length: previous.length + end - start, kind: piece.kind, diagnostics: covering)
                continue
            }
            spans.append(GesHighlightSpan(start: start, length: end - start, kind: piece.kind, diagnostics: covering))
        }
        return .init(spans: spans, diagnostics: diagnostics, isComplete: true)
    }
}

// Categorizes GES source from its syntax tree: a token's kind, the role the parser recorded for it and, for a few
// constructs, the node that owns it.
enum GesSourceHighlighting {
    // Adds the pieces of `text`, which starts at the UTF-8 `offset` of the highlighted document; diagnostics are reported
    // only for the complete document.
    static func highlight(_ text: String, offset: Int, _ spans: inout GesSpanBuilder, diagnostics: inout [GesSpanBuilder.Diagnostic]) {
        highlight(text, offset: offset, &spans, reportsDiagnostics: true, diagnostics: &diagnostics)
    }

    static func highlight(_ text: String, offset: Int, _ spans: inout GesSpanBuilder) {
        var none: [GesSpanBuilder.Diagnostic] = []
        highlight(text, offset: offset, &spans, reportsDiagnostics: false, diagnostics: &none)
    }

    private static func highlight(_ text: String, offset: Int, _ spans: inout GesSpanBuilder, reportsDiagnostics: Bool, diagnostics: inout [GesSpanBuilder.Diagnostic]) {
        // Statements outside a handler, as editor input has them, are categorized as statements; the diagnostics remain
        // those the Compiler reports for the source.
        var tree = GesSyntaxParser.parse(text, fragment: true)
        let tokens = tree.tokens
        var kinds = [GesSyntaxKind](repeating: .plain, count: tokens.count)
        for index in tokens.indices { kinds[index] = isDiceSides(tokens, tree.roles, index) ? .number : classify(tokens[index], tree.roles[index]) }
        refine(tree.root, tokens, &kinds)

        let bytes = offset == 0 && spans.bytes.count == text.utf8.count ? spans.bytes : Array(text.utf8)
        for index in tokens.indices {
            let token = tokens[index]
            addTrivia(bytes, token.fullStart, token.start, offset, &spans)
            spans.add(offset + token.start, offset + token.end, kinds[index])
        }

        guard reportsDiagnostics else { return }
        if hasStatements(tree) { tree = GesSyntaxParser.parse(text) }
        let found = tree.diagnostics.isEmpty ? GesSyntaxValidator.validate(tree) : tree.diagnostics
        guard !found.isEmpty else { return }
        let lines = lineStarts(bytes)
        for diagnostic in found {
            let start = position(bytes, lines, diagnostic.line, diagnostic.column)
            let end = max(start, position(bytes, lines, diagnostic.endLine, diagnostic.endColumn))
            diagnostics.append(.init(code: diagnostic.code, message: diagnostic.message, start: offset + start, end: offset + end))
        }
    }

    // Whether a fragment holds a statement outside a handler, which a source does not parse alike.
    private static func hasStatements(_ tree: GesSyntaxTree) -> Bool {
        tree.root.children.contains { node in
            switch tree.tokens[node.firstToken].kind {
            case .module, .constant, .record, .predicate, .function, .on, .illegal, .endOfFile: false
            default: true
            }
        }
    }

    // Leading trivia holds inline whitespace, at most one `//` comment running to the end of its line, and a leading BOM.
    private static func addTrivia(_ bytes: [UInt8], _ start: Int, _ end: Int, _ offset: Int, _ spans: inout GesSpanBuilder) {
        var comment = start
        while comment < end && !(bytes[comment] == 0x2F && comment + 1 < end && bytes[comment + 1] == 0x2F) { comment += 1 }
        spans.add(offset + start, offset + comment, .plain)
        spans.add(offset + comment, offset + end, .comment)
    }

    // The `d6` of a compact `2d6` directly follows its count. As a name, which an implicit multiplication such as `2x`
    // reads, it has a role.
    private static func isDiceSides(_ tokens: [GesToken], _ roles: [GesSyntaxRole], _ index: Int) -> Bool {
        let token = tokens[index]
        guard token.kind == .identifier, roles[index] == .none, index > 0, tokens[index - 1].kind == .float, token.fullStart == token.start else { return false }
        let text = token.text.utf8
        return text.count >= 2 && text.first == 0x64 && text.dropFirst().allSatisfy(GesNames.digit)
    }

    private static func classify(_ token: GesToken, _ role: GesSyntaxRole) -> GesSyntaxKind {
        switch role {
        case .none: break
        case .keyword: return .keyword
        case .opcode: return .builtin
        case .assemblyLabel: return .label
        case .messageName: return .message
        case .callable, .callTarget, .predicateTarget: return .function
        default: return .identifier
        }

        switch token.kind {
        case .endOfFile, .newLine: return .plain
        // An unterminated string is an illegal token up to the end of the source.
        case .illegal: return token.text.utf8.first == 0x27 || token.text.utf8.first == 0x22 ? .string : .plain
        case .identifier: return .identifier
        case .message: return .message
        case .typeName: return .type
        case .constantReference: return .constant
        case .tag: return .tag
        case .float, .percentage, .unitNumber, .mathConstantPi, .mathConstantE, .mathConstantTau, .mathConstantInfinity: return .number
        case .text: return .string
        case .intrinsicAbs, .intrinsicLn, .intrinsicExp, .intrinsicSqrt, .intrinsicCbrt, .intrinsicChance, .intrinsicFloor, .intrinsicCeil, .intrinsicTruncate,
            .intrinsicRad, .intrinsicDeg, .intrinsicWrap, .intrinsicRound, .intrinsicSin, .intrinsicCos, .intrinsicTan, .intrinsicAsin, .intrinsicAcos,
            .intrinsicAtan, .intrinsicAtan2, .intrinsicHypot, .intrinsicDistance, .intrinsicSquared, .intrinsicLength, .intrinsicNormalize, .intrinsicDot,
            .intrinsicCross, .intrinsicAngle:
            return .builtin
        case .series, .fibonacci, .factorial, .min, .max, .zip: return .builtin
        case .dot, .comma, .semicolon, .leftParen, .rightParen, .leftBrace, .rightBrace, .leftBracket, .rightBracket, .colon, .underscore: return .symbol
        default: return token.kind.isSelector ? .builtin : .keyword
        }
    }

    // Categories that depend on the owning node: module names, extension names, two-word intrinsics, the units of a
    // quantity type and the directive dot of inline assembly.
    private static func refine(_ node: GesSyntaxNode, _ tokens: [GesToken], _ kinds: inout [GesSyntaxKind]) {
        var unit = false
        var position = node.firstToken
        for child in node.children {
            while position < child.firstToken {
                own(node, tokens, position, &kinds, &unit)
                position += 1
            }
            refine(child, tokens, &kinds)
            position = child.endToken
        }
        while position < node.endToken {
            own(node, tokens, position, &kinds, &unit)
            position += 1
        }
    }

    // `unit` holds while the node's own tokens are inside the parentheses of a quantity type, such as `:Quantity(m/s)`.
    private static func own(_ node: GesSyntaxNode, _ tokens: [GesToken], _ index: Int, _ kinds: inout [GesSyntaxKind], _ unit: inout Bool) {
        let token = tokens[index]
        if token.kind == .leftParen && index > 0 && tokens[index - 1].kind == .typeName && tokens[index - 1].text == ":Quantity" {
            unit = true
            return
        }
        if unit {
            if token.kind == .rightParen { unit = false } else if token.kind != .newLine { kinds[index] = .builtin }
            return
        }

        switch node.kind {
        case .moduleDeclaration where token.kind != .module && token.kind != .newLine && token.kind != .endOfFile:
            kinds[index] = .module
        case .extensionCallExpression where token.kind == .dot && index > 0 && tokens[index - 1].kind == .selector:
            kinds[index] = .builtin
            if index + 1 < tokens.count { kinds[index + 1] = .builtin }
        // The second word of an intrinsic such as `angle between`.
        case .intrinsicCallExpression where kinds[index] == .keyword && token.kind == .identifier:
            kinds[index] = .builtin
        case .assemblyBlock where token.kind == .dot:
            kinds[index] = .keyword
        default:
            break
        }
    }

    // The UTF-8 offset each line starts at; a BOM occupies no column, so the first line starts after it.
    private static func lineStarts(_ bytes: [UInt8]) -> [Int] {
        var starts = [bytes.starts(with: [0xEF, 0xBB, 0xBF]) ? 3 : 0]
        var index = 0
        while index < bytes.count {
            if bytes[index] == 0x0D && index + 1 < bytes.count && bytes[index + 1] == 0x0A { index += 1 }
            if bytes[index] == 0x0D || bytes[index] == 0x0A { starts.append(index + 1) }
            index += 1
        }
        return starts
    }

    // Columns count Unicode scalars.
    private static func position(_ bytes: [UInt8], _ lines: [Int], _ line: Int?, _ column: Int?) -> Int {
        guard let line, line >= 1 else { return 0 }
        guard line <= lines.count else { return bytes.count }
        var position = lines[line - 1]
        var scalars = max((column ?? 1) - 1, 0)
        while scalars > 0 && position < bytes.count {
            position += 1
            while position < bytes.count && bytes[position] & 0xC0 == 0x80 { position += 1 }
            scalars -= 1
        }
        return position
    }
}

// Categorizes GESA line by line; a dumper string may contain line breaks and continues its line. A source segment holds
// GES up to the first line, outside a GES token, that starts with `.region-end` or `.segment`; a source-line annotation
// holds one GES line after its `|`. Both are highlighted as GES. All offsets are UTF-8 offsets; the syntax is ASCII.
enum GesAssemblyHighlighting {
    static func highlight(_ spans: inout GesSpanBuilder) {
        let text = spans.bytes
        let labels = definedLabels(text)
        var position = 0
        while position < text.count {
            let end = logicalLineEnd(text, position)
            let next = lineEnd(text, end)
            let sourceSegment = line(text, position, end, labels, &spans)
            spans.add(end, next, .plain)
            position = next
            guard sourceSegment else { continue }

            let blockEnd = sourceSegmentEnd(text, position)
            GesSourceHighlighting.highlight(string(text, position, blockEnd), offset: position, &spans)
            position = blockEnd
        }
    }

    private static func string(_ text: [UInt8], _ start: Int, _ end: Int) -> String { String(decoding: text[start..<end], as: UTF8.self) }

    // The names defined as `name:` at the start of a line; operands that name one are label references.
    private static func definedLabels(_ text: [UInt8]) -> Set<String> {
        var labels: Set<String> = []
        var position = 0
        while position < text.count {
            let end = logicalLineEnd(text, position)
            let start = skipSpaces(text, position, end)
            let word = wordEnd(text, start, end)
            if word > start && word < end && text[word] == 0x3A && !isDigit(text[start]) { labels.insert(string(text, start, word)) }
            position = lineEnd(text, end)
        }
        return labels
    }

    // Where the content of the line starting at `start` ends: at the first line break outside a dumper string. A comment
    // and a source-line annotation end with their physical line.
    private static func logicalLineEnd(_ text: [UInt8], _ start: Int) -> Int {
        let end = contentEnd(text, start)
        if isDirective(text, skipSpaces(text, start, end), end, ".source-line") { return end }
        var position = start
        while position < text.count && text[position] != 0x0D && text[position] != 0x0A {
            if text[position] == 0x2F && position + 1 < text.count && text[position + 1] == 0x2F { return contentEnd(text, position) }
            position = text[position] == 0x22 ? stringEnd(text, position, text.count) : position + 1
        }
        return position
    }

    // The first line start after `start`, outside a GES token, whose line begins with `.region-end` or `.segment`.
    private static func sourceSegmentEnd(_ text: [UInt8], _ start: Int) -> Int {
        let source = Array(text[start...])
        if endsSourceSegment(source, 0) { return start }
        var lexer = GesLexer(String(decoding: source, as: UTF8.self))
        while true {
            let token = lexer.read()
            if token.kind == .endOfFile { return text.count }
            if token.kind == .newLine && endsSourceSegment(source, token.end) { return start + token.end }
        }
    }

    private static func endsSourceSegment(_ source: [UInt8], _ position: Int) -> Bool {
        let end = contentEnd(source, position)
        let start = skipSpaces(source, position, end)
        return isDirective(source, start, end, ".region-end") || isDirective(source, start, end, ".segment")
    }

    // Adds the pieces of one line's content and reports whether it opens a source segment.
    private static func line(_ text: [UInt8], _ start: Int, _ end: Int, _ labels: Set<String>, _ spans: inout GesSpanBuilder) -> Bool {
        var position = skipSpaces(text, start, end)
        spans.add(start, position, .plain)
        if isDirective(text, position, end, ".region") || isDirective(text, position, end, ".region-end") {
            spans.add(position, end, .comment)
            return false
        }
        if isDirective(text, position, end, ".source-line") {
            sourceLine(text, position, end, &spans)
            return false
        }

        var sourceSegment = false
        var mnemonic = true
        var segment = false
        while position < end {
            let c = text[position]
            if c == 0x20 || c == 0x09 {
                let spaces = skipSpaces(text, position, end)
                spans.add(position, spaces, .plain)
                position = spaces
                continue
            }
            if c == 0x2F && position + 1 < end && text[position + 1] == 0x2F {
                spans.add(position, end, .comment)
                return sourceSegment
            }
            if c == 0x22 {
                let close = stringEnd(text, position, end)
                spans.add(position, close, .string)
                position = close
                continue
            }
            if c == 0x2E && position + 1 < end && isWordStart(text[position + 1]) {
                let directive = directiveEnd(text, position + 1, end)
                segment = string(text, position, directive) == ".segment"
                spans.add(position, directive, .keyword)
                position = directive
                mnemonic = false
                continue
            }
            if c == 0x23 {
                let immediate = immediateEnd(text, position + 1, end)
                spans.add(position, immediate, .number)
                position = immediate
                continue
            }
            if c == 0x40 && position + 1 < end && isDigit(text[position + 1]) {
                let address = digitsEnd(text, position + 1, end)
                spans.add(position, address, .comment)
                position = address
                continue
            }
            if isDigit(c) {
                let number = wordEnd(text, position, end)
                spans.add(position, number, .number)
                position = number
                continue
            }
            if isWordStart(c) {
                let wordEnd = wordEnd(text, position, end)
                let word = string(text, position, wordEnd)
                let after = skipSpaces(text, wordEnd, end)
                let kind: GesSyntaxKind
                if segment {
                    kind = .tag
                    sourceSegment = word == "source"
                    segment = false
                } else if mnemonic && wordEnd < end && text[wordEnd] == 0x3A && labels.contains(word) {
                    kind = .label
                } else if mnemonic {
                    kind = .keyword
                    mnemonic = false
                } else if word == "unit" && wordEnd < end && text[wordEnd] == 0x3A {
                    spans.add(position, wordEnd, .keyword)
                    let unit = self.wordEnd(text, wordEnd + 1, end)
                    spans.add(wordEnd, unit, .tag)
                    position = unit
                    continue
                } else if isRegister(word) && wordEnd < end && text[wordEnd] == 0x28 {
                    spans.add(position, wordEnd, .register)
                    let name = self.wordEnd(text, wordEnd + 1, end)
                    if name < end && text[name] == 0x29 {
                        spans.add(wordEnd, wordEnd + 1, .symbol)
                        spans.add(wordEnd + 1, name, .label)
                        spans.add(name, name + 1, .symbol)
                        position = name + 1
                    } else {
                        position = wordEnd
                    }
                    continue
                } else if isRegister(word) {
                    kind = .register
                } else if after < end && text[after] == 0x3D {
                    kind = .identifier
                } else if labels.contains(word) || word.utf8.contains(0x5F) {
                    kind = .label
                } else {
                    kind = c >= 0x41 && c <= 0x5A ? .type : .plain
                }
                spans.add(position, wordEnd, kind)
                position = wordEnd
                continue
            }

            let next = scalarEnd(text, position, end)
            switch c {
            case 0x5B, 0x5D, 0x28, 0x29, 0x2C, 0x3A: spans.add(position, next, .symbol)
            case 0x3D: spans.add(position, next, .keyword)
            default: spans.add(position, next, .plain)
            }
            position = next
        }
        return sourceSegment
    }

    // `.source-line "file" 3 | text`: the text after the separator is one GES line.
    private static func sourceLine(_ text: [UInt8], _ start: Int, _ end: Int, _ spans: inout GesSpanBuilder) {
        let directive = directiveEnd(text, start + 1, end)
        spans.add(start, directive, .keyword)
        var position = directive
        while position < end {
            let c = text[position]
            if c == 0x7C {
                spans.add(position, position + 1, .symbol)
                GesSourceHighlighting.highlight(string(text, position + 1, end), offset: position + 1, &spans)
                return
            }
            let next = c == 0x22 ? stringEnd(text, position, end) : isDigit(c) ? digitsEnd(text, position, end) : scalarEnd(text, position, end)
            spans.add(position, next, c == 0x22 ? .string : isDigit(c) ? .number : .plain)
            position = next
        }
    }

    private static func isDirective(_ text: [UInt8], _ start: Int, _ end: Int, _ directive: StaticString) -> Bool {
        let count = directive.utf8CodeUnitCount
        guard end - start >= count else { return false }
        let matches = directive.withUTF8Buffer { buffer in buffer.indices.allSatisfy { text[start + $0] == buffer[$0] } }
        return matches && (start + count == end || text[start + count] == 0x20 || text[start + count] == 0x09)
    }

    private static func isRegister(_ word: String) -> Bool {
        let bytes = word.utf8
        return bytes.count >= 2 && bytes.first == 0x72 && bytes.dropFirst().allSatisfy(isDigit)
    }

    // A dumper string with `\\` and `\"` escapes; an unterminated one ends at `end`.
    private static func stringEnd(_ text: [UInt8], _ start: Int, _ end: Int) -> Int {
        var position = start + 1
        while position < end {
            if text[position] == 0x5C && position + 1 < end {
                position += 2
            } else {
                position += 1
                if text[position - 1] == 0x22 { return position }
            }
        }
        return end
    }

    private static func immediateEnd(_ text: [UInt8], _ start: Int, _ end: Int) -> Int {
        var position = start
        while position < end && (isWordPart(text[position]) || text[position] == 0x2B || text[position] == 0x2D || text[position] == 0x2E) { position += 1 }
        return position
    }

    private static func directiveEnd(_ text: [UInt8], _ start: Int, _ end: Int) -> Int {
        var position = start
        while position < end && (isWordPart(text[position]) || text[position] == 0x2D) { position += 1 }
        return position
    }

    private static func wordEnd(_ text: [UInt8], _ start: Int, _ end: Int) -> Int {
        var position = start
        while position < end && isWordPart(text[position]) { position += 1 }
        return position
    }

    private static func digitsEnd(_ text: [UInt8], _ start: Int, _ end: Int) -> Int {
        var position = start
        while position < end && isDigit(text[position]) { position += 1 }
        return position
    }

    // The end of the scalar at `start`, so that a piece never splits one.
    private static func scalarEnd(_ text: [UInt8], _ start: Int, _ end: Int) -> Int {
        var position = start + 1
        while position < end && text[position] & 0xC0 == 0x80 { position += 1 }
        return position
    }

    private static func skipSpaces(_ text: [UInt8], _ start: Int, _ end: Int) -> Int {
        var position = start
        while position < end && (text[position] == 0x20 || text[position] == 0x09) { position += 1 }
        return position
    }

    private static func contentEnd(_ text: [UInt8], _ start: Int) -> Int {
        var position = start
        while position < text.count && text[position] != 0x0D && text[position] != 0x0A { position += 1 }
        return position
    }

    private static func lineEnd(_ text: [UInt8], _ contentEnd: Int) -> Int {
        var position = contentEnd
        if position < text.count && text[position] == 0x0D {
            position += 1
        } else if position < text.count && text[position] == 0x0A {
            return position + 1
        }
        if position < text.count && text[position] == 0x0A { position += 1 }
        return position
    }

    private static func isDigit(_ c: UInt8) -> Bool { c >= 0x30 && c <= 0x39 }

    private static func isWordStart(_ c: UInt8) -> Bool { c >= 0x41 && c <= 0x5A || c >= 0x61 && c <= 0x7A || c == 0x5F }

    private static func isWordPart(_ c: UInt8) -> Bool { isWordStart(c) || isDigit(c) }
}
