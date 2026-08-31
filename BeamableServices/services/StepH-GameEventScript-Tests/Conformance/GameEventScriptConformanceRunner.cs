using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.Values;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.CSharpBridge;
using StepH.GameEventScript.Runtime;

namespace StepH_GameEventScript_Tests.Conformance;

internal static class GameEventScriptConformanceRunner
{
    internal const string VirtualMachineEngine = "virtualmachine";
    internal static readonly IGameEventScriptExternalTypeRegistry ExternalTypeRegistry =
        GameEventScriptCSharpExternalTypes.CreateRegistry(typeof(AimValue));

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

    internal static IEnumerable<object[]> ConformanceCases(string specDirectory, string relativeSpecFile)
    {
        foreach (var testCase in EnumerateConformanceCases(specDirectory, relativeSpecFile))
        {
            yield return [testCase];
        }
    }

    internal static IReadOnlyList<GameEventScriptConformanceCase> AllConformanceCases(string specDirectory)
        => EnumerateConformanceCases(specDirectory).ToArray();

    internal static IReadOnlyList<GameEventScriptConformanceCase> AllConformanceCases(string specDirectory, string relativeSpecFile)
        => EnumerateConformanceCases(specDirectory, relativeSpecFile).ToArray();

    internal static IEnumerable<GameEventScriptConformanceCase> EnumerateVirtualMachineScriptApiCases(string specDirectory)
    {
        foreach (var (file, suite, test) in EnumerateTests(specDirectory))
        {
            ValidateRequired(test.Kind, "test kind", file, suite.Name, test.Name);
            ValidateRequired(test.Name, "test name", file, suite.Name, test.Name);
            if (string.Equals(test.Kind, "scriptApi", StringComparison.OrdinalIgnoreCase))
            {
                yield return new GameEventScriptConformanceCase(file, suite.Name!, ResolveLevel(file, suite, test), test, VirtualMachineEngine);
            }
        }
    }

    internal static string GetCaseId(GameEventScriptConformanceCase testCase)
        => $"{testCase.SuiteName}/{testCase.Test.Name}";

    internal static GameEventScriptProgram CompileScripts(GameEventScriptConformanceTest test)
    {
        return CreateScriptBuilder(test).Compile(CreateCompileOptions(test));
    }

    internal static GameEventScriptProgram CompileBytecodeForTest(GameEventScriptConformanceTest test)
        => CompileBytecode(test);

    internal static GameEventScriptRandomGenerator CreateRandomForTest(IReadOnlyList<string>? randomSequence)
        => CreateRandom(randomSequence);

    internal static GameEventScriptRuntimeLimits CreateRuntimeLimitsForTest(GameEventScriptRuntimeLimitsSpec? spec)
        => CreateRuntimeLimits(spec);

    private static IEnumerable<GameEventScriptConformanceCase> EnumerateConformanceCases(string specDirectory)
    {
        foreach (var (file, suite, test) in EnumerateTests(specDirectory))
        {
            ValidateRequired(test.Kind, "test kind", file, suite.Name, test.Name);
            ValidateRequired(test.Name, "test name", file, suite.Name, test.Name);
            yield return new GameEventScriptConformanceCase(file, suite.Name!, ResolveLevel(file, suite, test), test, UsesRuntimeEngine(test.Kind) ? VirtualMachineEngine : null);
        }
    }

    private static IEnumerable<GameEventScriptConformanceCase> EnumerateConformanceCases(
        string specDirectory,
        string relativeSpecFile)
    {
        var file = Path.Combine(
            new[] { specDirectory }
                .Concat(relativeSpecFile.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
                .ToArray());
        var suite = LoadSuite(file);
        foreach (var test in suite.Tests)
        {
            ValidateRequired(test.Kind, "test kind", file, suite.Name, test.Name);
            ValidateRequired(test.Name, "test name", file, suite.Name, test.Name);
            yield return new GameEventScriptConformanceCase(file, suite.Name!, ResolveLevel(file, suite, test), test, UsesRuntimeEngine(test.Kind) ? VirtualMachineEngine : null);
        }
    }

    private static string ResolveLevel(string file, GameEventScriptConformanceSuite suite, GameEventScriptConformanceTest test)
    {
        var level = test.Level ?? suite.Level;
        if (string.IsNullOrWhiteSpace(level))
        {
            return IsAtomicSpecPath(file) ? "atomic" : "scenario";
        }

        return string.Equals(level, "atomic", StringComparison.OrdinalIgnoreCase)
            ? "atomic"
            : "scenario";
    }

    private static bool IsAtomicSpecPath(string file)
        => file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => string.Equals(segment, "atomic", StringComparison.OrdinalIgnoreCase));

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

    internal static void RunCompileErrorTest(GameEventScriptConformanceCase testCase)
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

    internal static void RunMessageApiTest(GameEventScriptConformanceCase testCase)
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

    internal static void RunCompileMetadataTest(GameEventScriptConformanceCase testCase)
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

    internal static void RunBytecodeOpcodeTest(GameEventScriptConformanceCase testCase)
    {
        var expected = testCase.Test.ExpectedOpcodes;
        if (expected is null)
        {
            Assert.Fail($"{testCase}: bytecode tests require expectedOpcodes.");
        }

        var compiled = CompileBytecode(testCase.Test);
        var opCodes = compiled.InstructionTable.Select(instruction => instruction.OpCode).ToArray();
        var counts = opCodes
            .GroupBy(opCode => opCode)
            .ToDictionary(group => group.Key, group => group.Count());
        var failures = new List<string>();

        foreach (var name in expected.Contains ?? [])
        {
            var opCode = ParseExpectedOpcode(testCase, name);
            if (!counts.ContainsKey(opCode))
            {
                failures.Add($"expected opcode '{name}' to be present.");
            }
        }

        foreach (var name in expected.NotContains ?? [])
        {
            var opCode = ParseExpectedOpcode(testCase, name);
            if (counts.ContainsKey(opCode))
            {
                failures.Add($"expected opcode '{name}' to be absent, but found {counts[opCode]} occurrence(s).");
            }
        }

        foreach (var pair in expected.Counts ?? [])
        {
            var opCode = ParseExpectedOpcode(testCase, pair.Key);
            counts.TryGetValue(opCode, out var actual);
            if (actual != pair.Value)
            {
                failures.Add($"expected opcode '{pair.Key}' count {pair.Value}, actual {actual}.");
            }
        }

        foreach (var pair in expected.MinCounts ?? [])
        {
            var opCode = ParseExpectedOpcode(testCase, pair.Key);
            counts.TryGetValue(opCode, out var actual);
            if (actual < pair.Value)
            {
                failures.Add($"expected opcode '{pair.Key}' count >= {pair.Value}, actual {actual}.");
            }
        }

        if (failures.Count == 0)
        {
            return;
        }

        Assert.Fail(
            $"{testCase}: bytecode opcode expectations differ.{Environment.NewLine}" +
            string.Join(Environment.NewLine, failures) + Environment.NewLine +
            "Actual opcodes:" + Environment.NewLine +
            DescribeOpcodes(opCodes));
    }

    internal static void RegisterExternalSubscribers(GameEventScriptConformanceCase testCase, GameEventScriptHost host)
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
                        Dictionary<string, GesValue> args;
                        if (emit.ForwardArguments)
                        {
                            args = new Dictionary<string, GesValue>(message.Arguments.Count, StringComparer.Ordinal);
                            foreach (var pair in message.Arguments)
                            {
                                args[pair.Key] = pair.Value;
                            }
                        }
                        else
                        {
                            args = emit.Args.ValueKind == JsonValueKind.Undefined
                                ? new Dictionary<string, GesValue>(StringComparer.Ordinal)
                                : GameEventScriptConformanceValueCodec.DecodeArguments(emit.Args);
                        }

                        context.Emit(GameEventScriptMessage.Create(emit.Name!, args));
                    }
                },
                subscriber.Priority ?? 0);
        }
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
                MessageMatches(expected.MessageContains, error.Message, exception.Message) &&
                SourceLocationMatches(expected, error.SourceLocation)))
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

    private static bool SourceLocationMatches(
        GameEventScriptExpectedCompileErrorSpec expected,
        GameEventScriptSourceLocation location)
        => Matches(expected.SourceName, location.SourceName) &&
           Matches(expected.Line, location.Line) &&
           Matches(expected.Column, location.Column) &&
           Matches(expected.EndLine, location.EndLine) &&
           Matches(expected.EndColumn, location.EndColumn);

    private static GameEventScriptProgram CompileBytecode(GameEventScriptConformanceTest test)
    {
        return CreateScriptBuilder(test).Compile(CreateCompileOptions(test));
    }

    internal static GameEventScriptBuilder CreateScriptBuilderForTest(GameEventScriptConformanceTest test)
        => CreateScriptBuilder(test);

    internal static GameEventScriptCompileOptions CreateCompileOptionsForTest(GameEventScriptConformanceTest test)
        => CreateCompileOptions(test);

    private static GameEventScriptBuilder CreateScriptBuilder(GameEventScriptConformanceTest test)
    {
        var builder = GameEventScriptManager.CreateScriptBuilder()
            .WithExternalTypes(ExternalTypeRegistry);
        foreach (var source in GetSources(test))
        {
            builder.AddScript(source.Text!, source.SourceName);
        }

        return builder;
    }

    private static GameEventScriptCompileOptions CreateCompileOptions(GameEventScriptConformanceTest test)
        => new()
        {
            Optimize = test.CompileOptions?.Optimize ?? true,
            EnableDebugInfo = test.CompileOptions?.EnableDebugInfo ?? false
        };

    private static IReadOnlyDictionary<string, IReadOnlyList<GameEventScriptMessageSignature>> GetMessageDefinitions(
        GameEventScriptProgram compiled)
    {
        return compiled.BindTable.Entries
            .Where(entry => entry.Kind is GameEventScriptBinaryBindKind.MessageHandler or GameEventScriptBinaryBindKind.MessageNameHandler)
            .Select(entry => GameEventScriptMessageSignature.Create(
                compiled.TextConstantTable.Resolve(entry.Name),
                entry.ArgumentNames.Select(compiled.TextConstantTable.Resolve)))
            .GroupBy(signature => signature.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<GameEventScriptMessageSignature>)group.ToArray(),
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

        return GameEventScriptRandomGenerator.FromSequence(randomSequence.Select(value => double.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture)).ToArray());
    }

    [GesType("aim")]
    private sealed class AimValue
    {
        [GesConstruct]
        public AimValue(
            [GesParam("bearing", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitDegree)] double bearing,
            [GesParam("range", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitMeter)] double range,
            [GesParam("steps", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitMeter)] int steps,
            [GesParam("direction", GameEventScriptBytecodeTypeKind.Vector, GameEventScriptBytecodeInstructionUnit.UnitMeter)] GesValue direction)
        {
            Bearing = bearing;
            Range = range;
            Steps = steps;
            Direction = direction;
            Checksum = (int)(bearing + range + steps);
        }

        [GesField("bearing", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitDegree)]
        public double Bearing { get; }

        [GesField("range", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitMeter)]
        public double Range { get; }

        [GesField("steps", GameEventScriptBytecodeTypeKind.Float, GameEventScriptBytecodeInstructionUnit.UnitMeter)]
        public int Steps { get; }

        [GesField("direction", GameEventScriptBytecodeTypeKind.Vector, GameEventScriptBytecodeInstructionUnit.UnitMeter)]
        public GesValue Direction { get; }

        [GesField("checksum", GameEventScriptBytecodeTypeKind.Float)]
        public int Checksum { get; }
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

    private static bool Matches(int? expected, int? actual)
        => expected is null || expected == actual;

    private static GameEventScriptBytecodeOpCode ParseExpectedOpcode(GameEventScriptConformanceCase testCase, string name)
    {
        if (Enum.TryParse<GameEventScriptBytecodeOpCode>(name, ignoreCase: false, out var opCode))
        {
            return opCode;
        }

        Assert.Fail($"{testCase}: unknown expected opcode '{name}'.");
        return default;
    }

    private static string DescribeOpcodes(IReadOnlyList<GameEventScriptBytecodeOpCode> opCodes)
        => string.Join(
            Environment.NewLine,
            opCodes.Select((opCode, index) => $"{index:D4}: {opCode}"));

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

    private static readonly IGameEventScriptExtensionFunction MathFloor = new DelegateExtensionFunction((call, args) =>
        call.SetValue(args.Length == 1
            ? GesValue.GesFloat(Math.Floor(args.GetAsNumber(0)), args.UnitAt(0))
            : GesValue.GesNothing()));

    private static readonly IGameEventScriptExtensionFunction MathMax = new DelegateExtensionFunction((call, args) =>
    {
        if (args.Length == 0)
        {
            call.SetNothing();
            return;
        }

        var max = args.GetAsNumber(0);
        for (var index = 1; index < args.Length; index++)
        {
            max = Math.Max(max, args.GetAsNumber(index));
        }

        call.SetFloat(max);
    });

    private static readonly IGameEventScriptExtensionFunction NavShortestTurn = new DelegateExtensionFunction((call, args) =>
    {
        if (args.Length != 2)
        {
            call.SetNothing();
            return;
        }

        var from = args.GetAsNumber(0);
        var to = args.GetAsNumber(1);
        var delta = (to - from + 540d) % 360d - 180d;
        call.SetFloat(delta, GameEventScriptBytecodeInstructionUnit.UnitDegree);
    });

    private static readonly IGameEventScriptExtensionFunction NavIsNorth = new DelegateExtensionFunction((call, args) =>
    {
        if (args.Length != 1)
        {
            call.SetBoolean(false);
            return;
        }

        var value = args.GetAsNumber(0);
        var wrapped = ((value % 360d) + 360d) % 360d;
        call.SetBoolean(wrapped is <= 45d or >= 315d);
    });

    private static readonly IGameEventScriptExtensionFunction TestVectorSum = new DelegateExtensionFunction((call, args) =>
    {
        if (args.Length != 1)
        {
            call.SetNothing();
            return;
        }

        switch (args.KindAt(0))
        {
            case GameEventScriptBytecodeTypeKind.Vector:
                call.SetFloat(args.GetX(0) + args.GetY(0) + args.GetZ(0), args.UnitAt(0));
                break;
            default:
                call.SetNothing();
                break;
        }
    });

    private static readonly IGameEventScriptExtensionFunction TestEcho = new DelegateExtensionFunction((call, args) =>
    {
        if (args.Length == 1)
        {
            call.SetValue(args[0]);
        }
        else
        {
            call.SetNothing();
        }
    });

    private static readonly IGameEventScriptExtensionFunction TestTruth = new DelegateExtensionFunction((call, _) =>
        call.SetBoolean(true));

    private static readonly IGameEventScriptExtensionFunction TestFail = new DelegateExtensionFunction((_, _) =>
        throw new InvalidOperationException("Configured conformance extension failure."));

    private GameEventScriptConformanceExtensionRegistry()
    {
    }

    public IGameEventScriptExtensionFunction? Resolve(GameEventScriptExtensionReference reference)
    {
        if (string.Equals(reference.ExtensionName, "math", StringComparison.Ordinal) &&
            string.Equals(reference.FunctionName, "floor", StringComparison.Ordinal) &&
            reference.ArgumentLabels.Count == 1 &&
            IsUnlabeled(reference.ArgumentLabels[0]))
        {
            return MathFloor;
        }

        if (string.Equals(reference.ExtensionName, "math", StringComparison.Ordinal) &&
            string.Equals(reference.FunctionName, "max", StringComparison.Ordinal) &&
            reference.ArgumentLabels.Count > 0 &&
            reference.ArgumentLabels.All(IsUnlabeled))
        {
            return MathMax;
        }

        if (string.Equals(reference.ExtensionName, "nav", StringComparison.Ordinal) &&
            string.Equals(reference.FunctionName, "shortestTurn", StringComparison.Ordinal) &&
            reference.ArgumentLabels.SequenceEqual(["from", "to"], StringComparer.Ordinal))
        {
            return NavShortestTurn;
        }

        if (string.Equals(reference.ExtensionName, "nav", StringComparison.Ordinal) &&
            string.Equals(reference.FunctionName, "isNorth", StringComparison.Ordinal) &&
            reference.ArgumentLabels.Count == 1)
        {
            return NavIsNorth;
        }

        if (string.Equals(reference.ExtensionName, "test", StringComparison.Ordinal) &&
            string.Equals(reference.FunctionName, "vectorSum", StringComparison.Ordinal) &&
            reference.ArgumentLabels.Count == 1 &&
            IsUnlabeled(reference.ArgumentLabels[0]))
        {
            return TestVectorSum;
        }

        if (string.Equals(reference.ExtensionName, "test", StringComparison.Ordinal) &&
            string.Equals(reference.FunctionName, "echo", StringComparison.Ordinal) &&
            reference.ArgumentLabels.Count == 1 &&
            IsUnlabeled(reference.ArgumentLabels[0]))
        {
            return TestEcho;
        }

        if (string.Equals(reference.ExtensionName, "test", StringComparison.Ordinal) &&
            string.Equals(reference.FunctionName, "truth", StringComparison.Ordinal) &&
            reference.ArgumentLabels.Count == 0)
        {
            return TestTruth;
        }

        if (string.Equals(reference.ExtensionName, "test", StringComparison.Ordinal) &&
            string.Equals(reference.FunctionName, "fail", StringComparison.Ordinal) &&
            reference.ArgumentLabels.Count == 0)
        {
            return TestFail;
        }

        return null;
    }

    private static bool IsUnlabeled(string label)
        => string.Equals(label, GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal);

    private delegate void ExtensionInvoke(GesExtensionCall call, GesValueArguments arguments);

    private sealed class DelegateExtensionFunction(ExtensionInvoke invoke) : IGameEventScriptExtensionFunction
    {
        public void Invoke(GesExtensionCall call)
            => invoke(call, call.Arguments);
    }
}
