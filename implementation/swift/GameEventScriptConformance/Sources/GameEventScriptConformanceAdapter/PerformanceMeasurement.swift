// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import ConformanceInstrumentation
import Foundation
import GameEventScriptRuntime

#if canImport(Darwin)
    import Darwin
#endif

/// Native instrumentation never enters the portable library or shipped Runtime.
final class SwiftPerformanceProvider: @unchecked Sendable {
    static let profile = "swift-6.4-release-macos27-arm64-m3max"
    private let lock = NSLock()
    private var evidence: [[String: Any]] = []
    private var escapingControl: AnyObject?
    let sampleCount = 5
    let provenance: [String: Any]

    struct Sample {
        let elapsed: Double
        let bytes: UInt64
        let allocations: UInt64
        var json: [String: Any] { ["elapsedMilliseconds": elapsed, "allocatedBytes": bytes, "allocationCount": allocations] }
    }

    init() throws {
        #if DEBUG
            throw ConformanceExecutionError.invalidInput("Performance measurements require a Release build")
        #endif
        #if !os(macOS) || !arch(arm64) || !compiler(>=6.4) || compiler(>=6.5)
            throw ConformanceExecutionError.invalidInput("This profile requires Swift 6.4 on macOS arm64")
        #endif
        guard ProcessInfo.processInfo.operatingSystemVersion.majorVersion == 27 else { throw ConformanceExecutionError.invalidInput("This profile requires macOS 27") }
        guard ges_allocation_install(), ges_allocation_controls() else { throw ConformanceExecutionError.invalidInput("Allocation instrumentation controls failed or another logger is active") }
        provenance = [
            "profile": Self.profile, "processID": ProcessInfo.processInfo.processIdentifier, "measurementID": UUID().uuidString, "measuredAt": ISO8601DateFormatter().string(from: Date()),
            "operatingSystem": ProcessInfo.processInfo.operatingSystemVersionString, "architecture": "arm64", "configuration": "Release", "memoryManagement": "Swift ARC", "toolchain": "Swift 6.4 (compile-time profile gate)",
            "sdk": "toolchain default", "cpu": Self.cpu(),
            "instrumentation": "libmalloc malloc_logger; cumulative requested bytes on the calling thread", "coverageGaps": ["Other threads", "Direct mmap/mach_vm_allocate", "Private allocators bypassing libmalloc entry points", "Stack storage"],
            "controls": "malloc, calloc, realloc, zone malloc, aligned allocation, transient frees, empty interval; Swift escaping object and Array/String below", "sampleCount": sampleCount, "elapsedAggregation": "median",
            "allocationAggregation": "maximum", "timingScope": "Monotonic elapsed time with allocation instrumentation active",
            "runScope": "Reused inputs, enqueue and completion/frame pumping; excludes compile/link/init/warmup and report construction",
            "compileScope": "Builder, source parse, validation, lowering, optimization, Program validation and requested binary roundtrip", "loadScope": "Fresh Host construction, linking and initialization",
        ]
        guard provenance["cpu"] as? String == "Apple M3 Max" else { throw ConformanceExecutionError.invalidInput("The timing baseline requires Apple M3 Max; calibrate a separate profile for other hardware") }
        try swiftControls()
    }

    private static func cpu() -> String {
        #if canImport(Darwin)
            var count = 0
            guard sysctlbyname("machdep.cpu.brand_string", nil, &count, nil, 0) == 0 else { return "unknown" }
            var bytes = [CChar](repeating: 0, count: count)
            guard sysctlbyname("machdep.cpu.brand_string", &bytes, &count, nil, 0) == 0 else { return "unknown" }
            return String(decoding: bytes.prefix { $0 != 0 }.map { UInt8(bitPattern: $0) }, as: UTF8.self)
        #else
            return "unknown"
        #endif
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
        guard empty.bytes == 0, empty.allocations == 0, positive.bytes >= 4096, positive.allocations >= 3 else { throw ConformanceExecutionError.invalidInput("Swift allocation instrumentation controls failed") }
        escapingControl = nil
    }

    private func sample<T>(_ action: () throws -> T) throws -> (T, Sample) {
        let start = ges_monotonic_nanoseconds()
        guard ges_allocation_begin() else { throw ConformanceExecutionError.invalidInput("Cannot start allocation measurement") }
        let value: T
        do { value = try action() } catch {
            _ = ges_allocation_end()
            throw error
        }
        let allocation = ges_allocation_end()
        let elapsed = Double(ges_monotonic_nanoseconds() - start) / 1_000_000
        guard allocation.valid else { throw ConformanceExecutionError.invalidInput("Invalid allocation counter") }
        return (value, .init(elapsed: elapsed, bytes: allocation.bytes, allocations: allocation.allocations))
    }

    func measure(_ test: Input, profile: String) throws -> [ConformanceMeasuredMetric] {
        lock.lock()
        defer { lock.unlock() }
        guard profile == Self.profile else { throw ConformanceExecutionError.invalidInput("Unsupported performance profile") }
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
            guard expected.opcodes > 0, expected.messages >= workload.inputs.count, !workload.observeRuntime || expected.started > 0 && expected.started == expected.completed, expected.errors == 0, expected.limits == 0 else {
                throw ConformanceExecutionError.invalidInput("Invalid measured work")
            }
            let (actual, running) = try sample { try execution.run(iterations: workload.iterations) }
            guard actual == expected.scaled(workload.iterations) else { throw ConformanceExecutionError.invalidInput("Measured work/callback counters differ from warmup") }
            run.append(running)
            counters.append([
                "opcodes": actual.opcodes, "messages": actual.messages, "emits": actual.emits, "publishes": actual.publishes, "pauses": actual.pauses, "dispatchStarted": actual.started, "dispatchCompleted": actual.completed,
                "observedEmits": actual.observedEmits, "observedPublishes": actual.observedPublishes, "outbound": actual.outbound,
            ])
        }
        var metrics: [ConformanceMeasuredMetric] = []
        for (name, samples) in [("compile", compile), ("program-load", load), ("run", run)] {
            let elapsed = samples.map(\.elapsed).sorted()[sampleCount / 2]
            let allocated = Double(samples.map(\.bytes).max()!)
            metrics.append(.init(id: name + ".elapsed", value: elapsed, unit: "ms"))
            metrics.append(.init(id: name + ".allocated", value: allocated, unit: "B"))
            if name == "run" {
                metrics.append(.init(id: "run.allocations", value: Double(samples.map(\.allocations).max()!), unit: "count"))
                metrics.append(.init(id: "run.per-invoke-elapsed", value: elapsed / Double(workload.iterations), unit: "ms/iteration"))
                metrics.append(.init(id: "run.per-invoke-allocated", value: allocated / Double(workload.iterations), unit: "B/iteration"))
            }
        }
        evidence.append([
            "id": try test.text("id"), "iterations": workload.iterations, "warmupIterations": workload.warmupIterations, "observeRuntime": workload.observeRuntime, "compile": compile.map(\.json), "load": load.map(\.json), "run": run.map(\.json),
            "work": counters, "metrics": Dictionary(uniqueKeysWithValues: metrics.map { ($0.id, ["measured": $0.value, "unit": $0.unit] as [String: Any]) }),
        ])
        return metrics
    }

    func observation(_ test: Input) throws -> [String: Any] {
        let requested = try test.required("measurement")
        let measured = try measure(test, profile: requested.text("profile"))
        let metrics = try requested.values("metrics").map { metric -> [String: Any] in
            guard let value = measured.first(where: { $0.id == metric["id"]?.stringValue }), value.unit == metric["unit"]?.stringValue else { throw ConformanceExecutionError.invalidInput("Unsupported metric") }
            return ["id": value.id, "value": Values.numberText(value.value), "unit": value.unit]
        }
        return ["status": "ok", "measurement": ["profile": Self.profile, "metrics": metrics], "evidence": ["provenance": provenance, "samples": evidence.last!]]
    }

    static var available: Bool {
        #if DEBUG || !os(macOS) || !arch(arm64) || !compiler(>=6.4) || compiler(>=6.5)
            return false
        #else
            return ProcessInfo.processInfo.operatingSystemVersion.majorVersion == 27 && cpu() == "Apple M3 Max"
        #endif
    }
}

struct ConformanceMeasuredMetric {
    let id: String
    let value: Double
    let unit: String
}
