// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime
@_spi(Syntax) import GameEventScriptSyntax

// Reads GES documents for the document commands and :show, with the Syntax module's explanation of an error.
enum ToolDocuments {
    struct Failure {
        let line: Int
        let column: Int
        let code: String
        let message: String
    }

    // The document, or nil with the error of reading it; other errors propagate.
    static func read(_ text: String) throws -> (GesDocument?, Failure?) {
        do {
            return (try GesDocument(parsing: text), nil)
        } catch let error as GesDocumentError {
            let message = GesDocumentDiagnostic.explain(error.code, limitName: error.limitName, limitValue: error.limit ?? 0)
            return (nil, Failure(line: error.line, column: error.column, code: error.code, message: message))
        }
    }

    static func format(_ path: String, _ failure: Failure) -> String { "\(path)(\(failure.line),\(failure.column)): error \(failure.code): \(failure.message)" }
}

extension Tool {
    func document(_ arguments: [String]) throws -> Int {
        if arguments.isEmpty || arguments == ["--help"] || arguments == ["-h"] {
            io.line(ToolHelp.document)
            return arguments.isEmpty ? 2 : 0
        }
        guard arguments[0] == "check" || arguments[0] == "format" else { throw ToolError.usage("Unknown document command '\(arguments[0])'.") }
        let check = arguments[0] == "check"
        var paths: [String] = []
        var schemaArguments: [String] = []
        var catalogArguments: [(type: String, path: String)] = []
        var output: String?
        var quiet = false
        var color = false
        var options = true
        var index = 1
        while index < arguments.count {
            let argument = arguments[index]
            if options && argument == "--" {
                options = false
            } else if options && (argument == "--help" || argument == "-h") {
                io.line(ToolHelp.document)
                return 0
            } else if options && !check && ["-o", "--output"].contains(argument) {
                guard output == nil, index + 1 < arguments.count, !arguments[index + 1].hasPrefix("-") else { throw ToolError.usage("Specify one output path after -o or --output. Prefix a path starting with '-' with './'.") }
                index += 1
                output = arguments[index]
            } else if options && check && ["-q", "--quiet"].contains(argument) {
                quiet = true
            } else if options && check && argument == "--schema" {
                guard index + 1 < arguments.count, !arguments[index + 1].hasPrefix("-"), !arguments[index + 1].trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
                    throw ToolError.usage("Specify a schema path after --schema; prefix a path starting with '-' with './'.")
                }
                index += 1
                schemaArguments.append(arguments[index])
            } else if options && check && argument == ToolCatalogs.option {
                guard index + 2 < arguments.count, arguments[index + 1].hasPrefix("@"), arguments[index + 1].count > 1, !arguments[index + 2].hasPrefix("-"),
                    !arguments[index + 2].trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
                else { throw ToolError.usage("Specify a handle type and a root file after --catalog, for example '--catalog @Unit units.gesd'.") }
                catalogArguments.append((String(arguments[index + 1].dropFirst()), arguments[index + 2]))
                index += 2
            } else if options && !check && argument == "--color" {
                color = !io.noColor
            } else if options && argument.hasPrefix("-") {
                throw ToolError.usage("Unknown document \(arguments[0]) option '\(argument)'.")
            } else {
                guard !argument.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { throw ToolError.usage("Document paths must not be empty.") }
                paths.append(argument)
            }
            index += 1
        }
        guard !paths.isEmpty || !catalogArguments.isEmpty else { throw ToolError.usage("Specify a document file.") }
        guard catalogArguments.isEmpty || !schemaArguments.isEmpty else { throw ToolError.usage("--catalog needs the schemas of --schema.") }
        guard check || paths.count == 1 else { throw ToolError.usage("Format accepts exactly one document file.") }
        if let path = output {
            guard !path.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { throw ToolError.usage("The output path must not be empty.") }
            output = try ToolFiles.fullPath(path)
            do { try ToolFiles.ensureDistinct(path, from: paths) } catch ToolError.usage { throw ToolError.usage("Input and output must be different files.") }
        }
        if check {
            let schemas = try ToolDocumentSchemas.load(schemaArguments)
            var failed = 0
            if let schemas, !catalogArguments.isEmpty { failed = try ToolCatalogs.check(catalogArguments, schemas, quiet: quiet, io) }
            // An unreadable file fails like an invalid one; the remaining files are still checked.
            for path in paths {
                let text: String
                do {
                    text = try ToolFiles.source(path)
                } catch {
                    io.report(error)
                    failed += 1
                    continue
                }
                guard let document = try readDocument(path, text) else {
                    failed += 1
                    continue
                }
                if let schemas, !schemas.check(path, text, document, io) { failed += 1 }
            }
            if !quiet && failed == 0 && !paths.isEmpty { io.line(paths.count == 1 ? "Checked \(paths[0])." : "Checked \(paths.count) documents.") }
            return failed == 0 ? 0 : 1
        }
        guard let document = try readDocument(paths[0], ToolFiles.source(paths[0])) else { return 1 }
        var text = document.text()
        if let output {
            try ToolFiles.write(Data(text.utf8), to: output)
            io.line("Formatted \(paths[0]) -> \(output).")
            return 0
        }
        if io.outputTerminal { text = ToolTextDisplay.expandTabs(text) }
        io.write(color && io.outputTerminal ? RunHighlighting().renderDocument(text) : text)
        return 0
    }

    // Reads one document and reports its error as `path(line,column): error code: message`.
    private func readDocument(_ path: String, _ text: String) throws -> GesDocument? {
        let (document, failure) = try ToolDocuments.read(text)
        if let failure { io.line(ToolDocuments.format(path, failure), toError: true) }
        return document
    }
}

// The schemas of `document check --schema`: the given sources read as one schema set, against which each document with
// a header is checked. `:Schema(:Name)` names a schema of the set. `:Schema('file.gess')` names a file relative to the
// document, which must be one of the given sources; the set then names that source by the header's text, as the runtime
// matches a file header against source names, and every other source by its full path, so no two sources collide.
final class ToolDocumentSchemas {
    private struct FileKey: Hashable {
        let source: Int
        let name: String
    }

    private let sources: [GesSchemaSource]
    private let fullPaths: [String]
    private let set: GesSchemaSet
    private var fileSets: [FileKey: GesSchemaSet] = [:]

    private init(_ sources: [GesSchemaSource]) throws {
        self.sources = sources
        fullPaths = try sources.map { ToolDocumentSchemas.comparable(try ToolFiles.fullPath($0.name)) }
        set = try GesSchemaSet(sources: sources)
    }

    // The schema set of `--schema`, or nil without the option; a schema error throws as for compile.
    static func load(_ arguments: [String]) throws -> ToolDocumentSchemas? {
        var activePath = ""
        guard let sources = try ToolFiles.schemaSources(arguments, activePath: &activePath) else { return nil }
        return try ToolDocumentSchemas(sources)
    }

    // The handle type of the set with this name, or nil.
    func handleType(_ name: String) -> GesHandleType? { set.handleTypes.first { $0.name.utf8.elementsEqual(name.utf8) } }

    // Checks a readable document against the schema of its header and reports the first error; a document without a
    // header has nothing to check against.
    func check(_ path: String, _ text: String, _ document: GesDocument, _ io: ToolIO) -> Bool {
        guard let schema = document.schema else { return true }
        do {
            switch schema.kind {
            case .type:
                try set.checkDocument(text)
            case .file:
                guard let set = try fileSet(path, schema.name) else {
                    io.line("\(path): error schema.unknownName: The schema file '\(schema.name)' is none of the files given with --schema.", toError: true)
                    return false
                }
                try set.checkDocument(text)
            }
            return true
        } catch let error as GesSchemaError {
            let place = error.line > 0 && error.sourceName.isEmpty ? "\(path)(\(error.line),\(error.column))" : error.line > 0 ? "\(error.sourceName)(\(error.line),\(error.column))" : path
            io.line("\(place): error \(error.code): \(error.message)", toError: true)
            return false
        } catch {
            io.report(error)
            return false
        }
    }

    // The set for a file header, or nil when the file is none of the given sources.
    private func fileSet(_ path: String, _ name: String) throws -> GesSchemaSet? {
        let directory = URL(fileURLWithPath: try ToolFiles.fullPath(path)).deletingLastPathComponent()
        let fullPath = ToolDocumentSchemas.comparable(URL(fileURLWithPath: name, relativeTo: directory).standardizedFileURL.path)
        guard let index = fullPaths.firstIndex(of: fullPath) else { return nil }
        let key = FileKey(source: index, name: name)
        if let set = fileSets[key] { return set }
        var renamed: [GesSchemaSource] = []
        for (other, source) in sources.enumerated() {
            renamed.append(GesSchemaSource(name: other == index ? name : try ToolFiles.fullPath(source.name), text: source.text))
        }
        let set = try GesSchemaSet(sources: renamed)
        fileSets[key] = set
        return set
    }

    // Paths compare as the file system does: case-insensitively on macOS.
    private static func comparable(_ path: String) -> String {
        #if os(macOS)
            return path.lowercased()
        #else
            return path
        #endif
    }
}
