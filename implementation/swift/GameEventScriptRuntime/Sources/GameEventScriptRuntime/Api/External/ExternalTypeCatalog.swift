// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Immutable collection of unique external type declarations.
public struct GameEventScriptExternalTypeCatalog: GameEventScriptExternalTypeCatalogProtocol {
    /// Type definitions in supplied order.
    public let types: [GameEventScriptExternalTypeDefinition]

    /// Creates a catalog.
    ///
    /// - Throws: An API error for duplicate type names.
    public init(_ types: [GameEventScriptExternalTypeDefinition]) throws {
        if Set(types.map(\.name)).count != types.count { throw GameEventScriptAPIError.invalidArgument("Duplicate external type") }
        self.types = types
    }

    /// Normalizes a type name and returns its declaration, or nil when absent.
    ///
    /// - Throws: An API error for an invalid type name.
    public func resolve(_ typeName: String) throws -> GameEventScriptExternalTypeDefinition? {
        let name = try GesExternalNames.type(typeName)
        return types.first { $0.name == name }
    }
}
