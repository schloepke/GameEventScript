using System;
using System.Collections.Generic;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Experimental;

internal sealed class ExperimentalCompiledExecutionContext
{
    private readonly Stack<Dictionary<string, EventScriptValue>> _scopes = new();
    private readonly ExperimentalCompiledRunState _state;

    public ExperimentalCompiledExecutionContext(ExperimentalCompiledRunState state)
    {
        _state = state;
        _scopes.Push(new Dictionary<string, EventScriptValue>(StringComparer.Ordinal));
    }

    public void PushScope() => _scopes.Push(new Dictionary<string, EventScriptValue>(StringComparer.Ordinal));

    public void PopScope()
    {
        if (_scopes.Count == 1)
        {
            return;
        }

        _scopes.Pop();
    }

    public void Define(string name, EventScriptValue value)
        => _scopes.Peek()[name] = value;

    public EventScriptValue Resolve(string name)
    {
        foreach (var scope in _scopes)
        {
            if (scope.TryGetValue(name, out var value))
            {
                return value;
            }
        }

        return EventScriptValue.Nothing;
    }

    public void Publish(string message, IReadOnlyDictionary<string, EventScriptValue> arguments)
    {
        var publishedMessage = new EventScriptMessage(message, arguments);
        var emittedEvent = new EventScriptEmittedEvent(publishedMessage);
        _state.RecordPublishedEvent(emittedEvent);
        _state.RecordDiagnostic(
            EventScriptDiagnosticEventKind.EventPublished,
            publishedMessage.Name,
            emittedEvent.Arguments,
            $"Published '{publishedMessage.Name}'");
    }

    public void RecordDiagnostic(EventScriptDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, EventScriptValue> arguments, string? detail = null)
        => _state.RecordDiagnostic(kind, name, arguments, detail);

    public IReadOnlyDictionary<string, EventScriptValue> SnapshotTopScope()
        => new Dictionary<string, EventScriptValue>(_scopes.Peek(), StringComparer.Ordinal);
}
