// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

struct GesDelayedMessage {
    let deadline: Int64
    let pending: GesPendingMessage
    let publish: Bool
    let owner: GameEventScriptInstance?
}
