#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;

namespace StepH.GameEventScript.Runtime;

public enum GameEventScriptRunState
{
    Running,
    Paused,
    Completed,
    RuntimeLimitReached
}

public sealed record GameEventScriptRunStepResult(
    GameEventScriptRunState State,
    int ExecutedOpcodes,
    int PublishedMessages);

public sealed class GameEventScriptRun : IDisposable
{
    private readonly Func<GameEventScriptHostRunState, int, GameEventScriptRunStepResult> _drainSlice;
    private readonly GameEventScriptHostRunState _state;
    private readonly bool _accepted;
    private bool _canceled;
    private bool _disposed;

    internal GameEventScriptRun(
        Func<GameEventScriptHostRunState, int, GameEventScriptRunStepResult> drainSlice,
        GameEventScriptHostRunState state,
        bool accepted)
    {
        _drainSlice = drainSlice ?? throw new ArgumentNullException(nameof(drainSlice));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _accepted = accepted;
    }

    public bool IsCompleted => _canceled || !_accepted || _state.IsCompletedAndIdle || _state.Session.RuntimeBudget.IsExhausted;

    public int PendingMessageCount => _state.PendingMessageCount;

    public long ExecutedOpcodes => _state.TotalExecutedOpcodes;

    public GameEventScriptRunStepResult Step(int maxOpcodes)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(GameEventScriptRun));
        }

        if (!_accepted || _canceled)
        {
            return new GameEventScriptRunStepResult(GameEventScriptRunState.Completed, 0, 0);
        }

        return _drainSlice(_state, maxOpcodes);
    }

    public void Cancel() => _canceled = true;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Cancel();
    }
}
