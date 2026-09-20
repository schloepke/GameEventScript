// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime

struct RunOptions {
    var inputs: [String] = [], scenarios: [String] = [], arguments: [GesValue] = []
    var seed: Int64?
    var limits = GameEventScriptRuntimeLimits()
    var verbose = false, quiet = false, interactive = false, color = false, help = false

    init(_ args: [String]) throws {
        var options = true
        var group = false
        var suppliedArguments = false
        var supplied = Set<String>()
        var index = 0
        while index < args.count {
            let argument = args[index]
            defer { index += 1 }
            if !options {
                arguments.append(.text(argument))
                continue
            }
            if argument == "--" {
                options = false
                suppliedArguments = true
                continue
            }
            if argument == "--args" {
                guard index + 1 < args.count, !Self.isOption(args[index + 1]) else {
                    throw ToolError.usage("Specify at least one value after --args.")
                }
                group = true
                suppliedArguments = true
                continue
            }
            if group && !Self.isOption(argument) {
                arguments.append(.text(argument))
                continue
            }
            group = false
            if ["--help", "-h"].contains(argument) {
                help = true
                return
            }
            if ["--scenario", "--arg", "--seed", "--max-messages", "--max-steps"].contains(argument) {
                guard index + 1 < args.count else { throw ToolError.usage("Specify a value after \(argument).") }
                if !["--scenario", "--arg"].contains(argument), !supplied.insert(argument).inserted {
                    throw ToolError.usage("Specify \(argument) only once.")
                }
                index += 1
                let value = args[index]
                if argument == "--scenario" {
                    guard !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty, !value.hasPrefix("-") else {
                        throw ToolError.usage("Specify a scenario path; prefix a path starting with '-' with './'.")
                    }
                    scenarios.append(value)
                } else if argument == "--arg" {
                    suppliedArguments = true
                    arguments.append(.text(value))
                } else if argument == "--seed" {
                    guard let parsed = Int64(value), Self.integerSpelling(value, signed: true) else {
                        throw ToolError.usage("The seed must be a signed 64-bit integer.")
                    }
                    seed = parsed
                } else {
                    guard let parsed = Int32(value), parsed > 0, Self.integerSpelling(value, signed: false) else {
                        throw ToolError.usage("\(argument) requires a positive 32-bit integer.")
                    }
                    if argument == "--max-messages" {
                        limits.maxProcessedEventsPerRun = Int(parsed)
                    } else {
                        limits.maxExecutionSteps = Int(parsed)
                    }
                }
            } else if argument == "--color" {
                color = true
            } else if argument == "--interactive" {
                interactive = true
            } else if ["-v", "--verbose"].contains(argument) {
                verbose = true
            } else if ["-q", "--quiet"].contains(argument) {
                quiet = true
            } else if argument.hasPrefix("-") {
                throw ToolError.usage("Unknown run option '\(argument)'.")
            } else {
                guard !argument.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
                    throw ToolError.usage("Program paths must not be empty.")
                }
                inputs.append(argument)
            }
        }
        guard !inputs.isEmpty || interactive else {
            throw ToolError.usage("Specify source files or .gesb files to run.")
        }
        guard !(verbose && quiet) else { throw ToolError.usage("--verbose and --quiet cannot be combined.") }
        guard !(interactive && !scenarios.isEmpty) else {
            throw ToolError.usage("--interactive and --scenario cannot be combined.")
        }
        guard !(suppliedArguments && (interactive || !scenarios.isEmpty)) else {
            throw ToolError.usage("--arg, --args, and -- are available only when running Main.")
        }
    }
    static func integerSpelling(_ value: String, signed: Bool) -> Bool {
        var bytes = Array(value.utf8)
        if signed, bytes.first == 43 || bytes.first == 45 { bytes.removeFirst() }
        return !bytes.isEmpty && bytes.allSatisfy { (48...57).contains($0) }
    }
    static func isOption(_ text: String) -> Bool {
        let bytes = Array(text.utf8)
        return bytes.count > 1 && bytes[0] == 45 && !(48...57).contains(bytes[1])
            && !(bytes.count > 2 && bytes[1] == 46 && (48...57).contains(bytes[2]))
    }
}

extension Tool {
    func runPrograms(_ arguments: [String]) throws -> Int {
        var options = try RunOptions(arguments)
        if options.help {
            io.line(ToolHelp.run)
            return 0
        }
        let paths = try ToolFiles.expand(options.inputs)
        let binaries = paths.filter(ToolFiles.isBinary).count
        guard binaries == 0 || binaries == paths.count else {
            throw ToolError.usage(
                "Run accepts either sources compiled together or binaries loaded separately; inputs cannot be mixed.")
        }
        var programs: [GameEventScriptProgram] = []
        if binaries > 0 {
            for path in paths {
                do { programs.append(try ToolFiles.read(path)) } catch let error as GameEventScriptProgramFormatError {
                    io.report(error, fallback: path)
                    return 1
                }
            }
        } else if !paths.isEmpty {
            programs.append(try ToolFiles.compile(paths))
        }
        let scenarios = try ToolFiles.expand(options.scenarios)
        let scenario = scenarios.isEmpty ? nil : try ToolFiles.compile(scenarios)
        options.color = options.color && !io.noColor
        let session = try RunSession(options: options, io: io)
        for (index, program) in programs.enumerated() {
            try session.inventory.load(program, paths: binaries > 0 ? [paths[index]] : paths)
        }
        if let scenario { try session.inventory.load(scenario, paths: scenarios) }
        let main = scenario == nil && !options.interactive
        if main && !programs.contains(where: Self.hasMain) {
            io.line(
                "error cli.missingMain: No handler matches Main(args). Add 'on Main(args)', use --scenario, or use --interactive.",
                toError: true)
            return 1
        }
        guard try session.pump() else { return 1 }
        if main {
            let message = try GameEventScriptMessage(
                name: "Main", arguments: [.init(name: "args", value: .list(options.arguments))])
            guard session.host.receive(message) else {
                io.line("error cli.mainRejected: The host rejected Main(args).", toError: true)
                return 1
            }
            guard try session.pump() else { return 1 }
        } else if options.interactive {
            guard try runConsole(session, options: options) else { return 1 }
        }
        if !options.quiet { session.summary() }
        return session.observer.exitCode
    }
    static func hasMain(_ program: GameEventScriptProgram) -> Bool {
        program.bindings.contains {
            guard $0.requiredTags.isEmpty, program.stringConstants[Int($0.name)] == "Main" else { return false }
            return $0.kind == .messageNameHandler
                || ($0.kind == .messageHandler && $0.argumentNames.count == 1
                    && program.stringConstants[Int($0.argumentNames[0])] == "args")
        }
    }
}
