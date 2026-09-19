// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
@_spi(Performance) import GameEventScriptConformance

enum ToolError: Error {
    case invalidArguments(String)
    case invalidFixture(String)
}

func run() throws -> Int32 {
    var arguments = Array(CommandLine.arguments.dropFirst())
    if arguments.isEmpty || arguments.contains("--help") {
        print(
            """
            Usage: ges-conformance --corpus <suites-directory> --output <directory>
                                   [--fixtures <MarkdownV1-directory>] [--allow-incomplete]
                                   [--binary-fixtures <fixtures-directory>]
                                   [--runtime-programs <CSharp-export-directory>]
                                   [--performance | --calibrate-performance]

            Compiles and runs the shared Markdown corpus natively in Swift, emitting full JSON,
            Markdown, and compact results. --binary-fixtures supplies declared binary resources.
            --runtime-programs additionally verifies C#-compiled Programs in the Swift Runtime.
            --performance measures the hardware-matched Swift profile and checks its Markdown bounds.
            --calibrate-performance records samples without changing or approving any baseline.
            Missing Core capabilities remain errors. --allow-incomplete permits only those
            expected development errors in the process exit status; assertions still fail.
            """)
        return arguments.isEmpty ? 2 : 0
    }
    var corpus: String?
    var output: String?
    var fixtures: String?
    var runtimePrograms: String?
    var binaryFixtures: String?
    var allowIncomplete = false
    var measurePerformance = false
    var calibratePerformance = false
    while !arguments.isEmpty {
        let option = arguments.removeFirst()
        if option == "--performance" {
            measurePerformance = true
            continue
        }
        if option == "--calibrate-performance" {
            calibratePerformance = true
            continue
        }
        if option == "--allow-incomplete" {
            allowIncomplete = true
            continue
        }
        guard !arguments.isEmpty else { throw ToolError.invalidArguments("Missing value for \(option)") }
        let value = arguments.removeFirst()
        switch option {
        case "--corpus": corpus = value
        case "--output": output = value
        case "--fixtures": fixtures = value
        case "--runtime-programs": runtimePrograms = value
        case "--binary-fixtures": binaryFixtures = value
        default: throw ToolError.invalidArguments("Unknown option \(option)")
        }
    }
    guard let corpus, let output else { throw ToolError.invalidArguments("--corpus and --output are required") }
    if let fixtures { try verifyFixtures(URL(fileURLWithPath: fixtures)) }
    let corpusURL = URL(fileURLWithPath: corpus)
    guard
        let enumerator = FileManager.default.enumerator(at: corpusURL, includingPropertiesForKeys: [.isRegularFileKey])
    else {
        throw ToolError.invalidArguments("Cannot enumerate corpus")
    }
    let paths = enumerator.compactMap { $0 as? URL }.filter { $0.pathExtension == "md" }.sorted { $0.path < $1.path }
    let documents = try paths.map { path in
        do { return try ConformanceMarkdownParser.parse(Array(Data(contentsOf: path))) } catch {
            throw ToolError.invalidArguments("\(path.path): \(error)")
        }
    }
    let resolver = try binaryFixtures.map {
        try FileResources(directory: URL(fileURLWithPath: $0), documents: documents)
    }
    let provider = measurePerformance || calibratePerformance ? try SwiftPerformanceProvider() : nil
    let destination = URL(fileURLWithPath: output)
    try FileManager.default.createDirectory(at: destination, withIntermediateDirectories: true)
    if calibratePerformance {
        guard !measurePerformance, let provider else {
            throw ToolError.invalidArguments("Choose calibration or regression verification")
        }
        var count = 0
        for document in documents {
            for test in document.cases where test.kind == "performance" {
                let correctness = ConformanceRunner.runCase(
                    test, environment: .init(capabilities: ConformanceEnvironment.supportedCapabilities))
                // The backend's behavior execution is also required when no baseline exists yet.
                let workload = try ConformancePerformanceWorkload(test)
                let result = ConformanceRuntimeRunner.runCase(
                    test, programs: [.init(id: test.sources[0].programID, program: try workload.compile())])
                guard correctness.status == "skipped", result.status == "passed" else {
                    throw ToolError.invalidFixture("Performance correctness failed: " + test.fullID)
                }
                _ = try provider.measure(test, profile: SwiftPerformanceProvider.profile)
                print("Measured " + test.fullID)
                count += 1
            }
        }
        try provider.writeEvidence(destination, documents: documents)
        print("Calibration recorded \(count) workloads. This is measurement evidence, not a regression pass.")
        return 0
    }
    let environment = ConformanceEnvironment(
        capabilities: ConformanceEnvironment.supportedCapabilities + (measurePerformance ? ["performance"] : []),
        resourceResolver: resolver,
        performanceProfile: provider.map { _ in SwiftPerformanceProvider.profile }, performanceProvider: provider)
    let report = ConformanceRunner.runCorpus(documents, environment: environment)
    try provider?.writeEvidence(destination, documents: documents)
    let compact = try ConformanceReportWriter.crossLanguage(documents, report: report)
    for (name, text) in [
        ("ConformanceResults.json", ConformanceReportWriter.full(report)),
        ("ConformanceResults.md", ConformanceReportWriter.markdown(report)),
        ("SwiftResults.json", compact),
    ] { try Data(text.utf8).write(to: destination.appendingPathComponent(name), options: .atomic) }
    print(
        "Swift: \(report.count("passed")) passed, \(report.count("failed")) failed, \(report.count("error")) errors, \(report.count("skipped")) skipped; \(documents.count) documents."
    )
    var runtimePassed = true
    if let runtimePrograms {
        guard let binaryFixtures else {
            throw ToolError.invalidArguments("--runtime-programs requires --binary-fixtures")
        }
        let compactObject = try JSONSerialization.jsonObject(with: Data(compact.utf8)) as! [String: Any]
        runtimePassed = try verifyRuntime(
            documents, directory: URL(fileURLWithPath: runtimePrograms),
            binaryFixtures: URL(fileURLWithPath: binaryFixtures), destination: destination,
            corpus: compactObject["corpus"]!)
    }
    let failing = report.cases.filter {
        $0.status == "failed"
            || ($0.status == "error" && !(allowIncomplete && $0.code == "conformance.runner.missingCoreCapability"))
    }
    return failing.isEmpty && runtimePassed ? 0 : 1
}

/// Only the executable adapter translates declared resource IDs to bounded file reads.
struct FileResources: ConformanceResourceResolver {
    let paths: [String: URL]

    init(directory: URL, documents: [ConformanceDocument]) throws {
        let root = directory.standardizedFileURL.resolvingSymlinksInPath()
        var paths: [String: URL] = [:]
        for test in documents.flatMap(\.cases) {
            guard let fixture = test.metadata["binaryFixture"],
                let id = fixture["resourceId"]?.stringValue,
                let relative = fixture["relativePath"]?.stringValue
            else { continue }
            let path = root.appendingPathComponent(relative).standardizedFileURL.resolvingSymlinksInPath()
            guard path.path.hasPrefix(root.path + "/"), paths[id] == nil || paths[id] == path else {
                throw ToolError.invalidFixture("Invalid or conflicting resource path for \(id)")
            }
            paths[id] = path
        }
        self.paths = paths
    }

    func resolve(resourceID: String, maximumBytes: Int) -> ConformanceResourceResult {
        guard let path = paths[resourceID], FileManager.default.fileExists(atPath: path.path) else { return .notFound }
        do {
            let file = try FileHandle(forReadingFrom: path)
            defer { try? file.close() }
            let data = try file.read(upToCount: maximumBytes == Int.max ? Int.max : maximumBytes + 1) ?? Data()
            return data.count > maximumBytes ? .limitExceeded : .found(Array(data))
        } catch { return .error(String(describing: error)) }
    }
}

func verifyFixtures(_ directory: URL) throws {
    let manifest = try String(contentsOf: directory.appendingPathComponent("manifest.tsv"), encoding: .utf8)
    var count = 0
    for line in manifest.split(separator: "\n").dropFirst() {
        let fields = line.split(separator: "\t", omittingEmptySubsequences: false).map(String.init)
        guard fields.count == 8, fields[0] == "1" else { throw ToolError.invalidFixture("Invalid manifest") }
        let bytes = Array(try Data(contentsOf: directory.appendingPathComponent(fields[3])))
        guard ConformanceSha256.hex(bytes) == fields[4] else {
            throw ToolError.invalidFixture("Hash mismatch: \(fields[1])")
        }
        do {
            let document = try ConformanceMarkdownParser.parse(bytes)
            guard fields[2] == "valid", document.suiteID == fields[5], document.cases.map(\.id) == [fields[6]] else {
                throw ToolError.invalidFixture("Unexpected parse success/identity: \(fields[1])")
            }
        } catch let error as ConformanceParseError {
            guard fields[2] == "invalid", error.diagnostic.code == fields[7] else {
                throw ToolError.invalidFixture("Unexpected diagnostic: \(fields[1]): \(error)")
            }
        }
        count += 1
    }
    print("Markdown bootstrap: \(count) verified fixtures.")
}

do { exit(try run()) } catch {
    FileHandle.standardError.write(Data("ges-conformance: \(error)\n".utf8))
    exit(1)
}
