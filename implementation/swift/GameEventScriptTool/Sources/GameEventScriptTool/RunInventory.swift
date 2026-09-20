// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime

final class RunInventory {
    struct Entry {
        let id: Int
        let instance: GameEventScriptInstance
        let paths: [String]
    }
    let host: GameEventScriptHost
    let io: ToolIO
    private var entries: [Entry] = []
    private var native: [(String, GameEventScriptSubscription)] = []
    private(set) var nextID: Int
    var active: [Entry] { entries.filter { $0.instance.isAttached } }
    init(host: GameEventScriptHost, io: ToolIO, nextID: Int = 1) {
        self.host = host
        self.io = io
        self.nextID = nextID
    }
    @discardableResult func load(_ program: GameEventScriptProgram, paths: [String], id: Int? = nil) throws
        -> GameEventScriptInstance
    {
        let instance = try host.load(program)
        let assignedID = id ?? nextID
        entries.append(Entry(id: assignedID, instance: instance, paths: paths))
        nextID = max(nextID, assignedID + 1)
        return instance
    }
    func unload(_ selector: String) throws {
        let entry = try select(selector, command: ":unload")
        entry.instance.detach()
        entries.removeAll { $0.id == entry.id }
    }
    @discardableResult func unloadAll() -> Int {
        let count = active.count
        for entry in entries { entry.instance.detach() }
        entries.removeAll()
        return count
    }
    func subscribe(_ name: String, handler: any GameEventScriptNativeMessageHandler) throws {
        native.append((name, try host.subscribeMessageName(name, handler: handler)))
    }
    func handlers(_ program: GameEventScriptProgram) -> [GameEventScriptBinding] {
        program.bindings.filter { isHandler($0) && program.stringConstants[Int($0.name)] != "initialization" }
    }
    func programs() {
        io.line(toError: true)
        io.line("Loaded programs (\(active.count)):", toError: true)
        for entry in active {
            let p = entry.instance.program
            io.line(
                "  @\(entry.id)  \(p.moduleName)  version=\(p.programVersion)  handlers=\(handlers(p).count)",
                toError: true)
            io.line("      files: " + entry.paths.map(quoted).joined(separator: ", "), toError: true)
        }
        if active.isEmpty { io.line("  No programs loaded. Use :load <file>.", toError: true) }
        io.line(toError: true)
    }
    func registeredHandlers() {
        var lines = native.filter { $0.1.isSubscribed }.map { "  native  \($0.0)(...) [name-only]" }
        for entry in active {
            let p = entry.instance.program
            func tags(_ prefix: String, _ values: [UInt16]) -> String {
                values.isEmpty ? "" : prefix + values.map { "#" + p.stringConstants[Int($0)] }.joined(separator: ", ")
            }
            for binding in handlers(p) {
                let name = p.stringConstants[Int(binding.name)]
                let signature =
                    binding.kind == .messageNameHandler
                    ? name + "(...) [name-only]"
                    : name + "(" + binding.argumentNames.map { p.stringConstants[Int($0)] }.joined(separator: ",")
                        + ") [signature]"
                lines.append(
                    "  @\(entry.id)  \(p.moduleName)  \(signature)" + tags(" matching ", binding.requiredTags)
                        + tags(" without ", binding.excludedTags))
            }
        }
        io.line(toError: true)
        io.line("Registered handlers (\(lines.count)):", toError: true)
        for line in lines { io.line(line, toError: true) }
        io.line(toError: true)
    }
    func select(_ selector: String, command: String) throws -> Entry {
        guard !selector.isEmpty, !selector.contains(where: \.isWhitespace) else {
            throw ToolError.usage("Specify one module name or @program-id after \(command). Use :list.")
        }
        let matches: [Entry]
        if selector.hasPrefix("@") {
            let value = String(selector.dropFirst())
            guard RunOptions.integerSpelling(value, signed: false), let id = Int(value), id > 0 else {
                throw ToolError.usage("Use @ followed by a positive integer, for example \(command) @1.")
            }
            matches = active.filter { $0.id == id }
        } else {
            matches = active.filter { GesText.scalarEqual($0.instance.program.moduleName, selector) }
        }
        guard !matches.isEmpty else { throw ToolError.usage("No loaded program matches '\(selector)'. Use :list.") }
        guard matches.count == 1 else {
            throw ToolError.usage(
                "Module '\(selector)' is loaded more than once. Use \(command) with one of: "
                    + matches.map { "@\($0.id)" }.joined(separator: ", ") + ".")
        }
        return matches[0]
    }
    func dump(_ selector: String, color: Bool) throws {
        let p = try select(selector, command: ":dump").instance.program
        var text = GameEventScriptProgramDumper.dump(p)
        if io.errorTerminal { text = TextDisplay.expandTabs(text) }
        io.line(toError: true)
        io.write(color ? Highlighting().renderAssembly(text) : text, toError: true)
        io.line(toError: true)
    }
    func source(_ selector: String, color: Bool) throws {
        let entry = try select(selector, command: ":source")
        io.line(toError: true)
        guard let sources = entry.instance.program.sourceArchive, !sources.isEmpty else {
            io.line(
                "No embedded sources available for @\(entry.id) (\(entry.instance.program.moduleName)). Compile with source archive/debug information to include them.",
                toError: true)
            io.line(toError: true)
            return
        }
        for source in sources {
            let header = "// Source: " + quoted(source.sourceName)
            io.line(color ? Highlighting.paint(header, 90) : header, toError: true)
            var text = source.text
            if io.errorTerminal { text = TextDisplay.expandTabs(text) }
            io.write(color ? Highlighting().render(text) : text, toError: true)
            if text.utf8.last != 10 && text.utf8.last != 13 { io.line(toError: true) }
            io.line(toError: true)
        }
    }
}
