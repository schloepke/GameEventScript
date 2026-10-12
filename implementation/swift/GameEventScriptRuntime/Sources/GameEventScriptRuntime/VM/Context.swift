// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Synchronous services for the currently executing native, extension, or script handler.
public final class GameEventScriptContext {
    private weak var receiver: (any GesVmReceiver)?
    /// The executing host's private mutable random stream; access is serial.
    public let random: GameEventScriptRandomGenerator
    /// Execution and resource limits configured on the owning host.
    public let runtimeLimits: GameEventScriptRuntimeLimits
    /// The optional host extension resolver.
    public let extensionRegistry: (any GameEventScriptExtensionRegistry)?

    func send(_ message: GameEventScriptMessage, publish: Bool, microseconds: Int64) -> Bool { receiver?.send(message, publish: publish, microseconds: microseconds) ?? false }

    let observer: (any GameEventScriptRuntimeObserver)?
    lazy var budget = GesRuntimeBudget(context: self)

    init(receiver: any GesVmReceiver, random: GameEventScriptRandomGenerator, limits: GameEventScriptRuntimeLimits, extensions: (any GameEventScriptExtensionRegistry)?, observer: (any GameEventScriptRuntimeObserver)?) {
        self.receiver = receiver
        self.random = random
        runtimeLimits = limits
        extensionRegistry = extensions
        self.observer = observer
    }

    /// Number of queued messages, or zero if the owning host no longer exists.
    public var pendingMessageCount: Int { receiver?.pendingMessageCount ?? 0 }
    /// Whether the owning host has no queued or active work.
    public var isIdle: Bool { receiver?.isIdle ?? true }

    /// Enqueues local delivery without pumping. Returns false if the host no longer exists or rejects the message. The
    /// name overload validates message arguments and may throw.
    @discardableResult public func emit(_ message: GameEventScriptMessage) -> Bool { receiver?.emit(message) ?? false }

    /// Enqueues local delivery and attempts outbound delivery, or stages publication during initialization. Returns
    /// separate acceptance flags. The name overload validates the message and may throw.
    @discardableResult public func publish(_ message: GameEventScriptMessage) -> GameEventScriptPublishResult { receiver?.publish(message) ?? .init(localAccepted: false, outboundAttempted: false, outboundAccepted: false) }

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
