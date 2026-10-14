// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Declarative name and type of an external constructor parameter.
public struct GesExternalTypeParameterDefinition {
    /// Normalized lowercase constructor parameter name.
    public let name: String
    /// Normalized source type name, without the leading colon.
    public let typeName: String
    /// Built-in storage kind when the type name maps to one, otherwise nil.
    public let kind: GesBytecodeTypeKind?
    /// Declared quantity unit, or none when unspecified.
    public let unit: GesQuantityUnit

    /// Validates the name and source type identifier.
    ///
    /// - Throws: An API error for an invalid identifier.
    public init(name: String, typeName: String) throws {
        self.name = try GesExternalNames.identifier(name)
        self.typeName = try GesExternalNames.type(typeName)
        kind = GesExternalNames.kind(self.typeName)
        unit = .none
    }

    /// Creates a built-in declaration with an optional quantity unit.
    ///
    /// - Throws: An API error for an invalid name or unsupported kind/unit combination.
    public init(name: String, kind: GesBytecodeTypeKind, unit: GesQuantityUnit = .none) throws {
        self.name = try GesExternalNames.identifier(name)
        self.kind = kind
        self.unit = unit
        typeName = try GesExternalNames.typeName(kind, unit)
    }
}
