// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation

/// The fileless, synchronous parser for strict Conformance Markdown V1.
public enum ConformanceMarkdownParser {
    /// Parses and validates a complete Markdown conformance document under resource limits. The byte overload rejects
    /// malformed UTF-8; no partial model is returned.
    ///
    /// - Throws: `ConformanceParseError` with stable code and source range.
    public static func parse(_ text: String, limits: ConformanceParserLimits = .default) throws -> ConformanceDocument { try parse(Array(text.utf8), limits: limits) }

    /// Parses and validates a complete Markdown conformance document under resource limits. The byte overload rejects
    /// malformed UTF-8; no partial model is returned.
    ///
    /// - Throws: `ConformanceParseError` with stable code and source range.
    public static func parse(_ bytes: [UInt8], limits: ConformanceParserLimits = .default) throws -> ConformanceDocument {
        guard limits.maxDocumentBytes > 0, bytes.count <= limits.maxDocumentBytes else { throw ConformanceParseError("conformance.yaml.limitExceeded", "The document exceeds maxDocumentBytes.") }
        let start = bytes.starts(with: [0xef, 0xbb, 0xbf]) ? 3 : 0
        guard !bytes.contains(0), String(bytes: bytes.dropFirst(start), encoding: .utf8) != nil else { throw ConformanceParseError("conformance.markdown.invalidUtf8", "Conformance documents must be strict UTF-8 without NUL bytes.") }
        var lines: [ConformanceLine] = []
        var begin = start
        var index = start
        while index < bytes.count {
            if bytes[index] != 10 && bytes[index] != 13 {
                index += 1
                continue
            }
            lines.append(.init(String(decoding: bytes[begin..<index], as: UTF8.self), number: lines.count + 1, byteOffset: begin - start))
            let ending = bytes[index] == 13 && index + 1 < bytes.count && bytes[index + 1] == 10 ? 2 : 1
            index += ending
            begin = index
        }
        if begin < bytes.count || lines.isEmpty { lines.append(.init(String(decoding: bytes[begin...], as: UTF8.self), number: lines.count + 1, byteOffset: begin - start)) }
        let syntax = try ConformanceMarkdownScanner.scan(lines, limits: limits)
        return try ConformanceSchema.bind(syntax, bytes: bytes, limits: limits)
    }
}

internal struct ConformanceMarkdownBlock {
    let info: String
    let lines: [ConformanceLine]
    let range: ConformanceSourceRange
    let payloadRange: ConformanceSourceRange
    var payload: String { lines.map(\.text).joined(separator: "\n") }
}

internal struct ConformanceMarkdownStep {
    let id: String
    let receive: String
    let pump: String
    let budget: String
    let range: ConformanceSourceRange
}

internal struct ConformanceMarkdownCase {
    let title: String
    var range: ConformanceSourceRange
    var yaml: [ConformanceMarkdownBlock] = []
    var sources: [ConformanceMarkdownBlock] = []
    var assembler: ConformanceMarkdownBlock?
    var steps: [ConformanceMarkdownStep] = []
    var hasSteps = false
    var stepsTableRange: ConformanceSourceRange?
}

internal struct ConformanceMarkdownSyntax {
    let frontmatter: [ConformanceLine]
    let frontmatterRange: ConformanceSourceRange
    let cases: [ConformanceMarkdownCase]
}

internal enum ConformanceMarkdownScanner {
    static func scan(_ lines: [ConformanceLine], limits: ConformanceParserLimits) throws -> ConformanceMarkdownSyntax {
        guard lines.first?.text == "---" else { throw failure("missingFrontmatter", "A document must begin with YAML frontmatter.", lines.first?.range()) }
        guard let close = lines.dropFirst().firstIndex(where: { $0.text == "---" }) else { throw failure("unterminatedFrontmatter", "Frontmatter must end with '---'.", lines[0].range()) }
        var cases: [ConformanceMarkdownCase] = []
        var fixtures = false
        var fixtureSeen = false
        var i = close + 1
        while i < lines.count {
            let line = lines[i]
            if line.text.hasPrefix("```") {
                let info = String(line.text.dropFirst(3))
                let yaml = !fixtures && !cases.isEmpty && info == "yaml"
                let semantic = ["ges", "gesa"].contains(info) || yaml
                let suspicious = info.hasPrefix("ges") || (!fixtures && !cases.isEmpty && info.hasPrefix("yaml "))
                var end = i + 1
                while end < lines.count && lines[end].text != "```" {
                    if semantic && lines[end].text.hasPrefix("```") { throw failure("unknownSemanticFence", "Semantic fences cannot be nested.", lines[end].range()) }
                    end += 1
                }
                if end == lines.count {
                    if semantic || suspicious { throw failure("unterminatedFence", "A recognized fence is not terminated.", line.range()) }
                    i += 1
                    continue
                }
                if !fixtures {
                    if cases.isEmpty && semantic { throw schema("invalidCardinality", "Semantic fences belong inside a test.", line.range()) }
                    if !cases.isEmpty {
                        if !semantic && suspicious { throw failure("unknownSemanticFence", "Unknown semantic fence '\(info)'.", line.range(3)) }
                        let payloadRange = end == i + 1 ? lines[end].range(0, 0) : spanning(lines[i + 1].range(), through: lines[end - 1])
                        let block = ConformanceMarkdownBlock(info: info, lines: Array(lines[(i + 1)..<end]), range: spanning(line.range(), through: lines[end]), payloadRange: payloadRange)
                        if info == "yaml" { cases[cases.count - 1].yaml.append(block) }
                        if info == "ges" { cases[cases.count - 1].sources.append(block) }
                        if info == "gesa" {
                            guard cases[cases.count - 1].assembler == nil else { throw schema("invalidCardinality", "A test accepts at most one gesa block.", line.range()) }
                            cases[cases.count - 1].assembler = block
                        }
                    }
                }
                i = end + 1
                continue
            }
            if line.text.hasPrefix("## Test:") {
                guard line.text.hasPrefix("## Test: ") else { throw failure("invalidTestHeading", "Test headings use '## Test: Display title'.", line.range()) }
                let title = ConformanceYamlParser.trim(String(line.text.dropFirst(9)))
                guard !title.isEmpty else { throw failure("invalidTestHeading", "A test display title cannot be empty.", line.range()) }
                guard limits.maxTests > 0, cases.count < limits.maxTests else { throw ConformanceParseError("conformance.yaml.limitExceeded", "Document exceeds maxTests.", line.range()) }
                if !cases.isEmpty { cases[cases.count - 1].range = spanning(cases[cases.count - 1].range, through: lines[i - 1]) }
                cases.append(.init(title: title, range: line.range()))
                fixtures = false
            } else if line.text == "## Fixtures" {
                guard cases.isEmpty, !fixtureSeen else { throw schema("invalidCardinality", "Fixtures may occur once, before the first test.", line.range()) }
                fixtureSeen = true
                fixtures = true
            } else if !cases.isEmpty && line.text == "### Steps" {
                guard !cases[cases.count - 1].hasSteps else { throw failure("invalidStepsTable", "A test can contain only one Steps table.", line.range()) }
                i += 1
                while i < lines.count && ConformanceYamlParser.trim(lines[i].text).isEmpty { i += 1 }
                guard i + 1 < lines.count, lines[i].text == "| step | receive | pump | budget |", lines[i + 1].text == "| --- | --- | --- | --- |" else {
                    throw failure("invalidStepsTable", "The Steps table header or delimiter is invalid.", line.range())
                }
                i += 2
                while i < lines.count && lines[i].text.hasPrefix("|") {
                    let row = lines[i]
                    guard row.text.hasSuffix("|"), !row.text.contains("\\"), !row.text.contains("`") else { throw failure("invalidStepsTable", "Steps rows require unescaped plain cells.", row.range()) }
                    let cells = row.text.dropFirst().dropLast().split(separator: "|", omittingEmptySubsequences: false).map { ConformanceYamlParser.trim(String($0)) }
                    guard cells.count == 4 else { throw failure("invalidStepsTable", "A Steps row requires four cells.", row.range()) }
                    guard limits.maxStepsPerTest > 0, cases[cases.count - 1].steps.count < limits.maxStepsPerTest else { throw ConformanceParseError("conformance.yaml.limitExceeded", "Steps exceed maxStepsPerTest.", row.range()) }
                    cases[cases.count - 1].steps.append(.init(id: cells[0], receive: cells[1], pump: cells[2], budget: cells[3], range: row.range()))
                    i += 1
                }
                guard !cases[cases.count - 1].steps.isEmpty else { throw failure("invalidStepsTable", "Steps requires at least one row.", line.range()) }
                cases[cases.count - 1].hasSteps = true
                cases[cases.count - 1].stepsTableRange = spanning(line.range(), through: lines[i - 1])
                continue
            }
            i += 1
        }
        guard !cases.isEmpty else { throw schema("invalidCardinality", "A suite requires at least one test.", lines[close].range()) }
        cases[cases.count - 1].range = spanning(cases[cases.count - 1].range, through: lines[lines.count - 1])
        return .init(frontmatter: Array(lines[1..<close]), frontmatterRange: spanning(lines[0].range(), through: lines[close]), cases: cases)
    }

    private static func spanning(_ first: ConformanceSourceRange, through last: ConformanceLine) -> ConformanceSourceRange {
        let lastRange = last.range()
        return .init(byteOffset: first.byteOffset, byteLength: lastRange.byteOffset + lastRange.byteLength - first.byteOffset, line: first.line, column: first.column, endLine: lastRange.endLine, endColumn: lastRange.endColumn)
    }

    private static func failure(_ code: String, _ message: String, _ range: ConformanceSourceRange?) -> ConformanceParseError { .init("conformance.markdown.\(code)", message, range ?? .init()) }

    private static func schema(_ code: String, _ message: String, _ range: ConformanceSourceRange) -> ConformanceParseError { .init("conformance.schema.\(code)", message, range) }
}
