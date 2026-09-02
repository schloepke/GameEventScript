using System.Reflection;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Conformance;

namespace StepH_GameEventScript_Tests.Conformance;

[TestClass]
public sealed class ConformanceMarkdownCorpusTests
{
    private static readonly Lazy<IReadOnlyList<ConformanceDocument>> Documents = new(LoadDocuments);

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> Cases()
    {
        foreach (var document in Documents.Value)
            foreach (var data in ConformanceMSTestAdapter.Cases(document))
                yield return data;
    }

    public static string DisplayName(MethodInfo method, object[] data)
        => ConformanceMSTestAdapter.DisplayName(method, data);

    [TestMethod]
    [DynamicData(nameof(Cases), DynamicDataDisplayName = nameof(DisplayName))]
    public void MigratedMarkdownCasePasses(ConformanceDocument document, string caseId)
    {
        var result = ConformanceRunner.RunCase(
            document,
            caseId,
            Environment(),
            new ConformanceRunnerOptions { IncludeActualAssemblerOnSuccess = true });
        ConformanceMSTestAdapter.AssertPassed(result, TestContext);
    }

    [TestMethod]
    public void MigratedMarkdownCorpusHasStableUniqueIdentityAndExpectedCounts()
    {
        var documents = Documents.Value;
        Assert.HasCount(34, documents);
        Assert.AreEqual(961, documents.Sum(document => document.Cases.Count));
        Assert.AreEqual(956, documents.Sum(document => document.Cases.Count(testCase => testCase.Kind != ConformanceTestKind.BytecodeSnapshot)));
        Assert.AreEqual(5, documents.Sum(document => document.Cases.Count(testCase => testCase.Kind == ConformanceTestKind.BytecodeSnapshot)));
        Assert.AreEqual(34, documents.Select(document => document.SuiteId).Distinct(StringComparer.Ordinal).Count());
        Assert.AreEqual(961, documents.SelectMany(document => document.Cases).Select(testCase => testCase.FullId).Distinct(StringComparer.Ordinal).Count());

        var report = ConformanceRunner.RunCorpus(documents, Environment());
        Assert.AreEqual(ConformanceCaseStatus.Passed, report.Status);
        Assert.AreEqual(961, report.Summary.Passed);
        Assert.AreEqual(0, report.Summary.Failed);
        Assert.AreEqual(0, report.Summary.Error);
        Assert.AreEqual(0, report.Summary.Skipped);
    }

    internal static ConformanceRunnerEnvironment Environment()
        => new(
            "steph.ges.conformance.csharp", "0.1.0", "steph.ges.csharp", "0.1.0",
            ["bytecode-snapshot", "compiler", "external-types", "host", "message-api", "native-handlers", "observer", "performance", "program-binary", "publish-sink", "vm"],
            GameEventScriptConformanceRunner.ExternalTypeCatalog,
            GameEventScriptConformanceExtensionRegistry.Instance,
            GameEventScriptConformanceRunner.ExternalTypeRegistry,
            ConformanceJsonToMarkdownMigrator.PerformanceProfile,
            EchoPerformanceProvider.Instance);

    private static IReadOnlyList<ConformanceDocument> LoadDocuments()
    {
        var root = Path.Combine(GetSourceDirectory(), "SpecsMarkdown", "Migrated");
        return Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => ConformanceMarkdownParser.Parse(File.ReadAllBytes(path)))
            .ToArray();
    }

    private static string GetSourceDirectory([CallerFilePath] string sourceFile = "")
        => Path.GetDirectoryName(sourceFile)!;

    private sealed class EchoPerformanceProvider : IConformancePerformanceProvider
    {
        internal static EchoPerformanceProvider Instance { get; } = new();

        public ConformancePerformanceMeasurement Measure(ConformanceCase testCase, string profileId)
        {
            var profile = testCase.Expectation.Performance!.Profiles.Single(value => value.Id == profileId);
            return new ConformancePerformanceMeasurement(profile.Metrics
                .Select(metric => new ConformanceMeasuredMetric(metric.Id, metric.Reference, metric.Unit)).ToArray());
        }
    }
}
