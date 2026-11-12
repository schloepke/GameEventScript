// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Generator) import GameEventScriptRuntime

/// Generates code and layouts from schemas at build time. Every type reached from the roots is bound once with the given
/// parameters, so a type that several roots share appears once. The generator belongs to build tools and is never part of
/// a game.
public enum GesSchemaGenerator {
    /// Writes the language-neutral layout of the roots as canonical JSON, the input of every code emitter. Every port writes
    /// identical text for identical schemas and parameters.
    /// - Parameters:
    ///   - set: The schemas.
    ///   - roots: The schemas, messages or message sets to generate, in the order the output names them.
    ///   - parameters: A value for every parameter the roots reach.
    /// - Returns: The JSON text, ending with a line break.
    /// - Throws: `GesGeneratorError` with `generate.usage` when no root is given, and `GesSchemaError` when a root is not
    ///   declared or cannot be bound, for example because a parameter has no value.
    public static func layout(_ set: GesSchemaSet, roots: [String], parameters: GesSchemaParameters) throws -> String {
        GesSchemaLayoutJson.write(try GesSchemaLayoutBuilder.build(set, roots: roots, parameters: parameters))
    }

    /// Generates code for the roots and every type they reach: a type per schema that writes and reads its records with the
    /// bytes, error codes and paths of `GesSchemaWriter` and `GesSchemaReader`. Every port writes identical text for
    /// identical schemas, parameters and options.
    /// - Parameters:
    ///   - set: The schemas.
    ///   - roots: The schemas to generate, in the order the header names them.
    ///   - parameters: A value for every parameter the roots reach; they become constants of the code.
    ///   - options: The language, namespace and visibility.
    /// - Returns: The source text, ending with a line break.
    /// - Throws: `GesSchemaError` when a root is not declared or cannot be bound, and `GesGeneratorError` when no root is
    ///   given, the namespace is invalid, two generated names collide, or a reached type cannot be generated yet.
    public static func generate(_ set: GesSchemaSet, roots: [String], parameters: GesSchemaParameters, options: GesSchemaGenerateOptions) throws -> String {
        if options.split != .none { throw GesGeneratorError(GesGeneratorError.usage, "Split code consists of several files; generateFiles returns them.") }
        return try GesCodeFiles.single(try wire(set, roots, parameters, options).document)
    }

    /// Generates the code of `generate` as several files: one per declaration or per schema source as `options.split`
    /// chooses, a shared file whose name begins with `_` for the namespace and the embedded schema sources, and last the
    /// manifest `.ges-generated.json`, which names the options, roots, parameters, hashes and files. Nested types stay in
    /// the file of their record. Every port writes identical files.
    /// - Parameters:
    ///   - set: The schemas.
    ///   - roots: The schemas to generate, in the order the manifest names them.
    ///   - parameters: A value for every parameter the roots reach; they become constants of the code.
    ///   - options: The language, namespace, visibility and split.
    /// - Returns: The files ordered by name, and the manifest last.
    /// - Throws: `GesSchemaError` when a root is not declared or cannot be bound, and `GesGeneratorError` when no root or no
    ///   split is given, the namespace is invalid, two generated names or two file names that differ only in ASCII case
    ///   collide, or a reached type cannot be generated yet.
    public static func generateFiles(_ set: GesSchemaSet, roots: [String], parameters: GesSchemaParameters, options: GesSchemaGenerateOptions) throws -> [GesGeneratedFile] {
        let (document, layout) = try wire(set, roots, parameters, options)
        return try GesCodeFiles.split(document, options.split, sourceOf: set.sourceName(of:)) { files in
            manifest(options, model: false, layout.roots, { root in (layout.type(root).hash, layout.type(root).bindingHash) }, layout.parameters, files)
        }
    }

    private static func wire(_ set: GesSchemaSet, _ roots: [String], _ parameters: GesSchemaParameters, _ options: GesSchemaGenerateOptions) throws -> (document: GesCodeDocument, layout: GesSchemaLayout) {
        if options.bindings { throw GesGeneratorError(GesGeneratorError.usage, "Bindings concern model code; wire code converts through Values.") }
        if options.documents { throw GesGeneratorError(GesGeneratorError.usage, "Documents concern model code; wire code converts through Values.") }
        let layout = try GesSchemaLayoutBuilder.build(set, roots: roots, parameters: parameters)
        let types = try GesSchemaPlanner.plan(layout)
        let document = options.language == .csharp ? try GesCSharpEmitter.emit(layout, types, options) : try GesSwiftEmitter.emit(layout, types, options)
        return (document, layout)
    }

    /// Lists the wire roots of a group: the declarations marked `wire(group)`, or `wire` for the group `default`, in
    /// declaration order; empty when the group has no root.
    public static func wireRoots(_ set: GesSchemaSet, group: String) -> [String] {
        set.schemas.filter { $0.wireGroup == group }.map(\.name)
    }

    /// Lists the model roots of a group: the schemas marked `model(group)`, or `model` for the group `default`, in
    /// declaration order; empty when the group has no root.
    public static func modelRoots(_ set: GesSchemaSet, group: String) -> [String] {
        set.schemas.filter { $0.modelGroup == group }.map(\.name)
    }

    /// Writes the language-neutral model layout of the roots as canonical JSON: every schema they reach, described without
    /// binding, since models are never encoded. Every port writes identical text.
    /// - Parameters:
    ///   - set: The schemas.
    ///   - roots: The schemas to generate as models, in the order the output names them.
    /// - Returns: The JSON text, ending with a line break.
    /// - Throws: `GesSchemaError` when a root is not declared, and `GesGeneratorError` when no root is given, a root is no
    ///   schema, or a reached type has no model type.
    public static func modelLayout(_ set: GesSchemaSet, roots: [String]) throws -> String {
        GesModelLayoutJson.write(try GesModelLayoutBuilder.build(set, roots: roots))
    }

    /// Generates model types for the roots and every schema they reach: plain values with value equality that are never
    /// encoded, so they need no ranges, steps or parameter values and may use document-only types. Every port writes
    /// identical text for identical schemas and options.
    /// - Parameters:
    ///   - set: The schemas.
    ///   - roots: The schemas to generate as models, in the order the header names them.
    ///   - options: The language, namespace and visibility; `values` adds plain GES values, and `checkedExclusivity` concerns
    ///     wire code and must stay off.
    /// - Returns: The source text, ending with a line break.
    /// - Throws: `GesSchemaError` when a root is not declared, and `GesGeneratorError` when no root is given, a root is no
    ///   schema, an option concerns wire code, the namespace is invalid, two generated names collide, or a reached type has
    ///   no model type.
    public static func generateModels(_ set: GesSchemaSet, roots: [String], options: GesSchemaGenerateOptions) throws -> String {
        if options.split != .none { throw GesGeneratorError(GesGeneratorError.usage, "Split code consists of several files; generateModelFiles returns them.") }
        return try GesCodeFiles.single(try models(set, roots, options).document)
    }

    /// Generates the model types of `generateModels` as several files: one per declaration or per schema source as
    /// `options.split` chooses, with the protocol of a schema in its file, a shared file whose name begins with `_` for the
    /// namespace, the conversions and the helpers, and last the manifest `.ges-generated.json`. Every port writes identical
    /// files.
    /// - Parameters:
    ///   - set: The schemas.
    ///   - roots: The schemas to generate as models, in the order the manifest names them.
    ///   - options: The language, namespace, visibility, conversions and split.
    /// - Returns: The files ordered by name, and the manifest last.
    /// - Throws: `GesSchemaError` when a root is not declared, and `GesGeneratorError` when no root or no split is given, a
    ///   root is no schema, an option concerns wire code, the namespace is invalid, two generated names or two file names
    ///   that differ only in ASCII case collide, or a reached type has no model type.
    public static func generateModelFiles(_ set: GesSchemaSet, roots: [String], options: GesSchemaGenerateOptions) throws -> [GesGeneratedFile] {
        let (document, layout) = try models(set, roots, options)
        return try GesCodeFiles.split(document, options.split, sourceOf: set.sourceName(of:)) { files in
            manifest(options, model: true, layout.roots, { root in (layout.type(root).hash, nil) }, [], files)
        }
    }

    private static func models(_ set: GesSchemaSet, _ roots: [String], _ options: GesSchemaGenerateOptions) throws -> (document: GesCodeDocument, layout: GesModelLayout) {
        if options.checkedExclusivity { throw GesGeneratorError(GesGeneratorError.usage, "Checked exclusivity concerns wire code, not models.") }
        let layout = try GesModelLayoutBuilder.build(set, roots: roots)
        // Reading documents converts through the plain values.
        var effective = options
        if options.documents { effective.values = true }
        let document = effective.language == .csharp ? try GesCSharpModelEmitter.emit(layout, effective) : try GesSwiftModelEmitter.emit(layout, effective)
        return (document, layout)
    }

    /// The manifest of split output: the options, the roots with their hashes, the parameters and the files, so that a tool
    /// knows which files of the directory it owns.
    private static func manifest(
        _ options: GesSchemaGenerateOptions,
        model: Bool,
        _ roots: [String],
        _ hashes: (String) -> (UInt64, UInt64?),
        _ parameters: [GesLayoutParameter],
        _ files: [String]
    ) -> String {
        var json = GesSchemaLayoutJson.Writer()
        json.beginObject()
        json.number("manifest", 1)
        json.string("code", model ? "model" : "wire")
        json.string("language", options.language == .csharp ? "csharp" : "swift")
        json.string("split", options.split == .schema ? "schema" : "file")
        json.string("namespace", options.namespace)
        json.string("access", options.access == .public ? "public" : "internal")
        json.boolean("values", options.values)
        json.boolean("bindings", options.bindings)
        if options.documents { json.boolean("documents", true) }
        json.boolean("checkedExclusivity", options.checkedExclusivity)
        json.beginArray("roots")
        for root in roots {
            let (hash, bindingHash) = hashes(root)
            json.beginObject()
            json.string("name", root)
            json.string("hash", GesSchemaLayoutJson.hex(hash))
            if let bindingHash { json.string("bindingHash", GesSchemaLayoutJson.hex(bindingHash)) }
            json.endObject()
        }
        json.endArray()
        json.beginArray("parameters")
        for parameter in parameters {
            json.beginObject()
            json.string("name", parameter.name)
            json.string("type", parameter.type)
            json.string("unit", parameter.unit)
            json.string("value", parameter.value)
            json.endObject()
        }
        json.endArray()
        json.strings("files", files)
        json.endObject()
        return json.text + "\n"
    }
}
