// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Host-side resolver for executable extension imports.
public protocol GesExtensionRegistry {
    /// Resolves the exact extension reference, or returns nil when unavailable. Resolver errors propagate through
    /// linking.
    func resolve(_ reference: GesExtensionReference) throws -> (any GesExtensionFunction)?
}
