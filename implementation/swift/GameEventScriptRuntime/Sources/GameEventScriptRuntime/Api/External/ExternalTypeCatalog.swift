// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Immutable collection of unique external type declarations.
public struct GesDeclaredExternalTypeCatalog: GesExternalTypeCatalog {
    /// Type definitions in supplied order.
    public let types: [GesExternalTypeDefinition]

    /// Creates a catalog.
    ///
    /// - Throws: An API error for duplicate type names.
    public init(_ types: [GesExternalTypeDefinition]) throws {
        if Set(types.map(\.name)).count != types.count { throw GesApiError.invalidArgument("Duplicate external type") }
        self.types = types
    }

    /// Normalizes a type name and returns its declaration, or nil when absent.
    ///
    /// - Throws: An API error for an invalid type name.
    public func resolve(_ typeName: String) throws -> GesExternalTypeDefinition? {
        let name = try GesExternalNames.type(typeName)
        return types.first { $0.name == name }
    }
}
