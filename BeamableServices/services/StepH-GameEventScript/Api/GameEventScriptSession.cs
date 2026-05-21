#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Api;

public sealed class GameEventScriptSession
{
    private readonly GameEventScriptHost? _host;
    private readonly GameEventScriptDispatchMode _dispatchMode;
    private readonly GameEventScriptDispatcher? _dispatcher;
    private readonly Func<GameEventScriptMessage, bool> _emit;
    private readonly Func<GameEventScriptMessage, bool> _publish;
    private readonly object _pumpGate = new();
    private bool _automaticDispatchScheduled;

    public GameEventScriptSession(
        GameEventScriptRandomGenerator random,
        Func<GameEventScriptMessage, bool> emit,
        IGameEventScriptDiagnosticCollector? diagnosticCollector = null,
        GameEventScriptRuntimeLimits? runtimeLimits = null,
        IGameEventScriptExtensionRegistry? extensionRegistry = null,
        Func<GameEventScriptMessage, bool>? publish = null)
    {
        Random = random ?? throw new ArgumentNullException(nameof(random));
        _emit = emit ?? throw new ArgumentNullException(nameof(emit));
        _publish = publish ?? _emit;
        DiagnosticCollector = diagnosticCollector;
        RuntimeLimits = runtimeLimits ?? GameEventScriptRuntimeLimits.Default;
        RuntimeBudget = new GesRuntimeBudget(this, RuntimeLimits);
        ExtensionRegistry = extensionRegistry ?? GameEventScriptEmptyExtensionRegistry.Instance;
    }

    internal GameEventScriptSession(
        GameEventScriptHost host,
        GameEventScriptHostRunState state,
        GameEventScriptDispatchMode dispatchMode,
        GameEventScriptDispatcher dispatcher,
        GameEventScriptRandomGenerator random,
        Func<GameEventScriptMessage, bool> emit,
        IGameEventScriptDiagnosticCollector? diagnosticCollector,
        GameEventScriptRuntimeLimits runtimeLimits,
        IGameEventScriptExtensionRegistry extensionRegistry,
        Func<GameEventScriptMessage, bool>? publish)
        : this(random, emit, diagnosticCollector, runtimeLimits, extensionRegistry, publish)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        State = state ?? throw new ArgumentNullException(nameof(state));
        _dispatchMode = dispatchMode;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public GameEventScriptRandomGenerator Random { get; }

    public IGameEventScriptDiagnosticCollector? DiagnosticCollector { get; }

    public GameEventScriptRuntimeLimits RuntimeLimits { get; }

    public IGameEventScriptExtensionRegistry ExtensionRegistry { get; }

    public int PendingMessageCount => State?.PendingMessageCount ?? 0;

    public long ExecutedOpcodes => State?.TotalExecutedOpcodes ?? 0L;

    public bool IsCompletedAndIdle => State?.IsCompletedAndIdle ?? true;

    internal GameEventScriptHostRunState? State { get; }

    internal GesRuntimeBudget RuntimeBudget { get; }

    public bool Emit(GameEventScriptMessage message) => !string.IsNullOrWhiteSpace(message.Name) && _emit(message);

    public bool Emit(string message) => Emit(GameEventScriptMessage.Create(message));

    public bool Emit(string message, IReadOnlyDictionary<string, GameEventScriptValue> args) => Emit(GameEventScriptMessage.Create(message, args));

    public bool Emit(string message, params (string name, GameEventScriptValue value)[] args) => Emit(GameEventScriptMessage.Create(message, args));

    public bool Publish(GameEventScriptMessage message) => !string.IsNullOrWhiteSpace(message.Name) && _publish(message);

    public bool Publish(string message) => Publish(GameEventScriptMessage.Create(message));

    public bool Publish(string message, IReadOnlyDictionary<string, GameEventScriptValue> args) => Publish(GameEventScriptMessage.Create(message, args));

    public bool Publish(string message, params (string name, GameEventScriptValue value)[] args) => Publish(GameEventScriptMessage.Create(message, args));

    public void RecordDiagnostic(GameEventScriptDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, GameEventScriptValue> arguments, string? detail = null)
        => DiagnosticCollector?.Record(kind, name, arguments, detail);

    public bool Dispatch(GameEventScriptMessage message)
    {
        EnsureHostBacked();
        if (string.IsNullOrWhiteSpace(message.Name))
        {
            return false;
        }

        var shouldScheduleAutomaticDispatch = false;
        lock (_pumpGate)
        {
            if (!_host!.TryEnqueueSessionInvocations(State!, message))
            {
                return false;
            }

            if (_dispatchMode == GameEventScriptDispatchMode.Automatic && !_automaticDispatchScheduled)
            {
                _automaticDispatchScheduled = true;
                shouldScheduleAutomaticDispatch = true;
            }
        }

        if (shouldScheduleAutomaticDispatch)
        {
            try
            {
                _dispatcher!.Enqueue(RunAutomaticDispatchSlice);
            }
            catch
            {
                lock (_pumpGate)
                {
                    _automaticDispatchScheduled = false;
                }

                throw;
            }
        }

        return true;
    }

    public bool DispatchToCompletion(GameEventScriptMessage message)
    {
        EnsureHostBacked();
        if (string.IsNullOrWhiteSpace(message.Name))
        {
            return false;
        }

        lock (_pumpGate)
        {
            if (!_host!.TryEnqueueSessionInvocations(State!, message))
            {
                return false;
            }
        }

        _host!.DrainSessionToCompletion(State!);
        return true;
    }

    public GameEventScriptRunStepResult Update(int maxOpcodes)
    {
        EnsureHostBacked();
        if (_dispatchMode == GameEventScriptDispatchMode.Automatic)
        {
            throw new InvalidOperationException("GameEventScript automatic dispatch sessions cannot be stepped manually.");
        }

        if (maxOpcodes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxOpcodes), "Update opcode budget must be greater than zero.");
        }

        return _host!.DrainSessionSlice(State!, maxOpcodes);
    }

    public GameEventScriptRun BeginRun(GameEventScriptMessage message)
    {
        EnsureHostBacked();
        var accepted = _host!.TryEnqueueSessionInvocations(State!, message);
        return new GameEventScriptRun(_host.DrainSessionSlice, State!, accepted);
    }

    internal void ScheduleAutomaticDispatchIfNeeded()
    {
        EnsureHostBacked();
        if (_dispatchMode != GameEventScriptDispatchMode.Automatic || State!.IsCompletedAndIdle)
        {
            return;
        }

        var shouldSchedule = false;
        lock (_pumpGate)
        {
            if (!_automaticDispatchScheduled)
            {
                _automaticDispatchScheduled = true;
                shouldSchedule = true;
            }
        }

        if (!shouldSchedule)
        {
            return;
        }

        try
        {
            _dispatcher!.Enqueue(RunAutomaticDispatchSlice);
        }
        catch
        {
            lock (_pumpGate)
            {
                _automaticDispatchScheduled = false;
            }

            throw;
        }
    }

    private void RunAutomaticDispatchSlice()
    {
        try
        {
            _host!.DrainSessionOneToCompletion(State!);
        }
        finally
        {
            var shouldRestart = false;
            lock (_pumpGate)
            {
                shouldRestart = _dispatchMode == GameEventScriptDispatchMode.Automatic &&
                                !State!.IsCompletedAndIdle;
                _automaticDispatchScheduled = shouldRestart;
            }

            if (shouldRestart)
            {
                try
                {
                    _dispatcher!.Enqueue(RunAutomaticDispatchSlice);
                }
                catch
                {
                    lock (_pumpGate)
                    {
                        _automaticDispatchScheduled = false;
                    }

                    throw;
                }
            }
        }
    }

    private void EnsureHostBacked()
    {
        if (_host is null || State is null)
        {
            throw new InvalidOperationException("This GameEventScriptSession is not attached to a host dispatch queue.");
        }
    }
}
