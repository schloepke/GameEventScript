// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime
import XCTest

/// Verifies the document check and format commands and the console's :show, like the C# CLI tests.
final class DocumentTests: XCTestCase {
    private let valid = ":Schema(:DriveUnit)\n// Engine\nid: 'drive'\nmass: 550kg, ready:\nmount: :Vector(x: 0m, y: -0.8m)\n"
    // swift-corelibs-xctest on Linux creates a test case only from a name and a closure.
    #if canImport(Darwin)
        private let tool = ToolTests()
    #else
        private let tool = ToolTests(name: "DocumentTests.tool", testClosure: { _ in })
    #endif

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

    // A pinned reference in an unpinned handle field breaks the schema, which reading alone accepts.
    func testCheckWithSchemasChecksEachDocumentAgainstItsHeader() throws {
        let state = try tool.file("state.gess", "schema :Composition as { name: :Text max 32 }\nhandle @Unit of :Composition with id :Text max 64 versioned by hash(#ges)\n")
        let army = try tool.file("army.gess", "schema :Army as { parts: :List(@Unit) max 4 }\n")
        let pinned = try tool.file("pinned.gesd", ":Schema(:Army)\nparts: [@Unit:x~#live]\n")
        let good = try tool.file("army.gesd", ":Schema(:Army)\nparts: [@Unit:x, @Unit:y]\n")
        let plain = try tool.file("plain.gesd", "parts: [@Unit:x~#live]\n")
        let file = try tool.file("sub/file.gesd", ":Schema('../army.gess')\nparts: [@Unit:x]\n")
        let filePinned = try tool.file("sub/pinned.gesd", ":Schema('../army.gess')\nparts: [@Unit:x~#live]\n")
        let other = try tool.file("other.gesd", ":Schema('other.gess')\nparts: [@Unit:x]\n")
        let unknown = try tool.file("unknown.gesd", ":Schema(:Navy)\nparts: [@Unit:x]\n")

        // Without --schema only reading is checked.
        let read = tool.run(["document", "check", pinned, other, unknown])
        XCTAssertEqual(read.code, 0, read.error)
        XCTAssertEqual(read.output, "Checked 3 documents.\n")

        let valid = tool.run(["document", "check", good, plain, file, "--schema", state, "--schema", army])
        XCTAssertEqual(valid.code, 0, valid.error)
        XCTAssertEqual(valid.output, "Checked 3 documents.\n")

        let pattern = tool.directory.appendingPathComponent("*.gess").path
        let invalid = tool.run(["document", "check", pinned, filePinned, other, unknown, good, "--schema", pattern])
        XCTAssertEqual(invalid.code, 1)
        XCTAssertEqual(invalid.output, "")
        XCTAssertEqual(
            invalid.error,
            """
            \(pinned)(2,9): error document.unexpectedVersion: The document breaks its schema (document.unexpectedVersion) at line 2, column 9.
            \(filePinned)(2,9): error document.unexpectedVersion: The document breaks its schema (document.unexpectedVersion) at line 2, column 9.
            \(other): error schema.unknownName: The schema file 'other.gess' is none of the files given with --schema.
            \(unknown): error schema.unknownName: ':Navy' is not declared.

            """
        )

        // A schema error stops before any document, as for compile.
        let broken = try tool.file("broken.gess", "schema :Broken { }\n")
        let failed = tool.run(["document", "check", good, "--schema", broken])
        XCTAssertEqual(failed.code, 1)
        XCTAssertTrue(failed.error.hasPrefix("\(broken)(1,16): error schema.syntax:"), failed.error)
        XCTAssertEqual(failed.output, "")
    }

    // Each store is read after the ones before it, every error is reported with its file and position, and a valid
    // store reports its entry count.
    func testCheckWithStoresReadsTheStoresInOrder() throws {
        let schemas = try tool.file(
            "units.gess",
            """
            schema :Hull as { armor: :Number integer between 0 and 100 }
            handle @Hull of :Hull with id :Text max 32 versioned by hash(#ges)
            schema :Composition as { name: :Text max 64, hull: @Hull~, escort: @Unit optional }
            handle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)
            handle @Slot of :Composition attached to @Unit with part :Number integer between 0 and 15

            """
        )
        let hulls = try tool.file("content/hulls.gesd", ":Store(@Hull)\nlight: [armor: 20]\nheavy: [armor: 80]\n")
        let units = try tool.file("content/units.gesd", ":Store(@Unit)\nimport 'drones/all.gesd'\nscout: [name: 'Scout', hull: @Hull:light~]\n")
        let drones = try tool.file("content/drones/all.gesd", ":Store(@Unit)\nmodule drone {\n  gnabblo: [name: 'Gnabblo', hull: @Hull:heavy~, escort: @scout]\n}\n")
        let stores = ["--store", "@Hull", hulls, "--store", "@Unit", units]

        let valid = tool.run(["document", "check", "--schema", schemas] + stores)
        XCTAssertEqual(valid.code, 0, valid.error)
        XCTAssertEqual(valid.output, "Checked store @Hull: 2 entries.\nChecked store @Unit: 2 entries.\n")
        XCTAssertEqual(tool.run(["document", "check", "--schema", schemas, "-q"] + stores).output, "")

        _ = try tool.file("content/drones/all.gesd", ":Store(@Unit)\nmodule drone {\n  gnabblo: [name: 'Gnabblo', hull: @Hull:hevy~, escort: @scout]\n}\n")
        let invalid = tool.run(["document", "check", "--schema", schemas] + stores)
        XCTAssertEqual(invalid.code, 1)
        XCTAssertEqual(invalid.output, "Checked store @Hull: 2 entries.\n")
        XCTAssertEqual(invalid.error, "\(drones)(3,36): error store.unknownReference: @Hull:hevy names no entry. Did you mean @Hull:heavy?\n")

        let unknown = tool.run(["document", "check", "--schema", schemas, "--store", "@Ship", hulls])
        XCTAssertEqual(unknown.code, 1)
        XCTAssertEqual(unknown.error, "\(hulls): error schema.unknownName: The schemas of --schema declare no handle type @Ship.\n")

        let attached = tool.run(["document", "check", "--schema", schemas, "--store", "@Slot", hulls])
        XCTAssertEqual(attached.code, 1)
        XCTAssertEqual(attached.error, "\(hulls): error cli.usage: @Slot has a root; a store needs a type of its own. (Parameter 'type')\n")

        let twice = tool.run(["document", "check", "--schema", schemas, "--store", "@Hull", hulls, "--store", "@Hull", hulls])
        XCTAssertEqual(twice.code, 2)
        XCTAssertEqual(twice.error, "error cli.usage: The root '\(hulls)' of @Hull is given twice. Run 'ges document --help' for usage.\n")
    }

    func testUsageErrorsExitWithTwo() throws {
        let a = try tool.file("a.gesd", valid)
        let b = try tool.file("b.gesd", valid)
        for arguments in [
            ["document"], ["document", "lint", a], ["document", "check"], ["document", "format", a, b], ["document", "format", a, "-o", a], ["document", "check", "--color", a], ["document", "check", ""], ["document", "format", "", "-o", "out.gesd"],
            ["document", "check", a, "--schema"], ["document", "check", a, "--schema", "-q"], ["document", "format", a, "--schema", "units.gess"],
            ["document", "check", "--store", "@Unit", a], ["document", "check", "--schema", "a.gess", "--store", "Unit", a], ["document", "check", "--schema", "a.gess", "--store", "@Unit"],
            ["document", "format", a, "--store", "@Unit", b],
        ] {
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
