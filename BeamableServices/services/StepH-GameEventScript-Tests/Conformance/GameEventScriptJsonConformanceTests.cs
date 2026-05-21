using System.Runtime.CompilerServices;
using System.Text.Json;
using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.BytecodeExecutor;
using StepH.GameEventScript.Runtime;

namespace StepH_GameEventScript_Tests.Conformance;

[TestClass]
public sealed class GameEventScriptJsonConformanceTests
{
    private const bool NewVirtualMachineConformanceSoftAssertions = true;
    private static readonly string SpecDirectory = Path.Combine(GetSourceDirectory(), "Specs");

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> ConformanceCases()
        => GameEventScriptConformanceRunner.ConformanceCases(SpecDirectory);

    public static IEnumerable<object[]> NewVirtualMachineConformanceCases()
        => GameEventScriptConformanceRunner.AllConformanceCases(SpecDirectory)
            .Where(testCase => string.Equals(testCase.Test.Kind, "scriptApi", StringComparison.OrdinalIgnoreCase))
            .Select(testCase => new object[] { testCase });

    [TestMethod]
    [DynamicData(nameof(ConformanceCases))]
    public void JsonConformanceCasePasses(GameEventScriptConformanceCase testCase)
        => GameEventScriptConformanceRunner.RunCase(testCase);

    [TestMethod]
    [DynamicData(nameof(NewVirtualMachineConformanceCases))]
    public void NewVirtualMachineConformanceCase(GameEventScriptConformanceCase testCase)
    {
        var outcome = RunNewVirtualMachineCase(testCase);
        TestContext.WriteLine($"{outcome.Status}: {testCase}: {outcome.Detail}");
        if (!NewVirtualMachineConformanceSoftAssertions && !outcome.Passed)
        {
            Assert.Fail($"{outcome.Status}: {testCase}: {outcome.Detail}");
        }
    }

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
        var bySuite = new SortedDictionary<string, NewVmConformanceSuiteResult>(StringComparer.Ordinal);

        foreach (var testCase in testCases)
        {
            result.Attempted++;
            var suite = GetSuiteResult(bySuite, testCase.SuiteName);
            suite.Attempted++;
            var outcome = RunNewVirtualMachineCase(testCase);
            if (outcome.Passed)
            {
                result.Passed++;
                suite.Passed++;
                continue;
            }

            switch (outcome.Status)
            {
                case NewVmConformanceStatus.CompileFailure:
                    result.CompileFailures++;
                    suite.CompileFailures++;
                    break;
                case NewVmConformanceStatus.RuntimeFailure:
                    result.RuntimeFailures++;
                    suite.RuntimeFailures++;
                    break;
                default:
                    result.Mismatches++;
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

    private static NewVmConformanceOutcome RunNewVirtualMachineCase(GameEventScriptConformanceCase testCase)
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
            return RunNewVirtualMachineCase(testCase, binary, out var mismatch)
                ? NewVmConformanceOutcome.Pass()
                : NewVmConformanceOutcome.Mismatch(mismatch);
        }
        catch (Exception exception)
        {
            return NewVmConformanceOutcome.RuntimeFailure(exception.Message);
        }
    }

    private static string GetSourceDirectory([CallerFilePath] string sourceFile = "")
        => Path.GetDirectoryName(sourceFile)!;

    private static bool RunNewVirtualMachineCase(
        GameEventScriptConformanceCase testCase,
        GameEventScriptBinary binary,
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

            if (!MessagesMatch(step.ExpectedPublished, emitted))
            {
                mismatch = $"step {stepIndex + 1}: emitted messages differ.";
                return false;
            }

            if (!MessagesMatch(step.ExpectedOutboundPublished, published))
            {
                mismatch = $"step {stepIndex + 1}: published messages differ.";
                return false;
            }
        }

        return true;
    }

    private static bool MessagesMatch(IReadOnlyList<JsonElement>? expectedPublished, IReadOnlyList<GameEventScriptMessage> actual)
    {
        var expected = (expectedPublished ?? []).Select(GameEventScriptConformanceValueCodec.DecodeMessage).ToArray();
        return GameEventScriptConformanceValueCodec.ToCanonicalJson(expected) ==
               GameEventScriptConformanceValueCodec.ToCanonicalJson(actual);
    }

    private static bool HasDiagnosticExpectations(GameEventScriptApiStepSpec step)
        => step.ExpectedDiagnostics is { Count: > 0 } ||
           step.UnexpectedDiagnostics is { Count: > 0 };

    private static NewVmConformanceSuiteResult GetSuiteResult(
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

    private static void AddSample(List<string> samples, GameEventScriptConformanceCase testCase, string kind, string detail)
    {
        if (samples.Count >= 30)
        {
            return;
        }

        samples.Add($"{kind}: {testCase}: {detail}");
    }

    private sealed class NewVmConformanceResult(int totalCases, int targetedCases, int skippedNonRuntimeCases)
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

    private sealed class NewVmConformanceSuiteResult
    {
        public int Attempted { get; set; }
        public int Passed { get; set; }
        public int Mismatches { get; set; }
        public int CompileFailures { get; set; }
        public int RuntimeFailures { get; set; }
        public double PassRate => Attempted == 0 ? 0d : (double)Passed / Attempted;
    }

    private enum NewVmConformanceStatus
    {
        Passed,
        Mismatch,
        CompileFailure,
        RuntimeFailure
    }

    private sealed record NewVmConformanceOutcome(
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
