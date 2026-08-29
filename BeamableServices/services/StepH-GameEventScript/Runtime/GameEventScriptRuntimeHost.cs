#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptMessageSignature;

namespace StepH.GameEventScript.Runtime;

internal sealed class GameEventScriptRuntimeHost
{
    private const int NormalPriority = 0;

    private readonly GameEventScriptRandomGenerator _random;
    private readonly IGameEventScriptRuntimeObserver? _runtimeObserver;
    private readonly IGameEventScriptExtensionRegistry _extensionRegistry;
    private readonly IGameEventScriptExternalTypeRegistry _externalTypeRegistry;
    private readonly GameEventScriptRuntimeLimits _runtimeLimits;
    private readonly IGameEventScriptDispatcher? _dispatcher;
    private readonly IGameEventScriptRuntimeGate? _runtimeGate;
    private readonly Func<GameEventScriptMessage, bool>? _publishHook;
    private readonly Dictionary<string, MessageSubscription[]> _dispatchIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MessageSubscription[]> _messageNameDispatchIndex = new(StringComparer.Ordinal);
    private MessageSubscription[] _initializationSubscriptions = [];
    private GameEventScriptHostRunState _liveState;
    private bool _liveStateInitializationQueued;
    private bool _automaticDispatchScheduled;
    private long _nextRegistrationOrder;
    private const int DispatchEnqueueMatched = 1;
    private const int DispatchEnqueueAccepted = 2;

    internal GameEventScriptRuntimeHost(GameEventScriptRandomGenerator random, IGameEventScriptRuntimeObserver? runtimeObserver,
        IGameEventScriptExtensionRegistry? extensionRegistry,
        IGameEventScriptExternalTypeRegistry? externalTypeRegistry,
        GameEventScriptRuntimeLimits? runtimeLimits,
        IGameEventScriptDispatcher? dispatcher = null,
        IGameEventScriptRuntimeGate? runtimeGate = null,
        Func<GameEventScriptMessage, bool>? publishHook = null)
    {
        _random = random;
        _runtimeObserver = runtimeObserver;
        _extensionRegistry = extensionRegistry ?? GameEventScriptEmptyExtensionRegistry.Instance;
        _externalTypeRegistry = externalTypeRegistry ?? GameEventScriptEmptyExternalTypeRegistry.Instance;
        _runtimeLimits = runtimeLimits ?? GameEventScriptRuntimeLimits.Default;
        _dispatcher = dispatcher;
        _runtimeGate = runtimeGate;
        _publishHook = publishHook;
        _liveState = CreateLiveState();
    }

    #region Public interface

    public GameEventScriptRuntimeHost Load(IGameEventScriptModule module, int priority = NormalPriority)
    {
        _ = module ?? throw new ArgumentNullException(nameof(module));
        module.Bind(_extensionRegistry, _externalTypeRegistry);

        RegisterMany(module, priority);
        return this;
    }

    public GameEventScriptRuntimeHost Subscribe(string message, IReadOnlyCollection<string> parameterNames, Action<GameEventScriptMessage, GameEventScriptSession> handler, int priority = NormalPriority)
        => Subscribe(Create(!string.IsNullOrWhiteSpace(message) ? message : throw new ArgumentException("Message must not be null or whitespace", nameof(message)),
            parameterNames ?? throw new ArgumentNullException(nameof(parameterNames))), handler, priority);

    public GameEventScriptRuntimeHost Subscribe(GameEventScriptMessageSignature signature, Action<GameEventScriptMessage, GameEventScriptSession> handler, int priority = NormalPriority)
    {
        Register(
            signature ?? throw new ArgumentNullException(nameof(signature)),
            priority,
            handler ?? throw new ArgumentNullException(nameof(handler)));
        return this;
    }

    public GameEventScriptRuntimeHost Subscribe(GameEventScriptMessageHandlerDescriptor handler, int priority = NormalPriority)
    {
        _ = handler ?? throw new ArgumentNullException(nameof(handler));
        Register(handler, priority);

        return this;
    }

    public GameEventScriptRuntimeHost Subscribe(
        GameEventScriptMessageSignature signature,
        Action<GameEventScriptMessage, GameEventScriptSession> handler,
        IReadOnlyCollection<string>? matchingTags,
        IReadOnlyCollection<string>? withoutTags = null,
        int priority = NormalPriority)
    {
        Register(
            signature ?? throw new ArgumentNullException(nameof(signature)),
            priority,
            handler ?? throw new ArgumentNullException(nameof(handler)),
            matchingTags,
            withoutTags);
        return this;
    }

    public GameEventScriptRuntimeHost Subscribe(IGameEventScriptModule module, int priority = NormalPriority)
    {
        _ = module ?? throw new ArgumentNullException(nameof(module));
        module.Bind(_extensionRegistry, _externalTypeRegistry);

        RegisterMany(module, priority);
        return this;
    }

    public GameEventScriptSession StartSession()
    {
        var state = CreateLiveState();
        EnqueueInitializationInvocations(state);
        state.Session.ScheduleAutomaticDispatchIfNeeded();
        return state.Session;
    }

    public bool Publish(GameEventScriptMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Name))
        {
            return false;
        }

        var shouldScheduleAutomaticDispatch = false;
        ResetManualStateIfCompletedAndIdle();
        EnqueueLiveStateInitializationIfNeeded();
        if (!EnqueueInvocations(_liveState, message))
        {
            return false;
        }

        if (_dispatcher is not null && !_automaticDispatchScheduled)
        {
            _automaticDispatchScheduled = true;
            shouldScheduleAutomaticDispatch = true;
        }

        if (shouldScheduleAutomaticDispatch)
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

        return true;
    }

    public bool PublishToCompletion(GameEventScriptMessage message)
    {
        if (!Publish(message)) return false;
        RunToCompletion();
        return true;
    }

    public GameEventScriptRun? Dispatch()
    {
        if (_liveState.IsCompletedAndIdle)
        {
            return null;
        }

        return new GameEventScriptRun(DrainSlice, _liveState, accepted: true, _runtimeGate);
    }

    public GameEventScriptRunStepResult Update(int maxOpcodes)
    {
        if (_dispatcher is not null)
        {
            throw new InvalidOperationException("GameEventScript automatic dispatch hosts cannot be stepped manually.");
        }

        if (maxOpcodes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxOpcodes), "Update opcode budget must be greater than zero.");
        }

        var run = Dispatch();
        return run is null
            ? new GameEventScriptRunStepResult(GameEventScriptRunState.Completed, 0, 0)
            : run.Execute(maxOpcodes);
    }

    public GameEventScriptRun BeginRun(GameEventScriptMessage message)
    {
        var state = new GameEventScriptHostRunState(this, _dispatcher, _runtimeGate, _random, _runtimeObserver, _extensionRegistry, _runtimeLimits, queuePublisher: EnqueueInvocations, publishHook: _publishHook);
        EnqueueInitializationInvocations(state);
        var accepted = EnqueueInvocations(state, message);
        return new GameEventScriptRun(DrainSlice, state, accepted, _runtimeGate);
    }

    #endregion

    #region Internals

    private GameEventScriptHostRunState CreateLiveState()
        => new(this, _dispatcher, _runtimeGate, _random, _runtimeObserver, _extensionRegistry, _runtimeLimits, queuePublisher: EnqueueInvocations, publishHook: _publishHook);

    internal bool EnqueueSessionInvocations(GameEventScriptHostRunState state, GameEventScriptMessage message)
        => EnqueueInvocations(state, message);

    internal void DrainSessionToCompletion(GameEventScriptHostRunState state)
        => DrainToCompletion(state);

    internal void DrainSessionOneToCompletion(GameEventScriptHostRunState state)
        => DrainOneToCompletion(state);

    internal GameEventScriptRunStepResult DrainSessionSlice(GameEventScriptHostRunState state, int maxOpcodes)
        => DrainSlice(state, maxOpcodes);

    private void ResetManualStateIfCompletedAndIdle()
    {
        if (_dispatcher is not null ||
            !_liveState.IsCompletedAndIdle)
        {
            return;
        }

        _liveState = CreateLiveState();
        _liveStateInitializationQueued = false;
    }

    private void EnqueueLiveStateInitializationIfNeeded()
    {
        if (_liveStateInitializationQueued)
        {
            return;
        }

        EnqueueInitializationInvocations(_liveState);
        _liveStateInitializationQueued = true;
    }

    private void RunAutomaticDispatchSlice()
    {
        try
        {
            RunToCompletion();
        }
        finally
        {
            _automaticDispatchScheduled = false;
            if (_dispatcher is not null && _liveState.IsCompletedAndIdle)
            {
                _liveState = CreateLiveState();
                _liveStateInitializationQueued = false;
            }
        }
    }

    private void RunToCompletion()
    {
        while (Dispatch() is { } run)
        {
            var result = run.ExecuteAll();
            if (result.State == GameEventScriptRunState.RuntimeLimitReached ||
                (result.ExecutedOpcodes == 0 && result.PublishedMessages == 0))
            {
                break;
            }
        }
    }

    private void Register(
        GameEventScriptMessageSignature signature,
        int priority,
        Action<GameEventScriptMessage, GameEventScriptSession> handler)
        => Register(signature, priority, handler, [], []);

    private void RegisterMany(
        IGameEventScriptModule module,
        int priority)
    {
        foreach (var handler in module.Handlers)
        {
            _ = handler ?? throw new ArgumentException("Module contains a null handler.", nameof(module));
            _ = handler.Signature ?? throw new ArgumentException("Module contains a handler with a null signature.", nameof(module));
            Register(handler, priority);
        }
    }

    private void Register(
        GameEventScriptMessageSignature signature,
        int priority,
        Action<GameEventScriptMessage, GameEventScriptSession> handler,
        IReadOnlyCollection<string>? matchingTags = null,
        IReadOnlyCollection<string>? withoutTags = null)
        => Register(new GameEventScriptMessageHandlerDescriptor(signature, handler, matchingTags, withoutTags), priority);

    private void Register(GameEventScriptMessageHandlerDescriptor handler, int priority)
    {
        var subscription = new MessageSubscription(
            handler,
            priority,
            _nextRegistrationOrder++);

        if (GameEventScriptSystemEndpoints.IsInitializationName(subscription.Definition.Name))
        {
            _initializationSubscriptions = InsertSubscriptionByDispatchOrder(_initializationSubscriptions, subscription);
            return;
        }

        RegisterDispatchSubscription(subscription);
    }

    private void RegisterDispatchSubscription(MessageSubscription subscription)
    {
        if (!subscription.MatchArguments)
        {
            _messageNameDispatchIndex[subscription.Definition.Name] =
                _messageNameDispatchIndex.TryGetValue(subscription.Definition.Name, out var wildcardHandlers)
                    ? InsertSubscriptionByDispatchOrder(wildcardHandlers, subscription)
                    : [subscription];
            return;
        }

        _dispatchIndex[subscription.Definition.SignatureId] =
            _dispatchIndex.TryGetValue(subscription.Definition.SignatureId, out var exactHandlers)
                ? InsertSubscriptionByDispatchOrder(exactHandlers, subscription)
                : [subscription];
    }

    private void DrainToCompletion(GameEventScriptHostRunState state)
    {
        while (!state.IsCompletedAndIdle && !state.Session.RuntimeBudget.IsExhausted)
        {
            DrainSlice(state, int.MaxValue, stopAfterStartedEventCount: null, allowSynchronousScriptFastPath: true);
        }
    }

    private void DrainOneToCompletion(GameEventScriptHostRunState state)
    {
        var startedEventCount = state.StartedEventCount;
        do
        {
            DrainSlice(state, int.MaxValue, stopAfterStartedEventCount: startedEventCount + 1, allowSynchronousScriptFastPath: true);
        }
        while (!state.IsCompletedAndIdle &&
               !state.Session.RuntimeBudget.IsExhausted &&
               (state.HasActiveDispatch || state.StartedEventCount <= startedEventCount));
    }

    private GameEventScriptRunStepResult DrainSlice(GameEventScriptHostRunState state, int maxOpcodes)
    {
        return DrainSlice(state, maxOpcodes, stopAfterStartedEventCount: null, allowSynchronousScriptFastPath: false);
    }

    private GameEventScriptRunStepResult DrainSlice(
        GameEventScriptHostRunState state,
        int maxOpcodes,
        int? stopAfterStartedEventCount,
        bool allowSynchronousScriptFastPath)
    {
        state.BeginStep();
        var remainingOpcodes = maxOpcodes;
        while (remainingOpcodes > 0 && !state.Session.RuntimeBudget.IsExhausted)
        {
            if (!state.HasActiveDispatch)
            {
                if (stopAfterStartedEventCount is { } stopAfter && state.StartedEventCount >= stopAfter)
                {
                    break;
                }

                if (!state.CanStartDelivery)
                {
                    break;
                }

                var queuedInvocation = state.Dequeue();
                if (queuedInvocation is null)
                {
                    break;
                }

                state.StartDelivery();
                StartDispatch(state, queuedInvocation);
                if (!state.HasActiveDispatch)
                {
                    continue;
                }
            }

            var before = state.StepExecutedOpcodes;
            RunActiveDispatch(state, remainingOpcodes, allowSynchronousScriptFastPath);
            var consumed = state.StepExecutedOpcodes - before;
            remainingOpcodes -= consumed;

            if (state.HasActiveDispatch && consumed == 0)
            {
                break;
            }
        }

        return state.CreateStepResult();
    }

    private void StartDispatch(GameEventScriptHostRunState state, QueuedInvocation queuedInvocation)
    {
        var queuedEvent = queuedInvocation.Message;
        state.RecordDispatchStarted(queuedEvent, queuedInvocation.Subscription.DispatchSignatureId);
        state.StartDispatch(queuedInvocation);
    }

    private void RunActiveDispatch(GameEventScriptHostRunState state, int maxOpcodes, bool allowSynchronousScriptFastPath)
    {
        var remainingOpcodes = maxOpcodes;
        var subscription = state.ActiveSubscription!;
        var activeMessage = state.ActiveMessage!;
        var dispatchSignatureId = subscription.DispatchSignatureId;
        if (state.ActiveInvocation is not null)
        {
            var executed = RunMessageInvocationSlice(state, state.ActiveInvocation, remainingOpcodes);
            if (!state.ActiveInvocation.IsCompleted)
            {
                return;
            }

            state.ClearActiveInvocation();
            state.RecordDispatchCompleted(activeMessage, dispatchSignatureId);
            state.CompleteDispatch();
            return;
        }

        try
        {
            var invocation = subscription.Handler.Invoke(activeMessage, state.Session);
            state.SetActiveInvocation(invocation);
            var executed = RunMessageInvocationSlice(
                state,
                invocation,
                allowSynchronousScriptFastPath ? int.MaxValue : remainingOpcodes);
            if (!invocation.IsCompleted)
            {
                return;
            }

            state.ClearActiveInvocation();
        }
        catch (GameEventScriptFatalRuntimeException)
        {
            throw;
        }
        catch
        {
            // Runtime dispatch must remain lenient.
            state.ClearActiveInvocation();
        }

        state.RecordDispatchCompleted(activeMessage, dispatchSignatureId);
        state.CompleteDispatch();
    }

    private static int RunMessageInvocationSlice(GameEventScriptHostRunState state, IGameEventScriptMessageInvocation invocation, int maxOpcodes)
    {
        var executed = invocation.RunSlice(maxOpcodes);
        state.RecordExecutedOpcodes(executed);
        return executed;
    }

    private MessageSubscription[]? GetSubscriptions(string signatureId)
        => _dispatchIndex.TryGetValue(signatureId, out var subscriptions) ? subscriptions : null;

    private MessageSubscription[]? GetNameSubscriptions(string messageName)
        => _messageNameDispatchIndex.TryGetValue(messageName, out var subscriptions) ? subscriptions : null;

    private bool EnqueueInvocations(GameEventScriptHostRunState state, GameEventScriptMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Name))
        {
            return false;
        }

        if (GameEventScriptSystemEndpoints.IsInitializationName(message.Name))
        {
            return false;
        }

        var exactSubscriptions = GetSubscriptions(message.SignatureId) ?? [];
        var nameSubscriptions = GetNameSubscriptions(message.Name) ?? [];
        var hasExactSubscriptions = exactSubscriptions.Length > 0;
        var hasNameSubscriptions = nameSubscriptions.Length > 0;
        if (!hasExactSubscriptions && !hasNameSubscriptions)
        {
            return EnqueueUndeliverableInvocation(state, message);
        }

        var enqueueResult = EnqueueMatchingDispatchSubscriptions(
            state,
            message,
            exactSubscriptions,
            hasExactSubscriptions,
            nameSubscriptions,
            hasNameSubscriptions);

        return (enqueueResult & DispatchEnqueueMatched) != 0
            ? (enqueueResult & DispatchEnqueueAccepted) != 0
            : EnqueueUndeliverableInvocation(state, message);
    }

    private bool EnqueueUndeliverableInvocation(GameEventScriptHostRunState state, GameEventScriptMessage message)
    {
        if (GameEventScriptSystemEndpoints.IsUndeliverableName(message.Name))
        {
            return false;
        }

        var exactSubscriptions = GetSubscriptions(GameEventScriptSystemEndpoints.UndeliverableSignatureId) ?? [];
        var nameSubscriptions = GetNameSubscriptions(GameEventScriptSystemEndpoints.UndeliverableName) ?? [];
        var hasExactSubscriptions = exactSubscriptions.Length > 0;
        var hasNameSubscriptions = nameSubscriptions.Length > 0;
        if (!hasExactSubscriptions && !hasNameSubscriptions)
        {
            return false;
        }

        var enqueueResult = EnqueueMatchingDispatchSubscriptions(
            state,
            message,
            exactSubscriptions,
            hasExactSubscriptions,
            nameSubscriptions,
            hasNameSubscriptions);

        return (enqueueResult & DispatchEnqueueMatched) != 0 &&
               (enqueueResult & DispatchEnqueueAccepted) != 0;
    }

    private void EnqueueInitializationInvocations(GameEventScriptHostRunState state)
    {
        if (_initializationSubscriptions.Length == 0)
        {
            return;
        }

        var message = GameEventScriptSystemEndpoints.CreateInitializationMessage();
        foreach (var subscription in _initializationSubscriptions)
        {
            if (!subscription.MatchesTags(message))
            {
                continue;
            }

            state.Enqueue(new QueuedInvocation(message, subscription));
        }
    }

    private static int EnqueueMatchingDispatchSubscriptions(
        GameEventScriptHostRunState state,
        GameEventScriptMessage message,
        MessageSubscription[] exactSubscriptions,
        bool hasExactSubscriptions,
        MessageSubscription[] nameSubscriptions,
        bool hasNameSubscriptions)
    {
        var result = 0;
        if (!hasExactSubscriptions)
        {
            for (var index = 0; index < nameSubscriptions.Length; index++)
            {
                var subscription = nameSubscriptions[index];
                if (!subscription.MatchesTags(message))
                {
                    continue;
                }

                result |= DispatchEnqueueMatched;
                if (state.Enqueue(new QueuedInvocation(message, subscription)))
                {
                    result |= DispatchEnqueueAccepted;
                }
            }

            return result;
        }

        if (!hasNameSubscriptions)
        {
            for (var index = 0; index < exactSubscriptions.Length; index++)
            {
                var subscription = exactSubscriptions[index];
                if (!subscription.MatchesTags(message))
                {
                    continue;
                }

                result |= DispatchEnqueueMatched;
                if (state.Enqueue(new QueuedInvocation(message, subscription)))
                {
                    result |= DispatchEnqueueAccepted;
                }
            }

            return result;
        }

        var exactIndex = 0;
        var nameIndex = 0;
        while (exactIndex < exactSubscriptions.Length && nameIndex < nameSubscriptions.Length)
        {
            MessageSubscription subscription;
            if (CompareDispatchOrder(exactSubscriptions[exactIndex], nameSubscriptions[nameIndex]) <= 0)
            {
                subscription = exactSubscriptions[exactIndex++];
            }
            else
            {
                subscription = nameSubscriptions[nameIndex++];
            }

            if (!subscription.MatchesTags(message))
            {
                continue;
            }

            result |= DispatchEnqueueMatched;
            if (state.Enqueue(new QueuedInvocation(message, subscription)))
            {
                result |= DispatchEnqueueAccepted;
            }
        }

        while (exactIndex < exactSubscriptions.Length)
        {
            var subscription = exactSubscriptions[exactIndex++];
            if (!subscription.MatchesTags(message))
            {
                continue;
            }

            result |= DispatchEnqueueMatched;
            if (state.Enqueue(new QueuedInvocation(message, subscription)))
            {
                result |= DispatchEnqueueAccepted;
            }
        }

        while (nameIndex < nameSubscriptions.Length)
        {
            var subscription = nameSubscriptions[nameIndex++];
            if (!subscription.MatchesTags(message))
            {
                continue;
            }

            result |= DispatchEnqueueMatched;
            if (state.Enqueue(new QueuedInvocation(message, subscription)))
            {
                result |= DispatchEnqueueAccepted;
            }
        }

        return result;
    }

    private static MessageSubscription[] InsertSubscriptionByDispatchOrder(MessageSubscription[] handlers, MessageSubscription subscription)
    {
        var insertIndex = -1;
        for (var index = 0; index < handlers.Length; index++)
        {
            if (CompareDispatchOrder(subscription, handlers[index]) < 0)
            {
                insertIndex = index;
                break;
            }
        }

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
        GameEventScriptMessageHandlerDescriptor Handler,
        int Priority,
        long RegistrationOrder)
    {
        public GameEventScriptMessageSignature Definition => Handler.Signature;

        public bool MatchArguments => Handler.MatchArguments;

        public string DispatchSignatureId => Handler.DispatchSignatureId;

        public bool MatchesTags(GameEventScriptMessage message)
        {
            foreach (var tag in Handler.RequiredTags)
            {
                if (!message.HasTag(tag))
                {
                    return false;
                }
            }

            foreach (var tag in Handler.ExcludedTags)
            {
                if (message.HasTag(tag))
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal sealed record QueuedInvocation(GameEventScriptMessage Message, MessageSubscription Subscription);

    #endregion
}

internal sealed class GameEventScriptHostRunState
{
    private readonly Queue<GameEventScriptRuntimeHost.QueuedInvocation> _queue = new();
    private readonly Func<GameEventScriptHostRunState, GameEventScriptMessage, bool> _queuePublisher;
    private readonly Func<GameEventScriptMessage, bool>? _publishHook;
    private readonly bool _enforceProcessedEventsLimit;
    private readonly int _maxProcessedEventsPerRun;
    private readonly int _maxQueuedMessagesPerRun;
    private int _deliveredInvocations;
    private long _totalExecutedOpcodes;

    public GameEventScriptHostRunState(
        GameEventScriptRuntimeHost host,
        IGameEventScriptDispatcher? dispatcher,
        IGameEventScriptRuntimeGate? runtimeGate,
        GameEventScriptRandomGenerator random,
        IGameEventScriptRuntimeObserver? runtimeObserver,
        IGameEventScriptExtensionRegistry extensionRegistry,
        GameEventScriptRuntimeLimits runtimeLimits,
        bool enforceProcessedEventsLimit = true,
        Func<GameEventScriptHostRunState, GameEventScriptMessage, bool>? queuePublisher = null,
        Func<GameEventScriptMessage, bool>? publishHook = null)
    {
        _enforceProcessedEventsLimit = enforceProcessedEventsLimit;
        _maxProcessedEventsPerRun = runtimeLimits.MaxProcessedEventsPerRun;
        _maxQueuedMessagesPerRun = runtimeLimits.MaxQueuedMessagesPerRun;
        _queuePublisher = queuePublisher ?? ((_, _) => false);
        _publishHook = publishHook;
        RuntimeObserver = runtimeObserver;
        Session = new GameEventScriptSession(host, this, dispatcher, runtimeGate, random, EmitInternal, runtimeLimits, extensionRegistry, PublishInternal, runtimeObserver);
    }

    public GameEventScriptSession Session { get; }

    public GameEventScriptMessage? ActiveMessage { get; private set; }

    public GameEventScriptRuntimeHost.MessageSubscription? ActiveSubscription { get; private set; }

    public IGameEventScriptMessageInvocation? ActiveInvocation { get; private set; }

    public int StartedEventCount { get; private set; }

    public int StepExecutedOpcodes { get; private set; }

    public int StepPublishedMessages { get; private set; }

    public long TotalExecutedOpcodes => _totalExecutedOpcodes;

    public bool HasActiveDispatch => ActiveMessage is not null;

    public bool IsCompletedAndIdle => !HasActiveDispatch && PendingMessageCount == 0;

    public int PendingMessageCount => _queue.Count;

    private IGameEventScriptRuntimeObserver? RuntimeObserver { get; }

    public void BeginStep()
    {
        StepExecutedOpcodes = 0;
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
        var state = Session.RuntimeBudget.IsExhausted
            ? GameEventScriptRunState.RuntimeLimitReached
            : IsCompletedAndIdle
                ? GameEventScriptRunState.Completed
                : GameEventScriptRunState.Paused;

        return new GameEventScriptRunStepResult(
            state,
            StepExecutedOpcodes,
            StepPublishedMessages);
    }

    public bool Enqueue(GameEventScriptRuntimeHost.QueuedInvocation queuedInvocation)
    {
        var message = queuedInvocation.Message;
        if (string.IsNullOrWhiteSpace(message.Name))
        {
            return false;
        }

        if (_maxQueuedMessagesPerRun > 0 && _queue.Count >= _maxQueuedMessagesPerRun)
        {
            Session.RuntimeBudget.ReportLimit(
                nameof(GameEventScriptRuntimeLimits.MaxQueuedMessagesPerRun),
                $"Message queue limit reached. Dropped '{message.Name}'.",
                _maxQueuedMessagesPerRun);
            return false;
        }

        _queue.Enqueue(queuedInvocation);
        return true;
    }

    public GameEventScriptRuntimeHost.QueuedInvocation? Dequeue()
    {
        if (_queue.Count == 0)
        {
            return null;
        }

        return _queue.Dequeue();
    }

    public bool CanStartDelivery
        => !_enforceProcessedEventsLimit ||
           _maxProcessedEventsPerRun <= 0 ||
           _deliveredInvocations < _maxProcessedEventsPerRun;

    public void StartDelivery()
    {
        _deliveredInvocations++;
        StartedEventCount++;
    }

    public void StartDispatch(GameEventScriptRuntimeHost.QueuedInvocation queuedInvocation)
    {
        ActiveMessage = queuedInvocation.Message;
        ActiveSubscription = queuedInvocation.Subscription;
        ActiveInvocation = null;
    }

    public void CompleteDispatch()
    {
        ActiveMessage = null;
        ActiveSubscription = null;
        ActiveInvocation = null;
    }

    public void SetActiveInvocation(IGameEventScriptMessageInvocation invocation)
        => ActiveInvocation = invocation;

    public void ClearActiveInvocation()
        => ActiveInvocation = null;

    public void RecordDispatchStarted(GameEventScriptMessage message, string dispatchSignatureId)
        => RuntimeObserver?.DispatchStarted(message, dispatchSignatureId);

    public void RecordDispatchCompleted(GameEventScriptMessage message, string dispatchSignatureId)
        => RuntimeObserver?.DispatchCompleted(message, dispatchSignatureId);

    private bool EmitInternal(GameEventScriptMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Name))
        {
            return false;
        }

        var queued = _queuePublisher(this, message);
        RecordScriptMessageOutput(message, publish: false, queued);
        return queued;
    }

    private bool PublishInternal(GameEventScriptMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Name))
        {
            return false;
        }

        var queued = _publishHook is null
            ? _queuePublisher(this, message)
            : _publishHook(message);
        RecordScriptMessageOutput(message, publish: true, queued);
        return queued;
    }

    private void RecordScriptMessageOutput(GameEventScriptMessage message, bool publish, bool accepted)
    {
        StepPublishedMessages++;
        if (publish)
        {
            RuntimeObserver?.MessagePublished(message, accepted);
        }
        else
        {
            RuntimeObserver?.MessageEmitted(message, accepted);
        }
    }
}
