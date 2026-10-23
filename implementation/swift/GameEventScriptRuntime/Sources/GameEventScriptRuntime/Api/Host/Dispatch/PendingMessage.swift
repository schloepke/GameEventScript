// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

struct GesPendingMessage {
    let message: GameEventScriptMessage
    let exact: [GesSubscriptionEntry], names: [GesSubscriptionEntry]
    /// Host-local send ID that observer callbacks report for this message.
    let id: Int64
    var initialization: GesProgramInstance? = nil
    var initializationOutputID: Int64 = 0
    var exactIndex = 0, nameIndex = 0

    func canReceive(_ entry: GesSubscriptionEntry) -> Bool { !(entry.instance?.initializationFailed ?? false) && entry.matches(message) }

    var hasRecipients: Bool { exact.contains(where: canReceive) || names.contains(where: canReceive) }

    mutating func nextMatching() -> GesSubscriptionEntry? {
        while exactIndex < exact.count || nameIndex < names.count {
            let next: GesSubscriptionEntry
            if exactIndex >= exact.count {
                next = names[nameIndex]
                nameIndex += 1
            } else if nameIndex >= names.count || GesSubscriptionEntry.precedes(exact[exactIndex], names[nameIndex]) {
                next = exact[exactIndex]
                exactIndex += 1
            } else {
                next = names[nameIndex]
                nameIndex += 1
            }
            if canReceive(next) { return next }
        }
        return nil
    }
}
