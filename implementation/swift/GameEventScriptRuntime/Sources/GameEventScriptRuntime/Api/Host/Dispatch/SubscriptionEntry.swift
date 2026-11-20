// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

final class GesSubscriptionEntry {
    let signature: GesMessageSignature
    let requiredTags: [String], excludedTags: [String]
    /// The `|` groups of a script handler's `matching` clauses; nil for handlers without them and native subscriptions.
    let requiredTagAlternatives: [[String]]?
    let matchArguments: Bool
    let priority: Int
    let order: Int64, id: Int64
    let native: (any GesNativeMessageHandler)?
    let instance: GesProgramInstance?
    let address: Int
    let dispatchSignature: String

    init(
        signature: GesMessageSignature,
        requiredTags: [String],
        requiredTagAlternatives: [[String]]? = nil,
        excludedTags: [String],
        matchArguments: Bool,
        priority: Int,
        order: Int64,
        id: Int64,
        native: (any GesNativeMessageHandler)?,
        instance: GesProgramInstance?,
        address: Int
    ) {
        self.signature = signature
        self.requiredTags = requiredTags
        self.requiredTagAlternatives = requiredTagAlternatives
        self.excludedTags = excludedTags
        self.matchArguments = matchArguments
        self.priority = priority
        self.order = order
        self.id = id
        self.native = native
        self.instance = instance
        self.address = address
        dispatchSignature = matchArguments ? signature.signatureId : signature.name + "(*)"
    }

    func matches(_ message: GameEventScriptMessage) -> Bool {
        guard requiredTags.allSatisfy({ message.tags.contains($0) }), !excludedTags.contains(where: { message.tags.contains($0) }) else { return false }
        guard let alternatives = requiredTagAlternatives else { return true }
        return alternatives.allSatisfy { group in group.contains { message.tags.contains($0) } }
    }

    static func precedes(_ left: GesSubscriptionEntry, _ right: GesSubscriptionEntry) -> Bool { left.priority != right.priority ? left.priority > right.priority : left.order < right.order }
}
