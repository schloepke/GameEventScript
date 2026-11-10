// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// A runtime failure that ends the current handler, such as a failing external field getter that a host reads
/// directly through `GesValue.readField(_:)`, `GesValue.materializedMap()` or a schema encoding. Catching this
/// protocol covers deliberate `GesExtensionFaultError`s and failures the runtime raises itself; a host's own
/// conforming error keeps its diagnostic at every runtime boundary.
public protocol GesFatalRuntimeError: Error {
    /// The diagnostic with the stable phase and code of the failure.
    var diagnostic: GesDiagnostic { get }
}

struct GesRuntimeError: GesFatalRuntimeError { let diagnostic: GesDiagnostic }
