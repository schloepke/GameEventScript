// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Failure to bind a validated Program to this host's imports or resource limits.
public struct GesDynamicLinkError: Error, Sendable {
    /// The first diagnostic of `diagnostics`: stable link-phase classification with Program and optional symbol context.
    public var diagnostic: GesDiagnostic { diagnostics[0] }

    /// Every diagnostic of the failure, never empty. A failure of an import that the source calls, such as a missing
    /// extension, has one diagnostic per call site with its source location, in source order, when the Program has a
    /// SourceMap.
    public let diagnostics: [GesDiagnostic]

    init(code: String, program: String, message: String) { diagnostics = [.init(phase: .link, code: code, message: message, programName: program)] }

    /// A failure of an import that the source calls: one diagnostic per call site the SourceMap locates, or one without
    /// a location.
    init(code: String, program: GameEventScriptProgram, message: String, symbol: String, calls: (opcode: GesBytecodeOpCode, bindID: UInt16)) {
        let locations = GesSourceCallSites.locate(program, calls.opcode, calls.bindID)
        let diagnostic = { (location: GesSourceLocation?) in
            GesDiagnostic(phase: .link, code: code, message: message, symbol: symbol, sourceLocation: location, programName: program.moduleName)
        }
        diagnostics = locations.isEmpty ? [diagnostic(nil)] : locations.map(diagnostic)
    }
}
