// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

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
        if options.bindings { throw GesGeneratorError(GesGeneratorError.usage, "Bindings concern model code; wire code converts through Values.") }
        let layout = try GesSchemaLayoutBuilder.build(set, roots: roots, parameters: parameters)
        let types = try GesSchemaPlanner.plan(layout)
        return options.language == .csharp ? try GesCSharpEmitter.emit(layout, types, options) : try GesSwiftEmitter.emit(layout, types, options)
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
    ///   - options: The language, namespace and visibility; `values` and `checkedExclusivity` concern wire code and must stay off.
    /// - Returns: The source text, ending with a line break.
    /// - Throws: `GesSchemaError` when a root is not declared, and `GesGeneratorError` when no root is given, a root is no
    ///   schema, an option concerns wire code, the namespace is invalid, two generated names collide, or a reached type has
    ///   no model type.
    public static func generateModels(_ set: GesSchemaSet, roots: [String], options: GesSchemaGenerateOptions) throws -> String {
        if options.values || options.checkedExclusivity {
            throw GesGeneratorError(GesGeneratorError.usage, "Values and checked exclusivity concern wire code, not models.")
        }
        let layout = try GesModelLayoutBuilder.build(set, roots: roots)
        return options.language == .csharp ? try GesCSharpModelEmitter.emit(layout, options) : try GesSwiftModelEmitter.emit(layout, options)
    }
}
