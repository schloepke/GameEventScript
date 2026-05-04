#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.BytecodeVM;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptMessageSignature;

namespace StepH.GameEventScript.Runtime;

public sealed class GameEventScriptHost
{
    private const int NormalPriority = 0;

    private readonly GameEventScriptRandomGenerator _random;
    private readonly IGameEventScriptDiagnosticCollector? _diagnosticCollector;
    private readonly Action<GameEventScriptMessage>? _publishedMessageObserver;
    private readonly IGameEventScriptExtensionRegistry _extensionRegistry;
    private readonly GameEventScriptRuntimeLimits _runtimeLimits;
    private readonly GameEventScriptDispatchMode _dispatchMode;
    private readonly GameEventScriptDispatcher _dispatcher;
    private readonly Dictionary<string, MessageSubscription[]> _dispatchIndex = new(StringComparer.Ordinal);
    private readonly object _dispatchGate = new();
    private readonly object _pumpGate = new();
    private GameEventScriptHostRunState _liveState;
    private bool _automaticDispatchScheduled;
    private long _nextRegistrationOrder;

    internal GameEventScriptHost(GameEventScriptRandomGenerator random, IGameEventScriptDiagnosticCollector? diagnosticCollector, Action<GameEventScriptMessage>? publishedMessageObserver,
        IGameEventScriptExtensionRegistry? extensionRegistry, GameEventScriptRuntimeLimits? runtimeLimits, GameEventScriptDispatchMode dispatchMode = GameEventScriptDispatchMode.Manual,
        GameEventScriptDispatcher? dispatcher = null)
    {
        _random = random;
        _diagnosticCollector = diagnosticCollector;
        _publishedMessageObserver = publishedMessageObserver;
        _extensionRegistry = extensionRegistry ?? GameEventScriptEmptyExtensionRegistry.Instance;
        _runtimeLimits = runtimeLimits ?? GameEventScriptRuntimeLimits.Default;
        _dispatchMode = dispatchMode;
        _dispatcher = dispatcher ?? GameEventScriptDispatcher.Shared;
        _liveState = CreateLiveState(dispatchMode);
    }

    public static GameEventScriptHostBuilder CreateBuilder() => new();

    #region Public interface

    public GameEventScriptHost Load(GameEventScriptCompiled bytecode, int priority = NormalPriority)
    {
        _ = bytecode ?? throw new ArgumentNullException(nameof(bytecode));
        return Load(GesBytecodeVmExecutableBuilder.Build(bytecode), priority);
    }

    public GameEventScriptHost Load(IGameEventScriptMessageHandlerCollection handlers, int priority = NormalPriority)
    {
        _ = handlers ?? throw new ArgumentNullException(nameof(handlers));
        if (handlers is GesBytecodeVmExecutable registerCompiled)
        {
            GesDynamicLinker.Bind(registerCompiled, _extensionRegistry);
            RegisterMany(registerCompiled.CompiledHandlers, registerCompiled, priority);
            return this;
        }

        var handlerEntries = handlers.Handlers.ToArray();
        foreach (var (signature, handler) in handlerEntries)
        {
            _ = signature ?? throw new ArgumentException("Handler collection contains a null signature.", nameof(handlers));
            _ = handler ?? throw new ArgumentException("Handler collection contains a null handler.", nameof(handlers));
        }

        RegisterMany(handlerEntries, priority);
        return this;
    }

    public GameEventScriptHost Subscribe(string message, IReadOnlyCollection<string> parameterNames, Action<GameEventScriptMessage, GameEventScriptContext> handler, int priority = NormalPriority)
        => Subscribe(Create(!string.IsNullOrWhiteSpace(message) ? message : throw new ArgumentException("Message must not be null or whitespace", nameof(message)),
            parameterNames ?? throw new ArgumentNullException(nameof(parameterNames))), handler, priority);

    public GameEventScriptHost Subscribe(GameEventScriptMessageSignature signature, Action<GameEventScriptMessage, GameEventScriptContext> handler, int priority = NormalPriority)
    {
        Register(
            signature ?? throw new ArgumentNullException(nameof(signature)),
            priority,
            handler ?? throw new ArgumentNullException(nameof(handler)));
        return this;
    }

    public GameEventScriptHost Subscribe(IGameEventScriptMessageHandlerCollection handlers, int priority = NormalPriority)
    {
        _ = handlers ?? throw new ArgumentNullException(nameof(handlers));
        if (handlers is GesBytecodeVmExecutable registerCompiled)
        {
            GesDynamicLinker.Bind(registerCompiled, _extensionRegistry);
            RegisterMany(registerCompiled.CompiledHandlers, registerCompiled, priority);
            return this;
        }

        var handlerEntries = handlers.Handlers.ToArray();
        foreach (var (signature, handler) in handlerEntries)
        {
            _ = signature ?? throw new ArgumentException("Handler collection contains a null signature.", nameof(handlers));
            _ = handler ?? throw new ArgumentException("Handler collection contains a null handler.", nameof(handlers));
        }

        RegisterMany(handlerEntries, priority);
        return this;
    }

    public bool Publish(GameEventScriptMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Name))
        {
            return false;
        }

        var shouldScheduleAutomaticDispatch = false;
        lock (_pumpGate)
        {
            ResetManualStateIfCompletedAndIdle();
            if (!_liveState.Enqueue(message))
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
                _dispatcher.Enqueue(RunAutomaticDispatchSlice);
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

    public bool PublishToCompletion(GameEventScriptMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Name))
        {
            return false;
        }

        var state = new GameEventScriptHostRunState(_random, _diagnosticCollector, _publishedMessageObserver, _extensionRegistry, _runtimeLimits);
        if (!state.Enqueue(message))
        {
            return false;
        }

        DrainToCompletion(state);
        return true;
    }

    public GameEventScriptRunStepResult Update(int maxOpcodes)
    {
        if (_dispatchMode == GameEventScriptDispatchMode.Automatic)
        {
            throw new InvalidOperationException("GameEventScript automatic dispatch hosts cannot be stepped manually.");
        }

        if (maxOpcodes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxOpcodes), "Update opcode budget must be greater than zero.");
        }

        lock (_pumpGate)
        {
            ResetManualStateIfCompletedAndIdle();
        }

        return DrainSlice(_liveState, maxOpcodes);
    }

    public GameEventScriptRun BeginRun(GameEventScriptMessage message)
    {
        var state = new GameEventScriptHostRunState(_random, _diagnosticCollector, _publishedMessageObserver, _extensionRegistry, _runtimeLimits);
        var accepted = !string.IsNullOrWhiteSpace(message.Name) && state.Enqueue(message);
        return new GameEventScriptRun(DrainSlice, state, accepted);
    }

    #endregion

    #region Internals

    private GameEventScriptHostRunState CreateLiveState(GameEventScriptDispatchMode dispatchMode)
        => new(_random, _diagnosticCollector, _publishedMessageObserver, _extensionRegistry, _runtimeLimits, enforceProcessedEventsLimit: false);

    private void ResetManualStateIfCompletedAndIdle()
    {
        if (_dispatchMode != GameEventScriptDispatchMode.Manual ||
            !_liveState.IsCompletedAndIdle)
        {
            return;
        }

        _liveState = CreateLiveState(GameEventScriptDispatchMode.Manual);
    }

    private void RunAutomaticDispatchSlice()
    {
        try
        {
            DrainOneToCompletion(_liveState);
        }
        finally
        {
            var shouldRestart = false;
            lock (_pumpGate)
            {
                shouldRestart = _dispatchMode == GameEventScriptDispatchMode.Automatic &&
                                !_liveState.IsCompletedAndIdle;
                if (shouldRestart)
                {
                    _automaticDispatchScheduled = true;
                }
                else
                {
                    _automaticDispatchScheduled = false;
                    if (_dispatchMode == GameEventScriptDispatchMode.Automatic)
                    {
                        _liveState = CreateLiveState(GameEventScriptDispatchMode.Automatic);
                    }
                }
            }

            if (shouldRestart)
            {
                try
                {
                    _dispatcher.Enqueue(RunAutomaticDispatchSlice);
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

    private void Register(
        GameEventScriptMessageSignature signature,
        int priority,
        Action<GameEventScriptMessage, GameEventScriptContext> handler)
    {
        lock (_dispatchGate)
        {
            RegisterLocked(signature, priority, handler);
        }
    }

    private void RegisterMany(
        IReadOnlyList<(GameEventScriptMessageSignature Signature, Action<GameEventScriptMessage, GameEventScriptContext> Handler)> handlers,
        int priority)
    {
        lock (_dispatchGate)
        {
            foreach (var (signature, handler) in handlers)
            {
                RegisterLocked(signature, priority, handler);
            }
        }
    }

    private void RegisterMany(
        IReadOnlyList<GesBytecodeVmCompiledHandler> handlers,
        GesBytecodeVmExecutable executable,
        int priority)
    {
        lock (_dispatchGate)
        {
            foreach (var handler in handlers)
            {
                _ = handler ?? throw new ArgumentException("Handler collection contains a null compiled handler.", nameof(handlers));
                RegisterLocked(handler.Definition, priority, executable, handler);
            }
        }
    }

    private void RegisterLocked(
        GameEventScriptMessageSignature signature,
        int priority,
        Action<GameEventScriptMessage, GameEventScriptContext> handler)
    {
        var subscription = new MessageSubscription(
            signature,
            priority,
            _nextRegistrationOrder++,
            handler,
            null,
            null);

        _dispatchIndex[subscription.Definition.SignatureId] = _dispatchIndex.TryGetValue(subscription.Definition.SignatureId, out var handlers)
            ? InsertSubscriptionByDispatchOrder(handlers, subscription)
            : [subscription];
    }

    private void RegisterLocked(
        GameEventScriptMessageSignature signature,
        int priority,
        GesBytecodeVmExecutable executable,
        GesBytecodeVmCompiledHandler handler)
    {
        var subscription = new MessageSubscription(
            signature,
            priority,
            _nextRegistrationOrder++,
            null,
            executable,
            handler);

        _dispatchIndex[subscription.Definition.SignatureId] = _dispatchIndex.TryGetValue(subscription.Definition.SignatureId, out var handlers)
            ? InsertSubscriptionByDispatchOrder(handlers, subscription)
            : [subscription];
    }

    private void DrainToCompletion(GameEventScriptHostRunState state)
    {
        while (!state.IsCompletedAndIdle && !state.Context.RuntimeBudget.IsExhausted)
        {
            DrainSlice(state, int.MaxValue);
        }
    }

    private void DrainOneToCompletion(GameEventScriptHostRunState state)
    {
        var startedEventCount = state.StartedEventCount;
        do
        {
            DrainSlice(state, int.MaxValue, stopAfterStartedEventCount: startedEventCount + 1);
        }
        while (!state.IsCompletedAndIdle &&
               !state.Context.RuntimeBudget.IsExhausted &&
               (state.HasActiveDispatch || state.StartedEventCount <= startedEventCount));
    }

    private GameEventScriptRunStepResult DrainSlice(GameEventScriptHostRunState state, int maxOpcodes)
    {
        return DrainSlice(state, maxOpcodes, stopAfterStartedEventCount: null);
    }

    private GameEventScriptRunStepResult DrainSlice(GameEventScriptHostRunState state, int maxOpcodes, int? stopAfterStartedEventCount)
    {
        state.BeginStep();
        var remainingOpcodes = maxOpcodes;
        while (remainingOpcodes > 0 && !state.Context.RuntimeBudget.IsExhausted)
        {
            if (!state.HasActiveDispatch)
            {
                if (stopAfterStartedEventCount is { } stopAfter && state.StartedEventCount >= stopAfter)
                {
                    break;
                }

                if (!state.TryDequeue(out var queuedEvent))
                {
                    break;
                }

                if (queuedEvent is null || !state.TryStartProcessingEvent())
                {
                    continue;
                }

                StartDispatch(state, queuedEvent);
                if (!state.HasActiveDispatch)
                {
                    continue;
                }
            }

            var before = state.StepExecutedOpcodes;
            RunActiveDispatch(state, remainingOpcodes);
            var consumed = state.StepExecutedOpcodes - before;
            remainingOpcodes -= consumed;

            if (state.HasActiveDispatch && consumed == 0)
            {
                break;
            }
        }

        return state.CreateStepResult();
    }

    private void StartDispatch(GameEventScriptHostRunState state, GameEventScriptMessage queuedEvent)
    {
        state.RecordDiagnostic(GameEventScriptDiagnosticEventKind.DispatchStarted, queuedEvent.Name, queuedEvent.Arguments, $"Dispatch '{queuedEvent.Name}' started");

        if (!TryGetSubscriptions(queuedEvent.SignatureId, out var subscriptions))
        {
            state.RecordDiagnostic(GameEventScriptDiagnosticEventKind.DispatchCompleted, queuedEvent.Name, queuedEvent.Arguments, $"Dispatch '{queuedEvent.Name}' completed without subscribers");
            return;
        }

        state.StartDispatch(queuedEvent, subscriptions);
    }

    private void RunActiveDispatch(GameEventScriptHostRunState state, int maxOpcodes)
    {
        var remainingOpcodes = maxOpcodes;
        while (state.ActiveSubscriptionIndex < state.ActiveSubscriptions!.Length)
        {
            var subscription = state.ActiveSubscriptions[state.ActiveSubscriptionIndex];
            if (state.ActiveScriptFiber is not null)
            {
                var executed = RunScriptFiberSlice(state, state.ActiveScriptFiber, remainingOpcodes);
                remainingOpcodes -= executed;
                if (!state.ActiveScriptFiber.IsCompleted)
                {
                    return;
                }

                if (state.ActiveScriptFiber.Failed)
                {
                    state.ClearActiveScriptFiber();
                }
                else
                {
                    state.ClearActiveScriptFiber();
                }

                state.ActiveSubscriptionIndex++;
                continue;
            }

            state.RecordDiagnostic(GameEventScriptDiagnosticEventKind.SubscriberMatched, state.ActiveMessage!.Name, state.ActiveMessage.Arguments, $"Subscriber matched: {subscription.Definition.SignatureId}");

            try
            {
                state.RecordDiagnostic(GameEventScriptDiagnosticEventKind.SubscriberInvoked, state.ActiveMessage.Name, state.ActiveMessage.Arguments, "Subscriber invoked");
                if (subscription.ScriptExecutable is not null && subscription.ScriptHandler is not null)
                {
                    var fiber = GesBytecodeVmExecutionSession.CreateFiber(subscription.ScriptExecutable, state.Context, subscription.ScriptHandler, state.ActiveMessage.Arguments);
                    state.SetActiveScriptFiber(fiber);
                    var executed = RunScriptFiberSlice(state, fiber, remainingOpcodes);
                    remainingOpcodes -= executed;
                    if (!fiber.IsCompleted)
                    {
                        return;
                    }

                    state.ClearActiveScriptFiber();
                }
                else
                {
                    subscription.Handler!(state.ActiveMessage, state.Context);
                }
            }
            catch (GameEventScriptFatalRuntimeException)
            {
                throw;
            }
            catch
            {
                // Runtime dispatch must remain lenient.
                state.ClearActiveScriptFiber();
            }

            state.ActiveSubscriptionIndex++;
        }

        state.RecordDiagnostic(GameEventScriptDiagnosticEventKind.DispatchCompleted, state.ActiveMessage!.Name, state.ActiveMessage.Arguments, $"Dispatch '{state.ActiveMessage.Name}' completed");
        state.CompleteDispatch();
    }

    private static int RunScriptFiberSlice(GameEventScriptHostRunState state, GesBytecodeVmExecutionSession.Fiber fiber, int maxOpcodes)
    {
        var executed = fiber.RunSlice(maxOpcodes);
        state.RecordExecutedOpcodes(executed);
        return executed;
    }

    private bool TryGetSubscriptions(string signatureId, out MessageSubscription[] subscriptions)
    {
        lock (_dispatchGate)
        {
            return _dispatchIndex.TryGetValue(signatureId, out subscriptions!);
        }
    }

    private static MessageSubscription[] InsertSubscriptionByDispatchOrder(MessageSubscription[] handlers, MessageSubscription subscription)
    {
        var insertIndex = Array.FindIndex(handlers, existing => CompareDispatchOrder(subscription, existing) < 0);
        var result = new MessageSubscription[handlers.Length + 1];
        if (insertIndex < 0)
        {
            Array.Copy(handlers, result, handlers.Length);
            result[^1] = subscription;
            return result;
        }

        if (insertIndex > 0)
        {
            Array.Copy(handlers, 0, result, 0, insertIndex);
        }

        result[insertIndex] = subscription;
        Array.Copy(handlers, insertIndex, result, insertIndex + 1, handlers.Length - insertIndex);
        return result;
    }

    private static int CompareDispatchOrder(MessageSubscription left, MessageSubscription right)
    {
        var priorityComparison = right.Priority.CompareTo(left.Priority);
        return priorityComparison != 0
            ? priorityComparison
            : left.RegistrationOrder.CompareTo(right.RegistrationOrder);
    }

    internal sealed record MessageSubscription(
        GameEventScriptMessageSignature Definition,
        int Priority,
        long RegistrationOrder,
        Action<GameEventScriptMessage, GameEventScriptContext>? Handler,
        GesBytecodeVmExecutable? ScriptExecutable,
        GesBytecodeVmCompiledHandler? ScriptHandler);

    #endregion
}

internal sealed class GameEventScriptHostRunState
{
    private readonly Queue<GameEventScriptMessage> _queue = new();
    private readonly object _queueGate = new();
    private readonly bool _enforceProcessedEventsLimit;
    private readonly int _maxProcessedEventsPerRun;
    private readonly int _maxQueuedMessagesPerRun;
    private int _processedEvents;
    private long _totalExecutedOpcodes;

    public GameEventScriptHostRunState(
        GameEventScriptRandomGenerator random,
        IGameEventScriptDiagnosticCollector? diagnosticCollector,
        Action<GameEventScriptMessage>? publishedMessageObserver,
        IGameEventScriptExtensionRegistry extensionRegistry,
        GameEventScriptRuntimeLimits runtimeLimits,
        bool enforceProcessedEventsLimit = true)
    {
        _enforceProcessedEventsLimit = enforceProcessedEventsLimit;
        _maxProcessedEventsPerRun = runtimeLimits.MaxProcessedEventsPerRun;
        _maxQueuedMessagesPerRun = runtimeLimits.MaxQueuedMessagesPerRun;
        PublishedMessageObserver = publishedMessageObserver;
        Context = new GameEventScriptContext(random, PublishInternal, diagnosticCollector, runtimeLimits, extensionRegistry);
    }

    public GameEventScriptContext Context { get; }

    public GameEventScriptMessage? ActiveMessage { get; private set; }

    public GameEventScriptHost.MessageSubscription[]? ActiveSubscriptions { get; private set; }

    public int ActiveSubscriptionIndex { get; set; }

    public GesBytecodeVmExecutionSession.Fiber? ActiveScriptFiber { get; private set; }

    public int StartedEventCount { get; private set; }

    public int StepExecutedOpcodes { get; private set; }

    public int StepProcessedMessages { get; private set; }

    public int StepPublishedMessages { get; private set; }

    public long TotalExecutedOpcodes => _totalExecutedOpcodes;

    public bool HasActiveDispatch => ActiveMessage is not null;

    public bool IsCompletedAndIdle => !HasActiveDispatch && PendingMessageCount == 0;

    public int PendingMessageCount
    {
        get
        {
            lock (_queueGate)
            {
                return _queue.Count;
            }
        }
    }

    private Action<GameEventScriptMessage>? PublishedMessageObserver { get; }

    public void BeginStep()
    {
        StepExecutedOpcodes = 0;
        StepProcessedMessages = 0;
        StepPublishedMessages = 0;
    }

    public void RecordExecutedOpcodes(int count)
    {
        if (count <= 0)
        {
            return;
        }

        StepExecutedOpcodes += count;
        _totalExecutedOpcodes += count;
    }

    public GameEventScriptRunStepResult CreateStepResult()
    {
        var state = Context.RuntimeBudget.IsExhausted
            ? GameEventScriptRunState.RuntimeLimitReached
            : IsCompletedAndIdle
                ? GameEventScriptRunState.Completed
                : GameEventScriptRunState.Paused;

        return new GameEventScriptRunStepResult(
            state,
            StepExecutedOpcodes,
            StepProcessedMessages,
            StepPublishedMessages);
    }

    public bool Enqueue(GameEventScriptMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Name))
        {
            return false;
        }

        lock (_queueGate)
        {
            if (_maxQueuedMessagesPerRun > 0 && _queue.Count >= _maxQueuedMessagesPerRun)
            {
                Context.RuntimeBudget.ReportLimit(
                    nameof(GameEventScriptRuntimeLimits.MaxQueuedMessagesPerRun),
                    $"Message queue limit reached. Dropped '{message.Name}'.",
                    _maxQueuedMessagesPerRun);
                return false;
            }

            _queue.Enqueue(message);
        }
        return true;
    }

    public bool TryDequeue(out GameEventScriptMessage? message)
    {
        lock (_queueGate)
        {
            if (_queue.Count == 0)
            {
                message = null;
                return false;
            }

            message = _queue.Dequeue();
            return true;
        }
    }

    public bool TryStartProcessingEvent()
    {
        if (_enforceProcessedEventsLimit && _maxProcessedEventsPerRun > 0 && _processedEvents >= _maxProcessedEventsPerRun)
        {
            return false;
        }

        _processedEvents++;
        StartedEventCount++;
        StepProcessedMessages++;
        return true;
    }

    public void StartDispatch(GameEventScriptMessage message, GameEventScriptHost.MessageSubscription[] subscriptions)
    {
        ActiveMessage = message;
        ActiveSubscriptions = subscriptions;
        ActiveSubscriptionIndex = 0;
        ActiveScriptFiber = null;
    }

    public void CompleteDispatch()
    {
        ActiveMessage = null;
        ActiveSubscriptions = null;
        ActiveSubscriptionIndex = 0;
        ActiveScriptFiber = null;
    }

    public void SetActiveScriptFiber(GesBytecodeVmExecutionSession.Fiber fiber)
        => ActiveScriptFiber = fiber;

    public void ClearActiveScriptFiber()
        => ActiveScriptFiber = null;

    public void RecordDiagnostic(GameEventScriptDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, GameEventScriptValue> arguments, string? detail = null)
        => Context.RecordDiagnostic(kind, name, arguments, detail);

    private bool PublishInternal(GameEventScriptMessage message)
    {
        if (!Enqueue(message))
        {
            return false;
        }

        StepPublishedMessages++;
        PublishedMessageObserver?.Invoke(message);
        RecordDiagnostic(GameEventScriptDiagnosticEventKind.EventPublished, message.Name, message.Arguments, $"Published '{message.Name}'");
        return true;
    }
}
