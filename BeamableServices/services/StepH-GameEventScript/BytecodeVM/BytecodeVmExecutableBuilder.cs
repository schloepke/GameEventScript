#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.BytecodeVM;

internal static class BytecodeVmExecutableBuilder
{
    public static GseBytecodeVmExecutable Build(GameEventScriptCompiled compiled)
    {
        _ = compiled ?? throw new ArgumentNullException(nameof(compiled));
        return new GseBytecodeVmExecutable(
            compiled.Options,
            compiled,
            compiled.Handlers,
            compiled.TypeDefinitions);
    }
}
