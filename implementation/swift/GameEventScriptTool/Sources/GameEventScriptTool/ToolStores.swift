// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

// The stores of `document check --store @Type root.gesd`. Each option adds a root to the store of its type; the
// root is named by the path as given and its files are read relative to the root file's directory. Stores are read in
// the order their types first appear, each with every earlier store as a dependency, so a store comes after the
// stores it pins (specs/Documents.md#reading-a-store).
enum ToolStores {
    static let option = "--store"

    // Reads every store and reports each error as `path(line,column): error code: message`; returns the number of
    // stores that failed. A root given twice for one type is a usage error.
    static func check(_ options: [(type: String, path: String)], _ schemas: ToolDocumentSchemas, quiet: Bool, _ io: ToolIO) throws -> Int {
        var order: [String] = []
        var roots: [[UInt8]: [GesStoreRoot]] = [:]
        for (type, path) in options {
            let key = keyOf(type)
            if roots[key] == nil {
                roots[key] = []
                order.append(type)
            }
            if roots[key]!.contains(where: { $0.name.utf8.elementsEqual(path.utf8) }) { throw ToolError.usage("The root '\(path)' of @\(type) is given twice.") }
            roots[key]!.append(GesStoreRoot(name: path, path: fileName(path)))
        }

        var read: [GesStore] = []
        var failed = 0
        for name in order {
            let typeRoots = roots[keyOf(name)]!
            guard let type = schemas.handleType(name) else {
                io.line("\(typeRoots[0].name): error schema.unknownName: The schemas of --schema declare no handle type @\(name).", toError: true)
                failed += 1
                continue
            }
            do {
                let store = try GesStore.read(type, roots: typeRoots, files: Files(), dependencies: read)
                read.append(store)
                if !quiet { io.line("Checked store @\(name): \(count(store.entries.count)).") }
            } catch let error as GesStoreError {
                for diagnostic in error.diagnostics { io.line("\(place(diagnostic.location)): error \(diagnostic.code): \(diagnostic.message)", toError: true) }
                failed += 1
            } catch GesApiError.invalidArgument(let message) {
                // The parameter suffix matches the C# CLI, whose argument errors name the type parameter.
                io.line("\(typeRoots[0].name): error cli.usage: \(message) (Parameter 'type')", toError: true)
                failed += 1
            }
        }
        return failed
    }

    // Type names compare by their UTF-8 bytes, as the C# CLI compares them ordinally.
    private static func keyOf(_ type: String) -> [UInt8] { Array(type.utf8) }

    private static func count(_ entries: Int) -> String { entries == 1 ? "1 entry" : "\(entries) entries" }

    // A location as a file path: the root's directory joined with the root-relative path.
    private static func place(_ location: GesSourceLocation) -> String {
        let path = join(location.moduleName, location.sourceName)
        guard let line = location.line, line > 0 else { return path }
        return "\(path)(\(line),\(location.column.map(String.init) ?? ""))"
    }

    private static func fileName(_ path: String) -> String {
        guard let slash = path.lastIndex(of: "/") else { return path }
        return String(path[path.index(after: slash)...])
    }

    private static func join(_ root: String, _ path: String) -> String {
        guard let slash = root.lastIndex(of: "/") else { return path }
        let directory = slash == root.startIndex ? "/" : String(root[..<slash])
        return directory.hasSuffix("/") ? directory + path : directory + "/" + path
    }

    // Reads a root-relative path below the directory of the root file; a missing, unreadable or invalid UTF-8 file is
    // absent.
    private struct Files: GesStoreFiles {
        func read(root: String, path: String) -> String? { try? ToolFiles.source(join(root, path)) }
    }
}
