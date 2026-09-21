// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// Prepared workload used only by platform measurement adapters; contains no clocks or instrumentation.
@_spi(Performance) public final class ConformancePerformanceWorkload {
    /// Validated authored case supplying sources, inputs and performance configuration.
    public let test: ConformanceCase
    /// Number of measured input batches.
    public let iterations: Int
    /// Number of input batches requested before measurement.
    public let warmupIterations: Int
    /// Number of compilation warmup iterations requested by the case.
    public let compileWarmupIterations: Int
    /// Whether runtime observer callbacks participate in the workload.
    public let observeRuntime: Bool
    let inputs: [GameEventScriptMessage]
    let budgets: [Int?]

    /// Prepares reusable inputs and frame budgets for a supported single-host performance case.
    ///
    /// - Throws: A conformance input error for unsupported workload shapes or invalid input messages.
    public init(_ test: ConformanceCase) throws {
        guard test.kind == "performance", let config = test.metadata["performance"], let count = config["iterations"]?.integerValue, count > 0, (test.metadata["hostCount"]?.integerValue ?? 1) == 1, !test.sources.isEmpty,
            Set(test.sources.map(\.programID)).count == 1, test.metadata.values("nativeHandlers").isEmpty, test.metadata.values("deferredPrograms").isEmpty, test.metadata["stepActions"]?.objectValue?.isEmpty != false, !test.steps.isEmpty,
            test.steps.allSatisfy({ ["completion", "frames"].contains($0.pump) })
        else { throw ConformanceExecutionError.invalidInput("Unsupported performance workload") }
        self.test = test
        iterations = count
        warmupIterations = config["warmupIterations"]?.integerValue ?? 0
        compileWarmupIterations = config["compileWarmupIterations"]?.integerValue ?? 0
        observeRuntime = config["observeRuntime"]?.boolValue ?? true
        inputs = try test.steps.map { step in
            let input = test.expectation["steps"]?[step.id]?["input"] ?? .object([])
            return try ConformanceRuntimeValueCodec.message(input.replacing("name", with: step.receive ?? .string("")))
        }
        budgets = test.steps.map { $0.pump == "frames" ? $0.budget : nil }
    }

    /// Compiles the workload's single Program group, propagating compiler or invalid-input errors.
    public func compile() throws -> GameEventScriptProgram {
        guard let program = try ConformanceCompilerRunner.compile(test).first?.program else { throw ConformanceExecutionError.invalidInput("Missing performance Program") }
        return program
    }

    /// Loads, starts and drains initialization on a fresh host outside the measurement interval.
    ///
    /// - Throws: A linking, runtime or conformance input error if preparation fails.
    public func prepare(_ program: GameEventScriptProgram) throws -> ConformancePerformanceExecution { try .init(self, program) }
}

/// Integer counters establish that measured samples execute the prepared amount of work.
@_spi(Performance) public struct ConformanceWorkCounters: Equatable {
    /// Executed bytecode instructions.
    public var opcodes = 0
    /// Processed logical messages.
    public var messages = 0
    /// Emit operations reported by execution results.
    public var emits = 0
    /// Publish operations reported by execution results.
    public var publishes = 0
    /// Frames that paused before completing available work.
    public var pauses = 0
    /// Handler-entry observer callbacks.
    public var started = 0
    /// Handler-completion observer callbacks.
    public var completed = 0
    /// Emit observer callbacks.
    public var observedEmits = 0
    /// Publish observer callbacks.
    public var observedPublishes = 0
    /// Outbound sink invocations.
    public var outbound = 0
    /// Runtime-limit observer callbacks.
    public var limits = 0
    /// Runtime-error observer callbacks.
    public var errors = 0

    /// Creates zeroed counters.
    public init() {}

    /// Multiplies every counter by a batch count for comparison with a measured run.
    public func scaled(_ count: Int) -> Self {
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
@_spi(Performance) public final class ConformancePerformanceExecution {
    let workload: ConformancePerformanceWorkload
    let host: GameEventScriptHost
    private let observer: PerformanceObserver

    init(_ workload: ConformancePerformanceWorkload, _ program: GameEventScriptProgram) throws {
        self.workload = workload
        observer = PerformanceObserver(accept: workload.test.metadata["publishSink"]?.stringValue != "reject")
        host = try RuntimeScenario.makeHost(workload.test, observer: workload.observeRuntime ? observer : nil, publishSink: workload.test.metadata["publishSink"]?.stringValue == "absent" ? nil : observer)
        _ = try host.load(program)
        guard try host.start().state == .ready, try host.runToCompletion().state == .completed else { throw ConformanceExecutionError.invalidInput("Performance initialization failed") }
    }

    /// Executes repeated input batches and returns work counters, resetting observation counts for this call.
    ///
    /// - Throws: A conformance input error for negative counts, rejected inputs, faults or stalled execution.
    public func run(iterations: Int) throws -> ConformanceWorkCounters {
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

private final class PerformanceObserver: GameEventScriptRuntimeObserver, GameEventScriptPublishSink {
    var counts = ConformanceWorkCounters()
    let accept: Bool

    init(accept: Bool) { self.accept = accept }

    func messageEmitted(_ message: GameEventScriptMessage, accepted: Bool) { counts.observedEmits += 1 }

    func messagePublished(_ message: GameEventScriptMessage, result: GameEventScriptPublishResult) { counts.observedPublishes += 1 }

    func dispatchStarted(_ message: GameEventScriptMessage, signatureID: String) { counts.started += 1 }

    func dispatchCompleted(_ message: GameEventScriptMessage, signatureID: String) { counts.completed += 1 }

    func runtimeLimitReached(_ limitName: String, detail: String, limit: Int) { counts.limits += 1 }

    func runtimeError(_ diagnostic: GameEventScriptDiagnostic) { counts.errors += 1 }

    func publish(_ message: GameEventScriptMessage) throws -> Bool {
        counts.outbound += 1
        return accept
    }
}
