// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// Prepared workload used only by platform measurement adapters; contains no clocks or instrumentation.
@_spi(Performance) public final class ConformancePerformanceWorkload {
    public let test: ConformanceCase
    public let iterations: Int
    public let warmupIterations: Int
    public let compileWarmupIterations: Int
    public let observeRuntime: Bool
    let inputs: [GameEventScriptMessage]
    let budgets: [Int?]
    public init(_ test: ConformanceCase) throws {
        guard test.kind == "performance", let config = test.metadata["performance"],
            let count = config["iterations"]?.integerValue, count > 0,
            (test.metadata["hostCount"]?.integerValue ?? 1) == 1,
            !test.sources.isEmpty, Set(test.sources.map(\.programID)).count == 1,
            test.metadata.values("nativeHandlers").isEmpty,
            test.metadata.values("deferredPrograms").isEmpty,
            test.metadata["stepActions"]?.objectValue?.isEmpty != false,
            !test.steps.isEmpty, test.steps.allSatisfy({ ["completion", "frames"].contains($0.pump) })
        else {
            throw ConformanceExecutionError.invalidInput("Unsupported performance workload")
        }
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
    public func compile() throws -> GameEventScriptProgram {
        guard let program = try ConformanceCompilerRunner.compile(test).first?.program else {
            throw ConformanceExecutionError.invalidInput("Missing performance Program")
        }
        return program
    }
    public func prepare(_ program: GameEventScriptProgram) throws -> ConformancePerformanceExecution {
        try .init(self, program)
    }
}

/// Integer counters establish that measured samples execute the prepared amount of work.
@_spi(Performance) public struct ConformanceWorkCounters: Equatable {
    public var opcodes = 0, messages = 0, emits = 0, publishes = 0, pauses = 0
    public var started = 0, completed = 0, observedEmits = 0, observedPublishes = 0, outbound = 0
    public var limits = 0, errors = 0
    public init() {}
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

@_spi(Performance) public final class ConformancePerformanceExecution {
    let workload: ConformancePerformanceWorkload
    let host: GameEventScriptHost
    private let observer: PerformanceObserver
    init(_ workload: ConformancePerformanceWorkload, _ program: GameEventScriptProgram) throws {
        self.workload = workload
        observer = PerformanceObserver(accept: workload.test.metadata["publishSink"]?.stringValue != "reject")
        host = try RuntimeScenario.makeHost(
            workload.test, observer: workload.observeRuntime ? observer : nil,
            publishSink: workload.test.metadata["publishSink"]?.stringValue == "absent" ? nil : observer)
        _ = try host.load(program)
        guard try host.runToCompletion().state == .completed else {
            throw ConformanceExecutionError.invalidInput("Performance initialization failed")
        }
    }
    public func run(iterations: Int) throws -> ConformanceWorkCounters {
        guard iterations >= 0 else { throw ConformanceExecutionError.invalidInput("Invalid iteration count") }
        observer.counts = .init()
        var counts = ConformanceWorkCounters()
        for _ in 0..<iterations {
            for index in workload.inputs.indices {
                guard host.receive(workload.inputs[index]) else {
                    throw ConformanceExecutionError.invalidInput("Rejected measured input")
                }
                var frames = 0
                while true {
                    let result =
                        try workload.budgets[index].map { try host.executeFrame(opcodeBudget: $0) }
                        ?? host.runToCompletion()
                    counts.opcodes += result.executedOpcodes
                    counts.messages += result.processedMessages
                    counts.emits += result.emittedMessages
                    counts.publishes += result.publishedMessages
                    guard result.state == .completed || result.state == .paused else {
                        throw ConformanceExecutionError.invalidInput("Measured workload failed")
                    }
                    if result.state == .completed { break }
                    frames += 1
                    counts.pauses += 1
                    guard workload.budgets[index] != nil && result.executedOpcodes > 0 && frames <= 1_000_000 else {
                        throw ConformanceExecutionError.invalidInput("Measured workload did not make progress")
                    }
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
    func messagePublished(_ message: GameEventScriptMessage, result: GameEventScriptPublishResult) {
        counts.observedPublishes += 1
    }
    func dispatchStarted(_ message: GameEventScriptMessage, signatureID: String) { counts.started += 1 }
    func dispatchCompleted(_ message: GameEventScriptMessage, signatureID: String) { counts.completed += 1 }
    func runtimeLimitReached(_ limitName: String, detail: String, limit: Int) { counts.limits += 1 }
    func runtimeError(_ diagnostic: GameEventScriptDiagnostic) { counts.errors += 1 }
    func publish(_ message: GameEventScriptMessage) throws -> Bool {
        counts.outbound += 1
        return accept
    }
}
