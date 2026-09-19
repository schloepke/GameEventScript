// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// A stable assertion difference, independent of platform exception text.
public struct ConformanceMismatch: Sendable {
    public let path: String
    public let expected: String?
    public let actual: String?
}

/// The outcome of one independently executed portable case.
public struct ConformanceCaseResult: Sendable {
    public let testCase: ConformanceCase
    public let status: String
    public let code: String
    public let missingCapabilities: [String]
    public let mismatches: [ConformanceMismatch]
    public let technicalDetails: String?
    public let performance: ConformancePerformanceResult?
    init(
        testCase: ConformanceCase, status: String, code: String, missingCapabilities: [String],
        mismatches: [ConformanceMismatch], technicalDetails: String?, performance: ConformancePerformanceResult? = nil
    ) {
        self.testCase = testCase
        self.status = status
        self.code = code
        self.missingCapabilities = missingCapabilities
        self.mismatches = mismatches
        self.technicalDetails = technicalDetails
        self.performance = performance
    }
}

/// A bounded resource lookup outcome supplied by the embedding.
public enum ConformanceResourceResult: Sendable {
    case found([UInt8])
    case notFound
    case limitExceeded
    case error(String)
}

/// Resolves portable IDs without exposing paths or file access to the runner.
public protocol ConformanceResourceResolver: Sendable {
    func resolve(resourceID: String, maximumBytes: Int) -> ConformanceResourceResult
}

/// Native Swift execution capabilities and optional bounded binary resources.
public struct ConformanceEnvironment: Sendable {
    /// Capabilities available without a platform measurement provider.
    public static let supportedCapabilities = [
        "compiler", "program-binary", "host", "vm", "message-api", "value-api", "external-types",
        "native-handlers", "publish-sink", "observer", "bytecode-snapshot",
    ]
    public let capabilities: [String]
    public let resourceResolver: (any ConformanceResourceResolver)?
    public let maximumResourceBytes: Int
    public let performanceProfile: String?
    public let performanceProvider: (any ConformancePerformanceProvider)?
    public init(
        capabilities: [String] = Self.supportedCapabilities,
        resourceResolver: (any ConformanceResourceResolver)? = nil,
        maximumResourceBytes: Int = 64 * 1024 * 1024,
        performanceProfile: String? = nil, performanceProvider: (any ConformancePerformanceProvider)? = nil
    ) {
        self.capabilities = Array(Set(capabilities)).sorted()
        self.resourceResolver = resourceResolver
        self.maximumResourceBytes = maximumResourceBytes
        self.performanceProfile = performanceProfile
        self.performanceProvider = performanceProvider
    }
}

/// A complete execution report, including cases whose Core capabilities are unavailable.
public struct ConformanceRunReport: Sendable {
    public let capabilities: [String]
    public let cases: [ConformanceCaseResult]
    public let performanceProfile: String?
    init(capabilities: [String], cases: [ConformanceCaseResult], performanceProfile: String? = nil) {
        self.capabilities = capabilities
        self.cases = cases
        self.performanceProfile = performanceProfile
    }
    public var status: String {
        if cases.contains(where: { $0.status == "error" }) { return "error" }
        if cases.contains(where: { $0.status == "failed" }) { return "failed" }
        return cases.contains(where: { $0.status == "passed" }) ? "passed" : "skipped"
    }
    public func count(_ status: String) -> Int { cases.filter { $0.status == status }.count }
}

/// Executes validated documents without file access, a test framework, or ambient state.
public enum ConformanceRunner {
    public static func runCorpus(_ documents: [ConformanceDocument], environment: ConformanceEnvironment = .init())
        -> ConformanceRunReport
    {
        let suiteIDs = documents.map(\.suiteID)
        let cases = documents.flatMap(\.cases)
        let duplicate = Set(suiteIDs).count != suiteIDs.count || Set(cases.map(\.fullID)).count != cases.count
        let results = cases.map { testCase in
            duplicate
                ? result(testCase, "error", "conformance.runner.invalidModel", technical: "Duplicate corpus identity")
                : runCase(testCase, environment: environment)
        }
        return ConformanceRunReport(
            capabilities: environment.capabilities, cases: results, performanceProfile: environment.performanceProfile)
    }

    public static func runDocument(_ document: ConformanceDocument, environment: ConformanceEnvironment = .init())
        -> ConformanceRunReport
    {
        runCorpus([document], environment: environment)
    }

    public static func runCase(_ testCase: ConformanceCase, environment: ConformanceEnvironment = .init())
        -> ConformanceCaseResult
    {
        if (environment.capabilities.contains("performance")
            && (environment.performanceProvider == nil || environment.performanceProfile?.isEmpty != false))
            || environment.maximumResourceBytes <= 0
            || environment.capabilities.contains(where: {
                !(ConformanceEnvironment.supportedCapabilities + ["performance"]).contains($0)
            })
        {
            return result(
                testCase, "error", "conformance.runner.invalidEnvironment",
                technical: "An unimplemented capability or invalid resource limit was supplied")
        }
        let missing = testCase.requiredCore.filter { !environment.capabilities.contains($0) }.sorted()
        if !missing.isEmpty {
            return result(testCase, "error", "conformance.runner.missingCoreCapability", missing: missing)
        }
        let optional = testCase.requiredOptional.filter { !environment.capabilities.contains($0) }.sorted()
        if !optional.isEmpty {
            return result(testCase, "skipped", "conformance.runner.missingOptionalCapability", missing: optional)
        }
        if testCase.kind == "performance" { return performance(testCase, environment) }
        if testCase.kind == "programBinary" {
            guard let resolver = environment.resourceResolver,
                let id = testCase.metadata["binaryFixture"]?["resourceId"]?.stringValue
            else {
                return result(testCase, "error", "conformance.resource.unavailable")
            }
            switch resolver.resolve(resourceID: id, maximumBytes: environment.maximumResourceBytes) {
            case .found(let bytes):
                guard bytes.count <= environment.maximumResourceBytes else {
                    return result(testCase, "error", "conformance.resource.limitExceeded")
                }
                guard ConformanceSha256.hex(bytes) == testCase.metadata["binaryFixture"]?["sha256"]?.stringValue else {
                    return result(testCase, "error", "conformance.resource.integrityMismatch")
                }
                return ConformanceCompilerRunner.runCase(testCase, binaryFixture: bytes)
            case .limitExceeded: return result(testCase, "error", "conformance.resource.limitExceeded")
            case .notFound: return result(testCase, "error", "conformance.resource.unavailable")
            case .error(let detail):
                return result(testCase, "error", "conformance.resource.unavailable", technical: detail)
            }
        }
        do {
            let mismatches: [ConformanceMismatch]
            switch testCase.kind {
            case "messageApi": mismatches = try messageAPI(testCase)
            case "valueApi": mismatches = try valueAPI(testCase)
            case "externalTypeApi": mismatches = try externalTypeAPI(testCase)
            default:
                return ConformanceCompilerRunner.runCase(testCase)
            }
            return result(
                testCase, mismatches.isEmpty ? "passed" : "failed",
                mismatches.isEmpty ? "conformance.passed" : "conformance.assertion.mismatch", mismatches: mismatches)
        } catch {
            return result(
                testCase, "error", "conformance.runner.unhandledException", technical: String(describing: error))
        }
    }

    private static func result(
        _ testCase: ConformanceCase, _ status: String, _ code: String, missing: [String] = [],
        mismatches: [ConformanceMismatch] = [], technical: String? = nil
    ) -> ConformanceCaseResult {
        ConformanceCaseResult(
            testCase: testCase, status: status, code: code, missingCapabilities: missing, mismatches: mismatches,
            technicalDetails: technical)
    }

    private static func check(
        _ expected: ConformanceData, _ key: String, _ actual: String?, _ path: String,
        _ mismatches: inout [ConformanceMismatch]
    ) {
        guard let node = expected[key] else { return }
        let wanted = node.stringValue ?? node.numberValue ?? node.boolValue.map { $0 ? "true" : "false" }
        if wanted?.unicodeScalars.map(\.value) != actual?.unicodeScalars.map(\.value) {
            mismatches.append(ConformanceMismatch(path: path + "/" + key, expected: wanted, actual: actual))
        }
    }

    private static func check(
        _ expected: ConformanceData, _ key: String, _ actual: Bool, _ path: String,
        _ mismatches: inout [ConformanceMismatch]
    ) {
        check(expected, key, actual ? "true" : "false", path, &mismatches)
    }

    private static func externalTypeAPI(_ testCase: ConformanceCase) throws -> [ConformanceMismatch] {
        let definition = try testCase.metadata.required("externalTypeApi")
        let expected = try testCase.expectation.required("externalType")
        var differences: [ConformanceMismatch] = []
        do {
            let types = try definition.values("typeNames").map {
                try GameEventScriptExternalTypeDefinition(name: $0.stringValue!, fields: [], constructors: [])
            }
            let catalog = try GameEventScriptExternalTypeCatalog(types)
            check(expected, "typeCount", String(catalog.types.count), "/externalType", &differences)
            check(expected, "error", nil, "/externalType", &differences)
        } catch is GameEventScriptAPIError {
            if expected["error"] == nil {
                differences.append(.init(path: "/externalType/error", expected: nil, actual: "duplicateTypeName"))
            } else {
                check(expected, "error", "duplicateTypeName", "/externalType", &differences)
            }
        }
        return differences
    }

    private static func valueAPI(_ testCase: ConformanceCase) throws -> [ConformanceMismatch] {
        let definition = try testCase.metadata.required("valueApi")
        let expected = try testCase.expectation.required("value")
        let value = try ConformanceRuntimeValueCodec.decode(
            definition.required("value"), mutateSource: definition["mutateSourceAfterCreate"]?.boolValue == true)
        var differences: [ConformanceMismatch] = []
        check(expected, "isNumeric", value.isNumeric, "/value", &differences)
        check(expected, "hasValue", value.hasValue, "/value", &differences)
        check(expected, "isNothing", value.isNothing, "/value", &differences)
        check(expected, "hasUnit", value.hasUnit, "/value", &differences)
        check(expected, "asBoolean", value.asBoolean, "/value", &differences)
        check(expected, "length", String(value.length), "/value", &differences)
        check(expected, "customTypeName", value.customTypeName, "/value", &differences)
        if try !ConformanceRuntimeValueCodec.equal(
            expected.required("normalized"), value, comparison: testCase.metadata["comparison"])
        {
            differences.append(
                .init(
                    path: "/value/normalized", expected: "expected portable value", actual: "different portable value"))
        }
        // A normalized value assertion explicitly requests its transport storage,
        // even when a NaN-bearing range is accepted as nothing by message comparison.
        if expected["normalized"]?["type"]?.stringValue == ":Range.int64" && value.integerRangeValue == nil {
            differences.append(
                .init(path: "/value/normalized/type", expected: ":Range.int64", actual: ":Range.binary64"))
        } else if expected["normalized"]?["type"]?.stringValue == ":Range.binary64" && value.floatRangeValue == nil {
            differences.append(
                .init(path: "/value/normalized/type", expected: ":Range.binary64", actual: ":Range.int64"))
        }
        if let node = definition["equalTo"] {
            let other = try ConformanceRuntimeValueCodec.decode(node)
            check(expected, "equal", value == other, "/value", &differences)
            check(expected, "equalHash", value.hashValue == other.hashValue, "/value", &differences)
        }
        if let node = definition["notEqualTo"] {
            check(expected, "notEqual", try value != ConformanceRuntimeValueCodec.decode(node), "/value", &differences)
        }
        return differences
    }

    private static func messageAPI(_ testCase: ConformanceCase) throws -> [ConformanceMismatch] {
        let definition = try testCase.metadata.required("messageApi")
        let expected = try testCase.expectation.required("message")
        var differences: [ConformanceMismatch] = []
        if definition["message"]?["args"]?.objectValue != nil {
            check(expected, "error", "invalidArgumentsShape", "/message", &differences)
            if expected["error"] == nil {
                differences.append(.init(path: "/message/error", expected: nil, actual: "invalidArgumentsShape"))
            }
            return differences
        }
        do {
            let signature = try ConformanceRuntimeValueCodec.signature(definition.required("signature"))
            let message = try ConformanceRuntimeValueCodec.message(definition.required("message"))
            if expected["error"] != nil {
                check(expected, "error", nil, "/message", &differences)
                return differences
            }
            check(expected, "name", message.name, "/message", &differences)
            check(expected, "signatureId", signature.signatureId, "/message", &differences)
            check(expected, "messageSignatureId", message.signatureId, "/message", &differences)
            check(expected, "matches", signature.matches(message), "/message", &differences)
            check(expected, "argumentCount", String(message.arguments.count), "/message", &differences)
            if let node = definition["compareSignature"] {
                let other = try ConformanceRuntimeValueCodec.signature(node)
                check(expected, "signatureEquals", signature == other, "/message", &differences)
                check(expected, "signatureHashEquals", signature.hashValue == other.hashValue, "/message", &differences)
            }
            if let node = definition["compareMessage"] {
                let other = try ConformanceRuntimeValueCodec.message(node)
                check(expected, "messageEquals", message == other, "/message", &differences)
                check(expected, "messageHashEquals", message.hashValue == other.hashValue, "/message", &differences)
            }
            if let node = definition["compareHandler"] {
                let left = GesValue.handler(signature)
                let right = try GesValue.handler(ConformanceRuntimeValueCodec.signature(node))
                check(expected, "handlerEquals", left == right, "/message", &differences)
                check(expected, "handlerHashEquals", left.hashValue == right.hashValue, "/message", &differences)
            }
            if let node = definition["compareConformanceMessage"] {
                check(
                    expected, "conformanceEquals",
                    try ConformanceRuntimeValueCodec.messagesEqual(
                        node, message, comparison: testCase.metadata["comparison"]), "/message", &differences)
            }
            if definition["createArguments"] != nil || expected["createdMessageSignatureId"] != nil {
                let values = try definition.values("createArguments").map {
                    try ConformanceRuntimeValueCodec.decode($0)
                }
                check(
                    expected, "createdMessageSignatureId", signature.createMessage(values)?.signatureId ?? "<null>",
                    "/message", &differences)
            }
        } catch let error as GameEventScriptMessageError {
            let code: String
            switch error {
            case .duplicateArgumentName: code = "duplicateArgumentName"
            case .argumentCountMismatch, .missingArgument: code = "invalidArgument"
            default: code = "invalidMessage"
            }
            if expected["error"] == nil {
                differences.append(.init(path: "/message/error", expected: nil, actual: code))
            } else {
                check(expected, "error", code, "/message", &differences)
            }
        } catch is GesValueError {
            // Invalid argument values (currently malformed Tags) reach the same
            // public message-validation boundary as invalid names and delivery tags.
            let code = "invalidMessage"
            if expected["error"] == nil {
                differences.append(.init(path: "/message/error", expected: nil, actual: code))
            } else {
                check(expected, "error", code, "/message", &differences)
            }
        }
        return differences
    }
}
