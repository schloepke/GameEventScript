// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import XCTest

@testable import GameEventScriptConformance

final class PerformanceRunnerTests: XCTestCase {
    struct Provider: ConformancePerformanceProvider {
        let mode: String

        func measure(_ test: ConformanceCase, profile: String) throws -> [ConformanceMeasuredMetric] {
            if mode == "mustNotRun" {
                XCTFail("Measurement must follow successful correctness")
                return []
            }
            var result = test.expectation["performance"]!["profiles"]![profile]!["metrics"]!.objectValue!.map { entry in
                func number(_ key: String) -> Double? { entry.value[key].flatMap { Double($0.numberValue ?? $0.stringValue ?? "") } }

                let reference = number("reference")!
                let limit = min(number("maximum") ?? .infinity, number("toleranceRelative").map { reference * (1 + $0) } ?? .infinity, number("toleranceAbsolute").map { reference + $0 } ?? .infinity)
                return ConformanceMeasuredMetric(
                    id: entry.key,
                    value: mode == "nan" ? .nan : mode == "negative" ? -1 : mode == "regression" ? limit + 1 : mode == "improvement" ? 0 : limit,
                    unit: mode == "unit" ? "count" : entry.value["unit"]!.stringValue!
                )
            }
            if mode == "missing" { result.removeLast() }
            if mode == "duplicate" { result.append(result[0]) }
            return result
        }
    }

    func testPerformanceContractAndReports() throws {
        var root = URL(fileURLWithPath: #filePath)
        for _ in 0..<6 { root.deleteLastPathComponent() }
        let text = try String(contentsOf: root.appendingPathComponent("conformance/suites/performance/core.md"), encoding: .utf8)
        let document = try ConformanceMarkdownParser.parse(text)
        let test = try XCTUnwrap(document.cases.first { $0.kind == "performance" })

        func environment(_ mode: String, profile: String = "csharp-dotnet-release-macos-arm64") -> ConformanceEnvironment {
            .init(capabilities: ConformanceEnvironment.supportedCapabilities + ["performance"], performanceProfile: profile, performanceProvider: Provider(mode: mode))
        }

        for mode in ["limit", "improvement"] {
            let result = ConformanceRunner.runCase(test, environment: environment(mode))
            XCTAssertEqual(result.status, "passed", result.technicalDetails ?? "")
            XCTAssertFalse(try XCTUnwrap(result.performance).metrics.isEmpty)
            let report = ConformanceRunReport(capabilities: environment(mode).capabilities, cases: [result], performanceProfile: environment(mode).performanceProfile)
            let json = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(ConformanceReportWriter.full(report).utf8)) as? [String: Any])
            XCTAssertEqual(json["performanceProfile"] as? String, report.performanceProfile)
            XCTAssertTrue(ConformanceReportWriter.markdown(report).contains("| Measured | Reference | Allowed |"))
        }
        let regression = ConformanceRunner.runCase(test, environment: environment("regression"))
        XCTAssertEqual(regression.status, "failed")
        XCTAssertEqual(regression.code, "conformance.performance.regression")
        for mode in ["nan", "negative", "unit", "missing", "duplicate"] { XCTAssertEqual(ConformanceRunner.runCase(test, environment: environment(mode)).code, "conformance.runner.invalidEnvironment", mode) }
        XCTAssertEqual(ConformanceRunner.runCase(test, environment: environment("limit", profile: "absent")).code, "conformance.runner.missingPerformanceProfile")
        let changed = try ConformanceMarkdownParser.parse(text.replacingOccurrences(of: "value: \"29\"", with: "value: \"999\""))
        let wrong = try XCTUnwrap(changed.cases.first { $0.kind == "performance" })
        XCTAssertEqual(ConformanceRunner.runCase(wrong, environment: environment("mustNotRun")).code, "conformance.assertion.mismatch")
    }
}
