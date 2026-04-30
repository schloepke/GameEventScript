#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.Flow.EventScript.RegisterVM;

namespace StepH.Flow.EventScript.Runtime;

public static class EventScriptDynamicLinker
{
    public static void Bind(RegisterCompiledEventScript compiledScript, IEventScriptExtensionRegistry registry)
    {
        _ = compiledScript ?? throw new ArgumentNullException(nameof(compiledScript));
        _ = registry ?? throw new ArgumentNullException(nameof(registry));
        compiledScript.BindExtensions(registry);
    }
}
