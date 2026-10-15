// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Declarative fields and constructors for a host-defined type; contains no native objects or callbacks.
public struct GesExternalTypeDefinition {
    /// Normalized external type name.
    public let name: String
    /// Declared fields in definition order.
    public let fields: [GesExternalTypeFieldDefinition]
    /// Available constructor signatures.
    public let constructors: [GesExternalTypeConstructorDefinition]

    /// Validates field uniqueness and constructor consistency.
    ///
    /// - Throws: An API error for invalid names, duplicate definitions, foreign constructors or parameters without
    /// matching fields.
    public init(name: String, fields: [GesExternalTypeFieldDefinition], constructors: [GesExternalTypeConstructorDefinition]) throws {
        self.name = try GesExternalTypeNames.type(name)
        self.fields = fields
        self.constructors = constructors
        let fieldNames = Set(fields.map(\.name))
        if fieldNames.count != fields.count || Set(constructors.map(\.signatureID)).count != constructors.count { throw GesApiError.invalidArgument("Duplicate external definition") }
        for constructor in constructors {
            if constructor.typeName != self.name || constructor.parameters.contains(where: { !fieldNames.contains($0.name) }) { throw GesApiError.invalidArgument("Constructor does not match its type or fields") }
        }
    }
}
