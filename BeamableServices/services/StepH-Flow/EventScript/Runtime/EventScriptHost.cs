#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Runtime;

public sealed class EventScriptHost
{
    private readonly EventScriptHostOptions _options;
    private readonly IEventScriptRandom? _random;
    private readonly Dictionary<string, List<MessageSubscription>> _subscriptions = new(StringComparer.Ordinal);
    private long _nextRegistrationOrder;

    public EventScriptHost(IEventScriptRandom? random = null, EventScriptHostOptions? options = null)
    {
        _random = random;
        _options = options ?? new EventScriptHostOptions();
        if (_options.MaxProcessedEventsPerRun <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxProcessedEventsPerRun must be greater than zero.");
        }
    }

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
                    priority ?? _options.DefaultScriptHandlerPriority,
                    _nextRegistrationOrder++,
                    compiledScript,
                    handler));
            }
        }

        return this;
    }

    public EventScriptHost BindExternal(
        string message,
        IReadOnlyCollection<string> parameterNames,
        Action<IReadOnlyDictionary<string, EventScriptValue>> handler,
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
            priority ?? _options.DefaultExternalHandlerPriority,
            _nextRegistrationOrder++,
            handler));
        return this;
    }

    public EventScriptHost BindExternal(
        string message,
        Action<IReadOnlyDictionary<string, EventScriptValue>> handler,
        params string[] parameterNames)
        => BindExternal(message, parameterNames, handler, priority: null);

    public EventScriptExecutionResult Emit(string message)
        => Enqueue(message, EventScriptArgumentMap.Empty).Drain();

    public EventScriptExecutionResult Emit(string message, IReadOnlyDictionary<string, EventScriptValue> args)
        => Enqueue(message, args).Drain();

    public EventScriptExecutionResult Emit(
        string message,
        IReadOnlyDictionary<string, EventScriptValue> args,
        EventScriptInvocationContext? invocationContext,
        IEventScriptDiagnosticCollector? diagnosticCollector)
        => Enqueue(message, args, invocationContext, diagnosticCollector).Drain();

    public EventScriptExecutionResult Emit(string message, params (string Name, EventScriptValue Value)[] args)
        => Emit(message, args.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal));

    public EventScriptExecutionResult EmitClr(string message, IReadOnlyDictionary<string, object?> args)
        => Emit(message, EventScriptArgumentMap.FromClr(args));

    public EventScriptExecutionResult EmitClr(string message, params (string Name, object? Value)[] args)
        => EmitClr(message, args.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal));

    public EventScriptRun Enqueue(string message, IReadOnlyDictionary<string, EventScriptValue> args)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message must not be null or whitespace", nameof(message));
        }

        return new EventScriptRun(this, message, EventScriptArgumentMap.Normalize(args), _options.MaxProcessedEventsPerRun, invocationContext: null, diagnosticCollector: null);
    }

    public EventScriptRun Enqueue(
        string message,
        IReadOnlyDictionary<string, EventScriptValue> args,
        EventScriptInvocationContext? invocationContext,
        IEventScriptDiagnosticCollector? diagnosticCollector)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message must not be null or whitespace", nameof(message));
        }

        return new EventScriptRun(this, message, EventScriptArgumentMap.Normalize(args), _options.MaxProcessedEventsPerRun, invocationContext, diagnosticCollector);
    }

    public EventScriptRun Enqueue(string message, params (string Name, EventScriptValue Value)[] args)
        => Enqueue(message, args.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal));

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
                        external.Handler(queuedEvent.Arguments);
                    }
                    catch
                    {
                    }

                    break;
            }
        }

        state.RecordDiagnostic(EventScriptDiagnosticEventKind.DispatchCompleted, queuedEvent.Message, queuedEvent.Arguments, $"Dispatch '{queuedEvent.Message}' completed");
    }

    public sealed class EventScriptRun
    {
        private readonly EventScriptHost _host;
        private readonly EventScriptRunState _state;

        internal EventScriptRun(
            EventScriptHost host,
            string message,
            IReadOnlyDictionary<string, EventScriptValue> args,
            int maxProcessedEventsPerRun,
            EventScriptInvocationContext? invocationContext,
            IEventScriptDiagnosticCollector? diagnosticCollector)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            invocationContext ??= new EventScriptInvocationContext();
            invocationContext.Random ??= host._random;
            _state = new EventScriptRunState(message, maxProcessedEventsPerRun, invocationContext, diagnosticCollector);
            _state.Enqueue(message, args, captureVariables: true);
        }

        public EventScriptExecutionResult Drain() => _host.Drain(_state);
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
        Action<IReadOnlyDictionary<string, EventScriptValue>> Handler) : MessageSubscription(Message, SignatureKey, Priority, RegistrationOrder);
}
