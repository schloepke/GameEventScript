using System.Reflection;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.VirtualMachine;
using StepH.GameEventScript.Extensions;
using StepH.GameEventScript.Runtime;

namespace StepH_GameEventScript_Tests.Conformance;

[TestClass]
public sealed class GameEventScriptJsonConformanceTests : GameEventScriptJsonConformanceTestBase
{
    [TestMethod]
    [DynamicData(nameof(ApiMessagesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void ApiMessages(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(CompileBuildErrorsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void CompileBuildErrors(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(CompileSyntaxErrorsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void CompileSyntaxErrors(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(CompileBytecodeLoweringCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void CompileBytecodeLowering(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicMathMatrixCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicMathMatrix(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicEqualityCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicEquality(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicCompareCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicCompare(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicExternalAccessCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicExternalAccess(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicMemberIndexAccessCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicMemberIndexAccess(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicBooleanLogicCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicBooleanLogic(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicCreateValuesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicCreateValues(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicCastsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicCasts(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicCustomTypesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicCustomTypes(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicCollectionOperatorsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicCollectionOperators(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicControlFlowCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicControlFlow(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicSeriesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicSeries(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicStreamCoreCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicStreamCore(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicStreamTerminalsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicStreamTerminals(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicSortGroupDistinctCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicSortGroupDistinct(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicShuffleReverseCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicShuffleReverse(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicPatternsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicPatterns(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicRandomCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicRandom(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeCollectionsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeCollections(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeControlFlowCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeControlFlow(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeExtensionsSequencesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeExtensionsSequences(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeHostDispatchCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeHostDispatch(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeMessagesHandlersCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeMessagesHandlers(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimePredicatesFunctionsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimePredicatesFunctions(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimePublishTagsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimePublishTags(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeRandomDiceRangesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeRandomDiceRanges(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeTypesAndValuesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeTypesAndValues(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    public static string GetConformanceCaseDisplayName(MethodInfo methodInfo, object[] data)
        => FormatConformanceCaseDisplayName(methodInfo, data);
}

[TestClass]
public sealed class GameEventScriptJsonPerformanceTests : GameEventScriptJsonConformanceTestBase
{
    private static readonly bool RunPerformanceReport = false;
    private static readonly bool ComparePerformanceReportToReference = true;
    private static readonly double PerformanceElapsedRegressionTolerance = 0.15d;
    private static readonly double PerformanceElapsedMinimumToleranceMilliseconds = 1d;
    private static readonly int PerformanceIterations = 1_000;
    private static readonly int PerformanceWarmupIterations = 10;
    private const int DefaultIterations = 1_000;
    private const int DefaultWarmupIterations = 100;

    [TestMethod]
    [TestCategory("Performance")]
    [TestCategory("Explicit")]
    [DoNotParallelize]
    public void PerformanceReport()
    {
        var testCases = GameEventScriptConformanceRunner.AllConformanceCases(Path.Combine(SpecDirectory, "performance"));
        Assert.IsGreaterThan(0, testCases.Count);

        if (!RunPerformanceReport)
        {
            TestContext.WriteLine(
                "Explicit performance report is disabled. Set RunPerformanceReport=true in this test and run this test explicitly to execute it manually. " +
                "Use PerformanceIterations and PerformanceWarmupIterations in this test to override JSON iteration counts. " +
                "Copy PerformanceReport.current.txt to PerformanceReport.reference.txt to approve a new baseline. " +
                $"Loaded {testCases.Count} performance case(s).");
            return;
        }

        var report = new StringBuilder();
        report.AppendLine("# GameEventScript Performance Conformance Report");
        report.AppendLine();
        report.AppendLine($"cases={testCases.Count}");
        report.AppendLine($"performanceIterations={PerformanceIterations}");
        report.AppendLine($"performanceWarmupIterations={PerformanceWarmupIterations}");
        report.AppendLine();
        foreach (var testCase in testCases)
        {
            RunPerformanceCase(testCase, report);
        }

        var current = report.ToString();
        var referencePath = GetPerformanceReferencePath();
        var currentPath = GetPerformanceCurrentPath(referencePath);
        File.WriteAllText(currentPath, current);

        TestContext.WriteLine($"Performance current:   {currentPath}");
        TestContext.WriteLine($"Performance reference: {referencePath}");
        TestContext.WriteLine("Current:");
        TestContext.WriteLine(current);

        var reference = File.Exists(referencePath) ? File.ReadAllText(referencePath) : string.Empty;
        TestContext.WriteLine("Reference:");
        TestContext.WriteLine(reference.Length == 0 ? "<missing>" : reference);

        if (!ComparePerformanceReportToReference)
        {
            TestContext.WriteLine("Performance reference comparison is disabled. Set ComparePerformanceReportToReference=true to assert the snapshot.");
            return;
        }

        if (!File.Exists(referencePath))
        {
            Assert.Fail($"Performance reference does not exist. Copy current to reference to approve it: {currentPath} -> {referencePath}");
        }

        if (!PerformanceReportMatchesReference(reference, current, out var performanceDiff))
        {
            Assert.Fail(
                $"Performance report regressed. Current: {currentPath}; Reference: {referencePath}{Environment.NewLine}" +
                performanceDiff);
        }
    }

    private static string GetPerformanceReferencePath()
        => Path.Combine(
            Path.GetDirectoryName(SpecDirectory) ?? throw new DirectoryNotFoundException("Conformance directory was not found."),
            "PerformanceReport.reference.txt");

    private static string GetPerformanceCurrentPath(string referencePath)
        => Path.Combine(
            Path.GetDirectoryName(referencePath) ?? throw new DirectoryNotFoundException("Performance reference directory was not found."),
            "PerformanceReport.current.txt");

    private static bool PerformanceReportMatchesReference(string reference, string current, out string diff)
    {
        var referenceLines = reference.ReplaceLineEndings("\n").Split('\n');
        var currentLines = current.ReplaceLineEndings("\n").Split('\n');
        var builder = new StringBuilder();
        var count = Math.Max(referenceLines.Length, currentLines.Length);
        for (var index = 0; index < count; index++)
        {
            if (index >= referenceLines.Length)
            {
                builder.AppendLine($"line {index + 1}: unexpected current line '{currentLines[index]}'");
                continue;
            }

            if (index >= currentLines.Length)
            {
                builder.AppendLine($"line {index + 1}: missing current line, expected '{referenceLines[index]}'");
                continue;
            }

            var referenceLine = referenceLines[index];
            var currentLine = currentLines[index];
            if (referenceLine == currentLine)
            {
                continue;
            }

            if (TryComparePerformanceMetric(referenceLine, currentLine, out var metricDiff))
            {
                if (metricDiff.Length > 0)
                {
                    builder.AppendLine($"line {index + 1}: {metricDiff}");
                }

                continue;
            }

            builder.AppendLine($"line {index + 1}: expected '{referenceLine}' but was '{currentLine}'");
        }

        diff = builder.ToString();
        return diff.Length == 0;
    }

    private static bool TryComparePerformanceMetric(string referenceLine, string currentLine, out string diff)
    {
        diff = string.Empty;
        var referenceSeparator = referenceLine.IndexOf('=');
        var currentSeparator = currentLine.IndexOf('=');
        if (referenceSeparator <= 0 || currentSeparator <= 0)
        {
            return false;
        }

        var key = referenceLine[..referenceSeparator];
        if (!string.Equals(key, currentLine[..currentSeparator], StringComparison.Ordinal))
        {
            return false;
        }

        var referenceValue = referenceLine[(referenceSeparator + 1)..];
        var currentValue = currentLine[(currentSeparator + 1)..];
        if (key.EndsWith(".elapsedMs", StringComparison.Ordinal) ||
            key.EndsWith(".perInvokeElapsedMs", StringComparison.Ordinal))
        {
            if (!double.TryParse(referenceValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var referenceMs) ||
                !double.TryParse(currentValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var currentMs))
            {
                return false;
            }

            var allowed = referenceMs + Math.Max(
                referenceMs * PerformanceElapsedRegressionTolerance,
                PerformanceElapsedMinimumToleranceMilliseconds);
            if (currentMs > allowed)
            {
                diff = $"{key} regressed: reference={referenceMs.ToString("0.0000", CultureInfo.InvariantCulture)} ms, " +
                       $"current={currentMs.ToString("0.0000", CultureInfo.InvariantCulture)} ms, " +
                       $"allowed={allowed.ToString("0.0000", CultureInfo.InvariantCulture)} ms";
            }

            return true;
        }

        if (key.EndsWith(".allocatedBytes", StringComparison.Ordinal) ||
            key.EndsWith(".perInvokeAllocatedBytes", StringComparison.Ordinal))
        {
            if (!long.TryParse(referenceValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var referenceBytes) ||
                !long.TryParse(currentValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var currentBytes))
            {
                return false;
            }

            if (currentBytes > referenceBytes)
            {
                diff = $"{key} allocated more: reference={referenceBytes.ToString(CultureInfo.InvariantCulture)}, " +
                       $"current={currentBytes.ToString(CultureInfo.InvariantCulture)}";
            }

            return true;
        }

        return false;
    }

    private void RunPerformanceCase(GameEventScriptConformanceCase testCase, StringBuilder report)
    {
        if (!string.Equals(testCase.Test.Kind, "performance", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail($"{testCase}: performance suite contains unsupported test kind '{testCase.Test.Kind}'.");
        }

        if (testCase.Test.Steps is null || testCase.Test.Steps.Count == 0)
        {
            Assert.Fail($"{testCase}: performance tests require at least one step.");
        }

        var iterations = ResolveIterationCount(PerformanceIterations, testCase.Test.Iterations, DefaultIterations);
        var warmupIterations = ResolveIterationCount(PerformanceWarmupIterations, testCase.Test.WarmupIterations, DefaultWarmupIterations);
        var compiled = Measure("ges compile", () => GameEventScriptConformanceRunner.CompileBytecodeForTest(testCase.Test));
        var newBuild = Measure<IGameEventScriptModule>("new vm build", () => GameEventScriptVirtualMaschine.Create(compiled.Value, 4096, 256));
        if (testCase.Test.DumpBinary)
        {
            TestContext.WriteLine($"Binary dump: {testCase.SuiteName}/{testCase.Test.Name}");
            TestContext.WriteLine(compiled.Value.Dump(GetScriptSourceForDump(testCase)));
        }

        AssertPerformanceCorrectness(testCase, "new vm", newBuild.Value);

        var newRun = MeasurePerformanceRun(testCase, newBuild.Value, iterations, warmupIterations);

        AppendPerformanceCase(report, testCase, iterations, warmupIterations, compiled, newBuild, newRun);
    }

    private static void AppendPerformanceCase(
        StringBuilder report,
        GameEventScriptConformanceCase testCase,
        int iterations,
        int warmupIterations,
        Measured<GameEventScriptBinary> compiled,
        Measured<IGameEventScriptModule> build,
        PerformanceRunMetrics run)
    {
        report.AppendLine($"## {testCase.SuiteName}/{testCase.Test.Name}");
        report.AppendLine();
        report.AppendLine($"iterations={iterations}");
        report.AppendLine($"warmupIterations={warmupIterations}");
        report.AppendLine($"steps={testCase.Test.Steps!.Count}");
        AppendMeasured(report, "compile", compiled);
        AppendMeasured(report, "build", build);
        AppendRun(report, "run", run, iterations);
        report.AppendLine();
    }

    private static void AssertPerformanceCorrectness(
        GameEventScriptConformanceCase testCase,
        string engine,
        IGameEventScriptModule module)
    {
        var emitted = new List<GameEventScriptMessage>();
        var published = new List<GameEventScriptMessage>();
        var host = CreatePerformanceHost(testCase, module, emitted, published);

        for (var stepIndex = 0; stepIndex < testCase.Test.Steps!.Count; stepIndex++)
        {
            emitted.Clear();
            published.Clear();
            var step = testCase.Test.Steps[stepIndex];
            var handled = host.PublishToCompletion(GameEventScriptConformanceValueCodec.DecodeMessage(step.Input));
            if (!handled)
            {
                Assert.Fail($"{testCase} {engine} step {stepIndex + 1}: handler was not found or could not start.");
            }

            AssertMessages(testCase, engine, stepIndex, "emitted messages", step.ExpectedPublished, emitted);
            AssertMessages(testCase, engine, stepIndex, "outbound published messages", step.ExpectedOutboundPublished, published);
        }
    }

    private static PerformanceRunMetrics MeasurePerformanceRun(
        GameEventScriptConformanceCase testCase,
        IGameEventScriptModule module,
        int iterations,
        int warmupIterations)
    {
        var emittedCount = 0;
        var publishedCount = 0;
        var host = CreatePerformanceHost(
            testCase,
            module,
            _ => emittedCount++,
            _ => publishedCount++);

        RunPerformanceIterations(testCase, host, warmupIterations);
        ForceFullCollection();
        emittedCount = 0;
        publishedCount = 0;
        var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        RunPerformanceIterations(testCase, host, iterations);
        stopwatch.Stop();
        return new PerformanceRunMetrics(
            stopwatch.Elapsed,
            GC.GetAllocatedBytesForCurrentThread() - beforeAllocated,
            emittedCount,
            publishedCount);
    }

    private static void RunPerformanceIterations(GameEventScriptConformanceCase testCase, GameEventScriptHost host, int iterations)
    {
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            foreach (var step in testCase.Test.Steps!)
            {
                var handled = host.PublishToCompletion(GameEventScriptConformanceValueCodec.DecodeMessage(step.Input));
                if (!handled)
                {
                    Assert.Fail($"{testCase}: handler was not found or could not start during performance run.");
                }
            }
        }
    }

    private static GameEventScriptHost CreatePerformanceHost(
        GameEventScriptConformanceCase testCase,
        IGameEventScriptModule module,
        List<GameEventScriptMessage> emitted,
        List<GameEventScriptMessage> published)
        => CreatePerformanceHost(testCase, module, emitted.Add, published.Add);

    private static GameEventScriptHost CreatePerformanceHost(
        GameEventScriptConformanceCase testCase,
        IGameEventScriptModule module,
        Action<GameEventScriptMessage> emitted,
        Action<GameEventScriptMessage> published)
    {
        var builder = GameEventScriptManager.CreateHostBuilder()
            .WithRandom(GameEventScriptConformanceRunner.CreateRandomForTest(testCase.Test.RandomSequence))
            .WithRegistry(GameEventScriptConformanceExtensionRegistry.Instance)
            .WithExternalTypes(GameEventScriptConformanceRunner.ExternalTypeRegistry)
            .WithRuntimeLimits(GameEventScriptConformanceRunner.CreateRuntimeLimitsForTest(testCase.Test.RuntimeLimits))
            .WithRuntimeObserver(TestRuntimeObserver.ObserveMessages(
                messageEmitted: emitted,
                messagePublished: emitted))
            .WithPublishHook(message =>
            {
                published(message);
                return true;
            });
        var host = builder.Build().Load(module);
        GameEventScriptConformanceRunner.RegisterExternalSubscribers(testCase, host);
        return host;
    }

    private static void AssertMessages(
        GameEventScriptConformanceCase testCase,
        string engine,
        int stepIndex,
        string label,
        IReadOnlyList<JsonElement>? expectedPublished,
        IReadOnlyList<GameEventScriptMessage> actual)
    {
        var expected = (expectedPublished ?? []).Select(GameEventScriptConformanceValueCodec.DecodeMessage).ToArray();
        var expectedJson = GameEventScriptConformanceValueCodec.ToCanonicalJson(expected);
        var actualJson = GameEventScriptConformanceValueCodec.ToCanonicalJson(actual);
        if (expectedJson == actualJson)
        {
            return;
        }

        Assert.Fail(
            $"{testCase} {engine} step {stepIndex + 1}: {label} differ.{Environment.NewLine}" +
            $"Expected:{Environment.NewLine}{GameEventScriptConformanceValueCodec.ToPrettyJson(expected)}{Environment.NewLine}" +
            $"Actual:{Environment.NewLine}{GameEventScriptConformanceValueCodec.ToPrettyJson(actual)}");
    }

    private static int ResolveIterationCount(int testOverride, int? jsonValue, int defaultValue)
        => testOverride >= 0 ? testOverride : Math.Max(0, jsonValue ?? defaultValue);

    private static Measured<T> Measure<T>(string name, Func<T> action)
    {
        ForceFullCollection();
        var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        var value = action();
        stopwatch.Stop();
        return new Measured<T>(name, value, stopwatch.Elapsed, GC.GetAllocatedBytesForCurrentThread() - beforeAllocated);
    }

    private static void AppendMeasured<T>(StringBuilder report, string label, Measured<T> measured)
    {
        report.Append(label);
        report.Append(".elapsedMs=");
        report.AppendLine(FormatMilliseconds(measured.Elapsed.TotalMilliseconds));
        report.Append(label);
        report.Append(".allocatedBytes=");
        report.AppendLine(measured.AllocatedBytes.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendRun(StringBuilder report, string label, PerformanceRunMetrics run, int iterations)
    {
        var safeIterations = Math.Max(1, iterations);
        report.Append(label);
        report.Append(".elapsedMs=");
        report.AppendLine(FormatMilliseconds(run.Elapsed.TotalMilliseconds));
        report.Append(label);
        report.Append(".allocatedBytes=");
        report.AppendLine(run.AllocatedBytes.ToString(CultureInfo.InvariantCulture));
        report.Append(label);
        report.Append(".perInvokeElapsedMs=");
        report.AppendLine(FormatMilliseconds(run.Elapsed.TotalMilliseconds / safeIterations));
        report.Append(label);
        report.Append(".perInvokeAllocatedBytes=");
        report.AppendLine((run.AllocatedBytes / safeIterations).ToString(CultureInfo.InvariantCulture));
        report.Append(label);
        report.Append(".emittedMessages=");
        report.AppendLine(run.EmittedMessages.ToString(CultureInfo.InvariantCulture));
        report.Append(label);
        report.Append(".outboundPublishedMessages=");
        report.AppendLine(run.OutboundPublishedMessages.ToString(CultureInfo.InvariantCulture));
    }

    private static string FormatMilliseconds(double milliseconds)
        => milliseconds.ToString("0.0000", CultureInfo.InvariantCulture);

    private static void ForceFullCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private sealed record Measured<T>(string Name, T Value, TimeSpan Elapsed, long AllocatedBytes);

    private sealed record PerformanceRunMetrics(
        TimeSpan Elapsed,
        long AllocatedBytes,
        int EmittedMessages,
        int OutboundPublishedMessages);
}

public abstract class GameEventScriptJsonConformanceTestBase
{
    protected static readonly string SpecDirectory = Path.Combine(GetSourceDirectory(), "Specs");

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> ApiMessagesCases()
        => Cases("api/messages.json");

    public static IEnumerable<object[]> CompileBuildErrorsCases()
        => Cases("compile/build-errors.json");

    public static IEnumerable<object[]> CompileSyntaxErrorsCases()
        => Cases("compile/syntax-errors.json");

    public static IEnumerable<object[]> CompileBytecodeLoweringCases()
        => Cases("compile/bytecode-lowering.json");

    public static IEnumerable<object[]> RuntimeAtomicMathMatrixCases()
        => Cases("runtime/atomic/math-matrix.json");

    public static IEnumerable<object[]> RuntimeAtomicEqualityCases()
        => Cases("runtime/atomic/equality.json");

    public static IEnumerable<object[]> RuntimeAtomicCompareCases()
        => Cases("runtime/atomic/compare.json");

    public static IEnumerable<object[]> RuntimeAtomicExternalAccessCases()
        => Cases("runtime/atomic/external-access.json");

    public static IEnumerable<object[]> RuntimeAtomicMemberIndexAccessCases()
        => Cases("runtime/atomic/member-index-access.json");

    public static IEnumerable<object[]> RuntimeAtomicBooleanLogicCases()
        => Cases("runtime/atomic/boolean-logic.json");

    public static IEnumerable<object[]> RuntimeAtomicCreateValuesCases()
        => Cases("runtime/atomic/create-values.json");

    public static IEnumerable<object[]> RuntimeAtomicCastsCases()
        => Cases("runtime/atomic/casts.json");

    public static IEnumerable<object[]> RuntimeAtomicCustomTypesCases()
        => Cases("runtime/atomic/custom-types.json");

    public static IEnumerable<object[]> RuntimeAtomicCollectionOperatorsCases()
        => Cases("runtime/atomic/collection-operators.json");

    public static IEnumerable<object[]> RuntimeAtomicControlFlowCases()
        => Cases("runtime/atomic/control-flow.json");

    public static IEnumerable<object[]> RuntimeAtomicSeriesCases()
        => Cases("runtime/atomic/series.json");

    public static IEnumerable<object[]> RuntimeAtomicStreamCoreCases()
        => Cases("runtime/atomic/stream-core.json");

    public static IEnumerable<object[]> RuntimeAtomicStreamTerminalsCases()
        => Cases("runtime/atomic/stream-terminals.json");

    public static IEnumerable<object[]> RuntimeAtomicSortGroupDistinctCases()
        => Cases("runtime/atomic/sort-group-distinct.json");

    public static IEnumerable<object[]> RuntimeAtomicShuffleReverseCases()
        => Cases("runtime/atomic/shuffle-reverse.json");

    public static IEnumerable<object[]> RuntimeAtomicPatternsCases()
        => Cases("runtime/atomic/patterns.json");

    public static IEnumerable<object[]> RuntimeAtomicRandomCases()
        => Cases("runtime/atomic/random.json");

    public static IEnumerable<object[]> RuntimeCollectionsCases()
        => Cases("runtime/collections.json");

    public static IEnumerable<object[]> RuntimeControlFlowCases()
        => Cases("runtime/control-flow.json");

    public static IEnumerable<object[]> RuntimeExtensionsSequencesCases()
        => Cases("runtime/extensions-sequences.json");

    public static IEnumerable<object[]> RuntimeHostDispatchCases()
        => Cases("runtime/host-dispatch.json");

    public static IEnumerable<object[]> RuntimeMessagesHandlersCases()
        => Cases("runtime/messages-handlers.json");

    public static IEnumerable<object[]> RuntimePredicatesFunctionsCases()
        => Cases("runtime/predicates-functions.json");

    public static IEnumerable<object[]> RuntimePublishTagsCases()
        => Cases("runtime/publish-tags.json");

    public static IEnumerable<object[]> RuntimeRandomDiceRangesCases()
        => Cases("runtime/random-dice-ranges.json");

    public static IEnumerable<object[]> RuntimeTypesAndValuesCases()
        => Cases("runtime/types-and-values.json");

    protected static string FormatConformanceCaseDisplayName(MethodInfo methodInfo, object[] data)
        => data is [GameEventScriptConformanceCase testCase]
            ? testCase.Test.Name ?? testCase.ToString()
            : methodInfo.Name;

    protected void RunJsonConformanceCase(GameEventScriptConformanceCase testCase)
    {
        if (string.Equals(testCase.Test.Kind, "scriptApi", StringComparison.OrdinalIgnoreCase))
        {
            RunScriptApiConformanceCase(testCase);
            return;
        }

        switch (testCase.Test.Kind)
        {
            case "compileError":
                GameEventScriptConformanceRunner.RunCompileErrorTest(testCase);
                break;
            case "messageApi":
                GameEventScriptConformanceRunner.RunMessageApiTest(testCase);
                break;
            case "compileMetadata":
                GameEventScriptConformanceRunner.RunCompileMetadataTest(testCase);
                break;
            case "bytecode":
                GameEventScriptConformanceRunner.RunBytecodeOpcodeTest(testCase);
                break;
            default:
                Assert.Fail($"{testCase}: unsupported test kind '{testCase.Test.Kind}'.");
                break;
        }
    }

    protected void RunScriptApiConformanceCase(GameEventScriptConformanceCase testCase)
    {
        var outcome = RunScriptApiCase(testCase);
        TestContext.WriteLine($"{outcome.Status}: {testCase}: {outcome.Detail}");
        if (!outcome.Passed)
        {
            TestContext.WriteLine(outcome.DebugDump);
        }

        if (!outcome.Passed)
        {
            Assert.Fail($"{outcome.Status}: {testCase}: {outcome.Detail}");
        }
    }

    protected static ScriptApiConformanceOutcome RunScriptApiCase(
        GameEventScriptConformanceCase testCase,
        bool includeMessageDiff = true)
    {
        GameEventScriptBinary binary;
        try
        {
            binary = GameEventScriptConformanceRunner.CompileBytecodeForTest(testCase.Test);
        }
        catch (Exception exception)
        {
            return ScriptApiConformanceOutcome.CompileFailure(exception.Message);
        }

        try
        {
            return RunScriptApiCase(testCase, binary, includeMessageDiff, out var mismatch, out var debugDump)
                ? ScriptApiConformanceOutcome.Pass()
                : ScriptApiConformanceOutcome.Mismatch(mismatch, debugDump);
        }
        catch (Exception exception)
        {
            return ScriptApiConformanceOutcome.RuntimeFailure(exception.Message);
        }
    }

    protected static string? GetScriptSourceForDump(GameEventScriptConformanceCase testCase)
    {
        var test = testCase.Test;
        if (!string.IsNullOrWhiteSpace(test.Script))
        {
            return test.Script;
        }

        if (test.Scripts is not { Count: > 0 })
        {
            return null;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < test.Scripts.Count; index++)
        {
            var source = test.Scripts[index];
            if (index > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine($"// source: {source.SourceName ?? $"script-{index + 1}"}");
            builder.Append(source.Text ?? string.Empty);
            if (builder.Length > 0 && builder[^1] != '\n')
            {
                builder.AppendLine();
            }
        }
        return builder.ToString();
    }

    private static IEnumerable<object[]> Cases(string relativeSpecFile)
        => GameEventScriptConformanceRunner.ConformanceCases(SpecDirectory, relativeSpecFile);

    private static string GetSourceDirectory([CallerFilePath] string sourceFile = "")
        => Path.GetDirectoryName(sourceFile)!;

    private static bool RunScriptApiCase(
        GameEventScriptConformanceCase testCase,
        GameEventScriptBinary binary,
        bool includeMessageDiff,
        out string mismatch,
        out string debugDump)
    {
        mismatch = string.Empty;
        debugDump = string.Empty;
        var test = testCase.Test;
        var hasInitializationExpectations =
            test.ExpectedInitializationPublished is not null ||
            test.ExpectedInitializationOutboundPublished is not null;
        if ((test.Steps is null || test.Steps.Count == 0) && !hasInitializationExpectations)
        {
            mismatch = "scriptApi tests require at least one step or initialization expectation.";
            return false;
        }

        var random = GameEventScriptConformanceRunner.CreateRandomForTest(test.RandomSequence);
        var runtimeLimits = GameEventScriptConformanceRunner.CreateRuntimeLimitsForTest(test.RuntimeLimits);
        var emitted = new List<GameEventScriptMessage>();
        var published = new List<GameEventScriptMessage>();
        var observedRuntimeLimits = new List<TestRuntimeLimitEvent>();
        var scriptSource = GetScriptSourceForDump(testCase);
        var vmModule = (GameEventScriptVirtualMaschine)GameEventScriptManager.CreateModule(binary, 4096, 256);
        vmModule.DebugScriptSource = scriptSource;
        var lastCapturedVmDump = string.Empty;
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRandom(random)
            .WithRegistry(GameEventScriptConformanceExtensionRegistry.Instance)
            .WithExternalTypes(GameEventScriptConformanceRunner.ExternalTypeRegistry)
            .WithRuntimeLimits(runtimeLimits)
            .WithRuntimeObserver(TestRuntimeObserver.ObserveMessages(
                messageEmitted: message =>
                {
                    emitted.Add(message);
                    lastCapturedVmDump = vmModule.DumpState(scriptSource);
                },
                messagePublished: message =>
                {
                    emitted.Add(message);
                    lastCapturedVmDump = vmModule.DumpState(scriptSource);
                },
                runtimeLimitReached: (name, detail, limit) => observedRuntimeLimits.Add(new TestRuntimeLimitEvent(name, detail, limit))))
            .WithPublishHook(message =>
            {
                published.Add(message);
                lastCapturedVmDump = vmModule.DumpState(scriptSource);
                return true;
            })
            .Build()
            .Load(vmModule);
        GameEventScriptConformanceRunner.RegisterExternalSubscribers(testCase, host);
        if (hasInitializationExpectations)
        {
            host.StartSession().Update(runtimeLimits.MaxExecutionSteps);
        }

        if (test.ExpectedInitializationPublished is not null &&
            !TryMatchMessages(testCase, -1, "initialization emitted messages", test.ExpectedInitializationPublished, emitted, includeMessageDiff, out var initializationEmittedDiff))
        {
            mismatch = $"initialization: emitted messages differ.{Environment.NewLine}{initializationEmittedDiff}";
            debugDump = CaptureVmDump(lastCapturedVmDump);
            return false;
        }

        if (test.ExpectedInitializationOutboundPublished is not null &&
            !TryMatchMessages(testCase, -1, "initialization published messages", test.ExpectedInitializationOutboundPublished, published, includeMessageDiff, out var initializationPublishedDiff))
        {
            mismatch = $"initialization: published messages differ.{Environment.NewLine}{initializationPublishedDiff}";
            debugDump = CaptureVmDump(lastCapturedVmDump);
            return false;
        }

        for (var stepIndex = 0; stepIndex < (test.Steps?.Count ?? 0); stepIndex++)
        {
            var step = test.Steps![stepIndex];
            emitted.Clear();
            published.Clear();
            observedRuntimeLimits.Clear();
            var handled = host.PublishToCompletion(GameEventScriptConformanceValueCodec.DecodeMessage(step.Input));
            if (!handled)
            {
                mismatch = $"step {stepIndex + 1}: handler was not found or could not start.";
                debugDump = CaptureVmDump(lastCapturedVmDump);
                return false;
            }

            if (!TryMatchMessages(testCase, stepIndex, "emitted messages", step.ExpectedPublished, emitted, includeMessageDiff, out var emittedDiff))
            {
                mismatch = $"step {stepIndex + 1}: emitted messages differ.{Environment.NewLine}{emittedDiff}";
                debugDump = CaptureVmDump(lastCapturedVmDump);
                return false;
            }

            if (!TryMatchMessages(testCase, stepIndex, "published messages", step.ExpectedOutboundPublished, published, includeMessageDiff, out var publishedDiff))
            {
                mismatch = $"step {stepIndex + 1}: published messages differ.{Environment.NewLine}{publishedDiff}";
                debugDump = CaptureVmDump(lastCapturedVmDump);
                return false;
            }

            if (!TryMatchRuntimeLimits(testCase, stepIndex, step, observedRuntimeLimits, out var runtimeLimitDiff))
            {
                mismatch = $"step {stepIndex + 1}: runtime limits differ.{Environment.NewLine}{runtimeLimitDiff}";
                debugDump = CaptureVmDump(lastCapturedVmDump);
                return false;
            }
        }

        return true;
    }

    private static string CaptureVmDump(string lastCapturedVmDump)
        => !string.IsNullOrWhiteSpace(lastCapturedVmDump)
            ? lastCapturedVmDump
            : "//\t<vm state dump unavailable: no message was emitted or published before the assertion failed>";

    private static bool TryMatchMessages(
        GameEventScriptConformanceCase testCase,
        int stepIndex,
        string label,
        IReadOnlyList<JsonElement>? expectedPublished,
        IReadOnlyList<GameEventScriptMessage> actual,
        bool includeMessageDiff,
        out string diff)
    {
        diff = string.Empty;
        var expected = (expectedPublished ?? []).Select(GameEventScriptConformanceValueCodec.DecodeMessage).ToArray();
        var expectedJson = GameEventScriptConformanceValueCodec.ToCanonicalJson(expected);
        var actualJson = GameEventScriptConformanceValueCodec.ToCanonicalJson(actual);
        if (expectedJson == actualJson)
        {
            return true;
        }

        diff = includeMessageDiff
            ? BuildMessageDiff(testCase, stepIndex, label, expected, actual)
            : $"expectedMessages={expected.Length}, actualMessages={actual.Count}";
        return false;
    }

    private static bool TryMatchRuntimeLimits(
        GameEventScriptConformanceCase testCase,
        int stepIndex,
        GameEventScriptApiStepSpec step,
        IReadOnlyList<TestRuntimeLimitEvent> actual,
        out string diff)
    {
        diff = string.Empty;
        if (!RuntimeLimitsMatch(step.ExpectedRuntimeLimits, step.UnexpectedRuntimeLimits, actual, out var details))
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Test: {testCase.Test.Name ?? testCase.ToString()}");
            builder.AppendLine($"Step: {stepIndex + 1}");
            builder.AppendLine("Channel: runtime limits");
            builder.Append(details);
            diff = builder.ToString();
            return false;
        }

        return true;
    }

    private static bool RuntimeLimitsMatch(
        IReadOnlyList<GameEventScriptRuntimeLimitExpectationSpec>? expectedRuntimeLimits,
        IReadOnlyList<GameEventScriptRuntimeLimitExpectationSpec>? unexpectedRuntimeLimits,
        IReadOnlyList<TestRuntimeLimitEvent> actual,
        out string diff)
    {
        diff = string.Empty;
        var builder = new StringBuilder();
        var nextStart = 0;
        foreach (var expected in expectedRuntimeLimits ?? [])
        {
            var foundIndex = -1;
            for (var i = nextStart; i < actual.Count; i++)
            {
                if (RuntimeLimitMatches(expected, actual[i]))
                {
                    foundIndex = i;
                    break;
                }
            }

            if (foundIndex < 0)
            {
                AppendDiff(builder, "expectedRuntimeLimit", DescribeRuntimeLimitExpectation(expected), DescribeRuntimeLimits(actual));
            }
            else
            {
                nextStart = foundIndex + 1;
            }
        }

        foreach (var unexpected in unexpectedRuntimeLimits ?? [])
        {
            var found = actual.FirstOrDefault(runtimeLimit => RuntimeLimitMatches(unexpected, runtimeLimit));
            if (found is not null)
            {
                AppendDiff(builder, "unexpectedRuntimeLimit", DescribeRuntimeLimitExpectation(unexpected), DescribeRuntimeLimit(found));
            }
        }

        diff = builder.ToString();
        return diff.Length == 0;
    }

    private static bool RuntimeLimitMatches(GameEventScriptRuntimeLimitExpectationSpec expected, TestRuntimeLimitEvent actual)
    {
        if (!string.IsNullOrWhiteSpace(expected.Name) &&
            !string.Equals(expected.Name, actual.Name, StringComparison.Ordinal))
        {
            return false;
        }

        if (expected.Limit is not null && expected.Limit.Value != actual.Limit)
        {
            return false;
        }

        return string.IsNullOrEmpty(expected.DetailContains) ||
               actual.Detail.Contains(expected.DetailContains, StringComparison.Ordinal);
    }

    private static string DescribeRuntimeLimitExpectation(GameEventScriptRuntimeLimitExpectationSpec expected)
        => $"name={expected.Name ?? "*"}, limit={expected.Limit?.ToString() ?? "*"}, detailContains={expected.DetailContains ?? "*"}";

    private static string DescribeRuntimeLimits(IEnumerable<TestRuntimeLimitEvent> runtimeLimits)
        => string.Join("; ", runtimeLimits.Select(DescribeRuntimeLimit));

    private static string DescribeRuntimeLimit(TestRuntimeLimitEvent runtimeLimit)
        => $"{runtimeLimit.Name} ({runtimeLimit.Limit}): {runtimeLimit.Detail}";

    private static string BuildMessageDiff(
        GameEventScriptConformanceCase testCase,
        int stepIndex,
        string label,
        IReadOnlyList<GameEventScriptMessage> expected,
        IReadOnlyList<GameEventScriptMessage> actual)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Test: {testCase.Test.Name ?? testCase.ToString()}");
        builder.AppendLine(stepIndex >= 0 ? $"Step: {stepIndex + 1}" : "Step: initialization");
        builder.AppendLine($"Channel: {label}");

        if (expected.Count != actual.Count)
        {
            AppendDiff(builder, "messageCount", expected.Count.ToString(), actual.Count.ToString());
        }

        var count = Math.Max(expected.Count, actual.Count);
        for (var messageIndex = 0; messageIndex < count; messageIndex++)
        {
            if (messageIndex >= expected.Count)
            {
                AppendDiff(
                    builder,
                    $"message[{messageIndex}]",
                    "<missing>",
                    GameEventScriptConformanceValueCodec.ToCanonicalJson(actual[messageIndex]));
                continue;
            }

            if (messageIndex >= actual.Count)
            {
                AppendDiff(
                    builder,
                    $"message[{messageIndex}]",
                    GameEventScriptConformanceValueCodec.ToCanonicalJson(expected[messageIndex]),
                    "<missing>");
                continue;
            }

            AppendMessageDiff(builder, messageIndex, expected[messageIndex], actual[messageIndex]);
        }

        return builder.ToString();
    }

    private static void AppendMessageDiff(
        StringBuilder builder,
        int messageIndex,
        GameEventScriptMessage expected,
        GameEventScriptMessage actual)
    {
        if (!string.Equals(expected.Name, actual.Name, StringComparison.Ordinal))
        {
            AppendDiff(builder, $"message[{messageIndex}].name", Quote(expected.Name), Quote(actual.Name));
        }

        var expectedTags = JsonSerializer.Serialize(expected.Tags);
        var actualTags = JsonSerializer.Serialize(actual.Tags);
        if (!string.Equals(expectedTags, actualTags, StringComparison.Ordinal))
        {
            AppendDiff(builder, $"message[{messageIndex}].tags", expectedTags, actualTags);
        }

        foreach (var key in expected.Arguments.Keys.Concat(actual.Arguments.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var hasExpected = expected.Arguments.TryGetValue(key, out var expectedValue);
            var hasActual = actual.Arguments.TryGetValue(key, out var actualValue);
            var expectedJson = hasExpected ? GameEventScriptConformanceValueCodec.ToCanonicalJson(expectedValue!) : "<missing>";
            var actualJson = hasActual ? GameEventScriptConformanceValueCodec.ToCanonicalJson(actualValue!) : "<missing>";
            if (!string.Equals(expectedJson, actualJson, StringComparison.Ordinal))
            {
                AppendDiff(builder, $"message[{messageIndex}].args.{key}", expectedJson, actualJson);
            }
        }
    }

    private static void AppendDiff(StringBuilder builder, string path, string expected, string actual)
    {
        builder.AppendLine(path);
        builder.AppendLine($"  expected: {expected}");
        builder.AppendLine($"  actual:   {actual}");
    }

    private static string Quote(string value)
        => JsonSerializer.Serialize(value);

    protected enum ScriptApiConformanceStatus
    {
        Passed,
        Mismatch,
        CompileFailure,
        RuntimeFailure
    }

    protected sealed record ScriptApiConformanceOutcome(
        ScriptApiConformanceStatus Status,
        string Detail,
        string DebugDump = "")
    {
        public bool Passed => Status == ScriptApiConformanceStatus.Passed;

        public static ScriptApiConformanceOutcome Pass()
            => new(ScriptApiConformanceStatus.Passed, "passed");

        public static ScriptApiConformanceOutcome Mismatch(string detail, string debugDump)
            => new(ScriptApiConformanceStatus.Mismatch, detail, debugDump);

        public static ScriptApiConformanceOutcome CompileFailure(string detail)
            => new(ScriptApiConformanceStatus.CompileFailure, detail);

        public static ScriptApiConformanceOutcome RuntimeFailure(string detail)
            => new(ScriptApiConformanceStatus.RuntimeFailure, detail);
    }
}
