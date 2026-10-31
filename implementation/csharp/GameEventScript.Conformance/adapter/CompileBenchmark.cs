// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using GameEventScript.Api;

// Compiles every source group of a request repeatedly; inputs are prepared outside the timed rounds.
internal static class CompileBenchmark
{
    private static readonly string[] CompiledKinds = ["bytecodeSnapshot", "compileError", "bytecode", "compileMetadata", "programBinary", "scriptApi", "loadError"];

    internal static object Run(JsonElement cases, int rounds)
    {
        var units = new List<Unit>();
        foreach (var test in cases.EnumerateArray())
        {
            var kind = test.GetProperty("kind").GetString()!;
            if (!CompiledKinds.Contains(kind)) continue;
            var debug = GesDebugInfoOptions.None;
            foreach (var item in test.GetProperty("compile").GetProperty("debugInfo").EnumerateArray())
                debug |= item.GetString() switch
                {
                    "debugSymbols" => GesDebugInfoOptions.DebugSymbols,
                    "sourceMap" => GesDebugInfoOptions.SourceMap,
                    "sourceArchive" => GesDebugInfoOptions.SourceArchive,
                    _ => throw new ArgumentException("Invalid debug option.")
                };
            var groups = new Dictionary<string, List<(string Text, string Name)>>(StringComparer.Ordinal);
            foreach (var source in test.GetProperty("sources").EnumerateArray())
            {
                var program = source.GetProperty("program").GetString()!;
                if (!groups.TryGetValue(program, out var group)) groups.Add(program, group = []);
                group.Add((source.GetProperty("text").GetString()!, source.GetProperty("name").GetString()!));
            }
            foreach (var group in groups.Values) units.Add(new Unit(group, kind is "scriptApi" or "loadError", new GesCompileOptions { DebugInfo = debug }));
        }

        var failures = CompileAll(units);
        var nanoseconds = new long[rounds];
        var allocatedBytes = new long[rounds];
        for (var round = 0; round < rounds; round++)
        {
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var clock = Stopwatch.StartNew();
            CompileAll(units);
            nanoseconds[round] = (long)(clock.ElapsedTicks * (1_000_000_000.0 / Stopwatch.Frequency));
            allocatedBytes[round] = GC.GetAllocatedBytesForCurrentThread() - allocated;
        }

        return new { units = units.Count, failures, nanoseconds, allocatedBytes };
    }

    private static int CompileAll(List<Unit> units)
    {
        var failures = 0;
        foreach (var unit in units)
        {
            var builder = new GesProgramBuilder();
            if (unit.ExternalTypes) builder.WithExternalTypeCatalog(GameEventScript.Tests.Conformance.GameEventScriptConformanceExternalTypes.Catalog);
            foreach (var (text, name) in unit.Sources) builder.AddScript(text, name);
            try
            {
                builder.Compile(unit.Options);
            }
            catch (GesCompileException)
            {
                failures++;
            }
        }

        return failures;
    }

    private sealed record Unit(List<(string Text, string Name)> Sources, bool ExternalTypes, GesCompileOptions Options);
}
