// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptConformance
import GameEventScriptRuntime

/// Filesystem adapter for independently compiled inputs. The Markdown remains the sole oracle.
func verifyRuntime(_ documents: [ConformanceDocument], directory: URL, binaryFixtures: URL, destination: URL, corpus: Any) throws -> Bool {
    let manifest = try String(contentsOf: directory.appendingPathComponent("manifest.tsv"), encoding: .utf8)
    let lines = manifest.split(separator: "\n", omittingEmptySubsequences: true)
    guard lines.first == "GES-RUNTIME-FIXTURES-V1" else { throw ToolError.invalidFixture("Unknown Runtime manifest version") }
    var rows: [String: [[String]]] = [:]
    for line in lines.dropFirst() {
        let cells = line.split(separator: "\t", omittingEmptySubsequences: false).map(String.init)
        guard cells.count == 5 else { throw ToolError.invalidFixture("Invalid Runtime manifest row") }
        rows[cells[0], default: []].append(cells)
    }
    var programs: [String: GameEventScriptProgram] = [:]
    var results: [[String: Any]] = []
    let kinds = ["scriptApi", "loadError", "performance", "bytecodeSnapshot", "programBinary"]
    for document in documents {
        let hash = ConformanceSha256.hex(document.sourceBytes)
        for test in document.cases where kinds.contains(test.kind) {
            guard let entries = rows.removeValue(forKey: test.fullID) else { throw ToolError.invalidFixture("Missing exported input for " + test.fullID) }
            var inputs: [ConformanceRuntimeProgram] = []
            for entry in entries {
                guard entry[4] == hash else { throw ToolError.invalidFixture("Stale exported input for " + test.fullID) }
                if entry[1].isEmpty {
                    guard entries.count == 1 && entry[2].isEmpty && entry[3].isEmpty else { throw ToolError.invalidFixture("Invalid empty Program group") }
                    continue
                }
                guard entry[3].utf8.count == 64, entry[3].utf8.allSatisfy({ (48...57).contains($0) || (65...70).contains($0) }), entry[2] == entry[3] + ".gesb" else { throw ToolError.invalidFixture("Invalid binary resource name") }
                if programs[entry[3]] == nil {
                    let bytes = try boundedBinary(directory.appendingPathComponent(entry[2]))
                    guard ConformanceSha256.hex(bytes) == entry[3] else { throw ToolError.invalidFixture("Binary SHA-256 mismatch") }
                    programs[entry[3]] = try GameEventScriptProgramReader.read(bytes)
                }
                inputs.append(.init(id: entry[1], program: programs[entry[3]]!))
            }
            let expectedIDs = test.sources.reduce(into: [String]()) { if !$0.contains($1.programID) { $0.append($1.programID) } }
            guard inputs.map(\.id) == expectedIDs else { throw ToolError.invalidFixture("Program groups do not match " + test.fullID) }
            let result: ConformanceCaseResult
            if test.kind == "bytecodeSnapshot" {
                guard inputs.count == 1 else { throw ToolError.invalidFixture("Snapshot requires one Program") }
                result = ConformanceProgramRunner.runDumpCase(test, program: inputs[0].program)
            } else if test.kind == "programBinary" {
                guard let relative = test.metadata["binaryFixture"]?["relativePath"]?.stringValue else { throw ToolError.invalidFixture("Missing binary fixture path") }
                let path = binaryFixtures.appendingPathComponent(relative).standardizedFileURL
                guard path.path.hasPrefix(binaryFixtures.standardizedFileURL.path + "/") else { throw ToolError.invalidFixture("Binary fixture path escapes its root") }
                result = ConformanceProgramRunner.runBinaryCase(test, bytes: try boundedBinary(path), comparisonProgram: inputs.first?.program)
            } else {
                result = ConformanceRuntimeRunner.runCase(test, programs: inputs)
            }
            results.append([
                "id": test.fullID, "kind": test.kind, "status": result.status, "code": result.code, "scope": test.kind == "performance" ? "runtime-correctness-only" : test.kind == "bytecodeSnapshot" ? "program-dump-only" : "runtime",
                "mismatches": result.mismatches.map { ["path": $0.path, "expected": $0.expected ?? "<absent>", "actual": $0.actual ?? "<absent>"] }, "technicalDetails": result.technicalDetails ?? "",
            ])
        }
    }
    guard rows.isEmpty else { throw ToolError.invalidFixture("Manifest contains cases outside this Runtime corpus") }
    let passed = results.filter { $0["status"] as? String == "passed" }.count
    let report: [String: Any] = ["formatVersion": 1, "scope": "Swift Runtime verification using C# compiler inputs; no Swift compiler or performance measurement claims", "corpus": corpus, "passed": passed, "total": results.count, "cases": results]
    try JSONSerialization.data(withJSONObject: report, options: [.prettyPrinted, .sortedKeys]).write(to: destination.appendingPathComponent("RuntimeResults.json"), options: .atomic)
    var markdown =
        "# Swift Runtime verification\n\n\(passed)/\(results.count) cases passed.\n\nInputs: C# reference compiler. Execution, codecs, and GESA presentation: Swift.\nPerformance scenarios check behavior only; no timing/allocation profiles are measured.\nThis is separate from strict full-port acceptance.\n\n| Case | Kind | Status |\n| --- | --- | --- |\n"
    for result in results { markdown += "| \(result["id"]!) | \(result["kind"]!) | \(result["status"]!) |\n" }
    try Data(markdown.utf8).write(to: destination.appendingPathComponent("RuntimeResults.md"), options: .atomic)
    print("Swift Runtime: \(passed)/\(results.count) passed (C# compiler inputs; no performance measurements).")
    return passed == results.count
}

private func boundedBinary(_ path: URL) throws -> [UInt8] {
    let maximum = 64 * 1024 * 1024
    guard let size = try path.resourceValues(forKeys: [.fileSizeKey]).fileSize, size <= maximum else { throw ToolError.invalidFixture("Binary resource exceeds 64 MiB") }
    let bytes = Array(try Data(contentsOf: path))
    guard bytes.count <= maximum else { throw ToolError.invalidFixture("Binary resource exceeds 64 MiB") }
    return bytes
}
