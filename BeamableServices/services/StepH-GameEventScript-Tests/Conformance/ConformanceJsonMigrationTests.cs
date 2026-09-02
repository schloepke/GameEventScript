using System.Runtime.CompilerServices;
using StepH.GameEventScript.Conformance;

namespace StepH_GameEventScript_Tests.Conformance;

[TestClass]
[Ignore("The one-time migration has completed; this temporary parity harness is removed in step 5.7.")]
public sealed class ConformanceJsonMigrationTests : GameEventScriptJsonConformanceTestBase
{
    private const string WriteEnvironmentVariable = "GES_WRITE_MIGRATED_CONFORMANCE";

    [TestMethod]
    public void EntireLegacyCorpusMigratesDeterministicallyAndRunsWithParity()
    {
        var first = Migrate();
        var second = Migrate();
        Assert.HasCount(34, first);
        Assert.AreEqual(956, first.Sum(suite => suite.LegacyCaseCount));
        Assert.AreEqual(5, first.Sum(suite => suite.SnapshotCount));
        CollectionAssert.AreEqual(
            first.Select(suite => suite.RelativePath + "\n" + suite.Markdown).ToArray(),
            second.Select(suite => suite.RelativePath + "\n" + suite.Markdown).ToArray());

        var legacyPassed = 0;
        var markdownPassed = 0;
        foreach (var suite in first)
        {
            ConformanceDocument document;
            try { document = ConformanceMarkdownParser.Parse(suite.Markdown); }
            catch (ConformanceParseException exception)
            {
                Assert.Fail(suite.RelativePath + ": " + exception.Message + " at line " + exception.Diagnostics[0].Range.Line);
                throw;
            }
            Assert.HasCount(suite.LegacyCaseCount + suite.SnapshotCount, document.Cases, suite.RelativePath);
            var legacyRelative = suite.RelativePath[..^".md".Length] + ".json";
            var legacyCases = GameEventScriptConformanceRunner.AllConformanceCases(SpecDirectory, legacyRelative);
            for (var index = 0; index < legacyCases.Count; index++)
            {
                var legacy = legacyCases[index];
                var migrated = document.Cases[index * (suite.SnapshotCount == 0 ? 1 : 2)];
                Assert.AreEqual(legacy.Test.Name == "message member and index access" &&
                                legacyRelative == "runtime/atomic/member-index-access.json" && index == 17
                    ? "message handler member and index access"
                    : legacy.Test.Name, migrated.Title);
                Assert.AreEqual(legacy.Level, migrated.Level == ConformanceTestLevel.Atomic ? "atomic" : "scenario");
                Assert.HasCount(LegacySourceCount(legacy.Test), migrated.Sources);
                RunLegacy(legacy);
                legacyPassed++;

                var result = ConformanceRunner.RunCase(document, migrated.Id, Environment());
                Assert.AreEqual(ConformanceCaseStatus.Passed, result.Status,
                    migrated.FullId + ": " + result.Code + " " + string.Join("; ", result.Mismatches.Select(value => value.Path + " expected " + value.Expected + " actual " + value.Actual)));
                markdownPassed++;

                if (suite.SnapshotCount == 0) continue;
                var snapshot = document.Cases[index * 2 + 1];
                var snapshotResult = ConformanceRunner.RunCase(document, snapshot.Id, Environment(), new ConformanceRunnerOptions { IncludeActualAssemblerOnSuccess = true });
                Assert.AreEqual(ConformanceCaseStatus.Passed, snapshotResult.Status,
                    snapshot.FullId + ": " + snapshotResult.Code + " " +
                    string.Join("; ", snapshotResult.Mismatches.Select(value => value.Path + " expected " + value.Expected + " actual " + value.Actual)));
            }
        }

        Assert.AreEqual(956, legacyPassed);
        Assert.AreEqual(956, markdownPassed);
    }

    [TestMethod]
    public void WritesCheckedInCorpusOnlyWhenExplicitlyRequested()
    {
        if (!string.Equals(System.Environment.GetEnvironmentVariable(WriteEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set " + WriteEnvironmentVariable + "=1 to regenerate the checked-in migration output.");
            return;
        }

        var target = Path.Combine(GetSourceDirectory(), "SpecsMarkdown", "Migrated");
        foreach (var suite in Migrate())
        {
            var path = Path.Combine(target, suite.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, suite.Markdown, new System.Text.UTF8Encoding(false));
        }
    }

    private void RunLegacy(GameEventScriptConformanceCase testCase)
    {
        switch (testCase.Test.Kind)
        {
            case "scriptApi":
            case "performance":
                var outcome = RunScriptApiCase(testCase);
                Assert.IsTrue(outcome.Passed, testCase + ": " + outcome.Detail);
                break;
            case "compileError": GameEventScriptConformanceRunner.RunCompileErrorTest(testCase); break;
            case "loadError": GameEventScriptConformanceRunner.RunLoadErrorTest(testCase); break;
            case "messageApi": GameEventScriptConformanceRunner.RunMessageApiTest(testCase); break;
            case "compileMetadata": GameEventScriptConformanceRunner.RunCompileMetadataTest(testCase); break;
            case "bytecode": GameEventScriptConformanceRunner.RunBytecodeOpcodeTest(testCase); break;
            default: Assert.Fail("Unsupported legacy kind '" + testCase.Test.Kind + "'."); break;
        }
    }

    private static ConformanceRunnerEnvironment Environment()
        => new(
            "steph.ges.conformance.csharp", "0.1.0", "steph.ges.csharp", "0.1.0",
            ["bytecode-snapshot", "compiler", "external-types", "host", "message-api", "native-handlers", "observer", "performance", "program-binary", "publish-sink", "vm"],
            GameEventScriptConformanceRunner.ExternalTypeCatalog,
            GameEventScriptConformanceExtensionRegistry.Instance,
            GameEventScriptConformanceRunner.ExternalTypeRegistry,
            ConformanceJsonToMarkdownMigrator.PerformanceProfile,
            EchoPerformanceProvider.Instance);

    private static IReadOnlyList<ConformanceJsonToMarkdownMigrator.MigratedSuite> Migrate()
    {
        var directory = GetSourceDirectory();
        return ConformanceJsonToMarkdownMigrator.MigrateCorpus(
            Path.Combine(directory, "Specs"),
            Path.Combine(directory, "PerformanceReport.reference.txt"),
            Path.Combine(directory, "PerformanceBinaryDump.reference.gesa"));
    }

    private static int LegacySourceCount(GameEventScriptConformanceTest test)
        => test.Script is not null ? 1 : test.Scripts?.Count ?? test.Programs?.Count ?? 0;

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
