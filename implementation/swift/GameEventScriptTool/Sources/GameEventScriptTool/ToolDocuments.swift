// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime
import GameEventScriptSyntax

// Reads GES documents for the document commands and :show, with the highlighter's explanation of a reading error.
enum ToolDocuments {
    struct Failure {
        let line: Int
        let column: Int
        let code: String
        let message: String
    }

    // The document, or nil with the error of reading it.
    static func read(_ text: String) -> (GesDocument?, Failure?) {
        do {
            return (try GesDocument(parsing: text), nil)
        } catch let error as GesDocumentError {
            let reading = (try? GesSyntaxHighlighter(language: .gesd, maxInputLength: Int.max).highlight(text).diagnostics) ?? []
            let message: String
            if let first = reading.first, first.code == error.code {
                message = first.message
            } else if let limit = error.limitName {
                message = "The document exceeds \(limit) (\(error.limit ?? 0))."
            } else {
                message = "This explicit form does not construct a value."
            }
            return (nil, Failure(line: error.line, column: error.column, code: error.code, message: message))
        } catch {
            return (nil, Failure(line: 1, column: 1, code: "document.invalidLiteral", message: "\(error)"))
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
            } else if options && !check && argument == "--color" {
                color = !io.noColor
            } else if options && argument.hasPrefix("-") {
                throw ToolError.usage("Unknown document \(arguments[0]) option '\(argument)'.")
            } else {
                paths.append(argument)
            }
            index += 1
        }
        guard !paths.isEmpty else { throw ToolError.usage("Specify a document file.") }
        guard check || paths.count == 1 else { throw ToolError.usage("Format accepts exactly one document file.") }
        if let path = output {
            guard !path.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { throw ToolError.usage("The output path must not be empty.") }
            output = try ToolFiles.fullPath(path)
            do { try ToolFiles.ensureDistinct(path, from: paths) } catch ToolError.usage { throw ToolError.usage("Input and output must be different files.") }
        }
        if check {
            var failed = 0
            for path in paths where readDocument(path, try ToolFiles.source(path)) == nil { failed += 1 }
            if !quiet && failed == 0 { io.line(paths.count == 1 ? "Checked \(paths[0])." : "Checked \(paths.count) documents.") }
            return failed == 0 ? 0 : 1
        }
        guard let document = readDocument(paths[0], try ToolFiles.source(paths[0])) else { return 1 }
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
    private func readDocument(_ path: String, _ text: String) -> GesDocument? {
        let (document, failure) = ToolDocuments.read(text)
        if let failure { io.line(ToolDocuments.format(path, failure), toError: true) }
        return document
    }
}
