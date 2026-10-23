// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Synchronous observation hooks for host dispatch and failures. Default implementations do nothing.
///
/// Each accepted send carries a host-local send ID: it starts at 1, increases by one per accepted send and is reported
/// by every callback about that send, from emit, publish or scheduling to each dispatch. 0 reports a send without an ID.
public protocol GesRuntimeObserver {
    /// Observes a local emit, its send ID (0 when not queued) and whether the host accepted it; the default
    /// implementation does nothing.
    func messageEmitted(_ message: GameEventScriptMessage, id: Int64, accepted: Bool)

    /// Observes a publish, its send ID (also without local recipients; 0 when the host did not accept it) and its local
    /// and outbound outcome flags; the default implementation does nothing.
    func messagePublished(_ message: GameEventScriptMessage, id: Int64, result: GesPublishResult)

    /// Observes entry into a selected handler with the dispatched message's send ID; the default implementation does
    /// nothing.
    func dispatchStarted(_ message: GameEventScriptMessage, id: Int64, signatureID: String)

    /// Observes completion of a selected handler with the dispatched message's send ID; the default implementation does
    /// nothing.
    func dispatchCompleted(_ message: GameEventScriptMessage, id: Int64, signatureID: String)

    /// Observes a named runtime limit with its configured value and explanatory detail; the default implementation does
    /// nothing.
    func runtimeLimitReached(_ limitName: String, detail: String, limit: Int)

    /// Observes a classified runtime failure; the default implementation does nothing.
    func runtimeError(_ diagnostic: GesDiagnostic)

    /// Observes acceptance of one delayed `emit … after` or `publish … after`, once, when it is scheduled. `id` is the
    /// send ID that later emit, publish and dispatch callbacks of this message repeat. `deadline` is the
    /// absolute due time in whole microseconds on the host's monotonic clock, the clock `nextMessageDelay` uses; it is
    /// neither a relative delay nor wall-clock time. `publish` distinguishes a publish from an emit. The default
    /// implementation does nothing.
    func messageScheduled(_ message: GameEventScriptMessage, id: Int64, deadline: Int64, publish: Bool)

    /// Observes removal of a scheduled message before it became due, for example because its initialization failed,
    /// by the `id` that `messageScheduled` reported. A due message is not reported here; its ordinary delivery callbacks
    /// follow. The default implementation does nothing.
    func scheduledMessageDiscarded(id: Int64)
}

extension GesRuntimeObserver {
    /// Observes a local emit and its send ID; the default implementation does nothing.
    public func messageEmitted(_ message: GameEventScriptMessage, id: Int64, accepted: Bool) {}

    /// Observes a publish, its send ID and its outcome flags; the default implementation does nothing.
    public func messagePublished(_ message: GameEventScriptMessage, id: Int64, result: GesPublishResult) {}

    /// Observes entry into a selected handler; the default implementation does nothing.
    public func dispatchStarted(_ message: GameEventScriptMessage, id: Int64, signatureID: String) {}

    /// Observes completion of a selected handler; the default implementation does nothing.
    public func dispatchCompleted(_ message: GameEventScriptMessage, id: Int64, signatureID: String) {}

    /// Observes a named runtime limit with its configured value and explanatory detail; the default implementation does
    /// nothing.
    public func runtimeLimitReached(_ limitName: String, detail: String, limit: Int) {}

    /// Observes a classified runtime failure; the default implementation does nothing.
    public func runtimeError(_ diagnostic: GesDiagnostic) {}

    /// Observes acceptance of one delayed `emit … after` or `publish … after`; the default implementation does nothing.
    public func messageScheduled(_ message: GameEventScriptMessage, id: Int64, deadline: Int64, publish: Bool) {}

    /// Observes removal of a scheduled message before it became due; the default implementation does nothing.
    public func scheduledMessageDiscarded(id: Int64) {}
}
