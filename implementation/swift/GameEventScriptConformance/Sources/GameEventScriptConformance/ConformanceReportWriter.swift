// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// Invalid corpus or result identities cannot be exported as an acceptance artifact.
public enum ConformanceReportError: Error {
    /// Corpus identity cannot be formed because documents or case identities are invalid.
    case invalidCorpus
    /// Result identities or case metadata do not match the supplied corpus.
    case inconsistentResults
}

/// Identity of the exact authored Markdown corpus, independent of discovery order.
public struct ConformanceCorpusIdentity: Sendable {
    /// Common Markdown schema version of the identified documents.
    public let markdownFormatVersion: Int
    /// Lowercase SHA-256 of the canonical corpus identity framing and source bytes.
    public let sha256: String
    /// Number of uniquely identified documents.
    public let documentCount: Int
    /// Number of uniquely identified cases.
    public let caseCount: Int
}

/// Canonical report and cross-language identity output. No files are opened here.
public enum ConformanceReportWriter {
    /// Hashes the exact authored corpus in canonical suite order.
    ///
    /// - Throws: `ConformanceReportError.invalidCorpus` for an empty, duplicate or inconsistent corpus.
    public static func identify(_ documents: [ConformanceDocument]) throws -> ConformanceCorpusIdentity {
        guard !documents.isEmpty, Set(documents.map(\.suiteID)).count == documents.count,
            Set(documents.map(\.formatVersion)).count == 1
        else { throw ConformanceReportError.invalidCorpus }
        let cases = documents.flatMap(\.cases)
        guard Set(cases.map(\.fullID)).count == cases.count else { throw ConformanceReportError.invalidCorpus }
        var bytes = Array("GES-CONFORMANCE-CORPUS-V1\0".utf8)
        func append(_ value: Int) throws {
            guard let number = UInt32(exactly: value) else { throw ConformanceReportError.invalidCorpus }
            for shift in stride(from: 0, to: 32, by: 8) { bytes.append(UInt8(truncatingIfNeeded: number >> shift)) }
        }
        try append(documents.count)
        for document in documents.sorted(by: { scalarLess($0.suiteID, $1.suiteID) }) {
            try append(document.formatVersion)
            let id = Array(document.suiteID.utf8)
            try append(id.count)
            bytes += id
            try append(document.sourceBytes.count)
            bytes += document.sourceBytes
        }
        return ConformanceCorpusIdentity(
            markdownFormatVersion: documents[0].formatVersion, sha256: ConformanceSha256.hex(bytes),
            documentCount: documents.count, caseCount: cases.count)
    }

    /// Writes canonical cross-language acceptance JSON tied to the exact corpus identity.
    ///
    /// - Throws: `ConformanceReportError` if the corpus or result identities are inconsistent.
    public static func crossLanguage(_ documents: [ConformanceDocument], report: ConformanceRunReport) throws -> String
    {
        let identity = try identify(documents)
        let cases = documents.flatMap(\.cases)
        guard report.cases.count == cases.count, Set(report.cases.map { $0.testCase.fullID }).count == cases.count
        else { throw ConformanceReportError.inconsistentResults }
        let byID = Dictionary(uniqueKeysWithValues: report.cases.map { ($0.testCase.fullID, $0) })
        let entries = try cases.sorted(by: { scalarLess($0.fullID, $1.fullID) }).map { testCase -> ConformanceData in
            guard let result = byID[testCase.fullID], result.testCase.kind == testCase.kind,
                result.testCase.level == testCase.level,
                result.testCase.requiredCore == testCase.requiredCore,
                result.testCase.requiredOptional == testCase.requiredOptional
            else {
                throw ConformanceReportError.inconsistentResults
            }
            return object([
                ("id", .string(testCase.fullID)), ("kind", .string(testCase.kind)), ("level", .string(testCase.level)),
                (
                    "requires",
                    object([
                        ("core", strings(testCase.requiredCore.sorted())),
                        ("optional", strings(testCase.requiredOptional.sorted())),
                    ])
                ),
                ("status", .string(result.status)), ("code", .string(result.code)),
            ])
        }
        return json(
            object([
                ("schemaVersion", .number("1")),
                (
                    "corpus",
                    object([
                        ("markdownFormatVersion", .number(String(identity.markdownFormatVersion))),
                        ("sha256", .string(identity.sha256)),
                        ("documentCount", .number(String(identity.documentCount))),
                        ("caseCount", .number(String(identity.caseCount))),
                    ])
                ),
                ("cases", .array(entries)),
            ]))
    }

    /// Renders the complete machine-readable JSON report without file I/O.
    public static func full(_ report: ConformanceRunReport) -> String {
        let entries = report.cases.map { result -> ConformanceData in
            let testCase = result.testCase
            let suiteID = String(testCase.fullID.dropLast(testCase.id.count + 1))
            var fields: [(String, ConformanceData)] = [
                ("id", .string(testCase.fullID)), ("suiteId", .string(suiteID)), ("caseId", .string(testCase.id)),
                ("title", .string(testCase.title)),
                ("kind", .string(testCase.kind)), ("level", .string(testCase.level)),
                ("categories", testCase.metadata["categories"] ?? .array([])),
                ("tags", testCase.metadata["tags"] ?? .array([])),
                ("status", .string(result.status)), ("code", .string(result.code)),
                ("missingCapabilities", strings(result.missingCapabilities)),
                (
                    "mismatches",
                    .array(
                        result.mismatches.map { mismatch in
                            var entry: [(String, ConformanceData)] = [
                                ("path", .string(mismatch.path)),
                                (
                                    "code",
                                    .string(
                                        result.performance == nil
                                            ? "conformance.assertion.mismatch" : "conformance.performance.regression")
                                ),
                            ]
                            if let value = mismatch.expected { entry.append(("expected", .string(value))) }
                            if let value = mismatch.actual { entry.append(("actual", .string(value))) }
                            return object(entry)
                        })
                ), ("diagnostics", .array([])), ("runtimeLimits", .array([])), ("actualAssembler", .null),
                ("performance", result.performance.map(performanceData) ?? .null),
            ]
            if let details = result.technicalDetails { fields.append(("technicalDetails", .string(details))) }
            return object(fields)
        }
        return json(
            object([
                ("schemaVersion", .number("1")),
                ("runner", object([("id", .string("ges-swift-conformance")), ("version", .string("0.1.0"))])),
                ("implementation", object([("id", .string("swift")), ("version", .string("0.1.0"))])),
                ("capabilities", strings(report.capabilities)),
                ("performanceProfile", report.performanceProfile.map(ConformanceData.string) ?? .null),
                ("status", .string(report.status)),
                (
                    "summary",
                    object(
                        [("total", .number(String(report.cases.count)))]
                            + ["passed", "failed", "skipped", "error"].map { ($0, .number(String(report.count($0)))) })
                ),
                ("cases", .array(entries)),
            ]))
    }

    /// Renders a human-readable Markdown report without file I/O.
    public static func markdown(_ report: ConformanceRunReport) -> String {
        var result = "# Swift Conformance results\n\n"
        result += "Capabilities: " + report.capabilities.joined(separator: ", ") + ".\n\n"
        result +=
            "\(report.count("passed")) passed, \(report.count("failed")) failed, \(report.count("error")) errors, \(report.count("skipped")) skipped.\n\n"
        result += "Missing Core capabilities are errors. Unavailable optional capabilities are reported as skipped.\n\n"
        result += "| Case | Status | Code | Missing capabilities |\n| --- | --- | --- | --- |\n"
        for entry in report.cases {
            result +=
                "| \(entry.testCase.fullID) | \(entry.status) | \(entry.code) | \(entry.missingCapabilities.joined(separator: ", ")) |\n"
        }
        let measured = report.cases.filter { $0.performance != nil }
        if !measured.isEmpty {
            result +=
                "\nProfile: " + (report.performanceProfile ?? "")
                + "\n\n| Case | Metric | Measured | Reference | Allowed | Unit |\n| --- | --- | ---: | ---: | ---: | --- |\n"
            for entry in measured {
                for metric in entry.performance!.metrics {
                    result +=
                        "| \(entry.testCase.fullID) | \(metric.id) | \(GesValue.float(metric.measured).asText) | \(GesValue.float(metric.reference).asText) | \(GesValue.float(metric.allowed).asText) | \(metric.unit) |\n"
                }
            }
        }
        return result
    }
    static func performanceData(_ value: ConformancePerformanceResult) -> ConformanceData {
        object([
            ("profile", .string(value.profile)),
            (
                "metrics",
                object(
                    value.metrics.map { metric in
                        (
                            metric.id,
                            object([
                                ("measured", .string(GesValue.float(metric.measured).asText)),
                                ("reference", .string(GesValue.float(metric.reference).asText)),
                                ("allowed", .string(GesValue.float(metric.allowed).asText)),
                                ("unit", .string(metric.unit)), ("passed", .bool(metric.passed)),
                            ])
                        )
                    })
            ),
        ])
    }

    static func strings(_ values: [String]) -> ConformanceData { .array(values.map(ConformanceData.string)) }
    static func object(_ entries: [(String, ConformanceData)]) -> ConformanceData {
        .object(entries.map { ConformanceEntry(key: $0.0, value: $0.1) })
    }
    static func scalarLess(_ a: String, _ b: String) -> Bool {
        a.unicodeScalars.lexicographicallyPrecedes(b.unicodeScalars)
    }

    static func json(_ value: ConformanceData) -> String { encode(value, depth: 0) + "\n" }
    private static func encode(_ value: ConformanceData, depth: Int) -> String {
        let indent = String(repeating: " ", count: depth * 2)
        let child = indent + "  "
        switch value {
        case .null: return "null"
        case .bool(let value): return value ? "true" : "false"
        case .number(let text): return text
        case .string(let text): return quote(text)
        case .array(let values):
            return values.isEmpty
                ? "[]"
                : "[\n" + values.map { child + encode($0, depth: depth + 1) }.joined(separator: ",\n") + "\n" + indent
                    + "]"
        case .object(let values):
            return values.isEmpty
                ? "{}"
                : "{\n"
                    + values.map { child + quote($0.key) + ": " + encode($0.value, depth: depth + 1) }.joined(
                        separator: ",\n") + "\n" + indent + "}"
        }
    }
    private static func quote(_ text: String) -> String {
        var result = "\""
        for scalar in text.unicodeScalars {
            switch scalar.value {
            case 34: result += "\\\""
            case 92: result += "\\\\"
            case 8: result += "\\b"
            case 12: result += "\\f"
            case 10: result += "\\n"
            case 13: result += "\\r"
            case 9: result += "\\t"
            case 0..<32:
                let hex = String(scalar.value, radix: 16)
                result += "\\u" + String(repeating: "0", count: 4 - hex.count) + hex
            default: result.unicodeScalars.append(scalar)
            }
        }
        return result + "\""
    }
}
