#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Types;

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

    internal EventScriptHost(
        EventScriptRandomGenerator random,
        IEventScriptDiagnosticCollector? diagnosticCollector,
        int maxProcessedEventsPerRun,
        int defaultScriptHandlerPriority,
        int defaultExternalHandlerPriority)
    {
        _random = random;
        _diagnosticCollector = diagnosticCollector;
        _maxProcessedEventsPerRun = maxProcessedEventsPerRun <= 0 ? EventScriptHostBuilder.DefaultMaxProcessedEventsPerRun : maxProcessedEventsPerRun;
        _defaultScriptHandlerPriority = defaultScriptHandlerPriority;
        _defaultExternalHandlerPriority = defaultExternalHandlerPriority;
    }

    public static EventScriptHostBuilder CreateBuilder() => new();

    public EventScriptHost Load(CompiledEventScript compiledScript, int? priority = null)
    {
        _ = compiledScript ?? throw new ArgumentNullException(nameof(compiledScript));

        foreach (var handlerGroup in compiledScript.Handlers.Values)
        {
            foreach (var handler in handlerGroup)
            {
                Register(new ScriptMessageSubscription(
                    handler.Message,
                    handler.SignatureKey,
                    priority ?? _defaultScriptHandlerPriority,
                    _nextRegistrationOrder++,
                    compiledScript,
                    handler));
            }
        }

        return this;
    }

    public EventScriptHost Subscribe(
        string message,
        IReadOnlyCollection<string> parameterNames,
        Action<EventScriptSubscriptionContext> handler,
        int? priority = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message must not be null or whitespace", nameof(message));
        }

        _ = parameterNames ?? throw new ArgumentNullException(nameof(parameterNames));
        _ = handler ?? throw new ArgumentNullException(nameof(handler));

        Register(new ExternalMessageSubscription(
            message,
            EventScriptArgumentMap.CreateSignatureKey(parameterNames),
            priority ?? _defaultExternalHandlerPriority,
            _nextRegistrationOrder++,
            handler));
        return this;
    }

    public EventScriptHost Subscribe(string message, Action<EventScriptSubscriptionContext> handler, params string[] parameterNames)
        => Subscribe(message, parameterNames, handler, priority: null);

    public EventScriptHost Subscribe(string message, Action<EventScriptSubscriptionContext> handler)
        => Subscribe(message, Array.Empty<string>(), handler, priority: null);

    public EventScriptExecutionResult Publish(string message)
        => Publish(message, EventScriptArgumentMap.Empty);

    public EventScriptExecutionResult Publish(string message, IReadOnlyDictionary<string, EventScriptValue> args)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message must not be null or whitespace", nameof(message));
        }

        var invocationContext = new EventScriptInvocationContext
        {
            Random = _random
        };

        var state = new EventScriptRunState(message, _maxProcessedEventsPerRun, invocationContext, _diagnosticCollector);
        state.Enqueue(message, EventScriptArgumentMap.Normalize(args), captureVariables: true);
        return Drain(state);
    }

    public EventScriptExecutionResult Publish(string message, params (string Name, EventScriptValue Value)[] args)
        => Publish(message, args.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal));

    public EventScriptExecutionResult PublishClr(string message, IReadOnlyDictionary<string, object?> args)
        => Publish(message, EventScriptArgumentMap.FromClr(args));

    public EventScriptExecutionResult PublishClr(string message, params (string Name, object? Value)[] args)
        => PublishClr(message, args.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal));

    private void Register(MessageSubscription subscription)
    {
        if (!_subscriptions.TryGetValue(subscription.Message, out var handlers))
        {
            handlers = new List<MessageSubscription>();
            _subscriptions[subscription.Message] = handlers;
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

            Dispatch(queuedEvent, state);
        }

        return new EventScriptExecutionResult(state.InitialMessage, state.EmittedEvents, state.Variables);
    }

    private void Dispatch(QueuedMessage queuedEvent, EventScriptRunState state)
    {
        state.RecordDiagnostic(EventScriptDiagnosticEventKind.DispatchStarted, queuedEvent.Message, queuedEvent.Arguments, $"Dispatch '{queuedEvent.Message}' started");

        if (!_subscriptions.TryGetValue(queuedEvent.Message, out var subscriptions))
        {
            state.RecordDiagnostic(EventScriptDiagnosticEventKind.DispatchCompleted, queuedEvent.Message, queuedEvent.Arguments, $"Dispatch '{queuedEvent.Message}' completed without subscribers");
            return;
        }

        var signatureKey = EventScriptArgumentMap.CreateSignatureKey(queuedEvent.Arguments.Keys);
        foreach (var subscription in subscriptions
                     .Where(x => x.SignatureKey == signatureKey)
                     .OrderBy(x => x.Priority)
                     .ThenBy(x => x.RegistrationOrder))
        {
            state.RecordDiagnostic(
                EventScriptDiagnosticEventKind.SubscriberMatched,
                queuedEvent.Message,
                queuedEvent.Arguments,
                $"{subscription.GetType().Name} matched");

            switch (subscription)
            {
                case ScriptMessageSubscription script:
                {
                    state.RecordDiagnostic(EventScriptDiagnosticEventKind.SubscriberInvoked, queuedEvent.Message, queuedEvent.Arguments, "Script subscriber invoked");
                    var result = script.CompiledScript.InvokeHandler(script.Handler, queuedEvent.Arguments, state.InvocationContext, state.DiagnosticCollector);
                    if (queuedEvent.CaptureVariables)
                    {
                        state.CaptureVariables(result.Variables);
                    }

                    foreach (var emittedEvent in result.EmittedEvents)
                    {
                        state.RecordPublishedEvent(emittedEvent);
                        state.Enqueue(emittedEvent.Message, emittedEvent.Arguments, captureVariables: false);
                    }

                    break;
                }
                case ExternalMessageSubscription external:
                    try
                    {
                        state.RecordDiagnostic(EventScriptDiagnosticEventKind.SubscriberInvoked, queuedEvent.Message, queuedEvent.Arguments, "External subscriber invoked");
                        var subscriptionContext = new EventScriptSubscriptionContext(
                            queuedEvent.Message,
                            queuedEvent.Arguments,
                            (message, arguments) => state.PublishExternal(message, arguments),
                            (message, arguments) => state.PublishExternal(message, EventScriptArgumentMap.FromClr(arguments)));
                        external.Handler(subscriptionContext);
                    }
                    catch
                    {
                    }

                    break;
            }
        }

        state.RecordDiagnostic(EventScriptDiagnosticEventKind.DispatchCompleted, queuedEvent.Message, queuedEvent.Arguments, $"Dispatch '{queuedEvent.Message}' completed");
    }

    private sealed record QueuedMessage(string Message, IReadOnlyDictionary<string, EventScriptValue> Arguments, bool CaptureVariables);

    private sealed class EventScriptRunState
    {
        private readonly Queue<QueuedMessage> _queue = new();
        private readonly Dictionary<string, EventScriptValue> _variables = new(StringComparer.Ordinal);
        private readonly int _maxProcessedEventsPerRun;
        private readonly IEventScriptDiagnosticCollector? _diagnosticCollector;
        private int _processedEvents;

        public EventScriptRunState(string initialMessage, int maxProcessedEventsPerRun, EventScriptInvocationContext invocationContext, IEventScriptDiagnosticCollector? diagnosticCollector)
        {
            InitialMessage = initialMessage;
            _maxProcessedEventsPerRun = maxProcessedEventsPerRun;
            InvocationContext = invocationContext ?? throw new ArgumentNullException(nameof(invocationContext));
            _diagnosticCollector = diagnosticCollector;
        }

        public string InitialMessage { get; }

        public EventScriptInvocationContext InvocationContext { get; }

        public IEventScriptDiagnosticCollector? DiagnosticCollector => _diagnosticCollector;

        public IReadOnlyDictionary<string, EventScriptValue> Variables => _variables;

        public List<EventScriptEmittedEvent> EmittedEvents { get; } = new();

        public void Enqueue(string message, IReadOnlyDictionary<string, EventScriptValue> args, bool captureVariables)
        {
            _queue.Enqueue(new QueuedMessage(message, EventScriptArgumentMap.Normalize(args), captureVariables));
        }

        public bool TryDequeue(out QueuedMessage queuedEvent)
        {
            if (_queue.Count == 0)
            {
                queuedEvent = default!;
                return false;
            }

            queuedEvent = _queue.Dequeue();
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

        public void PublishExternal(string message, IReadOnlyDictionary<string, EventScriptValue> args)
        {
            var normalizedArguments = EventScriptArgumentMap.Normalize(args);
            var emittedEvent = new EventScriptEmittedEvent(message, EventScriptNamedArguments.Create(normalizedArguments));
            RecordPublishedEvent(emittedEvent);
            Enqueue(message, normalizedArguments, captureVariables: false);
            RecordDiagnostic(EventScriptDiagnosticEventKind.EventPublished, message, normalizedArguments, $"Published '{message}'");
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
            => _diagnosticCollector?.Record(kind, name, arguments, detail);
    }

    private abstract record MessageSubscription(string Message, string SignatureKey, int Priority, long RegistrationOrder);

    private sealed record ScriptMessageSubscription(
        string Message,
        string SignatureKey,
        int Priority,
        long RegistrationOrder,
        CompiledEventScript CompiledScript,
        CompiledEventScriptHandler Handler) : MessageSubscription(Message, SignatureKey, Priority, RegistrationOrder);

    private sealed record ExternalMessageSubscription(
        string Message,
        string SignatureKey,
        int Priority,
        long RegistrationOrder,
        Action<EventScriptSubscriptionContext> Handler) : MessageSubscription(Message, SignatureKey, Priority, RegistrationOrder);
}
