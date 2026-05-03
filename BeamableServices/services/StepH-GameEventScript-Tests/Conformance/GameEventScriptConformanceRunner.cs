using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using StepH.GameEventScript;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.RegisterVM;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;

namespace StepH_GameEventScript_Tests.Conformance;

internal static class GameEventScriptConformanceRunner
{
    internal const string RegisterVmEngine = "registervm";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    internal static IEnumerable<object[]> ConformanceCases(string specDirectory)
    {
        foreach (var testCase in EnumerateConformanceCases(specDirectory))
        {
            yield return [testCase];
        }
    }

    internal static IEnumerable<GameEventScriptConformanceCase> EnumerateRegisterVmScriptApiCases(string specDirectory)
    {
        foreach (var (file, suite, test) in EnumerateTests(specDirectory))
        {
            ValidateRequired(test.Kind, "test kind", file, suite.Name, test.Name);
            ValidateRequired(test.Name, "test name", file, suite.Name, test.Name);
            if (string.Equals(test.Kind, "scriptApi", StringComparison.OrdinalIgnoreCase))
            {
                yield return new GameEventScriptConformanceCase(file, suite.Name!, test, RegisterVmEngine);
            }
        }
    }

    internal static string GetCaseId(GameEventScriptConformanceCase testCase)
        => $"{testCase.SuiteName}/{testCase.Test.Name}";

    internal static void RunCase(GameEventScriptConformanceCase testCase)
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

    internal static void RunScriptApiTest(
        GameEventScriptConformanceCase testCase,
        Func<GameEventScriptConformanceTest, IGseMessageHandlerCollection>? compileScripts = null)
    {
        var test = testCase.Test;
        var compiled = (compileScripts ?? CompileScripts)(test);
        var collector = new GseDiagnosticTraceCollector();
        var published = new List<GseMessage>();
        var builder = GseHost.CreateBuilder()
            .WithRandom(CreateRandom(test.RandomSequence))
            .WithRegistry(GameEventScriptConformanceExtensionRegistry.Instance)
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

            host.Publish(GameEventScriptConformanceValueCodec.DecodeMessage(RequireDefined(step.Input, "step input", testCase)));

            AssertPublishedMessages(testCase, stepIndex, step.ExpectedPublished, published);
            AssertDiagnostics(testCase, stepIndex, step, collector.Events.Skip(diagnosticsStart).ToArray());
        }
    }

    internal static IGseMessageHandlerCollection CompileScripts(GameEventScriptConformanceTest test)
    {
        var module = BuildModule(test);
        var diagnosticsEnabled = test.CompileOptions?.EnableDiagnostics ?? false;
        return RegisterVmCompiler.Compile(
            module,
            new RegisterVmCompilationOptions
            {
                EnableDiagnostics = diagnosticsEnabled
            });
    }

    private static IEnumerable<GameEventScriptConformanceCase> EnumerateConformanceCases(string specDirectory)
    {
        foreach (var (file, suite, test) in EnumerateTests(specDirectory))
        {
            ValidateRequired(test.Kind, "test kind", file, suite.Name, test.Name);
            ValidateRequired(test.Name, "test name", file, suite.Name, test.Name);
            yield return new GameEventScriptConformanceCase(file, suite.Name!, test, UsesRuntimeEngine(test.Kind) ? RegisterVmEngine : null);
        }
    }

    private static IEnumerable<(string File, GameEventScriptConformanceSuite Suite, GameEventScriptConformanceTest Test)> EnumerateTests(
        string specDirectory)
    {
        foreach (var file in Directory.EnumerateFiles(specDirectory, "*.json", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
        {
            var suite = LoadSuite(file);
            foreach (var test in suite.Tests)
            {
                yield return (file, suite, test);
            }
        }
    }

    private static bool UsesRuntimeEngine(string? kind)
        => string.Equals(kind, "scriptApi", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(kind, "compileError", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(kind, "compileMetadata", StringComparison.OrdinalIgnoreCase);

    private static void RunCompileErrorTest(GameEventScriptConformanceCase testCase)
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
        catch (GameEventScriptSyntaxException exception)
        {
            AssertSyntaxError(testCase, expected, exception);
            return;
        }
        catch (GameEventScriptModuleBuildException exception)
        {
            AssertModuleBuildError(testCase, expected, exception);
            return;
        }
        catch (GameEventScriptCompilationException exception)
        {
            AssertGenericCompilationError(testCase, expected, exception);
            return;
        }

        Assert.Fail($"{testCase}: expected compilation to fail.");
    }

    private static void RunMessageApiTest(GameEventScriptConformanceCase testCase)
    {
        var signatureSpec = testCase.Test.Signature
                            ?? throw new InvalidOperationException($"{testCase}: messageApi tests require signature.");

        ValidateRequired(signatureSpec.Name, "messageApi signature name", testCase.SuiteFile, testCase.SuiteName, testCase.Test.Name);
        var signature = new GseMessageSignature(signatureSpec.Name!, signatureSpec.Parameters ?? []);
        var message = GameEventScriptConformanceValueCodec.DecodeMessage(RequireDefined(testCase.Test.Message, "messageApi message", testCase));

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

    private static void RunCompileMetadataTest(GameEventScriptConformanceCase testCase)
    {
        var expectedDefinitions = testCase.Test.ExpectedMessageDefinitions;
        if (expectedDefinitions is null || expectedDefinitions.Count == 0)
        {
            Assert.Fail($"{testCase}: compileMetadata tests require expectedMessageDefinitions.");
        }

        var compiled = CompileScripts(testCase.Test);
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
                Assert.HasCount(expectedCount, definitions, $"{testCase}: message definition count for '{expected.Name}' differs.");
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

    private static void RegisterExternalSubscribers(GameEventScriptConformanceCase testCase, GseHost host)
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
                                ? new Dictionary<string, GseValue>(StringComparer.Ordinal)
                                : GameEventScriptConformanceValueCodec.DecodeArguments(publish.Args);

                        context.Publish(GseMessage.Message(publish.Name!, args));
                    }
                },
                subscriber.Priority);
        }
    }

    private static void AssertPublishedMessages(
        GameEventScriptConformanceCase testCase,
        int stepIndex,
        IReadOnlyList<JsonElement>? expectedPublished,
        IReadOnlyList<GseMessage> actual)
    {
        var expected = (expectedPublished ?? []).Select(GameEventScriptConformanceValueCodec.DecodeMessage).ToArray();
        var expectedJson = GameEventScriptConformanceValueCodec.ToCanonicalJson(expected);
        var actualJson = GameEventScriptConformanceValueCodec.ToCanonicalJson(actual);
        if (expectedJson == actualJson)
        {
            return;
        }

        Assert.Fail(
            $"{testCase} step {stepIndex + 1}: published messages differ.{Environment.NewLine}" +
            $"Expected:{Environment.NewLine}{GameEventScriptConformanceValueCodec.ToPrettyJson(expected)}{Environment.NewLine}" +
            $"Actual:{Environment.NewLine}{GameEventScriptConformanceValueCodec.ToPrettyJson(actual)}");
    }

    private static void AssertDiagnostics(
        GameEventScriptConformanceCase testCase,
        int stepIndex,
        GameEventScriptApiStepSpec step,
        IReadOnlyList<GseDiagnosticEvent> actual)
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
        GameEventScriptConformanceCase testCase,
        int stepIndex,
        IReadOnlyList<GameEventScriptDiagnosticExpectationSpec> expectedDiagnostics,
        IReadOnlyList<GseDiagnosticEvent> actual)
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
        GameEventScriptConformanceCase testCase,
        int stepIndex,
        IReadOnlyList<GameEventScriptDiagnosticExpectationSpec>? unexpectedDiagnostics,
        IReadOnlyList<GseDiagnosticEvent> actual)
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

    private static bool DiagnosticMatches(GameEventScriptDiagnosticExpectationSpec expected, GseDiagnosticEvent actual)
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
        GameEventScriptConformanceCase testCase,
        GameEventScriptExpectedCompileErrorSpec expected,
        GameEventScriptSyntaxException exception)
    {
        if (!PhaseMatches(expected, "syntax"))
        {
            Assert.Fail($"{testCase}: expected phase '{expected.Phase}', but got syntax error: {exception.Message}");
        }

        if (exception.Errors.Any(error =>
                Matches(expected.ModuleName, error.ModuleName) &&
                MessageMatches(expected.MessageContains, error.Message, exception.Message)))
        {
            return;
        }

        Assert.Fail($"{testCase}: syntax error expectation did not match.{Environment.NewLine}{exception.Message}");
    }

    private static void AssertModuleBuildError(
        GameEventScriptConformanceCase testCase,
        GameEventScriptExpectedCompileErrorSpec expected,
        GameEventScriptModuleBuildException exception)
    {
        if (!PhaseMatches(expected, "build"))
        {
            Assert.Fail($"{testCase}: expected phase '{expected.Phase}', but got module build error: {exception.Message}");
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

        Assert.Fail($"{testCase}: module build error expectation did not match.{Environment.NewLine}{exception.Message}");
    }

    private static void AssertGenericCompilationError(
        GameEventScriptConformanceCase testCase,
        GameEventScriptExpectedCompileErrorSpec expected,
        GameEventScriptCompilationException exception)
    {
        if (!PhaseMatches(expected, "compilation") || !MessageMatches(expected.MessageContains, exception.Message))
        {
            Assert.Fail($"{testCase}: generic compilation error expectation did not match.{Environment.NewLine}{exception.Message}");
        }
    }

    private static GseModule BuildModule(GameEventScriptConformanceTest test)
    {
        var builder = GseModuleBuilder.Create();
        foreach (var source in GetSources(test))
        {
            builder.AddScript(source.Text!, source.SourceName);
        }

        return builder.Build();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<GseMessageSignature>> GetMessageDefinitions(
        IGseMessageHandlerCollection compiled)
    {
        return compiled.Handlers
            .GroupBy(handler => handler.Signature.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<GseMessageSignature>)group.Select(handler => handler.Signature).ToArray(),
                StringComparer.Ordinal);
    }

    private static IEnumerable<GameEventScriptSourceSpec> GetSources(GameEventScriptConformanceTest test)
    {
        if (test.Scripts is { Count: > 0 })
        {
            foreach (var source in test.Scripts)
            {
                if (string.IsNullOrWhiteSpace(source.Text))
                {
                    throw new InvalidOperationException($"Test '{test.Name}' has a script source without text.");
                }

                yield return new GameEventScriptSourceSpec
                {
                    SourceName = string.IsNullOrWhiteSpace(source.SourceName) ? $"{test.Name}.es" : source.SourceName,
                    Text = source.Text
                };
            }

            yield break;
        }

        if (!string.IsNullOrWhiteSpace(test.Script))
        {
            yield return new GameEventScriptSourceSpec
            {
                SourceName = $"{test.Name}.es",
                Text = test.Script
            };
            yield break;
        }

        throw new InvalidOperationException($"Test '{test.Name}' requires script or scripts.");
    }

    private static GseRandomGenerator CreateRandom(IReadOnlyList<string>? randomSequence)
    {
        if (randomSequence is null || randomSequence.Count == 0)
        {
            return GseRandomGenerator.Create();
        }

        return GseRandomGenerator.FromSequence(randomSequence.Select(value => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture)).ToArray());
    }

    private static GseRuntimeLimits CreateRuntimeLimits(GameEventScriptRuntimeLimitsSpec? spec)
    {
        var defaults = GseRuntimeLimits.Default;
        if (spec is null)
        {
            return defaults;
        }

        return new GseRuntimeLimits
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

    private static JsonElement RequireDefined(JsonElement element, string name, GameEventScriptConformanceCase testCase)
    {
        if (element.ValueKind == JsonValueKind.Undefined)
        {
            Assert.Fail($"{testCase}: missing {name}.");
        }

        return element;
    }

    private static GameEventScriptConformanceSuite LoadSuite(string file)
    {
        var suite = JsonSerializer.Deserialize<GameEventScriptConformanceSuite>(File.ReadAllText(file), JsonOptions)
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

    private static bool PhaseMatches(GameEventScriptExpectedCompileErrorSpec expected, string actual)
        => string.IsNullOrWhiteSpace(expected.Phase) ||
           string.Equals(expected.Phase, actual, StringComparison.OrdinalIgnoreCase);

    private static bool Matches(string? expected, string actual)
        => string.IsNullOrWhiteSpace(expected) ||
           string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);

    private static bool MessageMatches(string? expected, params string?[] actualMessages)
        => string.IsNullOrWhiteSpace(expected) ||
           actualMessages.Any(message => message?.Contains(expected, StringComparison.Ordinal) ?? false);

    private static void AssertOptionalEquals(GameEventScriptConformanceCase testCase, string description, string? expected, string actual)
    {
        if (expected is null)
        {
            return;
        }

        Assert.AreEqual(expected, actual, $"{testCase}: {description} differs.");
    }

    private static string DescribeDiagnosticExpectation(GameEventScriptDiagnosticExpectationSpec expected)
        => $"kind={expected.Kind ?? "*"}, name={expected.Name ?? "*"}, detailContains={expected.DetailContains ?? expected.MessageContains ?? "*"}";

    private static string DescribeDiagnostics(IEnumerable<GseDiagnosticEvent> diagnostics)
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
}

internal sealed class GameEventScriptConformanceExtensionRegistry : IGseExtensionRegistry
{
    public static readonly GameEventScriptConformanceExtensionRegistry Instance = new();

    private static readonly IGseExtensionFunction MathFloor = new DelegateExtensionFunction((_, args) =>
        args.Length == 1
            ? GseFastValue.FromDecimal(Math.Floor(args[0].Number), args[0].Unit)
            : GseFastValue.Nothing);

    private static readonly IGseExtensionFunction MathMax = new DelegateExtensionFunction((_, args) =>
    {
        if (args.Length == 0)
        {
            return GseFastValue.Nothing;
        }

        var max = args[0].Number;
        for (var index = 1; index < args.Length; index++)
        {
            max = Math.Max(max, args[index].Number);
        }

        return GseFastValue.FromDecimal(max);
    });

    private static readonly IGseExtensionFunction NavShortestTurn = new DelegateExtensionFunction((_, args) =>
    {
        if (args.Length != 2)
        {
            return GseFastValue.Nothing;
        }

        var from = args[0].Number;
        var to = args[1].Number;
        var delta = (to - from + 540m) % 360m - 180m;
        return GseFastValue.FromDecimal(delta, GseDecimalUnit.Degree);
    });

    private static readonly IGseExtensionFunction NavIsNorth = new DelegateExtensionFunction((_, args) =>
    {
        if (args.Length != 1)
        {
            return GseFastValue.FromBoolean(false);
        }

        var value = args[0].Number;
        var wrapped = ((value % 360m) + 360m) % 360m;
        return GseFastValue.FromBoolean(wrapped is <= 45m or >= 315m);
    });

    private static readonly IGseExtensionFunction TestVectorSum = new DelegateExtensionFunction((_, args) =>
    {
        if (args.Length != 1)
        {
            return GseFastValue.Nothing;
        }

        return args[0].Kind switch
        {
            GseValueKind.Vector2 => GseFastValue.FromDecimal(args[0].X + args[0].Y, args[0].Unit),
            GseValueKind.Vector3 => GseFastValue.FromDecimal(args[0].X + args[0].Y + args[0].Z, args[0].Unit),
            _ => GseFastValue.Nothing
        };
    });

    private GameEventScriptConformanceExtensionRegistry()
    {
    }

    public bool TryResolve(GseExtensionReference reference, out IGseExtensionFunction function)
    {
        if (string.Equals(reference.ExtensionName, "math", StringComparison.Ordinal) &&
            string.Equals(reference.FunctionName, "floor", StringComparison.Ordinal) &&
            reference.ArgumentLabels.Count == 1 &&
            IsUnlabeled(reference.ArgumentLabels[0]))
        {
            function = MathFloor;
            return true;
        }

        if (string.Equals(reference.ExtensionName, "math", StringComparison.Ordinal) &&
            string.Equals(reference.FunctionName, "max", StringComparison.Ordinal) &&
            reference.ArgumentLabels.Count > 0 &&
            reference.ArgumentLabels.All(IsUnlabeled))
        {
            function = MathMax;
            return true;
        }

        if (string.Equals(reference.ExtensionName, "nav", StringComparison.Ordinal) &&
            string.Equals(reference.FunctionName, "shortestTurn", StringComparison.Ordinal) &&
            reference.ArgumentLabels.SequenceEqual(["from", "to"], StringComparer.Ordinal))
        {
            function = NavShortestTurn;
            return true;
        }

        if (string.Equals(reference.ExtensionName, "nav", StringComparison.Ordinal) &&
            string.Equals(reference.FunctionName, "isNorth", StringComparison.Ordinal) &&
            reference.ArgumentLabels.Count == 1)
        {
            function = NavIsNorth;
            return true;
        }

        if (string.Equals(reference.ExtensionName, "test", StringComparison.Ordinal) &&
            string.Equals(reference.FunctionName, "vectorSum", StringComparison.Ordinal) &&
            reference.ArgumentLabels.Count == 1 &&
            IsUnlabeled(reference.ArgumentLabels[0]))
        {
            function = TestVectorSum;
            return true;
        }

        function = default!;
        return false;
    }

    private static bool IsUnlabeled(string label)
        => string.Equals(label, GseMessageSignature.UnlabeledParameterName, StringComparison.Ordinal);

    private delegate GseFastValue ExtensionInvoke(GseExtensionContext context, ReadOnlySpan<GseFastValue> arguments);

    private sealed class DelegateExtensionFunction(ExtensionInvoke invoke) : IGseExtensionFunction
    {
        public GseFastValue Invoke(GseExtensionContext context, ReadOnlySpan<GseFastValue> arguments)
            => invoke(context, arguments);
    }
}
