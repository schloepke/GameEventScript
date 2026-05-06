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
          let total be values[:filter value where value is high][:select value -> value + 5%][:sum value -> value]
          let average be values[:filter value where value is high][:select value -> value + 5%][:average value -> value]
          let oddCount be values[:filter value where value mod 2 = 1][:count value where true]
          let firstBoosted be values[:filter value where value is high][:select value -> value + 5%][:first]
          let scaled as :meter be 100m + 5%
          let folded be (15% + 15%) * 2
          let directOddScaled be values[:filter value where value mod 2 = 1][:select value -> value * 2][:count value where value > 10]
          if scaled > 100m {
            let success be scaled + folded
            let divis be scaled ÷ folded
            let myHandler be Success(message, value)
            let myMessage be myHandler(message: 'hello', value: success)
            let myMessageDirect be Success(message: 'world', value: scaled)
          }
          for item from 1 to 16 {
            let foldedBucket be values[:filter value where (value + item) mod 7 > 0][:select value -> (value + item) * 2][:sum value -> value]
          }
          let workTotal be values[:filter value where value >= 10][:select value -> value + 5%][:select value -> value * 2][:sum value -> value]
          emit Done(total: total, average: average, oddCount: oddCount, directOddScaled: directOddScaled, first: firstBoosted, scaled: scaled, folded: folded, workTotal: workTotal)
        }
        """;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void BytecodeVmRuntimeCostCanBeReported()
    {
        var input = Create("Start", ("values", GameEventScriptValueFactory.GesList(
            Enumerable.Range(1, 50).Select(value => GameEventScriptValueFactory.GesInteger(value)))));

        WarmUp(input);

        var bytecodeVmCompile = Measure<GameEventScriptCompiled>("ges compile", BuildPerformanceBytecode);
        var bytecodeVmBuild = Measure<IGameEventScriptMessageHandlerCollection>("bytecodevm build", () => BuildExecutable(bytecodeVmCompile.Value));

        var bytecodeVmRun = MeasureRun(bytecodeVmBuild.Value, input, MeasuredRuns);

        Assert.AreEqual(MeasuredRuns, bytecodeVmRun.PublishedMessages);
        Assert.AreEqual("Done", bytecodeVmRun.LastMessage.Name);

        WriteReport("ges compile", bytecodeVmCompile, bytecodeVmRun);
        WriteReport("bytecodevm build", bytecodeVmBuild, bytecodeVmRun);
        TestContext.WriteLine("allocation values are cumulative thread allocations, not peak live memory.");
        TestContext.WriteLine("-----");
        TestContext.WriteLine("BytecodeVM Dump:\n" + bytecodeVmCompile.Value.DumpBytecode());
    }

    private static void WarmUp(GameEventScriptMessage input)
    {
        MeasureRun(BuildExecutable(BuildPerformanceBytecode()), input, WarmupRuns);
    }

    private static GameEventScriptCompiled BuildPerformanceBytecode()
        => GameEventScriptBuilder.Create()
            .AddScript(PerformanceScript, "engine-performance.es")
            .Compile();

    private static GesBytecodeVmExecutable BuildExecutable(GameEventScriptCompiled bytecode)
        => GesBytecodeVmExecutableBuilder.Build(bytecode);

    private static Measured<T> Measure<T>(string name, Func<T> action)
    {
        ForceFullCollection();
        var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        var value = action();
        stopwatch.Stop();
        return new Measured<T>(name, value, stopwatch.Elapsed, GC.GetAllocatedBytesForCurrentThread() - beforeAllocated);
    }

    private static EngineRunMetrics MeasureRun(IGameEventScriptMessageHandlerCollection compiled, GameEventScriptMessage input, int iterations)
    {
        var publishedCount = 0;
        var lastMessage = GameEventScriptMessage.Empty;
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeLimits(new GameEventScriptRuntimeLimits
            {
                MaxProcessedEventsPerRun = 128
            })
            .WithPublishedMessageObserver(message =>
            {
                publishedCount++;
                lastMessage = message;
            })
            .Build()
            .Load(compiled);

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

    private void WriteReport<T>(string engine, Measured<T> compile, EngineRunMetrics run)
    {
        TestContext.WriteLine(
            "{0}: compile {1:0.###} ms, compile allocated {2}, run {3:0.###} ms for {4} emits, run allocated total {5}, run allocated per emit {6}",
            engine,
            compile.Elapsed.TotalMilliseconds,
            FormatBytes(compile.AllocatedBytes),
            run.Elapsed.TotalMilliseconds,
            run.PublishedMessages,
            FormatBytes(run.AllocatedBytes),
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
