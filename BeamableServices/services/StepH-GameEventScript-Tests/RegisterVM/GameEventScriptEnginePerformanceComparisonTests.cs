using System.Diagnostics;
using StepH.GameEventScript;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.RegisterVM;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.GseMessage;

namespace StepH_GameEventScript_Tests.RegisterVM;

[TestClass]
public sealed class RegisterVmPerformanceReportTests
{
    private const int WarmupRuns = 25;
    private const int MeasuredRuns = 1_000;

    private const string PerformanceScript =
        """
        module EnginePerformance

        rule high(value) means value >= 10

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
            let myHandler be Success(message, value)
            let myMessage be myHandler(message: 'hello', value: success)
            let myMessageDirect be Success(message: 'world', value: scaled)
          }
          for item from 1 to 16 {
            let foldedBucket be values[:filter value where (value + item) mod 7 > 0][:select value -> (value + item) * 2][:sum value -> value]
          }
          let workTotal be values[:filter value where value >= 10][:select value -> value + 5%][:select value -> value * 2][:sum value -> value]
          publish Done(total: total, average: average, oddCount: oddCount, directOddScaled: directOddScaled, first: firstBoosted, scaled: scaled, folded: folded, workTotal: workTotal)
        }
        """;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void RegisterVmRuntimeCostCanBeReported()
    {
        var input = Message("Start", ("values", GseValueFactory.List(
            Enumerable.Range(1, 50).Select(value => GseValueFactory.Integer(value)))));

        WarmUp(input);

        var module = BuildPerformanceModule();
        var registerVmCompile = Measure<IGseMessageHandlerCollection>("registervm compile", () => CompileScript(module));

        var registerVmRun = MeasureRun(registerVmCompile.Value, input, MeasuredRuns);

        Assert.AreEqual(MeasuredRuns, registerVmRun.PublishedMessages);
        Assert.AreEqual("Done", registerVmRun.LastMessage.Name);

        WriteReport("registervm", registerVmCompile, registerVmRun);
        TestContext.WriteLine("allocation values are cumulative thread allocations, not peak live memory.");
        TestContext.WriteLine("-----");
        TestContext.WriteLine("RegisterVM Dump:\n" + RegisterBytecodeDumper.ToDebugText(CompileScript(module)));
    }

    private static void WarmUp(GseMessage input)
    {
        var module = BuildPerformanceModule();
        MeasureRun(CompileScript(module), input, WarmupRuns);
    }

    private static GseModule BuildPerformanceModule()
        => GseModuleBuilder.Create()
            .AddScript(PerformanceScript, "engine-performance.es")
            .Build();

    private static RegisterCompiledGse CompileScript(GseModule module)
        => RegisterVmCompiler.Compile(module);

    private static Measured<T> Measure<T>(string name, Func<T> action)
    {
        ForceFullCollection();
        var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        var value = action();
        stopwatch.Stop();
        return new Measured<T>(name, value, stopwatch.Elapsed, GC.GetAllocatedBytesForCurrentThread() - beforeAllocated);
    }

    private static EngineRunMetrics MeasureRun(IGseMessageHandlerCollection compiled, GseMessage input, int iterations)
    {
        var publishedCount = 0;
        var lastMessage = GseMessage.EmptyMessage;
        var host = GseHost.CreateBuilder()
            .WithMaxProcessedEventsPerRun(128)
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
            host.Publish(input);
        }

        stopwatch.Stop();
        return new EngineRunMetrics(stopwatch.Elapsed, GC.GetAllocatedBytesForCurrentThread() - beforeAllocated, publishedCount, lastMessage);
    }

    private void WriteReport<T>(string engine, Measured<T> compile, EngineRunMetrics run)
    {
        TestContext.WriteLine(
            "{0}: compile {1:0.###} ms, compile allocated {2}, run {3:0.###} ms for {4} publishes, run allocated total {5}, run allocated per publish {6}",
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

    private sealed record EngineRunMetrics(TimeSpan Elapsed, long AllocatedBytes, int PublishedMessages, GseMessage LastMessage);
}
