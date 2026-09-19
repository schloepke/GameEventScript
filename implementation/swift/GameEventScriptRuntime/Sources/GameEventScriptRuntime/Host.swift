// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Autonomous serial event host. Pumping is synchronous; embedding code serializes access.
public final class GameEventScriptHost {
    private let random: GameEventScriptRandomGenerator
    private let observer: (any GameEventScriptRuntimeObserver)?
    private let extensions: (any GameEventScriptExtensionRegistry)?
    private let externalTypes: (any GameEventScriptExternalTypeRegistry)?
    private let limits: GameEventScriptRuntimeLimits
    private let sink: (any GameEventScriptPublishSink)?
    private lazy var context = GameEventScriptContext(
        host: self, random: random, limits: limits, extensions: extensions, observer: observer)
    private var exact: [String: [GesSubscriptionEntry]] = [:]
    private var byName: [String: [GesSubscriptionEntry]] = [:]
    private var native: [Int64: GesSubscriptionEntry] = [:]
    private var instances: [Int64: GameEventScriptInstance] = [:]
    private var queue = GesMessageQueue()
    private var active: GesPendingMessage?
    private var activeHandler: GesSubscriptionEntry?
    private var scriptActive = false
    private var vm: GesVmState?
    private var nextID: Int64 = 0
    private var nextOrder: Int64 = 0
    private var opcodes = 0, processed = 0, emitted = 0, published = 0
    private var diagnostic: GameEventScriptDiagnostic?
    private var randomBoundary = 0
    private var pumping = false

    public init(
        seed: Int64? = nil, sequence: [Double] = [], limits: GameEventScriptRuntimeLimits = .init(),
        observer: (any GameEventScriptRuntimeObserver)? = nil,
        extensions: (any GameEventScriptExtensionRegistry)? = nil,
        externalTypes: (any GameEventScriptExternalTypeRegistry)? = nil,
        publishSink: (any GameEventScriptPublishSink)? = nil
    ) throws {
        guard (0...65535).contains(limits.maxRandomScopeDepth) else {
            throw GameEventScriptAPIError.invalidArgument("Invalid random scope depth")
        }
        self.limits = limits
        self.observer = observer
        self.extensions = extensions
        self.externalTypes = externalTypes
        sink = publishSink
        random = GameEventScriptRandomGenerator(
            seed: seed ?? GameEventScriptRandomGenerator.entropySeed(), sequence: sequence,
            maxScopeDepth: limits.maxRandomScopeDepth)
    }
    public var pendingMessageCount: Int { queue.count }
    public var isIdle: Bool { active == nil && queue.count == 0 }

    /// Validates and links before atomically publishing registrations and initialization.
    public func load(_ program: GameEventScriptProgram, priority: Int = 0) throws -> GameEventScriptInstance {
        try GameEventScriptProgramValidator.validate(program)
        let registers = min(65535, limits.maxRegisterValues > 0 ? limits.maxRegisterValues : 512)
        let depth = min(65535, max(0, limits.maxCallDepth))
        if program.requiredRegisterCount > registers {
            throw GameEventScriptDynamicLinkError(
                code: "link.requiredRegisterCountExceeded", program: program.moduleName)
        }
        if program.requiredCallStackDepth > depth {
            throw GameEventScriptDynamicLinkError(
                code: "link.requiredCallStackDepthExceeded", program: program.moduleName)
        }
        let linked = try GesLinkedProgram(program, extensions: extensions, types: externalTypes)
        if queueFull && linked.handlers.contains(where: { $0.signature.name == "initialization" }) {
            throw GameEventScriptDynamicLinkError(code: "link.initializationQueueFull", program: program.moduleName)
        }
        if vm == nil { vm = GesVmState(maxRegisters: registers, maxCallDepth: depth) }
        vm!.prepareCapacity(Int(program.requiredRegisterCount))
        let instance = GameEventScriptInstance(host: self, id: try registrationID(), linked: linked)
        var initialization: [GesSubscriptionEntry] = []
        for handler in linked.handlers {
            let entry = GesSubscriptionEntry(
                signature: handler.signature, requiredTags: handler.requiredTags, excludedTags: handler.excludedTags,
                matchArguments: handler.binding.kind == .messageHandler, priority: priority, order: order(), id: 0,
                native: nil,
                instance: instance, address: Int(handler.binding.entryAddress))
            if handler.signature.name == "initialization" { initialization.append(entry) } else { register(entry) }
        }
        instances[instance.registrationID] = instance
        if !initialization.isEmpty {
            queue.enqueue(
                .init(
                    message: try GameEventScriptMessage(name: "initialization"),
                    exact: initialization.sorted(by: GesSubscriptionEntry.precedes), names: []))
        }
        return instance
    }
    public func subscribe(
        _ signature: GameEventScriptMessageSignature, handler: any GameEventScriptNativeMessageHandler,
        matchingTags: [String] = [], withoutTags: [String] = [], priority: Int = 0
    ) throws -> GameEventScriptSubscription {
        try subscribe(
            signature, handler: handler, matchingTags: matchingTags, withoutTags: withoutTags, priority: priority,
            matchArguments: true)
    }
    public func subscribeMessageName(
        _ name: String, handler: any GameEventScriptNativeMessageHandler,
        matchingTags: [String] = [], withoutTags: [String] = [], priority: Int = 0
    ) throws -> GameEventScriptSubscription {
        guard !MessageNames.trim(name).isEmpty else {
            throw GameEventScriptAPIError.invalidArgument("Empty message name")
        }
        return try subscribe(
            .init(name: name), handler: handler, matchingTags: matchingTags, withoutTags: withoutTags,
            priority: priority, matchArguments: false)
    }
    private func subscribe(
        _ signature: GameEventScriptMessageSignature, handler: any GameEventScriptNativeMessageHandler,
        matchingTags: [String], withoutTags: [String], priority: Int, matchArguments: Bool
    ) throws -> GameEventScriptSubscription {
        let required = try GameEventScriptMessage(name: "Tags", tags: matchingTags).tags
        let excluded = try GameEventScriptMessage(name: "Tags", tags: withoutTags).tags
        let id = try registrationID()
        let entry = GesSubscriptionEntry(
            signature: signature, requiredTags: required, excludedTags: excluded, matchArguments: matchArguments,
            priority: priority, order: order(), id: id, native: handler, instance: nil, address: 0)
        register(entry)
        native[id] = entry
        return GameEventScriptSubscription(host: self, id: id)
    }
    @discardableResult public func receive(_ message: GameEventScriptMessage) -> Bool {
        if message.name.isEmpty || message.name == "initialization" { return false }
        return enqueue(message)
    }
    public func executeFrame(opcodeBudget: Int) throws -> GameEventScriptExecutionResult {
        guard opcodeBudget > 0 else { throw GameEventScriptAPIError.invalidArgument("Opcode budget must be positive") }
        guard !pumping else { throw GameEventScriptAPIError.invalidOperation("A host cannot be pumped recursively") }
        pumping = true
        defer { pumping = false }
        opcodes = 0
        processed = 0
        emitted = 0
        published = 0
        diagnostic = nil
        var remaining = opcodeBudget
        var limitReached = false
        while remaining > 0 {
            if active == nil {
                active = queue.dequeue()
                if active == nil { break }
            }
            if scriptActive {
                let executed = GameEventScriptVirtualMachine.runSlice(vm!, context: context, budget: remaining)
                opcodes += executed
                remaining -= executed
                if context.budget.isExhausted {
                    vm!.reset()
                    completeHandler()
                    limitReached = true
                    break
                }
                if vm!.processing { break }
                if let error = vm!.error { record(error) }
                vm!.reset()
                completeHandler()
                continue
            }
            guard let next = active!.nextMatching() else {
                active = nil
                processed += 1
                if limits.maxProcessedEventsPerRun > 0 && processed >= limits.maxProcessedEventsPerRun
                    && queue.count > 0
                {
                    context.reportLimit("MaxProcessedEventsPerRun", limits.maxProcessedEventsPerRun)
                    limitReached = true
                    break
                }
                continue
            }
            activeHandler = next
            randomBoundary = context.beginHandler()
            observer?.dispatchStarted(active!.message, signatureID: next.dispatchSignature)
            if let handler = next.native {
                do { try handler.handle(active!.message, context: context) } catch let fault
                    as GameEventScriptExtensionFault
                { record(fault.diagnostic) } catch let fault as GesRuntimeError { record(fault.diagnostic) } catch {
                    record(runtimeDiagnostic("runtime.nativeHandlerFailure", error: error))
                }
                completeHandler()
                if context.budget.isExhausted {
                    limitReached = true
                    break
                }
            } else {
                do {
                    try GameEventScriptVirtualMachine.begin(
                        vm!, linked: next.instance!.linked, message: active!.message,
                        matchArguments: next.matchArguments, entry: next.address, signature: next.dispatchSignature)
                    scriptActive = true
                } catch let fault as GesRuntimeError {
                    record(fault.diagnostic)
                    vm!.reset()
                    completeHandler()
                } catch {
                    record(runtimeDiagnostic("runtime.preparationFailed", error: error))
                    vm!.reset()
                    completeHandler()
                }
            }
        }
        return .init(
            state: diagnostic != nil
                ? .runtimeError : limitReached ? .runtimeLimitReached : isIdle ? .completed : .paused,
            executedOpcodes: opcodes, processedMessages: processed, emittedMessages: emitted,
            publishedMessages: published, diagnostic: diagnostic)
    }
    public func runToCompletion() throws -> GameEventScriptExecutionResult {
        var totalOpcodes = 0
        var totalProcessed = 0
        var totalEmitted = 0
        var totalPublished = 0
        while true {
            let step = try executeFrame(opcodeBudget: Int(Int32.max))
            totalOpcodes += step.executedOpcodes
            totalProcessed += step.processedMessages
            totalEmitted += step.emittedMessages
            totalPublished += step.publishedMessages
            if step.state != .paused
                || step.executedOpcodes + step.processedMessages + step.emittedMessages + step.publishedMessages == 0
            {
                return .init(
                    state: step.state, executedOpcodes: totalOpcodes, processedMessages: totalProcessed,
                    emittedMessages: totalEmitted, publishedMessages: totalPublished, diagnostic: step.diagnostic)
            }
        }
    }
    func emit(_ message: GameEventScriptMessage) -> Bool {
        let accepted = !random.hasActiveScopeFault && enqueue(message)
        emitted += 1
        observer?.messageEmitted(message, accepted: accepted)
        return accepted
    }
    func publish(_ message: GameEventScriptMessage) -> GameEventScriptPublishResult {
        let fault = random.hasActiveScopeFault
        let accepted = !fault && enqueue(message)
        let attempted = !fault && sink != nil
        var outbound = false
        if attempted {
            do { outbound = try sink!.publish(message) } catch {
                observer?.runtimeError(runtimeDiagnostic("runtime.publishSinkFailure", error: error))
            }
        }
        let result = GameEventScriptPublishResult(
            localAccepted: accepted, outboundAttempted: attempted, outboundAccepted: outbound)
        published += 1
        observer?.messagePublished(message, result: result)
        return result
    }
    private func record(_ value: GameEventScriptDiagnostic) {
        var value = value
        if value.programName == nil { value.programName = activeHandler?.instance?.program.moduleName }
        if value.handlerName == nil { value.handlerName = activeHandler?.dispatchSignature }
        if diagnostic == nil { diagnostic = value }
        observer?.runtimeError(value)
    }
    private func runtimeDiagnostic(_ code: String, error: (any Error)? = nil) -> GameEventScriptDiagnostic {
        .init(
            phase: .runtime, code: code, programName: activeHandler?.instance?.program.moduleName,
            handlerName: activeHandler?.dispatchSignature, technicalDetails: error.map { String(describing: $0) })
    }
    private func completeHandler() {
        let fault = context.endBoundary(randomBoundary)
        if diagnostic == nil && !context.budget.isExhausted {
            if fault == .boundaryUnderflow { record(runtimeDiagnostic("runtime.randomStackUnderflow")) }
            if fault == .unbalanced { record(runtimeDiagnostic("runtime.randomScopeImbalance")) }
        }
        scriptActive = false
        let handler = activeHandler
        activeHandler = nil
        if let handler { observer?.dispatchCompleted(active!.message, signatureID: handler.dispatchSignature) }
    }
    private var queueFull: Bool { limits.maxQueuedMessagesPerRun > 0 && queue.count >= limits.maxQueuedMessagesPerRun }
    private func enqueue(_ message: GameEventScriptMessage) -> Bool {
        var exact = self.exact[message.signatureId] ?? []
        var names = byName[message.name] ?? []
        if !exact.contains(where: { $0.matches(message) }) && !names.contains(where: { $0.matches(message) }) {
            if message.name == "undeliverable" { return false }
            exact = self.exact["undeliverable(message)"] ?? []
            names = byName["undeliverable"] ?? []
            if !exact.contains(where: { $0.matches(message) }) && !names.contains(where: { $0.matches(message) }) {
                return false
            }
        }
        if queueFull {
            observer?.runtimeLimitReached(
                "MaxQueuedMessagesPerRun", detail: "Message queue limit reached. Dropped '" + message.name + "'.",
                limit: limits.maxQueuedMessagesPerRun)
            return false
        }
        queue.enqueue(.init(message: message, exact: exact, names: names))
        return true
    }
    private func registrationID() throws -> Int64 {
        if nextID == .max { throw GameEventScriptAPIError.invalidOperation("Registration IDs exhausted") }
        nextID += 1
        return nextID
    }
    private func order() -> Int64 {
        defer { nextOrder += 1 }
        return nextOrder
    }
    private func register(_ entry: GesSubscriptionEntry) {
        if entry.matchArguments {
            var items = exact[entry.signature.signatureId] ?? []
            items.append(entry)
            items.sort(by: GesSubscriptionEntry.precedes)
            exact[entry.signature.signatureId] = items
        } else {
            var items = byName[entry.signature.name] ?? []
            items.append(entry)
            items.sort(by: GesSubscriptionEntry.precedes)
            byName[entry.signature.name] = items
        }
    }
    func isAttached(_ id: Int64) -> Bool { instances[id] != nil }
    func isSubscribed(_ id: Int64) -> Bool { native[id] != nil }
    func detach(_ id: Int64) -> Bool {
        guard let instance = instances.removeValue(forKey: id) else { return false }
        for handler in instance.linked.handlers where handler.signature.name != "initialization" {
            if handler.binding.kind == .messageHandler {
                let key = handler.signature.signatureId
                exact[key] = exact[key]?.filter { $0.instance !== instance }
                if exact[key]?.isEmpty == true { exact[key] = nil }
            } else {
                let key = handler.signature.name
                byName[key] = byName[key]?.filter { $0.instance !== instance }
                if byName[key]?.isEmpty == true { byName[key] = nil }
            }
        }
        return true
    }
    func unsubscribe(_ id: Int64) -> Bool {
        guard let entry = native.removeValue(forKey: id) else { return false }
        if entry.matchArguments {
            let key = entry.signature.signatureId
            exact[key] = exact[key]?.filter { $0 !== entry }
            if exact[key]?.isEmpty == true { exact[key] = nil }
        } else {
            let key = entry.signature.name
            byName[key] = byName[key]?.filter { $0 !== entry }
            if byName[key]?.isEmpty == true { byName[key] = nil }
        }
        return true
    }
}

public final class GameEventScriptInstance {
    private weak var host: GameEventScriptHost?
    public let registrationID: Int64
    let linked: GesLinkedProgram
    public var program: GameEventScriptProgram { linked.program }
    public var isAttached: Bool { host?.isAttached(registrationID) ?? false }
    init(host: GameEventScriptHost, id: Int64, linked: GesLinkedProgram) {
        self.host = host
        registrationID = id
        self.linked = linked
    }
    @discardableResult public func detach() -> Bool { host?.detach(registrationID) ?? false }
}
public final class GameEventScriptSubscription {
    private weak var host: GameEventScriptHost?
    public let registrationID: Int64
    public var isSubscribed: Bool { host?.isSubscribed(registrationID) ?? false }
    init(host: GameEventScriptHost, id: Int64) {
        self.host = host
        registrationID = id
    }
    @discardableResult public func unsubscribe() -> Bool { host?.unsubscribe(registrationID) ?? false }
}

private final class GesSubscriptionEntry {
    let signature: GameEventScriptMessageSignature
    let requiredTags: [String], excludedTags: [String]
    let matchArguments: Bool
    let priority: Int
    let order: Int64, id: Int64
    let native: (any GameEventScriptNativeMessageHandler)?
    let instance: GameEventScriptInstance?
    let address: Int
    let dispatchSignature: String
    init(
        signature: GameEventScriptMessageSignature, requiredTags: [String], excludedTags: [String],
        matchArguments: Bool,
        priority: Int, order: Int64, id: Int64, native: (any GameEventScriptNativeMessageHandler)?,
        instance: GameEventScriptInstance?, address: Int
    ) {
        self.signature = signature
        self.requiredTags = requiredTags
        self.excludedTags = excludedTags
        self.matchArguments = matchArguments
        self.priority = priority
        self.order = order
        self.id = id
        self.native = native
        self.instance = instance
        self.address = address
        dispatchSignature = matchArguments ? signature.signatureId : signature.name + "(*)"
    }
    func matches(_ message: GameEventScriptMessage) -> Bool {
        requiredTags.allSatisfy { message.tags.contains($0) } && !excludedTags.contains { message.tags.contains($0) }
    }
    static func precedes(_ left: GesSubscriptionEntry, _ right: GesSubscriptionEntry) -> Bool {
        left.priority != right.priority ? left.priority > right.priority : left.order < right.order
    }
}
private struct GesPendingMessage {
    let message: GameEventScriptMessage
    let exact: [GesSubscriptionEntry], names: [GesSubscriptionEntry]
    var exactIndex = 0, nameIndex = 0
    mutating func nextMatching() -> GesSubscriptionEntry? {
        while exactIndex < exact.count || nameIndex < names.count {
            let next: GesSubscriptionEntry
            if exactIndex >= exact.count {
                next = names[nameIndex]
                nameIndex += 1
            } else if nameIndex >= names.count || GesSubscriptionEntry.precedes(exact[exactIndex], names[nameIndex]) {
                next = exact[exactIndex]
                exactIndex += 1
            } else {
                next = names[nameIndex]
                nameIndex += 1
            }
            if next.matches(message) { return next }
        }
        return nil
    }
}
private struct GesMessageQueue {
    private var items = [GesPendingMessage?](repeating: nil, count: 16)
    private var head = 0
    private(set) var count = 0
    mutating func enqueue(_ value: GesPendingMessage) {
        if count == items.count {
            var expanded = [GesPendingMessage?](repeating: nil, count: items.count * 2)
            for index in 0..<count { expanded[index] = items[(head + index) % items.count] }
            items = expanded
            head = 0
        }
        items[(head + count) % items.count] = value
        count += 1
    }
    mutating func dequeue() -> GesPendingMessage? {
        if count == 0 { return nil }
        let item = items[head]
        items[head] = nil
        head = (head + 1) % items.count
        count -= 1
        return item
    }
}
