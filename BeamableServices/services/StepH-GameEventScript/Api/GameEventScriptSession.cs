#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Api;

public sealed class GameEventScriptSession
{
    private readonly GameEventScriptRuntimeHost? _host;
    private readonly IGameEventScriptDispatcher? _dispatcher;
    private readonly IGameEventScriptRuntimeGate? _runtimeGate;
    private readonly Func<GameEventScriptMessage, bool> _emit;
    private readonly Func<GameEventScriptMessage, bool> _publish;
    private readonly IGameEventScriptRuntimeObserver? _runtimeObserver;
    private bool _automaticDispatchScheduled;

    private GameEventScriptSession(GameEventScriptRandomGenerator random, Func<GameEventScriptMessage, bool> emit,
        GameEventScriptRuntimeLimits? runtimeLimits, IGameEventScriptExtensionRegistry? extensionRegistry, Func<GameEventScriptMessage, bool>? publish, IGameEventScriptRuntimeObserver? runtimeObserver)
    {
        Random = random ?? throw new ArgumentNullException(nameof(random));
        _emit = emit ?? throw new ArgumentNullException(nameof(emit));
        _publish = publish ?? _emit;
        _runtimeObserver = runtimeObserver;
        RuntimeLimits = runtimeLimits ?? GameEventScriptRuntimeLimits.Default;
        RuntimeBudget = new GesRuntimeBudget(this, RuntimeLimits);
        ExtensionRegistry = extensionRegistry ?? GameEventScriptEmptyExtensionRegistry.Instance;
    }

    internal GameEventScriptSession(GameEventScriptRuntimeHost host, GameEventScriptHostRunState state, IGameEventScriptDispatcher? dispatcher, IGameEventScriptRuntimeGate? runtimeGate, GameEventScriptRandomGenerator random,
        Func<GameEventScriptMessage, bool> emit, GameEventScriptRuntimeLimits runtimeLimits, IGameEventScriptExtensionRegistry extensionRegistry,
        Func<GameEventScriptMessage, bool>? publish, IGameEventScriptRuntimeObserver? runtimeObserver) : this(random, emit, runtimeLimits, extensionRegistry, publish, runtimeObserver)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        State = state ?? throw new ArgumentNullException(nameof(state));
        _dispatcher = dispatcher;
        _runtimeGate = runtimeGate;
    }

    public GameEventScriptRandomGenerator Random { get; }

    public GameEventScriptRuntimeLimits RuntimeLimits { get; }

    public IGameEventScriptExtensionRegistry ExtensionRegistry { get; }

    public int PendingMessageCount => State?.PendingMessageCount ?? 0;

    public long ExecutedOpcodes => State?.TotalExecutedOpcodes ?? 0L;

    public bool IsCompletedAndIdle => State?.IsCompletedAndIdle ?? true;

    internal GameEventScriptHostRunState? State { get; }

    internal GesRuntimeBudget RuntimeBudget { get; }

    public bool Emit(GameEventScriptMessage message)
        => ExecuteCore(() => !string.IsNullOrWhiteSpace(message.Name) && _emit(message));

    public bool Emit(string message) => Emit(GameEventScriptMessage.Create(message));

    public bool Emit(string message, IReadOnlyDictionary<string, GesValue> args) => Emit(GameEventScriptMessage.Create(message, args));

    public bool Emit(string message, params (string name, GesValue value)[] args) => Emit(GameEventScriptMessage.Create(message, args));

    public bool Publish(GameEventScriptMessage message)
        => ExecuteCore(() => !string.IsNullOrWhiteSpace(message.Name) && _publish(message));

    public bool Publish(string message) => Publish(GameEventScriptMessage.Create(message));

    public bool Publish(string message, IReadOnlyDictionary<string, GesValue> args) => Publish(GameEventScriptMessage.Create(message, args));

    public bool Publish(string message, params (string name, GesValue value)[] args) => Publish(GameEventScriptMessage.Create(message, args));

    internal void RecordRuntimeLimitReached(string limitName, string detail, int limit) => _runtimeObserver?.RuntimeLimitReached(limitName, detail, limit);

    public bool Dispatch(GameEventScriptMessage message)
        => ExecuteCore(() => DispatchCore(message));

    private bool DispatchCore(GameEventScriptMessage message)
    {
        EnsureHostBacked();
        if (string.IsNullOrWhiteSpace(message.Name)) return false;
        if (!_host!.EnqueueSessionInvocations(State!, message)) return false;
        if (_dispatcher is not null && !_automaticDispatchScheduled)
        {
            _automaticDispatchScheduled = true;
            EnqueueAutomaticDispatchSlice();
        }

        return true;
    }

    public bool DispatchToCompletion(GameEventScriptMessage message)
        => ExecuteCore(() => DispatchToCompletionCore(message));

    private bool DispatchToCompletionCore(GameEventScriptMessage message)
    {
        EnsureHostBacked();
        if (string.IsNullOrWhiteSpace(message.Name)) return false;
        if (!_host!.EnqueueSessionInvocations(State!, message))
        {
            return false;
        }

        _host!.DrainSessionToCompletion(State!);
        return true;
    }

    public GameEventScriptRunStepResult Update(int maxOpcodes)
        => ExecuteCore(() => UpdateCore(maxOpcodes));

    private GameEventScriptRunStepResult UpdateCore(int maxOpcodes)
    {
        EnsureHostBacked();
        if (_dispatcher is not null) throw new InvalidOperationException("GameEventScript automatic dispatch sessions cannot be stepped manually.");
        return maxOpcodes <= 0 ? throw new ArgumentOutOfRangeException(nameof(maxOpcodes), "Update opcode budget must be greater than zero.") : _host!.DrainSessionSlice(State!, maxOpcodes);
    }

    public GameEventScriptRun BeginRun(GameEventScriptMessage message)
        => ExecuteCore(() => BeginRunCore(message));

    private GameEventScriptRun BeginRunCore(GameEventScriptMessage message)
    {
        EnsureHostBacked();
        var accepted = _host!.EnqueueSessionInvocations(State!, message);
        return new GameEventScriptRun(_host.DrainSessionSlice, State!, accepted, _runtimeGate);
    }

    internal void ScheduleAutomaticDispatchIfNeeded()
    {
        EnsureHostBacked();
        if (_dispatcher is null || State!.IsCompletedAndIdle) return;
        if (_automaticDispatchScheduled)
        {
            return;
        }

        _automaticDispatchScheduled = true;
        EnqueueAutomaticDispatchSlice();
    }

    private void RunAutomaticDispatchSlice()
    {
        try
        {
            _host!.DrainSessionToCompletion(State!);
        }
        finally
        {
            _automaticDispatchScheduled = false;
        }
    }

    private void EnqueueAutomaticDispatchSlice()
    {
        try
        {
            _dispatcher!.Enqueue(RunAutomaticDispatchSlice);
        }
        catch
        {
            _automaticDispatchScheduled = false;
            throw;
        }
    }

    private T ExecuteCore<T>(Func<T> workItem)
    {
        _runtimeGate?.Enter();
        try { return workItem(); }
        finally { _runtimeGate?.Exit(); }
    }

    private void EnsureHostBacked()
    {
        if (_host is null || State is null) throw new InvalidOperationException("This GameEventScriptSession is not attached to a host dispatch queue.");
    }
}
