using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Experimental;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.RegisterVM;
using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Types;

namespace StepH_Flow_Tests.EventScript.Conformance;

[TestClass]
public sealed class EventScriptJsonConformanceTests
{
    private const string InterpreterEngine = "interpreter";
    private const string ExperimentalEngine = "experimental";
    private const string RegisterVmEngine = "registervm";
    private static readonly string[] RuntimeEngines = [InterpreterEngine, ExperimentalEngine, RegisterVmEngine];
    private static readonly string SpecDirectory = Path.Combine(GetSourceDirectory(), "Specs");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static IEnumerable<object[]> ConformanceCases()
    {
        foreach (var file in Directory.EnumerateFiles(SpecDirectory, "*.json", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
        {
            var suite = LoadSuite(file);
            foreach (var test in suite.Tests)
            {
                ValidateRequired(test.Kind, "test kind", file, suite.Name, test.Name);
                ValidateRequired(test.Name, "test name", file, suite.Name, test.Name);
                foreach (var engine in GetEngines(test))
                {
                    yield return [new EventScriptConformanceCase(file, suite.Name!, test, engine)];
                }
            }
        }
    }

    private static IReadOnlyList<string?> GetEngines(EventScriptConformanceTest test)
        => UsesRuntimeEngine(test.Kind) ? RuntimeEngines : new string?[] { null };

    private static bool UsesRuntimeEngine(string? kind)
        => string.Equals(kind, "scriptApi", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(kind, "compileError", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(kind, "compileMetadata", StringComparison.OrdinalIgnoreCase);

    private static string RequireEngine(EventScriptConformanceCase testCase)
        => testCase.Engine ?? throw new InvalidOperationException($"{testCase}: test kind requires a conformance engine.");

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
            case "messageApi":
                RunMessageApiTest(testCase);
                break;
            case "compileMetadata":
                RunCompileMetadataTest(testCase);
                break;
            default:
                Assert.Fail($"{testCase}: unsupported test kind '{testCase.Test.Kind}'.");
                break;
        }
    }

    private static void RunScriptApiTest(EventScriptConformanceCase testCase)
    {
        var test = testCase.Test;
        var compiled = CompileScripts(test, RequireEngine(testCase));
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
        RegisterExternalSubscribers(testCase, host);
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
            AssertDiagnostics(testCase, stepIndex, step, collector.Events.Skip(diagnosticsStart).ToArray());
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
            CompileScripts(testCase.Test, RequireEngine(testCase));
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
        catch (EventScriptOpcodeCompilationException exception)
        {
            AssertOpcodeCompilationError(testCase, expected, exception);
            return;
        }
        catch (EventScriptCompilationException exception)
        {
            AssertGenericCompilationError(testCase, expected, exception);
            return;
        }

        Assert.Fail($"{testCase}: expected compilation to fail.");
    }

    private static void RunMessageApiTest(EventScriptConformanceCase testCase)
    {
        var signatureSpec = testCase.Test.Signature
                            ?? throw new InvalidOperationException($"{testCase}: messageApi tests require signature.");

        ValidateRequired(signatureSpec.Name, "messageApi signature name", testCase.SuiteFile, testCase.SuiteName, testCase.Test.Name);
        var signature = new EventScriptMessageSignature(signatureSpec.Name!, signatureSpec.Parameters ?? []);
        var message = EventScriptConformanceValueCodec.DecodeMessage(RequireDefined(testCase.Test.Message, "messageApi message", testCase));

        AssertOptionalEquals(testCase, "signature id", testCase.Test.ExpectedSignatureId, signature.SignatureId);
        AssertOptionalEquals(testCase, "message name", testCase.Test.ExpectedMessageName, message.Name);
        AssertOptionalEquals(testCase, "message signature id", testCase.Test.ExpectedMessageSignatureId, message.SignatureId);

        if (testCase.Test.ExpectedMatches is { } expectedMatches)
        {
            Assert.AreEqual(expectedMatches, signature.Matches(message), $"{testCase}: signature match result differs.");
        }

        if (testCase.Test.ExpectedArgumentCount is { } expectedArgumentCount)
        {
            Assert.AreEqual(expectedArgumentCount, message.Arguments.Count, $"{testCase}: message argument count differs.");
        }
    }

    private static void RunCompileMetadataTest(EventScriptConformanceCase testCase)
    {
        var expectedDefinitions = testCase.Test.ExpectedMessageDefinitions;
        if (expectedDefinitions is null || expectedDefinitions.Count == 0)
        {
            Assert.Fail($"{testCase}: compileMetadata tests require expectedMessageDefinitions.");
        }

        var compiled = CompileScripts(testCase.Test, RequireEngine(testCase));
        var messageDefinitions = GetMessageDefinitions(compiled);
        foreach (var expected in expectedDefinitions)
        {
            ValidateRequired(expected.Name, "expected message definition name", testCase.SuiteFile, testCase.SuiteName, testCase.Test.Name);
            if (!messageDefinitions.TryGetValue(expected.Name!, out var definitions))
            {
                Assert.Fail($"{testCase}: expected compiled message definition '{expected.Name}' was not found.");
            }

            if (expected.Count is { } expectedCount)
            {
                Assert.AreEqual(expectedCount, definitions.Count, $"{testCase}: message definition count for '{expected.Name}' differs.");
            }

            if (expected.SignatureIds is not null)
            {
                CollectionAssert.AreEqual(
                    expected.SignatureIds.ToArray(),
                    definitions.Select(definition => definition.SignatureId).ToArray(),
                    $"{testCase}: message definition signature ids for '{expected.Name}' differ.");
            }
        }
    }

    private static void RegisterExternalSubscribers(EventScriptConformanceCase testCase, EventScriptHost host)
    {
        if (testCase.Test.ExternalSubscribers is null)
        {
            return;
        }

        foreach (var subscriber in testCase.Test.ExternalSubscribers)
        {
            ValidateRequired(subscriber.Message, "external subscriber message", testCase.SuiteFile, testCase.SuiteName, testCase.Test.Name);
            host.Subscribe(
                subscriber.Message!,
                subscriber.Parameters ?? [],
                (message, context) =>
                {
                    if (subscriber.Throw)
                    {
                        throw new InvalidOperationException("Configured conformance subscriber failure.");
                    }

                    foreach (var publish in subscriber.Publish ?? [])
                    {
                        ValidateRequired(publish.Name, "external subscriber publish name", testCase.SuiteFile, testCase.SuiteName, testCase.Test.Name);
                        var args = publish.ForwardArguments
                            ? message.Arguments.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                            : publish.Args.ValueKind == JsonValueKind.Undefined
                                ? new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
                                : EventScriptConformanceValueCodec.DecodeArguments(publish.Args);

                        context.Publish(EventScriptMessage.Message(publish.Name!, args));
                    }
                },
                subscriber.Priority);
        }
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
        EventScriptApiStepSpec step,
        IReadOnlyList<EventScriptDiagnosticEvent> actual)
    {
        var expectedDiagnostics = step.ExpectedDiagnostics;
        if (expectedDiagnostics is null || expectedDiagnostics.Count == 0)
        {
            AssertUnexpectedDiagnostics(testCase, stepIndex, step.UnexpectedDiagnostics, actual);
            return;
        }

        if (string.Equals(step.ExpectedDiagnosticsMode, "exact", StringComparison.OrdinalIgnoreCase))
        {
            AssertExactDiagnostics(testCase, stepIndex, expectedDiagnostics, actual);
            AssertUnexpectedDiagnostics(testCase, stepIndex, step.UnexpectedDiagnostics, actual);
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

        AssertUnexpectedDiagnostics(testCase, stepIndex, step.UnexpectedDiagnostics, actual);
    }

    private static void AssertExactDiagnostics(
        EventScriptConformanceCase testCase,
        int stepIndex,
        IReadOnlyList<EventScriptDiagnosticExpectationSpec> expectedDiagnostics,
        IReadOnlyList<EventScriptDiagnosticEvent> actual)
    {
        if (expectedDiagnostics.Count != actual.Count)
        {
            Assert.Fail(
                $"{testCase} step {stepIndex + 1}: expected {expectedDiagnostics.Count} diagnostics but got {actual.Count}.{Environment.NewLine}" +
                $"Actual diagnostics:{Environment.NewLine}{DescribeDiagnostics(actual)}");
        }

        for (var i = 0; i < expectedDiagnostics.Count; i++)
        {
            if (!DiagnosticMatches(expectedDiagnostics[i], actual[i]))
            {
                Assert.Fail(
                    $"{testCase} step {stepIndex + 1}: diagnostic #{i + 1} did not match {DescribeDiagnosticExpectation(expectedDiagnostics[i])}.{Environment.NewLine}" +
                    $"Actual diagnostics:{Environment.NewLine}{DescribeDiagnostics(actual)}");
            }
        }
    }

    private static void AssertUnexpectedDiagnostics(
        EventScriptConformanceCase testCase,
        int stepIndex,
        IReadOnlyList<EventScriptDiagnosticExpectationSpec>? unexpectedDiagnostics,
        IReadOnlyList<EventScriptDiagnosticEvent> actual)
    {
        if (unexpectedDiagnostics is null || unexpectedDiagnostics.Count == 0)
        {
            return;
        }

        foreach (var unexpected in unexpectedDiagnostics)
        {
            var found = actual.FirstOrDefault(diagnostic => DiagnosticMatches(unexpected, diagnostic));
            if (found is not null)
            {
                Assert.Fail(
                    $"{testCase} step {stepIndex + 1}: unexpected diagnostic was found: {DescribeDiagnosticExpectation(unexpected)}.{Environment.NewLine}" +
                    $"Actual diagnostics:{Environment.NewLine}{DescribeDiagnostics(actual)}");
            }
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

    private static void AssertOpcodeCompilationError(
        EventScriptConformanceCase testCase,
        EventScriptExpectedCompileErrorSpec expected,
        EventScriptOpcodeCompilationException exception)
    {
        if (!PhaseMatches(expected, "compilation"))
        {
            Assert.Fail($"{testCase}: expected phase '{expected.Phase}', but got opcode compilation error: {exception.Message}");
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

        Assert.Fail($"{testCase}: opcode compilation error expectation did not match.{Environment.NewLine}{exception.Message}");
    }

    private static IEventScriptMessageHandlerCollection CompileScripts(EventScriptConformanceTest test, string engine)
    {
        var linked = LinkScripts(test);
        var diagnosticsEnabled = test.CompileOptions?.EnableDiagnostics ?? false;
        return engine switch
        {
            InterpreterEngine => new CompiledEventScript(
                linked,
                new EventScriptInterpreterCompilationOptions { EnableDiagnostics = diagnosticsEnabled }),
            ExperimentalEngine => ExperimentalEventScriptCompiler.Compile(
                linked,
                new ExperimentalEventScriptCompilationOptions { EnableDiagnostics = diagnosticsEnabled }),
            RegisterVmEngine => RegisterEventScriptCompiler.Compile(
                linked,
                new RegisterEventScriptCompilationOptions { EnableDiagnostics = diagnosticsEnabled }),
            _ => throw new InvalidOperationException($"Unsupported conformance engine '{engine}'.")
        };
    }

    private static LinkedEventScriptModule LinkScripts(EventScriptConformanceTest test)
    {
        var modules = GetSources(test)
            .Select(source => EventScriptManager.ParseModule(source.Text!, source.SourceName))
            .ToArray();
        return EventScriptManager.LinkModules(modules);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<EventScriptMessageSignature>> GetMessageDefinitions(
        IEventScriptMessageHandlerCollection compiled)
    {
        return compiled.Handlers
            .GroupBy(handler => handler.Signature.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<EventScriptMessageSignature>)group.Select(handler => handler.Signature).ToArray(),
                StringComparer.Ordinal);
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

    private static void AssertOptionalEquals(EventScriptConformanceCase testCase, string description, string? expected, string actual)
    {
        if (expected is null)
        {
            return;
        }

        Assert.AreEqual(expected, actual, $"{testCase}: {description} differs.");
    }

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
