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

    static func compile(_ paths: [String], debug: Bool = true) throws -> GameEventScriptProgram {
        let builder = GameEventScriptBuilder().withDebugInfo(debug ? .all : .none)
        for path in paths { builder.addScript(try source(path), sourceName: path) }
        return try builder.compile()
    }

    static func isBinary(_ path: String) -> Bool { (path as NSString).pathExtension.lowercased() == "gesb" }

    static func read(_ path: String) throws -> GameEventScriptProgram {
        if isBinary(path) { return try GameEventScriptProgramReader.read(Array(Data(contentsOf: URL(fileURLWithPath: path)))) }
        return try compile([path])
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
