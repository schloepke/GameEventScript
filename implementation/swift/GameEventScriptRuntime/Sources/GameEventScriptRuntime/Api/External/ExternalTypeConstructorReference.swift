// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Normalized external constructor identity used by the runtime linker.
public struct GesExternalTypeConstructorReference {
    /// Normalized external type name.
    public let typeName: String
    /// Normalized argument labels sorted for constructor identity.
    public let argumentLabels: [String]
    /// Canonical typeName(labels) constructor identity.
    public let signatureID: String

    /// Validates names and sorts labels to construct the canonical signature.
    ///
    /// - Throws: An API error for invalid names.
    public init(typeName: String, argumentLabels: [String] = []) throws {
        self.typeName = try GesExternalTypeNames.type(typeName)
        self.argumentLabels = try argumentLabels.map(GesExternalTypeNames.identifier).sorted()
        signatureID = self.typeName + "(" + self.argumentLabels.joined(separator: ",") + ")"
    }
}
