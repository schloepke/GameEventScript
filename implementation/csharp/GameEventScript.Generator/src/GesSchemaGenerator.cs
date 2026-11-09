// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Generator;

/// <summary>
/// Generates code and layouts from schemas at build time. Every type reached from the roots is bound once with the given
/// parameters, so a type that several roots share appears once. The generator belongs to build tools and is never part of
/// a game.
/// </summary>
public static class GesSchemaGenerator
{
    /// <summary>
    /// Writes the language-neutral layout of the roots as canonical JSON, the input of every code emitter. Every port writes
    /// identical text for identical schemas and parameters.
    /// </summary>
    /// <param name="set">The schemas.</param>
    /// <param name="roots">The schemas, messages or message sets to generate, in the order the output names them.</param>
    /// <param name="parameters">A value for every parameter the roots reach.</param>
    /// <returns>The JSON text, ending with a line break.</returns>
    /// <exception cref="ArgumentException">A root is not declared.</exception>
    /// <exception cref="GesGeneratorException">No root is given.</exception>
    /// <exception cref="GesSchemaException">A root cannot be bound, for example because a parameter has no value.</exception>
    public static string Layout(GesSchemaSet set, IReadOnlyList<string> roots, GesSchemaParameters parameters)
    {
        if (set is null) throw new ArgumentNullException(nameof(set));
        if (roots is null) throw new ArgumentNullException(nameof(roots));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        return GesSchemaLayoutJson.Write(GesSchemaLayoutBuilder.Build(set, roots, parameters));
    }

    /// <summary>
    /// Generates code for the roots and every type they reach: a type per schema that writes and reads its records with
    /// the bytes, error codes and paths of <see cref="GesSchemaWriter"/> and <see cref="GesSchemaReader"/>. Every port
    /// writes identical text for identical schemas, parameters and options.
    /// </summary>
    /// <param name="set">The schemas.</param>
    /// <param name="roots">The schemas to generate, in the order the header names them.</param>
    /// <param name="parameters">A value for every parameter the roots reach; they become constants of the code.</param>
    /// <param name="options">The language, namespace and visibility.</param>
    /// <returns>The source text, ending with a line break.</returns>
    /// <exception cref="ArgumentException">A root is not declared.</exception>
    /// <exception cref="GesSchemaException">A root cannot be bound, for example because a parameter has no value.</exception>
    /// <exception cref="GesGeneratorException">No root is given, the namespace is invalid, two generated names collide, or
    /// a reached type cannot be generated yet.</exception>
    public static string Generate(GesSchemaSet set, IReadOnlyList<string> roots, GesSchemaParameters parameters, GesSchemaGenerateOptions options)
    {
        if (set is null) throw new ArgumentNullException(nameof(set));
        if (roots is null) throw new ArgumentNullException(nameof(roots));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        if (options is null) throw new ArgumentNullException(nameof(options));
        var layout = GesSchemaLayoutBuilder.Build(set, roots, parameters);
        var types = GesSchemaPlanner.Plan(layout);
        return options.Language == GesSchemaLanguage.CSharp ? GesCSharpEmitter.Emit(layout, types, options) : GesSwiftEmitter.Emit(layout, types, options);
    }
}
