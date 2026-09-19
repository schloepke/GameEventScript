// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import GameEventScriptRuntime
import XCTest

@testable import GameEventScriptConformance

/// File-system adapter for the existing Markdown oracle, with native Swift compilation.
final class CompilerCorpusTests: XCTestCase {
    struct FixedResource: ConformanceResourceResolver {
        let result: ConformanceResourceResult
        init(_ result: ConformanceResourceResult) { self.result = result }
        func resolve(resourceID: String, maximumBytes: Int) -> ConformanceResourceResult { result }
    }

    func testCompilerBuilderOwnershipAndOptions() throws {
        let builder = GameEventScriptBuilder()
            .addScript("on Start() { emit First() }")
            .addScript("on Start() { emit Second(1, 2, 3) }")
        let program = try builder.compile()
        XCTAssertEqual(program.sourceArchive?.map(\.sourceID), [0, 1])
        XCTAssertEqual(program.sourceArchive?.map(\.sourceName), ["UnknownSource", "UnknownSource"])
        XCTAssertNoThrow(try GameEventScriptProgramReader.read(GameEventScriptProgramWriter.bytes(program)))
        let minimal = try builder.withDebugInfo(.none).withProgramVersion(42).compile()
        XCTAssertNil(minimal.sourceArchive)
        XCTAssertNil(minimal.sourceMap)
        XCTAssertNil(minimal.debugSymbols)
        XCTAssertEqual(minimal.programVersion, 42)
        XCTAssertNotNil(program.sourceArchive)
        XCTAssertThrowsError(try builder.compile(options: .init(debugInfo: .init(rawValue: 128)))) {
            guard case GameEventScriptAPIError.invalidArgument = $0 else { return XCTFail("Unexpected error: \($0)") }
        }
    }

    func testRunnerResourceBoundaries() throws {
        var root = URL(fileURLWithPath: #filePath)
        for _ in 0..<6 { root.deleteLastPathComponent() }
        let corpus = root.appendingPathComponent("conformance/suites")
        let paths = try XCTUnwrap(FileManager.default.enumerator(at: corpus, includingPropertiesForKeys: nil))
            .compactMap { $0 as? URL }.filter { $0.pathExtension == "md" }.sorted { $0.path < $1.path }
        let documents = try paths.map { try ConformanceMarkdownParser.parse(Array(Data(contentsOf: $0))) }
        let test = try XCTUnwrap(documents.flatMap(\.cases).first { $0.kind == "programBinary" })
        XCTAssertEqual(ConformanceRunner.runCase(test).code, "conformance.resource.unavailable")
        for (value, code) in [
            (ConformanceResourceResult.notFound, "conformance.resource.unavailable"),
            (.error("fixture unavailable"), "conformance.resource.unavailable"),
            (.limitExceeded, "conformance.resource.limitExceeded"),
            (.found([0, 1]), "conformance.resource.limitExceeded"),
            (.found([0]), "conformance.resource.integrityMismatch"),
        ] {
            let result = ConformanceRunner.runCase(
                test, environment: .init(resourceResolver: FixedResource(value), maximumResourceBytes: 1))
            XCTAssertEqual(result.status, "error")
            XCTAssertEqual(result.code, code)
        }
        let performance = try XCTUnwrap(documents.flatMap(\.cases).first { $0.kind == "performance" })
        let skipped = ConformanceRunner.runCase(performance)
        XCTAssertEqual(skipped.status, "skipped")
        XCTAssertEqual(skipped.code, "conformance.runner.missingOptionalCapability")
    }

    func testSharedCompilerCorpus() throws {
        var root = URL(fileURLWithPath: #filePath)
        for _ in 0..<6 { root.deleteLastPathComponent() }
        let corpus = root.appendingPathComponent("conformance/suites")
        let paths = try XCTUnwrap(FileManager.default.enumerator(at: corpus, includingPropertiesForKeys: nil))
            .compactMap { $0 as? URL }.filter { $0.pathExtension == "md" }.sorted { $0.path < $1.path }
        let filter = ProcessInfo.processInfo.environment["GES_SWIFT_CASE"]
        var results: [[String: Any]] = []
        for path in paths {
            let document = try ConformanceMarkdownParser.parse(Array(Data(contentsOf: path)))
            for test in document.cases {
                if let filter, !test.fullID.contains(filter) { continue }
                try test.fullID.write(
                    to: root.appendingPathComponent("artifacts/swift/compiler-active-case.txt"), atomically: true,
                    encoding: .utf8)
                var bytes: [UInt8]?
                if let fixture = test.metadata["binaryFixture"]?["relativePath"]?.stringValue {
                    bytes = Array(try Data(contentsOf: root.appendingPathComponent("conformance/fixtures/" + fixture)))
                }
                // Performance behavior remains checked, without claiming a Swift measurement profile.
                let result =
                    test.kind == "performance"
                    ? ConformanceCompilerRunner.runCase(test)
                    : ConformanceRunner.runCase(
                        test,
                        environment: .init(
                            resourceResolver: FixedResource(bytes.map(ConformanceResourceResult.found) ?? .notFound)))
                if result.status != "passed", ["bytecodeSnapshot", "programBinary"].contains(test.kind),
                    let program = try? ConformanceCompilerRunner.compile(test).first?.program
                {
                    let folder = root.appendingPathComponent("artifacts/swift/compiler-dumps")
                    try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
                    let name = test.fullID.replacingOccurrences(of: "/", with: "-")
                    try GameEventScriptProgramDumper.dump(program).write(
                        to: folder.appendingPathComponent(name + ".actual.gesa"), atomically: true, encoding: .utf8)
                    let expected =
                        test.assembler
                        ?? bytes.flatMap { try? GameEventScriptProgramReader.read($0) }.map {
                            GameEventScriptProgramDumper.dump($0)
                        }
                    try expected?.write(
                        to: folder.appendingPathComponent(name + ".expected.gesa"), atomically: true, encoding: .utf8)
                }
                let detail =
                    result.technicalDetails
                    ?? result.mismatches.map {
                        "\($0.path): expected \($0.expected ?? "nil"); actual \($0.actual ?? "nil")"
                    }.joined(separator: "\n")
                results.append(["id": test.fullID, "kind": test.kind, "status": result.status, "detail": detail])
                XCTAssertEqual(result.status, "passed", test.fullID + "\n" + detail)
            }
        }
        try JSONSerialization.data(withJSONObject: results, options: [.prettyPrinted, .sortedKeys]).write(
            to: root.appendingPathComponent("artifacts/swift/compiler-results.json"))
        print(
            "Swift compiler corpus: \(results.filter { $0["status"] as? String == "passed" }.count)/\(results.count) passed"
        )
    }
}
