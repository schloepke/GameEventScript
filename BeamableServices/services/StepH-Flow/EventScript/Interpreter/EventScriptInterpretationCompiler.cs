#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.Flow.EventScript.Linker;

namespace StepH.Flow.EventScript.Interpreter;

public static class EventScriptInterpretationCompiler
{
    public static CompiledEventScript Compile(LinkedEventScriptModule linkedModule, EventScriptInterpreterCompilationOptions? options = null)
    {
        _ = linkedModule ?? throw new ArgumentNullException(nameof(linkedModule));
        return new CompiledEventScript(linkedModule, options);
    }
}
