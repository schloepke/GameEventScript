// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import ConformanceInstrumentation
import Foundation
@_spi(Performance) import GameEventScriptConformance
import GameEventScriptRuntime

/// Native instrumentation never enters the portable library or shipped Runtime.
final class SwiftPerformanceProvider: ConformancePerformanceProvider, @unchecked Sendable {
    static let profile = "swift-6.4-release-macos26-arm64-m3max"
    private let lock = NSLock()
    private var evidence: [[String: Any]] = []
    private var escapingControl: AnyObject?
    let sampleCount = 5
    let provenance: [String: Any]
    struct Sample {
        let elapsed: Double
        let bytes: UInt64
        let allocations: UInt64
        var json: [String: Any] {
            ["elapsedMilliseconds": elapsed, "allocatedBytes": bytes, "allocationCount": allocations]
        }
    }
    init() throws {
        #if DEBUG
            throw ToolError.invalidArguments("Performance measurements require a Release build")
        #endif
        #if !os(macOS) || !arch(arm64) || !compiler(>=6.4) || compiler(>=6.5)
            throw ToolError.invalidArguments("This profile requires Swift 6.4 on macOS arm64")
        #endif
        guard ProcessInfo.processInfo.operatingSystemVersion.majorVersion == 26 else {
            throw ToolError.invalidArguments("This profile requires macOS 26")
        }
        guard ges_allocation_install(), ges_allocation_controls() else {
            throw ToolError.invalidArguments("Allocation instrumentation controls failed or another logger is active")
        }
        provenance = [
            "profile": Self.profile, "processID": ProcessInfo.processInfo.processIdentifier,
            "measurementID": UUID().uuidString, "measuredAt": ISO8601DateFormatter().string(from: Date()),
            "operatingSystem": ProcessInfo.processInfo.operatingSystemVersionString,
            "architecture": "arm64", "configuration": "Release", "memoryManagement": "Swift ARC",
            "toolchain": try Self.command("/usr/bin/xcrun", ["swift", "--version"]),
            "sdk": try Self.command("/usr/bin/xcrun", ["--show-sdk-version"]),
            "cpu": try Self.command("/usr/sbin/sysctl", ["-n", "machdep.cpu.brand_string"]),
            "instrumentation": "libmalloc malloc_logger; cumulative requested bytes on the calling thread",
            "coverageGaps": [
                "Other threads", "Direct mmap/mach_vm_allocate", "Private allocators bypassing libmalloc entry points",
                "Stack storage",
            ],
            "controls":
                "malloc, calloc, realloc, zone malloc, aligned allocation, transient frees, empty interval; Swift escaping object and Array/String below",
            "sampleCount": sampleCount, "elapsedAggregation": "median", "allocationAggregation": "maximum",
            "timingScope": "Monotonic elapsed time with allocation instrumentation active",
            "runScope":
                "Reused inputs, enqueue and completion/frame pumping; excludes compile/link/init/warmup and report construction",
            "compileScope":
                "Builder, source parse, validation, lowering, optimization, Program validation and requested binary roundtrip",
            "loadScope": "Fresh Host construction, linking and initialization",
        ]
        guard provenance["cpu"] as? String == "Apple M3 Max" else {
            throw ToolError.invalidArguments(
                "The timing baseline requires Apple M3 Max; calibrate a separate profile for other hardware")
        }
        try swiftControls()
    }
    private static func command(_ path: String, _ args: [String]) throws -> String {
        let process = Process()
        let output = Pipe()
        process.executableURL = URL(fileURLWithPath: path)
        process.arguments = args
        process.standardOutput = output
        process.standardError = Pipe()
        try process.run()
        let data = output.fileHandleForReading.readDataToEndOfFile()
        process.waitUntilExit()
        guard process.terminationStatus == 0 else {
            throw ToolError.invalidArguments("Cannot identify measurement environment")
        }
        return String(decoding: data, as: UTF8.self).trimmingCharacters(in: .whitespacesAndNewlines)
    }
    private final class ControlObject {
        let data: [UInt8]
        let text: String
        init() {
            data = Array(repeating: 7, count: 1024)
            text = String(repeating: "GES", count: 1024)
        }
    }
    @inline(never) private func allocateControl() { escapingControl = ControlObject() }
    private func swiftControls() throws {
        allocateControl()
        escapingControl = nil
        let (_, empty) = try sample { 42 }
        let (_, positive) = try sample { self.allocateControl() }
        guard empty.bytes == 0, empty.allocations == 0, positive.bytes >= 4096, positive.allocations >= 3 else {
            throw ToolError.invalidArguments("Swift allocation instrumentation controls failed")
        }
        escapingControl = nil
    }
    private func sample<T>(_ action: () throws -> T) throws -> (T, Sample) {
        let start = ges_monotonic_nanoseconds()
        guard ges_allocation_begin() else { throw ToolError.invalidArguments("Cannot start allocation measurement") }
        let value: T
        do { value = try action() } catch {
            _ = ges_allocation_end()
            throw error
        }
        let allocation = ges_allocation_end()
        let elapsed = Double(ges_monotonic_nanoseconds() - start) / 1_000_000
        guard allocation.valid else { throw ToolError.invalidArguments("Invalid allocation counter") }
        return (value, .init(elapsed: elapsed, bytes: allocation.bytes, allocations: allocation.allocations))
    }
    func measure(_ test: ConformanceCase, profile: String) throws -> [ConformanceMeasuredMetric] {
        lock.lock()
        defer { lock.unlock() }
        guard profile == Self.profile else { throw ToolError.invalidArguments("Unsupported performance profile") }
        let workload = try ConformancePerformanceWorkload(test)
        for _ in 0..<workload.compileWarmupIterations { _ = try workload.prepare(workload.compile()) }
        var compile: [Sample] = []
        var load: [Sample] = []
        var run: [Sample] = []
        var counters: [[String: Any]] = []
        for _ in 0..<sampleCount {
            let (program, compiling) = try sample { try workload.compile() }
            compile.append(compiling)
            let (execution, loading) = try sample { try workload.prepare(program) }
            load.append(loading)
            _ = try execution.run(iterations: workload.warmupIterations)
            let expected = try execution.run(iterations: 1)
            guard expected.opcodes > 0, expected.messages >= test.steps.count,
                !workload.observeRuntime || expected.started > 0 && expected.started == expected.completed,
                expected.errors == 0, expected.limits == 0
            else { throw ToolError.invalidArguments("Invalid measured work") }
            let (actual, running) = try sample { try execution.run(iterations: workload.iterations) }
            guard actual == expected.scaled(workload.iterations) else {
                throw ToolError.invalidArguments("Measured work/callback counters differ from warmup")
            }
            run.append(running)
            counters.append([
                "opcodes": actual.opcodes, "messages": actual.messages, "emits": actual.emits,
                "publishes": actual.publishes, "pauses": actual.pauses, "dispatchStarted": actual.started,
                "dispatchCompleted": actual.completed,
                "observedEmits": actual.observedEmits, "observedPublishes": actual.observedPublishes,
                "outbound": actual.outbound,
            ])
        }
        var metrics: [ConformanceMeasuredMetric] = []
        for (name, samples) in [("compile", compile), ("program-load", load), ("run", run)] {
            let elapsed = samples.map(\.elapsed).sorted()[sampleCount / 2]
            let allocated = Double(samples.map(\.bytes).max()!)
            metrics.append(.init(id: name + ".elapsed", value: elapsed, unit: "ms"))
            metrics.append(.init(id: name + ".allocated", value: allocated, unit: "B"))
            if name == "run" {
                metrics.append(
                    .init(id: "run.allocations", value: Double(samples.map(\.allocations).max()!), unit: "count"))
                metrics.append(
                    .init(
                        id: "run.per-invoke-elapsed", value: elapsed / Double(workload.iterations), unit: "ms/iteration"
                    ))
                metrics.append(
                    .init(
                        id: "run.per-invoke-allocated", value: allocated / Double(workload.iterations),
                        unit: "B/iteration"))
            }
        }
        evidence.append([
            "id": test.fullID, "iterations": workload.iterations, "warmupIterations": workload.warmupIterations,
            "observeRuntime": workload.observeRuntime, "compile": compile.map(\.json), "load": load.map(\.json),
            "run": run.map(\.json),
            "work": counters,
            "metrics": Dictionary(
                uniqueKeysWithValues: metrics.map { ($0.id, ["measured": $0.value, "unit": $0.unit] as [String: Any]) }),
        ])
        return metrics
    }
    func writeEvidence(_ directory: URL, documents: [ConformanceDocument]) throws {
        lock.lock()
        defer { lock.unlock() }
        let identity = try ConformanceReportWriter.identify(documents)
        let json: [String: Any] = [
            "schemaVersion": 1, "provenance": provenance, "cases": evidence,
            "corpus": [
                "sha256": identity.sha256, "documentCount": identity.documentCount, "caseCount": identity.caseCount,
            ],
            "documents": documents.map { ["suiteId": $0.suiteID, "sha256": ConformanceSha256.hex($0.sourceBytes)] },
        ]

        try JSONSerialization.data(withJSONObject: json, options: [.prettyPrinted, .sortedKeys]).write(
            to: directory.appendingPathComponent("SwiftPerformance.measurement.json"), options: .atomic)
    }
}
