// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime
@_spi(Syntax) import GameEventScriptSyntax

extension ToolHelp {
    static let schema = """
        Usage:
          ges schema check <schema.gess> [more.gess ...] [-q]
          ges schema format <schema.gess> [-o <output.gess>]
          ges schema hash <schema.gess> [more.gess ...] --root <Name> [--param <name>=<literal> ...]
          ges schema canonical <schema.gess> [more.gess ...] --root <Name>
          ges schema dump <schema.gess> [more.gess ...] --root <Name> [--param <name>=<literal> ...] --data <data.bin>

        Commands:
          check      Read schemas and report the first error with its line and column.
          format     Print a schema source in the recommended layout. Comments are dropped.
          hash       Print the schema hash and, with parameters, the binding hash.
          canonical  Print the canonical form that the schema hash runs over.
          dump       List each field of binary data with its bit offset, width and value.

        Options:
          --root <Name>             The schema, message or message set.
          --param <name>=<literal>  A parameter value such as worldWidth=5000m; repeat for each one.
          --data <path>             The binary data of dump.
          -o, --output <path>       Write the formatted source to a UTF-8 file instead of stdout.
          -q, --quiet               Hide the success report of check.
          -h, --help                Show this help.
        """
}

extension Tool {
    func schema(_ arguments: [String]) throws -> Int {
        if arguments.isEmpty || arguments == ["--help"] || arguments == ["-h"] {
            io.line(ToolHelp.schema)
            return arguments.isEmpty ? 2 : 0
        }
        let command = arguments[0]
        guard ["check", "format", "hash", "canonical", "dump"].contains(command) else { throw ToolError.usage("Unknown schema command '\(command)'.") }
        var paths: [String] = []
        var root: String?
        var data: String?
        var output: String?
        var quiet = false
        var parameters: [(String, String)] = []
        var index = 1
        while index < arguments.count {
            let argument = arguments[index]
            if argument == "--help" || argument == "-h" {
                io.line(ToolHelp.schema)
                return 0
            }
            if (argument == "-q" || argument == "--quiet") && command == "check" {
                quiet = true
            } else if ["--root", "--data", "--param", "-o", "--output"].contains(argument) {
                let applies: Bool
                switch argument {
                case "--root": applies = ["hash", "canonical", "dump"].contains(command)
                case "--param": applies = ["hash", "dump"].contains(command)
                case "--data": applies = command == "dump"
                default: applies = command == "format"
                }
                guard applies else { throw ToolError.usage("Unknown schema \(command) option '\(argument)'.") }
                guard index + 1 < arguments.count else { throw ToolError.usage("Specify a value after \(argument).") }
                index += 1
                let next = arguments[index]
                switch argument {
                case "--root": root = next
                case "--data": data = next
                case "-o", "--output": output = next
                default:
                    guard let separator = next.firstIndex(of: "="), separator != next.startIndex else { throw ToolError.usage("A parameter is written name=literal.") }
                    let name = String(next[..<separator])
                    if parameters.contains(where: { $0.0 == name }) { throw ToolError.usage("The parameter '\(name)' is given twice.") }
                    parameters.append((name, String(next[next.index(after: separator)...])))
                }
            } else if argument.hasPrefix("-") {
                throw ToolError.usage("Unknown schema \(command) option '\(argument)'.")
            } else {
                guard !argument.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { throw ToolError.usage("Schema paths must not be empty.") }
                paths.append(argument)
            }
            index += 1
        }
        guard !paths.isEmpty else { throw ToolError.usage("Specify a schema file.") }
        if command == "format" && paths.count > 1 { throw ToolError.usage("Format accepts exactly one schema file.") }
        if ["hash", "canonical", "dump"].contains(command) && root == nil { throw ToolError.usage("\(command) requires --root.") }
        if command == "dump" && data == nil { throw ToolError.usage("dump requires --data.") }
        if let output {
            guard !output.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { throw ToolError.usage("The output path must not be empty.") }
            try ToolFiles.ensureDistinct(output, from: [paths[0]])
        }
        do {
            if command == "format" {
                let syntax = GesSchemaSourceReader.read(try ToolFiles.source(paths[0]))
                if let error = syntax.error {
                    io.line("\(paths[0])(\(error.line),\(error.column)): error \(error.code): \(error.message)", toError: true)
                    return 1
                }
                let printed = GesSchemaFormatter.print(syntax)
                if let output { try ToolFiles.write(Data(printed.utf8), to: try ToolFiles.fullPath(output)) } else { io.write(printed) }
                return 0
            }
            let set = try GesSchemaSet(sources: try paths.map { GesSchemaSource(name: $0, text: try ToolFiles.source($0)) })
            switch command {
            case "check":
                if !quiet { io.line(paths.count == 1 ? "Checked \(paths[0])." : "Checked \(paths.count) schemas.") }
                return 0
            case "canonical":
                io.write(try set.schema(root!).canonicalForm)
                return 0
            default: break
            }
            let schema = try set.schema(root!)
            let values = GesSchemaParameters()
            for (name, literal) in parameters {
                if !schema.parameters.contains(where: { $0.name == name }) {
                    io.line("schema: error schema.unknownName: The root '\(root!)' has no parameter '\(name)'.", toError: true)
                    return 1
                }
                try values.with(name, literal: literal)
            }
            if command == "hash" && parameters.isEmpty {
                io.line(hex(schema.hash))
                return 0
            }
            let bound = try schema.bind(parameters: values)
            if command == "hash" {
                io.line(hex(bound.hash))
                io.line(hex(bound.bindingHash))
                return 0
            }
            let dump = GesSchemaDump.read(bound, data: Array(try Data(contentsOf: URL(fileURLWithPath: data!))))
            io.write(dump.text)
            return dump.isValid ? 0 : 1
        } catch let error as GesSchemaError {
            let place = error.line > 0 ? "\(error.sourceName)(\(error.line),\(error.column))" : "schema"
            io.line("\(place): error \(error.code): \(error.message)", toError: true)
            return 1
        }
    }

    private func hex(_ value: UInt64) -> String {
        let digits = String(value, radix: 16)
        return String(repeating: "0", count: 16 - digits.count) + digits
    }
}
