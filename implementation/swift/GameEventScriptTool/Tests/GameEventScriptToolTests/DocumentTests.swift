// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime
import XCTest

/// Verifies the document check and format commands and the console's :show, like the C# CLI tests.
final class DocumentTests: XCTestCase {
    private let valid = ":Schema(:DriveUnit)\n// Engine\nid: 'drive'\nmass: 550kg, ready:\nmount: :Vector(x: 0m, y: -0.8m)\n"
    private let tool = ToolTests()

    override func setUpWithError() throws { try tool.setUpWithError() }

    override func tearDownWithError() throws { try tool.tearDownWithError() }

    func testCheckReportsEachInvalidDocument() throws {
        let good = try tool.file("valid.gesd", valid)
        let text = try tool.file("text.gesd", "a: 1\nname: Basic Track\n")
        let form = try tool.file("form.gesd", "range: :Range(1)\n")
        let checked = tool.run(["document", "check", good])
        XCTAssertEqual(checked.code, 0, checked.error)
        XCTAssertEqual(checked.output, "Checked \(good).\n")
        XCTAssertEqual(tool.run(["document", "check", "-q", good]).output, "")

        let invalid = tool.run(["document", "check", good, text, form])
        XCTAssertEqual(invalid.code, 1)
        XCTAssertEqual(invalid.output, "")
        XCTAssertEqual(
            invalid.error,
            "\(text)(2,7): error document.unquotedText: Text values must be quoted.\n\(form)(1,8): error document.invalidLiteral: This is not a valid literal.\n"
        )

        // An unreadable file does not stop the check of the remaining files.
        let missing = tool.directory.appendingPathComponent("missing.gesd").path
        let partial = tool.run(["document", "check", missing, text])
        XCTAssertEqual(partial.code, 1)
        XCTAssertTrue(partial.error.contains("error cli.io:"), partial.error)
        XCTAssertTrue(partial.error.hasSuffix("\(text)(2,7): error document.unquotedText: Text values must be quoted.\n"), partial.error)
        let binary = tool.directory.appendingPathComponent("binary.gesd")
        try Data([0x61, 0x3A, 0x20, 0xFF, 0x0A]).write(to: binary)
        let encoding = tool.run(["document", "check", binary.path, text])
        XCTAssertEqual(encoding.code, 1)
        XCTAssertTrue(encoding.error.hasPrefix("\(binary.path): error cli.invalidEncoding:"), encoding.error)
        XCTAssertTrue(encoding.error.hasSuffix("\(text)(2,7): error document.unquotedText: Text values must be quoted.\n"), encoding.error)
    }

    func testFormatPrintsAndWritesTheRecommendedLayout() throws {
        let path = try tool.file("valid.gesd", valid)
        let expected = try GesDocument(parsing: valid).text()
        let printed = tool.run(["document", "format", path])
        XCTAssertEqual(printed.code, 0, printed.error)
        XCTAssertEqual(printed.output, expected)
        XCTAssertEqual(try GesDocument(parsing: printed.output), try GesDocument(parsing: valid))
        let destination = tool.directory.appendingPathComponent("out/formatted.gesd").path
        XCTAssertEqual(tool.run(["document", "format", path, "-o", destination]).code, 0)
        XCTAssertEqual(try String(contentsOfFile: destination, encoding: .utf8), expected)
    }

    func testFormatOfAnInvalidDocumentKeepsTheOutput() throws {
        let bad = try tool.file("bad.gesd", "a: [1, 2\n")
        let previous = try tool.file("previous.gesd", "previous")
        let result = tool.run(["document", "format", bad, "-o", previous])
        XCTAssertEqual(result.code, 1)
        XCTAssertEqual(result.error, "\(bad)(1,4): error document.unclosedBracket: This bracket is never closed.\n")
        XCTAssertEqual(try String(contentsOfFile: previous, encoding: .utf8), "previous")
    }

    func testUsageErrorsExitWithTwo() throws {
        let a = try tool.file("a.gesd", valid)
        let b = try tool.file("b.gesd", valid)
        for arguments in [["document"], ["document", "lint", a], ["document", "check"], ["document", "format", a, b], ["document", "format", a, "-o", a], ["document", "check", "--color", a]] {
            let result = tool.run(arguments)
            XCTAssertEqual(result.code, 2, "\(arguments) \(result.error)")
            if arguments.count > 1 { XCTAssertTrue(result.error.contains("cli.usage"), result.error) }
        }
    }

    func testShowMarksErrorsBelowTheText() throws {
        let bad = try tool.file("bad.gesd", "a: 1\nname: Basic Track\n")
        let form = try tool.file("form.gesd", "range: :Range(1)\n")
        let source = try tool.file("source.ges", "on Start {\n")
        let result = tool.run(["run", "--interactive", "--quiet"], input: [":show \(bad)", ":show \(form)", ":show \(source)", ":quit"])
        XCTAssertEqual(result.code, 0, result.error)
        let expected = [
            "", "// File: \"\(bad)\"", "a: 1", "name: Basic Track",
            "\(bad)(2,7): error document.unquotedText: Text values must be quoted.", "  name: Basic Track", "        ^^^^^", "",
            "", "// File: \"\(form)\"", "range: :Range(1)",
            "\(form)(1,8): error document.invalidLiteral: This is not a valid literal.", "  range: :Range(1)", "         ^^^^^^", "",
            "", "// File: \"\(source)\"", "on Start {", "",
        ].joined(separator: "\n")
        XCTAssertTrue(result.error.hasPrefix(expected), result.error)
        XCTAssertTrue(result.error.contains("\(source)(2,1): error parse.syntax:"), result.error)
        XCTAssertEqual(result.output, "")

        // Tabs and wide characters before the range move the caret by their display width.
        let wide = try tool.file("wide.gesd", "t: ['\u{65E5}\u{672C}', '\u{1F468}\u{200D}\u{1F469}\u{200D}\u{1F467}',\tBasic]\n")
        let shown = tool.run(["run", "--interactive", "--quiet"], input: [":show \(wide)", ":quit"])
        XCTAssertTrue(shown.error.contains("\(wide)(1,20): error document.unquotedText: Text values must be quoted.\n  t: ['\u{65E5}\u{672C}', '\u{1F468}\u{200D}\u{1F469}\u{200D}\u{1F467}',   Basic]\n                      ^^^^^\n"), shown.error)
    }
}
