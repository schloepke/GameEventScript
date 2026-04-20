#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Interpreter;

public sealed record EventScriptEmittedEvent(string Message, EventScriptNamedArguments Arguments);

public sealed record EventScriptExecutionResult(string Message, IReadOnlyList<EventScriptEmittedEvent> EmittedEvents, IReadOnlyDictionary<string, EventScriptValue> Variables);

public sealed class EventScriptInterpreter
{
    private readonly EventScriptInvocationEngine _engine;

    private EventScriptInterpreter(EventScriptInvocationEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public static CompiledEventScript CompileScript(string script, EventScriptInterpreterCompilationOptions? options = null)
        => CompileScript(EventScriptLinkBuilder.LinkScripts(script), options);

    public static CompiledEventScript CompileScript(EventScriptModule eventScriptModule, EventScriptInterpreterCompilationOptions? options = null)
    {
        _ = eventScriptModule ?? throw new ArgumentNullException(nameof(eventScriptModule));
        return CompileScript(new EventScriptLinkBuilder().AddModule(eventScriptModule).Link(), options);
    }

    public static CompiledEventScript CompileScript(LinkedEventScriptModule linkedModule, EventScriptInterpreterCompilationOptions? options = null)
    {
        _ = linkedModule ?? throw new ArgumentNullException(nameof(linkedModule));
        return new CompiledEventScript(linkedModule, options);
    }

    public static EventScriptInterpreter Compile(string script, IEventScriptRandom? random = null)
        => Compile(CompileScript(script), random);

    public static EventScriptInterpreter Compile(EventScriptModule eventScriptModule, IEventScriptRandom? random = null)
        => Compile(CompileScript(eventScriptModule), random);

    public static EventScriptInterpreter Compile(LinkedEventScriptModule linkedModule, IEventScriptRandom? random = null)
        => Compile(CompileScript(linkedModule), random);

    public static EventScriptInterpreter Compile(CompiledEventScript compiledScript, IEventScriptRandom? random = null)
    {
        _ = compiledScript ?? throw new ArgumentNullException(nameof(compiledScript));
        return new EventScriptInterpreter(EventScriptInvocationEngine.Compile(compiledScript, random));
    }

    public EventScriptExecutionResult Invoke(string message)
        => _engine.InvokeMessage(message, EventScriptArgumentMap.Empty);

    public EventScriptExecutionResult Invoke(string message, IReadOnlyDictionary<string, EventScriptValue> args)
        => _engine.InvokeMessage(message, args);

    public EventScriptExecutionResult Emit(string message)
        => Invoke(message);

    public EventScriptExecutionResult Emit(string message, IReadOnlyDictionary<string, EventScriptValue> args)
        => Invoke(message, args);

    public EventScriptExecutionResult Emit(string message, params (string Name, EventScriptValue Value)[] args)
        => Emit(message, args.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal));

    public EventScriptExecutionResult EmitClr(string message, IReadOnlyDictionary<string, object?> args)
        => Invoke(message, EventScriptArgumentMap.FromClr(args));

    public EventScriptExecutionResult EmitClr(string message, params (string Name, object? Value)[] args)
        => EmitClr(message, args.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal));

    internal EventScriptExecutionResult InvokeHandler(CompiledEventScriptHandler handler, IReadOnlyDictionary<string, EventScriptValue> args)
        => _engine.InvokeHandler(handler, args);
}
