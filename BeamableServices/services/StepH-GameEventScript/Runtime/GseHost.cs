#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.GameEventScript.RegisterVM;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.GseMessageSignature;

namespace StepH.GameEventScript.Runtime;

public sealed class GseHost
{
    private readonly GseRandomGenerator _random;
    private readonly IGseDiagnosticCollector? _diagnosticCollector;
    private readonly Action<GseMessage>? _publishedMessageObserver;
    private readonly IGseExtensionRegistry _extensionRegistry;
    private readonly GseRuntimeLimits _runtimeLimits;
    private readonly int _maxProcessedEventsPerRun;
    private readonly int _defaultScriptHandlerPriority;
    private readonly int _defaultExternalHandlerPriority;
    private readonly Dictionary<string, List<MessageSubscription>> _dispatchIndex = new(StringComparer.Ordinal);
    private long _nextRegistrationOrder;

    internal GseHost(GseRandomGenerator random, IGseDiagnosticCollector? diagnosticCollector, Action<GseMessage>? publishedMessageObserver,
        IGseExtensionRegistry extensionRegistry,
        GseRuntimeLimits runtimeLimits, int maxProcessedEventsPerRun,
        int defaultScriptHandlerPriority, int defaultExternalHandlerPriority)
    {
        _random = random;
        _diagnosticCollector = diagnosticCollector;
        _publishedMessageObserver = publishedMessageObserver;
        _extensionRegistry = extensionRegistry ?? GseEmptyExtensionRegistry.Instance;
        _runtimeLimits = runtimeLimits ?? GseRuntimeLimits.Default;
        _maxProcessedEventsPerRun = maxProcessedEventsPerRun <= 0 ? GseHostBuilder.DefaultMaxProcessedEventsPerRun : maxProcessedEventsPerRun;
        _defaultScriptHandlerPriority = defaultScriptHandlerPriority;
        _defaultExternalHandlerPriority = defaultExternalHandlerPriority;
    }

    public static GseHostBuilder CreateBuilder() => new();

    #region Public interface

    public GseHost Load(IGseMessageHandlerCollection handlers, int? priority = null)
    {
        _ = handlers ?? throw new ArgumentNullException(nameof(handlers));
        if (handlers is RegisterCompiledGse registerCompiled)
        {
            GseDynamicLinker.Bind(registerCompiled, _extensionRegistry);
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

    public GseHost Subscribe(string message, IReadOnlyCollection<string> parameterNames, Action<GseMessage, GseContext> handler, int? priority = null)
        => Subscribe(MessageSignature(!string.IsNullOrWhiteSpace(message) ? message : throw new ArgumentException("Message must not be null or whitespace", nameof(message)),
            parameterNames ?? throw new ArgumentNullException(nameof(parameterNames))), handler, priority);

    public GseHost Subscribe(GseMessageSignature signature, Action<GseMessage, GseContext> handler, int? priority = null)
    {
        Register(new MessageSubscription(signature ?? throw new ArgumentNullException(nameof(signature)), priority ?? _defaultExternalHandlerPriority, _nextRegistrationOrder++,
            handler ?? throw new ArgumentNullException(nameof(handler))));
        return this;
    }

    public GseHost Subscribe(IGseMessageHandlerCollection handlers, int? priority = null)
    {
        _ = handlers ?? throw new ArgumentNullException(nameof(handlers));
        foreach (var handler in handlers.Handlers)
        {
            Subscribe(handler.Signature, handler.Handler, priority);
        }

        return this;
    }

    public void Publish(GseMessage message)
    {
        if (message is null || string.IsNullOrWhiteSpace(message.Name))
        {
            return;
        }

        var state = new GseRunState(_maxProcessedEventsPerRun, _random, _diagnosticCollector, _publishedMessageObserver, _extensionRegistry, _runtimeLimits);
        state.Enqueue(message);
        Drain(state);
    }

    public void Publish(string message, params (string Name, GseValue Value)[] args)
        => Publish(GseMessage.Message(message, args));

    public void Publish(string message, params (string Name, object? Value)[] args)
        => Publish(GseMessage.Message(message, args));

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

    private void Drain(GseRunState state)
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

    private void Dispatch(GseMessage queuedEvent, GseRunState state)
    {
        state.RecordDiagnostic(GseDiagnosticEventKind.DispatchStarted, queuedEvent.Name, queuedEvent.Arguments, $"Dispatch '{queuedEvent.Name}' started");

        if (!_dispatchIndex.TryGetValue(queuedEvent.SignatureId, out var subscriptions))
        {
            state.RecordDiagnostic(GseDiagnosticEventKind.DispatchCompleted, queuedEvent.Name, queuedEvent.Arguments, $"Dispatch '{queuedEvent.Name}' completed without subscribers");
            return;
        }

        foreach (var subscription in subscriptions)
        {
            state.RecordDiagnostic(
                GseDiagnosticEventKind.SubscriberMatched,
                queuedEvent.Name,
                queuedEvent.Arguments,
                $"Subscriber matched: {subscription.Definition.SignatureId}");

            try
            {
                state.RecordDiagnostic(GseDiagnosticEventKind.SubscriberInvoked, queuedEvent.Name, queuedEvent.Arguments, "Subscriber invoked");
                subscription.Handler(queuedEvent, state.Context);
            }
            catch (GseFatalRuntimeException)
            {
                throw;
            }
            catch
            {
                // Runtime dispatch must remain lenient.
            }
        }

        state.RecordDiagnostic(GseDiagnosticEventKind.DispatchCompleted, queuedEvent.Name, queuedEvent.Arguments, $"Dispatch '{queuedEvent.Name}' completed");
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

    private sealed class GseRunState
    {
        private readonly Queue<GseMessage> _queue = new();
        private readonly int _maxProcessedEventsPerRun;
        private int _processedEvents;

        public GseRunState(
            int maxProcessedEventsPerRun,
            GseRandomGenerator random,
            IGseDiagnosticCollector? diagnosticCollector,
            Action<GseMessage>? publishedMessageObserver,
            IGseExtensionRegistry extensionRegistry,
            GseRuntimeLimits runtimeLimits)
        {
            _maxProcessedEventsPerRun = maxProcessedEventsPerRun;
            PublishedMessageObserver = publishedMessageObserver;
            Context = new GseContext(random, PublishInternal, diagnosticCollector, publishCallbackRecordsDiagnostics: true, runtimeLimits: runtimeLimits, extensionRegistry: extensionRegistry);
        }

        public GseContext Context { get; }

        private Action<GseMessage>? PublishedMessageObserver { get; }

        public void Enqueue(GseMessage message)
        {
            if (message is null || string.IsNullOrWhiteSpace(message.Name))
            {
                return;
            }

            _queue.Enqueue(message);
        }

        public bool TryDequeue(out GseMessage? message)
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

        public void RecordDiagnostic(GseDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, GseValue> arguments, string? detail = null)
            => Context.RecordDiagnostic(kind, name, arguments, detail);

        private void PublishInternal(GseMessage message)
        {
            Enqueue(message);
            PublishedMessageObserver?.Invoke(message);
            RecordDiagnostic(GseDiagnosticEventKind.EventPublished, message.Name, message.Arguments, $"Published '{message.Name}'");
        }
    }

    private sealed record MessageSubscription(
        GseMessageSignature Definition,
        int Priority,
        long RegistrationOrder,
        Action<GseMessage, GseContext> Handler);

    #endregion
}