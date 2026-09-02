using System.Reflection;
using StepH.GameEventScript.Conformance;

namespace StepH_GameEventScript_Tests.Conformance;

[TestClass]
public sealed class ConformanceMarkdownCorpusTests
{
    private static readonly object ResultLock = new();
    private static readonly Dictionary<string, ConformanceCaseResult> Results = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, ConformanceCaseResult> SnapshotResults = new(StringComparer.Ordinal);

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> Cases()
    {
        foreach (var document in ConformanceCSharpTestEnvironment.Documents)
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
            ConformanceCSharpTestEnvironment.Deterministic(),
            new ConformanceRunnerOptions { IncludeActualAssemblerOnSuccess = true });
        Record(document, result);
        ConformanceMSTestAdapter.AssertPassed(result, TestContext);
    }

    [TestMethod]
    public void MigratedMarkdownCorpusHasStableUniqueIdentityAndExpectedCounts()
    {
        var documents = ConformanceCSharpTestEnvironment.Documents;
        Assert.HasCount(34, documents);
        Assert.AreEqual(961, documents.Sum(document => document.Cases.Count));
        Assert.AreEqual(956, documents.Sum(document => document.Cases.Count(testCase => testCase.Kind != ConformanceTestKind.BytecodeSnapshot)));
        Assert.AreEqual(5, documents.Sum(document => document.Cases.Count(testCase => testCase.Kind == ConformanceTestKind.BytecodeSnapshot)));
        Assert.AreEqual(34, documents.Select(document => document.SuiteId).Distinct(StringComparer.Ordinal).Count());
        Assert.AreEqual(961, documents.SelectMany(document => document.Cases).Select(testCase => testCase.FullId).Distinct(StringComparer.Ordinal).Count());
    }

    [ClassCleanup]
    public static void WriteCorpusReport()
    {
        lock (ResultLock)
        {
            var documents = ConformanceCSharpTestEnvironment.Documents;
            var caseCount = documents.Sum(document => document.Cases.Count);
            if (Results.Count != caseCount) return;

            var ordered = documents
                .SelectMany(document => document.Cases)
                .Select(testCase => Results[testCase.FullId])
                .ToArray();
            var report = ConformanceCSharpTestEnvironment.Report(
                ConformanceCSharpTestEnvironment.Deterministic(),
                ordered);
            var root = Path.Combine(ConformanceCSharpTestEnvironment.GetSourceDirectory(), "SpecsMarkdown", "Received");
            Directory.CreateDirectory(root);
            File.WriteAllBytes(Path.Combine(root, "ConformanceResults.json"), ConformanceResultJsonWriter.ToArray(report));
            File.WriteAllText(Path.Combine(root, "ConformanceReport.md"), ConformanceMarkdownReportWriter.ToText(report));
        }
    }

    private static void Record(ConformanceDocument document, ConformanceCaseResult result)
    {
        lock (ResultLock)
        {
            Results[result.Id] = ResultForCorpusReport(document, result);
            if (result.Kind != ConformanceTestKind.BytecodeSnapshot) return;
            SnapshotResults[result.Id] = result;
            var ordered = document.Cases
                .Where(testCase => SnapshotResults.ContainsKey(testCase.FullId))
                .Select(testCase => SnapshotResults[testCase.FullId])
                .ToArray();
            var environment = ConformanceCSharpTestEnvironment.Deterministic();
            var report = ConformanceCSharpTestEnvironment.Report(environment, ordered);
            var root = Path.Combine(ConformanceCSharpTestEnvironment.GetSourceDirectory(), "SpecsMarkdown", "Received", "snapshots");
            Directory.CreateDirectory(root);
            File.WriteAllBytes(
                Path.Combine(root, document.SuiteId + ".received.md"),
                ConformanceReceivedMarkdownWriter.ToArray(document, report));
        }
    }

    private static ConformanceCaseResult ResultForCorpusReport(
        ConformanceDocument document,
        ConformanceCaseResult result)
    {
        if (result.Kind != ConformanceTestKind.Performance) return result;
        var testCase = document.Cases.Single(value => string.Equals(value.Id, result.CaseId, StringComparison.Ordinal));
        return new ConformanceCaseResult(
            testCase,
            result.Status,
            result.Code,
            result.MissingCapabilities,
            result.Mismatches,
            result.Diagnostics,
            result.RuntimeLimits,
            result.ActualAssembler,
            performance: null,
            result.TechnicalDetails);
    }

}
