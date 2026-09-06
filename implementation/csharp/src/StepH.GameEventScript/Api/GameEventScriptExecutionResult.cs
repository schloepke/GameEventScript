// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace StepH.GameEventScript.Api;

/// <summary>
/// Identifies why a synchronous host pump returned.
/// </summary>
public enum GameEventScriptExecutionState
{
    /// <summary>
    /// The frame opcode budget ended while work remains.
    /// </summary>
    Paused,
    /// <summary>
    /// The host became idle.
    /// </summary>
    Completed,
    /// <summary>
    /// A configured safety limit stopped the active run.
    /// </summary>
    RuntimeLimitReached,
    /// <summary>
    /// At least one handler produced a runtime diagnostic.
    /// </summary>
    RuntimeError
}

/// <summary>
/// Provides an allocation-free summary of one host pump.
/// </summary>
public readonly struct GameEventScriptExecutionResult(GameEventScriptExecutionState state, int executedOpcodes, int processedMessages, int emittedMessages, int publishedMessages, GameEventScriptDiagnostic? diagnostic = null)
{
    /// <summary>
    /// Gets the reason the pump returned.
    /// </summary>
    public GameEventScriptExecutionState State { get; } = state;
    /// <summary>
    /// Gets the number of script opcodes executed by this pump.
    /// </summary>
    public int ExecutedOpcodes { get; } = executedOpcodes;
    /// <summary>
    /// Gets the number of logical messages whose complete handler snapshots finished.
    /// </summary>
    public int ProcessedMessages { get; } = processedMessages;
    /// <summary>
    /// Gets the number of local emit attempts made during this pump.
    /// </summary>
    public int EmittedMessages { get; } = emittedMessages;
    /// <summary>
    /// Gets the number of publish attempts made during this pump.
    /// </summary>
    public int PublishedMessages { get; } = publishedMessages;
    /// <summary>
    /// Gets the first structured runtime diagnostic reported by this pump, if any.
    /// </summary>
    public GameEventScriptDiagnostic? Diagnostic { get; } = diagnostic;
}
