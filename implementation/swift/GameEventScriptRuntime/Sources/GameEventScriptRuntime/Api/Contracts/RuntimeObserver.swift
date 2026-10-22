// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Synchronous observation hooks for host dispatch and failures. Default implementations do nothing.
public protocol GesRuntimeObserver {
    /// Observes a local emit and whether the host accepted it; the default implementation does nothing.
    func messageEmitted(_ message: GameEventScriptMessage, accepted: Bool)

    /// Observes a publish with local and outbound outcome flags; the default implementation does nothing.
    func messagePublished(_ message: GameEventScriptMessage, result: GesPublishResult)

    /// Observes entry into a selected handler; the default implementation does nothing.
    func dispatchStarted(_ message: GameEventScriptMessage, signatureID: String)

    /// Observes completion of a selected handler; the default implementation does nothing.
    func dispatchCompleted(_ message: GameEventScriptMessage, signatureID: String)

    /// Observes a named runtime limit with its configured value and explanatory detail; the default implementation does
    /// nothing.
    func runtimeLimitReached(_ limitName: String, detail: String, limit: Int)

    /// Observes a classified runtime failure; the default implementation does nothing.
    func runtimeError(_ diagnostic: GesDiagnostic)

    /// Observes acceptance of one delayed `emit … after` or `publish … after`, once, when it is scheduled. `id` is the
    /// per-host scheduling ID and increases monotonically, so repeated identical messages differ. `deadline` is the
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
    /// Observes a local emit and whether the host accepted it; the default implementation does nothing.
    public func messageEmitted(_ message: GameEventScriptMessage, accepted: Bool) {}

    /// Observes a publish with local and outbound outcome flags; the default implementation does nothing.
    public func messagePublished(_ message: GameEventScriptMessage, result: GesPublishResult) {}

    /// Observes entry into a selected handler; the default implementation does nothing.
    public func dispatchStarted(_ message: GameEventScriptMessage, signatureID: String) {}

    /// Observes completion of a selected handler; the default implementation does nothing.
    public func dispatchCompleted(_ message: GameEventScriptMessage, signatureID: String) {}

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
