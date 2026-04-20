#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Interpreter;

public sealed class EventScriptInvocationContext
{
    public EventScriptRandomGenerator? Random { get; set; }
}

public enum EventScriptDiagnosticEventKind
{
    DispatchStarted,
    SubscriberMatched,
    SubscriberInvoked,
    DispatchCompleted,
    HandlerInvoked,
    ParameterBound,
    RuleCalled,
    SelectCalled,
    EventPublished
}

public sealed record EventScriptDiagnosticEvent(
    int Sequence,
    EventScriptDiagnosticEventKind Kind,
    string Name,
    EventScriptNamedArguments Arguments,
    string? Detail = null);

public interface IEventScriptDiagnosticCollector
{
    void Record(EventScriptDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, EventScriptValue> arguments, string? detail = null);
}

public sealed class EventScriptDiagnosticTraceCollector : IEventScriptDiagnosticCollector
{
    private readonly List<EventScriptDiagnosticEvent> _events = [];
    private int _sequence;

    public IReadOnlyList<EventScriptDiagnosticEvent> Events => _events;

    public void Record(EventScriptDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, EventScriptValue> arguments, string? detail = null)
        => _events.Add(new EventScriptDiagnosticEvent(++_sequence, kind, name, EventScriptNamedArguments.Create(arguments), detail));
}
