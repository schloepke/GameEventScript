// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

struct GesDeferredPublication {
    let message: GameEventScriptMessage
    let id: Int64
    let localAccepted: Bool
    let instance: GesProgramInstance
}
