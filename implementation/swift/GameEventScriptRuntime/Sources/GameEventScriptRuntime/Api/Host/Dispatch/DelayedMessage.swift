// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// A message or publication waiting for its monotonic deadline. `sender` is the instance whose handler sent it, which
/// `cancel all` selects by; `timer` is the named timer the entry is the current firing of.
struct GesDelayedMessage {
    let deadline: Int64
    let pending: GesPendingMessage
    let publish: Bool
    let owner: GesProgramInstance?
    var sender: GesProgramInstance? = nil
    var timer: GesNamedTimer? = nil

    /// Send ID reported when the entry was scheduled.
    var id: Int64 { pending.id }

    /// Whether a failed initialization abandoned the entry: its owner failed, an emit lost every recipient, or a repeating
    /// timer lost its registration with its failed instance. A repeating entry takes its recipients when due.
    var isAbandoned: Bool {
        if owner?.initializationFailed ?? false { return true }
        if let timer, timer.repeats { return !timer.registered }
        return !publish && !pending.hasRecipients
    }
}
