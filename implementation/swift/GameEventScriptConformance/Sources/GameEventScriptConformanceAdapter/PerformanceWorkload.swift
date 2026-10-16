// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// Prepared workload used only by platform measurement adapters; contains no clocks or instrumentation.
final class ConformancePerformanceWorkload {
    /// Validated authored case supplying sources, inputs and performance configuration.
    let test: Input
    /// Number of measured input batches.
    let iterations: Int
    /// Number of input batches requested before measurement.
    let warmupIterations: Int
    /// Number of compilation warmup iterations requested by the case.
    let compileWarmupIterations: Int
    /// Whether runtime observer callbacks participate in the workload.
    let observeRuntime: Bool
    let inputs: [GameEventScriptMessage]
    let budgets: [Int?]

    /// Prepares reusable inputs and frame budgets for a supported single-host performance case.
    ///
    /// - Throws: A conformance input error for unsupported workload shapes or invalid input messages.
    init(_ test: Input) throws {
        guard let config = test["measurement"], let count = config["iterations"]?.integerValue, count > 0,
            test["runtime"]?["hostCount"]?.integerValue == 1, !test.values("sources").isEmpty,
            Set(test.values("sources").compactMap { $0["program"]?.stringValue }).count == 1,
            test["runtime"]?.values("nativeHandlers").isEmpty == true,
            test["runtime"]?.values("deferredPrograms").isEmpty == true,
            test["runtime"]?["stepActions"]?.objectValue?.isEmpty == true,
            !(test["runtime"]?.values("steps").isEmpty ?? true),
            test["runtime"]!.values("steps").allSatisfy({ ["completion", "frames"].contains($0["pump"]?.stringValue ?? "") })
        else { throw ConformanceExecutionError.invalidInput("Unsupported performance workload") }
        self.test = test
        iterations = count
        warmupIterations = config["warmupIterations"]?.integerValue ?? 0
        compileWarmupIterations = config["compileWarmupIterations"]?.integerValue ?? 0
        observeRuntime = config["observeRuntime"]?.boolValue ?? true
        inputs = try test["runtime"]!.values("steps").map { try RuntimeValues.decodeMessage($0.required("input")) }
        budgets = test["runtime"]!.values("steps").map { $0["pump"]?.stringValue == "frames" ? $0["budget"]?.integerValue : nil }

    }

    /// Compiles the workload's single Program group, propagating compiler or invalid-input errors.
    func compile() throws -> GameEventScriptProgram {
        try CompilerObservations.compile(test)

    }

    /// Loads, starts and drains initialization on a fresh host outside the measurement interval.
    ///
    /// - Throws: A linking, runtime or conformance input error if preparation fails.
    func prepare(_ program: GameEventScriptProgram) throws -> ConformancePerformanceExecution { try .init(self, program) }
}

/// Integer counters establish that measured samples execute the prepared amount of work.
struct ConformanceWorkCounters: Equatable {
    /// Executed bytecode instructions.
    var opcodes = 0
    /// Processed logical messages.
    var messages = 0
    /// Emit operations reported by execution results.
    var emits = 0
    /// Publish operations reported by execution results.
    var publishes = 0
    /// Frames that paused before completing available work.
    var pauses = 0
    /// Handler-entry observer callbacks.
    var started = 0
    /// Handler-completion observer callbacks.
    var completed = 0
    /// Emit observer callbacks.
    var observedEmits = 0
    /// Publish observer callbacks.
    var observedPublishes = 0
    /// Outbound sink invocations.
    var outbound = 0
    /// Runtime-limit observer callbacks.
    var limits = 0
    /// Runtime-error observer callbacks.
    var errors = 0

    /// Creates zeroed counters.
    init() {}

    /// Multiplies every counter by a batch count for comparison with a measured run.
    func scaled(_ count: Int) -> Self {
        var value = self
        value.opcodes *= count
        value.messages *= count
        value.emits *= count
        value.publishes *= count
        value.pauses *= count
        value.started *= count
        value.completed *= count
        value.observedEmits *= count
        value.observedPublishes *= count
        value.outbound *= count
        value.limits *= count
        value.errors *= count
        return value
    }
}

/// A prepared host and reusable inputs owned by one synchronous measurement adapter.
final class ConformancePerformanceExecution {
    let workload: ConformancePerformanceWorkload
    let host: GameEventScriptHost
    private let observer: PerformanceObserver

    init(_ workload: ConformancePerformanceWorkload, _ program: GameEventScriptProgram) throws {
        self.workload = workload
        observer = PerformanceObserver(accept: workload.test["runtime"]?["publishSink"]?.stringValue != "reject")
        host = try HostScenario.makeHost(workload.test["runtime"]!, observer: workload.observeRuntime ? observer : nil, publishSink: workload.test["runtime"]?["publishSink"]?.stringValue == "absent" ? nil : observer)
        _ = try host.load(program)
        guard try host.start().state == .ready, try host.runToCompletion().state == .completed else { throw ConformanceExecutionError.invalidInput("Performance initialization failed") }
    }

    /// Executes repeated input batches and returns work counters, resetting observation counts for this call.
    ///
    /// - Throws: A conformance input error for negative counts, rejected inputs, faults or stalled execution.
    func run(iterations: Int) throws -> ConformanceWorkCounters {
        guard iterations >= 0 else { throw ConformanceExecutionError.invalidInput("Invalid iteration count") }
        observer.counts = .init()
        var counts = ConformanceWorkCounters()
        for _ in 0..<iterations {
            for index in workload.inputs.indices {
                guard host.receive(workload.inputs[index]) else { throw ConformanceExecutionError.invalidInput("Rejected measured input") }
                var frames = 0
                while true {
                    let result = try workload.budgets[index].map { try host.executeFrame(opcodeBudget: $0) } ?? host.runToCompletion()
                    counts.opcodes += result.executedOpcodes
                    counts.messages += result.processedMessages
                    counts.emits += result.emittedMessages
                    counts.publishes += result.publishedMessages
                    guard result.state == .completed || result.state == .paused else { throw ConformanceExecutionError.invalidInput("Measured workload failed") }
                    if result.state == .completed { break }
                    frames += 1
                    counts.pauses += 1
                    guard workload.budgets[index] != nil && result.executedOpcodes > 0 && frames <= 1_000_000 else { throw ConformanceExecutionError.invalidInput("Measured workload did not make progress") }
                }
            }
        }
        counts.started = observer.counts.started
        counts.completed = observer.counts.completed
        counts.observedEmits = observer.counts.observedEmits
        counts.observedPublishes = observer.counts.observedPublishes
        counts.outbound = observer.counts.outbound
        counts.limits = observer.counts.limits
        counts.errors = observer.counts.errors
        return counts
    }
}

private final class PerformanceObserver: GesRuntimeObserver, GesPublishSink {
    var counts = ConformanceWorkCounters()
    let accept: Bool

    init(accept: Bool) { self.accept = accept }

    func messageEmitted(_ message: GameEventScriptMessage, accepted: Bool) { counts.observedEmits += 1 }

    func messagePublished(_ message: GameEventScriptMessage, result: GesPublishResult) { counts.observedPublishes += 1 }

    func dispatchStarted(_ message: GameEventScriptMessage, signatureID: String) { counts.started += 1 }

    func dispatchCompleted(_ message: GameEventScriptMessage, signatureID: String) { counts.completed += 1 }

    func runtimeLimitReached(_ limitName: String, detail: String, limit: Int) { counts.limits += 1 }

    func runtimeError(_ diagnostic: GesDiagnostic) { counts.errors += 1 }

    func publish(_ message: GameEventScriptMessage) throws -> Bool {
        counts.outbound += 1
        return accept
    }
}
