using System.Diagnostics;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.BytecodeVM;
using StepH.GameEventScript.Extensions;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests.BytecodeVM;

[TestClass]
public sealed class BytecodeVmPerformanceReportTests
{
    private const int WarmupRuns = 25;
    private const int MeasuredRuns = 1_000;

    private const string PerformanceScript =
        """
        module EnginePerformance

        predicate high(value as :integer) means value >= 10

        on Start(values) {
          let total be values[:filter value where value is high][:select value => value + 5%][:sum value => value]
          let average be values[:filter value where value is high][:select value => value + 5%][:average value => value]
          let oddCount be values[:filter value where value mod 2 = 1][:count value where true]
          let firstBoosted be values[:filter value where value is high][:select value => value + 5%][:first]
          let scaled as :meter be 100m + 5%
          let folded be (15% + 15%) * 2
          let directOddScaled be values[:filter value where value mod 2 = 1][:select value => value * 2][:count value where value > 10]
          if scaled > 100m {
            let success be scaled + folded
            let divis be scaled ÷ folded
            let myHandler be Success(message, value)
            let myMessage be myHandler(message: 'hello', value: success)
            let myMessageDirect be Success(message: 'world', value: scaled)
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
    public void BytecodeVmRuntimeCostCanBeReported()
    {
        var diagnosticCollector = new GameEventScriptDiagnosticTraceCollector();

        var input = Create("Start", ("values", GameEventScriptValueFactory.GesList(Enumerable.Range(1, 50).Select(value => GameEventScriptValueFactory.GesInteger(value)))));

        WarmUp(input);

        var bytecodeVmCompile = Measure("ges compile", () => BuildPerformanceBytecode(false));
        var bytecodeVmBuild = Measure<IGameEventScriptMessageHandlerCollection>("bytecodevm build", () => BuildExecutable(bytecodeVmCompile.Value));

        var bytecodeVmRun = MeasureRun(bytecodeVmBuild.Value, input, MeasuredRuns);

        var bytecodeVmCompileDiag = Measure("ges compile", () => BuildPerformanceBytecode(true));
        var bytecodeVmBuildDiag = Measure<IGameEventScriptMessageHandlerCollection>("bytecodevm build", () => BuildExecutable(bytecodeVmCompileDiag.Value));

        var bytecodeVmRunDiag = MeasureRun(bytecodeVmBuildDiag.Value, input, MeasuredRuns, new GameEventScriptDiagnosticTraceCollector());
        MeasureRun(bytecodeVmBuildDiag.Value, input, 1, diagnosticCollector);

        Assert.AreEqual(MeasuredRuns, bytecodeVmRun.PublishedMessages);
        Assert.AreEqual("Done", bytecodeVmRun.LastMessage.Name);

        TestContext.WriteLine("-----");
        WriteReport("Without diagnostic;", bytecodeVmCompile, bytecodeVmBuild, bytecodeVmRun);
        WriteReport("With diagnostic:", bytecodeVmCompileDiag, bytecodeVmBuildDiag, bytecodeVmRunDiag);
        TestContext.WriteLine("-----");
        TestContext.WriteLine(diagnosticCollector.ToString());
        TestContext.WriteLine("-----");
        TestContext.WriteLine("BytecodeVM Dump:\n" + bytecodeVmCompile.Value.DumpBytecode());
        TestContext.WriteLine("-----");
    }

    private static void WarmUp(GameEventScriptMessage input)
    {
        MeasureRun(BuildExecutable(BuildPerformanceBytecode(false)), input, WarmupRuns);
    }

    private static GameEventScriptCompiled BuildPerformanceBytecode(bool diagnostic) => GameEventScriptBuilder.Create().WithEnableDiagnostic(diagnostic).AddScript(PerformanceScript, "engine-performance.es").Compile();

    private static GesBytecodeVmExecutable BuildExecutable(GameEventScriptCompiled bytecode) => GesBytecodeVmExecutableBuilder.Build(bytecode);

    private static Measured<T> Measure<T>(string name, Func<T> action)
    {
        ForceFullCollection();
        var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        var value = action();
        stopwatch.Stop();
        return new Measured<T>(name, value, stopwatch.Elapsed, GC.GetAllocatedBytesForCurrentThread() - beforeAllocated);
    }

    private static EngineRunMetrics MeasureRun(IGameEventScriptMessageHandlerCollection compiled, GameEventScriptMessage input, int iterations, IGameEventScriptDiagnosticCollector? diagnosticCollector = null)
    {
        var publishedCount = 0;
        var lastMessage = GameEventScriptMessage.Empty;
        var builder = GameEventScriptHost.CreateBuilder()
            .WithRuntimeLimits(new GameEventScriptRuntimeLimits
            {
                MaxProcessedEventsPerRun = 128
            })
            .WithPublishedMessageObserver(message =>
            {
                publishedCount++;
                lastMessage = message;
            });
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

    private void WriteReport(string runCase, Measured<GameEventScriptCompiled> compile, Measured<IGameEventScriptMessageHandlerCollection> lowered, EngineRunMetrics run)
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
            FormatBytes(run.AllocatedBytes / Math.Max(1, run.PublishedMessages)));
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