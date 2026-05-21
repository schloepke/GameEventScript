using System.Runtime.CompilerServices;
using System.Text.Json;
using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.BytecodeExecutor;

namespace StepH_GameEventScript_Tests.Conformance;

[TestClass]
public sealed class GameEventScriptJsonConformanceTests
{
    private static readonly string SpecDirectory = Path.Combine(GetSourceDirectory(), "Specs");

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> ConformanceCases()
        => GameEventScriptConformanceRunner.ConformanceCases(SpecDirectory);

    [TestMethod]
    [DynamicData(nameof(ConformanceCases))]
    public void JsonConformanceCasePasses(GameEventScriptConformanceCase testCase)
        => GameEventScriptConformanceRunner.RunCase(testCase);

    [TestMethod]
    public void NewVirtualMachineConformanceSmokeIsSoft()
    {
        var testCases = GameEventScriptConformanceRunner.EnumerateBytecodeVmScriptApiCases(SpecDirectory).ToArray();
        var attempted = 0;
        var passed = 0;
        var compileFailures = 0;
        var runtimeFailures = 0;
        var mismatches = 0;
        var samples = new List<string>();

        foreach (var testCase in testCases)
        {
            attempted++;
            GameEventScriptBinary binary;
            try
            {
                binary = GameEventScriptConformanceRunner.CompileBytecodeForTest(testCase.Test).ToGameEventScriptBinary();
            }
            catch (Exception exception)
            {
                compileFailures++;
                AddSample(samples, testCase, "compile", exception.Message);
                continue;
            }

            try
            {
                if (RunNewVirtualMachineCase(testCase, binary, out var mismatch))
                {
                    passed++;
                }
                else
                {
                    mismatches++;
                    AddSample(samples, testCase, "mismatch", mismatch);
                }
            }
            catch (Exception exception)
            {
                runtimeFailures++;
                AddSample(samples, testCase, "runtime", exception.Message);
            }
        }

        TestContext.WriteLine(
            $"New VM soft conformance: attempted={attempted}, passed={passed}, mismatches={mismatches}, compileFailures={compileFailures}, runtimeFailures={runtimeFailures}");
        foreach (var sample in samples)
        {
            TestContext.WriteLine(sample);
        }

        Assert.IsGreaterThan(0, attempted);
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
            var emitted = new List<GameEventScriptMessage>();
            var published = new List<GameEventScriptMessage>();
            var context = new GameEventScriptContext(
                random,
                message =>
                {
                    emitted.Add(message);
                    return true;
                },
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

    private static void AddSample(List<string> samples, GameEventScriptConformanceCase testCase, string kind, string detail)
    {
        if (samples.Count >= 20)
        {
            return;
        }

        samples.Add($"{kind}: {testCase}: {detail}");
    }
}
