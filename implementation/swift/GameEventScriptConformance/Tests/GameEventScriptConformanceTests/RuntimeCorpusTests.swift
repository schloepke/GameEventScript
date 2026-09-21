// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime
import XCTest

@testable import GameEventScriptConformance

/// Adapter bootstrap: the C# compiler produces binary inputs; Swift executes the shared expectations.
/// Neither compiler behavior nor platform-specific performance measurements are attributed to Swift.
final class RuntimeCorpusTests: XCTestCase {
    func testSharedRuntimeScenarios() throws {
        var root = URL(fileURLWithPath: #filePath)
        for _ in 0..<6 { root.deleteLastPathComponent() }
        let fixtures = root.appendingPathComponent("artifacts/swift/runtime-fixtures")
        let manifest = try String(contentsOf: fixtures.appendingPathComponent("manifest.tsv"), encoding: .utf8)
        let lines = manifest.split(separator: "\n", omittingEmptySubsequences: true)
        XCTAssertEqual(lines.first, "GES-RUNTIME-FIXTURES-V1")
        var entries: [String: [[String]]] = [:]
        for line in lines.dropFirst() {
            let cells = line.split(separator: "\t", omittingEmptySubsequences: false).map(String.init)
            guard cells.count == 5 else {
                XCTFail("Invalid runtime fixture manifest")
                return
            }
            entries[cells[0], default: []].append(cells)
        }
        let filter = ProcessInfo.processInfo.environment["GES_SWIFT_CASE"]
        let corpus = root.appendingPathComponent("conformance/suites")
        let paths = try XCTUnwrap(FileManager.default.enumerator(at: corpus, includingPropertiesForKeys: nil)).compactMap { $0 as? URL }.filter { $0.pathExtension == "md" }.sorted { $0.path < $1.path }
        var results: [[String: Any]] = []
        var programs: [String: GameEventScriptProgram] = [:]
        for path in paths {
            let bytes = Array(try Data(contentsOf: path))
            let document = try ConformanceMarkdownParser.parse(bytes)
            let documentHash = ConformanceSha256.hex(bytes)
            for testCase in document.cases where ["scriptApi", "loadError", "performance", "bytecodeSnapshot", "programBinary"].contains(testCase.kind) {
                if let filter, !testCase.fullID.contains(filter) { continue }
                let rows = try XCTUnwrap(entries[testCase.fullID], "Missing fixture export for \(testCase.fullID)")
                var inputs: [ConformanceRuntimeProgram] = []
                for row in rows {
                    XCTAssertEqual(row[4], documentHash, "Stale runtime fixture: \(testCase.fullID)")
                    if row[1].isEmpty { continue }
                    if programs[row[3]] == nil {
                        let binary = Array(try Data(contentsOf: fixtures.appendingPathComponent(row[2])))
                        XCTAssertEqual(ConformanceSha256.hex(binary), row[3])
                        programs[row[3]] = try GameEventScriptProgramReader.read(binary)
                    }
                    inputs.append(.init(id: row[1], program: programs[row[3]]!))
                }
                try testCase.fullID.write(to: root.appendingPathComponent("artifacts/swift/runtime-active-case.txt"), atomically: true, encoding: .utf8)
                let result: ConformanceCaseResult
                if testCase.kind == "bytecodeSnapshot" {
                    result = ConformanceProgramRunner.runDumpCase(testCase, program: try XCTUnwrap(inputs.first).program)
                } else if testCase.kind == "programBinary" {
                    let fixture = try testCase.metadata.required("binaryFixture")
                    let binary = Array(try Data(contentsOf: root.appendingPathComponent("conformance/fixtures/" + fixture.text("relativePath"))))
                    result = ConformanceProgramRunner.runBinaryCase(testCase, bytes: binary, comparisonProgram: inputs.first?.program)
                } else {
                    result = ConformanceRuntimeRunner.runCase(testCase, programs: inputs)
                }
                let detail = result.technicalDetails ?? result.mismatches.map { "\($0.path): expected \($0.expected ?? "nil"); actual \($0.actual ?? "nil")" }.joined(separator: "\n")
                results.append(["id": testCase.fullID, "kind": testCase.kind, "status": result.status, "detail": detail])
                XCTAssertEqual(result.status, "passed", testCase.fullID + "\n" + detail)
            }
        }
        try JSONSerialization.data(withJSONObject: results, options: [.prettyPrinted, .sortedKeys]).write(to: root.appendingPathComponent("artifacts/swift/runtime-results.json"))
        print("Swift Runtime scenarios: \(results.filter { $0["status"] as? String == "passed" }.count)/\(results.count) passed")
        if filter == nil { XCTAssertGreaterThan(results.count, 1000) }
    }
}
