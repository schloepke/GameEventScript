// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tests.Conformance;

[TestClass]
[DoNotParallelize]
public sealed class CollectionPipelineAllocationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Performance")]
    [TestCategory("Allocation")]
    [DataRow(8)]
    [DataRow(128)]
    public void ComponentIterationDoesNotAllocateOnePairPerRow(int size)
    {
        const string body = "asm { .register sum\n Add sum, a, b\n }";
        var nested = Measure($"for a in items {{ for b in items {{ {body} }} }}", size);
        var components = Measure($"for a, b in [:cartesian items, items] {{ {body} }}", size);
        var materialized = Measure($"let pairs be items * items\n for a, b in pairs {{ {body} }}", size);
        TestContext.WriteLine($"{size}x{size}: nested {nested.Bytes} B, {nested.Milliseconds:F4} ms; components {components.Bytes} B, {components.Milliseconds:F4} ms; materialized {materialized.Bytes} B, {materialized.Milliseconds:F4} ms.");
        // This bound allows iterator storage per source restart, but not a pair
        // container per Cartesian row. Timing is evidence, not a flaky CI gate.
        Assert.IsLessThan(4096L + size * 512L, components.Bytes);
        Assert.IsGreaterThan(components.Bytes, materialized.Bytes);
        if (size >= 128) Assert.IsGreaterThan(components.Bytes * 20, materialized.Bytes);
    }

    private static (long Bytes, double Milliseconds) Measure(string statements, int size)
    {
        var program = GameEventScriptBuilder.Create().AddScript($"module allocation\non Start(items) {{\n{statements}\n}}", "cartesian.ges")
            .Compile(new GameEventScriptCompileOptions { DebugInfo = GameEventScriptDebugInfoOptions.None });
        var host = GameEventScriptHost.CreateBuilder().WithRuntimeLimits(new GameEventScriptRuntimeLimits { MaxGeneratedCollectionItems = 100_000 }).Build();
        host.Load(program);
        host.Start();
        Assert.IsTrue(host.IsReady);
        var values = Enumerable.Range(1, size).Select(value => GesValue.GesInteger(value)).ToArray();
        var message = GameEventScriptMessage.Create("Start", [new("items", GesValue.GesList(values))]);
        void Run()
        {
            if (!host.Receive(message)) throw new InvalidOperationException("Benchmark input was rejected.");
            var result = host.RunToCompletion();
            if (result.State != GameEventScriptExecutionState.Completed || result.ExecutedOpcodes < size * size)
                throw new InvalidOperationException("Benchmark did not complete its Cartesian workload.");
        }
        for (var index = 0; index < 10; index++) Run();
        var allocated = ConformanceCSharpAllocationProvider.MeasureBytes(Run).AllocatedBytes;
        var samples = new double[5];
        for (var sample = 0; sample < samples.Length; sample++)
        {
            var start = Stopwatch.GetTimestamp();
            for (var iteration = 0; iteration < 20; iteration++) Run();
            samples[sample] = Stopwatch.GetElapsedTime(start).TotalMilliseconds / 20;
        }
        Array.Sort(samples);
        return (allocated, samples[2]);
    }
}
