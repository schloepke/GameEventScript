using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.BytecodeVM;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;

namespace StepH_GameEventScript_Tests.Conformance;

internal static class GameEventScriptConformanceRunner
{
    internal const string BytecodeVmEngine = "bytecodevm";

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

    internal static IEnumerable<GameEventScriptConformanceCase> EnumerateBytecodeVmScriptApiCases(string specDirectory)
    {
        foreach (var (file, suite, test) in EnumerateTests(specDirectory))
        {
            ValidateRequired(test.Kind, "test kind", file, suite.Name, test.Name);
            ValidateRequired(test.Name, "test name", file, suite.Name, test.Name);
            if (string.Equals(test.Kind, "scriptApi", StringComparison.OrdinalIgnoreCase))
            {
                yield return new GameEventScriptConformanceCase(file, suite.Name!, test, BytecodeVmEngine);
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
        Func<GameEventScriptConformanceTest, IGameEventScriptMessageHandlerCollection>? compileScripts = null)
    {
        var test = testCase.Test;
        var compiled = (compileScripts ?? CompileScripts)(test);
        var collector = new GameEventScriptDiagnosticTraceCollector();
        var published = new List<GameEventScriptMessage>();
        var outboundPublished = new List<GameEventScriptMessage>();
        var builder = GameEventScriptHost.CreateBuilder()
            .WithRandom(CreateRandom(test.RandomSequence))
            .WithRegistry(GameEventScriptConformanceExtensionRegistry.Instance)
            .WithRuntimeLimits(CreateRuntimeLimits(test.RuntimeLimits))
            .WithDiagnosticCollector(collector)
            .WithPublishedMessageObserver(published.Add)
            .WithPublishHook(message =>
            {
                outboundPublished.Add(message);
                return true;
            });

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
            outboundPublished.Clear();

            host.PublishToCompletion(GameEventScriptConformanceValueCodec.DecodeMessage(RequireDefined(step.Input, "step input", testCase)));

            AssertPublishedMessages(testCase, stepIndex, "published messages", step.ExpectedPublished, published);
            AssertPublishedMessages(testCase, stepIndex, "outbound published messages", step.ExpectedOutboundPublished, outboundPublished);
            AssertDiagnostics(testCase, stepIndex, step, collector.Events.Skip(diagnosticsStart).ToArray());
        }
    }

    internal static IGameEventScriptMessageHandlerCollection CompileScripts(GameEventScriptConformanceTest test)
    {
        return GesBytecodeVmExecutableBuilder.Build(CompileBytecode(test));
    }

    private static IEnumerable<GameEventScriptConformanceCase> EnumerateConformanceCases(string specDirectory)
    {
        foreach (var (file, suite, test) in EnumerateTests(specDirectory))
        {
            ValidateRequired(test.Kind, "test kind", file, suite.Name, test.Name);
            ValidateRequired(test.Name, "test name", file, suite.Name, test.Name);
            yield return new GameEventScriptConformanceCase(file, suite.Name!, test, UsesRuntimeEngine(test.Kind) ? BytecodeVmEngine : null);
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
        catch (GameEventScriptCompileException exception)
        {
            AssertCompileError(testCase, expected, exception);
            return;
        }

        Assert.Fail($"{testCase}: expected compilation to fail.");
    }

    private static void RunMessageApiTest(GameEventScriptConformanceCase testCase)
    {
        var signatureSpec = testCase.Test.Signature
                            ?? throw new InvalidOperationException($"{testCase}: messageApi tests require signature.");

        ValidateRequired(signatureSpec.Name, "messageApi signature name", testCase.SuiteFile, testCase.SuiteName, testCase.Test.Name);
        var signature = GameEventScriptMessageSignature.Create(signatureSpec.Name!, signatureSpec.Parameters ?? []);
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

    private static void RegisterExternalSubscribers(GameEventScriptConformanceCase testCase, GameEventScriptHost host)
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

                    foreach (var emit in subscriber.Emit ?? [])
                    {
                        ValidateRequired(emit.Name, "external subscriber emit name", testCase.SuiteFile, testCase.SuiteName, testCase.Test.Name);
                        var args = emit.ForwardArguments
                            ? message.Arguments.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                            : emit.Args.ValueKind == JsonValueKind.Undefined
                                ? new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal)
                                : GameEventScriptConformanceValueCodec.DecodeArguments(emit.Args);

                        context.Emit(GameEventScriptMessage.Create(emit.Name!, args));
                    }
                },
                subscriber.Priority ?? 0);
        }
    }

    private static void AssertPublishedMessages(
        GameEventScriptConformanceCase testCase,
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
            $"{testCase} step {stepIndex + 1}: {label} differ.{Environment.NewLine}" +
            $"Expected:{Environment.NewLine}{GameEventScriptConformanceValueCodec.ToPrettyJson(expected)}{Environment.NewLine}" +
            $"Actual:{Environment.NewLine}{GameEventScriptConformanceValueCodec.ToPrettyJson(actual)}");
    }

    private static void AssertDiagnostics(
        GameEventScriptConformanceCase testCase,
        int stepIndex,
        GameEventScriptApiStepSpec step,
        IReadOnlyList<GameEventScriptDiagnosticEvent> actual)
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
        IReadOnlyList<GameEventScriptDiagnosticEvent> actual)
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
        IReadOnlyList<GameEventScriptDiagnosticEvent> actual)
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

    private static bool DiagnosticMatches(GameEventScriptDiagnosticExpectationSpec expected, GameEventScriptDiagnosticEvent actual)
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

    private static void AssertCompileError(
        GameEventScriptConformanceCase testCase,
        GameEventScriptExpectedCompileErrorSpec expected,
        GameEventScriptCompileException exception)
    {
        if (exception.Errors.Count == 0)
        {
            AssertGenericCompilationError(testCase, expected, exception);
            return;
        }

        var expectedPhase = string.IsNullOrWhiteSpace(expected.Phase) ? null : expected.Phase;
        var matchingErrors = exception.Errors.Where(error =>
            expectedPhase is null ||
            PhaseMatches(expected, error.Kind == GameEventScriptCompileErrorKind.Syntax ? "syntax" : "build"));

        if (matchingErrors.Any(error =>
                Matches(expected.Kind, error.Kind.ToString()) &&
                Matches(expected.Symbol, error.Symbol) &&
                Matches(expected.SymbolKind, error.SymbolKind.ToString()) &&
                Matches(expected.ModuleName, error.ModuleName) &&
                MessageMatches(expected.MessageContains, error.Message, exception.Message)))
        {
            return;
        }

        Assert.Fail($"{testCase}: compile error expectation did not match.{Environment.NewLine}{exception.Message}");
    }

    private static void AssertGenericCompilationError(
        GameEventScriptConformanceCase testCase,
        GameEventScriptExpectedCompileErrorSpec expected,
        GameEventScriptCompileException exception)
    {
        if (!PhaseMatches(expected, "compilation") || !MessageMatches(expected.MessageContains, exception.Message))
        {
            Assert.Fail($"{testCase}: generic compilation error expectation did not match.{Environment.NewLine}{exception.Message}");
        }
    }

    private static GameEventScriptCompiled CompileBytecode(GameEventScriptConformanceTest test)
    {
        var builder = GameEventScriptBuilder.Create();
        foreach (var source in GetSources(test))
        {
            builder.AddScript(source.Text!, source.SourceName);
        }

        return builder.Compile(new GameEventScriptCompileOptions
        {
            Optimize = test.CompileOptions?.Optimize ?? true,
            EnableDiagnostics = test.CompileOptions?.EnableDiagnostics ?? false
        });
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<GameEventScriptMessageSignature>> GetMessageDefinitions(
        IGameEventScriptMessageHandlerCollection compiled)
    {
        return compiled.Handlers
            .GroupBy(handler => handler.Signature.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<GameEventScriptMessageSignature>)group.Select(handler => handler.Signature).ToArray(),
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

    private static GameEventScriptRandomGenerator CreateRandom(IReadOnlyList<string>? randomSequence)
    {
        if (randomSequence is null || randomSequence.Count == 0)
        {
            return GameEventScriptRandomGenerator.Create();
        }

        return GameEventScriptRandomGenerator.FromSequence(randomSequence.Select(value => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture)).ToArray());
    }

    private static GameEventScriptRuntimeLimits CreateRuntimeLimits(GameEventScriptRuntimeLimitsSpec? spec)
    {
        var defaults = GameEventScriptRuntimeLimits.Default;
        if (spec is null)
        {
            return defaults;
        }

        return new GameEventScriptRuntimeLimits
        {
            MaxProcessedEventsPerRun = spec.MaxProcessedEventsPerRun ?? defaults.MaxProcessedEventsPerRun,
            MaxQueuedMessagesPerRun = spec.MaxQueuedMessagesPerRun ?? defaults.MaxQueuedMessagesPerRun,
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

    private static string DescribeDiagnostics(IEnumerable<GameEventScriptDiagnosticEvent> diagnostics)
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

internal sealed class GameEventScriptConformanceExtensionRegistry : IGameEventScriptExtensionRegistry
{
    public static readonly GameEventScriptConformanceExtensionRegistry Instance = new();

    private static readonly IGameEventScriptExtensionFunction MathFloor = new DelegateExtensionFunction((_, args) =>
        args.Length == 1
            ? GameEventScriptFastValue.FromDecimal(Math.Floor(args[0].Number), args[0].Unit)
            : GameEventScriptFastValue.Nothing);

    private static readonly IGameEventScriptExtensionFunction MathMax = new DelegateExtensionFunction((_, args) =>
    {
        if (args.Length == 0)
        {
            return GameEventScriptFastValue.Nothing;
        }

        var max = args[0].Number;
        for (var index = 1; index < args.Length; index++)
        {
            max = Math.Max(max, args[index].Number);
        }

        return GameEventScriptFastValue.FromDecimal(max);
    });

    private static readonly IGameEventScriptExtensionFunction NavShortestTurn = new DelegateExtensionFunction((_, args) =>
    {
        if (args.Length != 2)
        {
            return GameEventScriptFastValue.Nothing;
        }

        var from = args[0].Number;
        var to = args[1].Number;
        var delta = (to - from + 540m) % 360m - 180m;
        return GameEventScriptFastValue.FromDecimal(delta, GameEventScriptDecimalUnit.Degree);
    });

    private static readonly IGameEventScriptExtensionFunction NavIsNorth = new DelegateExtensionFunction((_, args) =>
    {
        if (args.Length != 1)
        {
            return GameEventScriptFastValue.FromBoolean(false);
        }

        var value = args[0].Number;
        var wrapped = ((value % 360m) + 360m) % 360m;
        return GameEventScriptFastValue.FromBoolean(wrapped is <= 45m or >= 315m);
    });

    private static readonly IGameEventScriptExtensionFunction TestVectorSum = new DelegateExtensionFunction((_, args) =>
    {
        if (args.Length != 1)
        {
            return GameEventScriptFastValue.Nothing;
        }

        return args[0].Kind switch
        {
            GameEventScriptValueKind.Vector => GameEventScriptFastValue.FromDecimal(args[0].X + args[0].Y + args[0].Z, args[0].Unit),
            _ => GameEventScriptFastValue.Nothing
        };
    });

    private GameEventScriptConformanceExtensionRegistry()
    {
    }

    public bool TryResolve(GameEventScriptExtensionReference reference, out IGameEventScriptExtensionFunction function)
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
        => string.Equals(label, GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal);

    private delegate GameEventScriptFastValue ExtensionInvoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptFastValue> arguments);

    private sealed class DelegateExtensionFunction(ExtensionInvoke invoke) : IGameEventScriptExtensionFunction
    {
        public GameEventScriptFastValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptFastValue> arguments)
            => invoke(context, arguments);
    }
}
