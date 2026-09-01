#pragma warning disable CS1591 // Public architecture is documented in HostArchitecture.md.

namespace StepH.GameEventScript.Api;

public enum GameEventScriptExecutionState
{
    Paused,
    Completed,
    RuntimeLimitReached,
    RuntimeError
}

public readonly struct GameEventScriptExecutionResult(
    GameEventScriptExecutionState state,
    int executedOpcodes,
    int processedMessages,
    int emittedMessages,
    int publishedMessages,
    GameEventScriptDiagnostic? diagnostic = null)
{
    public GameEventScriptExecutionState State { get; } = state;
    public int ExecutedOpcodes { get; } = executedOpcodes;
    public int ProcessedMessages { get; } = processedMessages;
    public int EmittedMessages { get; } = emittedMessages;
    public int PublishedMessages { get; } = publishedMessages;
    public GameEventScriptDiagnostic? Diagnostic { get; } = diagnostic;
}
