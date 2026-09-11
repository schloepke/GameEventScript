// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace StepH.GameEventScript.Api;

/// <summary>
/// Represents a game event script compile options.
/// </summary>
public sealed class GameEventScriptCompileOptions
{
    /// <summary>
    /// Gets the debug info.
    /// </summary>
    public GameEventScriptDebugInfoOptions DebugInfo { get; init; } = GameEventScriptDebugInfoOptions.All;

    /// <summary>
    /// Gets the program version.
    /// </summary>
    public ulong ProgramVersion { get; init; }
}
