// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

// Shared immutable payload; wrapping a message/signature in a register never allocates.
final class GesSignatureStorage: Sendable {
    let name: String
    let parameters: [String]
    let signatureId: String
    // Computed once, as every statically shaped send creates its message from this signature.
    let isSystemEndpoint: Bool

    init(name: String, parameters: [String], signatureId: String) {
        self.name = name
        self.parameters = parameters
        self.signatureId = signatureId
        isSystemEndpoint = GesNames.systemEndpoint(name)
    }
}
