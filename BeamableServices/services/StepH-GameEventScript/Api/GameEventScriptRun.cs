#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Api;

public enum GameEventScriptRunState
{
    Running,
    Paused,
    Completed,
    RuntimeLimitReached
}

public sealed record GameEventScriptRunStepResult(GameEventScriptRunState State, int ExecutedOpcodes, int PublishedMessages);

public sealed class GameEventScriptRun : IDisposable
{
    private readonly Func<GameEventScriptHostRunState, int, GameEventScriptRunStepResult> _drainSlice;
    private readonly GameEventScriptHostRunState _state;
    private readonly IGameEventScriptRuntimeGate? _runtimeGate;
    private readonly bool _accepted;
    private bool _canceled;
    private bool _disposed;

    internal GameEventScriptRun(Func<GameEventScriptHostRunState, int, GameEventScriptRunStepResult> drainSlice, GameEventScriptHostRunState state, bool accepted, IGameEventScriptRuntimeGate? runtimeGate = null)
    {
        _drainSlice = drainSlice ?? throw new ArgumentNullException(nameof(drainSlice));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _accepted = accepted;
        _runtimeGate = runtimeGate;
    }

    public bool IsCompleted => ExecuteCore(() => _canceled || !_accepted || _state.IsCompletedAndIdle || _state.Session.RuntimeBudget.IsExhausted);

    public int PendingMessageCount => ExecuteCore(() => _state.PendingMessageCount);

    public long ExecutedOpcodes => ExecuteCore(() => _state.TotalExecutedOpcodes);

    public GameEventScriptRunStepResult Execute(int maxOpcodes)
        => ExecuteCore(() => ExecuteCoreStep(maxOpcodes));

    public GameEventScriptRunStepResult Step(int maxOpcodes) => Execute(maxOpcodes);

    public GameEventScriptRunStepResult ExecuteAll()
    {
        GameEventScriptRunStepResult result = new(GameEventScriptRunState.Completed, 0, 0);
        while (!IsCompleted)
        {
            result = Execute(int.MaxValue);
            if (result.State is GameEventScriptRunState.Paused or GameEventScriptRunState.Running &&
                result.ExecutedOpcodes == 0 &&
                result.PublishedMessages == 0)
            {
                break;
            }
        }

        return result;
    }

    private GameEventScriptRunStepResult ExecuteCoreStep(int maxOpcodes)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(GameEventScriptRun));
        if (maxOpcodes <= 0) throw new ArgumentOutOfRangeException(nameof(maxOpcodes), "Execute opcode budget must be greater than zero.");
        if (!_accepted || _canceled) return new GameEventScriptRunStepResult(GameEventScriptRunState.Completed, 0, 0);
        return _drainSlice(_state, maxOpcodes);
    }

    public void Cancel() => ExecuteCore(() => _canceled = true);

    public void Dispose()
        => ExecuteCore(DisposeCore);

    private void DisposeCore()
    {
        if (_disposed) return;
        _disposed = true;
        _canceled = true;
    }

    private T ExecuteCore<T>(Func<T> workItem)
    {
        _runtimeGate?.Enter();
        try { return workItem(); }
        finally { _runtimeGate?.Exit(); }
    }

    private void ExecuteCore(Action workItem)
    {
        _runtimeGate?.Enter();
        try { workItem(); }
        finally { _runtimeGate?.Exit(); }
    }
}
