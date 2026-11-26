// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Tool;

// The handle catalog of `--handles`: every handle type that the given schema sources declare, read once as one schema
// set. The CLI registers no resolver, so only programs that do not read handle state can load.
internal static class ToolHandles
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal const string Option = "--handles";

    internal static IReadOnlyList<GesHandleType> Load(IReadOnlyList<string> arguments, ref string? activePath)
    {
        if (arguments.Count == 0) return [];
        var paths = CompileSources.Expand(arguments);
        var sources = new GesSchemaSource[paths.Count];
        for (var index = 0; index < sources.Length; index++)
        {
            activePath = paths[index];
            sources[index] = new GesSchemaSource(paths[index], StrictUtf8.GetString(File.ReadAllBytes(paths[index])));
        }
        return [.. GesSchemaSet.Parse(sources).HandleTypes];
    }

    // Reported as `ges schema check` reports it.
    internal static void Report(GesSchemaException exception)
    {
        var place = exception.Line > 0 ? $"{exception.SourceName}({exception.Line},{exception.Column})" : "schema";
        Console.Error.WriteLine($"{place}: error {exception.Code}: {exception.Message}");
    }

    internal static GesProgramBuilder Builder(IReadOnlyList<GesHandleType> handleTypes)
    {
        var builder = new GesProgramBuilder();
        if (handleTypes.Count > 0) builder.WithHandleTypes(handleTypes);
        return builder;
    }
}
