#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.BytecodeVM;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptMessageSignature;

namespace StepH.GameEventScript.Runtime;

public sealed class GameEventScriptHost
{
    private readonly GameEventScriptRandomGenerator _random;
    private readonly IGameEventScriptDiagnosticCollector? _diagnosticCollector;
    private readonly Action<GameEventScriptMessage>? _publishedMessageObserver;
    private readonly IGameEventScriptExtensionRegistry _extensionRegistry;
    private readonly GameEventScriptRuntimeLimits _runtimeLimits;
    private readonly int _maxProcessedEventsPerRun;
    private readonly int _defaultScriptHandlerPriority;
    private readonly int _defaultExternalHandlerPriority;
    private readonly Dictionary<string, List<MessageSubscription>> _dispatchIndex = new(StringComparer.Ordinal);
    private long _nextRegistrationOrder;

    internal GameEventScriptHost(GameEventScriptRandomGenerator random, IGameEventScriptDiagnosticCollector? diagnosticCollector, Action<GameEventScriptMessage>? publishedMessageObserver,
        IGameEventScriptExtensionRegistry extensionRegistry,
        GameEventScriptRuntimeLimits runtimeLimits, int maxProcessedEventsPerRun,
        int defaultScriptHandlerPriority, int defaultExternalHandlerPriority)
    {
        _random = random;
        _diagnosticCollector = diagnosticCollector;
        _publishedMessageObserver = publishedMessageObserver;
        _extensionRegistry = extensionRegistry ?? GameEventScriptEmptyExtensionRegistry.Instance;
        _runtimeLimits = runtimeLimits ?? GameEventScriptRuntimeLimits.Default;
        _maxProcessedEventsPerRun = maxProcessedEventsPerRun <= 0 ? GameEventScriptHostBuilder.DefaultMaxProcessedEventsPerRun : maxProcessedEventsPerRun;
        _defaultScriptHandlerPriority = defaultScriptHandlerPriority;
        _defaultExternalHandlerPriority = defaultExternalHandlerPriority;
    }

    public static GameEventScriptHostBuilder CreateBuilder() => new();

    #region Public interface

    public GameEventScriptHost Load(GameEventScriptCompiled bytecode, int? priority = null)
    {
        _ = bytecode ?? throw new ArgumentNullException(nameof(bytecode));
        return Load(BytecodeVmExecutableBuilder.Build(bytecode), priority);
    }

    public GameEventScriptHost Load(IGameEventScriptMessageHandlerCollection handlers, int? priority = null)
    {
        _ = handlers ?? throw new ArgumentNullException(nameof(handlers));
        if (handlers is GseBytecodeVmExecutable registerCompiled)
        {
            GameEventScriptDynamicLinker.Bind(registerCompiled, _extensionRegistry);
        }

        foreach (var handler in handlers.Handlers)
        {
            Register(new MessageSubscription(
                handler.Signature,
                priority ?? _defaultScriptHandlerPriority,
                _nextRegistrationOrder++,
                handler.Handler));
        }

        return this;
    }

    public GameEventScriptHost Subscribe(string message, IReadOnlyCollection<string> parameterNames, Action<GameEventScriptMessage, GameEventScriptContext> handler, int? priority = null)
        => Subscribe(Create(!string.IsNullOrWhiteSpace(message) ? message : throw new ArgumentException("Message must not be null or whitespace", nameof(message)),
            parameterNames ?? throw new ArgumentNullException(nameof(parameterNames))), handler, priority);

    public GameEventScriptHost Subscribe(GameEventScriptMessageSignature signature, Action<GameEventScriptMessage, GameEventScriptContext> handler, int? priority = null)
    {
        Register(new MessageSubscription(signature ?? throw new ArgumentNullException(nameof(signature)), priority ?? _defaultExternalHandlerPriority, _nextRegistrationOrder++,
            handler ?? throw new ArgumentNullException(nameof(handler))));
        return this;
    }

    public GameEventScriptHost Subscribe(IGameEventScriptMessageHandlerCollection handlers, int? priority = null)
    {
        _ = handlers ?? throw new ArgumentNullException(nameof(handlers));
        foreach (var handler in handlers.Handlers)
        {
            Subscribe(handler.Signature, handler.Handler, priority);
        }

        return this;
    }

    public void Publish(GameEventScriptMessage message)
    {
        if (message is null || string.IsNullOrWhiteSpace(message.Name))
        {
            return;
        }

        var state = new GameEventScriptRunState(_maxProcessedEventsPerRun, _random, _diagnosticCollector, _publishedMessageObserver, _extensionRegistry, _runtimeLimits);
        state.Enqueue(message);
        Drain(state);
    }

    #endregion

    #region Internals

    private void Register(MessageSubscription subscription)
    {
        if (!_dispatchIndex.TryGetValue(subscription.Definition.SignatureId, out var handlers))
        {
            handlers = new List<MessageSubscription>();
            _dispatchIndex[subscription.Definition.SignatureId] = handlers;
        }

        InsertSubscriptionByDispatchOrder(handlers, subscription);
    }

    private void Drain(GameEventScriptRunState state)
    {
        while (state.TryDequeue(out var queuedEvent))
        {
            if (!state.TryStartProcessingEvent())
            {
                break;
            }

            if (queuedEvent is not null)
            {
                Dispatch(queuedEvent, state);
            }
        }
    }

    private void Dispatch(GameEventScriptMessage queuedEvent, GameEventScriptRunState state)
    {
        state.RecordDiagnostic(GameEventScriptDiagnosticEventKind.DispatchStarted, queuedEvent.Name, queuedEvent.Arguments, $"Dispatch '{queuedEvent.Name}' started");

        if (!_dispatchIndex.TryGetValue(queuedEvent.SignatureId, out var subscriptions))
        {
            state.RecordDiagnostic(GameEventScriptDiagnosticEventKind.DispatchCompleted, queuedEvent.Name, queuedEvent.Arguments, $"Dispatch '{queuedEvent.Name}' completed without subscribers");
            return;
        }

        foreach (var subscription in subscriptions)
        {
            state.RecordDiagnostic(
                GameEventScriptDiagnosticEventKind.SubscriberMatched,
                queuedEvent.Name,
                queuedEvent.Arguments,
                $"Subscriber matched: {subscription.Definition.SignatureId}");

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

    private static void InsertSubscriptionByDispatchOrder(List<MessageSubscription> handlers, MessageSubscription subscription)
    {
        var insertIndex = handlers.FindIndex(existing => CompareDispatchOrder(subscription, existing) < 0);
        if (insertIndex < 0)
        {
            handlers.Add(subscription);
            return;
        }

        handlers.Insert(insertIndex, subscription);
    }

    private static int CompareDispatchOrder(MessageSubscription left, MessageSubscription right)
    {
        var priorityComparison = left.Priority.CompareTo(right.Priority);
        return priorityComparison != 0
            ? priorityComparison
            : left.RegistrationOrder.CompareTo(right.RegistrationOrder);
    }

    private sealed class GameEventScriptRunState
    {
        private readonly Queue<GameEventScriptMessage> _queue = new();
        private readonly int _maxProcessedEventsPerRun;
        private int _processedEvents;

        public GameEventScriptRunState(
            int maxProcessedEventsPerRun,
            GameEventScriptRandomGenerator random,
            IGameEventScriptDiagnosticCollector? diagnosticCollector,
            Action<GameEventScriptMessage>? publishedMessageObserver,
            IGameEventScriptExtensionRegistry extensionRegistry,
            GameEventScriptRuntimeLimits runtimeLimits)
        {
            _maxProcessedEventsPerRun = maxProcessedEventsPerRun;
            PublishedMessageObserver = publishedMessageObserver;
            Context = new GameEventScriptContext(random, PublishInternal, diagnosticCollector, runtimeLimits: runtimeLimits, extensionRegistry: extensionRegistry);
        }

        public GameEventScriptContext Context { get; }

        private Action<GameEventScriptMessage>? PublishedMessageObserver { get; }

        public void Enqueue(GameEventScriptMessage message)
        {
            if (message is null || string.IsNullOrWhiteSpace(message.Name))
            {
                return;
            }

            _queue.Enqueue(message);
        }

        public bool TryDequeue(out GameEventScriptMessage? message)
        {
            if (_queue.Count == 0)
            {
                message = null;
                return false;
            }

            message = _queue.Dequeue();
            return true;
        }

        public bool TryStartProcessingEvent()
        {
            if (_processedEvents >= _maxProcessedEventsPerRun)
            {
                return false;
            }

            _processedEvents++;
            return true;
        }

        public void RecordDiagnostic(GameEventScriptDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, GameEventScriptValue> arguments, string? detail = null)
            => Context.RecordDiagnostic(kind, name, arguments, detail);

        private void PublishInternal(GameEventScriptMessage message)
        {
            Enqueue(message);
            PublishedMessageObserver?.Invoke(message);
            RecordDiagnostic(GameEventScriptDiagnosticEventKind.EventPublished, message.Name, message.Arguments, $"Published '{message.Name}'");
        }
    }

    private sealed record MessageSubscription(
        GameEventScriptMessageSignature Definition,
        int Priority,
        long RegistrationOrder,
        Action<GameEventScriptMessage, GameEventScriptContext> Handler);

    #endregion
}
