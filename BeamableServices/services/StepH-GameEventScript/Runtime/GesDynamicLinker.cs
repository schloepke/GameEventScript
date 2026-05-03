#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.BytecodeVM;

namespace StepH.GameEventScript.Runtime;

internal static class GesDynamicLinker
{
    public static void Bind(GseBytecodeVmExecutable compiledScript, IGameEventScriptExtensionRegistry registry)
    {
        _ = compiledScript ?? throw new ArgumentNullException(nameof(compiledScript));
        _ = registry ?? throw new ArgumentNullException(nameof(registry));
        compiledScript.BindExtensions(registry);
    }
}
