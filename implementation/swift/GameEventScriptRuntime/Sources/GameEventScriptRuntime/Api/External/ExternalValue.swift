// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// An embedding-provided value. Field access is synchronous and may report an explicit application fault.
public protocol GesExternalValue: AnyObject {
    /// Immutable field and constructor declaration associated with this value.
    var definition: GesExternalTypeDefinition { get }

    /// Reads a named field synchronously, returning nil for absence. Thrown application faults retain their
    /// classification at the runtime boundary.
    func field(_ name: String) throws -> GesValue?
}
