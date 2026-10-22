// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Failure to bind a validated Program to this host's imports or resource limits.
public struct GesDynamicLinkError: Error, Sendable {
    /// Stable link-phase classification with Program and optional symbol context.
    public let diagnostic: GesDiagnostic

    init(code: String, program: String, symbol: String? = nil) { diagnostic = .init(phase: .link, code: code, symbol: symbol, programName: program) }
}
