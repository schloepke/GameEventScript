using System;
using System.Threading;

namespace StepH.GameEventScript.Runtime;

internal sealed class GameEventScriptStepController
{
    private readonly object _gate = new();
    private int _remainingBudget;
    private int _stepExecutedOpcodes;
    private int _stepProcessedMessages;
    private int _stepPublishedMessages;
    private long _totalExecutedOpcodes;
    private bool _paused = true;
    private bool _completed;
    private bool _canceled;
    private Exception? _fault;

    public bool IsCompleted
    {
        get
        {
            lock (_gate)
            {
                return _completed || _canceled || _fault is not null;
            }
        }
    }

    public long TotalExecutedOpcodes
    {
        get
        {
            lock (_gate)
            {
                return _totalExecutedOpcodes;
            }
        }
    }

    public GameEventScriptRunStepResult Step(int maxOpcodes, Func<bool> runtimeLimitReached, Action? start = null)
    {
        if (maxOpcodes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxOpcodes), "Step opcode budget must be greater than zero.");
        }

        lock (_gate)
        {
            ThrowIfFaulted();
            if (_completed || _canceled)
            {
                return CreateResult(runtimeLimitReached());
            }

            _stepExecutedOpcodes = 0;
            _stepProcessedMessages = 0;
            _stepPublishedMessages = 0;
            _remainingBudget += maxOpcodes;
            _paused = false;
            start?.Invoke();
            Monitor.PulseAll(_gate);

            while (!_paused && !_completed && !_canceled && _fault is null)
            {
                Monitor.Wait(_gate);
            }

            ThrowIfFaulted();
            return CreateResult(runtimeLimitReached());
        }
    }

    public bool TryConsumeOpcode()
    {
        lock (_gate)
        {
            while (_remainingBudget <= 0 && !_completed && !_canceled && _fault is null)
            {
                _paused = true;
                Monitor.PulseAll(_gate);
                Monitor.Wait(_gate);
            }

            if (_completed || _canceled || _fault is not null)
            {
                return false;
            }

            _paused = false;
            _remainingBudget--;
            _stepExecutedOpcodes++;
            _totalExecutedOpcodes++;
            return true;
        }
    }

    public bool TryConsumeOpcodes(int count)
    {
        if (count <= 1)
        {
            return TryConsumeOpcode();
        }

        for (var index = 0; index < count; index++)
        {
            if (!TryConsumeOpcode())
            {
                return false;
            }
        }

        return true;
    }

    public void RecordProcessedMessage()
    {
        lock (_gate)
        {
            _stepProcessedMessages++;
        }
    }

    public void RecordPublishedMessage()
    {
        lock (_gate)
        {
            _stepPublishedMessages++;
        }
    }

    public void Complete()
    {
        lock (_gate)
        {
            _completed = true;
            _paused = false;
            Monitor.PulseAll(_gate);
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            _canceled = true;
            _paused = false;
            Monitor.PulseAll(_gate);
        }
    }

    public void Fault(Exception exception)
    {
        lock (_gate)
        {
            _fault = exception;
            _paused = false;
            Monitor.PulseAll(_gate);
        }
    }

    private GameEventScriptRunStepResult CreateResult(bool runtimeLimitReached)
    {
        var state = runtimeLimitReached
            ? GameEventScriptRunState.RuntimeLimitReached
            : _completed || _canceled
                ? GameEventScriptRunState.Completed
                : GameEventScriptRunState.Paused;

        return new GameEventScriptRunStepResult(
            state,
            _stepExecutedOpcodes,
            _stepProcessedMessages,
            _stepPublishedMessages);
    }

    private void ThrowIfFaulted()
    {
        if (_fault is not null)
        {
            throw new InvalidOperationException("GameEventScript stepped run failed.", _fault);
        }
    }
}
