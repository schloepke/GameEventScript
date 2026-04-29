using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using StepH.Flow.EventScript.RegisterVM;
using StepH.Flow.EventScript.Runtime;

namespace StepH_Flow_Tests.EventScript.Conformance;

[TestClass]
public sealed class RegisterVmNoFallbackConformanceTests
{
    private static readonly string SpecDirectory = Path.Combine(GetSourceDirectory(), "Specs");

    private static readonly HashSet<string> GatedCaseIds = new(StringComparer.Ordinal)
    {
        "RuntimeControlFlow/simple publish",
        "RuntimeControlFlow/standalone and trailing comments are ignored during execution",
        "RuntimeControlFlow/semicolons and continued expressions preserve behavior",
        "RuntimeControlFlow/published messages are dispatched through the same host queue",
        "RuntimeControlFlow/processing limit stops event loops",
        "RuntimeControlFlow/if and for single statements run without blocks",
        "RuntimeControlFlow/nested block scopes may shadow outer variables",
        "RuntimeControlFlow/block scopes do not leak variables",
        "RuntimeMessagesHandlers/handler binding creates message values and publishable calls",
        "RuntimeMessagesHandlers/direct message literal creates first class message value",
        "RuntimeMessagesHandlers/empty uppercase invocation creates handler value",
        "RuntimeMessagesHandlers/untyped handler literal and bind create first class values",
        "RuntimeMessagesHandlers/invalid handler binding and non message publish are lenient no ops",
        "RuntimeMessagesHandlers/message and handler values expose members without becoming dictionaries",
        "RuntimeRandomDiceRanges/sequential steps share random sequence",
        "RuntimeRandomDiceRanges/random can produce integer decimal and mixed values",
        "RuntimeRandomDiceRanges/random upper bound keeps arithmetic together",
        "RuntimeRandomDiceRanges/range direct lookup is not capped by materialization limit",
        "RuntimeRandomDiceRanges/range limit diagnostics",
        "RuntimeRandomDiceRanges/loop budget stops run after published iterations",
        "RuntimeRandomDiceRanges/dice limit diagnostics",
        "RuntimeRulesSelects/rules coerce to boolean while selects keep original value",
        "RuntimeTypesAndValues/percentage arithmetic keeps ratios and applies relative bases",
        "RuntimeTypesAndValues/percentage arithmetic preserves compatible decimal units",
        "RuntimeTypesAndValues/degree literals preserve raw angles and wrap explicitly",
        "RuntimeTypesAndValues/degree arithmetic percentages helpers and comparisons",
        "RuntimeTypesAndValues/decimal unit literals casts and strict arithmetic",
        "RuntimeTypesAndValues/inline literals escaped quotes and numeric helpers",
        "RuntimeTypesAndValues/lookup and empty checks are runtime-visible",
        "RuntimeTypesAndValues/decimal edge values stay observable",
        "RuntimeTypesAndValues/value checks defaults and optionals",
        "RuntimeTypesAndValues/type conversions length checks and type tags",
        "RuntimeTypesAndValues/missing invalid and overflowing values remain lenient"
    };

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> RegisterVmNoFallbackGatedCases()
    {
        foreach (var testCase in EventScriptConformanceRunner.EnumerateRegisterVmScriptApiCases(SpecDirectory))
        {
            if (GatedCaseIds.Contains(EventScriptConformanceRunner.GetCaseId(testCase)))
            {
                yield return [testCase];
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(RegisterVmNoFallbackGatedCases))]
    public void RegisterVmNoFallbackGatedConformanceCasePasses(EventScriptConformanceCase testCase)
        => RunNoFallback(testCase);

    [TestMethod]
    public void RegisterVmNoFallbackReportListsRemainingFallbacks()
    {
        var cases = EventScriptConformanceRunner.EnumerateRegisterVmScriptApiCases(SpecDirectory).ToArray();
        var fallbackFailures = new List<string>();
        var behaviorFailures = new List<string>();
        var unexpectedFailures = new List<string>();
        var passed = 0;

        foreach (var testCase in cases)
        {
            try
            {
                RunNoFallback(testCase);
                passed++;
            }
            catch (RegisterVmFallbackException exception)
            {
                fallbackFailures.Add($"{EventScriptConformanceRunner.GetCaseId(testCase)}: {exception.Reason}");
            }
            catch (AssertFailedException exception)
            {
                behaviorFailures.Add($"{EventScriptConformanceRunner.GetCaseId(testCase)}: {FirstLine(exception.Message)}");
            }
            catch (Exception exception)
            {
                unexpectedFailures.Add($"{EventScriptConformanceRunner.GetCaseId(testCase)}: {exception.GetType().Name}: {FirstLine(exception.Message)}");
            }
        }

        TestContext.WriteLine(
            "RegisterVM no-fallback report: {0}/{1} scriptApi cases pass without fallback.",
            passed,
            cases.Length);
        TestContext.WriteLine("Gated no-fallback cases: {0}", GatedCaseIds.Count);
        WriteSection("Fallbacks", fallbackFailures);
        WriteSection("Behavior differences without fallback", behaviorFailures);
        WriteSection("Unexpected failures", unexpectedFailures);
    }

    private static void RunNoFallback(EventScriptConformanceCase testCase)
        => EventScriptConformanceRunner.RunScriptApiTest(testCase, CompileRegisterVmNoFallback);

    private static IEventScriptMessageHandlerCollection CompileRegisterVmNoFallback(
        EventScriptConformanceTest test,
        string engine)
        => EventScriptConformanceRunner.CompileScripts(test, engine, RegisterVmFallbackMode.Throw);

    private void WriteSection(string title, IReadOnlyList<string> entries)
    {
        TestContext.WriteLine("{0}: {1}", title, entries.Count);
        foreach (var entry in entries)
        {
            TestContext.WriteLine("  {0}", entry);
        }
    }

    private static string FirstLine(string? message)
        => string.IsNullOrEmpty(message)
            ? string.Empty
            : message.Split('\n')[0].TrimEnd('\r');

    private static string GetSourceDirectory([CallerFilePath] string sourceFile = "")
        => Path.GetDirectoryName(sourceFile)!;
}
