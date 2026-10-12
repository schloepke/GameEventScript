// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Stable phase/code and structured context for a source, binary, linking or runtime failure.
public struct GameEventScriptDiagnostic: Sendable {
    /// Pipeline stage that owns this diagnostic.
    public let phase: GameEventScriptDiagnosticPhase
    /// Stable machine-readable classification; use with phase rather than matching English text.
    public let code: String
    /// Human-readable explanation, not a stable comparison key.
    public let message: String
    /// Affected symbol name when available.
    public let symbol: String?
    /// Role of the affected symbol.
    public let symbolKind: GameEventScriptSymbolKind
    /// Optional original source span.
    public let sourceLocation: GameEventScriptSourceLocation?
    /// Affected Program name; runtime boundaries may fill missing context.
    public var programName: String?
    /// Affected handler signature; runtime boundaries may fill missing context.
    public var handlerName: String?
    /// Optional implementation-specific details for troubleshooting.
    public let technicalDetails: String?

    /// Creates a diagnostic from a stable phase/code pair and optional structured context.
    public init(
        phase: GameEventScriptDiagnosticPhase,
        code: String,
        message: String = "",
        symbol: String? = nil,
        symbolKind: GameEventScriptSymbolKind = .unknown,
        sourceLocation: GameEventScriptSourceLocation? = nil,
        programName: String? = nil,
        handlerName: String? = nil,
        technicalDetails: String? = nil
    ) {
        self.phase = phase
        self.code = code
        self.message = message
        self.symbol = symbol
        self.symbolKind = symbolKind
        self.sourceLocation = sourceLocation
        self.programName = programName
        self.handlerName = handlerName
        self.technicalDetails = technicalDetails
    }
}
