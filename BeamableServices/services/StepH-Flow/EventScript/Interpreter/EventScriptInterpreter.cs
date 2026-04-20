#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;

namespace StepH.Flow.EventScript.Interpreter;

public static class EventScriptInterpreter
{
    public static CompiledEventScript Compile(LinkedEventScriptModule linkedModule, IEventScriptRandom? random)
        => CompileScript(linkedModule, options: null, random);

    public static CompiledEventScript Compile(LinkedEventScriptModule linkedModule, EventScriptInterpreterCompilationOptions? options = null)
        => CompileScript(linkedModule, options);

    public static CompiledEventScript CompileScript(LinkedEventScriptModule linkedModule, EventScriptInterpreterCompilationOptions? options = null)
    {
        _ = linkedModule ?? throw new ArgumentNullException(nameof(linkedModule));
        return new CompiledEventScript(linkedModule, options);
    }

    public static CompiledEventScript CompileScript(LinkedEventScriptModule linkedModule, EventScriptInterpreterCompilationOptions? options, IEventScriptRandom? random)
    {
        _ = linkedModule ?? throw new ArgumentNullException(nameof(linkedModule));
        return new CompiledEventScript(linkedModule, options, random);
    }
}
