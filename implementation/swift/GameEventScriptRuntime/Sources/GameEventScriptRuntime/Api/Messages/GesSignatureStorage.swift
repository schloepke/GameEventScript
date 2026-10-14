// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Shared immutable payload; wrapping a message/signature in a register never allocates.
final class GesSignatureStorage: Sendable {
    let name: String
    let parameters: [String]
    let signatureId: String

    init(name: String, parameters: [String], signatureId: String) {
        self.name = name
        self.parameters = parameters
        self.signatureId = signatureId
    }
}
