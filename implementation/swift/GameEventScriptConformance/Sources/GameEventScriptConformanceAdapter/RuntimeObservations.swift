// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime

final class VirtualClock: GesClock {
    var elapsedMicroseconds: Int64 = 0

    func advance(_ amount: String) -> Bool {
        let delta = Int64(amount)!
        guard delta <= Int64.max - elapsedMicroseconds else { return false }
        elapsedMicroseconds += delta
        return true
    }
}

final class HostScenario {
    let clock = VirtualClock()
    let test: Input
    let programs: [(id: String, program: GameEventScriptProgram)]
    let host: GameEventScriptHost
    let collector: RuntimeObservations
    var actionResults: [[String: Any]] = []
    var instances: [String: GesProgramInstance] = [:]
    var subscriptions: [String: GesSubscription] = [:]
    var path = "initialization"

    init(_ test: Input, programs: [(id: String, program: GameEventScriptProgram)]) throws {
        self.test = test
        self.programs = programs
        collector = RuntimeObservations(sink: test["publishSink"]?.stringValue ?? "accept")
        host = try Self.makeHost(test, observer: collector, publishSink: collector.sink == "absent" ? nil : collector, clock: clock)
    }

    static func makeHost(_ test: Input, observer: (any GesRuntimeObserver)?, publishSink: (any GesPublishSink)?, clock: (any GesClock)? = nil) throws -> GameEventScriptHost {
        var limits = GesRuntimeLimits()
        let setters: [String: WritableKeyPath<GesRuntimeLimits, Int>] = [
            "maxProcessedEventsPerRun": \.maxProcessedEventsPerRun, "maxQueuedMessagesPerRun": \.maxQueuedMessagesPerRun, "maxExecutionSteps": \.maxExecutionSteps, "maxRegisterValues": \.maxRegisterValues, "maxLoopIterations": \.maxLoopIterations,
            "maxCallDepth": \.maxCallDepth, "maxRandomScopeDepth": \.maxRandomScopeDepth, "maxRangeItems": \.maxRangeItems, "maxGeneratedCollectionItems": \.maxGeneratedCollectionItems, "maxDiceCount": \.maxDiceCount, "maxDiceSides": \.maxDiceSides,
        ]
        for entry in test["limits"]?.objectValue ?? [] { if let key = setters[entry.key], let number = entry.value.integerValue { limits[keyPath: key] = number } }
        let random = test["random"]
        var seed = random?["seed"].flatMap { Int64($0.numberValue ?? $0.stringValue ?? "") }
        if let entropy = random?["entropy"]?.stringValue {
            let scalars = Array(entropy.utf8)
            var bytes: [UInt8] = []
            for index in stride(from: 0, to: scalars.count, by: 2) {
                guard index + 1 < scalars.count, let byte = UInt8(String(decoding: scalars[index...index + 1], as: UTF8.self), radix: 16) else { throw ConformanceExecutionError.invalidInput("Invalid entropy bytes") }
                bytes.append(byte)
            }
            seed = try GesRandomGenerator.seedFromEntropy(bytes)
        }
        let sequence = try (random?["sequence"]?.arrayValue ?? []).map { try RuntimeValues.parseNumber($0.numberValue ?? $0.stringValue!) }
        let builder = GameEventScriptHost.makeBuilder()
            .withRandomSequence(sequence, fallbackSeed: seed ?? 0)
            .withRuntimeLimits(limits)
            .withRegistry(RuntimeFixtures())
        if let observer { builder.withRuntimeObserver(observer) }
        if test["externalTypeRegistry"]?.stringValue != "absent" { builder.withExternalTypeRegistry(RuntimeFixtures(mismatch: test["externalTypeRegistry"]?.stringValue == "mismatch")) }
        if let publishSink { builder.withPublishSink(publishSink) }
        if let clock { builder.withClock(clock) }
        return try builder.build()
    }

    func configure() throws {
        let deferred = test.values("deferredPrograms").compactMap(\.stringValue)
        for program in programs where !deferred.contains(program.id) { _ = try load(program.id) }
        for (index, definition) in test.values("nativeHandlers").enumerated() where definition["initiallySubscribed"]?.boolValue != false { _ = try subscribe(id(definition, index)) }
    }

    func run() throws -> [String: Any] {
        try configure()
        _ = try host.start()
        if host.isReady && test["initializationPump"]?.stringValue == "completion" { _ = try host.runToCompletion() }
        let initialization = try capture()
        var steps: [String: Any] = [:]
        for step in test.values("steps") {
            collector.clear()
            actionResults.removeAll()
            let id = try step.text("id")
            path = "step/" + id
            try actions(test["stepActions"]?[id]?.arrayValue ?? [], path: path)
            let accepted = host.receive(try RuntimeValues.decodeMessage(step.required("input")))
            var paused = false
            var waiting = false
            switch host.isReady ? step["pump"]?.stringValue : "enqueue" {
            case "completion":
                let result = try host.runToCompletion()
                paused = result.state == .paused
                waiting = result.state == .waiting
            case "frame", "frames":
                var frames = 0
                while true {
                    frames += 1
                    if frames > 1_000_000 { throw ConformanceExecutionError.invalidInput("Frame limit exceeded") }
                    let result = try host.executeFrame(opcodeBudget: step["budget"]!.integerValue!)
                    waiting = result.state == .waiting
                    paused = paused || result.state == .paused
                    if result.state != .paused || step["pump"]?.stringValue == "frame" { break }
                }
            default: break
            }
            var result = try capture()
            result["accepted"] = accepted
            result["paused"] = paused
            result["waiting"] = waiting
            steps[id] = result
        }
        return ["initialization": initialization, "steps": steps]
    }

    func actions(_ actions: [Input], path: String) throws {
        if actions.isEmpty { return }
        // Reserve the group before invoking actions: callbacks may append other groups.
        let group = actionResults.count
        actionResults.append(["origin": path, "results": []])
        var results: [[String: Any]] = []
        for action in actions {
            let operation = try action.text("operation")
            let target = try action.text("target")
            do {
                let result: Bool
                switch operation {
                case "advanceMicroseconds": result = clock.advance(target)
                case "loadProgram": result = try load(target)
                case "detachProgram": result = instances[target]?.detach() ?? false
                case "subscribeHandler": result = try subscribe(target)
                case "unsubscribeHandler": result = subscriptions[target]?.unsubscribe() ?? false
                default: throw ConformanceExecutionError.invalidInput("Unknown action")
                }
                results.append(["result": result, "error": NSNull()])
            } catch let error as GesDynamicLinkError {
                results.append(["result": NSNull(), "error": CompilerObservations.diagnostic(error.diagnostic)])
            }
        }
        actionResults[group]["results"] = results
    }

    func capture() throws -> [String: Any] {
        var result = try collector.capture()
        result["actions"] = actionResults
        result["hostReady"] = host.isReady
        result["idleDurationMicroseconds"] = host.idleDurationMicroseconds.map(String.init) ?? "none"
        result["programStarts"] = instances.mapValues { $0.startResult.map { String(describing: $0.state) } ?? "pending" }
        return result
    }

    func load(_ id: String) throws -> Bool {
        if instances[id]?.isAttached == true { return true }
        guard let program = programs.first(where: { $0.id == id }) else { return false }
        instances[id] = try host.load(program.program)
        return true
    }

    func subscribe(_ identifier: String) throws -> Bool {
        if subscriptions[identifier]?.isSubscribed == true { return true }
        for (index, definition) in test.values("nativeHandlers").enumerated() where id(definition, index) == identifier {
            let handler = RuntimeNativeHandler(definition: definition, scenario: self, id: identifier)
            if definition["messageName"]?.boolValue == true {
                subscriptions[identifier] = try host.subscribeMessageName(definition.text("message"), handler: handler, priority: definition["priority"]?.integerValue ?? 0)
            } else {
                subscriptions[identifier] = try host.subscribe(.init(name: definition.text("message"), parameters: definition.values("parameters").compactMap(\.stringValue)), handler: handler, priority: definition["priority"]?.integerValue ?? 0)
            }
            return true
        }
        return false
    }

    func id(_ definition: Input, _ index: Int) -> String { definition["id"]?.stringValue ?? "native-" + String(repeating: "0", count: max(0, 4 - String(index + 1).count)) + String(index + 1) }

}

private final class RuntimeNativeHandler: GesNativeMessageHandler {
    let definition: Input
    unowned let scenario: HostScenario
    let id: String

    init(definition: Input, scenario: HostScenario, id: String) {
        self.definition = definition
        self.scenario = scenario
        self.id = id
    }

    func handle(_ message: GameEventScriptMessage, context: GesContext) throws {
        if definition["throw"]?.boolValue == true { throw ConformanceExecutionError.invalidInput("Configured native failure") }
        if let code = definition["faultCode"]?.stringValue {
            throw try GesExtensionFaultError(code: code, message: "Configured native fault", programName: definition["faultContext"]?["programName"]?.stringValue, handlerName: definition["faultContext"]?["handlerName"]?.stringValue)
        }
        try scenario.actions(definition.values("actions"), path: "native/" + id)
        for emit in definition.values("emit") {
            if emit["forwardArguments"]?.boolValue == true {
                let arguments = try (0..<message.arguments.count).map { try GesMessageArgument(name: message.arguments.nameAt($0), value: message.arguments[$0]) }
                context.emit(try .init(name: emit.required("message").text("name"), arguments: arguments))
            } else {
                context.emit(try RuntimeValues.decodeMessage(emit.required("message")))
            }
        }
    }
}

final class RuntimeObservations: GesRuntimeObserver, GesPublishSink {
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
        var result: GesPublishResult?
        var limit: Limit?
        var diagnostic: GesDiagnostic?
        var id: Int64?
        var deadline: Int64?
        var publish: Bool?
    }

    let sink: String
    var local: [GameEventScriptMessage] = [], outbound: [GameEventScriptMessage] = []
    var limits: [Limit] = [], diagnostics: [GesDiagnostic] = [], trace: [Event] = []

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

    func messageEmitted(_ message: GameEventScriptMessage, id: Int64, accepted: Bool) {
        local.append(message)
        trace.append(.init(kind: "emit", message: message, accepted: accepted, id: id))
    }

    func messagePublished(_ message: GameEventScriptMessage, id: Int64, result: GesPublishResult) {
        local.append(message)
        trace.append(.init(kind: "publish", message: message, result: result, id: id))
    }

    func dispatchStarted(_ message: GameEventScriptMessage, id: Int64, signatureID: String) { trace.append(.init(kind: "dispatchStarted", message: message, signature: signatureID, id: id)) }

    func dispatchCompleted(_ message: GameEventScriptMessage, id: Int64, signatureID: String) { trace.append(.init(kind: "dispatchCompleted", message: message, signature: signatureID, id: id)) }

    func runtimeLimitReached(_ limitName: String, detail: String, limit: Int) {
        let value = Limit(name: limitName, detail: detail, limit: limit)
        limits.append(value)
        trace.append(.init(kind: "runtimeLimit", limit: value))
    }

    func runtimeError(_ diagnostic: GesDiagnostic) {
        diagnostics.append(diagnostic)
        trace.append(.init(kind: "diagnostic", diagnostic: diagnostic))
    }

    func messageScheduled(_ message: GameEventScriptMessage, id: Int64, deadline: Int64, publish: Bool) {
        trace.append(.init(kind: "scheduled", message: message, id: id, deadline: deadline, publish: publish))
    }

    func scheduledMessageDiscarded(id: Int64) { trace.append(.init(kind: "scheduledDiscarded", id: id)) }
}

extension RuntimeObservations {
    func capture() throws -> [String: Any] {
        [
            "local": try local.map(RuntimeValues.encodeMessage), "outbound": try outbound.map(RuntimeValues.encodeMessage),
            "runtimeLimits": limits.map { ["name": $0.name, "limit": $0.limit] }, "diagnostics": diagnostics.map(CompilerObservations.diagnostic),
            "trace": try trace.map { event -> [String: Any] in
                var item: [String: Any] = ["event": event.kind]
                if let message = event.message { item["message"] = try RuntimeValues.encodeMessage(message) }
                if let signature = event.signature { item["signatureId"] = signature }
                if let accepted = event.accepted { item["accepted"] = accepted }
                if let id = event.id { item["id"] = String(id) }
                if let deadline = event.deadline { item["deadline"] = String(deadline) }
                if let publish = event.publish { item["publish"] = publish }
                if let result = event.result { item["result"] = ["localAccepted": result.localAccepted, "outboundAttempted": result.outboundAttempted, "outboundAccepted": result.outboundAccepted, "anyAccepted": result.anyAccepted] }
                if let diagnostic = event.diagnostic { item["diagnostic"] = CompilerObservations.diagnostic(diagnostic) }
                if let limit = event.limit { item["runtimeLimit"] = ["name": limit.name, "limit": limit.limit] }
                return item
            },
        ]
    }
}

func runtime(_ programs: [(id: String, program: GameEventScriptProgram)], _ input: Input) throws -> [String: Any] {
    try ["hosts": (0..<(input["hostCount"]?.integerValue ?? 1)).map { _ in try HostScenario(input, programs: programs).run() }]
}
