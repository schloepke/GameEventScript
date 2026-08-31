#pragma warning disable CS1591 // Public architecture is documented in HostArchitecture.md.

namespace StepH.GameEventScript.Api;

public enum GameEventScriptExecutionState
{
    Paused,
    Completed,
    RuntimeLimitReached
}

public readonly struct GameEventScriptExecutionResult(
    GameEventScriptExecutionState state,
    int executedOpcodes,
    int processedMessages,
    int emittedMessages,
    int publishedMessages)
{
    public GameEventScriptExecutionState State { get; } = state;
    public int ExecutedOpcodes { get; } = executedOpcodes;
    public int ProcessedMessages { get; } = processedMessages;
    public int EmittedMessages { get; } = emittedMessages;
    public int PublishedMessages { get; } = publishedMessages;
}
