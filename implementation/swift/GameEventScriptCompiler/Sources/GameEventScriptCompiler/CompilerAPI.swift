// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// Independently selectable portable debug sections. All sections are enabled by default.
public struct GameEventScriptDebugInfoOptions: OptionSet, Sendable {
    public let rawValue: UInt8
    public init(rawValue: UInt8) { self.rawValue = rawValue }
    public static let symbols = Self(rawValue: 1)
    public static let sourceMap = Self(rawValue: 2)
    public static let sourceArchive = Self(rawValue: 4)
    public static let all: Self = [.symbols, .sourceMap, .sourceArchive]
    public static let none: Self = []
}

/// Compiler output configuration; no host state or file-system paths are required.
public struct GameEventScriptCompileOptions: Sendable {
    public var debugInfo: GameEventScriptDebugInfoOptions
    public var programVersion: UInt64
    public init(debugInfo: GameEventScriptDebugInfoOptions = .all, programVersion: UInt64 = 0) {
        self.debugInfo = debugInfo
        self.programVersion = programVersion
    }
}

/// Structured source diagnostics from parsing, validation, or lowering.
public struct GameEventScriptCompileError: Error, Sendable {
    public let diagnostics: [GameEventScriptDiagnostic]
    public init(diagnostics: [GameEventScriptDiagnostic]) { self.diagnostics = diagnostics }
}

/// Compiles one or more source texts into a reusable immutable Program.
public final class GameEventScriptBuilder {
    var sources: [GesSource] = []
    var options = GameEventScriptCompileOptions()
    var catalog: (any GameEventScriptExternalTypeCatalogProtocol)?
    public init() {}
    public static func create() -> GameEventScriptBuilder { .init() }
    @discardableResult
    public func addScript(_ text: String, sourceName: String? = nil) -> Self {
        let prepared = text.unicodeScalars.first?.value == 0xfeff ? String(text.unicodeScalars.dropFirst()) : text
        sources.append(GesSource(text: prepared, name: sourceName ?? "UnknownSource", id: UInt32(sources.count)))
        return self
    }
    @discardableResult
    public func withDebugInfo(_ options: GameEventScriptDebugInfoOptions = .all) -> Self {
        self.options.debugInfo = options
        return self
    }
    @discardableResult
    public func withProgramVersion(_ version: UInt64) -> Self {
        options.programVersion = version
        return self
    }
    @discardableResult
    public func withExternalTypeCatalog(_ catalog: any GameEventScriptExternalTypeCatalogProtocol) -> Self {
        self.catalog = catalog
        return self
    }
    public func compile(options: GameEventScriptCompileOptions? = nil) throws -> GameEventScriptProgram {
        let configuration = options ?? self.options
        guard configuration.debugInfo.rawValue & ~GameEventScriptDebugInfoOptions.all.rawValue == 0 else {
            throw GameEventScriptAPIError.invalidArgument("Unknown debug information option")
        }
        let modules = try sources.map { try GesParser(source: $0).parse() }
        return try GesCompiler(modules: modules, sources: sources, catalog: catalog, options: configuration).compile()
    }
}
