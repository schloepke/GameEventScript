// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptCompiler
import GameEventScriptRuntime
import TerminalSupport

// Editable snapshots belong to the CLI session, not the portable Host.
final class RunWorkspace {
    final class Draft {
        var id: Int
        var paths: [String]
        var text: [String]
        var baseline: [Data]
        var applied: [String]?
        var dirty: Bool { id == 0 ? text.contains { !$0.isEmpty } : zip(text, baseline).contains { !GesText.scalarEqual($0, String(decoding: $1, as: UTF8.self).trimmingPrefixBOM) } }
        var pending: Bool {
            guard let applied, applied.count == text.count else { return true }
            return !zip(applied, text).allSatisfy { GesText.scalarEqual($0, $1) }
        }

        init(id: Int, paths: [String], text: [String], baseline: [Data]) {
            self.id = id
            self.paths = paths
            self.text = text
            self.baseline = baseline
        }

        func readFromDisk() throws -> Draft {
            let bytes = try paths.map { try Data(contentsOf: URL(fileURLWithPath: $0)) }
            let sources = try zip(paths, bytes).map { path, data in
                guard let text = String(data: data, encoding: .utf8) else { throw ToolError.encoding(path) }
                return text.trimmingPrefixBOM
            }
            return Draft(id: id, paths: paths, text: sources, baseline: bytes)
        }

        func compile() throws -> GameEventScriptProgram {
            let builder = GameEventScriptBuilder()
            for (path, value) in zip(paths, text) { builder.addScript(value, sourceName: path) }
            return try builder.compile()
        }
    }

    var drafts: [Int: Draft] = [:]

    static func isScratch(_ selector: String) -> Bool { ["", "0", "@0"].contains(selector) }

    func get(_ inventory: RunInventory, _ selector: String) throws -> Draft {
        if Self.isScratch(selector) {
            guard let draft = drafts[0] else { throw ToolError.usage("No scratch found. Use :edit to create one.") }
            return draft
        }
        if let id = Int(selector.hasPrefix("@") ? String(selector.dropFirst()) : selector), let draft = drafts[id] { return draft }
        let entry = try inventory.select(selector, command: ":edit")
        if let draft = drafts[entry.id] { return draft }
        guard entry.paths.allSatisfy({ $0.lowercased().hasSuffix(".ges") }) else { throw ToolError.usage("Only loaded .ges source programs can be edited; binary programs are read-only.") }
        guard let sources = entry.instance.program.sourceArchive, sources.count == entry.paths.count else { throw ToolError.usage("The loaded program has no complete source archive. Reload its source files first.") }
        let text = sources.map(\.text)
        let baseline = try entry.paths.map { try Data(contentsOf: URL(fileURLWithPath: $0)) }
        guard zip(text, baseline).allSatisfy({ GesText.scalarEqual($0, String(decoding: $1, as: UTF8.self).trimmingPrefixBOM) }) else { throw ToolError.usage("Sources changed on disk since loading. Reload before starting an edit.") }
        let draft = Draft(id: entry.id, paths: entry.paths, text: text, baseline: baseline)
        draft.applied = text
        drafts[entry.id] = draft
        return draft
    }

    func edit(_ session: RunSession, _ argument: String) throws -> Bool {
        let words = try Self.words(argument)
        guard words.count <= 2 else { throw ToolError.usage("Use :edit [program-ID|module] [source-number].") }
        let selector = words.first ?? "0"
        if Self.isScratch(selector), drafts[0] == nil { drafts[0] = Draft(id: 0, paths: ["<scratch>"], text: [""], baseline: [Data()]) }
        let draft = try get(session.inventory, selector)
        var index = 0
        if words.count == 2 {
            guard let number = Int(words[1]), number > 0, number <= draft.text.count else { throw ToolError.usage("Source number is outside this program's source list.") }
            index = number - 1
        } else if draft.text.count != 1 {
            throw ToolError.usage("Select a source with :edit \(selector) <source-number>: " + draft.paths.enumerated().map { "\($0.offset + 1): \($0.element)" }.joined(separator: ", "))
        }
        let environment = ProcessInfo.processInfo.environment
        let editor = ["GES_EDITOR", "VISUAL", "EDITOR"].compactMap { environment[$0] }.first { !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty } ?? "nano"
        let command = try Self.words(editor)
        guard !command.isEmpty else { throw ToolError.usage("The editor command is empty.") }
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent("ges-edit-" + UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let file = directory.appendingPathComponent("edit.ges")
        try Data(draft.text[index].utf8).write(to: file)
        let arguments = (command + [file.path]).map { strdup($0) } + [nil]
        defer { for argument in arguments { free(argument) } }
        var exitCode: Int32 = 0
        let error = arguments.withUnsafeBufferPointer { ges_terminal_run_editor($0.baseAddress, &exitCode) }
        guard error == 0 else { throw ToolError.io("Could not start or wait for editor. Set GES_EDITOR to an installed editor command: " + String(cString: strerror(error))) }
        let bytes = try Data(contentsOf: file)
        guard let text = String(data: bytes, encoding: .utf8) else { throw ToolError.encoding(file.path) }
        draft.text[index] = text.trimmingPrefixBOM
        guard exitCode == 0 else { throw ToolError.usage("Editor exited with code \(exitCode). The returned draft was retained; it was not reloaded.") }
        if !draft.pending || draft.id == 0 && !draft.dirty && draft.applied == nil { return true }
        var path = "<scratch>"
        return try session.reload(activePath: &path, fromMemory: true)
    }

    func save(_ inventory: RunInventory, _ argument: String, io: ToolIO) throws {
        let words = try Self.words(argument)
        guard words.count <= 2 else { throw ToolError.usage("Use :save [program-ID|module] or :save 0 \"file.ges\".") }
        let selected = try words.isEmpty ? drafts.values.filter { $0.id != 0 && $0.dirty }.sorted { $0.id < $1.id } : [get(inventory, words[0])]
        var newPath: String?
        if words.count == 2 {
            guard selected[0].id == 0 else { throw ToolError.usage("A new save path is only supported for scratch 0.") }
            newPath = try ToolFiles.fullPath(words[1])
            guard newPath!.lowercased().hasSuffix(".ges") else { throw ToolError.usage("Save scratch to a .ges source file.") }
        }
        if selected.contains(where: { $0.id == 0 }), newPath == nil { throw ToolError.usage("Scratch 0 requires :save 0 \"file.ges\".") }
        var writes: [(Draft, Int, String, Data)] = []
        for draft in selected {
            for index in draft.text.indices {
                if draft.id != 0, GesText.scalarEqual(draft.text[index], String(decoding: draft.baseline[index], as: UTF8.self).trimmingPrefixBOM) { continue }
                let path = try newPath ?? ToolFiles.fullPath(draft.paths[index])
                guard !writes.contains(where: { $0.2 == path }) else { throw ToolError.usage("Several modified programs refer to the same file. Save them separately: " + path) }
                if draft.id == 0 {
                    guard !FileManager.default.fileExists(atPath: path) else { throw ToolError.usage("Save conflict: the file exists already: " + path) }
                } else {
                    guard (try? Data(contentsOf: URL(fileURLWithPath: path))) == draft.baseline[index] else { throw ToolError.usage("Save conflict: the file changed on disk: " + path) }
                }
                writes.append((draft, index, path, Data(draft.text[index].utf8)))
            }
        }
        for (draft, index, path, bytes) in writes {
            let url = URL(fileURLWithPath: path)
            try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            if draft.id == 0 { try bytes.write(to: url, options: .withoutOverwriting) } else { try bytes.write(to: url, options: .atomic) }
            draft.baseline[index] = bytes
        }
        if let newPath {
            let draft = selected[0]
            drafts.removeValue(forKey: 0)
            draft.paths = [newPath]
            draft.id = inventory.promoteScratch(newPath)
            drafts[draft.id] = draft
            io.line("Saved scratch as program \(draft.id): \(newPath)", toError: true)
        } else {
            io.line("Saved \(writes.count) source file(s).", toError: true)
        }
        if words.isEmpty, drafts[0]?.dirty == true { io.line("Scratch 0 is unsaved. Use :save 0 \"file.ges\".", toError: true) }
    }

    func canQuit(_ io: ToolIO) -> Bool {
        let dirty = drafts.values.filter(\.dirty).map(\.id).sorted()
        if dirty.isEmpty { return true }
        io.line("Unsaved changes in programs: " + dirty.map(String.init).joined(separator: ", ") + ". Use :save, or :quit! to discard and exit.", toError: true)
        if dirty.contains(0) { io.line("Scratch 0 requires :save 0 \"file.ges\".", toError: true) }
        return false
    }

    static func words(_ text: String) throws -> [String] {
        var result: [String] = []
        var word = ""
        var quote: Character?
        for c in text {
            if let q = quote {
                if c == q { quote = nil } else { word.append(c) }
            } else if c == "'" || c == "\"" {
                quote = c
            } else if c.isWhitespace {
                if !word.isEmpty {
                    result.append(word)
                    word = ""
                }
            } else {
                word.append(c)
            }
        }
        guard quote == nil else { throw ToolError.usage("Unterminated quoted argument.") }
        if !word.isEmpty { result.append(word) }
        return result
    }
}

extension String {
    fileprivate var trimmingPrefixBOM: String { hasPrefix("\u{feff}") ? String(dropFirst()) : self }
}
