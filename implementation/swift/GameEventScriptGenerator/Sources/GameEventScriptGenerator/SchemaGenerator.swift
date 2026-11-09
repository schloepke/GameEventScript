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
    /// - Throws: `GesGeneratorError` when no root is given, and `GesSchemaError` when a root is not declared or cannot be
    ///   bound, for example because a parameter has no value.
    public static func layout(_ set: GesSchemaSet, roots: [String], parameters: GesSchemaParameters) throws -> String {
        GesSchemaLayoutJson.write(try GesSchemaLayoutBuilder.build(set, roots: roots, parameters: parameters))
    }
}

/// A generator request that cannot be carried out, independent of the schemas.
public enum GesGeneratorError: Error, Equatable {
    /// The request is incomplete or contradictory; the text explains it.
    case usage(String)
}
