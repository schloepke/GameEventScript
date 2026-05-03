#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.BytecodeVM;

namespace StepH.GameEventScript.Runtime;

public static class GameEventScriptDynamicLinker
{
    public static void Bind(CompiledGameEventScript compiledScript, IGameEventScriptExtensionRegistry registry)
    {
        _ = compiledScript ?? throw new ArgumentNullException(nameof(compiledScript));
        _ = registry ?? throw new ArgumentNullException(nameof(registry));
        compiledScript.BindExtensions(registry);
    }
}
