// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime
import XCTest

@testable import GameEventScriptConformance

/// Bootstraps the binary boundary using the existing Markdown fixture manifest.
/// RuntimeCorpusTests additionally executes the full binary assertions and host steps.
final class ProgramBinaryBootstrapTests: XCTestCase {
    func testBinarySnapshotAssertsDumpAndRequiresCapability() throws {
        var repository = URL(fileURLWithPath: #filePath)
        for _ in 0..<6 { repository.deleteLastPathComponent() }
        let path = repository.appendingPathComponent("conformance/suites/compile/program-dumps.md")
        let markdown = try String(contentsOf: path, encoding: .utf8)
        let document = try ConformanceMarkdownParser.parse(markdown)
        let test = try XCTUnwrap(document.cases.first { $0.id == "float-exponents" })
        XCTAssertEqual(test.requiredCore, ["program-binary"])
        XCTAssertEqual(test.requiredOptional, ["bytecode-snapshot"])
        let relative = try XCTUnwrap(test.metadata["binaryFixture"]?["relativePath"]?.stringValue)
        let bytes = Array(try Data(contentsOf: repository.appendingPathComponent("conformance/fixtures/" + relative)))
        XCTAssertEqual(ConformanceProgramRunner.runBinaryCase(test, bytes: bytes).status, "passed")
        let wrongDocument = try ConformanceMarkdownParser.parse(markdown.replacingOccurrences(of: "#1e20", with: "#2e20"))
        let wrongTest = try XCTUnwrap(wrongDocument.cases.first { $0.id == test.id })
        let mismatch = ConformanceProgramRunner.runBinaryCase(wrongTest, bytes: bytes)
        XCTAssertEqual(mismatch.status, "failed")
        XCTAssertTrue(mismatch.mismatches.contains { $0.path.hasPrefix("/gesa/") })
        let skipped = ConformanceRunner.runCase(test, environment: .init(capabilities: ["program-binary"]))
        XCTAssertEqual(skipped.code, "conformance.runner.missingOptionalCapability")
    }

    func testSharedBinaryFixtureReadValidateRewrite() throws {
        var repository = URL(fileURLWithPath: #filePath)
        for _ in 0..<6 { repository.deleteLastPathComponent() }
        let path = repository.appendingPathComponent("conformance/suites/program/binary-format.md")
        let document = try ConformanceMarkdownParser.parse(Array(Data(contentsOf: path)))
        var count = 0
        for testCase in document.cases {
            guard let fixture = testCase.metadata["binaryFixture"], let relativePath = fixture["relativePath"]?.stringValue else { continue }
            let bytes = Array(try Data(contentsOf: repository.appendingPathComponent("conformance/fixtures/" + relativePath)))
            XCTAssertEqual(ConformanceSha256.hex(bytes), fixture["sha256"]?.stringValue, testCase.fullID)
            let expected = try testCase.expectation.required("binary")
            do {
                let program = try GameEventScriptProgramReader.read(bytes)
                XCTAssertEqual(expected["outcome"]?.stringValue, "valid", testCase.fullID)
                if let name = expected["moduleName"]?.stringValue { XCTAssertEqual(program.moduleName, name, testCase.fullID) }
                for (key, actual) in [("requiredRegisterCount", Int(program.requiredRegisterCount)), ("requiredCallStackDepth", Int(program.requiredCallStackDepth)), ("opaqueSectionCount", program.opaqueSections.count)] {
                    if let value = expected[key]?.numberValue { XCTAssertEqual(String(actual), value, testCase.fullID + " " + key) }
                }
                let rewrite = try GameEventScriptProgramWriter.bytes(program)
                XCTAssertEqual(try GameEventScriptProgramWriter.encodedSize(program), rewrite.count)
                if let exact = expected["rewriteByteExact"]?.boolValue { XCTAssertEqual(rewrite == bytes, exact, testCase.fullID) }
                if let hash = expected["rewriteSha256"]?.stringValue { XCTAssertEqual(ConformanceSha256.hex(rewrite), hash, testCase.fullID) }
                XCTAssertEqual(try GameEventScriptProgramWriter.bytes(GameEventScriptProgramReader.read(rewrite)), rewrite, testCase.fullID)
                var destination = [UInt8](repeating: 0xaa, count: rewrite.count + 3)
                XCTAssertEqual(try GameEventScriptProgramWriter.write(program, into: &destination), rewrite.count)
                XCTAssertEqual(Array(destination.prefix(rewrite.count)), rewrite)
                XCTAssertEqual(Array(destination.suffix(3)), [0xaa, 0xaa, 0xaa])
                var shortDestination = [UInt8](repeating: 0x5a, count: rewrite.count - 1)
                let original = shortDestination
                XCTAssertThrowsError(try GameEventScriptProgramWriter.write(program, into: &shortDestination))
                XCTAssertEqual(shortDestination, original, "Capacity failure must preserve caller storage")
                let runtime = try GameEventScriptProgramReader.read(bytes, options: .init(retention: .runtimeOnly))
                XCTAssertNil(runtime.debugSymbols)
                XCTAssertNil(runtime.sourceMap)
                XCTAssertNil(runtime.sourceArchive)
                XCTAssertNil(runtime.buildMetadata)
                XCTAssertTrue(runtime.opaqueSections.isEmpty)
                let known = try GameEventScriptProgramReader.read(bytes, options: .init(retention: .preserveKnown))
                XCTAssertTrue(known.opaqueSections.isEmpty)
            } catch let error as GameEventScriptProgramFormatError {
                XCTAssertNotEqual(expected["outcome"]?.stringValue, "valid", "\(testCase.fullID): \(error)")
                let name = String(describing: error.code)
                XCTAssertEqual(name.prefix(1).uppercased() + name.dropFirst(), expected["errorCode"]?.stringValue, testCase.fullID)
                for (key, actual) in [("byteOffset", error.byteOffset), ("sectionType", error.sectionType.map(Int.init)), ("entryIndex", error.entryIndex)] {
                    if let value = expected[key]?.numberValue { XCTAssertEqual(actual.map(String.init), value, testCase.fullID + " " + key) }
                }
            }
            count += 1
        }
        XCTAssertGreaterThan(count, 30)
    }
}
