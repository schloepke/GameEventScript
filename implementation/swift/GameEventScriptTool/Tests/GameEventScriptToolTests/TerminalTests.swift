// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import TerminalSupport
import XCTest

@testable import GameEventScriptTool

final class TerminalTests: XCTestCase {
    // The widths match the C# CLI per grapheme cluster.
    func testClusterWidthsAndTabColumns() {
        let clusters: [(Character, Int)] = [
            ("a", 1), ("e\u{301}", 1), ("\u{65E5}", 2), ("\u{231A}", 2), ("\u{2764}\u{FE0F}", 2), ("\u{1F1E6}", 2), ("\u{1F44D}\u{1F3FD}", 2), ("\u{1F1E9}\u{1F1EA}", 2), ("\u{1F468}\u{200D}\u{1F469}\u{200D}\u{1F467}", 2), ("\u{1D400}", 1),
            ("\u{65E5}\u{E0100}", 2), ("\u{30000}", 2), ("\u{200B}", 0), ("\u{915}\u{94D}\u{200D}\u{937}", 2),
        ]
        for (cluster, width) in clusters { XCTAssertEqual(ToolTextDisplay.width(cluster), width, String(cluster)) }
        XCTAssertEqual(ToolTextDisplay.columns("a\tb"), 5)
        XCTAssertEqual(ToolTextDisplay.columns("\u{65E5}\u{2764}\u{FE0F}\tb"), 9)
        XCTAssertEqual(ToolTextDisplay.expandTabs("\u{65E5}\u{2764}\u{FE0F}\tb"), "\u{65E5}\u{2764}\u{FE0F}    b")
    }

    // The generated table must keep every Emoji_Presentation scalar of this Swift's Unicode data wide.
    func testGeneratedWidthTableCoversEmojiPresentation() {
        XCTAssertEqual(ToolTextDisplay.scalarWidth(0), 0)
        for value in UInt32(0)...0x10FFFF {
            guard let scalar = Unicode.Scalar(value), scalar.properties.isEmojiPresentation else { continue }
            XCTAssertEqual(ToolTextDisplay.scalarWidth(value), 2, String(value, radix: 16))
        }
    }

    // The C# CLI's cluster data must match this Swift's segmentation: an attacher extends a consonant, a prepend joins a
    // following letter, a control does not take a following combining acute, a linker joins two consonants, a consonant
    // joins a preceding consonant and virama without extending a lone consonant, and an extender keeps the conjunct
    // joined before and after the virama. On macOS the segmentation data belongs to the OS's Swift runtime, whose
    // Unicode version may differ from the generated data in assignments and properties alike, so other versions skip.
    func testClusterTablesMatchNativeSegmentation() throws {
        var newest = (major: 0, minor: 0)
        for value in UInt32(0)...0x10FFFF {
            if let age = Unicode.Scalar(value)?.properties.age, age > newest { newest = age }
        }
        let runtime = "\(newest.major).\(newest.minor).0"
        guard runtime == ToolTextClusters.unicodeVersion else {
            throw XCTSkip("The Swift runtime segments with Unicode \(runtime), the cluster data is from Unicode \(ToolTextClusters.unicodeVersion).")
        }
        let ka = "\u{915}"
        let virama = "\u{94D}"
        let ssa = "\u{937}"
        var derived: [String: [UInt32]] = [:]
        for value in UInt32(0)...0x10FFFF {
            guard let scalar = Unicode.Scalar(value) else { continue }
            let text = String(scalar)
            if (text + "\u{301}").count == 2 { derived["clusterControls", default: []].append(value) }
            if (text + "a").count == 1 { derived["clusterPrepends", default: []].append(value) }
            if (ka + text).count == 1 {
                derived["clusterAttachers", default: []].append(value)
                if (ka + text + ka).count == 1 {
                    derived["conjunctLinkers", default: []].append(value)
                } else if (ka + text + virama + ssa).count == 1 {
                    XCTAssertEqual((ka + virama + text + ssa).count, 1, String(value, radix: 16))
                    derived["conjunctExtenders", default: []].append(value)
                }
            } else if scalar.properties.generalCategory == .otherLetter && (ka + virama + text).count == 1 {
                derived["conjunctConsonants", default: []].append(value)
            }
        }

        func ranges(_ values: [UInt32]) -> [UInt32] {
            var result: [UInt32] = []
            for value in values {
                if let last = result.last, last == value - 1 { result[result.count - 1] = value } else { result += [value, value] }
            }
            return result
        }

        XCTAssertEqual(ranges(derived["clusterAttachers", default: []]), ToolTextClusters.clusterAttachers)
        XCTAssertEqual(ranges(derived["clusterPrepends", default: []]), ToolTextClusters.clusterPrepends)
        XCTAssertEqual(ranges(derived["clusterControls", default: []]), ToolTextClusters.clusterControls)
        XCTAssertEqual(derived["conjunctLinkers", default: []], ToolTextClusters.conjunctLinkers)
        XCTAssertEqual(derived["conjunctConsonants", default: []], ToolTextClusters.conjunctConsonants)
        XCTAssertEqual(ranges(derived["conjunctExtenders", default: []]), ToolTextClusters.conjunctExtenders)
        XCTAssertFalse(derived["conjunctExtenders", default: []].contains(0x200c))
    }

    // Tab spaces wrap individually and wide characters move whole, like the C# waiting input.
    func testPromptPositions() {
        let tab = Array("abcd\tx")
        XCTAssertTrue(RunPrompt.position(tab[...], columns: 10) == (1, 4))
        XCTAssertTrue(RunPrompt.position(tab[..<5], columns: 10) == (1, 3))
        let wide = Array("abcd\u{65E5}")
        XCTAssertTrue(RunPrompt.position(wide[...], columns: 10) == (1, 2))
        XCTAssertTrue(RunPrompt.position(wide[..<4], columns: 10) == (0, 9))
        XCTAssertTrue(RunPrompt.position(Array("a\nb")[...], columns: 10) == (1, 6))
    }

    func testExtendedEnterAndPreservationAtDecoderBoundary() throws {
        for sequence in ["\u{1b}[27;2;13~", "\u{1b}[13;2u", "\u{1b}[27;3;13~", "\u{1b}[13;3u", "\u{1b}\r", "\u{1b}\n", "\u{0e}"] {
            var bytes = Array(sequence.utf8).map(Int32.init) + [65]
            let keys = RunKeyReader(read: { _ in bytes.isEmpty ? -2 : bytes.removeFirst() })
            XCTAssertEqual(try keys.next(), .newline, sequence)
            XCTAssertEqual(try keys.next(), .text("A"))
        }
        for sequence in ["\u{1b}", "\u{1b}[27;2;", "\u{1b}[Z"] {
            var bytes = Array(sequence.utf8).map(Int32.init)
            XCTAssertEqual(try RunKeyReader(read: { _ in bytes.isEmpty ? -2 : bytes.removeFirst() }).next(), .unrecognized(Array(sequence.utf8)))
        }
    }

    func testNormalAndApplicationCursorKeysPreserveFollowingInput() throws {
        let directions: [(String, TerminalKey)] = [("A", .up), ("B", .down), ("C", .right), ("D", .left)]
        for prefix in ["\u{1b}[", "\u{1b}O"] {
            for (suffix, expected) in directions {
                var bytes = Array((prefix + suffix + "x").utf8).map(Int32.init)
                let keys = RunKeyReader(read: { _ in bytes.isEmpty ? -1 : bytes.removeFirst() })
                XCTAssertEqual(try keys.next(), expected)
                XCTAssertEqual(try keys.next(), .text("x"))
                XCTAssertEqual(try keys.next(), .eof)
            }
        }
    }

    func testPasteIsOneEditableInputAndUnicodeIsPreserved() throws {
        let payload = "emit ConsoleOut(\"Grüße 👩‍💻\")\r\nemit ConsoleOut(12)"
        var bytes = Array(("\u{1b}[200~" + payload + "\u{1b}[201~").utf8).map(Int32.init)
        let keys = RunKeyReader(read: { _ in bytes.isEmpty ? -1 : bytes.removeFirst() })
        XCTAssertEqual(try keys.next(), .paste(payload.replacingOccurrences(of: "\r\n", with: "\n")))
        XCTAssertEqual(try keys.next(), .eof)
        bytes = Array("ü".utf8).map(Int32.init)
        XCTAssertEqual(try keys.next(), .text("ü"))
        bytes = [0xff]
        XCTAssertThrowsError(try keys.next())
    }

    func testHighlightingReusesGrammarsAndProtectsStringsComments() {
        let highlighter = RunHighlighting()
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
        let rendered = RunHighlighting().renderAssembly(source)
        XCTAssertTrue(rendered.contains(".segment code\nstill text"), rendered)
        XCTAssertEqual(ToolTextDisplay.expandTabs("a\tb\t\n中\te\u{301}\t👩‍💻\tz"), "a   b   \n中  e\u{301}   👩‍💻  z")
    }
}
