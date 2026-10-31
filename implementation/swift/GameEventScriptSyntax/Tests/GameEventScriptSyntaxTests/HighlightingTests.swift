// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptSyntax
import XCTest

final class HighlightingTests: XCTestCase {
    func testSharedMarkdownCasesMatchKindsAndDiagnostics() throws {
        var root = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        while !FileManager.default.fileExists(atPath: root.appendingPathComponent("conformance/highlighting/SyntaxHighlighting.md").path) {
            guard root.path != "/" else { return XCTFail("Repository root missing") }
            root.deleteLastPathComponent()
        }
        let markdown = try String(contentsOf: root.appendingPathComponent("conformance/highlighting/SyntaxHighlighting.md"), encoding: .utf8) as NSString
        let regex = try NSRegularExpression(pattern: "```highlight\\s*\\n(.*?)\\n```", options: [.dotMatchesLineSeparators])
        let cases = regex.matches(in: markdown as String, range: NSRange(location: 0, length: markdown.length))
        XCTAssertGreaterThanOrEqual(cases.count, 46)
        for match in cases {
            let data = try JSONSerialization.jsonObject(with: Data(markdown.substring(with: match.range(at: 1)).utf8)) as! [String: Any]
            let name = data["name"] as! String
            let source = data["source"] as! String
            let length = (source as NSString).length
            let result = try GesSyntaxHighlighter(language: GesSyntaxLanguage(rawValue: data["language"] as! String)!).highlight(source)
            XCTAssertTrue(result.isComplete, name)
            guard result.isComplete else { continue }
            let units = expand(result.spans, length: length)
            for expected in data["expect"] as! [[String: Any]] {
                guard let range = select(source, expected, name) else { continue }
                for unit in units[range] { XCTAssertEqual(unit.kind.rawValue, expected["kind"] as? String, name + ": " + (expected["text"] as! String)) }
            }

            if let expected = data["diagnostics"] as? [[String: Any]] {
                XCTAssertEqual(result.diagnostics.count, expected.count, name)
                for (diagnostic, expected) in zip(result.diagnostics, expected) {
                    guard let range = select(source, expected, name) else { continue }
                    XCTAssertEqual(diagnostic.code, expected["code"] as? String, name)
                    XCTAssertEqual(diagnostic.start, range.lowerBound, name)
                    XCTAssertEqual(diagnostic.length, range.count, name)
                }
            }

            for span in result.spans { XCTAssertEqual(span.diagnostics, result.diagnostics.filter { overlaps($0, span, length) }, name) }
            let ansi = try GesAnsiRenderer.render(source, spans: result.spans)
            XCTAssertEqual(ansi.replacingOccurrences(of: "\u{1b}\\[[0-9;]*m", with: "", options: .regularExpression), source, name)
        }
    }

    func testDiagnosticsWithoutLengthBelongToTheSpanAtTheirPosition() throws {
        let result = try GesSyntaxHighlighter().highlight("on Start() {")
        XCTAssertFalse(result.diagnostics.isEmpty)
        XCTAssertTrue(result.diagnostics.allSatisfy { $0.start == 12 && $0.length == 0 })
        XCTAssertEqual(result.spans.last?.diagnostics, result.diagnostics)
        XCTAssertTrue(result.spans.dropLast().allSatisfy { $0.diagnostics.isEmpty })
    }

    func testBoundsAndInvalidApiInputsAreExplicit() throws {
        let small = try GesSyntaxHighlighter(maxInputLength: 4)
        XCTAssertFalse(small.highlight("12345").isComplete)
        XCTAssertTrue(small.highlight("12345").spans.isEmpty)
        XCTAssertTrue(small.highlight("12345").diagnostics.isEmpty)
        XCTAssertTrue(small.highlight("").isComplete)
        XCTAssertTrue(small.highlight("").spans.isEmpty)
        XCTAssertThrowsError(try GesSyntaxHighlighter(maxInputLength: 0))
        let spans = small.highlight("1234").spans
        XCTAssertThrowsError(try GesAnsiRenderer.render("1", spans: spans))
        XCTAssertThrowsError(try GesAnsiRenderer.render("1234", spans: spans) { _ in 123 })
        XCTAssertEqual(try GesAnsiRenderer.render("1234", spans: spans) { _ in 31 }, "\u{1b}[31m1234\u{1b}[0m")
    }

    // The UTF-16 range a case selects by text, occurrence and the optional `after` and `length`.
    private func select(_ source: String, _ expected: [String: Any], _ name: String) -> Range<Int>? {
        let text = source as NSString
        let snippet = expected["text"] as! String
        var start = 0
        var range = NSRange(location: NSNotFound, length: 0)
        for _ in 0...(expected["occurrence"] as? Int ?? 0) {
            range = text.range(of: snippet, options: [.literal], range: NSRange(location: start, length: text.length - start))
            if range.location == NSNotFound { break }
            start = range.location + 1
        }
        XCTAssertNotEqual(range.location, NSNotFound, name + ": " + snippet)
        guard range.location != NSNotFound else { return nil }
        let lower = expected["after"] as? Bool == true ? NSMaxRange(range) : range.location
        return lower..<(lower + (expected["length"] as? Int ?? range.length))
    }

    private func overlaps(_ diagnostic: GesHighlightDiagnostic, _ span: GesHighlightSpan, _ length: Int) -> Bool {
        let start = min(diagnostic.start, max(length - 1, 0))
        let end = max(diagnostic.start + diagnostic.length, start + 1)
        return start < span.start + span.length && span.start < end
    }

    private func expand(_ spans: [GesHighlightSpan], length: Int) -> [GesHighlightSpan] {
        var units: [GesHighlightSpan] = []
        for span in spans {
            XCTAssertEqual(span.start, units.count)
            XCTAssertGreaterThan(span.length, 0)
            units += Array(repeating: span, count: span.length)
        }
        XCTAssertEqual(units.count, length)
        return units
    }
}
