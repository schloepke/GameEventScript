// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// Independently selectable portable debug sections. All sections are enabled by default.
public struct GameEventScriptDebugInfoOptions: OptionSet, Sendable {
    /// Bit mask selecting portable debug sections.
    public let rawValue: UInt8
    /// Creates an option mask; unsupported bits are rejected when compilation is requested.
    public init(rawValue: UInt8) { self.rawValue = rawValue }
    /// Retains register names and instruction lifetimes.
    public static let symbols = Self(rawValue: 1)
    /// Retains bytecode-to-source mappings.
    public static let sourceMap = Self(rawValue: 2)
    /// Embeds original source documents.
    public static let sourceArchive = Self(rawValue: 4)
    /// Enables all three portable debug sections.
    public static let all: Self = [.symbols, .sourceMap, .sourceArchive]
    /// Omits optional debug sections for size-sensitive Programs.
    public static let none: Self = []
}

/// Compiler output configuration; no host state or file-system paths are required.
public struct GameEventScriptCompileOptions: Sendable {
    /// Optional debug sections to emit; all are enabled by default.
    public var debugInfo: GameEventScriptDebugInfoOptions
    /// Application-controlled Program version, independent of the binary format version.
    public var programVersion: UInt64
    /// Creates compilation options with all debug sections and program version zero unless overridden.
    public init(debugInfo: GameEventScriptDebugInfoOptions = .all, programVersion: UInt64 = 0) {
        self.debugInfo = debugInfo
        self.programVersion = programVersion
    }
}

/// Structured source diagnostics from parsing, validation, or lowering.
public struct GameEventScriptCompileError: Error, Sendable {
    /// Structured parse, validation or compilation diagnostics.
    public let diagnostics: [GameEventScriptDiagnostic]
    /// Creates a compilation failure carrying the supplied diagnostics.
    public init(diagnostics: [GameEventScriptDiagnostic]) { self.diagnostics = diagnostics }
}

/// Compiles one or more source texts into a reusable immutable Program.
public final class GameEventScriptBuilder {
    var sources: [GesSource] = []
    var options = GameEventScriptCompileOptions()
    var catalog: (any GameEventScriptExternalTypeCatalogProtocol)?
    /// Creates an empty source builder with default compile options.
    public init() {}
    /// Creates an empty builder matching the portable compiler workflow.
    public static func create() -> GameEventScriptBuilder { .init() }
    /// Adds source text in input order, strips an initial BOM and uses UnknownSource when no name is supplied. Returns
    /// this builder; performs no file I/O.
    @discardableResult
    public func addScript(_ text: String, sourceName: String? = nil) -> Self {
        let prepared = text.unicodeScalars.first?.value == 0xfeff ? String(text.unicodeScalars.dropFirst()) : text
        sources.append(GesSource(text: prepared, name: sourceName ?? "UnknownSource", id: UInt32(sources.count)))
        return self
    }
    /// Selects debug sections for subsequent compilation. Returns this builder.
    @discardableResult
    public func withDebugInfo(_ options: GameEventScriptDebugInfoOptions = .all) -> Self {
        self.options.debugInfo = options
        return self
    }
    /// Sets the application Program version. Returns this builder.
    @discardableResult
    public func withProgramVersion(_ version: UInt64) -> Self {
        options.programVersion = version
        return self
    }
    /// Sets declarative external types for semantic validation. Returns this builder; executable bindings belong to the
    /// host.
    @discardableResult
    public func withExternalTypeCatalog(_ catalog: any GameEventScriptExternalTypeCatalogProtocol) -> Self {
        self.catalog = catalog
        return self
    }
    /// Compiles all accumulated sources into one immutable, validated Program. Explicit options override builder
    /// options for this call.
    ///
    /// - Throws: `GameEventScriptCompileError` for source diagnostics, or an API error for unsupported debug option
    /// bits.
    public func compile(options: GameEventScriptCompileOptions? = nil) throws -> GameEventScriptProgram {
        let configuration = options ?? self.options
        guard configuration.debugInfo.rawValue & ~GameEventScriptDebugInfoOptions.all.rawValue == 0 else {
            throw GameEventScriptAPIError.invalidArgument("Unknown debug information option")
        }
        let modules = try sources.map { try GesParser(source: $0).parse() }
        return try GesCompiler(modules: modules, sources: sources, catalog: catalog, options: configuration).compile()
    }
}
