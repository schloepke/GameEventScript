// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptSyntax

extension Tool {
    private struct Mark {
        let line: Int
        let column: Int
        let length: Int
        let code: String
        let message: String
    }

    // `:show <file>` displays a GES source, GESA or GES document file and marks its errors below the text, without
    // loading it.
    func writeShow(_ path: String, color: Bool) throws {
        let text = try ToolFiles.source(path)
        let lower = path.lowercased()
        let language: GesSyntaxLanguage = lower.hasSuffix(".gesd") ? .gesd : lower.hasSuffix(".gesa") ? .gesa : .ges
        let highlighter = try GesSyntaxHighlighter(language: language, maxInputLength: Int.max)
        let result = try highlighter.highlight(text)
        // Columns do not count a leading byte order mark.
        let lines = Self.lines(text.hasPrefix("\u{feff}") ? String(text.unicodeScalars.dropFirst()) : text)
        let units = Array(text.utf16)
        var marks: [Mark] = []
        for diagnostic in result.diagnostics {
            let (line, column) = Self.location(units, diagnostic.start)
            marks.append(Mark(line: line, column: column, length: max(1, Self.scalarCount(units, diagnostic.start, diagnostic.length)), code: diagnostic.code, message: diagnostic.message))
        }
        // A document read without error can still fail to construct an explicit form.
        if language == .gesd && marks.isEmpty, let failure = try ToolDocuments.read(text).1 {
            marks.append(Mark(line: failure.line, column: failure.column, length: Self.formLength(lines, failure.line, failure.column), code: failure.code, message: failure.message))
        }

        io.line(toError: true)
        let header = "// File: " + quoted(path)
        io.line(color ? RunHighlighting.paint(header, 90) : header, toError: true)
        let expandsTabs = io.errorTerminal && text.utf8.contains(9)
        let display = expandsTabs ? ToolTextDisplay.expandTabs(text) : text
        if color {
            // An expanded display is highlighted again with the same highlighter, so tabs never change its colors.
            let spans = expandsTabs ? try highlighter.highlight(display).spans : result.spans
            io.write(try GesAnsiRenderer.render(display, spans: spans), toError: true)
        } else {
            io.write(display, toError: true)
        }
        if !text.isEmpty && text.utf8.last != 10 && text.utf8.last != 13 { io.line(toError: true) }
        for mark in marks {
            let report = "\(path)(\(mark.line),\(mark.column)): error \(mark.code): \(mark.message)"
            io.write((color ? RunHighlighting.paint(report, 31) : report) + "\n", toError: true)
            let source = mark.line <= lines.count ? lines[mark.line - 1] : ""
            io.line("  " + ToolTextDisplay.expandTabs(source), toError: true)
            let scalars = Array(source.unicodeScalars)
            let start = min(mark.column - 1, scalars.count)
            // A range that continues on later lines is marked to the end of its first line, in expanded columns.
            let end = min(scalars.count, start + mark.length)
            let prefix = ToolTextDisplay.expandTabs(String(String.UnicodeScalarView(scalars[..<start])))
            let through = ToolTextDisplay.expandTabs(String(String.UnicodeScalarView(scalars[..<end])))
            let caret = String(repeating: "^", count: max(1, through.unicodeScalars.count - prefix.unicodeScalars.count))
            io.line("  " + String(repeating: " ", count: prefix.unicodeScalars.count) + (color ? RunHighlighting.paint(caret, 31) : caret), toError: true)
        }
        io.line(toError: true)
    }

    private static func lines(_ text: String) -> [String] {
        var result: [String] = []
        var current = String.UnicodeScalarView()
        var previousCR = false
        for scalar in text.unicodeScalars {
            if scalar == "\n" && previousCR {
                previousCR = false
                continue
            }
            previousCR = scalar == "\r"
            if scalar == "\n" || scalar == "\r" {
                result.append(String(current))
                current = String.UnicodeScalarView()
            } else {
                current.append(scalar)
            }
        }
        result.append(String(current))
        return result
    }

    // The 1-based line and column of a UTF-16 offset, in Unicode scalars; CRLF is one line break and a leading byte
    // order mark is not counted, as in document errors.
    private static func location(_ units: [UInt16], _ offset: Int) -> (Int, Int) {
        var line = 1
        var column = 1
        var index = units.first == 0xFEFF ? 1 : 0
        while index < offset && index < units.count {
            let unit = units[index]
            if unit == 13 || unit == 10 {
                if unit == 13 && index + 1 < units.count && units[index + 1] == 10 { index += 1 }
                line += 1
                column = 1
            } else if !(0xDC00...0xDFFF).contains(unit) {
                column += 1
            }
            index += 1
        }
        return (line, column)
    }

    private static func scalarCount(_ units: [UInt16], _ start: Int, _ length: Int) -> Int {
        var count = 0
        var index = start
        while index < start + length && index < units.count {
            if !(0xDC00...0xDFFF).contains(units[index]) && units[index] != 13 && units[index] != 10 { count += 1 }
            index += 1
        }
        return count
    }

    // A construction error points at the `:` of its form; the mark covers `:Name`.
    private static func formLength(_ lines: [String], _ line: Int, _ column: Int) -> Int {
        guard line <= lines.count else { return 1 }
        let scalars = Array(lines[line - 1].unicodeScalars.dropFirst(column - 1))
        guard scalars.first == ":" else { return 1 }
        var end = 1
        while end < scalars.count && (("a"..."z").contains(scalars[end]) || ("A"..."Z").contains(scalars[end]) || ("0"..."9").contains(scalars[end]) || scalars[end] == "_") { end += 1 }
        return end
    }
}
