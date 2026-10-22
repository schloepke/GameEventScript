// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Host-side resolver for executable external constructors.
public protocol GesExternalTypeRegistry {
    /// Resolves a constructor reference, or returns nil when unavailable. Resolver errors propagate through linking.
    func resolve(_ reference: GesExternalTypeConstructorReference) throws -> (any GesExternalTypeConstructor)?
}
