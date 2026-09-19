// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// One platform measurement in exactly the unit declared by its profile.
public struct ConformanceMeasuredMetric: Sendable {
    public let id: String
    public let value: Double
    public let unit: String
    public init(id: String, value: Double, unit: String) {
        self.id = id
        self.value = value
        self.unit = unit
    }
}
/// Measurement is supplied by an adapter; the portable runner owns comparisons.
public protocol ConformancePerformanceProvider: Sendable {
    func measure(_ test: ConformanceCase, profile: String) throws -> [ConformanceMeasuredMetric]
}
public struct ConformancePerformanceMetricResult: Sendable {
    public let id: String
    public let measured: Double
    public let reference: Double
    public let allowed: Double
    public let unit: String
    public var passed: Bool { measured <= allowed }
}
public struct ConformancePerformanceResult: Sendable {
    public let profile: String
    public let metrics: [ConformancePerformanceMetricResult]
}

extension ConformanceRunner {
    static func performance(_ test: ConformanceCase, _ environment: ConformanceEnvironment) -> ConformanceCaseResult {
        func failure(_ code: String, _ detail: String? = nil) -> ConformanceCaseResult {
            .init(
                testCase: test, status: "error", code: code, missingCapabilities: [], mismatches: [],
                technicalDetails: detail)
        }
        guard let profile = environment.performanceProfile,
            let expected = test.expectation["performance"]?["profiles"]?[profile]?["metrics"]?.objectValue,
            let provider = environment.performanceProvider
        else { return failure("conformance.runner.missingPerformanceProfile") }
        let correctness = ConformanceCompilerRunner.runCase(test)
        guard correctness.status == "passed" else { return correctness }
        do {
            let measurements = try provider.measure(test, profile: profile)
            guard Set(measurements.map(\.id)).count == measurements.count,
                Set(measurements.map(\.id)) == Set(expected.map(\.key))
            else {
                return failure(
                    "conformance.runner.invalidEnvironment", "Measurement metric set does not match the profile")
            }
            var metrics: [ConformancePerformanceMetricResult] = []
            for entry in expected {
                let actual = measurements.first { $0.id == entry.key }!
                guard actual.value.isFinite, actual.value >= 0, actual.unit == entry.value["unit"]?.stringValue else {
                    return failure(
                        "conformance.runner.invalidEnvironment", "Invalid performance value or unit: " + entry.key)
                }
                func number(_ key: String) -> Double? {
                    entry.value[key].flatMap { Double($0.numberValue ?? $0.stringValue ?? "") }
                }
                guard let reference = number("reference") else { return failure("conformance.runner.invalidModel") }
                var allowed = number("maximum") ?? Double.infinity
                if let relative = number("toleranceRelative") { allowed = min(allowed, reference * (1 + relative)) }
                if let absolute = number("toleranceAbsolute") { allowed = min(allowed, reference + absolute) }
                guard allowed.isFinite else { return failure("conformance.runner.invalidModel") }
                metrics.append(
                    .init(
                        id: entry.key, measured: actual.value, reference: reference, allowed: allowed, unit: actual.unit
                    ))
            }
            let differences = metrics.filter { !$0.passed }.map {
                ConformanceMismatch(
                    path: "/performance/metrics/" + $0.id, expected: GesValue.float($0.allowed).asText,
                    actual: GesValue.float($0.measured).asText)
            }
            return ConformanceCaseResult(
                testCase: test, status: differences.isEmpty ? "passed" : "failed",
                code: differences.isEmpty ? "conformance.passed" : "conformance.performance.regression",
                missingCapabilities: [], mismatches: differences, technicalDetails: nil,
                performance: .init(profile: profile, metrics: metrics))
        } catch { return failure("conformance.runner.invalidEnvironment", String(describing: error)) }
    }
}
