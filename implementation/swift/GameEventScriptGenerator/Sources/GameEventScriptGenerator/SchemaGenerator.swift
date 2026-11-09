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
        let layout = try GesSchemaLayoutBuilder.build(set, roots: roots, parameters: parameters)
        let types = try GesSchemaPlanner.plan(layout)
        return options.language == .csharp ? try GesCSharpEmitter.emit(layout, types, options) : try GesSwiftEmitter.emit(layout, types, options)
    }
}
