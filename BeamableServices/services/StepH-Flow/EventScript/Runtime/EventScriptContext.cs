#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Runtime;

public sealed class EventScriptContext
{
    private readonly Action<EventScriptMessage> _publish;

    public EventScriptContext(EventScriptRandomGenerator random, Action<EventScriptMessage> publish, IEventScriptDiagnosticCollector? diagnosticCollector = null)
    {
        Random = random ?? throw new ArgumentNullException(nameof(random));
        _publish = publish ?? throw new ArgumentNullException(nameof(publish));
        DiagnosticCollector = diagnosticCollector;
    }

    public EventScriptRandomGenerator Random { get; }

    public IEventScriptDiagnosticCollector? DiagnosticCollector { get; }

    public void Publish(EventScriptMessage message)
    {
        if (message is null || string.IsNullOrWhiteSpace(message.Name))
        {
            return;
        }

        _publish(message);
    }

    public void Publish(string message, IReadOnlyDictionary<string, EventScriptValue> args)
        => Publish(EventScriptMessage.Message(message, args));

    public void Publish(string message)
        => Publish(EventScriptMessage.Message(message));

    public void Publish(string message, params (string name, EventScriptValue value)[] args)
        => Publish(EventScriptMessage.Message(message, args));

    public void RecordDiagnostic(EventScriptDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, EventScriptValue> arguments, string? detail = null)
        => DiagnosticCollector?.Record(kind, name, arguments, detail);
}

