// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Host services required at the VM's message boundary, invoked synchronously.
/// The context borrows the receiver weakly; it never owns or pumps its queue.
/// Budget, register and random operations deliberately remain concrete VM services.
protocol GesVmReceiver: AnyObject {
    /// Number of queued messages, including deferred work.
    var pendingMessageCount: Int { get }

    /// Whether the receiver has no active or queued work.
    var isIdle: Bool { get }

    /// Enqueues local delivery and reports whether the message was accepted.
    func emit(_ message: GameEventScriptMessage) -> Bool

    /// Publishes using the host's local delivery and outbound publication rules.
    func publish(_ message: GameEventScriptMessage) -> GameEventScriptPublishResult

    /// Schedules a local or published message after a validated microsecond delay.
    /// Acceptance does not imply immediate delivery, and this call never pumps.
    func send(_ message: GameEventScriptMessage, publish: Bool, microseconds: Int64) -> Bool
}
