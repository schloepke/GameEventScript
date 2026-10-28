// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using GameEventScript.Api;

internal static class AllocationObservations
{
    internal const string Profile = "csharp-dotnet-release-managed";
    private readonly record struct Step(bool Frames, int Budget);

    internal static void Validate(JsonElement test)
    {
        var input = test.GetProperty("measurement");
        RuntimeValues.Fields(input, "profile", "iterations", "warmupIterations", "compileWarmupIterations", "observeRuntime", "metrics");
        if (input.GetProperty("profile").GetString() != Profile) throw new ArgumentException("Unsupported measurement profile.");
        if (input.GetProperty("iterations").GetUInt32() == 0 || input.GetProperty("warmupIterations").GetUInt32() == 0) throw new ArgumentException("Positive iteration and warmup counts required.");
        _ = input.GetProperty("compileWarmupIterations").GetUInt32();
        _ = input.GetProperty("observeRuntime").GetBoolean();
        var metrics = input.GetProperty("metrics");
        if (metrics.GetArrayLength() != 1) throw new ArgumentException("Only run.allocated is supported.");
        RuntimeValues.Fields(metrics[0], "id", "unit");
        if (metrics[0].GetProperty("id").GetString() != "run.allocated" || metrics[0].GetProperty("unit").GetString() != "B") throw new ArgumentException("Byte allocation metric required.");
        var runtime = test.GetProperty("runtime");
        if (runtime.GetProperty("hostCount").GetInt32() != 1
            || runtime.GetProperty("nativeHandlers").GetArrayLength() != 0
            || runtime.GetProperty("deferredPrograms").GetArrayLength() != 0
            || runtime.GetProperty("stepActions").EnumerateObject().Any())
            throw new ArgumentException("Allocation requires one host without lifecycle actions/native handlers.");
        if (test.GetProperty("sources").GetArrayLength() == 0 || test.GetProperty("sources").EnumerateArray().Select(s => s.GetProperty("program").GetString()).Distinct().Count() != 1) throw new ArgumentException("One source Program required.");
        foreach (var step in runtime.GetProperty("steps").EnumerateArray())
            if (step.GetProperty("pump").GetString() is not ("completion" or "frames")) throw new ArgumentException("Unsupported measured pump.");
    }

    internal static object Run(JsonElement test)
    {
        var assembly = typeof(AllocationObservations).Assembly;
        var configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;
        if (configuration != "Release") throw new InvalidOperationException("Measurement requires Release.");
        var controls = Controls();
        var input = test.GetProperty("measurement");
        var runtime = test.GetProperty("runtime");
        for (uint i = 0; i < input.GetProperty("compileWarmupIterations").GetUInt32(); i++) _ = AdapterCompiler.Compile(test);
        var program = AdapterCompiler.Compile(test);
        var messages = runtime.GetProperty("steps").EnumerateArray().Select(s => RuntimeValues.DecodeMessage(s.GetProperty("input"))).ToArray();
        var steps = runtime.GetProperty("steps").EnumerateArray().Select(s => new Step(s.GetProperty("pump").GetString() == "frames", s.GetProperty("budget").ValueKind == JsonValueKind.Null ? 0 : s.GetProperty("budget").GetInt32())).ToArray();
        var samples = new Sample[3];
        var iterations = input.GetProperty("iterations").GetUInt32();
        var warmup = input.GetProperty("warmupIterations").GetUInt32();
        var observe = input.GetProperty("observeRuntime").GetBoolean();
        for (var index = 0; index < samples.Length; index++)
        {
            var observer = observe ? new CountingObserver() : null;
            var builder = GameEventScriptHost.CreateBuilder().WithRegistry(new RuntimeFixtures())
                .WithExternalTypeRegistry(GameEventScript.Tests.Conformance.GameEventScriptConformanceExternalTypes.Registry)
                .WithRuntimeLimits(RuntimeObservations.CreateLimits(runtime.GetProperty("limits")));
            if (observer is not null) builder.WithRuntimeObserver(observer);
            RuntimeRandom.Configure(builder, runtime.GetProperty("random"));
            var host = builder.Build();
            host.Load(program);
            host.Start();
            if (!host.IsReady || host.RunToCompletion().State != GesExecutionState.Completed) throw new InvalidOperationException("Initialization failed.");
            _ = Run(host, messages, steps, warmup);
            observer?.Reset();
            var expectedWork = Run(host, messages, steps, 1);
            var expectedCallbacks = observer?.Counts ?? default;
            if (expectedWork.Opcodes == 0
            || expectedWork.ProcessedMessages < messages.Length
            || (observe && (expectedCallbacks.Started == 0
            || expectedCallbacks.Started != expectedCallbacks.Completed
            || expectedCallbacks.Errors != 0
            || expectedCallbacks.Limits != 0)))
                throw new InvalidOperationException("Preflight did not complete observed work.");
            observer?.Reset();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var before = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            var work = Run(host, messages, steps, iterations);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            var callbacks = observer?.Counts ?? default;
            if (work != expectedWork.Scale(iterations) || callbacks != expectedCallbacks.Scale(iterations)) throw new InvalidOperationException("Measured work differs from preflight.");
            samples[index] = new Sample(bytes, work, callbacks, elapsed);
        }
        return new
        {
            status = "ok",
            measurement = new { profile = Profile, metrics = new[] { new { id = "run.allocated", unit = "B", value = samples.Max(s => s.Bytes).ToString(CultureInfo.InvariantCulture) } } },
            evidence = new
            {
                runtime = RuntimeInformation.FrameworkDescription,
                runtimeVersion = Environment.Version.ToString(),
                buildSdkVersion = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "BuildSdkVersion").Value,
                configuration,
                os = RuntimeInformation.OSDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                instrumentation = "GC.GetAllocatedBytesForCurrentThread",
                scope = "Synchronous Receive and completion/frames; counting observer when enabled.",
                excluded = "Compilation, input/observer creation, linking, initialization, warmup, preflight, GC and assertions.",
                coverageGaps = "Managed allocations on the executing thread only; excludes native heaps and other threads.",
                aggregation = "Maximum of three fresh-host samples",
                iterations,
                warmup,
                observe,
                controls,
                samples
            }
        };
    }

    private sealed record Sample(long Bytes, WorkCounters Work, CallbackCounters Callbacks, double ElapsedMilliseconds);

    private static object Controls()
    {
        var retained = new byte[1000][];
        var existing = new byte[32];
        for (var i = 0; i < retained.Length; i++) retained[i] = existing;
        _ = GC.GetAllocatedBytesForCurrentThread();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < retained.Length; i++) retained[i] = existing;
        var reuse = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < retained.Length; i++) retained[i] = new byte[32];
        var escaping = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(retained);
        if (reuse != 0 || escaping < 32000) throw new InvalidOperationException("Allocation counter controls failed.");
        return new { reuse, escaping, minimumEscapingBytes = 32000 };
    }

    private static WorkCounters Run(GameEventScriptHost host, GameEventScriptMessage[] inputs, Step[] steps, uint iterations)
    {
        long opcodes = 0, processed = 0, emitted = 0, published = 0, pauses = 0;
        for (uint iteration = 0; iteration < iterations; iteration++)
            for (var index = 0; index < inputs.Length; index++)
            {
                if (!host.Receive(inputs[index])) throw new InvalidOperationException("Allocation input was not accepted.");
                var step = steps[index];
                GesExecutionResult result;
                do
                {
                    result = !step.Frames ? host.RunToCompletion() : host.ExecuteFrame(step.Budget);
                    if (result.State is GesExecutionState.RuntimeError or GesExecutionState.RuntimeLimitReached)
                        throw new InvalidOperationException("Allocation workload reported a runtime failure.");
                    opcodes += result.ExecutedOpcodes;
                    processed += result.ProcessedMessages;
                    emitted += result.EmittedMessages;
                    published += result.PublishedMessages;
                    if (result.State == GesExecutionState.Paused)
                    {
                        pauses++;
                        if (result.ExecutedOpcodes == 0) throw new InvalidOperationException("Allocation workload made no frame progress.");
                    }
                } while (result.State == GesExecutionState.Paused);
                if (!host.IsIdle)
                    throw new InvalidOperationException("Allocation workload did not follow its declared pump behavior.");
            }
        return new WorkCounters(opcodes, processed, emitted, published, pauses);
    }

    private readonly record struct WorkCounters(long Opcodes, long ProcessedMessages, long EmittedMessages, long PublishedMessages, long Pauses)
    {
        internal WorkCounters Scale(uint count) => new(Opcodes * count, ProcessedMessages * count, EmittedMessages * count, PublishedMessages * count, Pauses * count);
    }

    internal readonly record struct CallbackCounters(long Started, long Completed, long Emitted, long Published, long Limits, long Errors)
    {
        internal CallbackCounters Scale(uint count) => new(Started * count, Completed * count, Emitted * count, Published * count, Limits * count, Errors * count);
    }

    internal sealed class CountingObserver : IGesRuntimeObserver
    {
        internal CallbackCounters Counts { get; private set; }

        internal void Reset() => Counts = default;
        /// <summary>Counts DispatchStarted callbacks without allocating.</summary>
        public void DispatchStarted(GameEventScriptMessage message, long id, string dispatchSignatureId) => Counts = Counts with { Started = Counts.Started + 1 };
        /// <summary>Counts DispatchCompleted callbacks without allocating.</summary>
        public void DispatchCompleted(GameEventScriptMessage message, long id, string dispatchSignatureId) => Counts = Counts with { Completed = Counts.Completed + 1 };
        /// <summary>Counts MessageEmitted callbacks without allocating.</summary>
        public void MessageEmitted(GameEventScriptMessage message, long id, bool accepted) => Counts = Counts with { Emitted = Counts.Emitted + 1 };
        /// <summary>Counts MessagePublished callbacks without allocating.</summary>
        public void MessagePublished(GameEventScriptMessage message, long id, GesPublishResult result) => Counts = Counts with { Published = Counts.Published + 1 };
        /// <summary>Counts RuntimeLimitReached callbacks without allocating.</summary>
        public void RuntimeLimitReached(string limitName, string detail, int limit) => Counts = Counts with { Limits = Counts.Limits + 1 };
        /// <summary>Counts RuntimeError callbacks without allocating.</summary>
        public void RuntimeError(GesDiagnostic diagnostic) => Counts = Counts with { Errors = Counts.Errors + 1 };
    }
}
