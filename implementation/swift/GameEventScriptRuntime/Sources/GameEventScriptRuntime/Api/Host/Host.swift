// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Autonomous serial event host. Pumping is synchronous; embedding code serializes access.
public final class GameEventScriptHost {
    private let random: GesRandomGenerator
    private let profiler: (any GesProfiler)?
    private let observer: (any GesRuntimeObserver)?
    private let extensions: (any GesExtensionRegistry)?
    private let externalTypes: (any GesExternalTypeRegistry)?
    private let limits: GesRuntimeLimits
    private let clock: GesClockSource?
    // Whether the active handler already reported a runtime error; its random-scope cleanup is then expected recovery.
    private var activeHandlerFailed = false
    private var delayed = GesDelayedQueue()
    // Host-local send IDs start at 1; 0 reports a send without an ID.
    private var lastSendID: Int64 = 0
    private let sink: (any GesPublishSink)?
    private lazy var context = GesContext(host: self, random: random, limits: limits, extensions: extensions, observer: observer)
    private var exact: [String: [GesSubscriptionEntry]] = [:]
    private var byName: [String: [GesSubscriptionEntry]] = [:]
    private var native: [Int64: GesSubscriptionEntry] = [:]
    private var instances: [Int64: GesProgramInstance] = [:]
    private var queue = GesMessageQueue()
    private var active: GesPendingMessage?
    private var activeHandler: GesSubscriptionEntry?
    private var scriptActive = false
    private var startupQueue = GesMessageQueue(capacity: 0)
    private var startupResult: GesStartResult?
    private var initializationFailure: GesStartResult?
    private var initializationOpcodes = 0, initializationEmits = 0, initializationPublishes = 0
    private var starting = false
    private var executingScript = false
    // Counts host operations outside pumping that may call the observer or publish sink, so those callbacks cannot reset the host.
    private var callbackDepth = 0
    private var initializationInstance: GesProgramInstance?
    private var initializationDiagnostic: GesStartDiagnostic?
    private var initializationPublications: [GesDeferredPublication] = []
    private var startupPublications: [GesDeferredPublication] = []
    private var vm: GesVirtualMachine?
    private var nextID: Int64 = 0
    private var nextOrder: Int64 = 0
    private var opcodes = 0, processed = 0, emitted = 0, published = 0
    private var diagnostic: GesDiagnostic?
    private var randomBoundary = 0
    private var pumping = false
    private var idleSinceMicroseconds: Int64?
    // Named timers of all instances, keyed by the sending instance's registration ID and the name
    // (specs/HostRuntime.md#named-timers). A one-shot timer leaves the registry when its firing starts dispatch; a
    // repeating one stays until it is cancelled, replaced or its sender is detached.
    private var timers: [GesNamedTimer.Key: GesNamedTimer] = [:]

    // Hosts are created only through `GesHostBuilder.build()`, which forwards its complete configuration here.
    init(
        seed: Int64? = nil,
        sequence: [Double] = [],
        limits: GesRuntimeLimits = .init(),
        observer: (any GesRuntimeObserver)? = nil,
        extensions: (any GesExtensionRegistry)? = nil,
        externalTypes: (any GesExternalTypeRegistry)? = nil,
        publishSink: (any GesPublishSink)? = nil,
        clock: (any GesClock)? = nil,
        profiler: (any GesProfiler)? = nil
    ) throws {
        guard (0...65535).contains(limits.maxRandomScopeDepth) else { throw GesApiError.invalidArgument("Invalid random scope depth") }
        self.limits = limits
        self.observer = observer
        self.profiler = profiler
        self.extensions = extensions
        self.externalTypes = externalTypes
        sink = publishSink
        self.clock = clock.map(GesClockSource.init)
        random = GesRandomGenerator(seed: seed ?? GesRandomGenerator.entropySeed(), sequence: sequence, maxScopeDepth: limits.maxRandomScopeDepth)
    }

    /// Creates mutable configuration for independent hosts; equivalent to `GesHostBuilder()`.
    public static func makeBuilder() -> GesHostBuilder { .init() }

    /// Whether the complete initial load group started successfully; later instance failures do not clear readiness.
    public private(set) var isReady = false
    /// Number of queued ordinary and initialization messages, excluding an active dispatch.
    public var pendingMessageCount: Int { queue.count + startupQueue.count + delayed.count }
    /// Whether no dispatch is active and both message queues are empty; this does not imply readiness.
    public var isIdle: Bool { active == nil && pendingMessageCount == 0 }

    /// Elapsed whole microseconds in the current ready, work-free phase, or nil before readiness or while work
    /// is active, queued or delayed. Uses the host's monotonic clock; reading never pumps or schedules work.
    /// Rejected messages do not reset the duration. Access requires the same serialization as other host operations.
    public var idleDurationMicroseconds: Int64? {
        guard isReady, !pumping, isIdle, let since = idleSinceMicroseconds else { return nil }
        return max(0, elapsedMicroseconds - since)
    }

    private func updateIdleDuration() {
        if isReady && isIdle {
            if idleSinceMicroseconds == nil { idleSinceMicroseconds = elapsedMicroseconds }
        } else {
            idleSinceMicroseconds = nil
        }
    }

    /// Validates and links before atomically publishing registrations and initialization.
    /// Before `start()`, the instance joins the initial load group. On a ready host, its initialization enters
    /// the normal FIFO; `startResult` remains nil until initialization completes. Failure removes that instance
    /// and its captured deliveries while preserving other recipients.
    ///
    /// - Returns: An idempotently detachable Program instance; loading itself does not pump messages.
    /// - Throws: A format or linking error for invalid/unresolvable Programs, or an API error during
    ///   initialization, script execution or after failed initial startup without a reset. Failed linking adds no
    ///   registrations.
    public func load(_ program: GameEventScriptProgram, priority: Int = 0) throws -> GesProgramInstance {
        guard !starting, !executingScript, startupResult == nil || isReady else { throw GesApiError.invalidOperation("Loading is not allowed during initialization, script execution, or after a failed start without a reset") }
        try program.ensureValid()
        let registers = min(65535, limits.maxRegisterValues > 0 ? limits.maxRegisterValues : 512)
        let depth = min(65535, max(0, limits.maxCallDepth))
        if program.requiredRegisterCount > registers {
            throw GesDynamicLinkError(
                code: "link.requiredRegisterCountExceeded",
                program: program.moduleName,
                message: "Program requires \(program.requiredRegisterCount) registers but the host limit is \(registers)."
            )
        }
        if program.requiredCallStackDepth > depth {
            throw GesDynamicLinkError(
                code: "link.requiredCallStackDepthExceeded",
                program: program.moduleName,
                message: "Program requires call-stack depth \(program.requiredCallStackDepth) but the host limit is \(depth)."
            )
        }
        let linked = try GesLinkedProgram(program, extensions: extensions, types: externalTypes)
        if queueFull && linked.handlers.contains(where: { $0.signature.name == "initialization" }) {
            throw GesDynamicLinkError(code: "link.initializationQueueFull", program: program.moduleName, message: "The host message queue has no capacity for program initialization.")
        }
        if vm == nil { vm = GesVirtualMachine(maxRegisters: registers, maxCallDepth: depth) }
        vm!.prepareCapacity(Int(program.requiredRegisterCount))
        let id = try registrationID()
        linked.profiler = profiler?.createProgramProfiler(program)
        let instance = GesProgramInstance(host: self, id: id, linked: linked)
        var initialization: [GesSubscriptionEntry] = []
        for handler in linked.handlers {
            let entry = GesSubscriptionEntry(
                signature: handler.signature,
                requiredTags: handler.requiredTags,
                requiredTagAlternatives: handler.requiredTagAlternatives,
                excludedTags: handler.excludedTags,
                matchArguments: handler.binding.kind == .messageHandler,
                priority: priority,
                order: order(),
                id: 0,
                native: nil,
                instance: instance,
                address: Int(handler.binding.entryAddress)
            )
            if handler.signature.name == "initialization" { initialization.append(entry) } else { register(entry) }
        }
        instances[instance.registrationID] = instance
        if !initialization.isEmpty {
            let message = GameEventScriptMessage(normalizedName: "initialization", arguments: .empty, signatureId: "initialization()", normalizedTags: [])
            lastSendID += 1
            instance.initialization = initialization.sorted(by: GesSubscriptionEntry.precedes)
            let pending = GesPendingMessage(message: message, exact: instance.initialization!, names: [], id: lastSendID, initialization: instance)
            idleSinceMicroseconds = nil
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
    public func subscribe(_ signature: GesMessageSignature, handler: any GesNativeMessageHandler, matchingTags: [String] = [], withoutTags: [String] = [], priority: Int = 0) throws -> GesSubscription {
        try subscribe(signature, handler: handler, matchingTags: matchingTags, withoutTags: withoutTags, priority: priority, matchArguments: true)
    }

    /// Registers a native handler matching a message name with any argument labels and optional tag filters.
    ///
    /// - Returns: An idempotently detachable subscription.
    /// - Throws: An API error for an empty name, invalid tags or exhausted registration IDs.
    public func subscribeMessageName(_ name: String, handler: any GesNativeMessageHandler, matchingTags: [String] = [], withoutTags: [String] = [], priority: Int = 0) throws -> GesSubscription {
        guard !GesMessageNames.trim(name).isEmpty else { throw GesApiError.invalidArgument("Empty message name") }
        return try subscribe(.init(name: name), handler: handler, matchingTags: matchingTags, withoutTags: withoutTags, priority: priority, matchArguments: false)
    }

    private func subscribe(_ signature: GesMessageSignature, handler: any GesNativeMessageHandler, matchingTags: [String], withoutTags: [String], priority: Int, matchArguments: Bool) throws -> GesSubscription {
        let required = try GameEventScriptMessage(name: "Tags", tags: matchingTags).tags
        let excluded = try GameEventScriptMessage(name: "Tags", tags: withoutTags).tags
        let id = try registrationID()
        let entry = GesSubscriptionEntry(signature: signature, requiredTags: required, excludedTags: excluded, matchArguments: matchArguments, priority: priority, order: order(), id: id, native: handler, instance: nil, address: 0)
        register(entry)
        native[id] = entry
        return GesSubscription(host: self, id: id)
    }

    /// Enqueues an external message using the current subscription snapshot. Returns false before readiness, for
    /// initialization messages, or when the queue rejects delivery.
    @discardableResult public func receive(_ message: GameEventScriptMessage) -> Bool {
        if !isReady || message.name.isEmpty || message.name == "initialization" { return false }
        return enqueue(message) != 0
    }

    /// Processes queued work up to an opcode budget, preserving a paused script for the next frame. Native callbacks
    /// execute atomically.
    ///
    /// - Returns: Execution state, counters and an optional runtime diagnostic.
    /// - Throws: An API error for a nonpositive budget, a host that is not ready or reentrant execution.
    public func executeFrame(opcodeBudget: Int) throws -> GesExecutionResult {
        guard opcodeBudget > 0 else { throw GesApiError.invalidArgument("Opcode budget must be positive") }
        guard isReady else { throw GesApiError.invalidOperation("Start the host before processing messages") }
        guard !pumping else { throw GesApiError.invalidOperation("A host cannot be pumped recursively") }
        pumping = true
        defer {
            pumping = false
            updateIdleDuration()
        }
        return try executeFrameCore(opcodeBudget: opcodeBudget)
    }

    private func executeFrameCore(opcodeBudget: Int) throws -> GesExecutionResult {
        opcodes = 0
        processed = 0
        emitted = 0
        published = 0
        diagnostic = nil
        var remaining = opcodeBudget
        var limitReached = false
        while remaining > 0 {
            if active == nil {
                if !starting { promoteDelayed() }
                if !starting && limits.maxProcessedMessagesPerRun > 0 && processed >= limits.maxProcessedMessagesPerRun && queue.count > 0 {
                    context.reportLimit("MaxProcessedMessagesPerRun", limits.maxProcessedMessagesPerRun)
                    limitReached = true
                    break
                }
                active = starting ? startupQueue.dequeue() : queue.dequeue()
                if active == nil { break }
                if let timer = active!.timer { startTimerDispatch(timer) }
                initializationInstance = active?.initialization
                initializationOpcodes = 0
                initializationEmits = 0
                initializationPublishes = 0
                initializationDiagnostic = nil
            }
            if scriptActive {
                executingScript = true
                let executed = vm!.runSlice(context: context, budget: remaining)
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
                let timer = active!.timer
                active = nil
                processed += 1
                if let timer { completeTimerDispatch(timer) }
                continue
            }
            activeHandler = next
            activeHandlerFailed = false
            randomBoundary = context.beginHandler()
            observer?.dispatchStarted(active!.message, id: active!.id, signatureID: next.dispatchSignature)
            if let handler = next.native {
                do { try handler.handle(active!.message, context: context) } catch let fault as any GesFatalRuntimeError { record(fault.diagnostic) } catch {
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
                    try vm!.begin(linked: next.instance!.linked, message: active!.message, matchArguments: next.matchArguments, entry: next.address, signature: next.dispatchSignature)
                    scriptActive = true
                } catch let fault as any GesFatalRuntimeError {
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
            state: diagnostic != nil ? .runtimeError : limitReached ? .runtimeLimitReached : isIdle ? .completed : active == nil && queue.count == 0 && !starting ? .waiting : .paused,
            executedOpcodes: opcodes,
            processedMessages: processed,
            emittedMessages: emitted,
            publishedMessages: published,
            diagnostic: diagnostic
        )
    }

    /// Pumps runnable work until idle, waiting for future messages, a runtime fault or a runtime limit; never sleeps or starts a loading host.
    ///
    /// - Returns: Execution state and counters for this call.
    /// - Throws: An API error for invalid lifecycle or reentrant execution.
    public func runToCompletion() throws -> GesExecutionResult {
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
            if step.state != .paused || step.executedOpcodes + step.processedMessages + step.emittedMessages + step.publishedMessages == 0 {
                return .init(state: step.state, executedOpcodes: totalOpcodes, processedMessages: totalProcessed, emittedMessages: totalEmitted, publishedMessages: totalPublished, diagnostic: step.diagnostic)
            }
        }
    }

    private var elapsedMicroseconds: Int64 { clock?.value.elapsedMicroseconds ?? GesSystemClock.read() }

    /// Whole microseconds until the earliest delayed message, or nil when none remain. Reading does not pump.
    public var nextMessageDelay: Int64? { delayed.first.map { max(0, $0.deadline - elapsedMicroseconds) } }

    func send(_ message: GameEventScriptMessage, publish: Bool, microseconds: Int64) -> Bool {
        callbackDepth += 1
        defer { callbackDepth -= 1 }
        guard microseconds >= 0 && !random.hasActiveScopeFault else { return false }
        if microseconds == 0 {
            let matches = (exact[message.signatureId] ?? []).contains { $0.matches(message) } || (byName[message.name] ?? []).contains { $0.matches(message) }
            let fallback = message.name != "undeliverable" && ((exact["undeliverable(message)"] ?? []).contains { $0.matches(message) } || (byName["undeliverable"] ?? []).contains { $0.matches(message) })
            if queueFull && (matches || fallback) {
                context.budget.exhaust("MaxQueuedMessages", limits.maxQueuedMessages)
                return false
            }
            if publish { _ = self.publish(message) } else { _ = emit(message) }
            return !context.budget.isExhausted
        }
        let now = elapsedMicroseconds
        guard now >= 0 && microseconds <= Int64.max - now else { return false }
        var exact = self.exact[message.signatureId] ?? []
        var names = byName[message.name] ?? []
        var local = exact.contains(where: { $0.matches(message) }) || names.contains(where: { $0.matches(message) })
        if !local && message.name != "undeliverable" {
            exact = self.exact["undeliverable(message)"] ?? []
            names = byName["undeliverable"] ?? []
            local = exact.contains(where: { $0.matches(message) }) || names.contains(where: { $0.matches(message) })
        }
        // An emit without recipients can never be delivered; like the instant emit it is observed as not queued.
        if !local && !publish {
            _ = emit(message)
            return !context.budget.isExhausted
        }
        if queueFull {
            context.budget.exhaust("MaxQueuedMessages", limits.maxQueuedMessages)
            return false
        }
        lastSendID += 1
        let pending = GesPendingMessage(message: message, exact: exact, names: names, id: lastSendID, initializationOutputID: initializationInstance?.registrationID ?? 0)
        let entry = GesDelayedMessage(deadline: now + microseconds, pending: pending, publish: publish, owner: initializationInstance, sender: activeHandler?.instance)
        delayed.add(entry)
        idleSinceMicroseconds = nil
        observer?.messageScheduled(message, id: entry.id, deadline: entry.deadline, publish: publish, name: nil, period: 0)
        if publish {
            published += 1
            if initializationInstance != nil { initializationPublishes += 1 }
        } else {
            emitted += 1
            if initializationInstance != nil { initializationEmits += 1 }
            observer?.messageEmitted(message, id: entry.id, accepted: true)
        }
        return true
    }

    private func promoteDelayed() {
        guard !delayed.isEmpty else { return }
        let now = elapsedMicroseconds
        while let next = delayed.first, next.deadline <= now {
            // Failed initializations removed their entries already, so every due entry is delivered.
            let entry = delayed.removeFirst()
            if entry.timer != nil {
                promoteTimer(entry, now: now)
                continue
            }
            let local = entry.pending.hasRecipients
            if local { queue.enqueue(entry.pending) }
            if entry.publish {
                let attempted = sink != nil
                var accepted = false
                if attempted { do { accepted = try sink!.publish(entry.pending.message) } catch { observer?.runtimeError(runtimeDiagnostic("runtime.publishSinkFailure", error: error)) } }
                observer?.messagePublished(entry.pending.message, id: entry.id, result: .init(localAccepted: local, outboundAttempted: attempted, outboundAccepted: accepted))
            }
        }
    }

    func emit(_ message: GameEventScriptMessage) -> Bool {
        callbackDepth += 1
        defer { callbackDepth -= 1 }
        let id = random.hasActiveScopeFault ? 0 : enqueue(message, initializationOutput: initializationInstance != nil)
        emitted += 1
        if initializationInstance != nil { initializationEmits += 1 }
        observer?.messageEmitted(message, id: id, accepted: id != 0)
        return id != 0
    }

    func publish(_ message: GameEventScriptMessage) -> GesPublishResult {
        callbackDepth += 1
        defer { callbackDepth -= 1 }
        if initializationInstance != nil { initializationPublishes += 1 }
        let fault = random.hasActiveScopeFault
        // A publish owns its ID even without local recipients; the local entry reuses it.
        var id: Int64 = 0
        if !fault {
            lastSendID += 1
            id = lastSendID
        }
        let accepted = !fault && enqueue(message, initializationOutput: initializationInstance != nil, reservedID: id) != 0
        if let instance = initializationInstance {
            if fault {
                published += 1
                let result = GesPublishResult(localAccepted: false, outboundAttempted: false, outboundAccepted: false)
                observer?.messagePublished(message, id: 0, result: result)
                return result
            }
            initializationPublications.append(.init(message: message, id: id, localAccepted: accepted, instance: instance))
            published += 1
            return .init(localAccepted: accepted, outboundAttempted: false, outboundAccepted: false, outboundDeferred: !fault && sink != nil)
        }
        let attempted = !fault && sink != nil
        var outbound = false
        if attempted { do { outbound = try sink!.publish(message) } catch { observer?.runtimeError(runtimeDiagnostic("runtime.publishSinkFailure", error: error)) } }
        let result = GesPublishResult(localAccepted: accepted, outboundAttempted: attempted, outboundAccepted: outbound)
        published += 1
        observer?.messagePublished(message, id: id, result: result)
        return result
    }

    private func record(_ value: GesDiagnostic) {
        var value = value
        if value.programName == nil { value.programName = activeHandler?.instance?.program.moduleName }
        if value.handlerName == nil { value.handlerName = activeHandler?.dispatchSignature }
        if diagnostic == nil { diagnostic = value }
        if activeHandler != nil { activeHandlerFailed = true }
        if initializationInstance != nil && initializationDiagnostic == nil { initializationDiagnostic = GesStartDiagnostic(value) }
        observer?.runtimeError(value)
    }

    private func runtimeDiagnostic(_ code: String, error: (any Error)? = nil) -> GesDiagnostic {
        .init(phase: .runtime, code: code, programName: activeHandler?.instance?.program.moduleName, handlerName: activeHandler?.dispatchSignature, technicalDetails: error.map { String(describing: $0) })
    }

    private func completeHandler() {
        let fault = context.endBoundary(randomBoundary)
        // Only this handler's own failure suppresses the scope diagnostics, not one of an earlier handler or message.
        if !activeHandlerFailed && !context.budget.isExhausted {
            if fault == .boundaryUnderflow { record(runtimeDiagnostic("runtime.randomStackUnderflow")) }
            if fault == .unbalanced { record(runtimeDiagnostic("runtime.randomScopeImbalance")) }
        }
        scriptActive = false
        let handler = activeHandler
        activeHandler = nil
        if let handler { observer?.dispatchCompleted(active!.message, id: active!.id, signatureID: handler.dispatchSignature) }
    }

    private var queueFull: Bool { limits.maxQueuedMessages > 0 && pendingMessageCount >= limits.maxQueuedMessages }

    // Enqueues under a fresh send ID, or under the ID a publish reserved. Returns that ID, or 0 when nothing was enqueued.
    private func enqueue(_ message: GameEventScriptMessage, initializationOutput: Bool = false, reservedID: Int64 = 0) -> Int64 {
        var exact = self.exact[message.signatureId] ?? []
        var names = byName[message.name] ?? []
        if !exact.contains(where: { $0.matches(message) }) && !names.contains(where: { $0.matches(message) }) {
            if message.name == "undeliverable" { return 0 }
            exact = self.exact["undeliverable(message)"] ?? []
            names = byName["undeliverable"] ?? []
            if !exact.contains(where: { $0.matches(message) }) && !names.contains(where: { $0.matches(message) }) { return 0 }
        }
        if queueFull {
            if initializationOutput {
                context.budget.exhaust("MaxQueuedMessages", limits.maxQueuedMessages)
            } else {
                observer?.runtimeLimitReached("MaxQueuedMessages", detail: "Message queue limit reached. Dropped '" + message.name + "'.", limit: limits.maxQueuedMessages)
            }
            return 0
        }
        idleSinceMicroseconds = nil
        let id = reservedID != 0 ? reservedID : lastSendID + 1
        queue.enqueue(GesPendingMessage(message: message, exact: exact, names: names, id: id, initializationOutputID: initializationOutput ? (initializationInstance?.registrationID ?? 0) : 0))
        if reservedID == 0 { lastSendID = id }
        return id
    }

    private func registrationID() throws -> Int64 {
        if nextID == .max { throw GesApiError.invalidOperation("Registration IDs exhausted") }
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
        callbackDepth += 1
        defer { callbackDepth -= 1 }
        detachTimers(instance)
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

extension GameEventScriptHost {
    /// Initializes the initial load group in Load order without dispatching its emitted messages.
    /// The host becomes ready only if the entire group succeeds; staged outbound publications are then committed.
    /// On failure the host stays unready and initial Program registrations and staged deliveries are discarded.
    /// Repeated non-reentrant calls return the original outcome. A failed start is terminal until `reset()` returns the
    /// host to the loading state.
    ///
    /// - Returns: The retained group initialization result, including runtime failures and limit violations.
    /// - Throws: An API error if startup or pumping is reentered.
    public func start() throws -> GesStartResult {
        guard !pumping, !starting else { throw GesApiError.invalidOperation("A host cannot be started recursively") }
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
            updateIdleDuration()
        }
        while startupQueue.count > 0 || active != nil {
            let execution = try executeFrameCore(opcodeBudget: Int(Int32.max))
            totalOpcodes += execution.executedOpcodes
            totalProcessed += execution.processedMessages
            totalEmitted += execution.emittedMessages
            totalPublished += execution.publishedMessages
            if execution.state == .runtimeError || execution.state == .runtimeLimitReached {
                let result = GesStartResult(
                    state: execution.state == .runtimeError ? .runtimeError : .runtimeLimitReached,
                    diagnostic: execution.diagnostic ?? initializationFailure?.diagnostic ?? initializationLimitDiagnostic(nil),
                    executedOpcodes: totalOpcodes,
                    processedMessages: totalProcessed,
                    emittedMessages: totalEmitted,
                    publishedMessages: totalPublished
                )
                startupResult = result
                for instance in Array(instances.values) {
                    let prior = instance.startResult
                    instance.startResult = .init(
                        state: result.state,
                        diagnostic: result.diagnostic,
                        executedOpcodes: prior?.executedOpcodes ?? 0,
                        processedMessages: prior?.processedMessages ?? 0,
                        emittedMessages: prior?.emittedMessages ?? 0,
                        publishedMessages: prior?.publishedMessages ?? 0
                    )
                    _ = instance.detach()
                }
                // Discards are reported once: pending initializations, then queued messages, then delayed entries.
                startupQueue.clear(reportingTo: observer)
                queue.clear(reportingTo: observer)
                clearDelayed()
                discardDeferredPublications(startupPublications)
                startupPublications.removeAll(keepingCapacity: true)
                return result
            }
        }
        let result = GesStartResult(state: .ready, executedOpcodes: totalOpcodes, processedMessages: totalProcessed, emittedMessages: totalEmitted, publishedMessages: totalPublished)
        for instance in instances.values where instance.startResult == nil { instance.startResult = .init(state: .ready) }
        startupResult = result
        isReady = true
        let publications = startupPublications
        startupPublications.removeAll(keepingCapacity: true)
        flushPublications(publications)
        return result
    }

    private func completeInitialization() {
        guard let instance = initializationInstance else { return }
        instance.startResult = .init(state: .ready, executedOpcodes: initializationOpcodes, processedMessages: 1, emittedMessages: initializationEmits, publishedMessages: initializationPublishes)
        initializationInstance = nil
        initializationDiagnostic = nil
        let publications = initializationPublications
        initializationPublications.removeAll(keepingCapacity: true)
        if starting { startupPublications.append(contentsOf: publications) } else { flushPublications(publications) }
    }

    private func failInitialization(_ state: GesStartState) {
        guard let instance = initializationInstance else { return }
        instance.startResult = .init(
            state: state,
            diagnostic: initializationDiagnostic?.value ?? initializationLimitDiagnostic(instance),
            executedOpcodes: initializationOpcodes,
            emittedMessages: initializationEmits,
            publishedMessages: initializationPublishes
        )
        initializationFailure = instance.startResult
        _ = instance.detach()
        let publications = initializationPublications
        initializationPublications.removeAll(keepingCapacity: true)
        initializationInstance = nil
        initializationDiagnostic = nil
        active = nil
        // A failure during start fails the whole group, which then discards and reports every entry in one pass.
        if starting {
            startupPublications.append(contentsOf: publications)
            return
        }
        queue.removeFailedRecipients(discardingOutputsFrom: instance.registrationID, reportingTo: observer)
        removeFailedDelayed()
        discardDeferredPublications(publications)
    }

    // A deferred publication with a local queue entry was reported with that entry; one that only waited for the sink is
    // reported here, in send-ID order, so every send ID of a failed initialization reaches the observer.
    private func discardDeferredPublications(_ publications: [GesDeferredPublication]) {
        guard let observer else { return }
        for publication in publications where !publication.localAccepted { observer.messageDiscarded(id: publication.id) }
    }

    // Removes entries abandoned by a failed initialization. Each is reported in deadline order before the array
    // changes, so a throwing observer cannot leave it inconsistent.
    private func removeFailedDelayed() {
        if let observer { for entry in delayed.inOrder where entry.isAbandoned { observer.messageDiscarded(id: entry.id) } }
        delayed.removeAll { $0.isAbandoned }
    }

    // Discards every delayed entry, reporting each in deadline order.
    private func clearDelayed() {
        if let observer { for entry in delayed.inOrder { observer.messageDiscarded(id: entry.id) } }
        delayed.removeAll()
    }

    private func initializationLimitDiagnostic(_ instance: GesProgramInstance?) -> GesDiagnostic {
        .init(phase: .runtime, code: "runtime.initializationLimitReached", programName: instance?.program.moduleName, handlerName: "initialization()")
    }

    private func flushPublications(_ publications: [GesDeferredPublication]) {
        for publication in publications {
            let attempted = sink != nil
            var accepted = false
            if attempted {
                do { accepted = try sink!.publish(publication.message) } catch {
                    observer?.runtimeError(.init(phase: .runtime, code: "runtime.publishSinkFailure", programName: publication.instance.program.moduleName, handlerName: "initialization()", technicalDetails: String(describing: error)))
                }
            }
            observer?.messagePublished(publication.message, id: publication.id, result: .init(localAccepted: publication.localAccepted, outboundAttempted: attempted, outboundAccepted: accepted))
        }
    }
}

extension GameEventScriptHost {
    /// A snapshot of the pending named timers of all instances, ordered by deadline and then by send order.
    ///
    /// Reading never pumps the host. A timer whose firing waits in or runs from the message queue keeps that firing's
    /// deadline; a repeating timer stays listed while its handlers run. Each read returns a new array.
    public var pendingTimers: [GesTimerInfo] {
        guard !timers.isEmpty else { return [] }
        return timers.values.sorted(by: Self.timerPrecedes).map {
            GesTimerInfo(name: $0.name, deadline: $0.deadline, period: $0.period, messageName: $0.message.name, instance: $0.instance, publish: $0.publish)
        }
    }

    /// Discards all queued, delayed and active work and returns the host to the loading state, keeping its Programs,
    /// instances, registrations and random state.
    ///
    /// The runnable queue, delayed entries and named timers, buffered initialization outputs, a paused handler, the
    /// waiting state and every start result are cleared without individual discard reports; the observer receives one
    /// `hostReset()`. The initialization of each attached instance is queued again in load order under new send IDs, so
    /// a following `start()` runs it as the initial group. Send IDs keep counting and the random generator is neither
    /// reseeded nor rewound. A reset is also allowed after a failed start, which has already detached its Programs.
    ///
    /// - Throws: An API error while the host is starting or pumping, or from an observer or publish-sink callback of another
    ///   host operation.
    public func reset() throws {
        guard !pumping, !starting, !executingScript, callbackDepth == 0 else { throw GesApiError.invalidOperation("A host cannot be reset while it is starting or executing.") }
        if active != nil {
            if scriptActive { vm?.reset() }
            if activeHandler != nil { _ = random.releaseBoundary(randomBoundary) }
        }
        active = nil
        scriptActive = false
        activeHandler = nil
        initializationInstance = nil
        initializationDiagnostic = nil
        initializationFailure = nil
        initializationPublications.removeAll()
        startupPublications.removeAll()
        queue.reset()
        startupQueue.reset()
        delayed.removeAll()
        for timer in timers.values { timer.registered = false }
        timers.removeAll()
        startupResult = nil
        isReady = false
        idleSinceMicroseconds = nil
        // Initialization runs again in load order, which registration IDs follow.
        for instance in instances.values.sorted(by: { $0.registrationID < $1.registrationID }) {
            instance.startResult = nil
            if let entries = instance.initialization, !entries.isEmpty {
                lastSendID += 1
                let message = GameEventScriptMessage(normalizedName: "initialization", arguments: .empty, signatureId: "initialization()", normalizedTags: [])
                startupQueue.enqueue(GesPendingMessage(message: message, exact: entries, names: [], id: lastSendID, initialization: instance))
            }
        }
        observer?.hostReset()
    }

    private static func timerPrecedes(_ left: GesNamedTimer, _ right: GesNamedTimer) -> Bool {
        left.deadline != right.deadline ? left.deadline < right.deadline : left.id < right.id
    }

    private func hasMatch(_ message: GameEventScriptMessage, _ exact: [GesSubscriptionEntry], _ names: [GesSubscriptionEntry]) -> Bool {
        exact.contains(where: { $0.matches(message) }) || names.contains(where: { $0.matches(message) })
    }

    func sendNamed(_ message: GameEventScriptMessage, publish: Bool, microseconds: Int64, name: String, repeats: Bool) -> Bool {
        guard !random.hasActiveScopeFault, let sender = activeHandler?.instance else { return false }
        guard repeats ? microseconds > 0 : microseconds >= 0 else { return false }
        let now = elapsedMicroseconds
        guard now >= 0 && microseconds <= Int64.max - now else { return false }
        let key = GesNamedTimer.Key(instance: sender.registrationID, name: name)
        let old = timers[key]
        if old == nil && limits.maxNamedTimers > 0 && timers.count >= limits.maxNamedTimers {
            context.budget.exhaust("MaxNamedTimers", limits.maxNamedTimers)
            return false
        }
        // A replaced firing that still waits gives its queue slot to the new one.
        if !(old?.state == .delayed || old?.state == .runnable) && queueFull {
            context.budget.exhaust("MaxQueuedMessages", limits.maxQueuedMessages)
            return false
        }
        var exact: [GesSubscriptionEntry] = []
        var names: [GesSubscriptionEntry] = []
        var local = false
        if !repeats {
            exact = self.exact[message.signatureId] ?? []
            names = byName[message.name] ?? []
            local = hasMatch(message, exact, names)
            if !local && message.name != "undeliverable" {
                exact = self.exact["undeliverable(message)"] ?? []
                names = byName["undeliverable"] ?? []
                local = hasMatch(message, exact, names)
            }
        }
        if let old { removeTimer(old, report: true) }
        // Like an unnamed delayed emit, a one-shot emit without recipients is never queued; the name stays free.
        if !repeats && !local && !publish {
            _ = emit(message)
            return !context.budget.isExhausted
        }
        let timer = GesNamedTimer(instance: sender, name: name, period: repeats ? microseconds : 0, publish: publish, message: message, owner: initializationInstance)
        timer.registered = true
        timers[key] = timer
        lastSendID += 1
        timer.id = lastSendID
        timer.deadline = now + microseconds
        timer.state = .delayed
        var pending = GesPendingMessage(message: message, exact: exact, names: names, id: timer.id, initializationOutputID: initializationInstance?.registrationID ?? 0)
        pending.timer = timer
        delayed.add(.init(deadline: timer.deadline, pending: pending, publish: publish, owner: initializationInstance, sender: sender, timer: timer))
        idleSinceMicroseconds = nil
        observer?.messageScheduled(message, id: timer.id, deadline: timer.deadline, publish: publish, name: name, period: timer.period)
        if publish {
            published += 1
            if initializationInstance != nil { initializationPublishes += 1 }
        } else {
            emitted += 1
            if initializationInstance != nil { initializationEmits += 1 }
            if !repeats { observer?.messageEmitted(message, id: timer.id, accepted: true) }
        }
        return true
    }

    // Mode 0 cancels one name, mode 1 every named timer and mode 2 also the unnamed delayed sends of the active instance.
    func cancelTimers(mode: Int, name: String?) -> Bool {
        guard let sender = activeHandler?.instance else { return false }
        var selected: [GesNamedTimer] = []
        if !timers.isEmpty {
            if mode == 0 {
                if let name, let timer = timers[.init(instance: sender.registrationID, name: name)] { selected.append(timer) }
            } else {
                selected = timers.values.filter { $0.instance === sender }
            }
        }
        let unnamed = mode == 2 && !delayed.isEmpty ? delayed.inOrder.filter { $0.timer == nil && $0.sender === sender } : []
        if selected.isEmpty && unnamed.isEmpty { return false }
        // Discards are reported before anything changes: waiting runnable firings in send order, then delayed entries in
        // deadline order. A firing whose handlers already run only loses its name.
        if let observer {
            var runnable: [Int64] = []
            var waiting: [(deadline: Int64, id: Int64)] = []
            for timer in selected {
                if timer.state == .runnable { runnable.append(timer.id) } else if timer.state == .delayed { waiting.append((timer.deadline, timer.id)) }
            }
            for entry in unnamed { waiting.append((entry.deadline, entry.id)) }
            runnable.sort()
            waiting.sort { $0.deadline != $1.deadline ? $0.deadline < $1.deadline : $0.id < $1.id }
            for id in runnable { observer.messageDiscarded(id: id) }
            for entry in waiting { observer.messageDiscarded(id: entry.id) }
        }
        for timer in selected { removeTimer(timer, report: false) }
        for entry in unnamed { delayed.remove(id: entry.id) }
        return true
    }

    // Unregisters a timer and removes its waiting firing, if any.
    private func removeTimer(_ timer: GesNamedTimer, report: Bool) {
        unregisterTimer(timer)
        let removed =
            switch timer.state {
            case .delayed: delayed.remove(id: timer.id)
            case .runnable: queue.remove(id: timer.id)
            case .dispatching: false
            }
        if removed && report { observer?.messageDiscarded(id: timer.id) }
    }

    private func unregisterTimer(_ timer: GesNamedTimer) {
        guard timer.registered else { return }
        timer.registered = false
        timers[timer.key] = nil
    }

    // Promotes one due named firing. A repeating timer snapshots its recipients only now; without one it is armed again at
    // once, as if its handlers had completed.
    private func promoteTimer(_ entry: GesDelayedMessage, now: Int64) {
        let timer = entry.timer!
        var pending = entry.pending
        if timer.repeats {
            let message = timer.message
            var exact = self.exact[message.signatureId] ?? []
            var names = byName[message.name] ?? []
            if !hasMatch(message, exact, names) && message.name != "undeliverable" {
                exact = self.exact["undeliverable(message)"] ?? []
                names = byName["undeliverable"] ?? []
            }
            pending = GesPendingMessage(message: message, exact: exact, names: names, id: timer.id, initializationOutputID: entry.owner?.registrationID ?? 0)
            pending.timer = timer
        }
        let local = pending.hasRecipients
        if local {
            queue.enqueue(pending)
            timer.state = .runnable
            if timer.repeats && !timer.publish { observer?.messageEmitted(timer.message, id: timer.id, accepted: true) }
        } else {
            // A due one-shot firing without a recipient is dropped silently, like an unnamed delayed send.
            if timer.repeats {
                if !timer.publish { observer?.messageDiscarded(id: timer.id) }
            } else {
                unregisterTimer(timer)
            }
        }
        if timer.publish {
            let attempted = sink != nil
            var accepted = false
            if attempted { do { accepted = try sink!.publish(timer.message) } catch { observer?.runtimeError(runtimeDiagnostic("runtime.publishSinkFailure", error: error)) } }
            observer?.messagePublished(timer.message, id: timer.id, result: .init(localAccepted: local, outboundAttempted: attempted, outboundAccepted: accepted))
        }
        if !local && timer.repeats && timer.registered { armTimer(timer, now: now) }
    }

    // Arms a repeating timer for its next firing, one period after now, under a new send ID. No firing is caught up.
    private func armTimer(_ timer: GesNamedTimer, now: Int64) {
        guard now >= 0 && timer.period <= Int64.max - now else {
            unregisterTimer(timer)
            return
        }
        lastSendID += 1
        timer.id = lastSendID
        timer.deadline = now + timer.period
        timer.state = .delayed
        var pending = GesPendingMessage(message: timer.message, exact: [], names: [], id: timer.id)
        pending.timer = timer
        delayed.add(.init(deadline: timer.deadline, pending: pending, publish: timer.publish, owner: timer.owner, sender: timer.instance, timer: timer))
        idleSinceMicroseconds = nil
        observer?.messageScheduled(timer.message, id: timer.id, deadline: timer.deadline, publish: timer.publish, name: timer.name, period: timer.period)
    }

    // A one-shot firing releases its name when its dispatch starts; a repeating one marks itself as running.
    private func startTimerDispatch(_ timer: GesNamedTimer) {
        if timer.repeats { timer.state = .dispatching } else { unregisterTimer(timer) }
    }

    // Arms a repeating timer again once all handlers of its firing completed, unless it was cancelled meanwhile.
    private func completeTimerDispatch(_ timer: GesNamedTimer) {
        if timer.repeats && timer.registered { armTimer(timer, now: elapsedMicroseconds) }
    }

    // Ends the named timers of a detached instance. A waiting repeating firing is discarded unless the instance failed its
    // initialization, whose entries the failure path discards; queued firings and one-shot entries keep their delivery.
    private func detachTimers(_ instance: GesProgramInstance) {
        guard !timers.isEmpty else { return }
        let ended = timers.values.filter { $0.instance === instance }.sorted(by: Self.timerPrecedes)
        let failed = instance.startResult != nil && instance.startResult?.state != .ready
        for timer in ended {
            if timer.repeats && !failed && timer.state == .delayed { removeTimer(timer, report: true) } else { unregisterTimer(timer) }
        }
    }
}
