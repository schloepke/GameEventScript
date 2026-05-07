using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.BytecodeVM;

internal static class GesBytecodeVmExecutableBuilder
{
    public static GesBytecodeVmExecutable Build(GameEventScriptCompiled compiled)
    {
        _ = compiled ?? throw new ArgumentNullException(nameof(compiled));
        var linearExecutable = GesBytecodeVmLinearExecutable.Build(compiled);
        var handlers = compiled.Handlers.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<GesBytecodeVmCompiledHandler>)pair.Value
                .Select(handler => new GesBytecodeVmCompiledHandler(handler, compiled.Options.EnableDiagnostics))
                .ToArray(),
            StringComparer.Ordinal);

        return new GesBytecodeVmExecutable(
            compiled.Options,
            compiled,
            linearExecutable,
            handlers,
            compiled.TypeDefinitions);
    }
}
