#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Types;
using StepH.Flow.Extensions;
using static StepH.Flow.EventScript.EventScriptMessageSignature;

namespace StepH.Flow.EventScript.Runtime;

public sealed class EventScriptHost
{
    private readonly EventScriptRandomGenerator _random;
    private readonly IEventScriptDiagnosticCollector? _diagnosticCollector;
    private readonly EventScriptRuntimeLimits _runtimeLimits;
    private readonly int _maxProcessedEventsPerRun;
    private readonly int _defaultScriptHandlerPriority;
    private readonly int _defaultExternalHandlerPriority;
    private readonly Dictionary<string, List<MessageSubscription>> _subscriptions = new(StringComparer.Ordinal);
    private long _nextRegistrationOrder;

    internal EventScriptHost(EventScriptRandomGenerator random, IEventScriptDiagnosticCollector? diagnosticCollector, EventScriptRuntimeLimits runtimeLimits, int maxProcessedEventsPerRun,
        int defaultScriptHandlerPriority, int defaultExternalHandlerPriority)
    {
        _random = random;
        _diagnosticCollector = diagnosticCollector;
        _runtimeLimits = runtimeLimits ?? EventScriptRuntimeLimits.Default;
        _maxProcessedEventsPerRun = maxProcessedEventsPerRun <= 0 ? EventScriptHostBuilder.DefaultMaxProcessedEventsPerRun : maxProcessedEventsPerRun;
        _defaultScriptHandlerPriority = defaultScriptHandlerPriority;
        _defaultExternalHandlerPriority = defaultExternalHandlerPriority;
    }

    public static EventScriptHostBuilder CreateBuilder() => new();

    #region Public interface

    public EventScriptHost Load(IEventScriptMessageHandlerCollection handlers, int? priority = null)
    {
        _ = handlers ?? throw new ArgumentNullException(nameof(handlers));
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

    public EventScriptHost Subscribe(string message, IReadOnlyCollection<string> parameterNames, Action<EventScriptMessage, EventScriptContext> handler, int? priority = null)
        => Subscribe(MessageSignature(!string.IsNullOrWhiteSpace(message) ? message : throw new ArgumentException("Message must not be null or whitespace", nameof(message)),
            parameterNames ?? throw new ArgumentNullException(nameof(parameterNames))), handler, priority);

    public EventScriptHost Subscribe(EventScriptMessageSignature signature, Action<EventScriptMessage, EventScriptContext> handler, int? priority = null)
        => this.Also(_ => Register(new MessageSubscription(
            signature ?? throw new ArgumentNullException(nameof(signature)),
            priority ?? _defaultExternalHandlerPriority,
            _nextRegistrationOrder++,
            handler ?? throw new ArgumentNullException(nameof(handler)))));

    public EventScriptHost Subscribe(IEventScriptMessageHandlerCollection handlers, int? priority = null)
    {
        _ = handlers ?? throw new ArgumentNullException(nameof(handlers));
        foreach (var handler in handlers.Handlers)
        {
            Subscribe(handler.Signature, handler.Handler, priority);
        }

        return this;
    }

    public void Publish(EventScriptMessage message)
    {
        if (message is null || string.IsNullOrWhiteSpace(message.Name))
        {
            return;
        }

        var state = new EventScriptRunState(_maxProcessedEventsPerRun, _random, _diagnosticCollector, _runtimeLimits);
        state.Enqueue(message);
        Drain(state);
    }

    public void Publish(string message, params (string Name, EventScriptValue Value)[] args)
        => Publish(EventScriptMessage.Message(message, args));

    public void Publish(string message, params (string Name, object? Value)[] args)
        => Publish(EventScriptMessage.Message(message, args));

    #endregion

    #region Internals

    private void Register(MessageSubscription subscription)
    {
        if (!_subscriptions.TryGetValue(subscription.Definition.Name, out var handlers))
        {
            handlers = new List<MessageSubscription>();
            _subscriptions[subscription.Definition.Name] = handlers;
        }

        handlers.Add(subscription);
    }

    private void Drain(EventScriptRunState state)
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

    private void Dispatch(EventScriptMessage queuedEvent, EventScriptRunState state)
    {
        state.RecordDiagnostic(EventScriptDiagnosticEventKind.DispatchStarted, queuedEvent.Name, queuedEvent.Arguments, $"Dispatch '{queuedEvent.Name}' started");

        if (!_subscriptions.TryGetValue(queuedEvent.Name, out var subscriptions))
        {
            state.RecordDiagnostic(EventScriptDiagnosticEventKind.DispatchCompleted, queuedEvent.Name, queuedEvent.Arguments, $"Dispatch '{queuedEvent.Name}' completed without subscribers");
            return;
        }

        foreach (var subscription in subscriptions
                     .Where(x => string.Equals(x.Definition.SignatureId, queuedEvent.SignatureId, StringComparison.Ordinal))
                     .OrderBy(x => x.Priority)
                     .ThenBy(x => x.RegistrationOrder))
        {
            state.RecordDiagnostic(
                EventScriptDiagnosticEventKind.SubscriberMatched,
                queuedEvent.Name,
                queuedEvent.Arguments,
                $"Subscriber matched: {subscription.Definition.SignatureId}");

            try
            {
                state.RecordDiagnostic(EventScriptDiagnosticEventKind.SubscriberInvoked, queuedEvent.Name, queuedEvent.Arguments, "Subscriber invoked");
                subscription.Handler(queuedEvent, state.Context);
            }
            catch
            {
                // Runtime dispatch must remain lenient.
            }
        }

        state.RecordDiagnostic(EventScriptDiagnosticEventKind.DispatchCompleted, queuedEvent.Name, queuedEvent.Arguments, $"Dispatch '{queuedEvent.Name}' completed");
    }

    private sealed class EventScriptRunState
    {
        private readonly Queue<EventScriptMessage> _queue = new();
        private readonly int _maxProcessedEventsPerRun;
        private int _processedEvents;

        public EventScriptRunState(
            int maxProcessedEventsPerRun,
            EventScriptRandomGenerator random,
            IEventScriptDiagnosticCollector? diagnosticCollector,
            EventScriptRuntimeLimits runtimeLimits)
        {
            _maxProcessedEventsPerRun = maxProcessedEventsPerRun;
            Context = new EventScriptContext(random, PublishInternal, diagnosticCollector, publishCallbackRecordsDiagnostics: true, runtimeLimits: runtimeLimits);
        }

        public EventScriptContext Context { get; }

        public void Enqueue(EventScriptMessage message)
        {
            if (message is null || string.IsNullOrWhiteSpace(message.Name))
            {
                return;
            }

            _queue.Enqueue(message);
        }

        public bool TryDequeue(out EventScriptMessage? message)
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

        public void RecordDiagnostic(EventScriptDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, EventScriptValue> arguments, string? detail = null)
            => Context.RecordDiagnostic(kind, name, arguments, detail);

        private void PublishInternal(EventScriptMessage message)
        {
            Enqueue(message);
            RecordDiagnostic(EventScriptDiagnosticEventKind.EventPublished, message.Name, message.Arguments, $"Published '{message.Name}'");
        }
    }

    private sealed record MessageSubscription(
        EventScriptMessageSignature Definition,
        int Priority,
        long RegistrationOrder,
        Action<EventScriptMessage, EventScriptContext> Handler);

    #endregion
}
