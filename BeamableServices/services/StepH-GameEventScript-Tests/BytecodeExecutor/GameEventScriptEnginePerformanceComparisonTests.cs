using System.Diagnostics;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.BytecodeExecutor;
using StepH.GameEventScript.Extensions;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests.BytecodeExecutor;

[TestClass]
public sealed class BytecodeExecutorPerformanceReportTests
{
    private static readonly bool RunPerformanceReport = true;
    private const bool RunSoftMode = false;
    private const int WarmupRuns = 50;
    private const int MeasuredRuns = 100;

    private const string PerformanceScript =
        """
        module EnginePerformance

        predicate high(value as :number) means value >= 10

        on Start(values) {
          let total be values[:filter value where value is high][:select value => value + 5%][:sum value => :integer.floor value]
          let average be values[:filter value where value is high][:select value => value + 5%][:average value => :integer.floor value]
          let oddCount be values[:filter value where value mod 2 = 1][:count value where true]
          let firstBoosted be values[:filter value where value is high][:select value => value + 5%][:first]
          let scaled as :quantity(m) be 100m + 5%
          let folded be (15% + 15%) * 2
          let directOddScaled be values[:filter value where value mod 2 = 1][:select value => value * 2][:count value where value > 10]
          if scaled > 100m {
            let success be scaled + folded
            let divis be scaled ÷ folded
            let myHandler be Success(message, value)
            let myMessage be myHandler(message: 'hello', value: success)
            let myMessageDirect be Success(message: 'world', value: scaled)
          }
          if folded >= 50% {
            publish FoldedHigh(folded)
          }
          for item from 1 to 16 {
            let foldedBucket be values[:filter value where (value + item) mod 7 > 0][:select value => (value + item) * 2][:sum value => value]
          }
          let workTotal be values[:filter value where value >= 10][:select value => value + 5%][:select value => value * 2][:sum value => value]
          emit Done(total: total, average: average, oddCount: oddCount, directOddScaled: directOddScaled, first: firstBoosted, scaled: scaled, folded: folded, workTotal: workTotal)
        }
        """;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Performance")]
    public void BytecodeExecutorRuntimeCostCanBeReported()
    {
        if (!RunPerformanceReport)
        {
            TestContext.WriteLine("Performance report is disabled. Set RunPerformanceReport=true in this test to execute it manually.");
            return;
        }

        var diagnosticCollector = new GameEventScriptDiagnosticTraceCollector();

        var input = Create("Start", ("values", GameEventScriptValueFactory.GesList(Enumerable.Range(1, 50).Select(value => GameEventScriptValueFactory.GesInteger(value)))));

        WarmUp(input);

        var bytecodeVmCompile = Measure("ges compile", BuildPerformanceBytecode);
        var bytecodeVmBuild = Measure<IGameEventScriptModule>("bytecode executor build", () => BuildExecutable(bytecodeVmCompile.Value));

        var bytecodeVmRun = MeasureRun(bytecodeVmBuild.Value, input, MeasuredRuns);

        if (!RunSoftMode)
        {
            Assert.AreEqual(MeasuredRuns * 2, bytecodeVmRun.PublishedMessages);
            Assert.AreEqual("Done", bytecodeVmRun.LastMessage.Name);
        }

        TestContext.WriteLine("-----");
        WriteReport("Performance result;", bytecodeVmCompile, bytecodeVmBuild, bytecodeVmRun);
        TestContext.WriteLine("-----");
        TestContext.WriteLine("Bytecode Dump:\n" + bytecodeVmCompile.Value.DumpBytecode());
        TestContext.WriteLine("-----");
        TestContext.WriteLine("Binary file:\n" + bytecodeVmCompile.Value.ToGameEventScriptBinary().Dump(PerformanceScript));
        /*
        TestContext.WriteLine("-----");
        TestContext.WriteLine("Bytecode Diagnostics:\n" +diagnosticCollector.ToString());
        */
        TestContext.WriteLine("-----");
    }

    private static void WarmUp(GameEventScriptMessage input)
    {
        MeasureRun(BuildExecutable(BuildPerformanceBytecode()), input, WarmupRuns);
    }

    private static GameEventScriptCompiled BuildPerformanceBytecode() => GameEventScriptBuilder.Create()
        .WithEnableDiagnostic(false).WithDebugInfo().AddScript(PerformanceScript, "engine-performance.es").Compile();

    private static IGameEventScriptModule BuildExecutable(GameEventScriptCompiled bytecode) =>
        GameEventScriptVirtualMaschine.Create(bytecode.ToGameEventScriptBinary(), 128, 128);

    private static Measured<T> Measure<T>(string name, Func<T> action)
    {
        ForceFullCollection();
        var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        var value = action();
        stopwatch.Stop();
        return new Measured<T>(name, value, stopwatch.Elapsed, GC.GetAllocatedBytesForCurrentThread() - beforeAllocated);
    }

    private static EngineRunMetrics MeasureRun(IGameEventScriptModule compiled, GameEventScriptMessage input, int iterations, IGameEventScriptDiagnosticCollector? diagnosticCollector = null)
    {
        var publishedCount = 0;
        var lastMessage = input;
        var builder = GameEventScriptHost.CreateBuilder()
            .WithRuntimeLimits(new GameEventScriptRuntimeLimits
            {
                MaxProcessedEventsPerRun = 128
            })
            .WithRuntimeObserver(StepH_GameEventScript_Tests.TestRuntimeObserver.ObserveOutputs(message =>
            {
                publishedCount++;
                lastMessage = message;
            }));
        if (diagnosticCollector != null) builder.WithDiagnosticCollector(diagnosticCollector);
        var host = builder.Build().Load(compiled);
        ForceFullCollection();
        var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            host.PublishToCompletion(input);
        }

        stopwatch.Stop();
        return new EngineRunMetrics(stopwatch.Elapsed, GC.GetAllocatedBytesForCurrentThread() - beforeAllocated, publishedCount, lastMessage);
    }

    private void WriteReport(string runCase, Measured<GameEventScriptCompiled> compile, Measured<IGameEventScriptModule> lowered, EngineRunMetrics run)
    {
        TestContext.WriteLine(
            "{0}:\n    Ges Compile:      {1,9:#,##0.000} ms / {2,9} cumulated allocation\n    Executable Build: {3,9:#,##0.000} ms / {4,9} cumulated allocation\n    Run ({7} emits): {5,9:#,##0.000} ms / {6,9} cumulated allocation (average per emit {8})",
            runCase,
            compile.Elapsed.TotalMilliseconds,
            FormatBytes(compile.AllocatedBytes),
            lowered.Elapsed.TotalMilliseconds,
            FormatBytes(lowered.AllocatedBytes),
            run.Elapsed.TotalMilliseconds,
            FormatBytes(run.AllocatedBytes),
            run.PublishedMessages,
            FormatBytes(run.AllocatedBytes / Math.Max(1, run.PublishedMessages / 2)));
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)bytes;
        var unitIndex = 0;
        while (value >= 1024d && unitIndex < units.Length - 1)
        {
            value /= 1024d;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes} {units[unitIndex]}"
            : $"{value:0.##} {units[unitIndex]}";
    }

    private static void ForceFullCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private sealed record Measured<T>(string Name, T Value, TimeSpan Elapsed, long AllocatedBytes);

    private sealed record EngineRunMetrics(TimeSpan Elapsed, long AllocatedBytes, int PublishedMessages, GameEventScriptMessage LastMessage);
}
