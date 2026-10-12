// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Declarative fields and constructors for a host-defined type; contains no native objects or callbacks.
public struct GameEventScriptExternalTypeDefinition {
    /// Normalized external type name.
    public let name: String
    /// Declared fields in definition order.
    public let fields: [GameEventScriptExternalTypeFieldDefinition]
    /// Available constructor signatures.
    public let constructors: [GameEventScriptExternalTypeConstructorDefinition]

    /// Validates field uniqueness and constructor consistency.
    ///
    /// - Throws: An API error for invalid names, duplicate definitions, foreign constructors or parameters without
    /// matching fields.
    public init(name: String, fields: [GameEventScriptExternalTypeFieldDefinition], constructors: [GameEventScriptExternalTypeConstructorDefinition]) throws {
        self.name = try GesExternalNames.type(name)
        self.fields = fields
        self.constructors = constructors
        let fieldNames = Set(fields.map(\.name))
        if fieldNames.count != fields.count || Set(constructors.map(\.signatureID)).count != constructors.count { throw GameEventScriptAPIError.invalidArgument("Duplicate external definition") }
        for constructor in constructors {
            if constructor.typeName != self.name || constructor.parameters.contains(where: { !fieldNames.contains($0.name) }) { throw GameEventScriptAPIError.invalidArgument("Constructor does not match its type or fields") }
        }
    }
}
