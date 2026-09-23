// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import TerminalSupport
import XCTest

@testable import GameEventScriptTool

final class TerminalTests: XCTestCase {
    func testExtendedEnterAndPreservationAtDecoderBoundary() throws {
        for sequence in ["\u{1b}[27;2;13~", "\u{1b}[13;2u", "\u{1b}[27;3;13~", "\u{1b}[13;3u", "\u{1b}\r", "\u{1b}\n", "\u{0e}"] {
            var bytes = Array(sequence.utf8).map(Int32.init) + [65]
            let keys = TerminalKeys(read: { _ in bytes.isEmpty ? -2 : bytes.removeFirst() })
            XCTAssertEqual(try keys.next(), .newline, sequence)
            XCTAssertEqual(try keys.next(), .text("A"))
        }
        for sequence in ["\u{1b}", "\u{1b}[27;2;", "\u{1b}[Z"] {
            var bytes = Array(sequence.utf8).map(Int32.init)
            XCTAssertEqual(try TerminalKeys(read: { _ in bytes.isEmpty ? -2 : bytes.removeFirst() }).next(), .unrecognized(Array(sequence.utf8)))
        }
    }

    func testNormalAndApplicationCursorKeysPreserveFollowingInput() throws {
        let directions: [(String, TerminalKey)] = [("A", .up), ("B", .down), ("C", .right), ("D", .left)]
        for prefix in ["\u{1b}[", "\u{1b}O"] {
            for (suffix, expected) in directions {
                var bytes = Array((prefix + suffix + "x").utf8).map(Int32.init)
                let keys = TerminalKeys(read: { _ in bytes.isEmpty ? -1 : bytes.removeFirst() })
                XCTAssertEqual(try keys.next(), expected)
                XCTAssertEqual(try keys.next(), .text("x"))
                XCTAssertEqual(try keys.next(), .eof)
            }
        }
    }

    func testPasteIsOneEditableInputAndUnicodeIsPreserved() throws {
        let payload = "emit ConsoleOut(\"Grüße 👩‍💻\")\r\nemit ConsoleOut(12)"
        var bytes = Array(("\u{1b}[200~" + payload + "\u{1b}[201~").utf8).map(Int32.init)
        let keys = TerminalKeys(read: { _ in bytes.isEmpty ? -1 : bytes.removeFirst() })
        XCTAssertEqual(try keys.next(), .paste(payload.replacingOccurrences(of: "\r\n", with: "\n")))
        XCTAssertEqual(try keys.next(), .eof)
        bytes = Array("ü".utf8).map(Int32.init)
        XCTAssertEqual(try keys.next(), .text("ü"))
        bytes = [0xff]
        XCTAssertThrowsError(try keys.next())
    }

    func testHighlightingReusesGrammarsAndProtectsStringsComments() {
        let highlighter = Highlighting()
        XCTAssertEqual(highlighter.render("42"), "\u{1b}[34m42\u{1b}[0m")
        XCTAssertEqual(highlighter.render("\"emit 42\""), "\u{1b}[32m\"emit 42\"\u{1b}[0m")
        XCTAssertEqual(highlighter.render("// emit 42"), "\u{1b}[90m// emit 42\u{1b}[0m")
        XCTAssertTrue(highlighter.render("emit ConsoleOut(12)").contains("\u{1b}[35memit"))
        let assembly = ".segment code\nLoadInteger r0, #42\n.segment source \"x.ges\"\non Main(args) { emit ConsoleOut(12) }\n.region-end \"source\"\n"
        let rendered = highlighter.renderAssembly(assembly)
        XCTAssertTrue(rendered.contains("\u{1b}[34m#42"), rendered)
        XCTAssertTrue(rendered.contains("\u{1b}[35memit"), rendered)
        let regex = try! NSRegularExpression(pattern: "\u{1b}\\[[0-9;]*m")
        XCTAssertEqual(regex.stringByReplacingMatches(in: rendered, range: NSRange(rendered.startIndex..., in: rendered), withTemplate: ""), assembly)
    }

    func testMultilineStringDoesNotEndEmbeddedSourceAndTabsUseDisplayWidth() {
        ges_terminal_initialize()
        let source = ".segment source \"x\"\nemit ConsoleOut(\"first\n.segment code\nstill text\")\n.region-end \"source\"\n"
        let rendered = Highlighting().renderAssembly(source)
        XCTAssertTrue(rendered.contains(".segment code\nstill text"), rendered)
        XCTAssertEqual(TextDisplay.expandTabs("a\tb\t\n中\te\u{301}\t👩‍💻\tz"), "a   b   \n中  e\u{301}   👩‍💻  z")
    }
}
