using System;
using System.Collections.Generic;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Experimental;

internal sealed class ExperimentalCompiledExecutionContext
{
    private readonly Stack<Dictionary<string, EventScriptValue>> _scopes = new();
    private readonly EventScriptContext _context;
    private readonly bool _diagnosticsEnabled;

    public ExperimentalCompiledExecutionContext(EventScriptContext context, bool diagnosticsEnabled)
    {
        _context = context;
        _diagnosticsEnabled = diagnosticsEnabled;
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
        _context.Publish(publishedMessage);
        EventScriptInvocationKernel.RecordDiagnostic(
            _context,
            _diagnosticsEnabled,
            EventScriptDiagnosticEventKind.EventPublished,
            publishedMessage.Name,
            publishedMessage.Arguments,
            $"Published '{publishedMessage.Name}'");
    }

    public void RecordDiagnostic(EventScriptDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, EventScriptValue> arguments, string? detail = null)
        => EventScriptInvocationKernel.RecordDiagnostic(_context, _diagnosticsEnabled, kind, name, arguments, detail);
}
