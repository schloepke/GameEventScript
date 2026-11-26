// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import GameEventScriptRuntime

// Discovery and atomic replacement belong to the executable, never the portable libraries.
enum ToolFiles {
    static func fullPath(_ path: String) throws -> String {
        guard !path.contains("\0") else { throw ToolError.usage("Paths must not contain NUL.") }
        return URL(fileURLWithPath: path).standardizedFileURL.path
    }

    static func expand(_ arguments: [String]) throws -> [String] {
        var paths: [String] = []
        var seen = Set<[UInt8]>()

        func add(_ path: String) throws { if seen.insert(Array(try fullPath(path).utf8)).inserted { paths.append(path) } }

        for argument in arguments {
            _ = try fullPath(argument)
            if !argument.contains(where: { $0 == "*" || $0 == "?" }) || FileManager.default.fileExists(atPath: argument) {
                try add(argument)
                continue
            }
            let directory = (argument as NSString).deletingLastPathComponent
            let pattern = (argument as NSString).lastPathComponent
            guard !directory.contains(where: { $0 == "*" || $0 == "?" }), !pattern.contains("**") else { throw ToolError.usage("Wildcards are supported only in file names; recursive patterns are not supported.") }
            let expression = "\\A" + pattern.map { character in character == "*" ? ".*" : character == "?" ? "." : NSRegularExpression.escapedPattern(for: String(character)) }.joined() + "\\z"
            let regex = try NSRegularExpression(pattern: expression, options: [.dotMatchesLineSeparators])
            let matches = try FileManager.default.contentsOfDirectory(atPath: directory.isEmpty ? "." : directory).filter { name in
                guard regex.firstMatch(in: name, range: NSRange(name.startIndex..., in: name)) != nil else { return false }
                var isDirectory: ObjCBool = false
                let path = directory.isEmpty ? name : (directory as NSString).appendingPathComponent(name)
                return FileManager.default.fileExists(atPath: path, isDirectory: &isDirectory) && !isDirectory.boolValue
            }.sorted { $0.utf16.lexicographicallyPrecedes($1.utf16) }
            guard !matches.isEmpty else { throw ToolError.io("No source files match '\(argument)'.") }
            for name in matches { try add(directory.isEmpty ? name : (directory as NSString).appendingPathComponent(name)) }
        }
        return paths
    }

    static func source(_ path: String) throws -> String {
        let data = try Data(contentsOf: URL(fileURLWithPath: path))
        guard let text = String(data: data, encoding: .utf8) else { throw ToolError.encoding(path) }
        return text
    }

    // The schema set of `--schema`: every handle type that the given schema sources declare, read as one schema set.
    // The CLI registers no resolver, so only programs that do not read handle state can load. Nil without `--schema`.
    static func handleTypes(_ arguments: [String]) throws -> [GesHandleType]? {
        var activePath = ""
        return try handleTypes(arguments, activePath: &activePath)
    }

    static func handleTypes(_ arguments: [String], activePath: inout String) throws -> [GesHandleType]? {
        guard let sources = try schemaSources(arguments, activePath: &activePath) else { return nil }
        return try GesSchemaSet(sources: sources).handleTypes
    }

    // The UTF-8 sources of `--schema`, each named by its path as given or expanded; nil without the option.
    static func schemaSources(_ arguments: [String], activePath: inout String) throws -> [GesSchemaSource]? {
        if arguments.isEmpty { return nil }
        var sources: [GesSchemaSource] = []
        for path in try expand(arguments) {
            activePath = path
            sources.append(GesSchemaSource(name: path, text: try source(path)))
        }
        return sources
    }

    static func builder(_ handles: [GesHandleType]?) -> GesProgramBuilder {
        let builder = GesProgramBuilder()
        if let handles, !handles.isEmpty { builder.withHandleTypes(handles) }
        return builder
    }

    static func compile(_ paths: [String], handles: [GesHandleType]? = nil, debug: Bool = true) throws -> GameEventScriptProgram {
        let builder = builder(handles).withDebugInfo(debug ? .all : .none)
        for path in paths { builder.addScript(try source(path), sourceName: path) }
        return try builder.compile()
    }

    static func isBinary(_ path: String) -> Bool { (path as NSString).pathExtension.lowercased() == "gesb" }

    static func read(_ path: String, handles: [GesHandleType]? = nil) throws -> GameEventScriptProgram {
        if isBinary(path) { return try readBinary(path, handles: handles) }
        return try compile([path], handles: handles)
    }

    // A binary Program carries the handle types it was compiled with; they must match the schemas of `--schema`.
    static func readBinary(_ path: String, handles: [GesHandleType]?) throws -> GameEventScriptProgram {
        let program = try GameEventScriptProgram.read(Array(Data(contentsOf: URL(fileURLWithPath: path))))
        try checkImports(program, handles: handles)
        return program
    }

    // Checks every handle type a binary Program imports against the schemas of `--schema`. A type the schemas declare
    // links on a throwaway host whose resolvers are never called, so the comparison is the runtime's own; a type they
    // do not declare fails with the same code. Without `--schema` nothing is checked.
    static func checkImports(_ program: GameEventScriptProgram, handles: [GesHandleType]?) throws {
        guard let handles else { return }
        var builder: GesHostBuilder?
        for entry in program.bindings where entry.kind == .handleType {
            let name = program.stringConstants.resolve(entry.name)
            guard let type = handles.first(where: { $0.name == name }) else {
                throw ToolError.link(
                    GesDiagnostic(
                        phase: .link,
                        code: GesDiagnosticCodes.linkMismatchedHandleType,
                        message: "The schemas declare no handle type @\(name), which the Program was compiled with.",
                        symbol: name,
                        programName: program.moduleName
                    )
                )
            }
            builder = (builder ?? GameEventScriptHost.makeBuilder()).withHandleResolver(UnusedResolver(), for: type)
        }
        if let builder { _ = try builder.build().load(program) }
    }

    private struct UnusedResolver: GesHandleResolver {
        func resolve(_ call: GesHandleResolveCall) throws { throw ToolError.io("The import check reads no state.") }
    }

    static func ensureDistinct(_ output: String, from inputs: [String]) throws {
        let destination = try fullPath(output)
        for input in inputs {
            let source = try fullPath(input)
            #if os(macOS)
                let same = source.lowercased() == destination.lowercased()
            #else
                let same = source == destination
            #endif
            if same { throw ToolError.usage("Input and output must be different files.") }
        }
    }

    static func write(_ data: Data, to path: String) throws {
        let destination = URL(fileURLWithPath: try fullPath(path))
        try FileManager.default.createDirectory(at: destination.deletingLastPathComponent(), withIntermediateDirectories: true)
        try data.write(to: destination, options: .atomic)
    }
}
