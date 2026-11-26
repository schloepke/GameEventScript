// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// A read-only snapshot of one pending named timer of a host (specs/HostRuntime.md#named-timers), as listed by
/// `GameEventScriptHost.pendingTimers`.
public final class GesTimerInfo {
    /// The timer name: the Text or Tag key of the named send as Text, or the handle that names it.
    public let name: GesValue
    /// The absolute deadline of the current firing in whole microseconds on the host's monotonic clock. For a firing that
    /// is already due and waiting in or running from the message queue, it remains that firing's deadline.
    public let deadline: Int64
    /// The repeat period of an `every` timer in whole microseconds, or 0 for a one-shot `after` timer.
    public let period: Int64
    /// The name of the message the timer sends.
    public let messageName: String
    /// The sending Program instance whose names scope the timer.
    public let instance: GesProgramInstance
    /// Whether the timer publishes rather than emits.
    public let publish: Bool

    init(name: GesValue, deadline: Int64, period: Int64, messageName: String, instance: GesProgramInstance, publish: Bool) {
        self.name = name
        self.deadline = deadline
        self.period = period
        self.messageName = messageName
        self.instance = instance
        self.publish = publish
    }
}
