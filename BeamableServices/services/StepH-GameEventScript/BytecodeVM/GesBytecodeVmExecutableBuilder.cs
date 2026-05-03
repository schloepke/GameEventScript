using System;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.BytecodeVM;

internal static class GesBytecodeVmExecutableBuilder
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
