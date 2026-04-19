#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Linq;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Interpreter;

public sealed class EventScriptDiagnosticInterpreter
{
    private readonly CompiledEventScript _compiledScript;

    private EventScriptDiagnosticInterpreter(CompiledEventScript compiledScript)
    {
        _compiledScript = compiledScript ?? throw new ArgumentNullException(nameof(compiledScript));
    }

    public static EventScriptDiagnosticInterpreter Compile(string script)
        => Compile(EventScriptInterpreter.CompileScript(
            EventScriptLinkBuilder.LinkScripts(script),
            new EventScriptInterpreterCompilationOptions { EnableDiagnostics = true }));

    public static EventScriptDiagnosticInterpreter Compile(EventScriptModule eventScriptModule)
    {
        _ = eventScriptModule ?? throw new ArgumentNullException(nameof(eventScriptModule));
        return Compile(EventScriptInterpreter.CompileScript(
            new EventScriptLinkBuilder().AddModule(eventScriptModule).Link(),
            new EventScriptInterpreterCompilationOptions { EnableDiagnostics = true }));
    }

    public static EventScriptDiagnosticInterpreter Compile(LinkedEventScriptModule linkedModule)
    {
        _ = linkedModule ?? throw new ArgumentNullException(nameof(linkedModule));
        return Compile(EventScriptInterpreter.CompileScript(
            linkedModule,
            new EventScriptInterpreterCompilationOptions { EnableDiagnostics = true }));
    }

    public static EventScriptDiagnosticInterpreter Compile(CompiledEventScript compiledScript)
        => new(compiledScript);

    public EventScriptDiagnosticInvocationResult Invoke(string message, params EventScriptValue[] args)
        => Invoke(message, invocationContext: null, args);

    public EventScriptDiagnosticInvocationResult Invoke(string message, EventScriptInvocationContext? invocationContext, params EventScriptValue[] args)
        => EventScriptInvocationEngine
            .Compile(_compiledScript, invocationContext?.Random)
            .Invoke(message, args);

    public EventScriptDiagnosticInvocationResult InvokeClr(string message, params object?[] args)
        => Invoke(message, EventScriptValue.FromClrList(args).ToArray());

    public EventScriptDiagnosticInvocationResult InvokeClr(string message, EventScriptInvocationContext? invocationContext, params object?[] args)
        => Invoke(message, invocationContext, EventScriptValue.FromClrList(args).ToArray());
}
