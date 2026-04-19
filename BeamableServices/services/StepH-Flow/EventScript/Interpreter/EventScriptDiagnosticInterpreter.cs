#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Linq;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;

namespace StepH.Flow.EventScript.Interpreter;

public sealed class EventScriptDiagnosticInterpreter
{
    private readonly EventScriptInterpretationModel _interpretationModel;

    private EventScriptDiagnosticInterpreter(EventScriptInterpretationModel interpretationModel)
    {
        _interpretationModel = interpretationModel ?? throw new ArgumentNullException(nameof(interpretationModel));
    }

    public static EventScriptDiagnosticInterpreter Compile(string script)
        => Compile(EventScriptInterpreter.CompileModel(
            EventScriptLinkBuilder.LinkScripts(script),
            new EventScriptInterpreterCompilationOptions { EnableDiagnostics = true }));

    public static EventScriptDiagnosticInterpreter Compile(EventScriptModule eventScriptModule)
    {
        _ = eventScriptModule ?? throw new ArgumentNullException(nameof(eventScriptModule));
        return Compile(EventScriptInterpreter.CompileModel(
            new EventScriptLinkBuilder().AddModule(eventScriptModule).Link(),
            new EventScriptInterpreterCompilationOptions { EnableDiagnostics = true }));
    }

    public static EventScriptDiagnosticInterpreter Compile(LinkedEventScriptModule linkedModule)
    {
        _ = linkedModule ?? throw new ArgumentNullException(nameof(linkedModule));
        return Compile(EventScriptInterpreter.CompileModel(
            linkedModule,
            new EventScriptInterpreterCompilationOptions { EnableDiagnostics = true }));
    }

    public static EventScriptDiagnosticInterpreter Compile(EventScriptInterpretationModel interpretationModel)
        => new(interpretationModel);

    public EventScriptDiagnosticInvocationResult Invoke(string message, params EventScriptValue[] args)
        => Invoke(message, invocationContext: null, args);

    public EventScriptDiagnosticInvocationResult Invoke(string message, EventScriptInvocationContext? invocationContext, params EventScriptValue[] args)
        => EventScriptDiagnosticExecutionEngine
            .Compile(_interpretationModel, invocationContext?.Random)
            .Invoke(message, args);

    public EventScriptDiagnosticInvocationResult InvokeClr(string message, params object?[] args)
        => Invoke(message, EventScriptValue.FromClrList(args).ToArray());

    public EventScriptDiagnosticInvocationResult InvokeClr(string message, EventScriptInvocationContext? invocationContext, params object?[] args)
        => Invoke(message, invocationContext, EventScriptValue.FromClrList(args).ToArray());
}
