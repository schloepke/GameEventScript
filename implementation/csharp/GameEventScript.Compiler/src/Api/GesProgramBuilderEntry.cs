// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Api;

/// <summary>
/// Adds the Program builder entry point to <see cref="GameEventScriptProgram"/> when the Compiler package is referenced.
/// </summary>
/// <remarks>
/// The entry point is a C# 14 static extension member. Consumers compiled with an earlier language version, such as
/// Unity, call <c>new GesProgramBuilder()</c> directly; both create the same builder.
/// </remarks>
public static class GesProgramBuilderEntry
{
    extension(GameEventScriptProgram)
    {
        /// <summary>
        /// Creates an empty source builder with default compile options.
        /// </summary>
        /// <returns>A new builder; equivalent to <c>new GesProgramBuilder()</c>.</returns>
        public static GesProgramBuilder CreateBuilder() => new();
    }
}
