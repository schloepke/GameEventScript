// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

struct GesDelayedMessage {
    let deadline: Int64
    let pending: GesPendingMessage
    let publish: Bool
    let owner: GesProgramInstance?

    /// Send ID reported when the entry was scheduled.
    var id: Int64 { pending.id }

    /// Whether a failed initialization abandoned the entry: its owner failed or an emit lost every recipient.
    var isAbandoned: Bool { (owner?.initializationFailed ?? false) || !publish && !pending.hasRecipients }
}
