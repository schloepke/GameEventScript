// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Declarative external type information available to the compiler without executable bindings.
public protocol GesExternalTypeCatalog {
    /// Available type definitions.
    var types: [GesExternalTypeDefinition] { get }

    /// Looks up a declared type, returning nil when unavailable; implementations may reject invalid names.
    func resolve(_ typeName: String) throws -> GesExternalTypeDefinition?
}
