using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.BytecodeExecutor;
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
    [DynamicData(nameof(RuntimeAtomicMathMatrixCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicMathMatrix(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicMessagesHandlersCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicMessagesHandlers(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicBooleanLogicCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicBooleanLogic(GameEventScriptConformanceCase testCase)
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
    [DynamicData(nameof(RuntimeDiagnosticsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeDiagnostics(GameEventScriptConformanceCase testCase)
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
        => RunNewVirtualMachineConformanceCase(testCase, false);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicMessagesHandlersCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicMessagesHandlers(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeAtomicBooleanLogicCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicBooleanLogic(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase, false);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeCollectionsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeCollections(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeControlFlowCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeControlFlow(GameEventScriptConformanceCase testCase)
        => RunNewVirtualMachineConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineRuntimeDiagnosticsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeDiagnostics(GameEventScriptConformanceCase testCase)
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
    public void NewVirtualMachineConformanceSmokeIsSoft()
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
            $"New VM conformance ({(NewVirtualMachineConformanceSoftAssertions ? "soft" : "strict")}): " +
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
        if (!NewVirtualMachineConformanceSoftAssertions && result.Failed > 0)
        {
            Assert.Fail(
                $"New VM strict conformance failed: {result.Failed}/{result.Attempted} targeted runtime case(s) failed. " +
                $"Pass rate: {result.PassRate:P1}.{Environment.NewLine}{string.Join(Environment.NewLine, samples)}");
        }
    }
}

public abstract class GameEventScriptJsonConformanceTestBase
{
    protected const bool NewVirtualMachineConformanceSoftAssertions = true;
    protected static readonly string SpecDirectory = Path.Combine(GetSourceDirectory(), "Specs");

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> ApiMessagesCases()
        => Cases("api/messages.json");

    public static IEnumerable<object[]> CompileBuildErrorsCases()
        => Cases("compile/build-errors.json");

    public static IEnumerable<object[]> CompileSyntaxErrorsCases()
        => Cases("compile/syntax-errors.json");

    public static IEnumerable<object[]> RuntimeAtomicMathMatrixCases()
        => Cases("runtime/atomic/math-matrix.json");

    public static IEnumerable<object[]> RuntimeAtomicMessagesHandlersCases()
        => Cases("runtime/atomic/messages-handlers.json");

    public static IEnumerable<object[]> RuntimeAtomicBooleanLogicCases()
        => Cases("runtime/atomic/boolean-logic.json");

    public static IEnumerable<object[]> RuntimeCollectionsCases()
        => Cases("runtime/collections.json");

    public static IEnumerable<object[]> RuntimeControlFlowCases()
        => Cases("runtime/control-flow.json");

    public static IEnumerable<object[]> RuntimeDiagnosticsCases()
        => Cases("runtime/diagnostics.json");

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

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicMessagesHandlersCases()
        => NewVirtualMachineCases("runtime/atomic/messages-handlers.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeAtomicBooleanLogicCases()
        => NewVirtualMachineCases("runtime/atomic/boolean-logic.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeCollectionsCases()
        => NewVirtualMachineCases("runtime/collections.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeControlFlowCases()
        => NewVirtualMachineCases("runtime/control-flow.json");

    public static IEnumerable<object[]> NewVirtualMachineRuntimeDiagnosticsCases()
        => NewVirtualMachineCases("runtime/diagnostics.json");

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

    protected void RunNewVirtualMachineConformanceCase(GameEventScriptConformanceCase testCase, bool softRun = NewVirtualMachineConformanceSoftAssertions)
    {
        var outcome = RunNewVirtualMachineCase(testCase);
        TestContext.WriteLine($"{outcome.Status}: {testCase}: {outcome.Detail}");
        if (!softRun && !outcome.Passed)
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
            return RunNewVirtualMachineCase(testCase, binary, includeMessageDiff, out var mismatch)
                ? NewVmConformanceOutcome.Pass()
                : NewVmConformanceOutcome.Mismatch(mismatch);
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
        out string mismatch)
    {
        mismatch = string.Empty;
        var test = testCase.Test;
        if (test.Steps is null || test.Steps.Count == 0)
        {
            mismatch = "scriptApi tests require at least one step.";
            return false;
        }

        var random = GameEventScriptConformanceRunner.CreateRandomForTest(test.RandomSequence);
        var runtimeLimits = GameEventScriptConformanceRunner.CreateRuntimeLimitsForTest(test.RuntimeLimits);

        for (var stepIndex = 0; stepIndex < test.Steps.Count; stepIndex++)
        {
            var step = test.Steps[stepIndex];
            var diagnostics = new GameEventScriptDiagnosticTraceCollector();
            var emitted = new List<GameEventScriptMessage>();
            var published = new List<GameEventScriptMessage>();
            var context = new GameEventScriptSession(
                random,
                message =>
                {
                    emitted.Add(message);
                    return true;
                },
                diagnosticCollector: diagnostics,
                runtimeLimits: runtimeLimits,
                extensionRegistry: GameEventScriptConformanceExtensionRegistry.Instance,
                publish: message =>
                {
                    published.Add(message);
                    return true;
                });
            var vm = new GameEventScriptVirtualMaschine(binary, 4096, 256);
            var handled = vm.ExecuteMessage(GameEventScriptConformanceValueCodec.DecodeMessage(step.Input), context);
            if (!handled)
            {
                mismatch = $"step {stepIndex + 1}: handler was not found or could not start.";
                return false;
            }

            if (HasDiagnosticExpectations(step))
            {
                mismatch = $"step {stepIndex + 1}: diagnostic expectations are not implemented by the new VM soft runner yet.";
                return false;
            }

            if (!TryMatchMessages(step.ExpectedPublished, emitted, includeMessageDiff, out var emittedDiff))
            {
                mismatch = $"step {stepIndex + 1}: emitted messages differ.{Environment.NewLine}{emittedDiff}";
                return false;
            }

            if (!TryMatchMessages(step.ExpectedOutboundPublished, published, includeMessageDiff, out var publishedDiff))
            {
                mismatch = $"step {stepIndex + 1}: published messages differ.{Environment.NewLine}{publishedDiff}";
                return false;
            }
        }

        return true;
    }

    private static bool TryMatchMessages(
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
            ? $"Expected:{Environment.NewLine}{GameEventScriptConformanceValueCodec.ToPrettyJson(expected)}{Environment.NewLine}" +
              $"Actual:{Environment.NewLine}{GameEventScriptConformanceValueCodec.ToPrettyJson(actual)}"
            : $"expectedMessages={expected.Length}, actualMessages={actual.Count}";
        return false;
    }

    private static bool HasDiagnosticExpectations(GameEventScriptApiStepSpec step)
        => step.ExpectedDiagnostics is { Count: > 0 } ||
           step.UnexpectedDiagnostics is { Count: > 0 };

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
        string Detail)
    {
        public bool Passed => Status == NewVmConformanceStatus.Passed;

        public static NewVmConformanceOutcome Pass()
            => new(NewVmConformanceStatus.Passed, "passed");

        public static NewVmConformanceOutcome Mismatch(string detail)
            => new(NewVmConformanceStatus.Mismatch, detail);

        public static NewVmConformanceOutcome CompileFailure(string detail)
            => new(NewVmConformanceStatus.CompileFailure, detail);

        public static NewVmConformanceOutcome RuntimeFailure(string detail)
            => new(NewVmConformanceStatus.RuntimeFailure, detail);
    }
}
