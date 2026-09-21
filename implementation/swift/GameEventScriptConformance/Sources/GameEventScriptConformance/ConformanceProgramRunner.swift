// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// Verifies Runtime codecs and presentation from the shared Markdown corpus.
/// Compiler inputs are supplied independently; their compilation is not attributed to Swift.
public enum ConformanceProgramRunner {
    /// Compares a supplied Program's GESA dump with the case's authored expectation, returning structured mismatches.
    public static func runDumpCase(_ testCase: ConformanceCase, program: GameEventScriptProgram) -> ConformanceCaseResult {
        guard testCase.kind == "bytecodeSnapshot", let expected = testCase.assembler else { return result(testCase, [], error: "Expected a GESA snapshot case") }
        return result(testCase, dumpDifferences(expected, program: program))
    }

    private static func dumpDifferences(_ expected: String, program: GameEventScriptProgram) -> [ConformanceMismatch] {
        let actual = GameEventScriptProgramDumper.dump(program)
        let expectedLines = expected.split(separator: "\n", omittingEmptySubsequences: false)
        let actualLines = actual.split(separator: "\n", omittingEmptySubsequences: false)
        var differences: [ConformanceMismatch] = []
        for index in 0..<max(expectedLines.count, actualLines.count) {
            let left = index < expectedLines.count ? String(expectedLines[index]) : nil
            let right = index < actualLines.count ? String(actualLines[index]) : nil
            if left != right { differences.append(.init(path: "/gesa/line/" + String(index + 1), expected: left, actual: right)) }
        }
        return differences
    }

    /// The embedding bounds and resolves bytes; SHA-256 is checked before decoding.
    public static func runBinaryCase(_ testCase: ConformanceCase, bytes: [UInt8], comparisonProgram: GameEventScriptProgram? = nil) -> ConformanceCaseResult {
        do {
            guard testCase.kind == "programBinary" else { throw ConformanceExecutionError.invalidInput("Expected a binary case") }
            let fixture = try testCase.metadata.required("binaryFixture")
            let expected = try testCase.expectation.required("binary")
            guard fixture["sha256"]?.stringValue == ConformanceSha256.hex(bytes) else { throw ConformanceExecutionError.invalidInput("Fixture SHA-256 mismatch") }
            var differences: [ConformanceMismatch] = []

            func check(_ data: ConformanceData, _ key: String, _ value: String?, _ path: String = "/binary") {
                guard let wanted = data[key] else { return }
                let wantedString = wanted.stringValue ?? wanted.numberValue ?? wanted.boolValue.map(String.init)
                if wantedString != value { differences.append(.init(path: path + "/" + key, expected: wantedString, actual: value)) }
            }

            let program: GameEventScriptProgram
            do { program = try GameEventScriptProgramReader.read(bytes) } catch let error as GameEventScriptProgramFormatError {
                let name = String(describing: error.code)
                check(expected, "outcome", error.code.rawValue <= GameEventScriptProgramFormatErrorCode.tooManyEntries.rawValue ? "readError" : "validationError")
                check(expected, "errorCode", name.prefix(1).uppercased() + name.dropFirst())
                check(expected, "byteOffset", error.byteOffset.map(String.init))
                check(expected, "sectionType", error.sectionType.map(String.init))
                check(expected, "entryIndex", error.entryIndex.map(String.init))
                return result(testCase, differences)
            }
            check(expected, "outcome", "valid")
            check(expected, "moduleName", program.moduleName)
            check(expected, "requiredRegisterCount", String(program.requiredRegisterCount))
            check(expected, "requiredCallStackDepth", String(program.requiredCallStackDepth))
            check(expected, "opaqueSectionCount", String(program.opaqueSections.count))
            check(fixture, "programVersion", String(program.programVersion), "/binaryFixture")
            if let metadata = program.buildMetadata {
                check(fixture, "compilerId", metadata.compilerID, "/binaryFixture")
                check(fixture, "compilerVersion", metadata.compilerVersion, "/binaryFixture")
            }
            let rewritten = try GameEventScriptProgramWriter.bytes(program)
            check(expected, "rewriteByteExact", String(bytes == rewritten))
            check(expected, "rewriteSha256", ConformanceSha256.hex(rewritten))
            if fixture["compareCompiledRuntime"]?.boolValue == true {
                guard let comparisonProgram else { throw ConformanceExecutionError.invalidInput("Missing independently compiled comparison Program") }
                let actual = requiredSections(rewritten)
                let reference = try requiredSections(GameEventScriptProgramWriter.bytes(comparisonProgram))
                if actual != reference { differences.append(.init(path: "/binary/compiledRuntimeSegments", expected: ConformanceSha256.hex(reference), actual: ConformanceSha256.hex(actual))) }
            }
            if let expectedDump = testCase.assembler { differences += dumpDifferences(expectedDump, program: program) }
            if differences.isEmpty && !testCase.steps.isEmpty {
                let scenario = try RuntimeScenario(testCase, programs: [.init(id: "main", program: program)])
                try scenario.run()
                differences += scenario.differences
            }
            return result(testCase, differences)
        } catch { return result(testCase, [], error: String(describing: error)) }
    }

    private static func requiredSections(_ bytes: [UInt8]) -> [UInt8] {
        var result: [UInt8] = []
        var offset = 16
        while offset < bytes.count {
            let length = (0..<4).reduce(0) { $0 | Int(bytes[offset + 8 + $1]) << ($1 * 8) } + 12
            if bytes[offset + 2] & 1 != 0 { result.append(contentsOf: bytes[offset..<offset + length]) }
            offset += length
        }
        return result
    }

    private static func result(_ test: ConformanceCase, _ differences: [ConformanceMismatch], error: String? = nil) -> ConformanceCaseResult {
        .init(
            testCase: test,
            status: error != nil ? "error" : differences.isEmpty ? "passed" : "failed",
            code: error != nil ? "conformance.runner.invalidEnvironment" : differences.isEmpty ? "conformance.passed" : "conformance.assertion.mismatch",
            missingCapabilities: [],
            mismatches: differences,
            technicalDetails: error
        )
    }
}
