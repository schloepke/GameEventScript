// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import GameEventScriptRuntime

extension Tool {
    func runConsole(_ session: RunSession, options: RunOptions) throws -> Bool {
        if io.inputTerminal {
            io.line(
                "GES event console. Type :help for commands and examples, :load <file> to add a program, or :quit to exit.",
                toError: true)
        }
        let prompt =
            options.color && io.inputTerminal && io.outputTerminal && io.errorTerminal ? TerminalPrompt(io: io) : nil
        var success = true
        var lineNumber = 0
        while io.outputError == nil {
            if io.inputTerminal && prompt == nil { io.write("ges> ", toError: true) }
            guard var line = try prompt?.readLine() ?? (prompt == nil ? io.input() : nil) else { return success }
            lineNumber += 1
            if lineNumber == 1 && line.hasPrefix("\u{feff}") { line.removeFirst() }
            let trimmed = line.trimmingCharacters(in: .whitespacesAndNewlines)
            if trimmed.isEmpty { continue }
            let end = trimmed.firstIndex(where: \.isWhitespace) ?? trimmed.endIndex
            let command = String(trimmed[..<end])
            let argument = String(trimmed[end...]).trimmingCharacters(in: .whitespacesAndNewlines)
            if command == ":quit" && argument.isEmpty { return success }
            if command == ":help" {
                if !consoleHelp(argument) { success = false }
                continue
            }
            if command == ":list" && argument.isEmpty {
                session.inventory.programs()
                continue
            }
            if command == ":handler" && argument.isEmpty {
                session.inventory.registeredHandlers()
                continue
            }
            if ![":load", ":dump", ":source", ":unload", ":unloadAll", ":reload"].contains(command),
                command.hasPrefix(":"),
                command.dropFirst().allSatisfy({ $0 >= "a" && $0 <= "z" })
            {
                io.line(
                    "error cli.consoleCommand: Unknown command or arguments '\(trimmed)'. Use :help.", toError: true)
                success = false
                continue
            }
            var instance: GameEventScriptInstance?
            var path = "<interactive:\(lineNumber)>"
            do {
                defer { instance?.detach() }
                if command == ":unload" {
                    try session.inventory.unload(argument)
                    if !options.quiet { io.line("Unloaded \(argument).", toError: true) }
                    continue
                }
                if command == ":unloadAll" || command == ":reload" {
                    guard argument.isEmpty else { throw ToolError.usage("\(command) accepts no arguments.") }
                    if command == ":unloadAll" {
                        let count = session.inventory.unloadAll()
                        if !options.quiet {
                            io.line("Unloaded \(count) programs. Native console handlers remain active.", toError: true)
                        }
                    } else {
                        guard try session.reload(activePath: &path) else { return false }
                        if !options.quiet {
                            io.line(
                                "Reloaded all active programs on a fresh host; initialization completed.", toError: true
                            )
                        }
                    }
                    continue
                }
                if command == ":dump" {
                    try session.inventory.dump(argument, color: options.color)
                    continue
                }
                if command == ":source" {
                    try session.inventory.source(argument, color: options.color)
                    continue
                }
                if command == ":load" {
                    path = try Self.loadPath(argument)
                    try session.inventory.load(ToolFiles.read(path), paths: [path])
                    guard try session.pump() else { return false }
                    if !options.quiet {
                        io.line("Loaded \(path); initialization completed. Handlers remain active.", toError: true)
                    }
                    continue
                }
                let program = try GameEventScriptBuilder().addScript(
                    "on initialization {\n" + line + "\n}\n", sourceName: path
                ).compile()
                let handlers = program.bindings.filter(isHandler)
                guard handlers.count == 1, program.stringConstants[Int(handlers[0].name)] == "initialization" else {
                    io.line(
                        "error cli.interactiveInput: Enter handler-body statements, not additional handler declarations.",
                        toError: true)
                    success = false
                    continue
                }
                instance = try session.host.load(program)
                guard try session.pump() else { return false }
            } catch {
                io.report(error, fallback: path, argumentCode: "cli.consoleCommand")
                success = false
            }
        }
        return false
    }
    func consoleHelp(_ topic: String) -> Bool {
        let text: String
        switch topic {
        case "": text = ToolHelp.console
        case "load", ":load": text = ToolHelp.load
        case "unload", ":unload", "unloadAll", ":unloadAll", "reload", ":reload": text = ToolHelp.lifecycle
        case "list", ":list", "handler", ":handler", "dump", ":dump", "source", ":source": text = ToolHelp.inspection
        default:
            io.line(
                "error cli.consoleCommand: Unknown help topic '\(topic)'. Use :help, :help load, or :help dump.",
                toError: true)
            return false
        }
        io.line(toError: true)
        io.line(text, toError: true)
        io.line(toError: true)
        return true
    }
    static func loadPath(_ text: String) throws -> String {
        var path = text
        if let quote = path.first, quote == "'" || quote == "\"" {
            guard path.count >= 2, path.last == quote else {
                throw ToolError.usage("The quoted :load path must end with a matching quote.")
            }
            path = String(path.dropFirst().dropLast()).replacingOccurrences(
                of: String(repeating: quote, count: 2), with: String(quote))
        }
        guard !path.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            throw ToolError.usage("Specify one file after :load. Use :help load for examples.")
        }
        _ = try ToolFiles.fullPath(path)
        return path
    }
}
