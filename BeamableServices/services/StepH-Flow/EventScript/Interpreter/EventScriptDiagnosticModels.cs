#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Interpreter;

public sealed class EventScriptInvocationContext
{
    public IEventScriptRandom? Random { get; init; }
}

public enum EventScriptDiagnosticStepKind
{
    InvocationStarted,
    HandlerMatched,
    StatementExecuting,
    VariableResolved,
    VariableAssigned,
    ExpressionEvaluated,
    EventPublished,
    InvocationCompleted
}

public sealed record EventScriptDiagnosticStep(
    int Sequence,
    EventScriptDiagnosticStepKind Kind,
    string Message,
    IReadOnlyList<EventScriptValue> Arguments,
    string? Detail = null);

public sealed record EventScriptDiagnosticInvocationResult(
    string Message,
    IReadOnlyList<EventScriptValue> Arguments,
    IReadOnlyList<EventScriptEmittedEvent> EmittedEvents,
    IReadOnlyDictionary<string, EventScriptValue> Variables,
    IReadOnlyList<EventScriptDiagnosticStep> Steps);
