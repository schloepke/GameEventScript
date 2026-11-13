// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Synchronous services for the currently executing native, extension, or script handler.
public final class GesContext {
    private weak var host: GameEventScriptHost?
    /// The executing host's private mutable random stream; access is serial.
    public let random: GesRandomGenerator
    /// Execution and resource limits configured on the owning host.
    public let runtimeLimits: GesRuntimeLimits
    /// The host's extension resolver; a host without one has an empty registry that resolves nothing, never nil.
    public let extensionRegistry: any GesExtensionRegistry

    func send(_ message: GameEventScriptMessage, publish: Bool, microseconds: Int64) -> Bool { host?.send(message, publish: publish, microseconds: microseconds) ?? false }

    let observer: (any GesRuntimeObserver)?
    lazy var budget = GesRuntimeBudget(context: self)

    init(host: GameEventScriptHost, random: GesRandomGenerator, limits: GesRuntimeLimits, extensions: (any GesExtensionRegistry)?, observer: (any GesRuntimeObserver)?) {
        self.host = host
        self.random = random
        runtimeLimits = limits
        extensionRegistry = extensions ?? GesEmptyExtensionRegistry()
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
    @discardableResult public func publish(_ message: GameEventScriptMessage) -> GesPublishResult { host?.publish(message) ?? .init(localAccepted: false, outboundAttempted: false, outboundAccepted: false) }

    /// Enqueues local delivery without pumping. Returns false if the host no longer exists or rejects the message. The
    /// name overload validates message arguments and may throw.
    @discardableResult public func emit(_ name: String, arguments: [GesMessageArgument] = []) throws -> Bool { emit(try .init(name: name, arguments: arguments)) }

    /// Enqueues local delivery and attempts outbound delivery, or stages publication during initialization. Returns
    /// separate acceptance flags. The name overload validates the message and may throw.
    @discardableResult public func publish(_ name: String, arguments: [GesMessageArgument] = []) throws -> GesPublishResult { publish(try .init(name: name, arguments: arguments)) }

    func beginHandler() -> Int {
        budget.reset()
        return random.markBoundary()
    }

    func endBoundary(_ token: Int) -> GesRandomGenerator.BoundaryFault {
        let fault = random.releaseBoundary(token)
        if fault == .limitExceeded { budget.exhaust("MaxRandomScopeDepth", runtimeLimits.maxRandomScopeDepth) }
        return fault
    }

    func reportLimit(_ name: String, _ limit: Int) { observer?.runtimeLimitReached(name, detail: Self.limitDetail(name), limit: limit) }

    // What a limit stops, as the C# Runtime words it, so that both CLIs explain a reached limit.
    static func limitDetail(_ name: String) -> String {
        switch name {
        case "MaxProcessedMessagesPerRun": "Message processing limit reached before the host became idle."
        case "MaxQueuedMessages": "Message queue limit reached."
        case "MaxExecutionSteps": "Execution exceeds the configured step limit."
        case "MaxLoopIterations": "Loop iterations exceed the configured limit."
        case "MaxRangeItems": "Range exceeds the configured item limit."
        case "MaxGeneratedCollectionItems": "Generated collection exceeds the configured item limit."
        case "MaxDiceCount": "Dice count exceeds the configured limit."
        case "MaxDiceSides": "Dice side count exceeds the configured limit."
        case "MaxRandomScopeDepth": "Random scopes nest deeper than the configured limit."
        case "MaxValueDepth": "Value nesting exceeds the configured depth."
        case "MaxLiteralInputScalars": "Literal input exceeds the scalar limit."
        default: "Literal parsing exceeds the resource limit."
        }
    }
}
