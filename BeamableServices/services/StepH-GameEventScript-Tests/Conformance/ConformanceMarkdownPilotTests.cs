using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Conformance;

namespace StepH_GameEventScript_Tests.Conformance;

[TestClass]
public sealed class ConformanceMarkdownPilotTests : GameEventScriptJsonConformanceTestBase
{
    private const string PerformanceProfile = "csharp-dotnet-release-macos-arm64";

    private static readonly IReadOnlyDictionary<string, LegacyReference> LegacyReferences =
        new Dictionary<string, LegacyReference>(StringComparer.Ordinal)
        {
            ["script-api"] = new("runtime/messages-handlers.json", "handler binding creates message values and publishable calls"),
            ["compile-error"] = new("compile/build-errors.json", "module build errors preserve parser source range"),
            ["load-error"] = new("compile/binary-compiler.json", "host rejects program whose call depth exceeds its limit"),
            ["message-api"] = new("api/messages.json", "message signatures are ordered"),
            ["compile-metadata"] = new("compile/binary-compiler.json", "handler and program resource requirements include nested calls"),
            ["bytecode"] = new("compile/bytecode-lowering.json", "emit arguments read existing registers without temporary moves"),
            ["performance"] = new("performance/core.json", "math integer hot path"),
            ["bytecode-snapshot"] = new("performance/core.json", "math integer hot path", IsSnapshot: true)
        };

    private static readonly Lazy<ConformanceDocument> PilotDocument = new(() =>
        ConformanceMarkdownParser.Parse(File.ReadAllBytes(Path.Combine(GetSourceDirectory(), "SpecsMarkdown", "Pilot", "representative.md"))));

    public static IEnumerable<object[]> Cases()
        => ConformanceMSTestAdapter.Cases(PilotDocument.Value);

    public static string DisplayName(MethodInfo method, object[] data)
        => ConformanceMSTestAdapter.DisplayName(method, data);

    [TestMethod]
    [DynamicData(nameof(Cases), DynamicDataDisplayName = nameof(DisplayName))]
    public void RepresentativeMarkdownCaseMatchesJsonAndRuns(ConformanceDocument document, string caseId)
    {
        var markdownCase = document.Cases.Single(testCase => string.Equals(testCase.Id, caseId, StringComparison.Ordinal));
        var legacyReference = LegacyReferences[caseId];
        var legacyCase = GameEventScriptConformanceRunner
            .AllConformanceCases(SpecDirectory, legacyReference.RelativeFile)
            .Single(testCase => string.Equals(testCase.Test.Name, legacyReference.CaseName, StringComparison.Ordinal));

        AssertNormalizedModelParity(markdownCase, legacyCase, legacyReference.IsSnapshot);
        RunLegacyReference(legacyCase, markdownCase, legacyReference.IsSnapshot);

        var result = ConformanceRunner.RunCase(
            document,
            caseId,
            CreateEnvironment(),
            new ConformanceRunnerOptions { IncludeActualAssemblerOnSuccess = true });

        ConformanceMSTestAdapter.AssertPassed(result, TestContext);
    }

    [TestMethod]
    public void RepresentativeMarkdownDocumentProducesReportsAndStableReceivedBytes()
    {
        var document = PilotDocument.Value;
        var report = ConformanceRunner.RunDocument(
            document,
            CreateEnvironment(),
            new ConformanceRunnerOptions { IncludeActualAssemblerOnSuccess = true });

        Assert.AreEqual(ConformanceCaseStatus.Passed, report.Status);
        Assert.AreEqual(8, report.Summary.Total);
        Assert.AreEqual(8, report.Summary.Passed);
        StringAssert.Contains(ConformanceResultJsonWriter.ToText(report), "\"id\": \"migration-pilot/bytecode-snapshot\"");
        StringAssert.Contains(ConformanceMarkdownReportWriter.ToText(report), "| `migration-pilot/performance` | math integer hot path | performance | scenario | passed |");
        CollectionAssert.AreEqual(document.Source.Utf8Bytes.ToArray(), ConformanceReceivedMarkdownWriter.ToArray(document, report));
    }

    private static void AssertNormalizedModelParity(
        ConformanceCase markdownCase,
        GameEventScriptConformanceCase legacyCase,
        bool isSnapshot)
    {
        Assert.AreEqual(legacyCase.Test.Name, isSnapshot ? "math integer hot path" : markdownCase.Title);
        if (!isSnapshot) Assert.AreEqual(ParseKind(legacyCase.Test.Kind!), markdownCase.Kind);
        Assert.AreEqual(legacyCase.Level == "atomic" ? ConformanceTestLevel.Atomic : ConformanceTestLevel.Scenario, markdownCase.Level);
        Assert.AreEqual(legacyCase.Test.BinaryRoundTrip, markdownCase.Compile.BinaryRoundTrip);

        var legacySources = GetLegacySources(legacyCase.Test);
        if (markdownCase.Kind == ConformanceTestKind.MessageApi) Assert.HasCount(0, markdownCase.Sources);
        else Assert.HasCount(legacySources.Count, markdownCase.Sources);
        for (var index = 0; index < markdownCase.Sources.Count; index++)
        {
            Assert.AreEqual(legacySources[index].Name, markdownCase.Sources[index].Name);
            Assert.AreEqual(legacySources[index].Text, markdownCase.Sources[index].Text);
        }

        var legacySteps = legacyCase.Test.Steps ?? [];
        if (!isSnapshot && legacyCase.Test.Kind is ("scriptApi" or "performance"))
        {
            Assert.HasCount(legacySteps.Count, markdownCase.Steps);
            for (var index = 0; index < legacySteps.Count; index++)
            {
                Assert.AreEqual(legacySteps[index].Input.GetProperty("name").GetString(), markdownCase.Steps[index].Receive);
                Assert.HasCount(legacySteps[index].ExpectedPublished?.Count ?? 0, markdownCase.Steps[index].Expectation.Local);
                Assert.HasCount(legacySteps[index].ExpectedOutboundPublished?.Count ?? 0, markdownCase.Steps[index].Expectation.Outbound);
            }
        }

        switch (markdownCase.Kind)
        {
            case ConformanceTestKind.CompileError:
            case ConformanceTestKind.LoadError:
                Assert.AreEqual(legacyCase.Test.ExpectedError!.Phase, markdownCase.Expectation.Error!.Phase);
                Assert.AreEqual(legacyCase.Test.ExpectedError.Code, markdownCase.Expectation.Error.Code);
                Assert.AreEqual(legacyCase.Test.ExpectedError.Symbol, markdownCase.Expectation.Error.Symbol);
                Assert.AreEqual(LowerCamel(legacyCase.Test.ExpectedError.SymbolKind), markdownCase.Expectation.Error.SymbolKind);
                Assert.AreEqual(legacyCase.Test.ExpectedError.SourceName, markdownCase.Expectation.Error.SourceName);
                break;
            case ConformanceTestKind.MessageApi:
                Assert.AreEqual(legacyCase.Test.Signature!.Name, markdownCase.MessageApi!.SignatureName);
                CollectionAssert.AreEqual(legacyCase.Test.Signature.Parameters!, markdownCase.MessageApi.Parameters.ToArray());
                Assert.AreEqual(legacyCase.Test.ExpectedSignatureId, markdownCase.Expectation.MessageApi!.SignatureId);
                Assert.AreEqual(legacyCase.Test.ExpectedMessageSignatureId, markdownCase.Expectation.MessageApi.MessageSignatureId);
                break;
            case ConformanceTestKind.CompileMetadata:
                Assert.AreEqual(
                    legacyCase.Test.ExpectedProgramResources!.RequiredRegisterCount,
                    (int?)markdownCase.Expectation.Metadata!.ProgramResources!.RequiredRegisterCount);
                Assert.AreEqual(
                    legacyCase.Test.ExpectedProgramResources.RequiredCallStackDepth,
                    (int?)markdownCase.Expectation.Metadata.ProgramResources.RequiredCallStackDepth);
                Assert.HasCount(legacyCase.Test.ExpectedHandlerResources!.Count, markdownCase.Expectation.Metadata.HandlerResources);
                break;
            case ConformanceTestKind.Bytecode:
                CollectionAssert.AreEqual(legacyCase.Test.ExpectedOpcodes!.Contains!, markdownCase.Expectation.Opcodes!.Contains.ToArray());
                CollectionAssert.AreEqual(legacyCase.Test.ExpectedOpcodes.NotContains!, markdownCase.Expectation.Opcodes.Excludes.ToArray());
                break;
            case ConformanceTestKind.Performance:
                Assert.AreEqual((uint)legacyCase.Test.Iterations!.Value, markdownCase.Performance!.Iterations);
                Assert.AreEqual((uint)legacyCase.Test.WarmupIterations!.Value, markdownCase.Performance.WarmupIterations);
                Assert.AreEqual((uint)3, markdownCase.Performance.CompileWarmupIterations);
                Assert.HasCount(2, markdownCase.Expectation.Performance!.Profiles.Single(profile => profile.Id == PerformanceProfile).Metrics);
                break;
        }
    }

    private static void RunLegacyReference(
        GameEventScriptConformanceCase legacyCase,
        ConformanceCase markdownCase,
        bool isSnapshot)
    {
        if (isSnapshot)
        {
            var actual = NormalizeLf(GameEventScriptConformanceRunner.CompileBytecodeForTest(legacyCase.Test).Dump());
            Assert.AreEqual(NormalizeLf(markdownCase.ExpectedAssembler!), actual);
            return;
        }

        switch (legacyCase.Test.Kind)
        {
            case "scriptApi":
            case "performance":
                var outcome = RunScriptApiCase(legacyCase);
                Assert.IsTrue(outcome.Passed, outcome.Detail);
                break;
            case "compileError":
                GameEventScriptConformanceRunner.RunCompileErrorTest(legacyCase);
                break;
            case "loadError":
                GameEventScriptConformanceRunner.RunLoadErrorTest(legacyCase);
                break;
            case "messageApi":
                GameEventScriptConformanceRunner.RunMessageApiTest(legacyCase);
                break;
            case "compileMetadata":
                GameEventScriptConformanceRunner.RunCompileMetadataTest(legacyCase);
                break;
            case "bytecode":
                GameEventScriptConformanceRunner.RunBytecodeOpcodeTest(legacyCase);
                break;
            default:
                Assert.Fail("Unsupported pilot reference kind '" + legacyCase.Test.Kind + "'.");
                break;
        }
    }

    private static ConformanceRunnerEnvironment CreateEnvironment()
        => new(
            "steph.ges.conformance.csharp",
            "0.1.0",
            "steph.ges.csharp",
            "0.1.0",
            ["bytecode-snapshot", "compiler", "host", "message-api", "observer", "performance", "program-binary", "publish-sink", "vm"],
            GameEventScriptConformanceRunner.ExternalTypeCatalog,
            GameEventScriptConformanceExtensionRegistry.Instance,
            GameEventScriptConformanceRunner.ExternalTypeRegistry,
            PerformanceProfile,
            ReferencePerformanceProvider.Instance);

    private static IReadOnlyList<(string Name, string Text)> GetLegacySources(GameEventScriptConformanceTest test)
    {
        if (test.Scripts is { Count: > 0 })
            return test.Scripts.Select(source => (source.SourceName ?? test.Name + ".ges", source.Text!)).ToArray();
        if (test.Programs is { Count: > 0 })
            return test.Programs.Select(source => (source.SourceName ?? test.Name + ".ges", source.Text!)).ToArray();
        return [(test.Name + ".ges", test.Script!)];
    }

    private static ConformanceTestKind ParseKind(string kind)
        => kind switch
        {
            "scriptApi" => ConformanceTestKind.ScriptApi,
            "compileError" => ConformanceTestKind.CompileError,
            "loadError" => ConformanceTestKind.LoadError,
            "messageApi" => ConformanceTestKind.MessageApi,
            "compileMetadata" => ConformanceTestKind.CompileMetadata,
            "bytecode" => ConformanceTestKind.Bytecode,
            "performance" => ConformanceTestKind.Performance,
            _ => throw new InvalidOperationException("Unknown legacy kind '" + kind + "'.")
        };

    private static string NormalizeLf(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string? LowerCamel(string? value)
        => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value.Substring(1);

    private static string GetSourceDirectory([CallerFilePath] string sourceFile = "")
        => Path.GetDirectoryName(sourceFile)!;

    private sealed record LegacyReference(string RelativeFile, string CaseName, bool IsSnapshot = false);

    private sealed class ReferencePerformanceProvider : IConformancePerformanceProvider
    {
        internal static ReferencePerformanceProvider Instance { get; } = new();

        public ConformancePerformanceMeasurement Measure(ConformanceCase testCase, string profileId)
        {
            var profile = testCase.Expectation.Performance!.Profiles.Single(value => value.Id == profileId);
            return new ConformancePerformanceMeasurement(
                profile.Metrics.Select(metric => new ConformanceMeasuredMetric(metric.Id, metric.Reference, metric.Unit)).ToArray());
        }
    }
}
