// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Stable pipeline stage in which a diagnostic originated.
public enum GesDiagnosticPhase: Int, Sendable {
    /// Source tokenization or grammar recognition.
    case parse = 1
    /// Source semantic validation.
    case validate = 2
    /// Lowering or bytecode compilation.
    case compile = 3
    /// Portable binary decoding or validation.
    case decode = 4
    /// Resolving a Program against a host.
    case link = 5
    /// Execution of script or native callbacks.
    case runtime = 6
}
