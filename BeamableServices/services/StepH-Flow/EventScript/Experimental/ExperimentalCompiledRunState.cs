using System;
using System.Collections.Generic;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Experimental;

internal sealed class ExperimentalCompiledRunState
{
    private readonly Dictionary<string, EventScriptValue> _variables = new(StringComparer.Ordinal);
    private readonly IEventScriptDiagnosticCollector? _diagnosticCollector;
    private readonly bool _diagnosticsEnabled;

    public ExperimentalCompiledRunState(IEventScriptDiagnosticCollector? diagnosticCollector, bool diagnosticsEnabled)
    {
        _diagnosticCollector = diagnosticCollector;
        _diagnosticsEnabled = diagnosticsEnabled;
    }

    public IReadOnlyDictionary<string, EventScriptValue> Variables => _variables;

    public List<EventScriptEmittedEvent> EmittedEvents { get; } = new();

    public void RecordPublishedEvent(EventScriptEmittedEvent emittedEvent)
    {
        EmittedEvents.Add(emittedEvent);
    }

    public void RecordDiagnostic(EventScriptDiagnosticEventKind kind, string message, IReadOnlyDictionary<string, EventScriptValue> arguments, string? detail = null)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        _diagnosticCollector?.Record(kind, message, arguments, detail);
    }

    public void CaptureVariables(IReadOnlyDictionary<string, EventScriptValue> variables)
    {
        foreach (var pair in variables)
        {
            _variables[pair.Key] = pair.Value;
        }
    }
}