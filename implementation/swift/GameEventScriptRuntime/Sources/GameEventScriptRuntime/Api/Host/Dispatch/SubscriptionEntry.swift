// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

final class GesSubscriptionEntry {
    let signature: GameEventScriptMessageSignature
    let requiredTags: [String], excludedTags: [String]
    let matchArguments: Bool
    let priority: Int
    let order: Int64, id: Int64
    let native: (any GameEventScriptNativeMessageHandler)?
    let instance: GameEventScriptInstance?
    let address: Int
    let dispatchSignature: String

    init(
        signature: GameEventScriptMessageSignature,
        requiredTags: [String],
        excludedTags: [String],
        matchArguments: Bool,
        priority: Int,
        order: Int64,
        id: Int64,
        native: (any GameEventScriptNativeMessageHandler)?,
        instance: GameEventScriptInstance?,
        address: Int
    ) {
        self.signature = signature
        self.requiredTags = requiredTags
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

    func matches(_ message: GameEventScriptMessage) -> Bool { requiredTags.allSatisfy { message.tags.contains($0) } && !excludedTags.contains { message.tags.contains($0) } }

    static func precedes(_ left: GesSubscriptionEntry, _ right: GesSubscriptionEntry) -> Bool { left.priority != right.priority ? left.priority > right.priority : left.order < right.order }
}
