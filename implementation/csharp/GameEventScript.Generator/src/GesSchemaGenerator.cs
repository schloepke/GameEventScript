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
    /// <exception cref="ArgumentException">No root is given, or a root is not declared.</exception>
    /// <exception cref="GesSchemaException">A root cannot be bound, for example because a parameter has no value.</exception>
    public static string Layout(GesSchemaSet set, IReadOnlyList<string> roots, GesSchemaParameters parameters)
    {
        if (set is null) throw new ArgumentNullException(nameof(set));
        if (roots is null) throw new ArgumentNullException(nameof(roots));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        return GesSchemaLayoutJson.Write(GesSchemaLayoutBuilder.Build(set, roots, parameters));
    }
}
