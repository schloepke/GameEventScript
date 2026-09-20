// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Api;

/// <summary>Identifies the outcome of host or instance initialization.</summary>
public enum GameEventScriptStartState
{
    /// <summary>Initialization succeeded; ordinary messages may still be queued.</summary>
    Ready,
    /// <summary>Initialization failed with a runtime diagnostic.</summary>
    RuntimeError,
    /// <summary>A safety limit prevented initialization from completing.</summary>
    RuntimeLimitReached
}

/// <summary>Describes completed initialization independently of queue idleness.</summary>
/// <param name="state">The initialization outcome.</param>
/// <param name="diagnostic">The failure diagnostic, when present.</param>
/// <param name="executedOpcodes">The number of initialization opcodes executed.</param>
/// <param name="processedMessages">The number of fully completed initialization snapshots.</param>
/// <param name="emittedMessages">The number of initialization emit attempts.</param>
/// <param name="publishedMessages">The number of initialization publish attempts.</param>
public readonly struct GameEventScriptStartResult(
    GameEventScriptStartState state,
    GameEventScriptDiagnostic? diagnostic = null,
    int executedOpcodes = 0,
    int processedMessages = 0,
    int emittedMessages = 0,
    int publishedMessages = 0
)
{
    /// <summary>Gets the initialization outcome.</summary>
    public GameEventScriptStartState State { get; } = state;
    /// <summary>Gets the original failure diagnostic, when present.</summary>
    public GameEventScriptDiagnostic? Diagnostic { get; } = diagnostic;
    /// <summary>Gets the number of initialization opcodes executed.</summary>
    public int ExecutedOpcodes { get; } = executedOpcodes;
    /// <summary>Gets the number of fully completed initialization snapshots.</summary>
    public int ProcessedMessages { get; } = processedMessages;
    /// <summary>Gets the number of initialization emit attempts.</summary>
    public int EmittedMessages { get; } = emittedMessages;
    /// <summary>Gets the number of initialization publish attempts.</summary>
    public int PublishedMessages { get; } = publishedMessages;
}
