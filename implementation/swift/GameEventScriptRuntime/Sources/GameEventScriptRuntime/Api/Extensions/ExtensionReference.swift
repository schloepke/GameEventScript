// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Normalized extension namespace, function and ordered argument labels used during host linking.
public struct GameEventScriptExtensionReference {
    /// Normalized lowercase extension namespace.
    public let extensionName: String
    /// Normalized lowercase function name.
    public let functionName: String
    /// Ordered external labels; _ denotes a positional argument.
    public let argumentLabels: [String]
    /// Canonical namespace.function(labels) identity.
    public let signatureID: String

    /// Validates and normalizes namespace, function and parameter labels.
    ///
    /// - Throws: An API or message error for invalid identifiers.
    public init(extensionName: String, functionName: String, argumentLabels: [String] = []) throws {
        self.extensionName = try GesExternalNames.identifier(extensionName)
        self.functionName = try GesExternalNames.identifier(functionName)
        self.argumentLabels = try argumentLabels.map { try GameEventScriptMessageSignature.normalizeParameterName($0) }
        signatureID = self.extensionName + "." + self.functionName + "(" + self.argumentLabels.joined(separator: ",") + ")"
    }
}
