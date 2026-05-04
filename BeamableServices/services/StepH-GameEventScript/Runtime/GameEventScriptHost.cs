#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    private GameEventScriptStepController? _liveStepController;
    private Thread? _liveWorker;
    private bool _liveWorkerStarted;
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
        (_liveState, _liveStepController) = CreateLiveState(dispatchMode);
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

        Drain(state);
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

        GameEventScriptHostRunState state;
        GameEventScriptStepController stepController;
        Action? start = null;
        lock (_pumpGate)
        {
            ResetManualStateIfCompletedAndIdle();
            state = _liveState;
            stepController = _liveStepController ?? throw new InvalidOperationException("GameEventScript manual host is missing its step controller.");
            if (!_liveWorkerStarted)
            {
                _liveWorkerStarted = true;
                start = () => StartManualWorker(state, stepController);
            }
        }

        return stepController.Step(maxOpcodes, () => state.Context.RuntimeBudget.IsExhausted, start);
    }

    public GameEventScriptRun BeginRun(GameEventScriptMessage message)
    {
        var stepController = new GameEventScriptStepController();
        var state = new GameEventScriptHostRunState(_random, _diagnosticCollector, _publishedMessageObserver, _extensionRegistry, _runtimeLimits, stepController);
        var accepted = !string.IsNullOrWhiteSpace(message.Name) && state.Enqueue(message);
        return new GameEventScriptRun(Drain, state, stepController, accepted);
    }

    #endregion

    #region Internals

    private (GameEventScriptHostRunState State, GameEventScriptStepController? StepController) CreateLiveState(GameEventScriptDispatchMode dispatchMode)
    {
        if (dispatchMode == GameEventScriptDispatchMode.Automatic)
        {
            return (new GameEventScriptHostRunState(_random, _diagnosticCollector, _publishedMessageObserver, _extensionRegistry, _runtimeLimits, enforceProcessedEventsLimit: false), null);
        }

        var stepController = new GameEventScriptStepController();
        return (new GameEventScriptHostRunState(_random, _diagnosticCollector, _publishedMessageObserver, _extensionRegistry, _runtimeLimits, stepController, enforceProcessedEventsLimit: false), stepController);
    }

    private void ResetManualStateIfCompletedAndIdle()
    {
        if (_dispatchMode != GameEventScriptDispatchMode.Manual ||
            _liveStepController is null ||
            !_liveStepController.IsCompleted ||
            _liveState.PendingMessageCount != 0)
        {
            return;
        }

        (_liveState, _liveStepController) = CreateLiveState(GameEventScriptDispatchMode.Manual);
        _liveWorker = null;
        _liveWorkerStarted = false;
    }

    private void StartManualWorker(GameEventScriptHostRunState state, GameEventScriptStepController stepController)
    {
        _liveWorker = new Thread(() =>
        {
            try
            {
                Drain(state);
                stepController.Complete();
            }
            catch (Exception exception)
            {
                stepController.Fault(exception);
            }
        })
        {
            IsBackground = true,
            Name = "GameEventScript manual update pump"
        };
        _liveWorker.Start();
    }

    private void RunAutomaticDispatchSlice()
    {
        try
        {
            DrainOne(_liveState);
        }
        finally
        {
            var shouldRestart = false;
            lock (_pumpGate)
            {
                shouldRestart = _dispatchMode == GameEventScriptDispatchMode.Automatic &&
                                _liveState.PendingMessageCount > 0;
                if (shouldRestart)
                {
                    _automaticDispatchScheduled = true;
                }
                else
                {
                    _automaticDispatchScheduled = false;
                    if (_dispatchMode == GameEventScriptDispatchMode.Automatic)
                    {
                        (_liveState, _liveStepController) = CreateLiveState(GameEventScriptDispatchMode.Automatic);
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

    private void RegisterLocked(
        GameEventScriptMessageSignature signature,
        int priority,
        Action<GameEventScriptMessage, GameEventScriptContext> handler)
    {
        var subscription = new MessageSubscription(
            signature,
            priority,
            _nextRegistrationOrder++,
            handler);

        _dispatchIndex[subscription.Definition.SignatureId] = _dispatchIndex.TryGetValue(subscription.Definition.SignatureId, out var handlers)
            ? InsertSubscriptionByDispatchOrder(handlers, subscription)
            : [subscription];
    }

    private void Drain(GameEventScriptHostRunState state)
    {
        while (state.TryDequeue(out var queuedEvent))
        {
            DrainDequeued(state, queuedEvent);
        }
    }

    private void DrainOne(GameEventScriptHostRunState state)
    {
        if (state.TryDequeue(out var queuedEvent))
        {
            DrainDequeued(state, queuedEvent);
        }
    }

    private void DrainDequeued(GameEventScriptHostRunState state, GameEventScriptMessage? queuedEvent)
    {
        if (!state.TryStartProcessingEvent())
        {
            return;
        }

        if (queuedEvent is not null)
        {
            Dispatch(queuedEvent, state);
        }
    }

    private void Dispatch(GameEventScriptMessage queuedEvent, GameEventScriptHostRunState state)
    {
        state.RecordDiagnostic(GameEventScriptDiagnosticEventKind.DispatchStarted, queuedEvent.Name, queuedEvent.Arguments, $"Dispatch '{queuedEvent.Name}' started");

        if (!TryGetSubscriptions(queuedEvent.SignatureId, out var subscriptions))
        {
            state.RecordDiagnostic(GameEventScriptDiagnosticEventKind.DispatchCompleted, queuedEvent.Name, queuedEvent.Arguments, $"Dispatch '{queuedEvent.Name}' completed without subscribers");
            return;
        }

        foreach (var subscription in subscriptions)
        {
            state.RecordDiagnostic(GameEventScriptDiagnosticEventKind.SubscriberMatched, queuedEvent.Name, queuedEvent.Arguments, $"Subscriber matched: {subscription.Definition.SignatureId}");

            try
            {
                state.RecordDiagnostic(GameEventScriptDiagnosticEventKind.SubscriberInvoked, queuedEvent.Name, queuedEvent.Arguments, "Subscriber invoked");
                subscription.Handler(queuedEvent, state.Context);
            }
            catch (GameEventScriptFatalRuntimeException)
            {
                throw;
            }
            catch
            {
                // Runtime dispatch must remain lenient.
            }
        }

        state.RecordDiagnostic(GameEventScriptDiagnosticEventKind.DispatchCompleted, queuedEvent.Name, queuedEvent.Arguments, $"Dispatch '{queuedEvent.Name}' completed");
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

    private sealed record MessageSubscription(
        GameEventScriptMessageSignature Definition,
        int Priority,
        long RegistrationOrder,
        Action<GameEventScriptMessage, GameEventScriptContext> Handler);

    #endregion
}

internal sealed class GameEventScriptHostRunState
{
    private readonly Queue<GameEventScriptMessage> _queue = new();
    private readonly object _queueGate = new();
    private readonly bool _enforceProcessedEventsLimit;
    private readonly int _maxProcessedEventsPerRun;
    private readonly int _maxQueuedMessagesPerRun;
    private readonly GameEventScriptStepController? _stepController;
    private int _processedEvents;

    public GameEventScriptHostRunState(
        GameEventScriptRandomGenerator random,
        IGameEventScriptDiagnosticCollector? diagnosticCollector,
        Action<GameEventScriptMessage>? publishedMessageObserver,
        IGameEventScriptExtensionRegistry extensionRegistry,
        GameEventScriptRuntimeLimits runtimeLimits,
        GameEventScriptStepController? stepController = null,
        bool enforceProcessedEventsLimit = true)
    {
        _enforceProcessedEventsLimit = enforceProcessedEventsLimit;
        _maxProcessedEventsPerRun = runtimeLimits.MaxProcessedEventsPerRun;
        _maxQueuedMessagesPerRun = runtimeLimits.MaxQueuedMessagesPerRun;
        _stepController = stepController;
        PublishedMessageObserver = publishedMessageObserver;
        Context = new GameEventScriptContext(random, PublishInternal, diagnosticCollector, runtimeLimits, extensionRegistry, stepController);
    }

    public GameEventScriptContext Context { get; }

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
        _stepController?.RecordProcessedMessage();
        return true;
    }

    public void RecordDiagnostic(GameEventScriptDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, GameEventScriptValue> arguments, string? detail = null)
        => Context.RecordDiagnostic(kind, name, arguments, detail);

    private bool PublishInternal(GameEventScriptMessage message)
    {
        if (!Enqueue(message))
        {
            return false;
        }

        _stepController?.RecordPublishedMessage();
        PublishedMessageObserver?.Invoke(message);
        RecordDiagnostic(GameEventScriptDiagnosticEventKind.EventPublished, message.Name, message.Arguments, $"Published '{message.Name}'");
        return true;
    }
}
