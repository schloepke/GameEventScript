// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptSyntaxHighlighter
import XCTest

final class HighlightingTests: XCTestCase {
    func testSharedMarkdownCasesAndIncrementalScopeParity() throws {
        var root = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        while !FileManager.default.fileExists(atPath: root.appendingPathComponent("conformance/highlighting/SyntaxHighlighting.md").path) {
            guard root.path != "/" else { return XCTFail("Repository root missing") }
            root.deleteLastPathComponent()
        }
        let markdown = try String(contentsOf: root.appendingPathComponent("conformance/highlighting/SyntaxHighlighting.md"), encoding: .utf8) as NSString
        let regex = try NSRegularExpression(pattern: "```highlight\\s*\\n(.*?)\\n```", options: [.dotMatchesLineSeparators])
        let cases = regex.matches(in: markdown as String, range: NSRange(location: 0, length: markdown.length))
        XCTAssertGreaterThanOrEqual(cases.count, 11)
        for match in cases {
            let data = try JSONSerialization.jsonObject(with: Data(markdown.substring(with: match.range(at: 1)).utf8)) as! [String: Any]
            let name = data["name"] as! String
            let source = data["source"] as! String
            let text = source as NSString
            let highlighter = try GameEventScriptSyntaxHighlighter(language: data["language"] as! String == "ges" ? .ges : .gesa)
            let result = highlighter.highlight(source)
            XCTAssertTrue(result.isComplete, name)
            guard result.isComplete else { continue }
            let units = expand(result.spans, length: text.length)
            for expected in data["expect"] as! [[String: Any]] {
                let snippet = expected["text"] as! String
                var start = 0
                var range = NSRange(location: NSNotFound, length: 0)
                for _ in 0...(expected["occurrence"] as? Int ?? 0) {
                    range = text.range(of: snippet, options: [.literal], range: NSRange(location: start, length: text.length - start))
                    if range.location == NSNotFound { break }
                    start = NSMaxRange(range)
                }
                XCTAssertNotEqual(range.location, NSNotFound, name)
                guard range.location != NSNotFound else { continue }
                for unit in units[range.location..<NSMaxRange(range)] {
                    XCTAssertEqual(unit.kind.rawValue, expected["kind"] as? String, name + ": " + snippet)
                    if let scope = expected["scope"] as? String { XCTAssertTrue(unit.scopes.contains(scope), name + ": " + scope) }
                }
            }
            let lines = try NSRegularExpression(pattern: "[^\\r\\n]*(?:\\r\\n|\\r|\\n|$)").matches(in: source, range: NSRange(location: 0, length: text.length))
            var cursor = 0
            var state = highlighter.createState()
            for line in lines {
                if line.range.length == 0 && cursor == text.length { break }
                let incremental = try highlighter.highlightLine(text.substring(with: line.range), state: state)
                XCTAssertTrue(incremental.isComplete, name)
                let parts = expand(incremental.spans, length: line.range.length)
                for (index, part) in parts.enumerated() {
                    XCTAssertEqual(part.kind, units[cursor + index].kind, name)
                    XCTAssertEqual(part.scopes, units[cursor + index].scopes, name)
                }
                cursor += line.range.length
                state = try XCTUnwrap(incremental.nextState)
            }
            XCTAssertEqual(state, result.nextState, name)
            let ansi = try GameEventScriptAnsiRenderer.render(source, spans: result.spans)
            XCTAssertEqual(ansi.replacingOccurrences(of: "\u{1b}\\[[0-9;]*m", with: "", options: .regularExpression), source, name)
        }
    }

    func testEditsChangeContinuationAndStatesWorkAcrossInstances() throws {
        let highlighter = try GameEventScriptSyntaxHighlighter()
        let open = try highlighter.highlightLine("'open\n")
        let inside = try GameEventScriptSyntaxHighlighter().highlightLine("emit Done()", state: open.nextState)
        XCTAssertTrue(inside.spans.allSatisfy { $0.kind == .string })
        let closed = try highlighter.highlightLine("'closed'\n")
        XCTAssertEqual(highlighter.createState(), closed.nextState)
        XCTAssertNotEqual(open.nextState, closed.nextState)
        XCTAssertEqual(try highlighter.highlightLine("emit Done()", state: closed.nextState).spans.first?.kind, .keyword)
    }

    func testLimitsInvalidApiInputsAndCustomPalette() throws {
        let highlighter = try GameEventScriptSyntaxHighlighter(maxInputLength: 4)
        XCTAssertFalse(highlighter.highlight("12345").isComplete)
        XCTAssertNil(highlighter.highlight("12345").nextState)
        XCTAssertTrue(highlighter.highlight("12345").spans.isEmpty)
        XCTAssertTrue(highlighter.highlight("").isComplete)
        XCTAssertThrowsError(try highlighter.highlightLine("x\ny"))
        XCTAssertThrowsError(try highlighter.highlightLine("x", state: GameEventScriptSyntaxHighlighter(language: .gesa).createState()))
        XCTAssertThrowsError(try GameEventScriptSyntaxHighlighter(maxInputLength: 0))
        let spans = highlighter.highlight("1234").spans
        XCTAssertThrowsError(try GameEventScriptAnsiRenderer.render("1", spans: spans))
        XCTAssertThrowsError(try GameEventScriptAnsiRenderer.render("1234", spans: spans, color: { _ in 123 }))
        XCTAssertEqual(try GameEventScriptAnsiRenderer.render("1234", spans: spans, color: { _ in 31 }), "\u{1b}[31m1234\u{1b}[0m")
    }

    private func expand(_ spans: [GameEventScriptHighlightSpan], length: Int) -> [GameEventScriptHighlightSpan] {
        var units: [GameEventScriptHighlightSpan] = []
        for span in spans {
            XCTAssertEqual(span.start, units.count)
            units += Array(repeating: span, count: span.length)
        }
        XCTAssertEqual(units.count, length)
        return units
    }
}
