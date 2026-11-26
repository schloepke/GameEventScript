// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Where the current firing of a named timer is: waiting for its deadline, promoted to the FIFO, or being dispatched.
enum GesNamedTimerState {
    case delayed
    case runnable
    case dispatching
}

/// One named `emit|publish after|every` of a sending instance (specs/HostRuntime.md#named-timers). The host keeps it
/// registered under the instance and the name until it fires once (`after`), is cancelled or replaced, or its instance
/// detaches; an `every` timer re-arms when each logical message it sent completes.
final class GesNamedTimer {
    let instance: GesProgramInstance
    let name: String
    /// The repeat period in whole microseconds, or 0 for a one-shot `after` timer.
    let period: Int64
    let publish: Bool
    let message: GameEventScriptMessage
    /// The initializing instance that scheduled the timer, whose failure abandons it.
    let owner: GesProgramInstance?
    /// The send ID of the current firing.
    var id: Int64 = 0
    /// The deadline of the current firing on the host clock.
    var deadline: Int64 = 0
    var state = GesNamedTimerState.delayed
    /// Whether the host's registry still maps the instance and name to this timer.
    var registered = false

    init(instance: GesProgramInstance, name: String, period: Int64, publish: Bool, message: GameEventScriptMessage, owner: GesProgramInstance?) {
        self.instance = instance
        self.name = name
        self.period = period
        self.publish = publish
        self.message = message
        self.owner = owner
    }

    var repeats: Bool { period > 0 }

    var key: Key { .init(instance: instance.registrationID, name: name) }

    /// The registry key of a named timer: the sending instance's registration ID and the name.
    struct Key: Hashable {
        let instance: Int64
        let name: String
    }
}
