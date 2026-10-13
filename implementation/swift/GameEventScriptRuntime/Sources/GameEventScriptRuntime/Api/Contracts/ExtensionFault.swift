// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// An explicit application fault retains its stable code across callback boundaries.
public struct GameEventScriptExtensionFault: Error, Sendable {
    /// Application-provided runtime diagnostic preserved across callback boundaries.
    public let diagnostic: GameEventScriptDiagnostic

    /// Creates an explicit application fault without replacing its code at runtime boundaries.
    ///
    /// - Throws: An API error when the code is empty, whitespace-only or uses the reserved runtime. prefix.
    public init(code: String, message: String, symbol: String? = nil, technicalDetails: String? = nil, programName: String? = nil, handlerName: String? = nil) throws {
        if code.unicodeScalars.allSatisfy({ $0.properties.isWhitespace }) || code.hasPrefix("runtime.") { throw GameEventScriptAPIError.invalidArgument("Invalid extension fault code") }
        diagnostic = .init(phase: .runtime, code: code, message: message, symbol: symbol, programName: programName, handlerName: handlerName, technicalDetails: technicalDetails)
    }
}
