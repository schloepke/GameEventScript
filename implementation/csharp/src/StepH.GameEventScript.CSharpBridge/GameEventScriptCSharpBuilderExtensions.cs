// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Text;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.CSharpBridge;

/// <summary>Provides C# file-system conveniences for the portable compiler builder.</summary>
public static class GameEventScriptCSharpBuilderExtensions
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Synchronously reads a UTF-8 source file and adds its text to the builder, using the supplied path as its source name.</summary>
    /// <param name="builder">The builder receiving the source.</param>
    /// <param name="path">The path of the source file to read.</param>
    /// <returns>The same builder for fluent configuration.</returns>
    /// <remarks>The portable AddScript operation handles source validation and an optional leading Unicode BOM. No file handle is retained.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when the builder or path is null.</exception>
    /// <exception cref="DecoderFallbackException">Thrown when the file contains invalid UTF-8.</exception>
    /// <exception cref="IOException">Thrown when reading the file fails.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the file is denied.</exception>
    public static GameEventScriptBuilder AddFile(this GameEventScriptBuilder builder, string path)
    {
        _ = builder ?? throw new ArgumentNullException(nameof(builder));
        _ = path ?? throw new ArgumentNullException(nameof(path));
        return builder.AddScript(StrictUtf8.GetString(File.ReadAllBytes(path)), path);
    }
}
