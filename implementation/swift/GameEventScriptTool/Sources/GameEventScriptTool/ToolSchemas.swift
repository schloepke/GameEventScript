// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptGenerator
import GameEventScriptRuntime
@_spi(Syntax) import GameEventScriptSyntax

extension ToolHelp {
    static let schema = """
        Usage:
          ges schema check <schema.gess> [more.gess ...] [-q]
          ges schema format <schema.gess> [-o <output.gess>]
          ges schema hash <schema.gess> [more.gess ...] --root <Name> [--param <name>=<literal> ...]
          ges schema canonical <schema.gess> [more.gess ...] --root <Name>
          ges schema dump <schema.gess> [more.gess ...] --root <Name> [--param <name>=<literal> ...] --data <data.bin> [--baseline <record.bin>]
          ges schema layout <schema.gess> [more.gess ...] (--root <Name> ... | --all-roots | --wire <group> | --model <group>)
                            [--param <name>=<literal> ...] [-o <layout.json>]
          ges schema generate <schema.gess> [more.gess ...] (--root <Name> ... | --all-roots | --wire <group> | --model <group>)
                              [--param <name>=<literal> ...]
                              --language csharp|swift [--namespace <A.B>] [--access public|internal] [--values]
                              [--checked-exclusivity] [--bindings] [--documents] [--models <namespace>] [--extend-namespace]
                              [-o <file> | -d <dir> [--split schema|file]] [--check]

        Commands:
          check      Read schemas and report the first error with its line and column.
          format     Print a schema source in the recommended layout. Comments are dropped.
          hash       Print the schema hash and, with parameters, the binding hash.
          canonical  Print the canonical form that the schema hash runs over.
          dump       List each field of binary data with its bit offset, width and value.
          layout     Print the language-neutral generation layout of the roots and every type they reach as JSON.
          generate   Generate C# or Swift code that writes and reads the records of the roots and every type they reach.

        Options:
          --root <Name>             The schema, message or message set; layout and generate accept several.
          --all-roots               Make every declaration of the files a root of layout or generate.
          --wire <group>            Make the wire roots of the group the roots, in declaration order; wire alone is the group default.
          --model <group>           Generate plain model types of the model roots of the group instead of wire code.
          --language <name>         The language that generate writes: csharp or swift.
          --namespace <A.B>         The namespace of the generated types; Swift declares it as nested enums.
          --access <level>          The visibility of the generated types: public (default) or internal.
          --values                  Also convert generated records and models to and from GES values and messages.
          --checked-exclusivity     Keep the dynamic exclusivity checks of generated Swift classes.
          --extend-namespace        Extend the enclosing components of the Swift namespace, which another generation of the
                                    module declares, instead of declaring them; wire code with --models does so for shared ones.
          --bindings                Also offer generated model types to scripts as external types; only with --model.
          --documents               Also read model roots from GES document text; includes --values, only with --model.
          --models <namespace>      Share the packed types of the model code in that namespace ("" for the global one) and
                                    convert wire records directly to and from the models of their schemas; only wire code.
          -d, --out-dir <dir>       Write the code as several files into a directory that generate owns, with a manifest.
          --split <kind>            With --out-dir, one file per declaration (schema, the default) or per schema file (file).
          --check                   Compare the generated code with the output file or directory instead of writing it;
                                    changed, missing and surplus files fail.
          --param <name>=<literal>  A parameter value such as worldWidth=5000m; repeat for each one.
          --data <path>             The binary data of dump.
          --baseline <path>         The full record that makes dump read the data as a delta.
          -o, --output <path>       Write the formatted source, the layout or the code to a UTF-8 file instead of stdout.
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
        guard ["check", "format", "hash", "canonical", "dump", "layout", "generate"].contains(command) else { throw ToolError.usage("Unknown schema command '\(command)'.") }
        var paths: [String] = []
        var root: String?
        var roots: [String] = []
        var allRoots = false
        var wireGroup: String?
        var modelGroup: String?
        var check = false
        var withValues = false
        var checkedExclusivity = false
        var extendNamespace = false
        var bindings = false
        var documents = false
        var models: String?
        var language: String?
        var space: String?
        var access: String?
        var data: String?
        var baseline: String?
        var output: String?
        var outDir: String?
        var split: String?
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
            } else if argument == "--all-roots" && (command == "layout" || command == "generate") {
                allRoots = true
            } else if argument == "--check" && command == "generate" {
                check = true
            } else if argument == "--values" && command == "generate" {
                withValues = true
            } else if argument == "--checked-exclusivity" && command == "generate" {
                checkedExclusivity = true
            } else if argument == "--extend-namespace" && command == "generate" {
                extendNamespace = true
            } else if argument == "--bindings" && command == "generate" {
                bindings = true
            } else if argument == "--documents" && command == "generate" {
                documents = true
            } else if ["--root", "--data", "--baseline", "--param", "-o", "--output", "--language", "--namespace", "--access", "--wire", "--model", "-d", "--out-dir", "--split", "--models"].contains(argument) {
                let applies: Bool
                switch argument {
                case "--root": applies = ["hash", "canonical", "dump", "layout", "generate"].contains(command)
                case "--wire", "--model": applies = command == "layout" || command == "generate"
                case "--param": applies = ["hash", "dump", "layout", "generate"].contains(command)
                case "--data", "--baseline": applies = command == "dump"
                case "--language", "--namespace", "--access", "-d", "--out-dir", "--split", "--models": applies = command == "generate"
                default: applies = ["format", "layout", "generate"].contains(command)
                }
                guard applies else { throw ToolError.usage("Unknown schema \(command) option '\(argument)'.") }
                guard index + 1 < arguments.count else { throw ToolError.usage("Specify a value after \(argument).") }
                index += 1
                let next = arguments[index]
                switch argument {
                case "--wire", "--model":
                    if wireGroup != nil || modelGroup != nil { throw ToolError.usage("Specify one --wire or --model group.") }
                    if argument == "--wire" { wireGroup = next } else { modelGroup = next }
                case "--language": language = next
                case "--namespace": space = next
                case "--access": access = next
                case "--root" where command == "layout" || command == "generate":
                    if roots.contains(next) { throw ToolError.usage("The root '\(next)' is given twice.") }
                    roots.append(next)
                case "--root": root = next
                case "--data": data = next
                case "--baseline": baseline = next
                case "-o", "--output": output = next
                case "-d", "--out-dir": outDir = next
                case "--split": split = next
                case "--models": models = next
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
        if (command == "layout" || command == "generate") && (roots.isEmpty ? 0 : 1) + (allRoots ? 1 : 0) + (wireGroup == nil && modelGroup == nil ? 0 : 1) != 1 {
            throw ToolError.usage("\(command) requires one of --root, --all-roots, --wire and --model.")
        }
        if modelGroup != nil && checkedExclusivity { throw ToolError.usage("--checked-exclusivity concerns wire code, not --model.") }
        if bindings && modelGroup == nil { throw ToolError.usage("--bindings concerns model code and requires --model.") }
        if documents && modelGroup == nil { throw ToolError.usage("--documents concerns model code and requires --model.") }
        if models != nil && modelGroup != nil { throw ToolError.usage("--models concerns wire code, not --model.") }
        if command == "generate" && language != "csharp" && language != "swift" { throw ToolError.usage("generate requires --language csharp or --language swift.") }
        if let access, access != "public" && access != "internal" { throw ToolError.usage("--access is public or internal.") }
        if output != nil && outDir != nil { return generateUsage("-o writes one file and --out-dir a directory; give one of them.") }
        if split != nil && outDir == nil { return generateUsage("--split divides the files of --out-dir.") }
        if let split, split != "schema" && split != "file" { throw ToolError.usage("--split is schema or file.") }
        if let outDir, outDir.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { throw ToolError.usage("The output directory must not be empty.") }
        if check && output == nil && outDir == nil { throw ToolError.usage("--check requires -o or --out-dir with what to compare.") }
        if let output {
            guard !output.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { throw ToolError.usage("The output path must not be empty.") }
            try ToolFiles.ensureDistinct(output, from: paths)
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
            case "layout", "generate":
                let options =
                    command == "layout"
                    ? nil
                    : GesSchemaGenerateOptions(
                        language: language == "swift" ? .swift : .csharp,
                        namespace: space ?? "",
                        access: access == "internal" ? .internal : .public,
                        values: withValues,
                        checkedExclusivity: checkedExclusivity,
                        bindings: bindings,
                        documents: documents,
                        split: outDir == nil ? .none : split == "file" ? .file : .schema,
                        models: models,
                        extendNamespace: extendNamespace
                    )
                if let group = wireGroup ?? modelGroup {
                    roots = wireGroup != nil ? GesSchemaGenerator.wireRoots(set, group: group) : GesSchemaGenerator.modelRoots(set, group: group)
                    if roots.isEmpty {
                        io.line("schema: error generate.usage: The files have no \(wireGroup != nil ? "wire" : "model") root in the group '\(group)'.", toError: true)
                        return 1
                    }
                }
                return try layout(set, allRoots ? set.schemas.map(\.name) : roots, parameters, output, outDir, options, check, model: modelGroup != nil)
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
            let bytes = Array(try Data(contentsOf: URL(fileURLWithPath: data!)))
            let dump = try baseline.map { GesSchemaDump.read(bound, data: bytes, baseline: Array(try Data(contentsOf: URL(fileURLWithPath: $0)))) } ?? GesSchemaDump.read(bound, data: bytes)
            io.write(dump.text)
            return dump.isValid ? 0 : 1
        } catch let error as GesSchemaError {
            let place = error.line > 0 ? "\(error.sourceName)(\(error.line),\(error.column))" : "schema"
            io.line("\(place): error \(error.code): \(error.message)", toError: true)
            return 1
        }
    }

    // Prints the layout, or with options the generated code; with check it compares the code with the output file.
    private func layout(
        _ set: GesSchemaSet,
        _ roots: [String],
        _ parameters: [(String, String)],
        _ output: String?,
        _ outDir: String?,
        _ options: GesSchemaGenerateOptions?,
        _ check: Bool,
        model: Bool
    ) throws -> Int {
        let values = GesSchemaParameters()
        for (name, literal) in parameters {
            if try model || !roots.contains(where: { try set.schema($0).parameters.contains { $0.name == name } }) {
                io.line("schema: error schema.unknownName: No root reaches the parameter '\(name)'.", toError: true)
                return 1
            }
            try values.with(name, literal: literal)
        }
        let text: String
        do {
            if let outDir, let options {
                let files =
                    model
                    ? try GesSchemaGenerator.generateModelFiles(set, roots: roots, options: options)
                    : try GesSchemaGenerator.generateFiles(set, roots: roots, parameters: values, options: options)
                let directory = ToolGeneratedDirectory(io: io)
                return check ? try directory.check(outDir, files) : try directory.write(outDir, files)
            }
            if model {
                text = try options.map { try GesSchemaGenerator.generateModels(set, roots: roots, options: $0) } ?? GesSchemaGenerator.modelLayout(set, roots: roots)
            } else {
                text = try options.map { try GesSchemaGenerator.generate(set, roots: roots, parameters: values, options: $0) } ?? GesSchemaGenerator.layout(set, roots: roots, parameters: values)
            }
        } catch let error as GesGeneratorError {
            io.line("schema: error \(error.code): \(error.message)", toError: true)
            return 1
        }
        if check, let output {
            let path = try ToolFiles.fullPath(output)
            guard FileManager.default.fileExists(atPath: path) else {
                io.line("error cli.outdated: \(output) does not exist.", toError: true)
                return 1
            }
            if Array(try Data(contentsOf: URL(fileURLWithPath: path))) == Array(text.utf8) { return 0 }
            io.line("error cli.outdated: \(output) differs from the generated code.", toError: true)
            return 1
        }
        if let output { try ToolFiles.write(Data(text.utf8), to: try ToolFiles.fullPath(output)) } else { io.write(text) }
        return 0
    }

    private func generateUsage(_ message: String) -> Int {
        io.line("schema: error generate.usage: \(message)", toError: true)
        return 1
    }

    private func hex(_ value: UInt64) -> String {
        let digits = String(value, radix: 16)
        return String(repeating: "0", count: 16 - digits.count) + digits
    }
}
