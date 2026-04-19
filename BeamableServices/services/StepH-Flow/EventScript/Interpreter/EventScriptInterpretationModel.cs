#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.Flow.EventScript.Linker;

namespace StepH.Flow.EventScript.Interpreter;

public sealed class EventScriptInterpretationModel
{
    public EventScriptInterpretationModel(LinkedEventScriptModule linkedModule, EventScriptInterpreterCompilationOptions? options = null)
    {
        LinkedModule = linkedModule ?? throw new ArgumentNullException(nameof(linkedModule));
        Options = options ?? new EventScriptInterpreterCompilationOptions();
    }

    public LinkedEventScriptModule LinkedModule { get; }

    public EventScriptInterpreterCompilationOptions Options { get; }

    public bool DiagnosticsEnabled => Options.EnableDiagnostics;
}
