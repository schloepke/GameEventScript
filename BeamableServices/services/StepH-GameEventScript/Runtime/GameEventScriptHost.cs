#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptMessageSignature;

namespace StepH.GameEventScript.Runtime;

public sealed class GameEventScriptHost
{
    private const int NormalPriority = 0;

    private readonly GameEventScriptRandomGenerator _random;
    private readonly IGameEventScriptRuntimeObserver? _runtimeObserver;
    private readonly IGameEventScriptExtensionRegistry _extensionRegistry;
    private readonly IGameEventScriptExternalTypeRegistry _externalTypeRegistry;
    private readonly GameEventScriptRuntimeLimits _runtimeLimits;
    private readonly GameEventScriptDispatchMode _dispatchMode;
    private readonly IGameEventScriptDispatcher? _dispatcher;
    private readonly Func<GameEventScriptMessage, bool>? _publishHook;
    private readonly Dictionary<string, MessageSubscription[]> _dispatchIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MessageSubscription[]> _messageNameDispatchIndex = new(StringComparer.Ordinal);
    private MessageSubscription[] _initializationSubscriptions = [];
    private readonly object _dispatchGate = new();
    private readonly object _pumpGate = new();
    private GameEventScriptHostRunState _liveState;
    private bool _liveStateInitializationQueued;
    private bool _automaticDispatchScheduled;
    private long _nextRegistrationOrder;

    internal GameEventScriptHost(GameEventScriptRandomGenerator random, IGameEventScriptRuntimeObserver? runtimeObserver,
        IGameEventScriptExtensionRegistry? extensionRegistry,
        IGameEventScriptExternalTypeRegistry? externalTypeRegistry,
        GameEventScriptRuntimeLimits? runtimeLimits,
        GameEventScriptDispatchMode dispatchMode = GameEventScriptDispatchMode.Manual,
        IGameEventScriptDispatcher? dispatcher = null,
        Func<GameEventScriptMessage, bool>? publishHook = null)
    {
        _random = random;
        _runtimeObserver = runtimeObserver;
        _extensionRegistry = extensionRegistry ?? GameEventScriptEmptyExtensionRegistry.Instance;
        _externalTypeRegistry = externalTypeRegistry ?? GameEventScriptEmptyExternalTypeRegistry.Instance;
        _runtimeLimits = runtimeLimits ?? GameEventScriptRuntimeLimits.Default;
        _dispatchMode = dispatchMode;
        _dispatcher = dispatcher;
        _publishHook = publishHook;
        _liveState = CreateLiveState(dispatchMode);
    }

    public static GameEventScriptHostBuilder CreateBuilder() => new();

    #region Public interface

    public GameEventScriptHost Load(IGameEventScriptModule module, int priority = NormalPriority)
    {
        _ = module ?? throw new ArgumentNullException(nameof(module));
        module.Bind(_extensionRegistry, _externalTypeRegistry);

        var handlerEntries = module.Handlers.ToArray();
        foreach (var handler in handlerEntries)
        {
            _ = handler ?? throw new ArgumentException("Module contains a null handler.", nameof(module));
            _ = handler.Signature ?? throw new ArgumentException("Module contains a handler with a null signature.", nameof(module));
        }

        RegisterMany(handlerEntries, priority);
        return this;
    }

    public GameEventScriptHost Subscribe(string message, IReadOnlyCollection<string> parameterNames, Action<GameEventScriptMessage, GameEventScriptSession> handler, int priority = NormalPriority)
        => Subscribe(Create(!string.IsNullOrWhiteSpace(message) ? message : throw new ArgumentException("Message must not be null or whitespace", nameof(message)),
            parameterNames ?? throw new ArgumentNullException(nameof(parameterNames))), handler, priority);

    public GameEventScriptHost Subscribe(GameEventScriptMessageSignature signature, Action<GameEventScriptMessage, GameEventScriptSession> handler, int priority = NormalPriority)
    {
        Register(
            signature ?? throw new ArgumentNullException(nameof(signature)),
            priority,
            handler ?? throw new ArgumentNullException(nameof(handler)));
        return this;
    }

    public GameEventScriptHost Subscribe(GameEventScriptMessageHandlerDescriptor handler, int priority = NormalPriority)
    {
        _ = handler ?? throw new ArgumentNullException(nameof(handler));
        lock (_dispatchGate)
        {
            RegisterLocked(handler, priority);
        }

        return this;
    }

    public GameEventScriptHost Subscribe(
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

    public GameEventScriptHost Subscribe(IGameEventScriptModule module, int priority = NormalPriority)
    {
        _ = module ?? throw new ArgumentNullException(nameof(module));
        module.Bind(_extensionRegistry, _externalTypeRegistry);

        var handlerEntries = module.Handlers.ToArray();
        foreach (var handler in handlerEntries)
        {
            _ = handler ?? throw new ArgumentException("Module contains a null handler.", nameof(module));
            _ = handler.Signature ?? throw new ArgumentException("Module contains a handler with a null signature.", nameof(module));
        }

        RegisterMany(handlerEntries, priority);
        return this;
    }

    public GameEventScriptSession StartSession()
    {
        var state = CreateLiveState(_dispatchMode);
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
        lock (_pumpGate)
        {
            ResetManualStateIfCompletedAndIdle();
            EnqueueLiveStateInitializationIfNeeded();
            if (!TryEnqueueInvocations(_liveState, message))
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

    public bool PublishToCompletion(GameEventScriptMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Name))
        {
            return false;
        }

        var state = new GameEventScriptHostRunState(this, _dispatchMode, _dispatcher, _random, _runtimeObserver, _extensionRegistry, _runtimeLimits, queuePublisher: TryEnqueueInvocations, publishHook: _publishHook);
        EnqueueInitializationInvocations(state);
        if (!TryEnqueueInvocations(state, message))
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
            EnqueueLiveStateInitializationIfNeeded();
        }

        return DrainSlice(_liveState, maxOpcodes);
    }

    public GameEventScriptRun BeginRun(GameEventScriptMessage message)
    {
        var state = new GameEventScriptHostRunState(this, _dispatchMode, _dispatcher, _random, _runtimeObserver, _extensionRegistry, _runtimeLimits, queuePublisher: TryEnqueueInvocations, publishHook: _publishHook);
        EnqueueInitializationInvocations(state);
        var accepted = TryEnqueueInvocations(state, message);
        return new GameEventScriptRun(DrainSlice, state, accepted);
    }

    #endregion

    #region Internals

    private GameEventScriptHostRunState CreateLiveState(GameEventScriptDispatchMode dispatchMode)
        => new(this, dispatchMode, _dispatcher, _random, _runtimeObserver, _extensionRegistry, _runtimeLimits, enforceProcessedEventsLimit: false, queuePublisher: TryEnqueueInvocations, publishHook: _publishHook);

    internal bool TryEnqueueSessionInvocations(GameEventScriptHostRunState state, GameEventScriptMessage message)
        => TryEnqueueInvocations(state, message);

    internal void DrainSessionToCompletion(GameEventScriptHostRunState state)
        => DrainToCompletion(state);

    internal void DrainSessionOneToCompletion(GameEventScriptHostRunState state)
        => DrainOneToCompletion(state);

    internal GameEventScriptRunStepResult DrainSessionSlice(GameEventScriptHostRunState state, int maxOpcodes)
        => DrainSlice(state, maxOpcodes);

    private void ResetManualStateIfCompletedAndIdle()
    {
        if (_dispatchMode != GameEventScriptDispatchMode.Manual ||
            !_liveState.IsCompletedAndIdle)
        {
            return;
        }

        _liveState = CreateLiveState(GameEventScriptDispatchMode.Manual);
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
                        _liveStateInitializationQueued = false;
                    }
                }
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

    private void Register(
        GameEventScriptMessageSignature signature,
        int priority,
        Action<GameEventScriptMessage, GameEventScriptSession> handler)
        => Register(signature, priority, handler, [], []);

    private void Register(
        GameEventScriptMessageSignature signature,
        int priority,
        Action<GameEventScriptMessage, GameEventScriptSession> handler,
        IReadOnlyCollection<string>? matchingTags,
        IReadOnlyCollection<string>? withoutTags)
    {
        lock (_dispatchGate)
        {
            RegisterLocked(signature, priority, handler, matchingTags, withoutTags);
        }
    }

    private void RegisterMany(
        IReadOnlyList<GameEventScriptMessageHandlerDescriptor> handlers,
        int priority)
    {
        lock (_dispatchGate)
        {
            foreach (var handler in handlers)
            {
                RegisterLocked(handler, priority);
            }
        }
    }

    private void RegisterLocked(
        GameEventScriptMessageSignature signature,
        int priority,
        Action<GameEventScriptMessage, GameEventScriptSession> handler,
        IReadOnlyCollection<string>? matchingTags = null,
        IReadOnlyCollection<string>? withoutTags = null)
        => RegisterLocked(new GameEventScriptMessageHandlerDescriptor(signature, handler, matchingTags, withoutTags), priority);

    private void RegisterLocked(GameEventScriptMessageHandlerDescriptor handler, int priority)
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

                if (!state.TryDequeue(out var queuedInvocation))
                {
                    break;
                }

                if (queuedInvocation is null || !state.TryStartDelivery())
                {
                    continue;
                }

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
        if (state.ActiveInvocation is not null)
        {
            var executed = RunMessageInvocationSlice(state, state.ActiveInvocation, remainingOpcodes);
            if (!state.ActiveInvocation.IsCompleted)
            {
                return;
            }

            state.ClearActiveInvocation();
            state.RecordDispatchCompleted(state.ActiveMessage, state.ActiveSubscription!.DispatchSignatureId);
            state.CompleteDispatch();
            return;
        }

        try
        {
            var invocation = subscription.Handler.Invoke(state.ActiveMessage, state.Session);
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

        state.RecordDispatchCompleted(state.ActiveMessage, state.ActiveSubscription!.DispatchSignatureId);
        state.CompleteDispatch();
    }

    private static int RunMessageInvocationSlice(GameEventScriptHostRunState state, IGameEventScriptMessageInvocation invocation, int maxOpcodes)
    {
        var executed = invocation.RunSlice(maxOpcodes);
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

    private bool TryGetNameSubscriptions(string messageName, out MessageSubscription[] subscriptions)
    {
        lock (_dispatchGate)
        {
            return _messageNameDispatchIndex.TryGetValue(messageName, out subscriptions!);
        }
    }

    private bool TryEnqueueInvocations(GameEventScriptHostRunState state, GameEventScriptMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Name))
        {
            return false;
        }

        if (GameEventScriptSystemEndpoints.IsInitializationName(message.Name))
        {
            return false;
        }

        var hasExactSubscriptions = TryGetSubscriptions(message.SignatureId, out var exactSubscriptions) &&
                                    exactSubscriptions.Length > 0;
        var hasNameSubscriptions = TryGetNameSubscriptions(message.Name, out var nameSubscriptions) &&
                                       nameSubscriptions.Length > 0;
        if (!hasExactSubscriptions && !hasNameSubscriptions)
        {
            return TryEnqueueUndeliverableInvocation(state, message);
        }

        var accepted = false;
        var matched = false;
        foreach (var subscription in EnumerateDispatchSubscriptions(
                     exactSubscriptions,
                     hasExactSubscriptions,
                     nameSubscriptions,
                     hasNameSubscriptions))
        {
            if (!subscription.MatchesTags(message))
            {
                continue;
            }

            matched = true;
            accepted |= state.Enqueue(new QueuedInvocation(message, subscription));
        }

        return matched
            ? accepted
            : TryEnqueueUndeliverableInvocation(state, message);
    }

    private bool TryEnqueueUndeliverableInvocation(GameEventScriptHostRunState state, GameEventScriptMessage message)
    {
        if (GameEventScriptSystemEndpoints.IsUndeliverableName(message.Name))
        {
            return false;
        }

        var hasExactSubscriptions = TryGetSubscriptions(GameEventScriptSystemEndpoints.UndeliverableSignatureId, out var exactSubscriptions) &&
                                    exactSubscriptions.Length > 0;
        var hasNameSubscriptions = TryGetNameSubscriptions(GameEventScriptSystemEndpoints.UndeliverableName, out var nameSubscriptions) &&
                                       nameSubscriptions.Length > 0;
        if (!hasExactSubscriptions && !hasNameSubscriptions)
        {
            return false;
        }

        var accepted = false;
        var matched = false;
        foreach (var subscription in EnumerateDispatchSubscriptions(
                     exactSubscriptions,
                     hasExactSubscriptions,
                     nameSubscriptions,
                     hasNameSubscriptions))
        {
            if (!subscription.MatchesTags(message))
            {
                continue;
            }

            matched = true;
            accepted |= state.Enqueue(new QueuedInvocation(message, subscription));
        }

        return matched && accepted;
    }

    private void EnqueueInitializationInvocations(GameEventScriptHostRunState state)
    {
        MessageSubscription[] subscriptions;
        lock (_dispatchGate)
        {
            subscriptions = _initializationSubscriptions;
        }

        if (subscriptions.Length == 0)
        {
            return;
        }

        var message = GameEventScriptSystemEndpoints.CreateInitializationMessage();
        foreach (var subscription in subscriptions)
        {
            if (!subscription.MatchesTags(message))
            {
                continue;
            }

            state.Enqueue(new QueuedInvocation(message, subscription));
        }
    }

    private static IEnumerable<MessageSubscription> EnumerateDispatchSubscriptions(
        MessageSubscription[] exactSubscriptions,
        bool hasExactSubscriptions,
        MessageSubscription[] nameSubscriptions,
        bool hasNameSubscriptions)
    {
        if (!hasExactSubscriptions)
        {
            foreach (var subscription in nameSubscriptions)
            {
                yield return subscription;
            }

            yield break;
        }

        if (!hasNameSubscriptions)
        {
            foreach (var subscription in exactSubscriptions)
            {
                yield return subscription;
            }

            yield break;
        }

        var exactIndex = 0;
        var nameIndex = 0;
        while (exactIndex < exactSubscriptions.Length && nameIndex < nameSubscriptions.Length)
        {
            if (CompareDispatchOrder(exactSubscriptions[exactIndex], nameSubscriptions[nameIndex]) <= 0)
            {
                yield return exactSubscriptions[exactIndex++];
            }
            else
            {
                yield return nameSubscriptions[nameIndex++];
            }
        }

        while (exactIndex < exactSubscriptions.Length)
        {
            yield return exactSubscriptions[exactIndex++];
        }

        while (nameIndex < nameSubscriptions.Length)
        {
            yield return nameSubscriptions[nameIndex++];
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
    private readonly Queue<GameEventScriptHost.QueuedInvocation> _queue = new();
    private readonly object _queueGate = new();
    private readonly Func<GameEventScriptHostRunState, GameEventScriptMessage, bool> _queuePublisher;
    private readonly Func<GameEventScriptMessage, bool>? _publishHook;
    private readonly bool _enforceProcessedEventsLimit;
    private readonly int _maxProcessedEventsPerRun;
    private readonly int _maxQueuedMessagesPerRun;
    private int _deliveredInvocations;
    private long _totalExecutedOpcodes;

    public GameEventScriptHostRunState(
        GameEventScriptHost host,
        GameEventScriptDispatchMode dispatchMode,
        IGameEventScriptDispatcher? dispatcher,
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
        Session = new GameEventScriptSession(host, this, dispatchMode, dispatcher, random, EmitInternal, runtimeLimits, extensionRegistry, PublishInternal, runtimeObserver);
    }

    public GameEventScriptSession Session { get; }

    public GameEventScriptMessage? ActiveMessage { get; private set; }

    public GameEventScriptHost.MessageSubscription? ActiveSubscription { get; private set; }

    public IGameEventScriptMessageInvocation? ActiveInvocation { get; private set; }

    public int StartedEventCount { get; private set; }

    public int StepExecutedOpcodes { get; private set; }

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

    public bool Enqueue(GameEventScriptHost.QueuedInvocation queuedInvocation)
    {
        var message = queuedInvocation.Message;
        if (string.IsNullOrWhiteSpace(message.Name))
        {
            return false;
        }

        lock (_queueGate)
        {
            if (_maxQueuedMessagesPerRun > 0 && _queue.Count >= _maxQueuedMessagesPerRun)
            {
                Session.RuntimeBudget.ReportLimit(
                    nameof(GameEventScriptRuntimeLimits.MaxQueuedMessagesPerRun),
                    $"Message queue limit reached. Dropped '{message.Name}'.",
                    _maxQueuedMessagesPerRun);
                return false;
            }

            _queue.Enqueue(queuedInvocation);
        }
        return true;
    }

    public bool TryDequeue(out GameEventScriptHost.QueuedInvocation? queuedInvocation)
    {
        lock (_queueGate)
        {
            if (_queue.Count == 0)
            {
                queuedInvocation = null;
                return false;
            }

            queuedInvocation = _queue.Dequeue();
            return true;
        }
    }

    public bool TryStartDelivery()
    {
        if (_enforceProcessedEventsLimit && _maxProcessedEventsPerRun > 0 && _deliveredInvocations >= _maxProcessedEventsPerRun)
        {
            return false;
        }

        _deliveredInvocations++;
        StartedEventCount++;
        return true;
    }

    public void StartDispatch(GameEventScriptHost.QueuedInvocation queuedInvocation)
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
