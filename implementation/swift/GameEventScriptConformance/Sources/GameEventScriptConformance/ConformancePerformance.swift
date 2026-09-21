// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// One platform measurement in exactly the unit declared by its profile.
public struct ConformanceMeasuredMetric: Sendable {
    /// Stable metric identifier selected by the performance profile.
    public let id: String
    /// Measured nonnegative finite value in the declared unit.
    public let value: Double
    /// Unit identifier matching the expectation profile.
    public let unit: String
    /// Creates an adapter measurement; the runner validates metric identity, unit and numeric bounds.
    public init(id: String, value: Double, unit: String) {
        self.id = id
        self.value = value
        self.unit = unit
    }
}
/// Measurement is supplied by an adapter; the portable runner owns comparisons.
public protocol ConformancePerformanceProvider: Sendable {
    /// Measures the declared workload for the selected platform profile. Return exactly the expected metric set and
    /// units. Adapter failures may throw and are reported as invalid measurement environments.
    func measure(_ test: ConformanceCase, profile: String) throws -> [ConformanceMeasuredMetric]
}
/// One measured metric compared with its reference and effective upper bound.
public struct ConformancePerformanceMetricResult: Sendable {
    /// Stable metric identifier.
    public let id: String
    /// Observed value in the declared unit.
    public let measured: Double
    /// Authored baseline value.
    public let reference: Double
    /// Effective maximum after applying all declared tolerances and ceilings.
    public let allowed: Double
    /// Shared unit of measurement and expectation.
    public let unit: String
    /// Whether the observed value is at or below the effective upper bound.
    public var passed: Bool { measured <= allowed }
}
/// Performance comparisons for one case and platform profile.
public struct ConformancePerformanceResult: Sendable {
    /// Selected platform profile identifier.
    public let profile: String
    /// Per-metric measurements and effective limits.
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
