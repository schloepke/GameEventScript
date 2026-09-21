// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import XCTest

@testable import GameEventScriptConformance

final class ConformanceCorpusTests: XCTestCase {
    private let apiEnvironment = ConformanceEnvironment(capabilities: ["message-api", "value-api", "external-types"])

    private var repository: URL {
        var url = URL(fileURLWithPath: #filePath)
        for _ in 0..<6 { url.deleteLastPathComponent() }
        return url
    }

    private func documents() throws -> [ConformanceDocument] {
        let root = repository.appendingPathComponent("conformance/suites")
        let enumerator = try XCTUnwrap(FileManager.default.enumerator(at: root, includingPropertiesForKeys: nil))
        let paths = enumerator.compactMap { $0 as? URL }.filter { $0.pathExtension == "md" }.sorted {
            $0.path < $1.path
        }
        return try paths.map { path in
            do { return try ConformanceMarkdownParser.parse(Array(Data(contentsOf: path))) } catch {
                XCTFail("\(path.path): \(error)")
                throw error
            }
        }
    }

    func testSharedCorpusIdentityCapabilitiesAndAPIs() throws {
        let documents = try documents()
        XCTAssertEqual(documents.count, 94)
        XCTAssertEqual(documents.flatMap(\.cases).count, 1517)
        let report = ConformanceRunner.runCorpus(documents, environment: apiEnvironment)
        let compact = try ConformanceReportWriter.crossLanguage(documents, report: report)
        let actual = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(compact.utf8)) as? [String: Any])
        let reference = try XCTUnwrap(
            JSONSerialization.jsonObject(
                with: Data(
                    contentsOf: repository.appendingPathComponent(
                        "conformance/cross-language/CSharpReferenceResults.json"))) as? [String: Any])
        XCTAssertEqual(actual["corpus"] as? NSDictionary, reference["corpus"] as? NSDictionary)
        let actualCases = try XCTUnwrap(actual["cases"] as? [[String: Any]])
        let referenceCases = try XCTUnwrap(reference["cases"] as? [[String: Any]])
        XCTAssertEqual(actualCases.count, referenceCases.count)
        for (left, right) in zip(actualCases, referenceCases) {
            for key in ["id", "kind", "level"] { XCTAssertEqual(left[key] as? String, right[key] as? String, key) }
            XCTAssertEqual(
                left["requires"] as? NSDictionary, right["requires"] as? NSDictionary, left["id"] as? String ?? "")
        }
        XCTAssertFalse(report.cases.isEmpty)
        XCTAssertTrue(report.cases.contains { $0.testCase.kind == "valueApi" && $0.status == "passed" })
        XCTAssertTrue(report.cases.contains { $0.testCase.kind == "messageApi" && $0.status == "passed" })
        for result in report.cases {
            let missing = result.testCase.requiredCore.filter { !report.capabilities.contains($0) }.sorted()
            if missing.isEmpty {
                XCTAssertEqual(
                    result.status, "passed",
                    "\(result.testCase.fullID): \(result.mismatches) \(result.technicalDetails ?? "")")
            } else {
                XCTAssertEqual(result.status, "error", result.testCase.fullID)
                XCTAssertEqual(result.code, "conformance.runner.missingCoreCapability", result.testCase.fullID)
                XCTAssertEqual(result.missingCapabilities, missing, result.testCase.fullID)
            }
        }
        XCTAssertEqual(report.count("failed"), 0)
        XCTAssertEqual(report.count("skipped"), 0)
        _ = try JSONSerialization.jsonObject(with: Data(ConformanceReportWriter.full(report).utf8))
    }

    func testSharedMarkdownBootstrapFixtures() throws {
        let root = repository.appendingPathComponent("conformance/fixtures/MarkdownV1")
        let manifest = try String(contentsOf: root.appendingPathComponent("manifest.tsv"), encoding: .utf8)
        for line in manifest.split(separator: "\n").dropFirst() {
            let fields = line.split(separator: "\t", omittingEmptySubsequences: false).map(String.init)
            XCTAssertEqual(fields.count, 8)
            let bytes = Array(try Data(contentsOf: root.appendingPathComponent(fields[3])))
            XCTAssertEqual(ConformanceSha256.hex(bytes), fields[4], fields[1])
            if fields[2] == "valid" {
                let document = try ConformanceMarkdownParser.parse(bytes)
                XCTAssertEqual(document.suiteID, fields[5])
                XCTAssertEqual(document.cases.map(\.id), [fields[6]])
            } else {
                XCTAssertThrowsError(try ConformanceMarkdownParser.parse(bytes)) { error in
                    XCTAssertEqual((error as? ConformanceParseError)?.diagnostic.code, fields[7], fields[1])
                }
            }
        }
    }

    func testHashAndCanonicalReportDeterminism() throws {
        XCTAssertEqual(ConformanceSha256.hex([]), "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855")
        XCTAssertEqual(
            ConformanceSha256.hex(Array("abc".utf8)), "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD"
        )
        XCTAssertEqual(
            ConformanceSha256.hex(Array("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq".utf8)),
            "248D6A61D20638B8E5C026930C3E6039A33CE45964FF2167F6ECEDD419DB06C1")
        let documents = try documents()
        let report = ConformanceRunner.runCorpus(documents, environment: apiEnvironment)
        let reverse = Array(documents.reversed())
        XCTAssertEqual(
            try ConformanceReportWriter.crossLanguage(documents, report: report),
            try ConformanceReportWriter.crossLanguage(
                reverse, report: ConformanceRunner.runCorpus(reverse, environment: apiEnvironment)))
        XCTAssertThrowsError(try ConformanceReportWriter.identify(documents + [documents[0]]))
    }

    func testRunnerDetectsMismatchesAndMissingCapabilities() throws {
        let path = repository.appendingPathComponent("conformance/fixtures/MarkdownV1/Valid/message-api.md")
        let text = try String(contentsOf: path, encoding: .utf8)
        let valid = try ConformanceMarkdownParser.parse(Array(text.utf8))
        XCTAssertEqual(ConformanceRunner.runDocument(valid).count("passed"), 1)
        let changed = try ConformanceMarkdownParser.parse(
            Array(text.replacingOccurrences(of: "matches: false", with: "matches: true").utf8))
        XCTAssertEqual(ConformanceRunner.runDocument(changed).count("failed"), 1)
        let unavailable = ConformanceRunner.runDocument(valid, environment: .init(capabilities: []))
        XCTAssertEqual(unavailable.cases.first?.code, "conformance.runner.missingCoreCapability")
        let forged = ConformanceRunner.runDocument(
            valid, environment: .init(capabilities: ["performance", "message-api"]))
        XCTAssertEqual(forged.cases.first?.code, "conformance.runner.invalidEnvironment")
    }

    func testClosedSchemasAndCanonicalTransportNumbers() throws {
        func document(_ number: String, extraCase: String = "", extraExpectation: String = "") -> String {
            """
            ---
            formatVersion: 1
            suiteId: bootstrap.swift
            kind: valueApi
            level: atomic
            ---
            ## Test: Value
            ```yaml
            gesBlock: case
            id: value
            valueApi:
              value: { type: ":Number.binary64", value: "\(number)" }
            \(extraCase)
            ```
            ```yaml
            gesBlock: expect
            value:
              normalized: { type: ":Nothing" }
            \(extraExpectation)
            ```
            """
        }
        for number in ["1.0", "1E+20", "-0", "1e020", "0.00001"] {
            XCTAssertThrowsError(try ConformanceMarkdownParser.parse(document(number))) {
                XCTAssertEqual(
                    ($0 as? ConformanceParseError)?.diagnostic.code, "conformance.schema.invalidValue", number)
            }
        }
        for number in ["0", "0.0001", "1e-5", "10000000000000000", "1e17", "NaN", "Infinity", "-Infinity"] {
            XCTAssertNoThrow(try ConformanceMarkdownParser.parse(document(number)), number)
        }
        for text in [document("1", extraCase: "  surprise: true"), document("1", extraExpectation: "  surprise: true")]
        {
            XCTAssertThrowsError(try ConformanceMarkdownParser.parse(text)) {
                XCTAssertEqual(($0 as? ConformanceParseError)?.diagnostic.code, "conformance.schema.unknownField")
            }
        }
        XCTAssertNotEqual(ConformanceData.string("é"), ConformanceData.string("e\u{301}"))
    }

    func testOriginalByteRangesPreserveBOMNewlinesAndScalars() throws {
        let path = repository.appendingPathComponent(
            "conformance/fixtures/MarkdownV1/Valid/minimal-bytecode-snapshot.md")
        let original = try String(contentsOf: path, encoding: .utf8).replacingOccurrences(
            of: "emit Done()", with: "emit Done(\"😀\")")
        for newline in ["\n", "\r\n", "\r"] {
            let content = Array(original.replacingOccurrences(of: "\n", with: newline).utf8)
            let bytes: [UInt8] = [0xef, 0xbb, 0xbf] + content
            let document = try ConformanceMarkdownParser.parse(bytes)
            XCTAssertTrue(document.hasByteOrderMark)
            XCTAssertEqual(document.sourceBytes, bytes)
            XCTAssertEqual(document.frontmatterRange.byteOffset, 0)
            let test = try XCTUnwrap(document.cases.first)
            let source = try XCTUnwrap(test.sources.first)
            func extract(_ range: ConformanceSourceRange) -> String {
                String(decoding: content[range.byteOffset..<(range.byteOffset + range.byteLength)], as: UTF8.self)
            }
            XCTAssertTrue(extract(test.testRange).hasPrefix("## Test:"))
            XCTAssertTrue(extract(test.caseMetadataRange).hasPrefix("```yaml"))
            XCTAssertTrue(extract(source.blockRange).hasPrefix("```ges"))
            XCTAssertEqual(extract(source.payloadRange).replacingOccurrences(of: newline, with: "\n"), source.text)
            XCTAssertEqual(extract(try XCTUnwrap(test.assemblerPayloadRange)), ".segment code")
            XCTAssertTrue(source.text.contains("😀"))
        }
        let empty = original.replacingOccurrences(of: "on Start() {\n  emit Done(\"😀\")\n}\n", with: "")
            .replacingOccurrences(of: ".segment code\n", with: "")
        let parsed = try ConformanceMarkdownParser.parse(empty)
        let test = try XCTUnwrap(parsed.cases.first)
        XCTAssertEqual(test.sources.first?.payloadRange.byteLength, 0)
        XCTAssertEqual(test.assemblerPayloadRange?.byteLength, 0)
    }
}
