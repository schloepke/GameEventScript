#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

public enum GameEventScriptDiagnosticEventKind
{
    DispatchStarted,
    SubscriberMatched,
    SubscriberInvoked,
    DispatchCompleted,
    HandlerInvoked,
    ParameterBound,
    LetEvaluated,
    ExpressionEvaluatedToNothing,
    PredicateCalled,
    FunctionCalled,
    EventPublished,
    RuntimeLimitReached
}


public interface IGameEventScriptDiagnosticCollector
{
    void Record(GameEventScriptDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, GameEventScriptValue> arguments, string? detail = null);
}


public sealed record GameEventScriptDiagnosticEvent(int Sequence, GameEventScriptDiagnosticEventKind Kind, string Name, GameEventScriptNamedArguments Arguments, string? Detail = null)
{
    public override string ToString()
    {
        var call = Name + "(" + Arguments + ")";
        return $"[#{Sequence:D5}] {Kind,-18}: {call} => {Detail}";
    }
}


public sealed class GameEventScriptDiagnosticTraceCollector : IGameEventScriptDiagnosticCollector
{
    private readonly List<GameEventScriptDiagnosticEvent> _events = [];
    private int _sequence;

    public IReadOnlyList<GameEventScriptDiagnosticEvent> Events => _events;

    public void Record(GameEventScriptDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, GameEventScriptValue> arguments, string? detail = null)
        => _events.Add(new GameEventScriptDiagnosticEvent(++_sequence, kind, name, GameEventScriptNamedArguments.Create(arguments), detail));

    public override string ToString() => _events.Count == 0
        ? "GameEventScriptDiagnosticTraceCollector: no events"
        : $"GameEventScriptDiagnosticTraceCollector: {_events.Count} events\n{_events.Select(e => e.ToString()).Aggregate((a, b) => a + "\n" + b)}";
}