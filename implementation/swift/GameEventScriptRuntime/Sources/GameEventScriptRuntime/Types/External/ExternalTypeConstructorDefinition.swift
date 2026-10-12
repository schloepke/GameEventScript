// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Declarative external constructor signature with parameters in invocation order.
public struct GameEventScriptExternalTypeConstructorDefinition {
    /// Normalized owning type name.
    public let typeName: String
    /// Declared parameters in invocation order.
    public let parameters: [GameEventScriptExternalTypeParameterDefinition]
    /// Canonical constructor identity with sorted labels.
    public let signatureID: String

    /// Creates a validated constructor declaration.
    ///
    /// - Throws: An API error for invalid names or duplicate parameter labels.
    public init(typeName: String, parameters: [GameEventScriptExternalTypeParameterDefinition] = []) throws {
        let reference = try GameEventScriptExternalTypeConstructorReference(typeName: typeName, argumentLabels: parameters.map(\.name))
        if Set(reference.argumentLabels).count != parameters.count { throw GameEventScriptAPIError.invalidArgument("Duplicate external constructor parameter") }
        self.typeName = reference.typeName
        self.parameters = parameters
        signatureID = reference.signatureID
    }
}
