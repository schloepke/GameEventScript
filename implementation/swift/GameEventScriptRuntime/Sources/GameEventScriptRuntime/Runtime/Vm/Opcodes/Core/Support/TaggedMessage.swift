// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    static func tagged(_ message: GameEventScriptMessage, _ values: [GesValue]) throws -> GameEventScriptMessage {
        var tags: [String] = []

        func append(_ value: GesValue) {
            if let list = value.listValue {
                for item in list { append(item) }
            } else {
                // `_` is no tag name, so `nothing` makes the tag clause invalid.
                tags.append(value.isNothing ? "_" : value.asText)
            }
        }

        for value in values { append(value) }
        let untagged = GameEventScriptMessage(normalizedName: message.name, arguments: message.arguments, signatureId: message.signatureId, normalizedTags: [])
        return try untagged.withTags(tags)
    }
}
