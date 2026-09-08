// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.Conformance;
using StepH.GameEventScript.Runtime;

namespace StepH_GameEventScript_Tests.Conformance;

internal sealed class ConformanceCSharpAllocationProvider : IConformancePerformanceProvider
{
    internal const string ProfileId = "csharp-dotnet-release-managed";
    private const int SampleCount = 3;
    private static readonly Dictionary<string, MeasurementEvidence> Evidence = new(StringComparer.Ordinal);

    internal static ConformanceCSharpAllocationProvider Instance { get; } = new();

    public ConformancePerformanceMeasurement Measure(ConformanceCase testCase, string profileId)
    {
        Evidence.Remove(testCase.FullId);
        if (typeof(ConformanceCSharpAllocationProvider).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration != "Release")
            throw new InvalidOperationException("Allocation qualification requires a Release build.");
        if (profileId != ProfileId) throw new InvalidOperationException("Unsupported allocation profile.");
        if (testCase.Performance is not { WarmupIterations: > 0 } workload || testCase.HostCount != 1 || testCase.NativeHandlers.Count != 0)
            throw new InvalidOperationException("Allocation workloads require warmup, one host and script handlers.");
        if (testCase.Sources.Count == 0 || testCase.Sources.Select(source => source.ProgramId).Distinct(StringComparer.Ordinal).Count() != 1)
            throw new InvalidOperationException("Allocation workloads require one program.");
        if (testCase.Steps.Any(step => step.Actions.Count != 0 || step.Pump is not (ConformancePumpMode.Completion or ConformancePumpMode.Frames)))
            throw new InvalidOperationException("Allocation workloads support completion or frames without lifecycle actions.");
        var profile = testCase.Expectation.Performance!.Profiles.Single(profile => profile.Id == profileId);
        if (profile.Metrics.Count != 1 || profile.Metrics[0].Id != "run.allocated" || profile.Metrics[0].Unit != "B")
            throw new InvalidOperationException("The managed allocation profile requires the exact run.allocated metric in bytes.");

        var options = new GameEventScriptCompileOptions { DebugInfo = ConformanceCSharpPerformanceProvider.DebugInfo(testCase.Compile.DebugInfo) };
        GameEventScriptProgram Compile()
        {
            var syntax = ConformanceCSharpPerformanceProvider.CreateBuilder(testCase).BuildModule(options);
            var compiled = GesCompiler.Compile(syntax, options);
            return testCase.Compile.BinaryRoundTrip ? GameEventScriptProgramReader.Read(GameEventScriptProgramWriter.ToArray(compiled)) : compiled;
        }
        for (uint warmup = 0; warmup < workload.CompileWarmupIterations; warmup++) _ = Compile();
        var program = Compile();
        var inputs = testCase.Steps.Select(step => ConformanceRuntimeValueCodec.DecodeMessage(step.Expectation.Input)).ToArray();
        var samples = new SampleEvidence[SampleCount];
        for (var sample = 0; sample < samples.Length; sample++) samples[sample] = MeasureSample(testCase, program, inputs);
        Evidence[testCase.FullId] = new MeasurementEvidence(workload.Iterations, workload.WarmupIterations, workload.ObserveRuntime, samples);
        var maximum = samples.Max(sample => sample.AllocatedBytes);
        return new ConformancePerformanceMeasurement([new ConformanceMeasuredMetric("run.allocated", maximum.ToString(CultureInfo.InvariantCulture), "B")]);
    }

    private static SampleEvidence MeasureSample(ConformanceCase testCase, GameEventScriptProgram program, GameEventScriptMessage[] inputs)
    {
        var workload = testCase.Performance!;
        var observer = workload.ObserveRuntime ? new CountingObserver() : null;
        var builder = GameEventScriptHost.CreateBuilder()
            .WithRegistry(ConformanceTestExtensionRegistry.Instance)
            .WithExternalTypeRegistry(GameEventScriptConformanceExternalTypes.Registry)
            .WithRuntimeLimits(ConformanceCSharpPerformanceProvider.CreateRuntimeLimits(testCase.RuntimeLimits));
        if (observer is not null) builder.WithRuntimeObserver(observer);
        ConformanceCSharpPerformanceProvider.ConfigureRandom(builder, testCase.Random);
        var host = builder.Build();
        host.Load(program);
        if (host.RunToCompletion().State != GameEventScriptExecutionState.Completed)
            throw new InvalidOperationException("Allocation workload initialization failed.");

        _ = Run(host, inputs, testCase.Steps, workload.WarmupIterations);
        observer?.Reset();
        var expectedWork = Run(host, inputs, testCase.Steps, 1);
        var expectedCallbacks = observer?.Counts ?? default;
        if (expectedWork.Opcodes == 0 || expectedWork.ProcessedMessages < inputs.Length)
            throw new InvalidOperationException("Allocation workload did not execute script instructions and consume its inputs.");
        if (observer is not null && (expectedCallbacks.Started == 0 || expectedCallbacks.Completed != expectedCallbacks.Started || expectedCallbacks.Limits != 0 || expectedCallbacks.Errors != 0))
            throw new InvalidOperationException("The configured counting observer did not observe successful handler dispatch.");
        observer?.Reset();

        WorkCounters measuredWork = default;
        Action action = () => measuredWork = Run(host, inputs, testCase.Steps, workload.Iterations);
        var measured = MeasureBytes(action);
        var callbacks = observer?.Counts ?? default;
        if (measuredWork != expectedWork.Scale(workload.Iterations) || callbacks != expectedCallbacks.Scale(workload.Iterations))
            throw new InvalidOperationException("Measured work or observer callbacks differ from the prepared workload.");
        return new SampleEvidence(measured.AllocatedBytes, measuredWork, callbacks);
    }

    private static WorkCounters Run(GameEventScriptHost host, GameEventScriptMessage[] inputs, IReadOnlyList<ConformanceStep> steps, uint iterations)
    {
        long opcodes = 0, processed = 0, emitted = 0, published = 0, pauses = 0;
        for (uint iteration = 0; iteration < iterations; iteration++)
            for (var index = 0; index < inputs.Length; index++)
            {
                if (!host.Receive(inputs[index])) throw new InvalidOperationException("Allocation input was not accepted.");
                var step = steps[index];
                var paused = false;
                GameEventScriptExecutionResult result;
                do
                {
                    result = step.Pump == ConformancePumpMode.Completion ? host.RunToCompletion() : host.ExecuteFrame(checked((int)step.Budget!.Value));
                    if (result.State is GameEventScriptExecutionState.RuntimeError or GameEventScriptExecutionState.RuntimeLimitReached)
                        throw new InvalidOperationException("Allocation workload reported a runtime failure.");
                    opcodes += result.ExecutedOpcodes;
                    processed += result.ProcessedMessages;
                    emitted += result.EmittedMessages;
                    published += result.PublishedMessages;
                    if (result.State == GameEventScriptExecutionState.Paused)
                    {
                        paused = true;
                        pauses++;
                        if (result.ExecutedOpcodes == 0) throw new InvalidOperationException("Allocation workload made no frame progress.");
                    }
                } while (result.State == GameEventScriptExecutionState.Paused);
                if (!host.IsIdle || step.Expectation.Paused is { } expectedPaused && paused != expectedPaused)
                    throw new InvalidOperationException("Allocation workload did not follow its declared pump behavior.");
            }
        return new WorkCounters(opcodes, processed, emitted, published, pauses);
    }

    internal static AllocationSample MeasureBytes(Action action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return new AllocationSample(GC.GetAllocatedBytesForCurrentThread() - before);
    }

    internal static void WriteManifest(string path, IReadOnlyList<ConformanceCaseResult> results)
    {
        var assembly = typeof(ConformanceCSharpAllocationProvider).Assembly;
        var manifest = new
        {
            profile = ProfileId,
            runtime = RuntimeInformation.FrameworkDescription,
            runtimeVersion = Environment.Version.ToString(),
            buildSdkVersion = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "BuildSdkVersion").Value,
            buildConfiguration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration,
            os = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            serverGc = GCSettings.IsServerGC,
            gcLatencyMode = GCSettings.LatencyMode.ToString(),
            instrumentation = "GC.GetAllocatedBytesForCurrentThread",
            scope = "Synchronous Receive and completion/frames of reused inputs; includes counting observer callbacks when enabled.",
            excluded = "Compilation, linking, initialization, input/observer creation, declared warmup, one preflight iteration, collection and assertions.",
            coverageGaps = "Managed heap on the executing thread only; native allocations and other threads are not measured. This is not a complete all-heap qualification.",
            aggregation = "Maximum allocated bytes of three fresh-host samples. No tolerance above zero.",
            cases = results.Select(result => new { id = result.Id, status = result.Status.ToString(), measured = Evidence.GetValueOrDefault(result.Id) })
        };
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    internal readonly record struct AllocationSample(long AllocatedBytes);
    private sealed record MeasurementEvidence(uint Iterations, uint WarmupIterations, bool ObserveRuntime, SampleEvidence[] Samples);
    private sealed record SampleEvidence(long AllocatedBytes, WorkCounters Work, CallbackCounters Callbacks);
    private readonly record struct WorkCounters(long Opcodes, long ProcessedMessages, long EmittedMessages, long PublishedMessages, long Pauses)
    {
        internal WorkCounters Scale(uint count) => new(Opcodes * count, ProcessedMessages * count, EmittedMessages * count, PublishedMessages * count, Pauses * count);
    }

    internal readonly record struct CallbackCounters(long Started, long Completed, long Emitted, long Published, long Limits, long Errors)
    {
        internal CallbackCounters Scale(uint count) => new(Started * count, Completed * count, Emitted * count, Published * count, Limits * count, Errors * count);
    }

    internal sealed class CountingObserver : IGameEventScriptRuntimeObserver
    {
        internal CallbackCounters Counts { get; private set; }

        internal void Reset() => Counts = default;
        public void DispatchStarted(GameEventScriptMessage message, string dispatchSignatureId) => Counts = Counts with { Started = Counts.Started + 1 };
        public void DispatchCompleted(GameEventScriptMessage message, string dispatchSignatureId) => Counts = Counts with { Completed = Counts.Completed + 1 };
        public void MessageEmitted(GameEventScriptMessage message, bool accepted) => Counts = Counts with { Emitted = Counts.Emitted + 1 };
        public void MessagePublished(GameEventScriptMessage message, GameEventScriptPublishResult result) => Counts = Counts with { Published = Counts.Published + 1 };
        public void RuntimeLimitReached(string limitName, string detail, int limit) => Counts = Counts with { Limits = Counts.Limits + 1 };
        public void RuntimeError(GameEventScriptDiagnostic diagnostic) => Counts = Counts with { Errors = Counts.Errors + 1 };
    }
}
