// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Dispatch
import Foundation
import GameEventScriptRuntime

// Measures the steady state of a compiled program in this process; the contract it shares with `dotnet ges bench` is in
// the C# CLI README (In-process benchmark).
struct BenchOptions {
    var path = ""
    var entry = "Main"
    var warmup = 20
    var iterations = 50
    var minTimeMs = 0.0
    var freshInstance = false
    var gcBetween = false
    var perIteration = false
    var json = false
    var seed: Int64?
    var maxMessages = GesRuntimeLimits().maxProcessedMessagesPerRun
    var maxSteps = GesRuntimeLimits().maxExecutionSteps
}

// A failure that ends the benchmark with exit code 1, reported with its code.
struct BenchFailure: Error {
    let message: String
    var code = "cli.benchFailed"
}

// Records the console endpoints instead of writing them, and the first failure of a run.
final class BenchRecorder: GesNativeMessageHandler, GesRuntimeObserver {
    var messages: [GameEventScriptMessage] = []
    var failure: String?

    init() { messages.reserveCapacity(64) }

    func handle(_ message: GameEventScriptMessage, context: GesContext) throws { messages.append(message) }

    func runtimeLimitReached(_ limitName: String, detail: String, limit: Int) {
        if failure == nil { failure = "\(limitName)=\(limit): \(detail)" }
    }

    func runtimeError(_ diagnostic: GesDiagnostic) {
        if failure == nil { failure = "\(diagnostic.code): \(diagnostic.message)" }
    }
}

extension Tool {
    func bench(_ arguments: [String]) throws -> Int {
        if arguments == ["--help"] || arguments == ["-h"] {
            io.line(ToolHelp.bench)
            return 0
        }
        let options = try Self.benchOptions(arguments)
        do {
            return try measure(options)
        } catch let failure as BenchFailure {
            io.line("error \(failure.code): \(failure.message)", toError: true)
            return 1
        } catch let error as GesProgramFormatError {
            io.report(error, fallback: options.path)
            return 1
        }
    }

    static func benchOptions(_ arguments: [String]) throws -> BenchOptions {
        var options = BenchOptions()
        var supplied: Set<String> = []
        var index = 0

        func count(_ option: String, _ value: String, _ minimum: Int) throws -> Int {
            guard value.allSatisfy(\.isASCII), value.allSatisfy(\.isNumber), let parsed = Int32(value).map(Int.init), parsed >= minimum else {
                throw ToolError.usage("\(option) requires an integer of at least \(minimum).")
            }
            return parsed
        }

        while index < arguments.count {
            let argument = arguments[index]
            switch argument {
            case "--fresh-instance": options.freshInstance = true
            case "--gc-between": options.gcBetween = true
            case "--per-iteration": options.perIteration = true
            case "--json": options.json = true
            case "--entry", "--warmup", "--iterations", "--min-time", "--seed", "--max-messages", "--max-steps":
                guard index + 1 < arguments.count else { throw ToolError.usage("Specify a value after \(argument).") }
                guard supplied.insert(argument).inserted else { throw ToolError.usage("Specify \(argument) only once.") }
                index += 1
                let value = arguments[index]
                switch argument {
                case "--entry":
                    guard !value.trimmingCharacters(in: .whitespaces).isEmpty, !value.hasPrefix("-") else { throw ToolError.usage("Specify the entry message name after --entry.") }
                    options.entry = value
                case "--warmup": options.warmup = try count(argument, value, 0)
                case "--iterations": options.iterations = try count(argument, value, 1)
                case "--max-messages": options.maxMessages = try count(argument, value, 1)
                case "--max-steps": options.maxSteps = try count(argument, value, 1)
                case "--min-time":
                    guard value.allSatisfy({ $0.isASCII && ($0.isNumber || $0 == ".") }), let milliseconds = Double(value), milliseconds.isFinite else {
                        throw ToolError.usage("--min-time requires a nonnegative number of milliseconds.")
                    }
                    options.minTimeMs = milliseconds
                default:
                    guard let seed = Int64(value) else { throw ToolError.usage("The seed must be a signed 64-bit integer.") }
                    options.seed = seed
                }
            default:
                if argument.hasPrefix("-") { throw ToolError.usage("Unknown bench option '\(argument)'.") }
                guard options.path.isEmpty else { throw ToolError.usage("Specify exactly one compiled program.") }
                guard argument.lowercased().hasSuffix(".gesb") else {
                    throw ToolError.usage("bench measures a compiled .gesb and never compiles; compile the source with 'ges compile' first.")
                }
                options.path = argument
            }
            index += 1
        }
        guard !options.path.isEmpty else { throw ToolError.usage("Specify the compiled program to measure.") }
        return options
    }

    private func measure(_ options: BenchOptions) throws -> Int {
        // Load: read, decode and validate, then build, link and initialize the Host.
        let loadStarted = Self.now()
        let program = try GameEventScriptProgram.read(Array(Data(contentsOf: URL(fileURLWithPath: options.path))))
        let entry = try Self.entryMessage(program, options.entry)
        var (host, recorder) = try Self.createHost(program, options)
        let loadMs = Self.milliseconds(Self.now() - loadStarted)

        var instanceTimes: [Double] = []

        func instance() throws -> GameEventScriptHost {
            guard options.freshInstance else { return host }
            let started = Self.now()
            (host, recorder) = try Self.createHost(program, options)
            instanceTimes.append(Self.milliseconds(Self.now() - started))
            return host
        }

        // Warm-up: the given count, then on until --min-time has passed. Reference counting needs no collection.
        let warmupStarted = Self.now()
        var warmups = 0
        while warmups < options.warmup || Self.milliseconds(Self.now() - warmupStarted) < options.minTimeMs {
            let current = try instance()
            var ticks: UInt64 = 0
            if let failure = try Self.runOnce(current, recorder, entry, &ticks) { throw BenchFailure(message: "warm-up \(warmups + 1): \(failure)") }
            warmups += 1
        }
        instanceTimes.removeAll()

        var samples: [Double] = []
        samples.reserveCapacity(options.iterations)
        var checksum = BenchChecksum()
        for iteration in 0..<options.iterations {
            let current = try instance()
            var ticks: UInt64 = 0
            if let failure = try Self.runOnce(current, recorder, entry, &ticks) { throw BenchFailure(message: "iteration \(iteration + 1): \(failure)") }
            samples.append(Self.milliseconds(ticks))
            for message in recorder.messages { checksum.add(message) }
        }

        let report = BenchReport(
            options: options,
            loadMs: loadMs,
            warmups: warmups,
            samples: samples,
            checksum: checksum.hex,
            instanceMedianMs: instanceTimes.isEmpty ? nil : BenchReport.quantile(instanceTimes.sorted(), 0.5)
        )
        io.write(options.json ? report.json() : report.text(), toError: false)
        return 0
    }

    // `<entry>(args: [])` for an `<entry>(args)` handler, otherwise `<entry>()`, as `run` sends Main(args).
    static func entryMessage(_ program: GameEventScriptProgram, _ entry: String) throws -> GameEventScriptMessage {
        // Only the host sends its system endpoints; no message can name them.
        if entry == "initialization" || entry == "undeliverable" {
            throw BenchFailure(message: "\(entry) is a system endpoint that only the host sends. Choose another message with --entry.", code: "cli.missingEntry")
        }
        var withArgs = false
        var withoutArgs = false
        for binding in program.bindings where binding.requiredTags.isEmpty && program.stringConstants[Int(binding.name)] == entry {
            if binding.kind == .messageNameHandler { withoutArgs = true }
            guard binding.kind == .messageHandler else { continue }
            if binding.argumentNames.isEmpty { withoutArgs = true }
            if binding.argumentNames.count == 1 && program.stringConstants[Int(binding.argumentNames[0])] == "args" { withArgs = true }
        }
        if withArgs { return try GameEventScriptMessage(name: entry, arguments: [.init(name: "args", value: .list([]))]) }
        if withoutArgs { return try GameEventScriptMessage(name: entry) }
        throw BenchFailure(message: "No handler matches \(entry)(args) or \(entry)(). Choose another message with --entry.", code: "cli.missingEntry")
    }

    static func createHost(_ program: GameEventScriptProgram, _ options: BenchOptions) throws -> (GameEventScriptHost, BenchRecorder) {
        let recorder = BenchRecorder()
        var limits = GesRuntimeLimits()
        limits.maxProcessedMessagesPerRun = options.maxMessages
        limits.maxExecutionSteps = options.maxSteps
        let builder = GameEventScriptHost.makeBuilder().withRuntimeObserver(recorder).withRuntimeLimits(limits)
        if let seed = options.seed { builder.withRandomSeed(seed) }
        let host = try builder.build()
        for endpoint in ["ConsoleOut", "ConsoleErr", "ErrorCode"] { _ = try host.subscribeMessageName(endpoint, handler: recorder) }
        _ = try host.load(program)
        let start = try host.start()
        guard start.state == .ready else {
            let reason = start.diagnostic.map { "\($0.code): \($0.message)" } ?? recorder.failure ?? String(describing: start.state)
            throw BenchFailure(message: "initialization: \(reason)")
        }
        return (host, recorder)
    }

    // One run: send the entry message and pump until the Host is idle; only that is timed.
    static func runOnce(_ host: GameEventScriptHost, _ recorder: BenchRecorder, _ entry: GameEventScriptMessage, _ ticks: inout UInt64) throws -> String? {
        recorder.messages.removeAll(keepingCapacity: true)
        let started = now()
        let accepted = host.receive(entry)
        let result = try host.runToCompletion()
        ticks = now() - started
        if !accepted { return "the host rejected the entry message" }
        if let failure = recorder.failure { return failure }
        if result.state == .waiting { return "delayed messages remain; a benchmark run must finish without waiting" }
        if result.state != .completed { return result.diagnostic.map { "\($0.code): \($0.message)" } ?? String(describing: result.state) }
        for message in recorder.messages where message.name == "ErrorCode" && message.arguments.count == 1 {
            let code = message.arguments[0]
            if !code.isNothing && code.asNumber != 0 { return "the script set ErrorCode(\(code.description))" }
        }
        return nil
    }

    static func now() -> UInt64 { DispatchTime.now().uptimeNanoseconds }

    static func milliseconds(_ nanoseconds: UInt64) -> Double { Double(nanoseconds) / 1_000_000 }
}

// FNV-1a 64 over one UTF-8 line per recorded message: its name, ':' and its arguments' texts.
struct BenchChecksum {
    private var hash: UInt64 = 14_695_981_039_346_656_037

    var hex: String {
        let digits = String(hash, radix: 16)
        return String(repeating: "0", count: 16 - digits.count) + digits
    }

    mutating func add(_ message: GameEventScriptMessage) {
        var line = message.name + ":"
        for index in 0..<message.arguments.count { line += message.arguments[index].description }
        for byte in (line + "\n").utf8 {
            hash ^= UInt64(byte)
            hash = hash &* 1_099_511_628_211
        }
    }
}

struct BenchReport {
    let options: BenchOptions
    let loadMs: Double
    let warmups: Int
    let samples: [Double]
    let checksum: String
    let instanceMedianMs: Double?

    static func quantile(_ sorted: [Double], _ q: Double) -> Double {
        let position = Double(sorted.count - 1) * q
        let lower = Int(position.rounded(.down))
        let upper = min(lower + 1, sorted.count - 1)
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - Double(lower))
    }

    private var statistics: (min: Double, median: Double, mean: Double, p95: Double, max: Double, stddev: Double) {
        let sorted = samples.sorted()
        let mean = samples.reduce(0, +) / Double(samples.count)
        let variance = samples.count < 2 ? 0 : samples.reduce(0) { $0 + ($1 - mean) * ($1 - mean) } / Double(samples.count - 1)
        return (sorted[0], Self.quantile(sorted, 0.5), mean, Self.quantile(sorted, 0.95), sorted[sorted.count - 1], variance.squareRoot())
    }

    private static var os: String {
        #if os(macOS)
            "macos"
        #elseif os(Windows)
            "windows"
        #else
            "linux"
        #endif
    }

    private static var arch: String {
        #if arch(arm64)
            "arm64"
        #else
            "x64"
        #endif
    }

    // The compiler version, which on Swift is the runtime that matters for generated code.
    private static var runtimeVersion: String {
        #if compiler(>=6.5)
            "6.5+"
        #elseif compiler(>=6.4)
            "6.4"
        #elseif compiler(>=6.3)
            "6.3"
        #elseif compiler(>=6.2)
            "6.2"
        #elseif compiler(>=6.1)
            "6.1"
        #else
            "6.0"
        #endif
    }

    private static func number(_ value: Double, _ decimals: Int) -> String { String(format: "%.\(decimals)f", value) }

    func text() -> String {
        let s = statistics
        var text = "Benchmark: \(options.path), entry \(options.entry)\n"
        text += "Runtime: swift \(Self.runtimeVersion) (\(Self.os) \(Self.arch))\n"
        text += "Tiering: n/a\n"
        text += "Load: \(Self.number(loadMs, 3)) ms\n"
        if let instance = instanceMedianMs { text += "Instance creation (median): \(Self.number(instance, 3)) ms\n" }
        text += "Warm-up: \(warmups) runs\n"
        text += "Iterations: \(samples.count)\n"
        if options.perIteration {
            for (index, sample) in samples.enumerated() { text += "Run \(index + 1): \(Self.number(sample, 3)) ms\n" }
        }
        text += "Time per run (ms): min \(Self.number(s.min, 3)), median \(Self.number(s.median, 3)), mean \(Self.number(s.mean, 3)), "
        text += "p95 \(Self.number(s.p95, 3)), max \(Self.number(s.max, 3)), stddev \(Self.number(s.stddev, 3))\n"
        text += "Allocated per run: n/a\n"
        text += "Checksum: \(checksum)\n"
        return text
    }

    func json() -> String {
        let s = statistics
        var members: [(String, String)] = [
            ("runtime", quoted("swift")),
            ("runtimeVersion", quoted(Self.runtimeVersion)),
            ("os", quoted(Self.os)),
            ("arch", quoted(Self.arch)),
            ("script", quoted(options.path)),
            ("entry", quoted(options.entry)),
            ("freshInstance", options.freshInstance ? "true" : "false"),
            ("loadMs", Self.number(loadMs, 6)),
            ("instanceMedianMs", instanceMedianMs.map { Self.number($0, 6) } ?? "null"),
            ("warmup", String(warmups)),
            ("iterations", String(samples.count)),
            ("minMs", Self.number(s.min, 6)),
            ("medianMs", Self.number(s.median, 6)),
            ("meanMs", Self.number(s.mean, 6)),
            ("p95Ms", Self.number(s.p95, 6)),
            ("maxMs", Self.number(s.max, 6)),
            ("stddevMs", Self.number(s.stddev, 6)),
            ("allocatedBytesPerIteration", "null"),
            ("gen0Collections", "null"),
            ("gen1Collections", "null"),
            ("gen2Collections", "null"),
            ("tieredCompilation", "null"),
            ("tieredPgo", "null"),
            ("readyToRun", "null"),
            ("checksum", quoted(checksum)),
        ]
        if options.perIteration { members.append(("samplesMs", "[" + samples.map { Self.number($0, 6) }.joined(separator: ", ") + "]")) }
        return "{\n" + members.map { "  \"\($0.0)\": \($0.1)" }.joined(separator: ",\n") + "\n}\n"
    }
}
