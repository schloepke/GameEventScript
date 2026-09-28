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

    let workspace: RunWorkspace
    let host: GameEventScriptHost
    let io: ToolIO
    private var entries: [Entry] = []
    private var native: [(String, GameEventScriptSubscription)] = []
    private(set) var nextID: Int
    var active: [Entry] { entries.filter { $0.instance.isAttached } }

    init(host: GameEventScriptHost, io: ToolIO, workspace: RunWorkspace, nextID: Int = 1) {
        self.workspace = workspace
        self.host = host
        self.io = io
        self.nextID = nextID
    }

    @discardableResult func load(_ program: GameEventScriptProgram, paths: [String], id: Int? = nil) throws -> GameEventScriptInstance {
        let instance = try host.load(program)
        let assignedID = id ?? nextID
        entries.append(Entry(id: assignedID, instance: instance, paths: paths))
        nextID = max(nextID, assignedID + 1)
        return instance
    }

    func unload(_ selector: String) throws {
        if let id = Int(selector.hasPrefix("@") ? String(selector.dropFirst()) : selector), workspace.drafts[id] != nil, !active.contains(where: { $0.id == id }) {
            try removeDraft(selector)
            return
        }
        let entry = try select(selector, command: ":unload")
        try removeDraft(selector)
        entry.instance.detach()
        entries.removeAll { $0.id == entry.id }
    }

    @discardableResult func unloadAll() -> Int {
        let count = active.count
        for entry in entries { entry.instance.detach() }
        entries.removeAll()
        return count
    }

    func subscribe(_ name: String, handler: any GameEventScriptNativeMessageHandler) throws { native.append((name, try host.subscribeMessageName(name, handler: handler))) }

    func handlers(_ program: GameEventScriptProgram) -> [GameEventScriptBinding] { program.bindings.filter { isHandler($0) && program.stringConstants[Int($0.name)] != "initialization" } }

    func programs() {
        io.line(toError: true)
        io.line("Loaded programs (\(active.count)):", toError: true)
        for entry in active {
            let p = entry.instance.program
            io.line("  \(entry.id)  \(entry.id == 0 ? "scratch [no file]" : p.moduleName)  version=\(p.programVersion)  handlers=\(handlers(p).count)\(draftStatus(entry.id))", toError: true)
            io.line("      files: " + entry.paths.map(quoted).joined(separator: ", "), toError: true)
        }
        for draft in workspace.drafts.values.sorted(by: { $0.id < $1.id }) where !active.contains(where: { $0.id == draft.id }) {
            io.line("  \(draft.id)  \(draft.id == 0 ? "scratch [no file]" : draft.paths.joined(separator: ", "))\(draftStatus(draft.id))", toError: true)
        }
        if active.isEmpty && workspace.drafts.isEmpty { io.line("  No programs loaded. Use :load <file>.", toError: true) }
        io.line(toError: true)
    }

    func registeredHandlers() {
        var lines = native.filter { $0.1.isSubscribed }.map { "  native  \($0.0)(...) [name-only]" }
        for entry in active {
            let p = entry.instance.program

            func tags(_ prefix: String, _ values: [UInt16]) -> String { values.isEmpty ? "" : prefix + values.map { "#" + p.stringConstants[Int($0)] }.joined(separator: ", ") }

            for binding in handlers(p) {
                let name = p.stringConstants[Int(binding.name)]
                let signature = binding.kind == .messageNameHandler ? name + "(...) [name-only]" : name + "(" + binding.argumentNames.map { p.stringConstants[Int($0)] }.joined(separator: ",") + ") [signature]"
                lines.append("  \(entry.id)  \(p.moduleName)  \(signature)" + tags(" matching ", binding.requiredTags) + tags(" without ", binding.excludedTags))
            }
        }
        io.line(toError: true)
        io.line("Registered handlers (\(lines.count)):", toError: true)
        for line in lines { io.line(line, toError: true) }
        io.line(toError: true)
    }

    func findDraft(_ selector: String) throws -> RunWorkspace.Draft? {
        if let id = Int(selector.hasPrefix("@") ? String(selector.dropFirst()) : selector) { return workspace.drafts[id] }
        return try workspace.drafts[select(selector, command: ":source").id]
    }

    private func draftStatus(_ id: Int) -> String {
        guard let draft = workspace.drafts[id] else { return "" }
        return (draft.dirty ? " *" : "") + (draft.pending ? " [draft not applied]" : "")
    }

    func promoteScratch(_ path: String) -> Int {
        let id = nextID
        nextID += 1
        if let index = entries.firstIndex(where: { $0.id == 0 }) { entries[index] = Entry(id: id, instance: entries[index].instance, paths: [path]) }
        return id
    }

    func removeDraft(_ selector: String) throws {
        let draft = try findDraft(selector)
        guard draft?.dirty != true else { throw ToolError.usage("Unsaved changes. Save before unloading, or use :quit! to discard the session.") }
        if let draft { workspace.drafts.removeValue(forKey: draft.id) }
    }

    func select(_ selector: String, command: String) throws -> Entry {
        guard !selector.isEmpty, !selector.contains(where: \.isWhitespace) else { throw ToolError.usage("Specify one module name or program ID after \(command). Use :list.") }
        let matches: [Entry]
        if selector.hasPrefix("@") || RunOptions.integerSpelling(selector, signed: false) {
            let value = selector.hasPrefix("@") ? String(selector.dropFirst()) : selector
            guard RunOptions.integerSpelling(value, signed: false), let id = Int(value), id >= 0 else { throw ToolError.usage("A program ID must be a positive integer, for example \(command) 1.") }
            matches = active.filter { $0.id == id }
        } else {
            matches = active.filter { GesText.scalarEqual($0.instance.program.moduleName, selector) }
        }
        guard !matches.isEmpty else { throw ToolError.usage("No loaded program matches '\(selector)'. Use :list.") }
        guard matches.count == 1 else { throw ToolError.usage("Module '\(selector)' is loaded more than once. Use \(command) with one of: " + matches.map { "\($0.id)" }.joined(separator: ", ") + ".") }
        return matches[0]
    }

    func dump(_ selector: String, color: Bool) throws {
        let selector = RunWorkspace.isScratch(selector) ? "0" : selector
        let draft = try findDraft(selector)
        if selector == "0", draft == nil { throw ToolError.usage("No scratch found. Use :edit to create one.") }
        if let draft, draft.applied == nil { throw ToolError.usage("No successful compile is available for this draft.") }
        if draft?.pending == true { io.line("Draft differs from the last successful compile; showing the previous compiled program.", toError: true) }
        let p = try select(selector, command: ":dump").instance.program
        var text = GameEventScriptProgramDumper.dump(p)
        if io.errorTerminal { text = TextDisplay.expandTabs(text) }
        io.line(toError: true)
        io.write(color ? Highlighting().renderAssembly(text) : text, toError: true)
        io.line(toError: true)
    }

    func source(_ selector: String, color: Bool) throws {
        let selector = RunWorkspace.isScratch(selector) ? "0" : selector
        let draft = try findDraft(selector)
        if selector == "0", draft == nil { throw ToolError.usage("No scratch found. Use :edit to create one.") }
        if let draft {
            for (path, source) in zip(draft.paths, draft.text) {
                let header = "// Source: " + quoted(path)
                io.line(color ? Highlighting.paint(header, 90) : header, toError: true)
                let text = io.errorTerminal ? TextDisplay.expandTabs(source) : source
                io.line(color ? Highlighting().render(text) : text, toError: true)
            }
            return
        }
        let entry = try select(selector, command: ":source")
        io.line(toError: true)
        guard let sources = entry.instance.program.sourceArchive, !sources.isEmpty else {
            io.line("No embedded sources available for \(entry.id) (\(entry.instance.program.moduleName)). Compile with source archive/debug information to include them.", toError: true)
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
