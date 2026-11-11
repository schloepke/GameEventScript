// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptGenerator

/// A directory that `schema generate --out-dir` owns. Writing replaces changed files, removes the files that the previous
/// manifest names and that are no longer generated, and leaves other files alone with a warning. Hidden files and Unity's
/// .meta files are never reported, so temporary files and engine metadata can live beside the code.
struct ToolGeneratedDirectory {
    let io: ToolIO

    func write(_ directory: String, _ files: [GesGeneratedFile]) throws -> Int {
        let root = try ToolFiles.fullPath(directory)
        let owned = Self.owned(Self.join(root, files[files.count - 1].path))
        try FileManager.default.createDirectory(atPath: root, withIntermediateDirectories: true)
        var generated: Set<String> = []
        for file in files {
            generated.insert(file.path)
            let path = Self.join(root, file.path)
            let bytes = Data(file.text.utf8)
            // An unchanged file keeps its time stamp, so that builds do not compile it again.
            if FileManager.default.contents(atPath: path) == bytes { continue }
            try ToolFiles.write(bytes, to: path)
        }
        for name in try Self.entries(root) where !generated.contains(name) {
            let path = Self.join(root, name)
            var isDirectory: ObjCBool = false
            if owned.contains(name) && FileManager.default.fileExists(atPath: path, isDirectory: &isDirectory) && !isDirectory.boolValue {
                try FileManager.default.removeItem(atPath: path)
            } else {
                io.line("warning cli.unowned: \(Self.join(directory, name)) is not generated and was left alone.", toError: true)
            }
        }
        return 0
    }

    func check(_ directory: String, _ files: [GesGeneratedFile]) throws -> Int {
        let root = try ToolFiles.fullPath(directory)
        var generated: Set<String> = []
        var failed = false

        func report(_ message: String) {
            io.line("error cli.outdated: " + message, toError: true)
            failed = true
        }

        for file in files {
            generated.insert(file.path)
            let path = Self.join(root, file.path)
            var isDirectory: ObjCBool = false
            if !FileManager.default.fileExists(atPath: path, isDirectory: &isDirectory) || isDirectory.boolValue {
                report("\(Self.join(directory, file.path)) does not exist.")
            } else if FileManager.default.contents(atPath: path) != Data(file.text.utf8) {
                report("\(Self.join(directory, file.path)) differs from the generated code.")
            }
        }
        for name in try Self.entries(root) where !generated.contains(name) { report("\(Self.join(directory, name)) is not generated.") }
        return failed ? 1 : 0
    }

    private static func join(_ directory: String, _ name: String) -> String { (directory as NSString).appendingPathComponent(name) }

    // The entries of the directory that a report names, ordered by name: neither hidden nor Unity metadata.
    private static func entries(_ root: String) throws -> [String] {
        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: root, isDirectory: &isDirectory), isDirectory.boolValue else { return [] }
        return try FileManager.default.contentsOfDirectory(atPath: root)
            .filter { !$0.hasPrefix(".") && !$0.hasSuffix(".meta") }
            .sorted { $0.utf16.lexicographicallyPrecedes($1.utf16) }
    }

    // The plain file names that a previous manifest lists; none when it is missing or unreadable, so that nothing is removed.
    private static func owned(_ manifest: String) -> Set<String> {
        guard let data = FileManager.default.contents(atPath: manifest),
            let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
            let files = object["files"] as? [Any]
        else { return [] }
        var owned: Set<String> = []
        for case let name as String in files where !name.isEmpty && name != "." && name != ".." && !name.contains("/") && !name.contains("\\") {
            owned.insert(name)
        }
        return owned
    }
}
