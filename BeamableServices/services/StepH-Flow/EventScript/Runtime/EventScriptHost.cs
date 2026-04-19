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

        var interpreter = EventScriptInterpreter.Compile(compiledScript, _random);
        foreach (var handlerGroup in compiledScript.Handlers.Values)
        {
            foreach (var handler in handlerGroup)
            {
                Register(new ScriptMessageSubscription(
                    handler.Message,
                    priority ?? _options.DefaultScriptHandlerPriority,
                    _nextRegistrationOrder++,
                    interpreter,
                    handler));
            }
        }

        return this;
    }

    public EventScriptHost BindExternal(string message, Action<IReadOnlyList<EventScriptValue>> handler, int? parameterCount = null, int? priority = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message must not be null or whitespace", nameof(message));
        }

        _ = handler ?? throw new ArgumentNullException(nameof(handler));

        Register(new ExternalMessageSubscription(
            message,
            priority ?? _options.DefaultExternalHandlerPriority,
            _nextRegistrationOrder++,
            handler,
            parameterCount));
        return this;
    }

    public EventScriptExecutionResult Emit(string message, params EventScriptValue[] args)
        => Enqueue(message, args).Drain();

    public EventScriptExecutionResult EmitClr(string message, params object?[] args)
        => Emit(message, EventScriptValue.FromClrList(args).ToArray());

    public EventScriptRun Enqueue(string message, params EventScriptValue[] args)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message must not be null or whitespace", nameof(message));
        }

        return new EventScriptRun(this, message, NormalizeArgs(args), _options.MaxProcessedEventsPerRun);
    }

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
        if (!_subscriptions.TryGetValue(queuedEvent.Message, out var subscriptions))
        {
            return;
        }

        foreach (var subscription in subscriptions.OrderBy(x => x.Priority).ThenBy(x => x.RegistrationOrder))
        {
            switch (subscription)
            {
                case ScriptMessageSubscription script:
                {
                    var result = script.Interpreter.InvokeHandler(script.Handler, queuedEvent.Arguments);
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
                        external.Handler(queuedEvent.Arguments);
                    }
                    catch
                    {
                    }

                    break;
            }
        }
    }

    private static EventScriptValue[] NormalizeArgs(IEnumerable<EventScriptValue?> args)
        => args.Select(arg => arg ?? EventScriptValue.Nothing).ToArray();

    public sealed class EventScriptRun
    {
        private readonly EventScriptHost _host;
        private readonly EventScriptRunState _state;

        internal EventScriptRun(EventScriptHost host, string message, IReadOnlyList<EventScriptValue> args, int maxProcessedEventsPerRun)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _state = new EventScriptRunState(message, maxProcessedEventsPerRun);
            _state.Enqueue(message, args, captureVariables: true);
        }

        public EventScriptExecutionResult Drain() => _host.Drain(_state);
    }

    private sealed record QueuedMessage(string Message, IReadOnlyList<EventScriptValue> Arguments, bool CaptureVariables);

    private sealed class EventScriptRunState
    {
        private readonly Queue<QueuedMessage> _queue = new();
        private readonly Dictionary<string, EventScriptValue> _variables = new(StringComparer.Ordinal);
        private readonly int _maxProcessedEventsPerRun;
        private int _processedEvents;

        public EventScriptRunState(string initialMessage, int maxProcessedEventsPerRun)
        {
            InitialMessage = initialMessage;
            _maxProcessedEventsPerRun = maxProcessedEventsPerRun;
        }

        public string InitialMessage { get; }

        public IReadOnlyDictionary<string, EventScriptValue> Variables => _variables;

        public List<EventScriptEmittedEvent> EmittedEvents { get; } = new();

        public void Enqueue(string message, IReadOnlyList<EventScriptValue> args, bool captureVariables)
        {
            _queue.Enqueue(new QueuedMessage(message, args.ToArray(), captureVariables));
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
    }

    private abstract record MessageSubscription(string Message, int Priority, long RegistrationOrder);

    private sealed record ScriptMessageSubscription(
        string Message,
        int Priority,
        long RegistrationOrder,
        EventScriptInterpreter Interpreter,
        CompiledEventScriptHandler Handler) : MessageSubscription(Message, Priority, RegistrationOrder);

    private sealed record ExternalMessageSubscription(
        string Message,
        int Priority,
        long RegistrationOrder,
        Action<IReadOnlyList<EventScriptValue>> Handler,
        int? ParameterCount) : MessageSubscription(Message, Priority, RegistrationOrder);
}
