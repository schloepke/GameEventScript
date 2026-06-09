using System.Reflection;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.BytecodeExecutor;
using StepH.GameEventScript.BytecodeVM;
using StepH.GameEventScript.Runtime;

namespace StepH_GameEventScript_Tests.Conformance;

[TestClass]
public sealed class GameEventScriptOldVmJsonConformanceTests : GameEventScriptJsonConformanceTestBase
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
public sealed class GameEventScriptNewVmJsonConformanceTests : GameEventScriptJsonConformanceTestBase
{
    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicMathMatrixCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicMathMatrix(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicEqualityCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicEquality(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicCompareCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicCompare(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicExternalAccessCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicExternalAccess(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicMemberIndexAccessCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicMemberIndexAccess(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicBooleanLogicCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicBooleanLogic(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicCreateValuesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicCreateValues(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicCastsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicCasts(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicCustomTypesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicCustomTypes(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicCollectionOperatorsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicCollectionOperators(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicControlFlowCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicControlFlow(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicSeriesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicSeries(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicStreamCoreCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicStreamCore(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicStreamTerminalsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicStreamTerminals(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicSortGroupDistinctCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicSortGroupDistinct(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicShuffleReverseCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicShuffleReverse(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicPatternsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicPatterns(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicRandomCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicRandom(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeCollectionsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeCollections(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeControlFlowCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeControlFlow(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeExtensionsSequencesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeExtensionsSequences(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeHostDispatchCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeHostDispatch(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeMessagesHandlersCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeMessagesHandlers(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimePredicatesFunctionsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimePredicatesFunctions(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimePublishTagsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimePublishTags(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeRandomDiceRangesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeRandomDiceRanges(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeTypesAndValuesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeTypesAndValues(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    public static string GetConformanceCaseDisplayName(MethodInfo methodInfo, object[] data)
        => FormatConformanceCaseDisplayName(methodInfo, data);
}

[TestClass]
public sealed class GameEventScriptNewVmJsonConformanceSmokeTests : GameEventScriptJsonConformanceTestBase
{
    [TestMethod]
    public void NewVirtualMachineConformanceSmoke()
    {
        var allCases = GameEventScriptConformanceRunner.AllConformanceCases(SpecDirectory);
        var testCases = allCases
            .Where(testCase => string.Equals(testCase.Test.Kind, "scriptApi", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var result = new NewVmConformanceResult(
            totalCases: allCases.Count,
            targetedCases: testCases.Length,
            skippedNonRuntimeCases: allCases.Count - testCases.Length);
        var samples = new List<string>();
        var byLevel = new SortedDictionary<string, NewVmConformanceSuiteResult>(StringComparer.Ordinal);
        var bySuite = new SortedDictionary<string, NewVmConformanceSuiteResult>(StringComparer.Ordinal);

        foreach (var testCase in testCases)
        {
            result.Attempted++;
            var level = GetSuiteResult(byLevel, testCase.Level);
            level.Attempted++;
            var suite = GetSuiteResult(bySuite, testCase.SuiteName);
            suite.Attempted++;
            var outcome = RunNewVirtualMachineCase(testCase, includeMessageDiff: false);
            if (outcome.Passed)
            {
                result.Passed++;
                level.Passed++;
                suite.Passed++;
                continue;
            }

            switch (outcome.Status)
            {
                case NewVmConformanceStatus.CompileFailure:
                    result.CompileFailures++;
                    level.CompileFailures++;
                    suite.CompileFailures++;
                    break;
                case NewVmConformanceStatus.RuntimeFailure:
                    result.RuntimeFailures++;
                    level.RuntimeFailures++;
                    suite.RuntimeFailures++;
                    break;
                default:
                    result.Mismatches++;
                    level.Mismatches++;
                    suite.Mismatches++;
                    break;
            }

            AddSample(samples, testCase, outcome.Status.ToString(), outcome.Detail);
        }

        TestContext.WriteLine(
            "New VM conformance (strict): " +
            $"{result.PassRate:P1} reached ({result.Passed}/{result.Attempted} targeted runtime scriptApi cases passed). " +
            $"allCases={result.TotalCases}, targeted={result.TargetedCases}, skippedNonRuntime={result.SkippedNonRuntimeCases}, " +
            $"mismatches={result.Mismatches}, compileFailures={result.CompileFailures}, runtimeFailures={result.RuntimeFailures}");
        foreach (var pair in byLevel)
        {
            var level = pair.Value;
            TestContext.WriteLine(
                $"  {pair.Key}: {level.PassRate:P1} ({level.Passed}/{level.Attempted}), " +
                $"mismatches={level.Mismatches}, compileFailures={level.CompileFailures}, runtimeFailures={level.RuntimeFailures}");
        }

        foreach (var pair in bySuite)
        {
            var suite = pair.Value;
            TestContext.WriteLine(
                $"  {pair.Key}: {suite.PassRate:P1} ({suite.Passed}/{suite.Attempted}), " +
                $"mismatches={suite.Mismatches}, compileFailures={suite.CompileFailures}, runtimeFailures={suite.RuntimeFailures}");
        }

        foreach (var sample in samples)
        {
            TestContext.WriteLine(sample);
        }

        Assert.IsGreaterThan(0, result.Attempted);
        if (result.Failed > 0)
        {
            Assert.Fail(
                $"New VM strict conformance failed: {result.Failed}/{result.Attempted} targeted runtime case(s) failed. " +
                $"Pass rate: {result.PassRate:P1}.{Environment.NewLine}{string.Join(Environment.NewLine, samples)}");
        }
    }
}

[TestClass]
public sealed class GameEventScriptJsonPerformanceTests : GameEventScriptJsonConformanceTestBase
{
    private static readonly bool RunPerformanceReport = true;
    private static readonly int PerformanceIterations = 1_000;
    private static readonly int PerformanceWarmupIterations = 10;
    private const int DefaultIterations = 1_000;
    private const int DefaultWarmupIterations = 100;

    [TestMethod]
    [TestCategory("Performance")]
    public void PerformanceReport()
    {
        var testCases = GameEventScriptConformanceRunner.AllConformanceCases(Path.Combine(SpecDirectory, "performance"));
        Assert.IsGreaterThan(0, testCases.Count);

        if (!RunPerformanceReport)
        {
            TestContext.WriteLine(
                "Performance report is disabled. Set RunPerformanceReport=true in this test to execute it manually. " +
                "Use PerformanceIterations and PerformanceWarmupIterations in this test to override JSON iteration counts. " +
                $"Loaded {testCases.Count} performance case(s).");
            return;
        }

        foreach (var testCase in testCases)
        {
            RunPerformanceCase(testCase);
        }
    }

    private void RunPerformanceCase(GameEventScriptConformanceCase testCase)
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
        var oldBuild = Measure<IGameEventScriptModule>("old vm build", () => GesBytecodeVmExecutableBuilder.Build(compiled.Value));
        var newBuild = Measure<IGameEventScriptModule>("new vm build", () => GameEventScriptVirtualMaschine.Create(compiled.Value.ToGameEventScriptBinary(), 4096, 256));

        AssertPerformanceCorrectness(testCase, "old vm", oldBuild.Value);
        AssertPerformanceCorrectness(testCase, "new vm", newBuild.Value);

        var oldRun = MeasurePerformanceRun(testCase, oldBuild.Value, iterations, warmupIterations);
        var newRun = MeasurePerformanceRun(testCase, newBuild.Value, iterations, warmupIterations);

        TestContext.WriteLine($"Performance: {testCase.SuiteName}/{testCase.Test.Name}");
        TestContext.WriteLine($"  iterations={iterations}, warmupIterations={warmupIterations}, steps={testCase.Test.Steps.Count}");
        WriteMeasured("compile", compiled);
        WriteMeasured("old build", oldBuild);
        WriteMeasured("new build", newBuild);
        WriteRun("old run", oldRun, iterations);
        WriteRun("new run", newRun, iterations);
        TestContext.WriteLine($"  speedup={(oldRun.Elapsed.TotalMilliseconds / Math.Max(0.0001d, newRun.Elapsed.TotalMilliseconds)):0.00}x");
        TestContext.WriteLine($"  allocationRatio={(oldRun.AllocatedBytes / (double)Math.Max(1L, newRun.AllocatedBytes)):0.00}x");
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

    private void WriteMeasured<T>(string label, Measured<T> measured)
        => TestContext.WriteLine(
            $"  {label}: {measured.Elapsed.TotalMilliseconds,9:#,##0.000} ms / {FormatBytes(measured.AllocatedBytes),9}");

    private void WriteRun(string label, PerformanceRunMetrics run, int iterations)
        => TestContext.WriteLine(
            $"  {label}: {run.Elapsed.TotalMilliseconds,9:#,##0.000} ms / {FormatBytes(run.AllocatedBytes),9} " +
            $"(per invoke {run.Elapsed.TotalMilliseconds / Math.Max(1, iterations):0.0000} ms / {FormatBytes(run.AllocatedBytes / Math.Max(1, iterations))}, " +
            $"emitted={run.EmittedMessages}, outbound={run.OutboundPublishedMessages})");

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

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicMathMatrixCases()
        => NewVirtualMachineCases("runtime/atomic/math-matrix.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicEqualityCases()
        => NewVirtualMachineCases("runtime/atomic/equality.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicCompareCases()
        => NewVirtualMachineCases("runtime/atomic/compare.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicExternalAccessCases()
        => NewVirtualMachineCases("runtime/atomic/external-access.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicMemberIndexAccessCases()
        => NewVirtualMachineCases("runtime/atomic/member-index-access.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicBooleanLogicCases()
        => NewVirtualMachineCases("runtime/atomic/boolean-logic.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicCreateValuesCases()
        => NewVirtualMachineCases("runtime/atomic/create-values.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicCastsCases()
        => NewVirtualMachineCases("runtime/atomic/casts.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicCustomTypesCases()
        => NewVirtualMachineCases("runtime/atomic/custom-types.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicCollectionOperatorsCases()
        => NewVirtualMachineCases("runtime/atomic/collection-operators.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicControlFlowCases()
        => NewVirtualMachineCases("runtime/atomic/control-flow.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicSeriesCases()
        => NewVirtualMachineCases("runtime/atomic/series.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicStreamCoreCases()
        => NewVirtualMachineCases("runtime/atomic/stream-core.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicStreamTerminalsCases()
        => NewVirtualMachineCases("runtime/atomic/stream-terminals.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicSortGroupDistinctCases()
        => NewVirtualMachineCases("runtime/atomic/sort-group-distinct.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicShuffleReverseCases()
        => NewVirtualMachineCases("runtime/atomic/shuffle-reverse.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicPatternsCases()
        => NewVirtualMachineCases("runtime/atomic/patterns.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicRandomCases()
        => NewVirtualMachineCases("runtime/atomic/random.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeCollectionsCases()
        => NewVirtualMachineCases("runtime/collections.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeControlFlowCases()
        => NewVirtualMachineCases("runtime/control-flow.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeExtensionsSequencesCases()
        => NewVirtualMachineCases("runtime/extensions-sequences.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeHostDispatchCases()
        => NewVirtualMachineCases("runtime/host-dispatch.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeMessagesHandlersCases()
        => NewVirtualMachineCases("runtime/messages-handlers.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimePredicatesFunctionsCases()
        => NewVirtualMachineCases("runtime/predicates-functions.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimePublishTagsCases()
        => NewVirtualMachineCases("runtime/publish-tags.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeRandomDiceRangesCases()
        => NewVirtualMachineCases("runtime/random-dice-ranges.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeTypesAndValuesCases()
        => NewVirtualMachineCases("runtime/types-and-values.json");

    protected static string FormatConformanceCaseDisplayName(MethodInfo methodInfo, object[] data)
        => data is [GameEventScriptConformanceCase testCase]
            ? testCase.Test.Name ?? testCase.ToString()
            : methodInfo.Name;

    protected static void RunJsonConformanceCase(GameEventScriptConformanceCase testCase)
        => GameEventScriptConformanceRunner.RunCase(testCase);

    protected void RunNewVirtualMachineConformanceCase(GameEventScriptConformanceCase testCase)
    {
        var outcome = RunNewVirtualMachineCase(testCase);
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

    protected static NewVmConformanceOutcome RunNewVirtualMachineCase(
        GameEventScriptConformanceCase testCase,
        bool includeMessageDiff = true)
    {
        GameEventScriptBinary binary;
        try
        {
            binary = GameEventScriptConformanceRunner.CompileBytecodeForTest(testCase.Test).ToGameEventScriptBinary();
        }
        catch (Exception exception)
        {
            return NewVmConformanceOutcome.CompileFailure(exception.Message);
        }

        try
        {
            return RunNewVirtualMachineCase(testCase, binary, includeMessageDiff, out var mismatch, out var debugDump)
                ? NewVmConformanceOutcome.Pass()
                : NewVmConformanceOutcome.Mismatch(mismatch, debugDump);
        }
        catch (Exception exception)
        {
            return NewVmConformanceOutcome.RuntimeFailure(exception.Message);
        }
    }

    protected static NewVmConformanceSuiteResult GetSuiteResult(
        SortedDictionary<string, NewVmConformanceSuiteResult> suites,
        string suiteName)
    {
        if (!suites.TryGetValue(suiteName, out var result))
        {
            result = new NewVmConformanceSuiteResult();
            suites.Add(suiteName, result);
        }

        return result;
    }

    protected static void AddSample(List<string> samples, GameEventScriptConformanceCase testCase, string kind, string detail)
    {
        if (samples.Count >= 30)
        {
            return;
        }

        samples.Add($"{kind}: {testCase}: {detail}");
    }

    private static string? GetScriptSourceForDump(GameEventScriptConformanceCase testCase)
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

    private static IEnumerable<object[]> NewVirtualMachineCases(string relativeSpecFile)
        => GameEventScriptConformanceRunner.AllConformanceCases(SpecDirectory, relativeSpecFile)
            .Where(testCase => string.Equals(testCase.Test.Kind, "scriptApi", StringComparison.OrdinalIgnoreCase))
            .Select(testCase => new object[] { testCase });

    private static string GetSourceDirectory([CallerFilePath] string sourceFile = "")
        => Path.GetDirectoryName(sourceFile)!;

    private static bool RunNewVirtualMachineCase(
        GameEventScriptConformanceCase testCase,
        GameEventScriptBinary binary,
        bool includeMessageDiff,
        out string mismatch,
        out string debugDump)
    {
        mismatch = string.Empty;
        debugDump = string.Empty;
        var test = testCase.Test;
        if (test.Steps is null || test.Steps.Count == 0)
        {
            mismatch = "scriptApi tests require at least one step.";
            return false;
        }

        var random = GameEventScriptConformanceRunner.CreateRandomForTest(test.RandomSequence);
        var runtimeLimits = GameEventScriptConformanceRunner.CreateRuntimeLimitsForTest(test.RuntimeLimits);
        var emitted = new List<GameEventScriptMessage>();
        var published = new List<GameEventScriptMessage>();
        var observedRuntimeLimits = new List<TestRuntimeLimitEvent>();
        var scriptSource = GetScriptSourceForDump(testCase);
        var vmModule = (GameEventScriptVirtualMaschine)GameEventScriptManager.CreateModuleNewVm(binary, 4096, 256);
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

        for (var stepIndex = 0; stepIndex < test.Steps.Count; stepIndex++)
        {
            var step = test.Steps[stepIndex];
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
        builder.AppendLine($"Step: {stepIndex + 1}");
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

    protected sealed class NewVmConformanceResult(int totalCases, int targetedCases, int skippedNonRuntimeCases)
    {
        public int TotalCases { get; } = totalCases;
        public int TargetedCases { get; } = targetedCases;
        public int SkippedNonRuntimeCases { get; } = skippedNonRuntimeCases;
        public int Attempted { get; set; }
        public int Passed { get; set; }
        public int Mismatches { get; set; }
        public int CompileFailures { get; set; }
        public int RuntimeFailures { get; set; }
        public int Failed => Mismatches + CompileFailures + RuntimeFailures;
        public double PassRate => Attempted == 0 ? 0d : (double)Passed / Attempted;
    }

    protected sealed class NewVmConformanceSuiteResult
    {
        public int Attempted { get; set; }
        public int Passed { get; set; }
        public int Mismatches { get; set; }
        public int CompileFailures { get; set; }
        public int RuntimeFailures { get; set; }
        public double PassRate => Attempted == 0 ? 0d : (double)Passed / Attempted;
    }

    protected enum NewVmConformanceStatus
    {
        Passed,
        Mismatch,
        CompileFailure,
        RuntimeFailure
    }

    protected sealed record NewVmConformanceOutcome(
        NewVmConformanceStatus Status,
        string Detail,
        string DebugDump = "")
    {
        public bool Passed => Status == NewVmConformanceStatus.Passed;

        public static NewVmConformanceOutcome Pass()
            => new(NewVmConformanceStatus.Passed, "passed");

        public static NewVmConformanceOutcome Mismatch(string detail, string debugDump)
            => new(NewVmConformanceStatus.Mismatch, detail, debugDump);

        public static NewVmConformanceOutcome CompileFailure(string detail)
            => new(NewVmConformanceStatus.CompileFailure, detail);

        public static NewVmConformanceOutcome RuntimeFailure(string detail)
            => new(NewVmConformanceStatus.RuntimeFailure, detail);
    }
}
