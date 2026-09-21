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
    private var startupQueue = GesMessageQueue(capacity: 0)
    private var startupResult: GameEventScriptStartResult?
    private var initializationFailure: GameEventScriptStartResult?
    private var initializationOpcodes = 0, initializationEmits = 0, initializationPublishes = 0
    private var starting = false
    private var executingScript = false
    private var initializationInstance: GameEventScriptInstance?
    private var initializationDiagnostic: GesStartDiagnostic?
    private var initializationPublications: [GesDeferredPublication] = []
    private var startupPublications: [GesDeferredPublication] = []
    private var vm: GesVmState?
    private var nextID: Int64 = 0
    private var nextOrder: Int64 = 0
    private var opcodes = 0, processed = 0, emitted = 0, published = 0
    private var diagnostic: GameEventScriptDiagnostic?
    private var randomBoundary = 0
    private var pumping = false

    /// Creates a loading, single-caller host with a private random stream. Load initial Programs and call `start()`
    /// before receiving messages.
    ///
    /// - Throws: `GameEventScriptAPIError.invalidArgument` if the random-scope limit is outside 0...65535.
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
    /// Creates mutable configuration for independent hosts.
    public static func createBuilder() -> GameEventScriptHostBuilder { .init() }
    /// Whether the complete initial load group started successfully; later instance failures do not clear readiness.
    public private(set) var isReady = false
    /// Number of queued ordinary and initialization messages, excluding an active dispatch.
    public var pendingMessageCount: Int { queue.count + startupQueue.count }
    /// Whether no dispatch is active and both message queues are empty; this does not imply readiness.
    public var isIdle: Bool { active == nil && queue.count == 0 && startupQueue.count == 0 }

    /// Validates and links before atomically publishing registrations and initialization.
    /// Before `start()`, the instance joins the initial load group. On a ready host, its initialization enters
    /// the normal FIFO; `startResult` remains nil until initialization completes. Failure removes that instance
    /// and its captured deliveries while preserving other recipients.
    ///
    /// - Returns: An idempotently detachable Program instance; loading itself does not pump messages.
    /// - Throws: A format or linking error for invalid/unresolvable Programs, or an API error during
    ///   initialization, script execution or after failed initial startup. Failed linking adds no registrations.
    public func load(_ program: GameEventScriptProgram, priority: Int = 0) throws -> GameEventScriptInstance {
        guard !starting, !executingScript, startupResult == nil || isReady else {
            throw GameEventScriptAPIError.invalidOperation(
                "Loading is not allowed during initialization, script execution, or after a failed start")
        }
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
            let pending = GesPendingMessage(
                message: try GameEventScriptMessage(name: "initialization"),
                exact: initialization.sorted(by: GesSubscriptionEntry.precedes), names: [], initialization: instance)
            if isReady { queue.enqueue(pending) } else { startupQueue.enqueue(pending) }
        } else if isReady {
            instance.startResult = .init(state: .ready)
        }
        return instance
    }
    /// Registers a synchronous native handler for an exact signature and optional tag filters. Higher priority runs
    /// first, then registration order.
    ///
    /// - Returns: An idempotently detachable subscription.
    /// - Throws: An API error for invalid tag names or exhausted registration IDs.
    public func subscribe(
        _ signature: GameEventScriptMessageSignature, handler: any GameEventScriptNativeMessageHandler,
        matchingTags: [String] = [], withoutTags: [String] = [], priority: Int = 0
    ) throws -> GameEventScriptSubscription {
        try subscribe(
            signature, handler: handler, matchingTags: matchingTags, withoutTags: withoutTags, priority: priority,
            matchArguments: true)
    }
    /// Registers a native handler matching a message name with any argument labels and optional tag filters.
    ///
    /// - Returns: An idempotently detachable subscription.
    /// - Throws: An API error for an empty name, invalid tags or exhausted registration IDs.
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
    /// Enqueues an external message using the current subscription snapshot. Returns false before readiness, for
    /// initialization messages, or when the queue rejects delivery.
    @discardableResult public func receive(_ message: GameEventScriptMessage) -> Bool {
        if !isReady || message.name.isEmpty || message.name == "initialization" { return false }
        return enqueue(message)
    }
    /// Processes queued work up to an opcode budget, preserving a paused script for the next frame. Native callbacks
    /// execute atomically.
    ///
    /// - Returns: Execution state, counters and an optional runtime diagnostic.
    /// - Throws: An API error for a nonpositive budget, a host that is not ready or reentrant execution.
    public func executeFrame(opcodeBudget: Int) throws -> GameEventScriptExecutionResult {
        guard opcodeBudget > 0 else { throw GameEventScriptAPIError.invalidArgument("Opcode budget must be positive") }
        guard isReady else {
            throw GameEventScriptAPIError.invalidOperation("Start the host before processing messages")
        }
        guard !pumping else { throw GameEventScriptAPIError.invalidOperation("A host cannot be pumped recursively") }
        pumping = true
        defer { pumping = false }
        return try executeFrameCore(opcodeBudget: opcodeBudget)
    }
    private func executeFrameCore(opcodeBudget: Int) throws -> GameEventScriptExecutionResult {
        opcodes = 0
        processed = 0
        emitted = 0
        published = 0
        diagnostic = nil
        var remaining = opcodeBudget
        var limitReached = false
        while remaining > 0 {
            if active == nil {
                active = starting ? startupQueue.dequeue() : queue.dequeue()
                if active == nil { break }
                initializationInstance = active?.initialization
                initializationOpcodes = 0
                initializationEmits = 0
                initializationPublishes = 0
                initializationDiagnostic = nil
            }
            if scriptActive {
                executingScript = true
                let executed = GameEventScriptVirtualMachine.runSlice(vm!, context: context, budget: remaining)
                executingScript = false
                opcodes += executed
                if initializationInstance != nil { initializationOpcodes += executed }
                remaining -= executed
                if context.budget.isExhausted {
                    vm!.reset()
                    completeHandler()
                    failInitialization(.runtimeLimitReached)
                    limitReached = true
                    break
                }
                if vm!.processing { break }
                if let error = vm!.error { record(error) }
                vm!.reset()
                completeHandler()
                if initializationInstance != nil && context.budget.isExhausted {
                    failInitialization(.runtimeLimitReached)
                    limitReached = true
                    break
                }
                if initializationDiagnostic != nil {
                    failInitialization(.runtimeError)
                    if starting { break }
                }
                continue
            }
            guard let next = active!.nextMatching() else {
                completeInitialization()
                active = nil
                processed += 1
                if limits.maxProcessedEventsPerRun > 0 && processed >= limits.maxProcessedEventsPerRun
                    && (starting ? startupQueue.count : queue.count) > 0
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
                executingScript = true
                defer { executingScript = false }
                do {
                    try GameEventScriptVirtualMachine.begin(
                        vm!, linked: next.instance!.linked, message: active!.message,
                        matchArguments: next.matchArguments, entry: next.address, signature: next.dispatchSignature)
                    scriptActive = true
                } catch let fault as GesRuntimeError {
                    record(fault.diagnostic)
                    vm!.reset()
                    completeHandler()
                    failInitialization(.runtimeError)
                    if starting { break }
                } catch {
                    record(runtimeDiagnostic("runtime.preparationFailed", error: error))
                    vm!.reset()
                    completeHandler()
                    failInitialization(.runtimeError)
                    if starting { break }
                }
            }
        }
        return .init(
            state: diagnostic != nil
                ? .runtimeError : limitReached ? .runtimeLimitReached : isIdle ? .completed : .paused,
            executedOpcodes: opcodes, processedMessages: processed, emittedMessages: emitted,
            publishedMessages: published, diagnostic: diagnostic)
    }
    /// Pumps queued work until idle, a runtime fault or a runtime limit; it does not start a loading host.
    ///
    /// - Returns: Execution state and counters for this call.
    /// - Throws: An API error for invalid lifecycle or reentrant execution.
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
        let accepted =
            !random.hasActiveScopeFault && enqueue(message, initializationOutput: initializationInstance != nil)
        emitted += 1
        if initializationInstance != nil { initializationEmits += 1 }
        observer?.messageEmitted(message, accepted: accepted)
        return accepted
    }
    func publish(_ message: GameEventScriptMessage) -> GameEventScriptPublishResult {
        if initializationInstance != nil { initializationPublishes += 1 }
        let fault = random.hasActiveScopeFault
        let accepted = !fault && enqueue(message, initializationOutput: initializationInstance != nil)
        if let instance = initializationInstance {
            if fault {
                published += 1
                let result = GameEventScriptPublishResult(
                    localAccepted: false, outboundAttempted: false, outboundAccepted: false)
                observer?.messagePublished(message, result: result)
                return result
            }
            initializationPublications.append(.init(message: message, localAccepted: accepted, instance: instance))
            published += 1
            return .init(
                localAccepted: accepted, outboundAttempted: false, outboundAccepted: false,
                outboundDeferred: !fault && sink != nil)
        }
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
        if initializationInstance != nil && initializationDiagnostic == nil {
            initializationDiagnostic = GesStartDiagnostic(value)
        }
        observer?.runtimeError(value)
    }
    private func runtimeDiagnostic(_ code: String, error: (any Error)? = nil) -> GameEventScriptDiagnostic {
        .init(
            phase: .runtime, code: code, programName: activeHandler?.instance?.program.moduleName,
            handlerName: activeHandler?.dispatchSignature, technicalDetails: error.map { String(describing: $0) })
    }
    private func completeHandler() {
        let fault = context.endBoundary(randomBoundary)
        if (initializationInstance != nil ? initializationDiagnostic == nil : diagnostic == nil)
            && !context.budget.isExhausted
        {
            if fault == .boundaryUnderflow { record(runtimeDiagnostic("runtime.randomStackUnderflow")) }
            if fault == .unbalanced { record(runtimeDiagnostic("runtime.randomScopeImbalance")) }
        }
        scriptActive = false
        let handler = activeHandler
        activeHandler = nil
        if let handler { observer?.dispatchCompleted(active!.message, signatureID: handler.dispatchSignature) }
    }
    private var queueFull: Bool {
        limits.maxQueuedMessagesPerRun > 0 && pendingMessageCount >= limits.maxQueuedMessagesPerRun
    }
    private func enqueue(_ message: GameEventScriptMessage, initializationOutput: Bool = false) -> Bool {
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
            if initializationOutput {
                context.budget.exhaust("MaxQueuedMessagesPerRun", limits.maxQueuedMessagesPerRun)
            } else {
                observer?.runtimeLimitReached(
                    "MaxQueuedMessagesPerRun", detail: "Message queue limit reached. Dropped '" + message.name + "'.",
                    limit: limits.maxQueuedMessagesPerRun)
            }
            return false
        }
        queue.enqueue(
            GesPendingMessage(
                message: message, exact: exact, names: names,
                initializationOutputID: initializationOutput ? (initializationInstance?.registrationID ?? 0) : 0))
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

/// A host-local loaded Program handle. Detachment is idempotent and does not revoke ordinary captured deliveries.
public final class GameEventScriptInstance {
    private weak var host: GameEventScriptHost?
    /// Stable registration identifier, unique within the owning host.
    public let registrationID: Int64
    let linked: GesLinkedProgram
    /// The immutable Program shared by this instance.
    public var program: GameEventScriptProgram { linked.program }
    /// Whether the owning host still registers this instance.
    public var isAttached: Bool { host?.isAttached(registrationID) ?? false }
    /// Nil while initialization is pending; retains its success or failure result after completion.
    public internal(set) var startResult: GameEventScriptStartResult? {
        didSet { initializationFailed = startResult != nil && startResult?.state != .ready }
    }
    fileprivate private(set) var initializationFailed = false
    init(host: GameEventScriptHost, id: Int64, linked: GesLinkedProgram) {
        self.host = host
        registrationID = id
        self.linked = linked
    }
    /// Removes this instance from future subscription snapshots. Returns true only when this call removed a
    /// registration.
    @discardableResult public func detach() -> Bool { host?.detach(registrationID) ?? false }
}
/// An idempotently detachable native subscription identified within its owning host.
public final class GameEventScriptSubscription {
    private weak var host: GameEventScriptHost?
    /// Stable registration identifier, unique within the owning host.
    public let registrationID: Int64
    /// Whether the host still registers this native handler.
    public var isSubscribed: Bool { host?.isSubscribed(registrationID) ?? false }
    init(host: GameEventScriptHost, id: Int64) {
        self.host = host
        registrationID = id
    }
    /// Removes this handler from future subscription snapshots. Returns true only when this call removed it; already
    /// captured deliveries remain valid.
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
    var initialization: GameEventScriptInstance? = nil
    var initializationOutputID: Int64 = 0
    var exactIndex = 0, nameIndex = 0
    func canReceive(_ entry: GesSubscriptionEntry) -> Bool {
        !(entry.instance?.initializationFailed ?? false) && entry.matches(message)
    }
    var hasRecipients: Bool { exact.contains(where: canReceive) || names.contains(where: canReceive) }
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
            if canReceive(next) { return next }
        }
        return nil
    }
}
private struct GesMessageQueue {
    private var items: [GesPendingMessage?]
    init(capacity: Int = 16) { items = .init(repeating: nil, count: capacity) }
    private var head = 0
    private(set) var count = 0
    mutating func enqueue(_ value: GesPendingMessage) {
        if count == items.count {
            var expanded = [GesPendingMessage?](repeating: nil, count: max(1, items.count * 2))
            for index in 0..<count { expanded[index] = items[(head + index) % items.count] }
            items = expanded
            head = 0
        }
        items[(head + count) % items.count] = value
        count += 1
    }
    mutating func clear() { while dequeue() != nil {} }
    mutating func removeFailedRecipients(discardingOutputsFrom id: Int64) {
        let length = count
        for _ in 0..<length {
            if let item = dequeue(), item.initializationOutputID != id, item.hasRecipients { enqueue(item) }
        }
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

private struct GesDeferredPublication {
    let message: GameEventScriptMessage
    let localAccepted: Bool
    let instance: GameEventScriptInstance
}

extension GameEventScriptHost {
    /// Initializes the initial load group in Load order without dispatching its emitted messages.
    /// The host becomes ready only if the entire group succeeds; staged outbound publications are then committed.
    /// On failure the host stays unready and initial Program registrations and staged deliveries are discarded.
    /// Repeated non-reentrant calls return the original outcome.
    ///
    /// - Returns: The retained group initialization result, including runtime failures and limit violations.
    /// - Throws: An API error if startup or pumping is reentered.
    public func start() throws -> GameEventScriptStartResult {
        guard !pumping, !starting else {
            throw GameEventScriptAPIError.invalidOperation("A host cannot be started recursively")
        }
        if let result = startupResult { return result }
        var totalOpcodes = 0
        var totalProcessed = 0
        var totalEmitted = 0
        var totalPublished = 0
        starting = true
        pumping = true
        defer {
            starting = false
            pumping = false
        }
        while startupQueue.count > 0 || active != nil {
            let execution = try executeFrameCore(opcodeBudget: Int(Int32.max))
            totalOpcodes += execution.executedOpcodes
            totalProcessed += execution.processedMessages
            totalEmitted += execution.emittedMessages
            totalPublished += execution.publishedMessages
            if execution.state == .runtimeError || execution.state == .runtimeLimitReached {
                let result = GameEventScriptStartResult(
                    state: execution.state == .runtimeError ? .runtimeError : .runtimeLimitReached,
                    diagnostic: execution.diagnostic ?? initializationFailure?.diagnostic
                        ?? initializationLimitDiagnostic(nil),
                    executedOpcodes: totalOpcodes, processedMessages: totalProcessed, emittedMessages: totalEmitted,
                    publishedMessages: totalPublished)
                startupResult = result
                for instance in Array(instances.values) {
                    let prior = instance.startResult
                    instance.startResult = .init(
                        state: result.state, diagnostic: result.diagnostic,
                        executedOpcodes: prior?.executedOpcodes ?? 0, processedMessages: prior?.processedMessages ?? 0,
                        emittedMessages: prior?.emittedMessages ?? 0, publishedMessages: prior?.publishedMessages ?? 0)
                    _ = instance.detach()
                }
                queue.clear()
                startupQueue.clear()
                startupPublications.removeAll(keepingCapacity: true)
                return result
            }
        }
        let result = GameEventScriptStartResult(
            state: .ready, executedOpcodes: totalOpcodes,
            processedMessages: totalProcessed, emittedMessages: totalEmitted, publishedMessages: totalPublished)
        for instance in instances.values where instance.startResult == nil {
            instance.startResult = .init(state: .ready)
        }
        startupResult = result
        isReady = true
        let publications = startupPublications
        startupPublications.removeAll(keepingCapacity: true)
        flushPublications(publications)
        return result
    }
    private func completeInitialization() {
        guard let instance = initializationInstance else { return }
        instance.startResult = .init(
            state: .ready, executedOpcodes: initializationOpcodes, processedMessages: 1,
            emittedMessages: initializationEmits, publishedMessages: initializationPublishes)
        initializationInstance = nil
        initializationDiagnostic = nil
        let publications = initializationPublications
        initializationPublications.removeAll(keepingCapacity: true)
        if starting { startupPublications.append(contentsOf: publications) } else { flushPublications(publications) }
    }
    private func failInitialization(_ state: GameEventScriptStartState) {
        guard let instance = initializationInstance else { return }
        instance.startResult = .init(
            state: state, diagnostic: initializationDiagnostic?.value ?? initializationLimitDiagnostic(instance),
            executedOpcodes: initializationOpcodes, emittedMessages: initializationEmits,
            publishedMessages: initializationPublishes)
        initializationFailure = instance.startResult
        _ = instance.detach()
        initializationPublications.removeAll(keepingCapacity: true)
        initializationInstance = nil
        initializationDiagnostic = nil
        active = nil
        queue.removeFailedRecipients(discardingOutputsFrom: instance.registrationID)
    }
    private func initializationLimitDiagnostic(_ instance: GameEventScriptInstance?) -> GameEventScriptDiagnostic {
        .init(
            phase: .runtime, code: "runtime.initializationLimitReached", programName: instance?.program.moduleName,
            handlerName: "initialization()")
    }
    private func flushPublications(_ publications: [GesDeferredPublication]) {
        for publication in publications {
            let attempted = sink != nil
            var accepted = false
            if attempted {
                do { accepted = try sink!.publish(publication.message) } catch {
                    observer?.runtimeError(
                        .init(
                            phase: .runtime, code: "runtime.publishSinkFailure",
                            programName: publication.instance.program.moduleName, handlerName: "initialization()",
                            technicalDetails: String(describing: error)))
                }
            }
            observer?.messagePublished(
                publication.message,
                result: .init(
                    localAccepted: publication.localAccepted, outboundAttempted: attempted, outboundAccepted: accepted))
        }
    }
}
