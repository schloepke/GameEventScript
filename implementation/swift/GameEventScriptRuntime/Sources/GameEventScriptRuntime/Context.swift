// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Synchronous services for the currently executing native, extension, or script handler.
public final class GameEventScriptContext {
    private weak var host: GameEventScriptHost?
    /// The executing host's private mutable random stream; access is serial.
    public let random: GameEventScriptRandomGenerator
    /// Execution and resource limits configured on the owning host.
    public let runtimeLimits: GameEventScriptRuntimeLimits
    /// The optional host extension resolver.
    public let extensionRegistry: (any GameEventScriptExtensionRegistry)?
    let observer: (any GameEventScriptRuntimeObserver)?
    lazy var budget = GesRuntimeBudget(context: self)

    init(host: GameEventScriptHost, random: GameEventScriptRandomGenerator, limits: GameEventScriptRuntimeLimits, extensions: (any GameEventScriptExtensionRegistry)?, observer: (any GameEventScriptRuntimeObserver)?) {
        self.host = host
        self.random = random
        runtimeLimits = limits
        extensionRegistry = extensions
        self.observer = observer
    }

    /// Number of queued messages, or zero if the owning host no longer exists.
    public var pendingMessageCount: Int { host?.pendingMessageCount ?? 0 }
    /// Whether the owning host has no queued or active work.
    public var isIdle: Bool { host?.isIdle ?? true }

    /// Enqueues local delivery without pumping. Returns false if the host no longer exists or rejects the message. The
    /// name overload validates message arguments and may throw.
    @discardableResult public func emit(_ message: GameEventScriptMessage) -> Bool { host?.emit(message) ?? false }

    /// Enqueues local delivery and attempts outbound delivery, or stages publication during initialization. Returns
    /// separate acceptance flags. The name overload validates the message and may throw.
    @discardableResult public func publish(_ message: GameEventScriptMessage) -> GameEventScriptPublishResult { host?.publish(message) ?? .init(localAccepted: false, outboundAttempted: false, outboundAccepted: false) }

    /// Enqueues local delivery without pumping. Returns false if the host no longer exists or rejects the message. The
    /// name overload validates message arguments and may throw.
    @discardableResult public func emit(_ name: String, arguments: [GameEventScriptMessageArgument] = []) throws -> Bool { emit(try .init(name: name, arguments: arguments)) }

    /// Enqueues local delivery and attempts outbound delivery, or stages publication during initialization. Returns
    /// separate acceptance flags. The name overload validates the message and may throw.
    @discardableResult public func publish(_ name: String, arguments: [GameEventScriptMessageArgument] = []) throws -> GameEventScriptPublishResult { publish(try .init(name: name, arguments: arguments)) }

    func beginHandler() -> Int {
        budget.reset()
        return random.markBoundary()
    }

    func endBoundary(_ token: Int) -> GameEventScriptRandomGenerator.BoundaryFault {
        let fault = random.releaseBoundary(token)
        if fault == .limitExceeded { budget.exhaust("MaxRandomScopeDepth", runtimeLimits.maxRandomScopeDepth) }
        return fault
    }

    func reportLimit(_ name: String, _ limit: Int) { observer?.runtimeLimitReached(name, detail: "Runtime limit reached", limit: limit) }
}

final class GesRuntimeBudget {
    unowned let context: GameEventScriptContext
    var executionSteps = 0
    var loopIterations = 0
    var callDepth = 0
    private(set) var isExhausted = false
    var limits: GameEventScriptRuntimeLimits { context.runtimeLimits }

    init(context: GameEventScriptContext) { self.context = context }

    func reset() {
        executionSteps = 0
        loopIterations = 0
        callDepth = 0
        isExhausted = false
    }

    func reserve(_ requested: Int) -> Int {
        if isExhausted || requested <= 0 { return 0 }
        if limits.maxExecutionSteps <= 0 { return requested }
        let remaining = limits.maxExecutionSteps - executionSteps
        if remaining <= 0 {
            exhaust("MaxExecutionSteps", limits.maxExecutionSteps)
            return 0
        }
        let reserved = min(requested, remaining)
        executionSteps += reserved
        return reserved
    }

    func complete(executed: Int, reserved: Int, processing: Bool) {
        if limits.maxExecutionSteps <= 0 || isExhausted { return }
        executionSteps -= reserved - executed
        if processing && executionSteps >= limits.maxExecutionSteps { exhaust("MaxExecutionSteps", limits.maxExecutionSteps) }
    }

    func loop() -> Bool {
        if isExhausted { return false }
        if limits.maxLoopIterations > 0 && loopIterations >= limits.maxLoopIterations {
            exhaust("MaxLoopIterations", limits.maxLoopIterations)
            return false
        }
        loopIterations += 1
        return true
    }

    func enterCall() -> Bool {
        if isExhausted { return false }
        if limits.maxCallDepth > 0 && callDepth >= limits.maxCallDepth {
            exhaust("MaxCallDepth", limits.maxCallDepth)
            return false
        }
        callDepth += 1
        return true
    }

    func exitCall() { if callDepth > 0 { callDepth -= 1 } }

    func generated(_ count: Int) -> Bool {
        if isExhausted { return false }
        if limits.maxGeneratedCollectionItems > 0 && count > limits.maxGeneratedCollectionItems {
            exhaust("MaxGeneratedCollectionItems", limits.maxGeneratedCollectionItems)
            return false
        }
        return true
    }

    func range(_ count: Int64) -> Bool {
        if limits.maxRangeItems > 0 && count > limits.maxRangeItems {
            context.reportLimit("MaxRangeItems", limits.maxRangeItems)
            return false
        }
        return true
    }

    func dice(count: Int, sides: Int) -> Bool {
        if limits.maxDiceCount > 0 && count > limits.maxDiceCount {
            context.reportLimit("MaxDiceCount", limits.maxDiceCount)
            return false
        }
        if limits.maxDiceSides > 0 && sides > limits.maxDiceSides {
            context.reportLimit("MaxDiceSides", limits.maxDiceSides)
            return false
        }
        return true
    }

    func exhaust(_ name: String, _ limit: Int) {
        if isExhausted { return }
        isExhausted = true
        context.reportLimit(name, limit)
    }
}
