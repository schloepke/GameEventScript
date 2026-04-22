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
    private readonly int _maxProcessedEventsPerRun;
    private readonly int _defaultScriptHandlerPriority;
    private readonly int _defaultExternalHandlerPriority;
    private readonly Dictionary<string, List<MessageSubscription>> _subscriptions = new(StringComparer.Ordinal);
    private long _nextRegistrationOrder;

    internal EventScriptHost(EventScriptRandomGenerator random, IEventScriptDiagnosticCollector? diagnosticCollector, int maxProcessedEventsPerRun, int defaultScriptHandlerPriority,
        int defaultExternalHandlerPriority)
    {
        _random = random;
        _diagnosticCollector = diagnosticCollector;
        _maxProcessedEventsPerRun = maxProcessedEventsPerRun <= 0 ? EventScriptHostBuilder.DefaultMaxProcessedEventsPerRun : maxProcessedEventsPerRun;
        _defaultScriptHandlerPriority = defaultScriptHandlerPriority;
        _defaultExternalHandlerPriority = defaultExternalHandlerPriority;
    }

    public static EventScriptHostBuilder CreateBuilder() => new();

    #region Public interface

    public EventScriptHost Load(CompiledEventScript compiledScript, int? priority = null)
    {
        foreach (var handlerGroup in (compiledScript ?? throw new ArgumentNullException(nameof(compiledScript))).Handlers.Values)
        {
            foreach (var handler in handlerGroup)
            {
                Register(new ScriptMessageSubscription(handler.Definition, priority ?? _defaultScriptHandlerPriority, _nextRegistrationOrder++, compiledScript, handler));
            }
        }

        return this;
    }

    public EventScriptHost Subscribe(string message, IReadOnlyCollection<string> parameterNames, Action<EventScriptSubscriptionContext> handler, int? priority = null)
        => Subscribe(MessageSignature(!string.IsNullOrWhiteSpace(message) ? message : throw new ArgumentException("Message must not be null or whitespace", nameof(message)),
            parameterNames ?? throw new ArgumentNullException(nameof(parameterNames))), handler, priority);

    public EventScriptHost Subscribe(EventScriptMessageSignature signature, Action<EventScriptSubscriptionContext> handler, int? priority = null)
        => this.Also(_ => Register(new ExternalMessageSubscription(signature ?? throw new ArgumentNullException(nameof(signature)), priority ?? _defaultExternalHandlerPriority,
            _nextRegistrationOrder++, handler ?? throw new ArgumentNullException(nameof(handler)))));

    public EventScriptExecutionResult Publish(EventScriptMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Name)) throw new ArgumentException("Message name must not be null or whitespace", nameof(message));
        var invocationContext = new EventScriptInvocationContext
        {
            Random = _random
        };

        var state = new EventScriptRunState(message, _maxProcessedEventsPerRun, invocationContext, _diagnosticCollector);
        state.Enqueue(message, captureVariables: true);
        return Drain(state);
    }

    public EventScriptExecutionResult Publish(string message, params (string Name, EventScriptValue Value)[] args)
        => Publish(EventScriptMessage.Message(message, args));

    public EventScriptExecutionResult Publish(string message, params (string Name, object? Value)[] args)
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

    private EventScriptExecutionResult Drain(EventScriptRunState state)
    {
        while (state.TryDequeue(out var queuedEvent))
        {
            if (!state.TryStartProcessingEvent())
            {
                break;
            }

            if (queuedEvent != null) Dispatch(queuedEvent, state);
        }

        return new EventScriptExecutionResult(state.InitialMessage, state.EmittedEvents, state.Variables);
    }

    private void Dispatch(QueuedMessage queuedEvent, EventScriptRunState state)
    {
        state.RecordDiagnostic(EventScriptDiagnosticEventKind.DispatchStarted, queuedEvent.Message.Name, queuedEvent.Message.Arguments, $"Dispatch '{queuedEvent.Message.Name}' started");

        if (!_subscriptions.TryGetValue(queuedEvent.Message.Name, out var subscriptions))
        {
            state.RecordDiagnostic(EventScriptDiagnosticEventKind.DispatchCompleted, queuedEvent.Message.Name, queuedEvent.Message.Arguments,
                $"Dispatch '{queuedEvent.Message.Name}' completed without subscribers");
            return;
        }

        foreach (var subscription in subscriptions
                     .Where(x => string.Equals(x.Definition.SignatureId, queuedEvent.Message.SignatureId, StringComparison.Ordinal))
                     .OrderBy(x => x.Priority)
                     .ThenBy(x => x.RegistrationOrder))
        {
            state.RecordDiagnostic(
                EventScriptDiagnosticEventKind.SubscriberMatched,
                queuedEvent.Message.Name,
                queuedEvent.Message.Arguments,
                $"{subscription.GetType().Name} matched");

            switch (subscription)
            {
                case ScriptMessageSubscription script:
                {
                    state.RecordDiagnostic(EventScriptDiagnosticEventKind.SubscriberInvoked, queuedEvent.Message.Name, queuedEvent.Message.Arguments, "Script subscriber invoked");
                    var result = script.CompiledScript.InvokeHandler(script.Handler, queuedEvent.Message.Arguments, state.InvocationContext, state.DiagnosticCollector);
                    if (queuedEvent.CaptureVariables)
                    {
                        state.CaptureVariables(result.Variables);
                    }

                    foreach (var emittedEvent in result.EmittedEvents)
                    {
                        state.RecordPublishedEvent(emittedEvent);
                        state.Enqueue(emittedEvent.Event, captureVariables: false);
                    }

                    break;
                }
                case ExternalMessageSubscription external:
                    try
                    {
                        state.RecordDiagnostic(EventScriptDiagnosticEventKind.SubscriberInvoked, queuedEvent.Message.Name, queuedEvent.Message.Arguments, "External subscriber invoked");
                        external.Handler(new EventScriptSubscriptionContext(queuedEvent.Message, state.PublishExternal));
                    }
                    catch
                    {
                        // Execution must always be lenient, so errors have to be handled by the handler
                    }

                    break;
            }
        }

        state.RecordDiagnostic(EventScriptDiagnosticEventKind.DispatchCompleted, queuedEvent.Message.Name, queuedEvent.Message.Arguments, $"Dispatch '{queuedEvent.Message.Name}' completed");
    }

    private sealed record QueuedMessage(EventScriptMessage Message, bool CaptureVariables);

    private sealed class EventScriptRunState(
        EventScriptMessage initialMessage,
        int maxProcessedEventsPerRun,
        EventScriptInvocationContext invocationContext,
        IEventScriptDiagnosticCollector? diagnosticCollector)
    {
        private readonly Queue<QueuedMessage> _queue = new();
        private readonly Dictionary<string, EventScriptValue> _variables = new(StringComparer.Ordinal);
        private int _processedEvents;

        public EventScriptMessage InitialMessage { get; } = initialMessage;

        public EventScriptInvocationContext InvocationContext { get; } = invocationContext ?? throw new ArgumentNullException(nameof(invocationContext));

        public IEventScriptDiagnosticCollector? DiagnosticCollector => diagnosticCollector;

        public IReadOnlyDictionary<string, EventScriptValue> Variables => _variables;

        public List<EventScriptEmittedEvent> EmittedEvents { get; } = [];

        public void Enqueue(EventScriptMessage message, bool captureVariables)
        {
            _queue.Enqueue(new QueuedMessage(message, captureVariables));
        }

        public bool TryDequeue(out QueuedMessage? queuedEvent)
        {
            if (_queue.Count == 0)
            {
                queuedEvent = null;
                return false;
            }

            queuedEvent = _queue.Dequeue();
            return true;
        }

        public bool TryStartProcessingEvent()
        {
            if (_processedEvents >= maxProcessedEventsPerRun)
            {
                return false;
            }

            _processedEvents++;
            return true;
        }

        public void PublishExternal(EventScriptMessage message)
        {
            var emittedEvent = new EventScriptEmittedEvent(message);
            RecordPublishedEvent(emittedEvent);
            Enqueue(message, captureVariables: false);
            RecordDiagnostic(EventScriptDiagnosticEventKind.EventPublished, message.Name, message.Arguments, $"Published '{message.Name}'");
        }

        public void RecordPublishedEvent(EventScriptEmittedEvent emittedEvent)
        {
            EmittedEvents.Add(emittedEvent);
        }

        public void CaptureVariables(IReadOnlyDictionary<string, EventScriptValue> variables)
        {
            foreach (var pair in variables)
            {
                _variables[pair.Key] = pair.Value;
            }
        }

        public void RecordDiagnostic(EventScriptDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, EventScriptValue> arguments, string? detail = null)
            => diagnosticCollector?.Record(kind, name, arguments, detail);
    }

    private abstract record MessageSubscription(EventScriptMessageSignature Definition, int Priority, long RegistrationOrder);

    private sealed record ScriptMessageSubscription(
        EventScriptMessageSignature Definition,
        int Priority,
        long RegistrationOrder,
        CompiledEventScript CompiledScript,
        CompiledEventScriptHandler Handler) : MessageSubscription(Definition, Priority, RegistrationOrder);

    private sealed record ExternalMessageSubscription(
        EventScriptMessageSignature Definition,
        int Priority,
        long RegistrationOrder,
        Action<EventScriptSubscriptionContext> Handler) : MessageSubscription(Definition, Priority, RegistrationOrder);

    #endregion
}