// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// One compiler-produced Program supplied by the embedding for Runtime-only verification.
public struct ConformanceRuntimeProgram {
    /// Logical Program group identifier from the case sources.
    public let id: String
    /// Already compiled immutable Program used for runtime verification.
    public let program: GameEventScriptProgram
    /// Associates an already compiled Program with its logical source group.
    public init(id: String, program: GameEventScriptProgram) {
        self.id = id
        self.program = program
    }
}

/// Verifies the runtime portion of shared scenarios using already compiled Programs.
/// This entry point does not claim or test a Swift compiler or full-port acceptance.
public enum ConformanceRuntimeRunner {
    /// Checks runtime scenario behavior using supplied Programs in authored group order. Compilation and performance
    /// measurement are outside this entry point; invalid inputs are reported as case errors.
    public static func runCase(_ testCase: ConformanceCase, programs: [ConformanceRuntimeProgram])
        -> ConformanceCaseResult
    {
        do {
            guard ["scriptApi", "loadError", "performance"].contains(testCase.kind) else {
                throw ConformanceExecutionError.invalidInput("Not a runtime scenario")
            }
            let ids = testCase.sources.reduce(into: [String]()) {
                if !$0.contains($1.programID) { $0.append($1.programID) }
            }
            guard ids == programs.map(\.id) else {
                throw ConformanceExecutionError.invalidInput("Missing, reordered, or extra Program group")
            }
            var differences: [ConformanceMismatch] = []
            for _ in 0..<(testCase.metadata["hostCount"]?.integerValue ?? 1) {
                let scenario = try RuntimeScenario(testCase, programs: programs)
                if testCase.kind == "loadError" {
                    do {
                        try scenario.configure()
                        differences.append(.init(path: "/error", expected: "link error", actual: "success"))
                    } catch let error as GameEventScriptDynamicLinkError {
                        scenario.checkDiagnostic(
                            testCase.expectation["error"] ?? .null, error.diagnostic, path: "/error")
                    }
                } else {
                    try scenario.run()
                }
                differences += scenario.differences
            }
            return .init(
                testCase: testCase, status: differences.isEmpty ? "passed" : "failed",
                code: differences.isEmpty ? "conformance.passed" : "conformance.assertion.mismatch",
                missingCapabilities: [], mismatches: differences, technicalDetails: nil)
        } catch {
            return .init(
                testCase: testCase, status: "error", code: "conformance.runner.unhandledException",
                missingCapabilities: [], mismatches: [], technicalDetails: String(describing: error))
        }
    }
}

final class RuntimeScenario {
    let test: ConformanceCase
    let programs: [ConformanceRuntimeProgram]
    let host: GameEventScriptHost
    let collector: RuntimeCollector
    var differences: [ConformanceMismatch] = []
    var instances: [String: GameEventScriptInstance] = [:]
    var subscriptions: [String: GameEventScriptSubscription] = [:]
    var path = "/initialization"
    init(_ test: ConformanceCase, programs: [ConformanceRuntimeProgram]) throws {
        self.test = test
        self.programs = programs
        collector = RuntimeCollector(sink: test.metadata["publishSink"]?.stringValue ?? "accept")
        host = try Self.makeHost(test, observer: collector, publishSink: collector.sink == "absent" ? nil : collector)
    }
    static func makeHost(
        _ test: ConformanceCase, observer: (any GameEventScriptRuntimeObserver)?,
        publishSink: (any GameEventScriptPublishSink)?
    ) throws -> GameEventScriptHost {
        var limits = GameEventScriptRuntimeLimits()
        let setters: [String: WritableKeyPath<GameEventScriptRuntimeLimits, Int>] = [
            "maxProcessedEventsPerRun": \.maxProcessedEventsPerRun,
            "maxQueuedMessagesPerRun": \.maxQueuedMessagesPerRun,
            "maxExecutionSteps": \.maxExecutionSteps, "maxRegisterValues": \.maxRegisterValues,
            "maxLoopIterations": \.maxLoopIterations,
            "maxCallDepth": \.maxCallDepth, "maxRandomScopeDepth": \.maxRandomScopeDepth,
            "maxRangeItems": \.maxRangeItems,
            "maxGeneratedCollectionItems": \.maxGeneratedCollectionItems, "maxDiceCount": \.maxDiceCount,
            "maxDiceSides": \.maxDiceSides,
        ]
        for entry in test.metadata["runtimeLimits"]?.objectValue ?? [] {
            if let key = setters[entry.key], let number = entry.value.integerValue { limits[keyPath: key] = number }
        }
        let random = test.metadata["random"]
        var seed = random?["seed"].flatMap { Int64($0.numberValue ?? $0.stringValue ?? "") }
        if let entropy = random?["entropy"]?.stringValue {
            let scalars = Array(entropy.utf8)
            var bytes: [UInt8] = []
            for index in stride(from: 0, to: scalars.count, by: 2) {
                guard index + 1 < scalars.count,
                    let byte = UInt8(String(decoding: scalars[index...index + 1], as: UTF8.self), radix: 16)
                else {
                    throw ConformanceExecutionError.invalidInput("Invalid entropy bytes")
                }
                bytes.append(byte)
            }
            seed = try GameEventScriptRandomGenerator.seedFromEntropy(bytes)
        }
        let sequence = try (random?["sequence"]?.arrayValue ?? []).map {
            try ConformanceRuntimeValueCodec.number($0.numberValue ?? $0.stringValue!)
        }
        return try GameEventScriptHost(
            seed: seed, sequence: sequence, limits: limits, observer: observer,
            extensions: RuntimeFixtures(),
            externalTypes: test.metadata["externalTypeRegistry"]?.stringValue == "absent"
                ? nil : RuntimeFixtures(mismatch: test.metadata["externalTypeRegistry"]?.stringValue == "mismatch"),
            publishSink: publishSink)
    }
    func configure() throws {
        let deferred = test.metadata.values("deferredPrograms").compactMap(\.stringValue)
        for program in programs where !deferred.contains(program.id) { _ = try load(program.id) }
        for (index, definition) in test.metadata.values("nativeHandlers").enumerated()
        where definition["initiallySubscribed"]?.boolValue != false { _ = try subscribe(id(definition, index)) }
    }
    func run() throws {
        try configure()
        _ = try host.start()
        if host.isReady && test.expectation["initialization"]?["pump"]?.stringValue != "start" {
            _ = try host.runToCompletion()
        }
        try compare(test.expectation["initialization"] ?? .object([]), path: "/initialization")
        for step in test.steps {
            collector.clear()
            path = "/steps/" + step.id
            try actions(test.metadata["stepActions"]?[step.id]?.arrayValue ?? [], path: path + "/actions")
            let expected = test.expectation["steps"]?[step.id] ?? .object([])
            let input = (expected["input"] ?? .object([])).replacing("name", with: step.receive ?? .string(""))
            let accepted = host.receive(try ConformanceRuntimeValueCodec.message(input))
            var paused = false
            switch host.isReady ? step.pump : "enqueue" {
            case "completion": paused = try host.runToCompletion().state == .paused
            case "frame": paused = try host.executeFrame(opcodeBudget: step.budget!).state == .paused
            case "frames":
                var frames = 0
                while true {
                    frames += 1
                    if frames > 1_000_000 { throw ConformanceExecutionError.invalidInput("Frame limit exceeded") }
                    let result = try host.executeFrame(opcodeBudget: step.budget!)
                    if result.state != .paused { break }
                    paused = true
                }
            default: break
            }
            check(path + "/accepted", expected["accepted"]?.boolValue ?? true, accepted)
            if let wanted = expected["paused"]?.boolValue { check(path + "/paused", wanted, paused) }
            try compare(expected, path: path)
        }
    }
    func actions(_ actions: [ConformanceData], path: String) throws {
        for (index, action) in actions.enumerated() {
            let prefix = path + "/" + String(index)
            let operation = ["loadProgram", "detachProgram", "subscribeHandler", "unsubscribeHandler"].first {
                action[$0] != nil
            }!
            let target = action[operation]!.stringValue!
            let result: Bool
            do {
                switch operation {
                case "loadProgram": result = try load(target)
                case "detachProgram": result = instances[target]?.detach() ?? false
                case "subscribeHandler": result = try subscribe(target)
                default: result = subscriptions[target]?.unsubscribe() ?? false
                }
            } catch let error as GameEventScriptDynamicLinkError {
                if let expected = action["expectError"] {
                    checkDiagnostic(expected, error.diagnostic, path: prefix + "/error")
                    continue
                }
                throw error
            }
            if action["expectError"] != nil { mismatch(prefix + "/error", "link error", "success") }
            if let expected = action["expectResult"]?.boolValue { check(prefix + "/result", expected, result) }
        }
    }
    func load(_ id: String) throws -> Bool {
        if instances[id]?.isAttached == true { return true }
        guard let program = programs.first(where: { $0.id == id }) else { return false }
        instances[id] = try host.load(program.program)
        return true
    }
    func subscribe(_ identifier: String) throws -> Bool {
        if subscriptions[identifier]?.isSubscribed == true { return true }
        for (index, definition) in test.metadata.values("nativeHandlers").enumerated()
        where id(definition, index) == identifier {
            let handler = RuntimeNativeHandler(definition: definition, scenario: self, id: identifier)
            if definition["messageName"]?.boolValue == true {
                subscriptions[identifier] = try host.subscribeMessageName(
                    definition.text("message"), handler: handler, priority: definition["priority"]?.integerValue ?? 0)
            } else {
                subscriptions[identifier] = try host.subscribe(
                    .init(
                        name: definition.text("message"),
                        parameters: definition.values("parameters").compactMap(\.stringValue)), handler: handler,
                    priority: definition["priority"]?.integerValue ?? 0)
            }
            return true
        }
        return false
    }
    func id(_ definition: ConformanceData, _ index: Int) -> String {
        definition["id"]?.stringValue ?? "native-" + String(repeating: "0", count: max(0, 4 - String(index + 1).count))
            + String(index + 1)
    }
    func compare(_ expected: ConformanceData, path: String) throws {
        if let ready = expected["hostReady"]?.boolValue { check(path + "/hostReady", ready, host.isReady) }
        if case .object(let starts) = expected["programStarts"] {
            for entry in starts {
                let id = entry.key
                let expectedState = entry.value
                let actual: String
                if let instance = instances[id] {
                    switch instance.startResult?.state {
                    case .ready: actual = "ready"
                    case .runtimeError: actual = "runtimeError"
                    case .runtimeLimitReached: actual = "runtimeLimitReached"
                    case nil: actual = "pending"
                    }
                } else {
                    actual = "missing"
                }
                if actual != expectedState.stringValue {
                    mismatch(path + "/programStarts/" + id, expectedState.stringValue ?? "", actual)
                }
            }
        }
        try channel(expected.values("local"), collector.local, path: path + "/local")
        try channel(expected.values("outbound"), collector.outbound, path: path + "/outbound")
        let diagnostics = expected.values("diagnostics")
        if diagnostics.count != collector.diagnostics.count {
            mismatch(path + "/diagnostics/length", String(diagnostics.count), String(collector.diagnostics.count))
        }
        for (index, pair) in zip(diagnostics, collector.diagnostics).enumerated() {
            checkDiagnostic(pair.0, pair.1, path: path + "/diagnostics/" + String(index))
        }
        var offset = 0
        for limit in expected["runtimeLimits"]?.values("include") ?? [] {
            if let index = collector.limits.indices.dropFirst(offset).first(where: {
                matches(limit, collector.limits[$0])
            }) {
                offset = index + 1
            } else {
                mismatch(path + "/runtimeLimits/included", String(describing: limit), "missing")
            }
        }
        for limit in expected["runtimeLimits"]?.values("exclude") ?? []
        where collector.limits.contains(where: { matches(limit, $0) }) {
            mismatch(path + "/runtimeLimits/excluded", "absent", String(describing: limit))
        }
        if let trace = expected["trace"]?.arrayValue {
            if trace.count != collector.trace.count {
                mismatch(path + "/trace/length", String(trace.count), String(collector.trace.count))
            }
            for (index, pair) in zip(trace, collector.trace).enumerated() {
                try compareEvent(pair.0, pair.1, path: path + "/trace/" + String(index))
            }
        }
    }
    func channel(_ expected: [ConformanceData], _ actual: [GameEventScriptMessage], path: String) throws {
        if expected.count != actual.count { mismatch(path + "/length", String(expected.count), String(actual.count)) }
        for (index, pair) in zip(expected, actual).enumerated() {
            if try !ConformanceRuntimeValueCodec.messagesEqual(pair.0, pair.1, comparison: test.metadata["comparison"])
            {
                mismatch(path + "/" + String(index), String(describing: pair.0), pair.1.description)
            }
        }
    }
    func checkDiagnostic(_ expected: ConformanceData, _ actual: GameEventScriptDiagnostic, path: String) {
        let fields: [String: String?] = [
            "phase": String(describing: actual.phase), "code": actual.code, "symbol": actual.symbol,
            "symbolKind": String(describing: actual.symbolKind), "programName": actual.programName,
            "handlerName": actual.handlerName,
            "sourceName": actual.sourceLocation?.sourceName,
        ]
        for (key, value) in fields {
            guard let wanted = expected[key] else { continue }
            if wanted == .null && key != "programName" && key != "handlerName" { continue }
            if wanted.stringValue != value { mismatch(path + "/" + key, wanted.stringValue, value) }
        }
        let location = actual.sourceLocation
        for (key, value) in [
            ("line", location?.line), ("column", location?.column), ("endLine", location?.endLine),
            ("endColumn", location?.endColumn),
        ] {
            if let wanted = expected[key]?.integerValue, wanted != value {
                mismatch(path + "/" + key, String(wanted), value.map(String.init))
            }
        }
    }
    func matches(_ expected: ConformanceData, _ actual: RuntimeCollector.Limit) -> Bool {
        (expected["name"] == nil || expected["name"]?.stringValue == actual.name)
            && (expected["limit"] == nil || expected["limit"]?.integerValue == actual.limit)
            && (expected["detailContains"]?.stringValue.map { actual.detail.contains($0) } ?? true)
    }
    func compareEvent(_ expected: ConformanceData, _ actual: RuntimeCollector.Event, path: String) throws {
        if expected["event"]?.stringValue != actual.kind {
            mismatch(path + "/event", expected["event"]?.stringValue, actual.kind)
            return
        }
        if let message = actual.message, let wanted = expected["message"] {
            try channel([wanted], [message], path: path + "/message")
        }
        if let value = expected["signatureId"]?.stringValue, value != actual.signature {
            mismatch(path + "/signatureId", value, actual.signature)
        }
        if let value = expected["accepted"]?.boolValue { check(path + "/accepted", value, actual.accepted ?? false) }
        if let result = actual.result {
            for (key, value) in [
                ("localAccepted", result.localAccepted), ("outboundAttempted", result.outboundAttempted),
                ("outboundAccepted", result.outboundAccepted), ("anyAccepted", result.anyAccepted),
            ] { if let wanted = expected["result"]?[key]?.boolValue { check(path + "/result/" + key, wanted, value) } }
        }
        if let value = actual.diagnostic {
            checkDiagnostic(expected["diagnostic"] ?? .null, value, path: path + "/diagnostic")
        }
        if let value = actual.limit, !matches(expected["runtimeLimit"] ?? .null, value) {
            mismatch(path + "/runtimeLimit", "matching limit", value.name)
        }
    }
    func check(_ path: String, _ expected: Bool, _ actual: Bool) {
        if expected != actual { mismatch(path, String(expected), String(actual)) }
    }
    func mismatch(_ path: String, _ expected: String?, _ actual: String?) {
        differences.append(.init(path: path, expected: expected, actual: actual))
    }
}

private final class RuntimeNativeHandler: GameEventScriptNativeMessageHandler {
    let definition: ConformanceData
    unowned let scenario: RuntimeScenario
    let id: String
    init(definition: ConformanceData, scenario: RuntimeScenario, id: String) {
        self.definition = definition
        self.scenario = scenario
        self.id = id
    }
    func handle(_ message: GameEventScriptMessage, context: GameEventScriptContext) throws {
        if definition["throw"]?.boolValue == true {
            throw ConformanceExecutionError.invalidInput("Configured native failure")
        }
        if let code = definition["faultCode"]?.stringValue {
            throw try GameEventScriptExtensionFault(
                code: code, message: "Configured native fault",
                programName: definition["faultContext"]?["programName"]?.stringValue,
                handlerName: definition["faultContext"]?["handlerName"]?.stringValue)
        }
        try scenario.actions(definition.values("actions"), path: scenario.path + "/nativeHandlers/" + id + "/actions")
        for emit in definition.values("emit") {
            if emit["forwardArguments"]?.boolValue == true {
                let arguments = try (0..<message.arguments.count).map {
                    try GameEventScriptMessageArgument(name: message.arguments.nameAt($0), value: message.arguments[$0])
                }
                context.emit(try .init(name: emit.text("name"), arguments: arguments))
            } else {
                context.emit(try ConformanceRuntimeValueCodec.message(emit))
            }
        }
    }
}

final class RuntimeCollector: GameEventScriptRuntimeObserver, GameEventScriptPublishSink {
    struct Limit {
        let name: String
        let detail: String
        let limit: Int
    }
    struct Event {
        let kind: String
        var message: GameEventScriptMessage?
        var signature: String?
        var accepted: Bool?
        var result: GameEventScriptPublishResult?
        var limit: Limit?
        var diagnostic: GameEventScriptDiagnostic?
    }
    let sink: String
    var local: [GameEventScriptMessage] = [], outbound: [GameEventScriptMessage] = []
    var limits: [Limit] = [], diagnostics: [GameEventScriptDiagnostic] = [], trace: [Event] = []
    init(sink: String) { self.sink = sink }
    func clear() {
        local.removeAll(keepingCapacity: true)
        outbound.removeAll(keepingCapacity: true)
        limits.removeAll(keepingCapacity: true)
        diagnostics.removeAll(keepingCapacity: true)
        trace.removeAll(keepingCapacity: true)
    }
    func publish(_ message: GameEventScriptMessage) throws -> Bool {
        outbound.append(message)
        if sink == "throw" { throw ConformanceExecutionError.invalidInput("Configured sink failure") }
        return sink == "accept"
    }
    func messageEmitted(_ message: GameEventScriptMessage, accepted: Bool) {
        local.append(message)
        trace.append(.init(kind: "emit", message: message, accepted: accepted))
    }
    func messagePublished(_ message: GameEventScriptMessage, result: GameEventScriptPublishResult) {
        local.append(message)
        trace.append(.init(kind: "publish", message: message, result: result))
    }
    func dispatchStarted(_ message: GameEventScriptMessage, signatureID: String) {
        trace.append(.init(kind: "dispatchStarted", message: message, signature: signatureID))
    }
    func dispatchCompleted(_ message: GameEventScriptMessage, signatureID: String) {
        trace.append(.init(kind: "dispatchCompleted", message: message, signature: signatureID))
    }
    func runtimeLimitReached(_ limitName: String, detail: String, limit: Int) {
        let value = Limit(name: limitName, detail: detail, limit: limit)
        limits.append(value)
        trace.append(.init(kind: "runtimeLimit", limit: value))
    }
    func runtimeError(_ diagnostic: GameEventScriptDiagnostic) {
        diagnostics.append(diagnostic)
        trace.append(.init(kind: "diagnostic", diagnostic: diagnostic))
    }
}
