#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Interpreter;

public sealed record EventScriptEmittedEvent(string Message, IReadOnlyList<EventScriptValue> Arguments);

public sealed record EventScriptExecutionResult(string Message, IReadOnlyList<EventScriptEmittedEvent> EmittedEvents, IReadOnlyDictionary<string, EventScriptValue> Variables);

public interface IEventScriptRandom
{
    int NextInclusive(int minInclusive, int maxInclusive);
}

public sealed class DefaultEventScriptRandom(Random? random = null) : IEventScriptRandom
{
    private readonly Random _random = random ?? new Random();

    public int NextInclusive(int minInclusive, int maxInclusive)
    {
        if (minInclusive > maxInclusive)
        {
            (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        }

        if (maxInclusive != int.MaxValue) return _random.Next(minInclusive, maxInclusive + 1);
        var sample = _random.NextDouble();
        return minInclusive + (int)Math.Floor(sample * ((long)maxInclusive - minInclusive + 1));
    }
}

public sealed class EventScriptRuntimeException(string message) : Exception(message);

public sealed class EventScriptInterpreter
{
    private readonly EventScriptInvocationEngine _engine;

    private EventScriptInterpreter(EventScriptInvocationEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public static EventScriptInterpretationModel CompileModel(string script, EventScriptInterpreterCompilationOptions? options = null)
        => CompileModel(EventScriptLinkBuilder.LinkScripts(script), options);

    public static EventScriptInterpretationModel CompileModel(EventScriptModule eventScriptModule, EventScriptInterpreterCompilationOptions? options = null)
    {
        _ = eventScriptModule ?? throw new ArgumentNullException(nameof(eventScriptModule));
        return CompileModel(new EventScriptLinkBuilder().AddModule(eventScriptModule).Link(), options);
    }

    public static EventScriptInterpretationModel CompileModel(LinkedEventScriptModule linkedModule, EventScriptInterpreterCompilationOptions? options = null)
    {
        _ = linkedModule ?? throw new ArgumentNullException(nameof(linkedModule));
        return new EventScriptInterpretationModel(linkedModule, options);
    }

    public static EventScriptInterpreter Compile(string script, IEventScriptRandom? random = null, EventScriptCompilationContext? context = null)
        => Compile(CompileModel(script), random, context);

    public static EventScriptInterpreter Compile(EventScriptModule eventScriptModule, IEventScriptRandom? random = null, EventScriptCompilationContext? context = null)
        => Compile(CompileModel(eventScriptModule), random, context);

    public static EventScriptInterpreter Compile(LinkedEventScriptModule linkedModule, IEventScriptRandom? random = null, EventScriptCompilationContext? context = null)
        => Compile(CompileModel(linkedModule), random, context);

    public static EventScriptInterpreter Compile(EventScriptInterpretationModel interpretationModel, IEventScriptRandom? random = null, EventScriptCompilationContext? context = null)
    {
        _ = interpretationModel ?? throw new ArgumentNullException(nameof(interpretationModel));
        return new EventScriptInterpreter(EventScriptInvocationEngine.Compile(interpretationModel, random, context));
    }

    public EventScriptExecutionResult Emit(string message, params EventScriptValue[] args)
        => _engine.Emit(message, args);

    public EventScriptExecutionResult EmitClr(string message, params object?[] args)
        => Emit(message, EventScriptValue.FromClrList(args).ToArray());

    public EventScriptRun Enqueue(string message, params EventScriptValue[] args)
        => new(_engine.Enqueue(message, args));

    public sealed class EventScriptRun
    {
        private readonly EventScriptInvocationEngine.EventScriptRun _innerRun;

        internal EventScriptRun(EventScriptInvocationEngine.EventScriptRun innerRun)
        {
            _innerRun = innerRun ?? throw new ArgumentNullException(nameof(innerRun));
        }

        public EventScriptExecutionResult Drain() => _innerRun.Drain();
    }
}
