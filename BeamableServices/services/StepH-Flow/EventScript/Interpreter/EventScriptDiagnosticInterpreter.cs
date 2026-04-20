#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
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
        => Compile(EventScriptInterpreter.CompileScript(EventScriptLinkBuilder.LinkScripts(script), new EventScriptInterpreterCompilationOptions { EnableDiagnostics = true }));

    public static EventScriptDiagnosticInterpreter Compile(EventScriptModule eventScriptModule)
    {
        _ = eventScriptModule ?? throw new ArgumentNullException(nameof(eventScriptModule));
        return Compile(EventScriptInterpreter.CompileScript(new EventScriptLinkBuilder().AddModule(eventScriptModule).Link(), new EventScriptInterpreterCompilationOptions { EnableDiagnostics = true }));
    }

    public static EventScriptDiagnosticInterpreter Compile(LinkedEventScriptModule linkedModule)
    {
        _ = linkedModule ?? throw new ArgumentNullException(nameof(linkedModule));
        return Compile(EventScriptInterpreter.CompileScript(linkedModule, new EventScriptInterpreterCompilationOptions { EnableDiagnostics = true }));
    }

    public static EventScriptDiagnosticInterpreter Compile(CompiledEventScript compiledScript)
        => new(compiledScript);

    public EventScriptDiagnosticInvocationResult Invoke(string message)
        => Invoke(message, invocationContext: null, EventScriptArgumentMap.Empty);

    public EventScriptDiagnosticInvocationResult Invoke(string message, IReadOnlyDictionary<string, EventScriptValue> args)
        => Invoke(message, invocationContext: null, args);

    public EventScriptDiagnosticInvocationResult Invoke(string message, params (string Name, EventScriptValue Value)[] args)
        => Invoke(message, args.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal));

    public EventScriptDiagnosticInvocationResult Invoke(string message, EventScriptInvocationContext? invocationContext, IReadOnlyDictionary<string, EventScriptValue> args)
        => EventScriptInvocationEngine.Compile(_compiledScript, invocationContext?.Random).Invoke(message, args);

    public EventScriptDiagnosticInvocationResult InvokeClr(string message, IReadOnlyDictionary<string, object?> args)
        => Invoke(message, EventScriptArgumentMap.FromClr(args));

    public EventScriptDiagnosticInvocationResult InvokeClr(string message, params (string Name, object? Value)[] args)
        => InvokeClr(message, args.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal));

    public EventScriptDiagnosticInvocationResult InvokeClr(string message, EventScriptInvocationContext? invocationContext, IReadOnlyDictionary<string, object?> args)
        => Invoke(message, invocationContext, EventScriptArgumentMap.FromClr(args));
}