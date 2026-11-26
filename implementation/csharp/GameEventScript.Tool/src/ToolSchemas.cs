// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tool;

// The schema set of `--schema`: every handle type that the given schema sources declare, read as one schema set. The
// CLI registers no resolver, so only programs that do not read handle state can load.
internal static class ToolSchemas
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal const string Option = "--schema";

    internal static IReadOnlyList<GesHandleType>? Load(IReadOnlyList<string> arguments, ref string? activePath)
        => Sources(arguments, ref activePath) is { } sources ? [.. GesSchemaSet.Parse(sources).HandleTypes] : null;

    // The strict UTF-8 sources of `--schema`, each named by its path as given or expanded; null without the option.
    internal static GesSchemaSource[]? Sources(IReadOnlyList<string> arguments, ref string? activePath)
    {
        if (arguments.Count == 0) return null;
        var paths = CompileSources.Expand(arguments);
        var sources = new GesSchemaSource[paths.Count];
        for (var index = 0; index < sources.Length; index++)
        {
            activePath = paths[index];
            sources[index] = new GesSchemaSource(paths[index], StrictUtf8.GetString(File.ReadAllBytes(paths[index])));
        }
        return sources;
    }

    // Reported as `ges schema check` reports it.
    internal static void Report(GesSchemaException exception)
    {
        var place = exception.Line > 0 ? $"{exception.SourceName}({exception.Line},{exception.Column})" : "schema";
        Console.Error.WriteLine($"{place}: error {exception.Code}: {exception.Message}");
    }

    internal static GesProgramBuilder Builder(IReadOnlyList<GesHandleType>? handleTypes)
    {
        var builder = new GesProgramBuilder();
        if (handleTypes is { Count: > 0 }) builder.WithHandleTypes(handleTypes);
        return builder;
    }

    // Checks every handle type a binary Program imports against the schemas of `--schema`. A type the schemas declare
    // links on a throwaway host whose resolvers are never called, so the comparison is the runtime's own; a type they
    // do not declare fails with the same code. Without `--schema` nothing is checked.
    internal static void CheckImports(GameEventScriptProgram program, IReadOnlyList<GesHandleType>? handleTypes)
    {
        if (handleTypes is null) return;
        GesHostBuilder? builder = null;
        foreach (var bind in program.Bindings.Entries)
        {
            if (bind.Kind != GesBinaryBindKind.HandleType) continue;
            var name = program.StringConstants.Resolve(bind.Name);
            var type = handleTypes.FirstOrDefault(candidate => candidate.Name == name)
                ?? throw new GesDynamicLinkException(new GesDiagnostic(
                    GesDiagnosticPhase.Link,
                    GesDiagnosticCodes.LinkMismatchedHandleType,
                    $"The schemas declare no handle type @{name}, which the Program was compiled with.",
                    name,
                    ProgramName: program.ModuleName));
            builder ??= GameEventScriptHost.CreateBuilder();
            builder.WithHandleResolver(type, UnusedResolver.Instance);
        }
        builder?.Build().Load(program);
    }

    private sealed class UnusedResolver : IGesHandleResolver
    {
        internal static readonly UnusedResolver Instance = new();

        public void Resolve(GesHandleResolveCall call) => throw new InvalidOperationException("The import check reads no state.");
    }
}
