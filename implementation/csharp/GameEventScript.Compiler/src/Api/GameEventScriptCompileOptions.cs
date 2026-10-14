// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Api;

/// <summary>
/// Represents a game event script compile options.
/// </summary>
public sealed class GesCompileOptions
{
    /// <summary>
    /// Gets the debug info.
    /// </summary>
    public GesDebugInfoOptions DebugInfo { get; init; } = GesDebugInfoOptions.All;

    /// <summary>
    /// Gets the program version.
    /// </summary>
    public ulong ProgramVersion { get; init; }
}
