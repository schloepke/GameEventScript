// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
@_spi(Syntax) import GameEventScriptSyntax

/// Compiler output configuration; no host state or file-system paths are required.
public struct GesCompileOptions: Sendable {
    /// Optional debug sections to emit; all are enabled by default.
    public var debugInfo: GesDebugInfoOptions
    /// Application-controlled Program version, independent of the binary format version.
    public var programVersion: UInt64

    /// Creates compilation options with all debug sections and program version zero unless overridden.
    public init(debugInfo: GesDebugInfoOptions = .all, programVersion: UInt64 = 0) {
        self.debugInfo = debugInfo
        self.programVersion = programVersion
    }
}

/// Structured source diagnostics from parsing, validation, or lowering.
public struct GesCompileError: Error, Sendable {
    /// Structured parse, validation or compilation diagnostics.
    public let diagnostics: [GesDiagnostic]

    /// Creates a compilation failure carrying the supplied diagnostics.
    public init(diagnostics: [GesDiagnostic]) { self.diagnostics = diagnostics }
}

/// Compiles one or more source texts into a reusable immutable Program.
public final class GesProgramBuilder {
    var sources: [GesSource] = []
    var options = GesCompileOptions()
    var catalog: (any GesExternalTypeCatalog)?

    /// Creates an empty source builder with default compile options. `GameEventScriptProgram.makeBuilder()` is
    /// equivalent.
    public init() {}

    /// Adds source text in input order, strips an initial BOM and uses UnknownSource when no name is supplied. Returns
    /// this builder; performs no file I/O.
    @discardableResult public func addScript(_ text: String, sourceName: String? = nil) -> Self {
        let prepared = text.unicodeScalars.first?.value == 0xfeff ? String(text.unicodeScalars.dropFirst()) : text
        sources.append(GesSource(text: prepared, name: sourceName ?? "UnknownSource", id: UInt32(sources.count)))
        return self
    }

    /// Selects debug sections for subsequent compilation. Returns this builder.
    @discardableResult public func withDebugInfo(_ options: GesDebugInfoOptions = .all) -> Self {
        self.options.debugInfo = options
        return self
    }

    /// Sets the application Program version. Returns this builder.
    @discardableResult public func withProgramVersion(_ version: UInt64) -> Self {
        options.programVersion = version
        return self
    }

    /// Sets declarative external types for semantic validation. Returns this builder; executable bindings belong to the
    /// host.
    @discardableResult public func withExternalTypeCatalog(_ catalog: any GesExternalTypeCatalog) -> Self {
        self.catalog = catalog
        return self
    }

    /// Compiles all accumulated sources into one immutable, validated Program. Explicit options override builder
    /// options for this call.
    ///
    /// - Throws: `GesCompileError` for source diagnostics, or an API error for unsupported debug option
    /// bits.
    public func compile(options: GesCompileOptions? = nil) throws -> GameEventScriptProgram {
        let configuration = options ?? self.options
        guard configuration.debugInfo.rawValue & ~GesDebugInfoOptions.all.rawValue == 0 else { throw GesApiError.invalidArgument("Unknown debug information option") }
        let modules = try sources.map { try GesSyntaxBinder.parse($0) }
        let validator = GesAstValidator(modules: modules, catalog: catalog)
        try validator.validate()
        return try GesCompiler(validator, sources: sources, options: configuration).compile()
    }
}

extension GameEventScriptProgram {
    /// Creates an empty source builder with default compile options; equivalent to `GesProgramBuilder()`. Available
    /// when the Compiler module is imported.
    public static func makeBuilder() -> GesProgramBuilder { .init() }
}

extension GesProgramBuilder {
    /// The lossless syntax tree of one source, also when the source has syntax errors.
    @_spi(Syntax) public static func syntaxTree(_ text: String) -> GesSyntaxTree {
        GesSyntaxParser.parse(text)
    }
}
