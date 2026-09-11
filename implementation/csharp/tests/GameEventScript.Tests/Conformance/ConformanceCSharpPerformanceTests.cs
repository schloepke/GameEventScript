// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using GameEventScript.Api;
using GameEventScript.Compiler;
using GameEventScript.Conformance;
using GameEventScript.Runtime;
using GameEventScript.Tests.Native.Runtime;

namespace GameEventScript.Tests.Conformance;

[TestClass]
[DoNotParallelize]
public sealed class ConformanceCSharpPerformanceTests
{
    private static readonly Dictionary<string, ConformanceCaseResult> Results = new(StringComparer.Ordinal);

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> Cases()
        => SelectCases(allocation: false);

    public static IEnumerable<object[]> AllocationCases()
        => SelectCases(allocation: true);

    private static IEnumerable<object[]> SelectCases(bool allocation)
    {
        foreach (var document in ConformanceCSharpTestEnvironment.Documents)
            for (var index = 0; index < document.Cases.Count; index++)
                if (document.Cases[index].Kind == ConformanceTestKind.Performance && document.Cases[index].Categories.Contains("allocation") == allocation)
                    yield return [document, document.Cases[index].Id];
    }

    public static string DisplayName(MethodInfo method, object[] data)
        => ConformanceMSTestAdapter.DisplayName(method, data);

    [TestMethod]
    [TestCategory("Performance")]
    [TestCategory("Explicit")]
    [DynamicData(nameof(Cases), DynamicDataDisplayName = nameof(DisplayName))]
    public void MarkdownPerformanceCaseMeetsReference(ConformanceDocument document, string caseId)
        => RunMeasured(document, caseId, ConformanceCSharpTestEnvironment.Measured());

    [TestMethod]
    [TestCategory("Performance")]
    [TestCategory("Allocation")]
    [DynamicData(nameof(AllocationCases), DynamicDataDisplayName = nameof(DisplayName))]
    public void MarkdownAllocationCaseMeetsProfile(ConformanceDocument document, string caseId)
        => RunMeasured(document, caseId, ConformanceCSharpTestEnvironment.MeasuredAllocation());

    private void RunMeasured(ConformanceDocument document, string caseId, ConformanceRunnerEnvironment environment)
    {
        var result = ConformanceRunner.RunCase(document, caseId, environment, new ConformanceRunnerOptions
        {
            IncludeTechnicalDetails = true
        });

        Results[result.Id] = result;
        WriteArtifacts(document, environment);
        ConformanceMSTestAdapter.AssertPassed(result, TestContext);
    }

    private static void WriteArtifacts(ConformanceDocument document, ConformanceRunnerEnvironment environment)
    {
        var ordered = document.Cases
            .Where(testCase => Results.ContainsKey(testCase.FullId))
            .Select(testCase => Results[testCase.FullId])
            .ToArray();
        var report = ConformanceCSharpTestEnvironment.Report(environment, ordered);

        var root = Path.Combine(ConformanceCSharpTestEnvironment.GetConformanceArtifactsDirectory(), "received", "performance");
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, document.SuiteId + ".received.md"), ConformanceReceivedMarkdownWriter.ToArray(document, report));
        File.WriteAllBytes(Path.Combine(root, document.SuiteId + ".results.json"), ConformanceResultJsonWriter.ToArray(report));
        File.WriteAllText(Path.Combine(root, document.SuiteId + ".report.md"), ConformanceMarkdownReportWriter.ToText(report));
        if (environment.PerformanceProvider is ConformanceCSharpAllocationProvider)
            ConformanceCSharpAllocationProvider.WriteManifest(Path.Combine(root, document.SuiteId + ".measurement.json"), ordered);
    }
}

internal sealed class ConformanceCSharpPerformanceProvider : IConformancePerformanceProvider
{
    private const int MeasurementSamples = 3;

    internal static ConformanceCSharpPerformanceProvider Instance { get; } = new();

    public ConformancePerformanceMeasurement Measure(ConformanceCase testCase, string profileId)
    {
        if (!string.Equals(profileId, ConformanceCSharpTestEnvironment.PerformanceProfile, StringComparison.Ordinal))
            throw new InvalidOperationException("The C# performance provider received an unsupported profile.");
        if (testCase.Categories.Contains("allocation"))
            return ConformanceCSharpAllocationProvider.Instance.Measure(testCase, ConformanceCSharpAllocationProvider.ProfileId);
        if (testCase.Performance is null || testCase.Sources.Count == 0 || testCase.Sources.Select(source => source.ProgramId).Distinct(StringComparer.Ordinal).Count() != 1)
            throw new InvalidOperationException("C# performance cases require one non-empty program.");
        if (testCase.NativeHandlers.Count != 0)
            throw new InvalidOperationException("C# performance cases do not currently support declarative native handlers.");

        var options = new GameEventScriptCompileOptions { DebugInfo = DebugInfo(testCase.Compile.DebugInfo) };
        WarmCompile(testCase, options, testCase.Performance.CompileWarmupIterations);

        var ast = MeasureBest(() => CreateBuilder(testCase).BuildModule(options));
        var binary = MeasureBest(() => GesCompiler.Compile(ast.Value, options));
        var program = testCase.Compile.BinaryRoundTrip
            ? GameEventScriptProgramReader.Read(GameEventScriptProgramWriter.ToArray(binary.Value))
            : binary.Value;
        var load = MeasureBest(() =>
        {
            var host = CreateLoadHost(testCase);
            return host.Load(program);
        });
        var run = MeasureRunBest(testCase, program);

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ast-build.elapsed"] = FormatMilliseconds(ast.Elapsed.TotalMilliseconds),
            ["ast-build.allocated"] = FormatKiB(ast.AllocatedBytes),
            ["binary-build.elapsed"] = FormatMilliseconds(binary.Elapsed.TotalMilliseconds),
            ["binary-build.allocated"] = FormatKiB(binary.AllocatedBytes),
            ["program-load.elapsed"] = FormatMilliseconds(load.Elapsed.TotalMilliseconds),
            ["program-load.allocated"] = FormatKiB(load.AllocatedBytes),
            ["compile.elapsed"] = FormatMilliseconds(ast.Elapsed.TotalMilliseconds + binary.Elapsed.TotalMilliseconds + load.Elapsed.TotalMilliseconds),
            ["compile.allocated"] = FormatKiB(ast.AllocatedBytes + binary.AllocatedBytes + load.AllocatedBytes),
            ["run.elapsed"] = FormatMilliseconds(run.Elapsed.TotalMilliseconds),
            ["run.allocated"] = FormatKiB(run.AllocatedBytes),
            ["run.per-invoke-elapsed"] = FormatMilliseconds(run.Elapsed.TotalMilliseconds / Math.Max(1u, testCase.Performance.Iterations)),
            ["run.per-invoke-allocated"] = FormatKiB((double)run.AllocatedBytes / Math.Max(1u, testCase.Performance.Iterations))
        };

        var profile = testCase.Expectation.Performance!.Profiles.Single(value => string.Equals(value.Id, profileId, StringComparison.Ordinal));
        var metrics = new ConformanceMeasuredMetric[profile.Metrics.Count];
        for (var index = 0; index < metrics.Length; index++)
        {
            var expected = profile.Metrics[index];
            if (!values.TryGetValue(expected.Id, out var value))
                throw new InvalidOperationException("The C# performance provider received an unknown metric: " + expected.Id);
            metrics[index] = new ConformanceMeasuredMetric(expected.Id, value, expected.Unit);
        }
        return new ConformancePerformanceMeasurement(metrics);
    }

    internal static GameEventScriptBuilder CreateBuilder(ConformanceCase testCase)
    {
        var builder = GameEventScriptBuilder.Create()
            .WithExternalTypeCatalog(GameEventScriptConformanceExternalTypes.Catalog);
        for (var index = 0; index < testCase.Sources.Count; index++)
            builder.AddScript(testCase.Sources[index].Text, testCase.Sources[index].Name);
        return builder;
    }

    private static void WarmCompile(ConformanceCase testCase, GameEventScriptCompileOptions options, uint iterations)
    {
        for (uint index = 0; index < iterations; index++)
        {
            var syntax = CreateBuilder(testCase).BuildModule(options);
            var program = GesCompiler.Compile(syntax, options);
            _ = CreateHost(testCase).Load(program);
        }
    }

    private static PerformanceRun MeasureRunBest(ConformanceCase testCase, GameEventScriptProgram program)
    {
        var best = MeasureRun(testCase, program);
        for (var sample = 1; sample < MeasurementSamples; sample++)
        {
            var current = MeasureRun(testCase, program);
            if (current.Elapsed < best.Elapsed) best = current;
        }
        return best;
    }

    private static PerformanceRun MeasureRun(ConformanceCase testCase, GameEventScriptProgram program)
    {
        var host = CreateHost(testCase);
        host.Load(program);
        var inputs = new GameEventScriptMessage[testCase.Steps.Count];
        for (var index = 0; index < inputs.Length; index++)
            inputs[index] = ConformanceRuntimeValueCodec.DecodeMessage(testCase.Steps[index].Expectation.Input);

        Run(host, inputs, testCase.Performance!.WarmupIterations);
        ForceFullCollection();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        Run(host, inputs, testCase.Performance.Iterations);
        var elapsed = Stopwatch.GetElapsedTime(started);
        return new PerformanceRun(elapsed, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static void Run(GameEventScriptHost host, IReadOnlyList<GameEventScriptMessage> inputs, uint iterations)
    {
        for (uint iteration = 0; iteration < iterations; iteration++)
        {
            for (var inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
            {
                if (!host.Receive(inputs[inputIndex])) throw new InvalidOperationException("A performance input was not accepted.");
                var result = host.RunToCompletion();
                if (result.State is GameEventScriptExecutionState.Paused or GameEventScriptExecutionState.RuntimeLimitReached or GameEventScriptExecutionState.RuntimeError)
                    throw new InvalidOperationException("A performance workload did not complete successfully.");
            }
        }
    }

    private static GameEventScriptHost CreateHost(ConformanceCase testCase)
    {
        var outbound = 0;
        var builder = GameEventScriptHost.CreateBuilder()
            .WithRegistry(ConformanceTestExtensionRegistry.Instance)
            .WithExternalTypeRegistry(GameEventScriptConformanceExternalTypes.Registry)
            .WithRuntimeLimits(CreateRuntimeLimits(testCase.RuntimeLimits))
            .WithPublishSink(new TestPublishSink(_ => outbound++));
        if (testCase.Performance!.ObserveRuntime) builder.WithRuntimeObserver(new ConformanceCSharpAllocationProvider.CountingObserver());
        ConfigureRandom(builder, testCase.Random);
        return builder.Build();
    }

    private static GameEventScriptHost CreateLoadHost(ConformanceCase testCase)
        => GameEventScriptHost.CreateBuilder()
            .WithRegistry(ConformanceTestExtensionRegistry.Instance)
            .WithExternalTypeRegistry(GameEventScriptConformanceExternalTypes.Registry)
            .WithRuntimeLimits(CreateRuntimeLimits(testCase.RuntimeLimits))
            .Build();

    internal static void ConfigureRandom(GameEventScriptHostBuilder builder, ConformanceRandomConfiguration? configuration)
    {
        if (configuration is { Entropy.Count: > 0 })
        {
            builder.WithRandomEntropy(configuration.Entropy.ToArray());
            return;
        }
        if (configuration?.Seed is { } seed)
        {
            builder.WithRandomSeed(seed);
            return;
        }
        if (configuration is { Sequence.Count: > 0 })
        {
            var values = new double[configuration.Sequence.Count];
            for (var index = 0; index < values.Length; index++) values[index] = ConformanceRuntimeValueCodec.ParseBinary64(configuration.Sequence[index]);
            builder.WithRandomSequence(values, 0L);
            return;
        }
        builder.WithRandomSeed(0L);
    }

    internal static GameEventScriptRuntimeLimits CreateRuntimeLimits(ConformanceRuntimeLimits limits)
    {
        var defaults = GameEventScriptRuntimeLimits.Default;
        if (limits.Values.Count == 0) return defaults;
        int Read(string name, int fallback) => limits.Values.TryGetValue(name, out var value) ? checked((int)value) : fallback;
        return new GameEventScriptRuntimeLimits
        {
            MaxProcessedEventsPerRun = Read("maxProcessedEventsPerRun", defaults.MaxProcessedEventsPerRun),
            MaxQueuedMessagesPerRun = Read("maxQueuedMessagesPerRun", defaults.MaxQueuedMessagesPerRun),
            MaxExecutionSteps = Read("maxExecutionSteps", defaults.MaxExecutionSteps),
            MaxRegisterValues = Read("maxRegisterValues", defaults.MaxRegisterValues),
            MaxLoopIterations = Read("maxLoopIterations", defaults.MaxLoopIterations),
            MaxCallDepth = Read("maxCallDepth", defaults.MaxCallDepth),
            MaxRandomScopeDepth = Read("maxRandomScopeDepth", defaults.MaxRandomScopeDepth),
            MaxRangeItems = Read("maxRangeItems", defaults.MaxRangeItems),
            MaxGeneratedCollectionItems = Read("maxGeneratedCollectionItems", defaults.MaxGeneratedCollectionItems),
            MaxDiceCount = Read("maxDiceCount", defaults.MaxDiceCount),
            MaxDiceSides = Read("maxDiceSides", defaults.MaxDiceSides)
        };
    }

    internal static GameEventScriptDebugInfoOptions DebugInfo(IReadOnlyList<string> values)
    {
        var result = GameEventScriptDebugInfoOptions.None;
        for (var index = 0; index < values.Count; index++)
            result |= values[index] switch
            {
                "debugSymbols" => GameEventScriptDebugInfoOptions.DebugSymbols,
                "sourceMap" => GameEventScriptDebugInfoOptions.SourceMap,
                "sourceArchive" => GameEventScriptDebugInfoOptions.SourceArchive,
                _ => GameEventScriptDebugInfoOptions.None
            };
        return result;
    }

    private static Measured<T> MeasureBest<T>(Func<T> action)
    {
        var best = Measure(action);
        for (var sample = 1; sample < MeasurementSamples; sample++)
        {
            var current = Measure(action);
            if (current.Elapsed < best.Elapsed) best = current;
        }
        return best;
    }

    private static Measured<T> Measure<T>(Func<T> action)
    {
        ForceFullCollection();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        var value = action();
        var elapsed = Stopwatch.GetElapsedTime(started);
        return new Measured<T>(value, elapsed, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static void ForceFullCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static string FormatMilliseconds(double value)
        => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string FormatKiB(long bytes)
        => FormatKiB((double)bytes);

    private static string FormatKiB(double bytes)
        => (bytes / 1024d).ToString("0.###", CultureInfo.InvariantCulture);

    private sealed record Measured<T>(T Value, TimeSpan Elapsed, long AllocatedBytes);
    private sealed record PerformanceRun(TimeSpan Elapsed, long AllocatedBytes);
}
