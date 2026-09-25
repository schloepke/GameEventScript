// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import GameEventScriptRuntime

final class Tool {
    let io: ToolIO

    init(io: ToolIO) { self.io = io }

    func run(_ arguments: [String]) -> Int {
        guard let command = arguments.first else {
            io.line(ToolHelp.overview)
            return 0
        }
        let args = Array(arguments.dropFirst())
        if args.isEmpty {
            if command == "--help" || command == "-h" {
                io.line(ToolHelp.overview)
                return 0
            }
            if command == "--version" {
                io.line("ges \(ToolBuildInfo.version) (Swift)")
                return 0
            }
        }
        guard ["compile", "check", "dump", "run"].contains(command) else {
            io.line("Unknown command or arguments. Run 'ges --help' for usage.", toError: true)
            return 2
        }
        do {
            let result: Int
            switch command {
            case "compile", "check": result = try compile(args, check: command == "check")
            case "dump": result = try dump(args)
            default: result = try runPrograms(args)
            }
            return io.outputError == nil ? result : 1
        } catch ToolError.usage(let message) {
            io.line("error cli.usage: \(message) Run 'ges \(command) --help' for usage.", toError: true)
            return 2
        } catch {
            io.report(error)
            return 1
        }
    }

    func compile(_ arguments: [String], check: Bool) throws -> Int {
        if arguments == ["--help"] || arguments == ["-h"] {
            io.line(check ? ToolHelp.check : ToolHelp.compile)
            return 0
        }
        var inputs: [String] = []
        var output: String?
        var debug = true
        var verbose = false
        var quiet = false
        var options = true
        var index = 0
        while index < arguments.count {
            let argument = arguments[index]
            if options && argument == "--" {
                options = false
            } else if !check && options && ["-o", "--output"].contains(argument) {
                guard output == nil, index + 1 < arguments.count, !arguments[index + 1].hasPrefix("-") else { throw ToolError.usage("Specify one output path after -o or --output. Prefix paths starting with '-' with './'.") }
                index += 1
                output = arguments[index]
            } else if !check && options && argument == "--no-debug" {
                debug = false
            } else if options && ["-v", "--verbose"].contains(argument) {
                verbose = true
            } else if options && ["-q", "--quiet"].contains(argument) {
                quiet = true
            } else if options && argument.hasPrefix("-") {
                throw ToolError.usage("Unknown option '\(argument)'.")
            } else {
                guard !argument.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { throw ToolError.usage("Source paths must not be empty.") }
                inputs.append(argument)
            }
            index += 1
        }
        guard !inputs.isEmpty else { throw ToolError.usage("Specify at least one source file to compile.") }
        guard !(verbose && quiet) else { throw ToolError.usage("--verbose and --quiet cannot be combined.") }
        if let output, output.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { throw ToolError.usage("The output path must not be empty.") }
        let started = ProcessInfo.processInfo.systemUptime
        let paths = try ToolFiles.expand(inputs)
        if !check {
            guard paths.count == 1 || output != nil else { throw ToolError.usage("Specify -o or --output when compiling multiple source files.") }
            output = try ToolFiles.fullPath(output ?? (paths[0] as NSString).deletingPathExtension + ".gesb")
            try ToolFiles.ensureDistinct(output!, from: paths)
        }
        let program = try ToolFiles.compile(paths, debug: debug)
        var fileSize = 0
        if let output {
            let bytes = try GameEventScriptProgramWriter.bytes(program)
            try ToolFiles.write(Data(bytes), to: output)
            fileSize = bytes.count
        }
        if !quiet {
            io.line(check ? "Checked successfully." : "Compiled successfully.")
            io.line("Sources (\(paths.count)):")
            for path in paths { io.line("  " + path) }
            if let output { io.line("Output: " + output) }
            io.line("Module: " + program.moduleName)
            if !check {
                io.line("Handlers: \(program.bindings.filter(isHandler).count)")
                io.line("File size: \(fileSize) bytes")
                var sections: [String] = []
                if program.debugSymbols != nil { sections.append("symbols") }
                if program.sourceMap != nil { sections.append("source map") }
                if program.sourceArchive != nil { sections.append("source archive") }
                io.line("Debug info: " + (sections.isEmpty ? "none" : sections.joined(separator: ", ")))
            }
            io.line(String(format: "Duration: %.1f ms", locale: Locale(identifier: "en_US_POSIX"), (ProcessInfo.processInfo.systemUptime - started) * 1000))
            if verbose { reportDetails(program) }
        }
        return 0
    }

    func reportDetails(_ program: GameEventScriptProgram) {
        io.line()
        io.line("Binary format: .gesb V\(program.formatVersion)")
        io.line("Bytecode: \(program.code.count) instructions, \(16 * program.code.count) bytes (instruction data)")
        io.line("Required registers: \(program.requiredRegisterCount)")
        io.line("Required call stack depth: \(program.requiredCallStackDepth)")
        let groups: [(String, [GameEventScriptBinaryBindKind])] = [("Message bindings", [.messageHandler, .messageNameHandler, .outboundMessage]), ("Required extensions", [.extensionCall]), ("Required external types", [.externalType])]
        for (title, kinds) in groups {
            let entries = program.bindings.filter { kinds.contains($0.kind) }
            io.line("\(title) (\(entries.count)):")
            for entry in entries {
                let name = program.stringConstants[Int(entry.name)]
                let signature = entry.kind == .messageNameHandler ? name + " as message" : name + "(" + entry.argumentNames.map { program.stringConstants[Int($0)] }.joined(separator: ", ") + ")"
                let prefix = isHandler(entry) ? "handler " : entry.kind == .outboundMessage ? "outbound " : ""
                io.line("  " + prefix + signature)
            }
        }
    }

    func dump(_ arguments: [String]) throws -> Int {
        if arguments == ["--help"] || arguments == ["-h"] {
            io.line(ToolHelp.dump)
            return 0
        }
        var input: String?
        var output: String?
        var addresses = false
        var options = true
        var index = 0
        while index < arguments.count {
            let argument = arguments[index]
            if options && argument == "--" {
                options = false
            } else if options && ["-o", "--output"].contains(argument) {
                guard output == nil, index + 1 < arguments.count, !arguments[index + 1].hasPrefix("-") else { throw ToolError.usage("Specify one output path after -o or --output.") }
                index += 1
                output = arguments[index]
            } else if options && argument == "--addresses" {
                addresses = true
            } else if options && argument.hasPrefix("-") {
                throw ToolError.usage("Unknown dump option '\(argument)'.")
            } else if input == nil {
                input = argument
            } else {
                throw ToolError.usage("Dump accepts exactly one binary file.")
            }
            index += 1
        }
        guard let input, !input.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { throw ToolError.usage("Specify a binary file to dump.") }
        _ = try ToolFiles.fullPath(input)
        if let path = output {
            guard !path.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { throw ToolError.usage("The output path must not be empty.") }
            output = try ToolFiles.fullPath(path)
            try ToolFiles.ensureDistinct(path, from: [input])
        }
        let program: GameEventScriptProgram
        do { program = try GameEventScriptProgramReader.read(Array(Data(contentsOf: URL(fileURLWithPath: input)))) } catch let error as GameEventScriptProgramFormatError {
            io.report(error, fallback: input)
            return 1
        }
        var text = GameEventScriptProgramDumper.dump(program, includeInstructionAddresses: addresses)
        if output == nil && io.outputTerminal { text = TextDisplay.expandTabs(text) }
        if let output {
            let data = Data(text.utf8)
            try ToolFiles.write(data, to: output)
            io.line("Dumped \(input) -> \(output) (\(data.count) bytes).")
        } else {
            io.write(text)
        }
        return 0
    }
}

func isHandler(_ binding: GameEventScriptBinding) -> Bool { binding.kind == .messageHandler || binding.kind == .messageNameHandler }
