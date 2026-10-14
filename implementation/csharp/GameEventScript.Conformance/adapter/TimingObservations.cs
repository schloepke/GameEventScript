// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using GameEventScript.Api;
using GameEventScript.Compiler;

internal static class TimingObservations
{
    internal const string Profile = "csharp-dotnet-release-macos-arm64";
    internal static void Validate(JsonElement test)
    {
        var input = test.GetProperty("measurement");
        if (input.GetProperty("profile").GetString() != Profile) throw new ArgumentException("Unsupported timing profile.");
        var seen = new HashSet<string>();
        foreach (var metric in input.GetProperty("metrics").EnumerateArray())
        {
            RuntimeValues.Fields(metric, "id", "unit");
            var id = metric.GetProperty("id").GetString()!;
            var parts = id.Split('.');
            if (parts.Length != 2
            || parts[0] is not ("ast-build" or "binary-build" or "program-load" or "compile" or "run")
            || parts[1] is not ("elapsed" or "allocated" or "per-invoke-elapsed" or "per-invoke-allocated")
            || (parts[1].StartsWith("per-invoke", StringComparison.Ordinal) && parts[0] != "run")
            || !seen.Add(id))
                throw new ArgumentException("Unknown or duplicate timing metric.");
            if (metric.GetProperty("unit").GetString() != (id.EndsWith("elapsed", StringComparison.Ordinal) ? "ms" : "KiB")) throw new ArgumentException("Invalid timing unit.");
        }
        if (seen.Count == 0) throw new ArgumentException("Metrics required.");
        AllocationObservations.Validate(AllocationInput(test));
        foreach (var step in test.GetProperty("runtime").GetProperty("steps").EnumerateArray())
            if (step.GetProperty("pump").GetString() != "completion") throw new ArgumentException("Timing profile requires completion pumping.");
    }

    private static JsonElement AllocationInput(JsonElement test)
    {
        var node = JsonNode.Parse(test.GetRawText())!;
        node["measurement"]!["profile"] = AllocationObservations.Profile;
        node["measurement"]!["metrics"] = JsonNode.Parse("[{\"id\":\"run.allocated\",\"unit\":\"B\"}]");
        return JsonSerializer.SerializeToElement(node);
    }

    internal static object Run(JsonElement test)
    {
        if (!OperatingSystem.IsMacOS() || RuntimeInformation.ProcessArchitecture != Architecture.Arm64) throw new InvalidOperationException("Timing profile requires macOS ARM64.");
        // Qualified run samples are measured independently of compilation and JSON transport.
        var run = JsonSerializer.SerializeToElement(AllocationObservations.Run(AllocationInput(test)));
        var sources = test.GetProperty("sources").EnumerateArray().Select(s => (Name: s.GetProperty("name").GetString()!, Text: s.GetProperty("text").GetString()!)).ToArray();
        var debug = GesDebugInfoOptions.None;
        foreach (var flag in test.GetProperty("compile").GetProperty("debugInfo").EnumerateArray())
            debug |= flag.GetString() switch
            {
                "debugSymbols" => GesDebugInfoOptions.DebugSymbols,
                "sourceMap" => GesDebugInfoOptions.SourceMap,
                "sourceArchive" => GesDebugInfoOptions.SourceArchive,
                _ => throw new ArgumentException("Invalid debug flag.")
            };
        var options = new GesCompileOptions { DebugInfo = debug };
        var limits = RuntimeObservations.CreateLimits(test.GetProperty("runtime").GetProperty("limits"));
        GesProgramBuilder Builder()
        {
            var builder = new GesProgramBuilder().WithExternalTypeCatalog(GameEventScript.Tests.Conformance.GameEventScriptConformanceExternalTypes.Catalog);
            foreach (var source in sources) builder.AddScript(source.Text, source.Name);
            return builder;
        }
        GesProgramInstance Load(GameEventScriptProgram program)
        {
            var host = GameEventScriptHost.CreateBuilder().WithRegistry(new RuntimeFixtures()).WithExternalTypeRegistry(GameEventScript.Tests.Conformance.GameEventScriptConformanceExternalTypes.Registry).WithRuntimeLimits(limits).Build();
            host.Start();
            return host.Load(program);
        }
        void Warmup()
        {
            for (uint i = 0; i < test.GetProperty("measurement").GetProperty("compileWarmupIterations").GetUInt32(); i++) _ = Load(Builder().Compile(options));
        }
        var ast = Measure(() => Builder().BuildModule(options), Warmup);
        var binary = Measure(() => GesCompiler.Compile(ast.Value, options), Warmup);
        var program = binary.Value;
        if (test.GetProperty("compile").GetProperty("binaryRoundTrip").GetBoolean()) program = GameEventScriptProgramReader.Read(GameEventScriptProgramWriter.ToArray(program));
        var load = Measure(() => Load(program), Warmup);
        var samples = run.GetProperty("evidence").GetProperty("samples").EnumerateArray().ToArray();
        var elapsed = samples.Min(s => s.GetProperty("ElapsedMilliseconds").GetDouble());
        var allocated = samples.Max(s => s.GetProperty("Bytes").GetInt64());
        var iterations = test.GetProperty("measurement").GetProperty("iterations").GetUInt32();
        var values = new Dictionary<string, double>
        {
            ["ast-build.elapsed"] = ast.Samples.Min(s => s.ElapsedMilliseconds),
            ["ast-build.allocated"] = ast.Samples.Max(s => s.Bytes) / 1024d,
            ["binary-build.elapsed"] = binary.Samples.Min(s => s.ElapsedMilliseconds),
            ["binary-build.allocated"] = binary.Samples.Max(s => s.Bytes) / 1024d,
            ["program-load.elapsed"] = load.Samples.Min(s => s.ElapsedMilliseconds),
            ["program-load.allocated"] = load.Samples.Max(s => s.Bytes) / 1024d,
            ["run.elapsed"] = elapsed,
            ["run.allocated"] = allocated / 1024d,
            ["run.per-invoke-elapsed"] = elapsed / iterations,
            ["run.per-invoke-allocated"] = allocated / (1024d * iterations)
        };
        values["compile.elapsed"] = values["ast-build.elapsed"] + values["binary-build.elapsed"] + values["program-load.elapsed"];
        values["compile.allocated"] = values["ast-build.allocated"] + values["binary-build.allocated"] + values["program-load.allocated"];
        return new
        {
            status = "ok",
            measurement = new
            {
                profile = Profile,
                metrics = test.GetProperty("measurement").GetProperty("metrics").EnumerateArray().Select(m => new
                {
                    id = m.GetProperty("id").GetString(),
                    unit = m.GetProperty("unit").GetString(),
                    value = values[m.GetProperty("id").GetString()!].ToString("R", CultureInfo.InvariantCulture)
                }).ToArray()
            },
            evidence = new
            {
                run,
                timer = "Stopwatch.GetTimestamp",
                timerFrequency = Stopwatch.Frequency,
                scope = "AST includes builder/source registration; binary uses prepared AST; load includes host creation/start and load; run includes Receive and completion.",
                excluded = "JSON transport, GC, declared warmup, sample objects and assertions; binary roundtrip is outside phase timing.",
                ast = ast.Samples,
                binary = binary.Samples,
                load = load.Samples,
                aggregation = "Minimum elapsed and maximum allocated independently; compile is the sum of its three phases."
            }
        };
    }

    private sealed record Sample(double ElapsedMilliseconds, long Bytes);
    private sealed record Measurement<T>(T Value, Sample[] Samples);

    private static Measurement<T> Measure<T>(Func<T> action, Action warmup)
    {
        var samples = new Sample[3];
        T value = default!;
        for (var i = 0; i < samples.Length; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            warmup();
            var before = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            value = action();
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            samples[i] = new Sample(elapsed, allocated);
        }
        return new Measurement<T>(value, samples);
    }
}
