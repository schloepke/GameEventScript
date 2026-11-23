// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Generator) import GameEventScriptRuntime

// The schema set of a generation: the type that schemaSet declares, or the one that useSchemaSet names. Conversions, network
// bindings and document readers call it in place of the private helpers of the file.
struct GesSchemaSetReference {
    typealias Code = GesSchemaPlanner.Code

    // The declared name, or nil where another generation declares the type.
    let declared: String?

    // The name that generated code calls it by: C# with global::, Swift as given.
    let qualified: String

    // The members of the generated C# class.
    private static let csharpMembers: Set<String> = ["__set", "Hash", "Set", "Parameters", "Bind", "BindNetwork", "ReadDocument"]

    // Checks the options and gives the schema set of the generation, or nil without one.
    static func from(_ options: GesSchemaGenerateOptions) throws(GesGeneratorError) -> GesSchemaSetReference? {
        let swift = options.language == .swift
        let keywords = swift ? GesSwiftEmitter.keywords : GesCSharpEmitter.keywords
        if !swift && !options.imports.isEmpty {
            throw GesGeneratorError(GesGeneratorError.usage, "Imports concern Swift, whose generated files import modules; C# names types with qualified names.")
        }
        for module in options.imports where !identifier(module, keywords) {
            throw GesGeneratorError(GesGeneratorError.usage, "The module '\(module)' is no identifier.")
        }
        if options.schemaSet != nil && options.useSchemaSet != nil {
            throw GesGeneratorError(GesGeneratorError.usage, "A generation either declares a schema set or uses the one of another generation, not both.")
        }
        if let name = options.schemaSet {
            if !identifier(name, keywords) { throw GesGeneratorError(GesGeneratorError.usage, "The schema set name '\(name)' is no identifier.") }
            // C# names no member like its type; Swift allows it.
            if !swift && csharpMembers.contains(name) {
                throw GesGeneratorError(GesGeneratorError.nameCollision, "The schema set name '\(name)' is the name of one of its members.")
            }
            let space = try GesSchemaPlanner.namespace(options.namespace, keywords)
            return GesSchemaSetReference(declared: name, qualified: swift ? name : "global::" + space.map { $0 + "." }.joined() + name)
        }
        guard let used = options.useSchemaSet else { return nil }
        for part in used.split(separator: ".", omittingEmptySubsequences: false) where !identifier(String(part), keywords) {
            throw GesGeneratorError(GesGeneratorError.usage, "The schema set name '\(used)' is no sequence of identifiers.")
        }
        return GesSchemaSetReference(declared: nil, qualified: swift ? used : "global::" + used)
    }

    // Adds the declared name to the top-level names of the generation, which no other type may take.
    func reserve(_ names: inout Set<String>) throws(GesGeneratorError) {
        if let name = declared, !names.insert(name).inserted {
            throw GesGeneratorError(GesGeneratorError.nameCollision, "The schema set takes the name '\(name)', which another type of the generation has.")
        }
    }

    private static func identifier(_ text: String, _ keywords: Set<String>) -> Bool {
        func letter(_ byte: UInt8) -> Bool { (byte >= 0x61 && byte <= 0x7A) || (byte >= 0x41 && byte <= 0x5A) || byte == UInt8(ascii: "_") }

        guard let first = text.utf8.first, letter(first), !keywords.contains(text) else { return false }
        return text.utf8.allSatisfy { letter($0) || ($0 >= 0x30 && $0 <= 0x39) }
    }

    // FNV-1a 64 over the UTF-8 bytes of each source's name and text and each parameter's name and literal, each followed by a
    // zero byte, in the order of the generation.
    static func hash(_ sources: [GesSchemaSource], _ parameters: [GesLayoutParameter]) -> UInt64 {
        var hash: UInt64 = 14_695_981_039_346_656_037

        func add(_ text: String) {
            for value in text.utf8 { hash = (hash ^ UInt64(value)) &* 1_099_511_628_211 }
            hash = hash &* 1_099_511_628_211
        }

        for source in sources {
            add(source.name)
            add(source.text)
        }
        for parameter in parameters {
            add(parameter.name)
            add(parameter.literal)
        }
        return hash
    }

    static func writeCSharp(_ code: inout Code, _ name: String, _ access: String, _ sources: [GesSchemaSource], _ parameters: [GesLayoutParameter], networks: Bool) {
        code.blank()
        code.line("/// <summary>")
        code.line("/// The schema set of the generated code: the embedded schema sources, parsed once, the parameter values of the generation")
        code.line("/// and the binding of schemas" + (networks ? ", networks" : "") + " and documents against them, which other generations share.")
        code.line("/// </summary>")
        code.line("\(access) static class \(name)")
        code.open("{")
        code.line("private static GesSchemaSet? __set;")
        code.blank()
        code.line("/// <summary>Gets the FNV-1a 64 hash over the embedded sources and the parameter values.</summary>")
        code.line("public static ulong Hash => 0x\(GesSchemaLayoutJson.hex(hash(sources, parameters)))UL;")
        code.blank()
        code.line("/// <summary>Gets the embedded schema sources, parsed on first access.</summary>")
        code.line("/// <exception cref=\"GesSchemaException\">The runtime no longer parses the sources.</exception>")
        code.line("public static GesSchemaSet Set => __set ??= GesSchemaSet.Parse(")
        code.indent()
        for (index, source) in sources.enumerated() {
            code.line("new GesSchemaSource(\(GesSchemaPlanner.literal(source.name, swift: false)), string.Join(\"\\n\", new[]")
            code.open("{")
            for line in GesSchemaPlanner.lines(source.text) { code.line(GesSchemaPlanner.literal(line, swift: false) + ",") }
            code.close(index + 1 < sources.count ? "}))," : "})));")
        }
        code.dedent()
        code.blank()
        code.line("/// <summary>Gets the parameter values of the generation, a new instance on each access.</summary>")
        var values = "new GesSchemaParameters()"
        for parameter in parameters {
            values += ".With(\(GesSchemaPlanner.literal(parameter.name, swift: false)), \(GesSchemaPlanner.literal(parameter.literal, swift: false)))"
        }
        code.line("public static GesSchemaParameters Parameters => \(values);")
        code.blank()
        code.line("/// <summary>Binds a schema with the parameter values and checks that it binds like the generated code.</summary>")
        code.line("/// <param name=\"root\">The schema, message or message set.</param>")
        code.line("/// <param name=\"bindingHash\">The binding hash of the generated code.</param>")
        code.line("/// <returns>The bound schema.</returns>")
        code.line("/// <exception cref=\"GesSchemaException\">The sources no longer parse, or the root is not declared or no longer binds.</exception>")
        code.line("/// <exception cref=\"global::System.InvalidOperationException\">The binding hash differs from <paramref name=\"bindingHash\"/>.</exception>")
        code.line("public static GesBoundSchema Bind(string root, ulong bindingHash)")
        code.open("{")
        code.line("var bound = Set.Schema(root).Bind(Parameters);")
        code.line("if (bound.BindingHash != bindingHash) throw new global::System.InvalidOperationException($\"The embedded schema sources no longer bind '{root}' like the generated code.\");")
        code.line("return bound;")
        code.close()
        code.blank()
        code.line("/// <summary>Binds a network with the parameter values and checks that it binds like the generated code.</summary>")
        code.line("/// <param name=\"name\">The network.</param>")
        code.line("/// <param name=\"hash\">The network hash of the generated code.</param>")
        code.line("/// <returns>The bound network.</returns>")
        code.line("/// <exception cref=\"GesSchemaException\">The sources no longer parse, or the network is not declared or no longer binds.</exception>")
        code.line("/// <exception cref=\"global::System.InvalidOperationException\">The network hash differs from <paramref name=\"hash\"/>.</exception>")
        code.line("public static GesBoundNetwork BindNetwork(string name, ulong hash)")
        code.open("{")
        code.line("var bound = Set.Network(name).Bind(Parameters);")
        code.line("if (bound.Hash != hash) throw new global::System.InvalidOperationException($\"The embedded schema sources no longer bind the network '{name}' like the generated code.\");")
        code.line("return bound;")
        code.close()
        code.blank()
        code.line("/// <summary>Reads GES document text and checks it against the embedded schema sources.</summary>")
        code.line("/// <param name=\"text\">The document text.</param>")
        code.line("/// <param name=\"roots\">The schemas the header may name.</param>")
        code.line("/// <returns>The checked document.</returns>")
        code.line("/// <exception cref=\"GesDocumentException\">The text is no document, names another schema or breaks its schema.</exception>")
        code.line("/// <exception cref=\"GesSchemaException\">The runtime no longer parses the sources.</exception>")
        code.line("public static GesSchemaDocument ReadDocument(string text, params string[] roots) => GesSchemaDocument.Read(Set, text, roots);")
        code.close()
    }

    static func writeSwift(_ code: inout Code, _ name: String, _ access: String, _ sources: [GesSchemaSource], _ parameters: [GesLayoutParameter], networks: Bool) {
        code.blank()
        code.line("/// The schema set of the generated code: the embedded schema sources, parsed once, the parameter values of the generation")
        code.line("/// and the binding of schemas" + (networks ? ", networks" : "") + " and documents against them, which other generations share.")
        code.open("\(access)enum \(name) {")
        code.line("/// A schema or network that no longer binds like the generated code.")
        code.open("\(access)struct Mismatch: Error, Sendable {")
        code.line("/// The schema or network.")
        code.line("\(access)let name: String")
        code.close()
        code.blank()
        code.line("/// The FNV-1a 64 hash over the embedded sources and the parameter values.")
        code.line("\(access)static let hash: UInt64 = 0x\(GesSchemaLayoutJson.hex(hash(sources, parameters)))")
        code.blank()
        code.open("private static let __set = Result<GesSchemaSet, any Error> {")
        code.open("try GesSchemaSet(sources: [")
        for source in sources {
            code.open("GesSchemaSource(name: \(GesSchemaPlanner.literal(source.name, swift: true)), text: [")
            for line in GesSchemaPlanner.lines(source.text) { code.line(GesSchemaPlanner.literal(line, swift: true) + ",") }
            code.close("].joined(separator: \"\\n\")),")
        }
        code.close("])")
        code.close()
        code.blank()
        code.line("/// The embedded schema sources, parsed on first access.")
        code.line("/// - Throws: `GesSchemaError` when the runtime no longer parses the sources.")
        code.open("\(access)static var set: GesSchemaSet {")
        code.line("get throws { try __set.get() }")
        code.close()
        code.blank()
        code.line("/// The parameter values of the generation, a new instance on each call.")
        code.line("/// - Throws: `GesSchemaError` when the runtime no longer reads a value.")
        code.open("\(access)static func parameters() throws -> GesSchemaParameters {")
        code.line(parameters.isEmpty ? "GesSchemaParameters()" : "let parameters = GesSchemaParameters()")
        for parameter in parameters {
            code.line("try parameters.with(\(GesSchemaPlanner.literal(parameter.name, swift: true)), literal: \(GesSchemaPlanner.literal(parameter.literal, swift: true)))")
        }
        if !parameters.isEmpty { code.line("return parameters") }
        code.close()
        code.blank()
        code.line("/// Binds a schema with the parameter values and checks that it binds like the generated code.")
        code.line("/// - Parameters:")
        code.line("///   - root: The schema, message or message set.")
        code.line("///   - bindingHash: The binding hash of the generated code.")
        code.line("/// - Returns: The bound schema.")
        code.line("/// - Throws: `GesSchemaError` when the sources no longer parse or the root is not declared or no longer binds, and")
        code.line("///   `Mismatch` when the binding hash differs.")
        code.open("\(access)static func bind(_ root: String, bindingHash: UInt64) throws -> GesBoundSchema {")
        code.line("let bound = try set.schema(root).bind(parameters: parameters())")
        code.line("if bound.bindingHash != bindingHash { throw Mismatch(name: root) }")
        code.line("return bound")
        code.close()
        code.blank()
        code.line("/// Binds a network with the parameter values and checks that it binds like the generated code.")
        code.line("/// - Parameters:")
        code.line("///   - name: The network.")
        code.line("///   - hash: The network hash of the generated code.")
        code.line("/// - Returns: The bound network.")
        code.line("/// - Throws: `GesSchemaError` when the sources no longer parse or the network is not declared or no longer binds,")
        code.line("///   and `Mismatch` when the network hash differs.")
        code.open("\(access)static func bindNetwork(_ name: String, hash: UInt64) throws -> GesBoundNetwork {")
        code.line("let bound = try set.network(name).bind(parameters: parameters())")
        code.line("if bound.hash != hash { throw Mismatch(name: name) }")
        code.line("return bound")
        code.close()
        code.blank()
        code.line("/// Reads GES document text and checks it against the embedded schema sources.")
        code.line("/// - Parameters:")
        code.line("///   - text: The document text.")
        code.line("///   - roots: The schemas the header may name.")
        code.line("/// - Returns: The checked document.")
        code.line("/// - Throws: `GesDocumentError` when the text is no document, names another schema or breaks its schema, and")
        code.line("///   `document.schemaMismatch` when the runtime no longer parses the sources.")
        code.open("\(access)static func readDocument(_ text: String, roots: [String]) throws(GesDocumentError) -> GesSchemaDocument {")
        code.open("guard let set = try? __set.get() else {")
        code.line("throw GesDocumentError(code: GesSchemaDocument.schemaMismatch, line: 1, column: 1, message: \"The embedded schema sources no longer parse.\")")
        code.close()
        code.line("return try GesSchemaDocument(text, schemas: set, expected: roots)")
        code.close()
        code.close()
    }
}
