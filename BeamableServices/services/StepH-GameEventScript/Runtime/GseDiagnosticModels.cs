#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

public enum GseDiagnosticEventKind
{
    DispatchStarted,
    SubscriberMatched,
    SubscriberInvoked,
    DispatchCompleted,
    HandlerInvoked,
    ParameterBound,
    LetEvaluated,
    ExpressionEvaluatedToNothing,
    RuleCalled,
    SelectCalled,
    EventPublished,
    RuntimeLimitReached
}

public sealed record GseDiagnosticEvent(
    int Sequence,
    GseDiagnosticEventKind Kind,
    string Name,
    GseNamedArguments Arguments,
    string? Detail = null)
{
    public override string ToString()
    {
        var call = Name + "(" + Arguments + ")";
        return $"[#{Sequence:D5}] {Kind,-18}: {call} => {Detail}";
    }
}

public interface IGseDiagnosticCollector
{
    void Record(GseDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, GseValue> arguments, string? detail = null);
}

public sealed class GseDiagnosticTraceCollector : IGseDiagnosticCollector
{
    private readonly List<GseDiagnosticEvent> _events = [];
    private int _sequence;

    public IReadOnlyList<GseDiagnosticEvent> Events => _events;

    public void Record(GseDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, GseValue> arguments, string? detail = null)
        => _events.Add(new GseDiagnosticEvent(++_sequence, kind, name, GseNamedArguments.Create(arguments), detail));

    public override string ToString() => _events.Count == 0
        ? "GseDiagnosticTraceCollector: no events"
        : $"GseDiagnosticTraceCollector: {_events.Count} events\n{_events.Select(e => e.ToString()).Aggregate((a, b) => a + "\n" + b)}";
}
