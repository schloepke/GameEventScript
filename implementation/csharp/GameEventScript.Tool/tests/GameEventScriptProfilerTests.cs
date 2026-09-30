// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Tool;

namespace GameEventScript.Tests.Native.Tool;

/// <summary>Verifies native instrumentation timing, slice boundaries and CLI report delivery.</summary>
[TestClass]
public sealed class GameEventScriptProfilerTests
{
    /// <summary>Each slice excludes outside time; duplicate loaded instances have independent counters.</summary>
    [TestMethod]
    public void ProfilesInstancesAcrossSingleOpcodeSlices()
    {
        long now = 0;
        var profiler = new RunProfiler(() => now += 10, 1_000_000_000);
        var program = GameEventScriptBuilder.Create().AddScript("function f(_ x) be x + 1\non Tick { emit Result(f(10)) }", "sample.ges").Compile();
        var host = GameEventScriptHost.CreateBuilder().WithProfiler(profiler).Build();
        host.Load(program);
        host.Load(program);
        host.Start();
        Assert.AreEqual(0L, now);
        host.Receive(GameEventScriptMessage.Create("Tick"));
        long executed = 0;
        while (!host.IsIdle)
        {
            executed += host.ExecuteFrame(1).ExecutedOpcodes;
            now += 1_000_000;
        }
        Assert.HasCount(2, profiler.Programs);
        Assert.IsGreaterThan(0L, executed);
        Assert.AreEqual(executed, profiler.Programs.Sum(p => p.Counts.Sum()));
        foreach (var measurement in profiler.Programs)
        {
            Assert.IsGreaterThan(0L, measurement.Counts.Sum());
            for (var index = 0; index < measurement.Counts.Length; index++) Assert.AreEqual(measurement.Counts[index] * 10, measurement.Ticks[index]);
            measurement.FinishSlice();
        }
        CollectionAssert.AreEqual(profiler.Programs[0].Counts, profiler.Programs[1].Counts);
        StringAssert.Contains(profiler.Markdown(), "sample.ges:");
    }

    /// <summary>Counter and clock callbacks allocate nothing after per-Program storage is prepared.</summary>
    [TestMethod]
    [TestCategory("Allocation")]
    public void InstructionMeasurementsDoNotAllocate()
    {
        long now = 0;
        var profiler = new RunProfiler(() => ++now, 1_000_000_000);
        var program = GameEventScriptBuilder.Create().AddScript("on Tick { emit Result(10) }").Compile();
        var measurement = profiler.CreateProgramProfiler(program);
        for (var index = 0; index < 1000; index++) measurement.InstructionStarting(0);
        measurement.FinishSlice();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1000; index++) measurement.InstructionStarting(0);
        measurement.FinishSlice();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.AreEqual(0L, allocated);
        Assert.AreEqual(2000L, profiler.Programs[0].Counts[0]);
    }

    /// <summary>Writes Markdown on successful and limit-failed runs, without changing script output.</summary>
    [TestMethod]
    public void CliWritesProfilesIncludingPartialFailures()
    {
        var directory = Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "tests", "profiler", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "main.ges"), "on Main(args) { emit ConsoleOut(42) }");
            var success = ToolProcess.Execute(directory, "run", "main.ges", "--profile", "success.md", "--quiet");
            Assert.AreEqual(0, success.ExitCode, success.StandardError);
            Assert.AreEqual("42", success.StandardOutput.Trim());
            var report = File.ReadAllText(Path.Combine(directory, "success.md"));
            StringAssert.Contains(report, "# GES opcode profile");
            StringAssert.Contains(report, "## Source lines");
            StringAssert.Contains(report, "main.ges:1");
            var failure = ToolProcess.Execute(directory, "run", "main.ges", "--max-steps", "1", "--profile", "failure.md");
            Assert.AreEqual(1, failure.ExitCode);
            StringAssert.Contains(File.ReadAllText(Path.Combine(directory, "failure.md")), "Instruction starts: 1.");
            Assert.AreEqual(2, ToolProcess.Execute(directory, "run", "--interactive", "--profile", "interactive.md").ExitCode);
            Assert.IsFalse(File.Exists(Path.Combine(directory, "interactive.md")));
            Assert.AreEqual(2, ToolProcess.Execute(directory, "run", "main.ges", "--profile", "main.ges").ExitCode);
        }
        finally { Directory.Delete(directory, true); }
    }

    /// <summary>Programs without debug data retain usable opcode/address reports and escaped module names.</summary>
    [TestMethod]
    public void ReportWorksWithoutDebugData()
    {
        var program = GameEventScriptBuilder.Create().WithDebugInfo(GameEventScriptDebugInfoOptions.None).AddScript("on Tick { emit Result(10) }").Compile();
        var profiler = new RunProfiler();
        var host = GameEventScriptHost.CreateBuilder().WithProfiler(profiler).Build();
        host.Load(program);
        host.Start();
        host.Receive(GameEventScriptMessage.Create("Tick"));
        host.RunToCompletion();
        StringAssert.Contains(profiler.Markdown(), "[unmapped]");
        StringAssert.Contains(profiler.Markdown(), "EmitMessage");
        StringAssert.Contains(profiler.Markdown(), "ReturnVoid");
    }
}
