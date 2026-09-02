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
        Assert.HasCount(40, documents);
        Assert.AreEqual(1016, documents.Sum(document => document.Cases.Count));
        Assert.AreEqual(1009, documents.Sum(document => document.Cases.Count(testCase => testCase.Kind != ConformanceTestKind.BytecodeSnapshot)));
        Assert.AreEqual(7, documents.Sum(document => document.Cases.Count(testCase => testCase.Kind == ConformanceTestKind.BytecodeSnapshot)));
        Assert.AreEqual(40, documents.Select(document => document.SuiteId).Distinct(StringComparer.Ordinal).Count());
        Assert.AreEqual(1016, documents.SelectMany(document => document.Cases).Select(testCase => testCase.FullId).Distinct(StringComparer.Ordinal).Count());
        AssertCanonicalReadableLayout();
    }

    private static void AssertCanonicalReadableLayout()
    {
        const string caution = "\n\n> [!CAUTION]\n> **Executable Conformance Test Markdown**\n>\n" +
            "> - This file controls executable conformance tests; it is not free-form documentation.\n" +
            "> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.\n" +
            "> - Validate every edit with a conforming parser and runner.\n\n";
        var root = Path.Combine(ConformanceCSharpTestEnvironment.GetConformanceDirectory(), "Suites");
        var paths = Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories).ToArray();
        foreach (var path in paths)
        {
            var text = File.ReadAllText(path);
            var h1Start = text.IndexOf("\n# ", StringComparison.Ordinal);
            Assert.IsGreaterThanOrEqualTo(0, h1Start, $"{path} has no H1 suite title.");
            var h1End = text.IndexOf('\n', h1Start + 1);
            Assert.IsGreaterThanOrEqualTo(0, h1End, $"{path} has an unterminated H1 suite title.");
            Assert.IsTrue(text.AsSpan(h1End).StartsWith(caution, StringComparison.Ordinal), $"{path} must place the canonical caution directly after its H1.");
            var firstTestSeparator = text.IndexOf("\n---\n\n## Test: ", h1End + caution.Length, StringComparison.Ordinal);
            Assert.IsGreaterThan(h1End + caution.Length, firstTestSeparator, $"{path} needs suite prose between the caution and first test.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(text.Substring(h1End + caution.Length, firstTestSeparator - h1End - caution.Length)), $"{path} needs meaningful suite prose.");

            var tests = text.Split("\n## Test: ", StringSplitOptions.None);
            Assert.IsGreaterThan(1, tests.Length, $"{path} has no tests.");
            Assert.AreEqual(tests.Length - 1, Count(text, "\n---\n\n## Test: "), $"{path} must place a thematic break before every test.");
            foreach (var test in tests.Skip(1))
            {
                var titleEnd = test.IndexOf('\n');
                Assert.IsGreaterThan(0, titleEnd, $"{path} has an invalid test title.");
                var body = test.Substring(titleEnd);
                Assert.IsTrue(body.StartsWith("\n\nThis ", StringComparison.Ordinal), $"{path} test '{test.Substring(0, titleEnd)}' needs explanatory prose.");
                StringAssert.Contains(body, "### Case description\n\n```yaml\ngesBlock: case", $"{path} test '{test.Substring(0, titleEnd)}' needs its Case description heading.");
                if (body.Contains("\n```ges\n", StringComparison.Ordinal)) StringAssert.Contains(body, "### Source code under test\n\n```ges");
                if (body.Contains("gesBlock: expect", StringComparison.Ordinal)) StringAssert.Contains(body, "### Expectation\n\n```yaml\ngesBlock: expect");
                if (body.Contains("\n```gesa\n", StringComparison.Ordinal)) StringAssert.Contains(body, "### Expected Game Event Script Assembler\n\n```gesa");
            }
        }
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        for (var offset = 0; (offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0; offset += value.Length) count++;
        return count;
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
            var root = Path.Combine(ConformanceCSharpTestEnvironment.GetConformanceDirectory(), "Received");
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
            var root = Path.Combine(ConformanceCSharpTestEnvironment.GetConformanceDirectory(), "Received", "snapshots");
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
