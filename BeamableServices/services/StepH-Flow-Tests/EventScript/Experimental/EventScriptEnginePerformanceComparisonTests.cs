using System.Diagnostics;
using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Experimental;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Types;
using static StepH.Flow.EventScript.EventScriptMessage;

namespace StepH_Flow_Tests.EventScript.Experimental;

[TestClass]
public sealed class EventScriptEnginePerformanceComparisonTests
{
    private const int WarmupRuns = 25;
    private const int MeasuredRuns = 1_000;

    private const string PerformanceScript =
        """
        module EnginePerformance

        rule high(value) means value >= 10
        select boosted(values) means values[:filter value where value is high][:select value -> value + 5%]

        on Start(values) {
          let boostedValues be boosted(values)
          let oddValues be values[:filter value where value % 2 = 1]
          let total be boostedValues[:sum value -> value]
          let average be boostedValues[:average value -> value]
          let scaled be 100m + 5%
          let folded be (15% + 15%) * 2
          let loopWork be :list[:select item from 1 to 32 -> values[:sum value -> (value + item) * ((item % 7) + 1)]]
          for item in loopWork {
            let adjusted be item + 5%
            let bucket be adjusted % 11
            let foldedBucket be (bucket + 3) * 2
          }
          let workTotal be loopWork[:sum value -> value]
          publish Done(total: total, average: average, oddCount: :len oddValues, first: boostedValues[1], scaled: scaled, folded: folded, workTotal: workTotal)
        }
        """;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void InterpreterAndExperimentalEnginesCanBeComparedForRuntimeCost()
    {
        var input = Message("Start", ("values", EventScriptValueFactory.List(
            Enumerable.Range(1, 50).Select(value => EventScriptValueFactory.Integer(value)))));

        WarmUp(input);

        var linked = LinkPerformanceScript();
        var interpreterCompile = Measure("interpreter compile", () => (IEventScriptMessageHandlerCollection)new CompiledEventScript(linked));
        var experimentalCompile = Measure("experimental compile", () => (IEventScriptMessageHandlerCollection)ExperimentalEventScriptCompiler.Compile(linked));

        var interpreterRun = MeasureRun(interpreterCompile.Value, input, MeasuredRuns);
        var experimentalRun = MeasureRun(experimentalCompile.Value, input, MeasuredRuns);

        AssertEquivalentOutput(interpreterRun.LastMessage, experimentalRun.LastMessage);
        Assert.AreEqual(MeasuredRuns, interpreterRun.PublishedMessages);
        Assert.AreEqual(MeasuredRuns, experimentalRun.PublishedMessages);

        WriteReport("interpreter", interpreterCompile, interpreterRun);
        WriteReport("experimental", experimentalCompile, experimentalRun);
        TestContext.WriteLine(
            "experimental/interpreter runtime ratio: {0:0.00}x",
            experimentalRun.Elapsed.TotalMilliseconds / Math.Max(0.001d, interpreterRun.Elapsed.TotalMilliseconds));
        TestContext.WriteLine(
            "experimental/interpreter runtime allocation ratio: {0:0.00}x",
            (double)experimentalRun.AllocatedBytes / Math.Max(1L, interpreterRun.AllocatedBytes));
        TestContext.WriteLine("allocation values are cumulative thread allocations, not peak live memory.");
    }

    private static void WarmUp(EventScriptMessage input)
    {
        var linked = LinkPerformanceScript();
        MeasureRun(new CompiledEventScript(linked), input, WarmupRuns);
        MeasureRun(ExperimentalEventScriptCompiler.Compile(linked), input, WarmupRuns);
    }

    private static LinkedEventScriptModule LinkPerformanceScript()
        => EventScriptManager.LinkModules(EventScriptManager.ParseModule(PerformanceScript, "engine-performance.es"));

    private static Measured<T> Measure<T>(string name, Func<T> action)
    {
        ForceFullCollection();
        var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        var value = action();
        stopwatch.Stop();
        return new Measured<T>(name, value, stopwatch.Elapsed, GC.GetAllocatedBytesForCurrentThread() - beforeAllocated);
    }

    private static EngineRunMetrics MeasureRun(IEventScriptMessageHandlerCollection compiled, EventScriptMessage input, int iterations)
    {
        var publishedCount = 0;
        var lastMessage = EventScriptMessage.EmptyMessage;
        var host = EventScriptHost.CreateBuilder()
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

    private static void AssertEquivalentOutput(EventScriptMessage expected, EventScriptMessage actual)
    {
        Assert.AreEqual(expected.Name, actual.Name);
        CollectionAssert.AreEqual(expected.Arguments.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray(), actual.Arguments.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
        foreach (var key in expected.Arguments.Keys)
        {
            Assert.AreEqual(expected.Arguments[key], actual.Arguments[key], $"Argument '{key}' differs.");
        }
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

    private sealed record EngineRunMetrics(TimeSpan Elapsed, long AllocatedBytes, int PublishedMessages, EventScriptMessage LastMessage);
}
