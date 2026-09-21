// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation

internal struct ConformanceLine {
    let text: String
    let scalars: [Unicode.Scalar]
    let number: Int
    let byteOffset: Int

    init(_ text: String, number: Int, byteOffset: Int) {
        self.text = text
        self.scalars = Array(text.unicodeScalars)
        self.number = number
        self.byteOffset = byteOffset
    }

    func range(_ offset: Int = 0, _ length: Int? = nil) -> ConformanceSourceRange {
        let begin = min(max(0, offset), scalars.count)
        let count = min(max(0, length ?? (scalars.count - begin)), scalars.count - begin)
        let prefix = String(String.UnicodeScalarView(scalars[..<begin]))
        let value = String(String.UnicodeScalarView(scalars[begin..<(begin + count)]))
        return .init(byteOffset: byteOffset + prefix.utf8.count, byteLength: value.utf8.count, line: number, column: begin + 1, endLine: number, endColumn: begin + count + 1)
    }
}

internal indirect enum ConformanceYamlContent {
    case scalar(ConformanceData)
    case array([ConformanceYamlNode])
    case object([ConformanceYamlEntry])
}

internal struct ConformanceYamlEntry {
    let key: String
    let value: ConformanceYamlNode
    let range: ConformanceSourceRange
}

internal struct ConformanceYamlNode {
    let content: ConformanceYamlContent
    let range: ConformanceSourceRange
    var entries: [ConformanceYamlEntry]? {
        if case .object(let entries) = content { return entries }
        return nil
    }
    var items: [ConformanceYamlNode]? {
        if case .array(let items) = content { return items }
        return nil
    }
    var scalar: ConformanceData? {
        if case .scalar(let value) = content { return value }
        return nil
    }
    var string: String? { scalar?.stringValue }
    var number: String? { scalar?.numberValue }
    var boolean: Bool? { scalar?.boolValue }

    subscript(_ key: String) -> ConformanceYamlNode? { entries?.first(where: { $0.key.utf8.elementsEqual(key.utf8) })?.value }

    var data: ConformanceData {
        switch content {
        case .scalar(let value): return value
        case .array(let items): return .array(items.map(\.data))
        case .object(let entries): return .object(entries.map { .init(key: $0.key, value: $0.value.data) })
        }
    }
}

internal final class ConformanceYamlParser {
    let lines: [ConformanceLine]
    let limits: ConformanceParserLimits
    private var index = 0
    private var nodes = 0

    init(_ lines: [ConformanceLine], limits: ConformanceParserLimits) {
        self.lines = lines
        self.limits = limits
    }

    func parse() throws -> ConformanceYamlNode {
        skipEmpty()
        guard index < lines.count else { throw failure("syntax", "A YAML root mapping is required.") }
        let root = try block(0, 0)
        skipEmpty()
        guard index == lines.count, root.entries != nil else { throw failure("syntax", "The YAML root must be one mapping.") }
        return root
    }

    private func block(_ indent: Int, _ depth: Int) throws -> ConformanceYamlNode {
        try checkDepth(depth)
        skipEmpty()
        guard index < lines.count, try indentation(lines[index]) == indent, indent % 2 == 0 else { throw failure("syntax", "YAML requires exactly two spaces per indentation level.") }
        let text = content(lines[index], indent)
        if text == "---" || text == "..." || text.first == "%" { throw failure("unsupportedFeature", "YAML directives and multiple documents are not supported.") }
        return try text.first == "-" ? sequence(indent, depth) : mapping(indent, depth)
    }

    private func mapping(_ indent: Int, _ depth: Int) throws -> ConformanceYamlNode {
        let range = lines[index].range(indent)
        try countNode(range)
        var entries: [ConformanceYamlEntry] = []
        var names: Set<[UInt8]> = []
        while index < lines.count {
            if Self.empty(lines[index].text) {
                index += 1
                continue
            }
            let actual = try indentation(lines[index])
            if actual < indent { break }
            guard actual == indent else { throw failure("syntax", "Unexpected YAML indentation.") }
            let line = lines[index]
            let raw = Self.trimEnd(Self.stripComment(content(line, indent)))
            if raw.first == "-" { break }
            try property(raw, line: line, offset: indent, depth: depth, entries: &entries, names: &names)
        }
        return .init(content: .object(entries), range: range)
    }

    private func sequence(_ indent: Int, _ depth: Int) throws -> ConformanceYamlNode {
        let range = lines[index].range(indent)
        try countNode(range)
        var values: [ConformanceYamlNode] = []
        while index < lines.count {
            if Self.empty(lines[index].text) {
                index += 1
                continue
            }
            let actual = try indentation(lines[index])
            if actual < indent { break }
            guard actual == indent else { throw failure("syntax", "Unexpected sequence indentation.") }
            let line = lines[index]
            let raw = Array(Self.trimEnd(Self.stripComment(content(line, indent))).unicodeScalars)
            guard raw.first == "-", raw.count == 1 || raw[1] == " " else { break }
            let item = String(String.UnicodeScalarView(raw.dropFirst(min(2, raw.count))))
            if item.isEmpty {
                index += 1
                skipEmpty()
                guard index < lines.count, try indentation(lines[index]) == indent + 2 else { throw failure("syntax", "A sequence item requires a nested value.") }
                values.append(try block(indent + 2, depth + 1))
            } else if Self.mappingColon(item) != nil && !"[{\"'".contains(item.first!) {
                try checkDepth(depth + 1)
                try countNode(line.range(indent + 2))
                var entries: [ConformanceYamlEntry] = []
                var names: Set<[UInt8]> = []
                try property(item, line: line, offset: indent + 2, depth: depth + 1, entries: &entries, names: &names)
                while true {
                    skipEmpty()
                    if index == lines.count { break }
                    let actual = try indentation(lines[index])
                    if actual < indent + 2 { break }
                    guard actual == indent + 2 else { throw failure("syntax", "Unexpected sequence mapping indentation.") }
                    let next = Self.trimEnd(Self.stripComment(content(lines[index], indent + 2)))
                    if next.first == "-" { break }
                    try property(next, line: lines[index], offset: indent + 2, depth: depth + 1, entries: &entries, names: &names)
                }
                values.append(.init(content: .object(entries), range: line.range(indent + 2)))
            } else {
                index += 1
                values.append(try inline(item, line: line, offset: indent + 2, depth: depth + 1))
            }
        }
        return .init(content: .array(values), range: range)
    }

    private func property(_ raw: String, line: ConformanceLine, offset: Int, depth: Int, entries: inout [ConformanceYamlEntry], names: inout Set<[UInt8]>) throws {
        guard let colon = Self.mappingColon(raw), colon > 0 else { throw failure("syntax", "A mapping entry requires key: value.", line.range(offset)) }
        let chars = Array(raw.unicodeScalars)
        let token = Self.trimEnd(String(String.UnicodeScalarView(chars[..<colon])))
        let key = try parseKey(token, line: line, offset: offset)
        guard names.insert(Array(key.utf8)).inserted else { throw failure("duplicateKey", "Duplicate YAML key '\(key)'.", line.range(offset, token.unicodeScalars.count)) }
        var start = colon + 1
        while start < chars.count && chars[start] == " " { start += 1 }
        let valueText = String(String.UnicodeScalarView(chars[start...]))
        index += 1
        let value: ConformanceYamlNode
        if valueText.isEmpty {
            skipEmpty()
            if index < lines.count, try indentation(lines[index]) > offset {
                guard try indentation(lines[index]) == offset + 2 else { throw failure("syntax", "YAML uses exactly two spaces per indentation level.") }
                value = try block(offset + 2, depth + 1)
            } else {
                value = try node(.scalar(.null), line.range(offset + colon + 1, 0))
            }
        } else {
            value = try inline(valueText, line: line, offset: offset + start, depth: depth + 1)
        }
        entries.append(.init(key: key, value: value, range: line.range(offset, token.unicodeScalars.count)))
    }

    fileprivate func parseKey(_ token: String, line: ConformanceLine, offset: Int) throws -> String {
        if token.first == "\"" || token.first == "'" {
            let value = try inline(token, line: line, offset: offset, depth: 1)
            guard let string = value.string else { throw failure("syntax", "Quoted keys must be strings.", line.range(offset)) }
            return string
        }
        if token == "<<" { throw failure("unsupportedFeature", "YAML merge keys are not supported.", line.range(offset)) }
        let bytes = Array(token.utf8)
        guard let first = bytes.first, Self.letter(first), bytes.dropFirst().allSatisfy({ Self.letter($0) || (48...57).contains($0) || [95, 46, 45].contains($0) }) else {
            throw failure("syntax", "Invalid plain YAML mapping key.", line.range(offset))
        }
        return token
    }

    private func inline(_ text: String, line: ConformanceLine, offset: Int, depth: Int) throws -> ConformanceYamlNode {
        let parser = ConformanceYamlFlow(owner: self, text: text, line: line, offset: offset)
        let value = try parser.value(depth)
        parser.spaces()
        guard parser.atEnd else { throw failure("syntax", "Unexpected characters after YAML value.", line.range(offset + parser.position)) }
        return value
    }

    fileprivate func node(_ value: ConformanceYamlContent, _ range: ConformanceSourceRange) throws -> ConformanceYamlNode {
        try countNode(range)
        if case .scalar(let scalar) = value, let text = scalar.stringValue ?? scalar.numberValue, text.utf8.count > limits.maxScalarBytes { throw failure("limitExceeded", "YAML scalar exceeds maxScalarBytes.", range) }
        return .init(content: value, range: range)
    }

    fileprivate func checkDepth(_ depth: Int) throws { guard limits.maxYamlDepth > 0, depth <= limits.maxYamlDepth else { throw failure("limitExceeded", "YAML nesting exceeds maxYamlDepth.") } }

    fileprivate func failure(_ code: String, _ message: String, _ range: ConformanceSourceRange? = nil) -> ConformanceParseError {
        .init("conformance.yaml.\(code)", message, range ?? (lines.isEmpty ? .init() : lines[min(index, lines.count - 1)].range()))
    }

    private func countNode(_ range: ConformanceSourceRange) throws {
        nodes += 1
        guard limits.maxYamlNodes > 0, nodes <= limits.maxYamlNodes else { throw failure("limitExceeded", "YAML node count exceeds maxYamlNodes.", range) }
    }

    private func indentation(_ line: ConformanceLine) throws -> Int {
        let count = line.scalars.prefix(while: { $0 == " " }).count
        if count < line.scalars.count, line.scalars[count] == "\t" { throw failure("syntax", "Tabs are invalid in YAML indentation.", line.range(count, 1)) }
        return count
    }

    private func content(_ line: ConformanceLine, _ offset: Int) -> String { String(String.UnicodeScalarView(line.scalars.dropFirst(offset))) }

    private func skipEmpty() { while index < lines.count && Self.empty(lines[index].text) { index += 1 } }

    private static func empty(_ value: String) -> Bool {
        let text = trim(value)
        return text.isEmpty || text.first == "#"
    }

    static func trim(_ value: String) -> String { String(String.UnicodeScalarView(value.unicodeScalars.drop(while: { $0 == " " || $0 == "\t" }).reversed().drop(while: { $0 == " " || $0 == "\t" }).reversed())) }

    fileprivate static func trimEnd(_ value: String) -> String { String(String.UnicodeScalarView(value.unicodeScalars.reversed().drop(while: { $0 == " " || $0 == "\t" }).reversed())) }

    fileprivate static func letter(_ byte: UInt8) -> Bool { (65...90).contains(byte) || (97...122).contains(byte) }

    private static func mappingColon(_ value: String) -> Int? {
        let chars = Array(value.unicodeScalars)
        var quote: Unicode.Scalar?
        var depth = 0
        var i = 0
        while i < chars.count {
            let c = chars[i]
            if let current = quote {
                if c == "\\", current == "\"" {
                    i += 2
                    continue
                }
                if c == current {
                    if current == "'", i + 1 < chars.count, chars[i + 1] == "'" {
                        i += 2
                        continue
                    }
                    quote = nil
                }
            } else if c == "\"" || c == "'" {
                quote = c
            } else if c == "[" || c == "{" {
                depth += 1
            } else if c == "]" || c == "}" {
                depth -= 1
            } else if c == ":", depth == 0, i + 1 == chars.count || chars[i + 1] == " " {
                return i
            }
            i += 1
        }
        return nil
    }

    private static func stripComment(_ value: String) -> String {
        let chars = Array(value.unicodeScalars)
        var quote: Unicode.Scalar?
        var i = 0
        while i < chars.count {
            let c = chars[i]
            if let current = quote {
                if c == "\\", current == "\"" {
                    i += 2
                    continue
                }
                if c == current {
                    if current == "'", i + 1 < chars.count, chars[i + 1] == "'" {
                        i += 2
                        continue
                    }
                    quote = nil
                }
            } else if c == "\"" || c == "'" {
                quote = c
            } else if c == "#", i == 0 || chars[i - 1].properties.isWhitespace {
                return String(String.UnicodeScalarView(chars[..<i]))
            }
            i += 1
        }
        return value
    }
}

private final class ConformanceYamlFlow {
    let owner: ConformanceYamlParser
    let text: [Unicode.Scalar]
    let line: ConformanceLine
    let offset: Int
    var position = 0
    var atEnd: Bool { position == text.count }

    init(owner: ConformanceYamlParser, text: String, line: ConformanceLine, offset: Int) {
        self.owner = owner
        self.text = Array(text.unicodeScalars)
        self.line = line
        self.offset = offset
    }

    func spaces() { while position < text.count && text[position] == " " { position += 1 } }

    func value(_ depth: Int) throws -> ConformanceYamlNode {
        try owner.checkDepth(depth)
        spaces()
        guard !atEnd else { throw error("syntax", "A YAML value is required.") }
        let start = position
        switch text[position] {
        case "[":
            position += 1
            spaces()
            var items: [ConformanceYamlNode] = []
            if consume("]") { return try owner.node(.array(items), range(start)) }
            while true {
                items.append(try value(depth + 1))
                spaces()
                if consume("]") { return try owner.node(.array(items), range(start)) }
                try require(",")
                if position < text.count && text[position] == "]" { throw error("syntax", "Trailing flow commas are not supported.") }
            }
        case "{":
            position += 1
            spaces()
            var entries: [ConformanceYamlEntry] = []
            var keys: Set<[UInt8]> = []
            if consume("}") { return try owner.node(.object(entries), range(start)) }
            while true {
                spaces()
                let begin = position
                let key: String
                if position < text.count && (text[position] == "\"" || text[position] == "'") {
                    key = try quoted()
                } else {
                    while position < text.count && ![":", "}", ","].contains(text[position]) { position += 1 }
                    let token = ConformanceYamlParser.trimEnd(String(String.UnicodeScalarView(text[begin..<position])))
                    key = try owner.parseKey(token, line: line, offset: offset + begin)
                }
                guard keys.insert(Array(key.utf8)).inserted else { throw error("duplicateKey", "Duplicate YAML key '\(key)'.", begin) }
                try require(":")
                let nested = try value(depth + 1)
                entries.append(.init(key: key, value: nested, range: range(begin)))
                spaces()
                if consume("}") { return try owner.node(.object(entries), range(start)) }
                try require(",")
            }
        case "\"", "'": return try owner.node(.scalar(.string(quoted())), range(start))
        case "|", ">", "&", "*", "!", "%", "@", "`": throw error("unsupportedFeature", "This YAML feature is not supported.")
        default:
            while position < text.count && ![",", "]", "}"].contains(text[position]) {
                if text[position] == "#", position == start || text[position - 1].properties.isWhitespace { break }
                position += 1
            }
            let token = ConformanceYamlParser.trimEnd(String(String.UnicodeScalarView(text[start..<position])))
            guard !token.isEmpty else { throw error("invalidScalar", "An empty plain scalar is invalid.", start) }
            let result: ConformanceData
            if token == "null" {
                result = .null
            } else if token == "true" || token == "false" {
                result = .bool(token == "true")
            } else if Self.number(token), !token.contains(".") && !token.contains("e") && !token.contains("E") {
                result = .number(token)
            } else if Self.number(token), let value = Double(token), value.isFinite {
                result = .number(token)
            } else {
                if [".nan", ".inf", "-.inf"].contains(token) || token.lowercased().hasPrefix("0x") || token.lowercased().hasPrefix("0o") { throw error("unsupportedFeature", "This YAML numeric form is not supported.", start) }
                if token.contains(": ") { throw error("syntax", "A plain scalar containing ': ' must be quoted.", start) }
                result = .string(token)
            }
            return try owner.node(.scalar(result), range(start))
        }
    }

    private func quoted() throws -> String {
        let quote = text[position]
        position += 1
        var result = String.UnicodeScalarView()
        while position < text.count {
            let c = text[position]
            position += 1
            if c == quote {
                if quote == "'", consume("'") {
                    result.append("'")
                    continue
                }
                return String(result)
            }
            if c != "\\" || quote == "'" {
                result.append(c)
                continue
            }
            guard position < text.count else { break }
            let escape = text[position]
            position += 1
            switch escape {
            case "\"", "\\", "/": result.append(escape)
            case "b": result.append("\u{8}")
            case "f": result.append("\u{c}")
            case "n": result.append("\n")
            case "r": result.append("\r")
            case "t": result.append("\t")
            case "u":
                let first = try hex4()
                if (0xd800...0xdbff).contains(first) {
                    guard consume("\\"), consume("u") else { throw error("invalidScalar", "A high surrogate must be followed by a low surrogate.") }
                    let second = try hex4()
                    guard (0xdc00...0xdfff).contains(second) else { throw error("invalidScalar", "Invalid low surrogate.") }
                    result.append(Unicode.Scalar(0x10000 + (first - 0xd800) * 1024 + second - 0xdc00)!)
                } else {
                    guard let scalar = Unicode.Scalar(first) else { throw error("invalidScalar", "Unpaired low surrogate.") }
                    result.append(scalar)
                }
            default: throw error("invalidScalar", "Unsupported YAML string escape.")
            }
        }
        throw error("syntax", "Unterminated quoted string.")
    }

    private func hex4() throws -> UInt32 {
        guard position + 4 <= text.count else { throw error("invalidScalar", "Incomplete Unicode escape.") }
        let digits = String(String.UnicodeScalarView(text[position..<(position + 4)]))
        position += 4
        guard let value = UInt32(digits, radix: 16) else { throw error("invalidScalar", "Invalid Unicode escape.") }
        return value
    }

    private static func number(_ value: String) -> Bool {
        let bytes = Array(value.utf8)
        var i = 0
        if bytes.first == 45 { i += 1 }
        guard i < bytes.count else { return false }
        if bytes[i] == 48 {
            i += 1
        } else {
            guard (49...57).contains(bytes[i]) else { return false }
            while i < bytes.count && (48...57).contains(bytes[i]) { i += 1 }
        }
        if i < bytes.count && bytes[i] == 46 {
            i += 1
            let begin = i
            while i < bytes.count && (48...57).contains(bytes[i]) { i += 1 }
            if i == begin { return false }
        }
        if i < bytes.count && (bytes[i] == 69 || bytes[i] == 101) {
            i += 1
            if i < bytes.count && (bytes[i] == 43 || bytes[i] == 45) { i += 1 }
            let begin = i
            while i < bytes.count && (48...57).contains(bytes[i]) { i += 1 }
            if i == begin { return false }
        }
        return i == bytes.count
    }

    private func consume(_ char: Unicode.Scalar) -> Bool {
        guard position < text.count && text[position] == char else { return false }
        position += 1
        return true
    }

    private func require(_ char: Unicode.Scalar) throws {
        spaces()
        guard consume(char) else { throw error("syntax", "Expected '\(char)'.") }
        spaces()
    }

    private func range(_ start: Int) -> ConformanceSourceRange { line.range(offset + start, max(0, position - start)) }

    private func error(_ code: String, _ message: String, _ start: Int? = nil) -> ConformanceParseError { owner.failure(code, message, range(start ?? position)) }
}
