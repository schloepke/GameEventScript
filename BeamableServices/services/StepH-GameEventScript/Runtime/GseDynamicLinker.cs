#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.RegisterVM;

namespace StepH.GameEventScript.Runtime;

public static class GseDynamicLinker
{
    public static void Bind(RegisterCompiledGse compiledScript, IGseExtensionRegistry registry)
    {
        _ = compiledScript ?? throw new ArgumentNullException(nameof(compiledScript));
        _ = registry ?? throw new ArgumentNullException(nameof(registry));
        compiledScript.BindExtensions(registry);
    }
}
