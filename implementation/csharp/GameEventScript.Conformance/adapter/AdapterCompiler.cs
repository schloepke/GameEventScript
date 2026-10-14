// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using GameEventScript.Api;

internal static class AdapterCompiler
{
    internal static GameEventScriptProgram Compile(JsonElement test, ulong programVersion = 0, string? programId = null)
    {
        var builder = new GesProgramBuilder();
        if (test.GetProperty("kind").GetString() is "scriptApi" or "loadError") builder.WithExternalTypeCatalog(GameEventScript.Tests.Conformance.GameEventScriptConformanceExternalTypes.Catalog);
        foreach (var source in test.GetProperty("sources").EnumerateArray())
            if (programId is null || source.GetProperty("program").GetString() == programId) builder.AddScript(source.GetProperty("text").GetString()!, source.GetProperty("name").GetString()!);
        var options = test.GetProperty("compile");
        var debug = GesDebugInfoOptions.None;
        foreach (var item in options.GetProperty("debugInfo").EnumerateArray())
            debug |= item.GetString() switch
            {
                "debugSymbols" => GesDebugInfoOptions.DebugSymbols,
                "sourceMap" => GesDebugInfoOptions.SourceMap,
                "sourceArchive" => GesDebugInfoOptions.SourceArchive,
                _ => throw new ArgumentException("Invalid debug option.")
            };
        var program = builder.Compile(new GesCompileOptions { DebugInfo = debug, ProgramVersion = programVersion });
        if (options.GetProperty("binaryRoundTrip").GetBoolean()) program = GameEventScriptProgramReader.Read(GameEventScriptProgramWriter.ToArray(program));
        return program;
    }
}
