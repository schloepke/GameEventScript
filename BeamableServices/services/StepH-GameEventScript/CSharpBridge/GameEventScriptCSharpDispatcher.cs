#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Threading;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.CSharpBridge;

internal sealed class GameEventScriptCSharpDispatcher : IGameEventScriptDispatcher, IDisposable
{
    private readonly Queue<Action> _workItems = new();
    private readonly object _gate = new();
    private readonly Thread[] _workers;
    private bool _disposed;

    private GameEventScriptCSharpDispatcher(int workerCount, string workerName)
    {
        if (workerCount <= 0) throw new ArgumentOutOfRangeException(nameof(workerCount), "GameEventScript dispatcher worker count must be greater than zero.");
        _workers = new Thread[workerCount];
        for (var index = 0; index < _workers.Length; index++)
        {
            var worker = new Thread(WorkerLoop) { IsBackground = true, Name = workerCount == 1 ? workerName : $"{workerName} #{index + 1}" };
            _workers[index] = worker;
            worker.Start();
        }
    }

    public static GameEventScriptCSharpDispatcher Shared { get; } = new(1, "GameEventScript shared dispatch pump");

    public static GameEventScriptCSharpDispatcher Create(int workerCount = 1) => new(workerCount, "GameEventScript dispatch pump");

    public void Enqueue(Action workItem)
    {
        _ = workItem ?? throw new ArgumentNullException(nameof(workItem));
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GameEventScriptCSharpDispatcher));
            _workItems.Enqueue(workItem);
            Monitor.Pulse(_gate);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            Monitor.PulseAll(_gate);
        }

        var current = Thread.CurrentThread;
        foreach (var worker in _workers)
        {
            if (!ReferenceEquals(worker, current)) worker.Join();
        }
    }

    private void WorkerLoop()
    {
        while (true)
        {
            Action workItem;
            lock (_gate)
            {
                while (_workItems.Count == 0 && !_disposed)
                {
                    Monitor.Wait(_gate);
                }

                if (_workItems.Count == 0 && _disposed) return;
                workItem = _workItems.Dequeue();
            }

            try
            {
                workItem();
            }
            catch
            {
                // Automatic dispatch must keep the shared pump alive.
            }
        }
    }
}
