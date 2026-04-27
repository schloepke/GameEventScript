using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Semantics;
using StepH.Flow.EventScript.Types;

namespace StepH_Flow_Tests.EventScript.Conformance;

[TestClass]
public sealed class EventScriptJsonConformanceTests
{
    private static readonly string SpecDirectory = Path.Combine(GetSourceDirectory(), "Specs");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static IEnumerable<object[]> ConformanceCases()
    {
        foreach (var file in Directory.EnumerateFiles(SpecDirectory, "*.json").OrderBy(path => path, StringComparer.Ordinal))
        {
            var suite = LoadSuite(file);
            foreach (var test in suite.Tests)
            {
                ValidateRequired(test.Kind, "test kind", file, suite.Name, test.Name);
                ValidateRequired(test.Name, "test name", file, suite.Name, test.Name);
                yield return [new EventScriptConformanceCase(file, suite.Name!, test)];
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(ConformanceCases))]
    public void JsonConformanceCasePasses(EventScriptConformanceCase testCase)
    {
        switch (testCase.Test.Kind)
        {
            case "scriptApi":
                RunScriptApiTest(testCase);
                break;
            case "compileError":
                RunCompileErrorTest(testCase);
                break;
            case "valueSemantics":
                RunValueSemanticsTest(testCase);
                break;
            default:
                Assert.Fail($"{testCase}: unsupported test kind '{testCase.Test.Kind}'.");
                break;
        }
    }

    private static void RunScriptApiTest(EventScriptConformanceCase testCase)
    {
        var test = testCase.Test;
        var compiled = CompileScripts(test);
        var collector = new EventScriptDiagnosticTraceCollector();
        var published = new List<EventScriptMessage>();
        var builder = EventScriptHost.CreateBuilder()
            .WithRandom(CreateRandom(test.RandomSequence))
            .WithRuntimeLimits(CreateRuntimeLimits(test.RuntimeLimits))
            .WithDiagnosticCollector(collector)
            .WithPublishedMessageObserver(published.Add);

        if (test.MaxProcessedEventsPerRun.HasValue)
        {
            builder.WithMaxProcessedEventsPerRun(test.MaxProcessedEventsPerRun.Value);
        }

        var host = builder.Build().Load(compiled);
        if (test.Steps is null || test.Steps.Count == 0)
        {
            Assert.Fail($"{testCase}: scriptApi tests require at least one step.");
        }

        for (var stepIndex = 0; stepIndex < test.Steps.Count; stepIndex++)
        {
            var step = test.Steps[stepIndex];
            var diagnosticsStart = collector.Events.Count;
            published.Clear();

            host.Publish(EventScriptConformanceValueCodec.DecodeMessage(RequireDefined(step.Input, "step input", testCase)));

            AssertPublishedMessages(testCase, stepIndex, step.ExpectedPublished, published);
            AssertDiagnostics(testCase, stepIndex, step.ExpectedDiagnostics, collector.Events.Skip(diagnosticsStart).ToArray());
        }
    }

    private static void RunCompileErrorTest(EventScriptConformanceCase testCase)
    {
        var expected = testCase.Test.ExpectedError;
        if (expected is null)
        {
            Assert.Fail($"{testCase}: compileError tests require expectedError.");
        }

        try
        {
            CompileScripts(testCase.Test);
        }
        catch (EventScriptSyntaxException exception)
        {
            AssertSyntaxError(testCase, expected, exception);
            return;
        }
        catch (EventScriptLinkageException exception)
        {
            AssertLinkageError(testCase, expected, exception);
            return;
        }
        catch (EventScriptCompilationException exception)
        {
            AssertGenericCompilationError(testCase, expected, exception);
            return;
        }

        Assert.Fail($"{testCase}: expected compilation to fail.");
    }

    private static void RunValueSemanticsTest(EventScriptConformanceCase testCase)
    {
        var test = testCase.Test;
        ValidateRequired(test.Operation, "valueSemantics operation", testCase.SuiteFile, testCase.SuiteName, test.Name);
        var result = ExecuteValueSemanticsOperation(testCase);
        AssertExpectedResult(testCase, test.Expected, result);
    }

    private static object ExecuteValueSemanticsOperation(EventScriptConformanceCase testCase)
    {
        var test = testCase.Test;
        var operation = test.Operation!.Trim().ToLowerInvariant();

        return operation switch
        {
            "lookup" => EventScriptValueSemantics.Lookup(DecodeRequiredValue(test.Target, "target", testCase), DecodeRequiredValue(test.Selector, "selector", testCase)),
            "hasvalue" => EventScriptValueSemantics.HasValue(DecodePrimaryValue(testCase)),
            "isempty" => EventScriptValueSemantics.IsEmpty(DecodePrimaryValue(testCase)),
            "contains" => EventScriptValueSemantics.Contains(DecodeRequiredValue(test.Target, "target", testCase), DecodeRequiredValue(test.Value, "value", testCase)),
            "containsvalue" => EventScriptValueSemantics.ContainsValue(DecodeRequiredValue(test.Target, "target", testCase), DecodeRequiredValue(test.Value, "value", testCase)),
            "startswith" => EventScriptValueSemantics.StartsWith(DecodeRequiredValue(test.Target, "target", testCase), DecodeRequiredValue(test.Value, "value", testCase)),
            "endswith" => EventScriptValueSemantics.EndsWith(DecodeRequiredValue(test.Target, "target", testCase), DecodeRequiredValue(test.Value, "value", testCase)),
            "containsall" => ContainsAll(testCase),
            "containsany" => ContainsAny(testCase),
            "sort" => Sort(testCase),
            "distinct" => Distinct(testCase),
            _ => throw new InvalidOperationException($"{testCase}: unsupported valueSemantics operation '{test.Operation}'.")
        };
    }

    private static bool ContainsAll(EventScriptConformanceCase testCase)
    {
        var target = DecodeRequiredValue(testCase.Test.Target, "target", testCase);
        return EventScriptCollectionSemantics.ContainsAll(target, target.AsList(), DecodeRequiredValue(testCase.Test.Value, "value", testCase));
    }

    private static bool ContainsAny(EventScriptConformanceCase testCase)
    {
        var target = DecodeRequiredValue(testCase.Test.Target, "target", testCase);
        return EventScriptCollectionSemantics.ContainsAny(target, target.AsList(), DecodeRequiredValue(testCase.Test.Value, "value", testCase));
    }

    private static EventScriptValue Sort(EventScriptConformanceCase testCase)
    {
        var target = DecodeRequiredValue(testCase.Test.Target, "target", testCase);
        return EventScriptCollectionSemantics.Sort(target, target.AsEnumerable(), testCase.Test.Direction ?? "ascending");
    }

    private static EventScriptValue Distinct(EventScriptConformanceCase testCase)
    {
        var target = DecodeRequiredValue(testCase.Test.Target, "target", testCase);
        return EventScriptCollectionSemantics.Distinct(target, target.AsEnumerable());
    }

    private static void AssertPublishedMessages(
        EventScriptConformanceCase testCase,
        int stepIndex,
        IReadOnlyList<JsonElement>? expectedPublished,
        IReadOnlyList<EventScriptMessage> actual)
    {
        var expected = (expectedPublished ?? []).Select(EventScriptConformanceValueCodec.DecodeMessage).ToArray();
        var expectedJson = EventScriptConformanceValueCodec.ToCanonicalJson(expected);
        var actualJson = EventScriptConformanceValueCodec.ToCanonicalJson(actual);
        if (expectedJson == actualJson)
        {
            return;
        }

        Assert.Fail(
            $"{testCase} step {stepIndex + 1}: published messages differ.{Environment.NewLine}" +
            $"Expected:{Environment.NewLine}{EventScriptConformanceValueCodec.ToPrettyJson(expected)}{Environment.NewLine}" +
            $"Actual:{Environment.NewLine}{EventScriptConformanceValueCodec.ToPrettyJson(actual)}");
    }

    private static void AssertDiagnostics(
        EventScriptConformanceCase testCase,
        int stepIndex,
        IReadOnlyList<EventScriptDiagnosticExpectationSpec>? expectedDiagnostics,
        IReadOnlyList<EventScriptDiagnosticEvent> actual)
    {
        if (expectedDiagnostics is null || expectedDiagnostics.Count == 0)
        {
            return;
        }

        var nextStart = 0;
        foreach (var expected in expectedDiagnostics)
        {
            var foundIndex = -1;
            for (var i = nextStart; i < actual.Count; i++)
            {
                if (DiagnosticMatches(expected, actual[i]))
                {
                    foundIndex = i;
                    break;
                }
            }

            if (foundIndex < 0)
            {
                Assert.Fail(
                    $"{testCase} step {stepIndex + 1}: expected diagnostic was not found in order: {DescribeDiagnosticExpectation(expected)}.{Environment.NewLine}" +
                    $"Actual diagnostics:{Environment.NewLine}{DescribeDiagnostics(actual)}");
            }

            nextStart = foundIndex + 1;
        }
    }

    private static bool DiagnosticMatches(EventScriptDiagnosticExpectationSpec expected, EventScriptDiagnosticEvent actual)
    {
        if (!string.IsNullOrWhiteSpace(expected.Kind) &&
            !string.Equals(expected.Kind, actual.Kind.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(expected.Name) &&
            !string.Equals(expected.Name, actual.Name, StringComparison.Ordinal))
        {
            return false;
        }

        var detailContains = expected.DetailContains ?? expected.MessageContains;
        return string.IsNullOrEmpty(detailContains) ||
               (actual.Detail?.Contains(detailContains, StringComparison.Ordinal) ?? false);
    }

    private static void AssertExpectedResult(EventScriptConformanceCase testCase, JsonElement expected, object actual)
    {
        RequireDefined(expected, "expected", testCase);

        switch (actual)
        {
            case bool actualBoolean:
                AssertExpectedBoolean(testCase, expected, actualBoolean);
                break;
            case EventScriptValue actualValue:
                AssertExpectedValue(testCase, expected, actualValue);
                break;
            default:
                Assert.Fail($"{testCase}: unsupported valueSemantics result type '{actual.GetType().Name}'.");
                break;
        }
    }

    private static void AssertExpectedBoolean(EventScriptConformanceCase testCase, JsonElement expected, bool actual)
    {
        if (expected.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            Assert.AreEqual(expected.GetBoolean(), actual, $"{testCase}: boolean result differs.");
            return;
        }

        AssertExpectedValue(testCase, expected, EventScriptValue.Boolean(actual));
    }

    private static void AssertExpectedValue(EventScriptConformanceCase testCase, JsonElement expected, EventScriptValue actual)
    {
        var expectedValue = expected.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? EventScriptValue.Boolean(expected.GetBoolean())
            : EventScriptConformanceValueCodec.DecodeValue(expected);

        if (expectedValue.Equals(actual))
        {
            return;
        }

        Assert.Fail(
            $"{testCase}: value result differs.{Environment.NewLine}" +
            $"Expected:{Environment.NewLine}{EventScriptConformanceValueCodec.ToPrettyJson(expectedValue)}{Environment.NewLine}" +
            $"Actual:{Environment.NewLine}{EventScriptConformanceValueCodec.ToPrettyJson(actual)}");
    }

    private static void AssertSyntaxError(
        EventScriptConformanceCase testCase,
        EventScriptExpectedCompileErrorSpec expected,
        EventScriptSyntaxException exception)
    {
        if (!PhaseMatches(expected, "syntax"))
        {
            Assert.Fail($"{testCase}: expected phase '{expected.Phase}', but got syntax error: {exception.Message}");
        }

        if (exception.Errors.Any(error =>
                Matches(expected.Kind, error.Kind.ToString()) &&
                Matches(expected.ModuleName, error.ModuleName) &&
                MessageMatches(expected.MessageContains, error.Message, exception.Message)))
        {
            return;
        }

        Assert.Fail($"{testCase}: syntax error expectation did not match.{Environment.NewLine}{exception.Message}");
    }

    private static void AssertLinkageError(
        EventScriptConformanceCase testCase,
        EventScriptExpectedCompileErrorSpec expected,
        EventScriptLinkageException exception)
    {
        if (!PhaseMatches(expected, "linkage"))
        {
            Assert.Fail($"{testCase}: expected phase '{expected.Phase}', but got linkage error: {exception.Message}");
        }

        if (exception.Errors.Any(error =>
                Matches(expected.Kind, error.Kind.ToString()) &&
                Matches(expected.Symbol, error.Symbol) &&
                Matches(expected.SymbolKind, error.SymbolKind.ToString()) &&
                Matches(expected.ModuleName, error.ModuleName) &&
                MessageMatches(expected.MessageContains, error.Message, exception.Message)))
        {
            return;
        }

        Assert.Fail($"{testCase}: linkage error expectation did not match.{Environment.NewLine}{exception.Message}");
    }

    private static void AssertGenericCompilationError(
        EventScriptConformanceCase testCase,
        EventScriptExpectedCompileErrorSpec expected,
        EventScriptCompilationException exception)
    {
        if (!PhaseMatches(expected, "compilation") || !MessageMatches(expected.MessageContains, exception.Message))
        {
            Assert.Fail($"{testCase}: generic compilation error expectation did not match.{Environment.NewLine}{exception.Message}");
        }
    }

    private static CompiledEventScript CompileScripts(EventScriptConformanceTest test)
    {
        var modules = GetSources(test)
            .Select(source => EventScriptManager.ParseModule(source.Text!, source.SourceName))
            .ToArray();

        return new CompiledEventScript(EventScriptManager.LinkModules(modules));
    }

    private static IEnumerable<EventScriptSourceSpec> GetSources(EventScriptConformanceTest test)
    {
        if (test.Scripts is { Count: > 0 })
        {
            foreach (var source in test.Scripts)
            {
                if (string.IsNullOrWhiteSpace(source.Text))
                {
                    throw new InvalidOperationException($"Test '{test.Name}' has a script source without text.");
                }

                yield return new EventScriptSourceSpec
                {
                    SourceName = string.IsNullOrWhiteSpace(source.SourceName) ? $"{test.Name}.es" : source.SourceName,
                    Text = source.Text
                };
            }

            yield break;
        }

        if (!string.IsNullOrWhiteSpace(test.Script))
        {
            yield return new EventScriptSourceSpec
            {
                SourceName = $"{test.Name}.es",
                Text = test.Script
            };
            yield break;
        }

        throw new InvalidOperationException($"Test '{test.Name}' requires script or scripts.");
    }

    private static EventScriptRandomGenerator CreateRandom(IReadOnlyList<string>? randomSequence)
    {
        if (randomSequence is null || randomSequence.Count == 0)
        {
            return EventScriptRandomGenerator.Create();
        }

        return EventScriptRandomGenerator.FromSequence(randomSequence.Select(value => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture)).ToArray());
    }

    private static EventScriptRuntimeLimits CreateRuntimeLimits(EventScriptRuntimeLimitsSpec? spec)
    {
        var defaults = EventScriptRuntimeLimits.Default;
        if (spec is null)
        {
            return defaults;
        }

        return new EventScriptRuntimeLimits
        {
            MaxExecutionSteps = spec.MaxExecutionSteps ?? defaults.MaxExecutionSteps,
            MaxLoopIterations = spec.MaxLoopIterations ?? defaults.MaxLoopIterations,
            MaxCallDepth = spec.MaxCallDepth ?? defaults.MaxCallDepth,
            MaxRangeItems = spec.MaxRangeItems ?? defaults.MaxRangeItems,
            MaxGeneratedCollectionItems = spec.MaxGeneratedCollectionItems ?? defaults.MaxGeneratedCollectionItems,
            MaxDiceCount = spec.MaxDiceCount ?? defaults.MaxDiceCount,
            MaxDiceSides = spec.MaxDiceSides ?? defaults.MaxDiceSides
        };
    }

    private static EventScriptValue DecodePrimaryValue(EventScriptConformanceCase testCase)
        => testCase.Test.Value.ValueKind == JsonValueKind.Undefined
            ? DecodeRequiredValue(testCase.Test.Target, "target", testCase)
            : EventScriptConformanceValueCodec.DecodeValue(testCase.Test.Value);

    private static EventScriptValue DecodeRequiredValue(JsonElement element, string name, EventScriptConformanceCase testCase)
        => EventScriptConformanceValueCodec.DecodeValue(RequireDefined(element, name, testCase));

    private static JsonElement RequireDefined(JsonElement element, string name, EventScriptConformanceCase testCase)
    {
        if (element.ValueKind == JsonValueKind.Undefined)
        {
            Assert.Fail($"{testCase}: missing {name}.");
        }

        return element;
    }

    private static EventScriptConformanceSuite LoadSuite(string file)
    {
        var suite = JsonSerializer.Deserialize<EventScriptConformanceSuite>(File.ReadAllText(file), JsonOptions)
                    ?? throw new InvalidOperationException($"Conformance suite '{file}' could not be read.");

        if (suite.Version != 1)
        {
            throw new InvalidOperationException($"Conformance suite '{file}' has unsupported version '{suite.Version}'.");
        }

        ValidateRequired(suite.Name, "suite name", file, suite.Name, null);
        if (suite.Tests.Count == 0)
        {
            throw new InvalidOperationException($"Conformance suite '{file}' contains no tests.");
        }

        return suite;
    }

    private static bool PhaseMatches(EventScriptExpectedCompileErrorSpec expected, string actual)
        => string.IsNullOrWhiteSpace(expected.Phase) ||
           string.Equals(expected.Phase, actual, StringComparison.OrdinalIgnoreCase);

    private static bool Matches(string? expected, string actual)
        => string.IsNullOrWhiteSpace(expected) ||
           string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);

    private static bool MessageMatches(string? expected, params string?[] actualMessages)
        => string.IsNullOrWhiteSpace(expected) ||
           actualMessages.Any(message => message?.Contains(expected, StringComparison.Ordinal) ?? false);

    private static string DescribeDiagnosticExpectation(EventScriptDiagnosticExpectationSpec expected)
        => $"kind={expected.Kind ?? "*"}, name={expected.Name ?? "*"}, detailContains={expected.DetailContains ?? expected.MessageContains ?? "*"}";

    private static string DescribeDiagnostics(IEnumerable<EventScriptDiagnosticEvent> diagnostics)
        => string.Join(Environment.NewLine, diagnostics.Select(diagnostic => $"{diagnostic.Kind} {diagnostic.Name}: {diagnostic.Detail}"));

    private static void ValidateRequired(string? value, string description, string file, string? suite, string? test)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var context = test is null ? suite : $"{suite}/{test}";
        throw new InvalidOperationException($"{file}: missing {description} in {context}.");
    }

    private static string GetSourceDirectory([CallerFilePath] string sourceFile = "")
        => Path.GetDirectoryName(sourceFile)!;
}
