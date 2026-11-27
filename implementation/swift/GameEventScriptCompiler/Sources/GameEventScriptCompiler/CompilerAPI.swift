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
    var handleTypes: [String: GesHandleType] = [:]
    // The handle type names in first declaration order, as C#'s Dictionary enumerates them; a replacement keeps its place.
    var handleTypeOrder: [String] = []
    var enumerableHandleTypes: Set<String>?

    /// The warnings of the last `compile(options:)` call, in report order; empty before the first call. Warnings do not
    /// fail a compilation, and a failed compilation reports them as of its validation.
    public private(set) var warnings: [GesDiagnostic] = []

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

    /// Declares the handle types the sources may name as `@Name`, usually from `GesSchemaSet.handleTypes`. A later
    /// declaration of the same name replaces an earlier one; composite roots are known through `GesHandleType.root`
    /// and need not be declared separately. Store file types (`GesHandleType.storeOf`) are passed over, since scripts
    /// cannot name them. Returns this builder.
    @discardableResult public func withHandleTypes(_ types: some Sequence<GesHandleType>) -> Self {
        // A store file type is no script type; only reading a store uses it.
        for type in types where type.storeOf == nil {
            var current: GesHandleType? = type
            while let handle = current {
                if handleTypes[handle.name] == nil { handleTypeOrder.append(handle.name) }
                if handle === type || handleTypes[handle.name] == nil { handleTypes[handle.name] = handle }
                current = handle.root
            }
        }
        return self
    }

    /// Tells the compiler which handle types the host enumerates (`GesHandleResolver.handles(root:)`), so that a store
    /// read of any other declared type, which reads as nothing, reports a `validate.handleTypeNotEnumerable` warning in
    /// `warnings`. Without this call the compiler does not know and warns about none; each call replaces the previous
    /// set. Names are written without `@`. Returns this builder.
    @discardableResult public func withEnumerableHandleTypes(_ names: some Sequence<String>) -> Self {
        enumerableHandleTypes = Set(names)
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
        warnings = []
        // Each source's own rules report first, in source order; the program-wide rules follow.
        var sourceDiagnostics: [GesDiagnostic] = []
        let modules = try sources.map { try GesSyntaxBinder.parse($0, validation: &sourceDiagnostics) }
        let validator = GesAstValidator(modules: modules, catalog: catalog, handleTypes: handleTypes, handleTypeOrder: handleTypeOrder, enumerableHandleTypes: enumerableHandleTypes, diagnostics: sourceDiagnostics)
        defer { warnings = validator.warnings }
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
