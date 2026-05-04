#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Threading;

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
    int ProcessedMessages,
    int PublishedMessages);

public sealed class GameEventScriptRun : IDisposable
{
    private readonly Action<GameEventScriptHostRunState> _drain;
    private readonly GameEventScriptHostRunState _state;
    private readonly GameEventScriptStepController _stepController;
    private readonly bool _accepted;
    private Thread? _worker;
    private int _started;
    private bool _disposed;

    internal GameEventScriptRun(
        Action<GameEventScriptHostRunState> drain,
        GameEventScriptHostRunState state,
        GameEventScriptStepController stepController,
        bool accepted)
    {
        _drain = drain ?? throw new ArgumentNullException(nameof(drain));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _stepController = stepController ?? throw new ArgumentNullException(nameof(stepController));
        _accepted = accepted;
        if (!accepted)
        {
            _stepController.Complete();
        }
    }

    public bool IsCompleted => _stepController.IsCompleted;

    public int PendingMessageCount => _state.PendingMessageCount;

    public long ExecutedOpcodes => _stepController.TotalExecutedOpcodes;

    public GameEventScriptRunStepResult Step(int maxOpcodes)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(GameEventScriptRun));
        }

        if (!_accepted)
        {
            return new GameEventScriptRunStepResult(GameEventScriptRunState.Completed, 0, 0, 0);
        }

        var start = Interlocked.CompareExchange(ref _started, 1, 0) == 0
            ? StartWorker
            : (Action?)null;
        return _stepController.Step(maxOpcodes, () => _state.Context.RuntimeBudget.IsExhausted, start);
    }

    public void Cancel() => _stepController.Cancel();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Cancel();
    }

    private void StartWorker()
    {
        _worker = new Thread(Run)
        {
            IsBackground = true,
            Name = "GameEventScript stepped run"
        };
        _worker.Start();
    }

    private void Run()
    {
        try
        {
            _drain(_state);
            _stepController.Complete();
        }
        catch (Exception exception)
        {
            _stepController.Fault(exception);
        }
    }
}
