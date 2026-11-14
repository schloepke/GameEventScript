// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// One top-level declaration of generated code and the declaration it belongs to. An empty owner is the shared part, such
// as the embedded schema sources; an outside unit stands after the namespace, at file level.
struct GesCodeUnit {
    var owner: String
    var write: (inout GesSchemaPlanner.Code) throws -> Void
    var outside = false
}

// The declarations of generated code with what surrounds them: the header of the single file, the lines before the
// namespace and the namespace itself.
struct GesCodeDocument {
    var header = ""
    var prologue = ""
    var space: [String] = []
    var swift = false
    // How many leading Swift namespace components another generation of the module declares, which this one extends.
    var extended = 0
    // The Swift access keyword with its space, which declares the namespace enums.
    var access = ""
    // What the namespace holds, "schema" or "model", for the documentation of Swift namespace enums.
    var holds = ""
    // Whether the code depends on parameters, which the header of split files mentions.
    var parameters = false
    var units: [GesCodeUnit] = []
}

// Assembles generated declarations into one file or, split, into a file per declaration or per schema source with the
// shared part in its own file. The single file keeps one blank line between declarations, as split files do.
enum GesCodeFiles {
    static let manifest = ".ges-generated.json"

    static func single(_ document: GesCodeDocument) throws -> String {
        var code = GesSchemaPlanner.Code()
        code.raw(document.header)
        code.raw(document.prologue)
        if document.swift {
            openSwiftNamespace(&code, document)
        } else if !document.space.isEmpty {
            code.line("namespace " + document.space.joined(separator: "."))
            code.open("{")
        }
        var first = true
        for unit in document.units where !unit.outside {
            if !first { code.blank() }
            first = false
            code.raw(try render(unit, code.depth))
        }
        if document.swift {
            for _ in (document.extended > 0 ? document.extended - 1 : 0)..<document.space.count { code.close() }
        } else if !document.space.isEmpty {
            code.close()
        }
        try outside(&code, document.units)
        return code.text
    }

    // Opens the Swift namespace: an extension of the components another generation declares, then an enum per component.
    private static func openSwiftNamespace(_ code: inout GesSchemaPlanner.Code, _ document: GesCodeDocument) {
        if document.extended > 0 { code.open("extension \(document.space[0..<document.extended].joined(separator: ".")) {") }
        for part in document.space[document.extended...] {
            code.line("/// The namespace `\(part)` of generated \(document.holds) types.")
            code.open("\(document.access)enum \(part) {")
        }
    }

    // The files of split code, ordered by name, and the manifest last.
    static func split(_ document: GesCodeDocument, _ split: GesSchemaSplit, sourceOf: (String) -> String, manifest: ([String]) -> String) throws -> [GesGeneratedFile] {
        if split == .none { throw GesGeneratorError(GesGeneratorError.usage, "Split output needs a split by schema or by file.") }
        let fileExtension = document.swift ? ".swift" : ".g.cs"
        // Swift requires distinct file names within a module, so names carry the namespace: wire and model code of one target
        // live in different namespaces.
        let space = document.space.joined(separator: ".")
        let prefix = space.isEmpty ? "" : space + "."
        let shared = "_" + (space.isEmpty ? "Generated" : space) + fileExtension

        // Groups by the declaration or the full source name, so that two sources of one file name collide.
        var groups: [(name: String, units: [GesCodeUnit])] = []
        var keys: [String: Int] = [:]
        var sharedUnits: [GesCodeUnit] = []
        for unit in document.units {
            if unit.owner.isEmpty {
                sharedUnits.append(unit)
                continue
            }
            let key = split == .schema ? unit.owner : sourceOf(unit.owner)
            let index: Int
            if let found = keys[key] {
                index = found
            } else {
                index = groups.count
                keys[key] = index
                groups.append((prefix + (split == .schema ? unit.owner : fileName(key)) + fileExtension, []))
            }
            groups[index].units.append(unit)
        }

        var files: [GesGeneratedFile] = []
        if let sharedText = try sharedText(document, sharedUnits) { files.append(GesGeneratedFile(shared, sharedText)) }
        for group in groups { files.append(GesGeneratedFile(group.name, try fileText(document, group.units))) }
        var folded: [String: String] = [:]
        for file in files {
            if let other = folded[fold(file.path)] {
                throw GesGeneratorError(GesGeneratorError.nameCollision, "The generated files '\(other)' and '\(file.path)' differ only in case or come from sources of one name.")
            }
            folded[fold(file.path)] = file.path
        }
        files.sort { $0.path.utf16.lexicographicallyPrecedes($1.path.utf16) }
        files.append(GesGeneratedFile(Self.manifest, manifest(files.map(\.path))))
        return files
    }

    // The header of a split file, which leaves the roots, parameters and hashes to the manifest so that a change touches
    // only the files of the declarations it changes.
    private static func header(_ document: GesCodeDocument) -> String {
        "// <auto-generated>\n"
            + "//     Generated by `ges schema generate`; \(manifest) in this directory names the roots, parameters and hashes.\n"
            + "//     Do not edit it; generate it again when a schema\(document.parameters ? " or a parameter" : "") changes.\n"
            + "// </auto-generated>\n"
    }

    private static func fileText(_ document: GesCodeDocument, _ units: [GesCodeUnit]) throws -> String {
        var code = GesSchemaPlanner.Code()
        code.raw(header(document))
        code.raw(document.prologue)
        // A file of units outside the namespace alone, such as the conversion of a shared packed root, opens no namespace.
        let space = !document.space.isEmpty && units.contains { !$0.outside }
        if space && document.swift {
            code.open("extension \(document.space.joined(separator: ".")) {")
        } else if space {
            code.line("namespace " + document.space.joined(separator: "."))
            code.open("{")
        }
        // Units outside the namespace, such as extensions of model types, follow it at file scope.
        var first = true
        for unit in units where !unit.outside {
            if !first { code.blank() }
            first = false
            code.raw(try render(unit, code.depth))
        }
        if space { code.close() }
        try outside(&code, units)
        return code.text
    }

    // The namespace and the shared declarations; nil when there is neither.
    private static func sharedText(_ document: GesCodeDocument, _ units: [GesCodeUnit]) throws -> String? {
        let inside = units.filter { !$0.outside }
        let outside = units.filter(\.outside)
        let declares = document.swift && document.space.count > document.extended
        if inside.isEmpty && outside.isEmpty && !declares { return nil }
        var code = GesSchemaPlanner.Code()
        code.raw(header(document))
        code.raw(document.prologue)
        var opened = 0
        if document.swift && (document.extended < document.space.count || !inside.isEmpty) && document.extended > 0 {
            code.open("extension \(document.space[0..<document.extended].joined(separator: ".")) {")
            opened += 1
        }
        if document.swift {
            for (index, part) in document.space.enumerated() where index >= document.extended {
                code.line("/// The namespace `\(part)` of generated \(document.holds) types.")
                if index + 1 < document.space.count || !inside.isEmpty {
                    code.open("\(document.access)enum \(part) {")
                    opened += 1
                } else {
                    code.line("\(document.access)enum \(part) {}")
                }
            }
        } else if !document.space.isEmpty && !inside.isEmpty {
            code.line("namespace " + document.space.joined(separator: "."))
            code.open("{")
            opened += 1
        }
        for (index, unit) in inside.enumerated() {
            if index > 0 { code.blank() }
            code.raw(try render(unit, code.depth))
        }
        for _ in 0..<opened { code.close() }
        try self.outside(&code, outside)
        return code.text
    }

    private static func outside(_ code: inout GesSchemaPlanner.Code, _ units: [GesCodeUnit]) throws {
        for unit in units where unit.outside {
            code.blank()
            code.raw(try render(unit, 0))
        }
    }

    // A declaration written at the depth of its place, without the blank lines around it.
    private static func render(_ unit: GesCodeUnit, _ depth: Int) throws -> String {
        var code = GesSchemaPlanner.Code(depth: depth)
        try unit.write(&code)
        var text = Substring(code.text)
        while text.first == "\n" { text.removeFirst() }
        while text.count > 1 && text.hasSuffix("\n\n") { text.removeLast() }
        return String(text)
    }

    // The file name of a schema source without its directories and without the extension .gess.
    private static func fileName(_ source: String) -> String {
        let name = source.split(separator: "/", omittingEmptySubsequences: false).last!.split(separator: "\\", omittingEmptySubsequences: false).last!
        return name.hasSuffix(".gess") && name.count > 5 ? String(name.dropLast(5)) : String(name)
    }

    // ASCII letters folded to lowercase, as case-insensitive file systems compare names.
    private static func fold(_ name: String) -> String {
        String(String.UnicodeScalarView(name.unicodeScalars.map { $0.value >= 65 && $0.value <= 90 ? Unicode.Scalar($0.value + 32)! : $0 }))
    }
}
