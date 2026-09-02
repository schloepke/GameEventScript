using System.Reflection;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.Runtime.VM;
using StepH.GameEventScript.Runtime;

namespace StepH_GameEventScript_Tests.Conformance;

[TestClass]
[Ignore("Legacy JSON sources were retired after the verified Markdown migration; infrastructure is removed in step 5.7.")]
public sealed class GameEventScriptJsonConformanceTests : GameEventScriptJsonConformanceTestBase
{
    [TestMethod]
    [DynamicData(nameof(ApiMessagesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void ApiMessages(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(CompileBuildErrorsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void CompileBuildErrors(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(CompileSyntaxErrorsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void CompileSyntaxErrors(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(CompileBytecodeLoweringCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void CompileBytecodeLowering(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(CompileBinaryCompilerCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void CompileBinaryCompiler(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicMathMatrixCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicMathMatrix(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicTrigNavigationCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicTrigNavigation(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicEqualityCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicEquality(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicCompareCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicCompare(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicExternalAccessCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicExternalAccess(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicMemberIndexAccessCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicMemberIndexAccess(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicBooleanLogicCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicBooleanLogic(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicCreateValuesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicCreateValues(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicCastsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicCasts(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicCustomTypesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicCustomTypes(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicCollectionOperatorsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicCollectionOperators(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicControlFlowCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicControlFlow(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicSeriesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicSeries(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicIteratorCoreCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicIteratorCore(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicIteratorTerminalsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicIteratorTerminals(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicSortGroupDistinctCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicSortGroupDistinct(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicShuffleReverseCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicShuffleReverse(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicPatternsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicPatterns(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeAtomicRandomCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeAtomicRandom(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeCollectionsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeCollections(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeControlFlowCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeControlFlow(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeExtensionsSequencesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeExtensionsSequences(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeHostDispatchCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeHostDispatch(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeMessagesHandlersCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeMessagesHandlers(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimePredicatesFunctionsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimePredicatesFunctions(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimePublishTagsCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimePublishTags(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeRandomDiceRangesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeRandomDiceRanges(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    [TestMethod]
    [DynamicData(nameof(RuntimeTypesAndValuesCases), DynamicDataDisplayName = nameof(GetConformanceCaseDisplayName))]
    public void RuntimeTypesAndValues(GameEventScriptConformanceCase testCase)
        => RunJsonConformanceCase(testCase);

    public static string GetConformanceCaseDisplayName(MethodInfo methodInfo, object[] data)
        => FormatConformanceCaseDisplayName(methodInfo, data);
}

[TestClass]
[Ignore("Legacy combined JSON performance runner is replaced in step 5.7.")]
public sealed class GameEventScriptJsonPerformanceTests : GameEventScriptJsonConformanceTestBase
{
    private static readonly bool RunPerformanceReport = true;
    private static readonly bool ComparePerformanceReportToReference = true;
    private static readonly double PerformanceElapsedRegressionTolerance = 0.15d;
    private static readonly double PerformanceElapsedMinimumToleranceMilliseconds = 1d;
    private static readonly int PerformanceIterations = 1_000;
    private static readonly int PerformanceWarmupIterations = 10;
    private static readonly int PerformanceCompileWarmupIterations = 3;
    private const int DefaultIterations = 1_000;
    private const int DefaultWarmupIterations = 100;

    [TestMethod]
    [TestCategory("Performance")]
    [TestCategory("Explicit")]
    [DoNotParallelize]
    public void PerformanceReport()
    {
        var testCases = GameEventScriptConformanceRunner.AllConformanceCases(Path.Combine(SpecDirectory, "performance"));
        Assert.IsGreaterThan(0, testCases.Count);

        if (!RunPerformanceReport)
        {
            TestContext.WriteLine(
                "Explicit performance report is disabled. Set RunPerformanceReport=true in this test and run this test explicitly to execute it manually. " +
                "Use PerformanceIterations and PerformanceWarmupIterations in this test to override JSON iteration counts. " +
                "Copy PerformanceReport.current.txt to PerformanceReport.reference.txt to approve a new baseline. " +
                $"Loaded {testCases.Count} performance case(s).");
            return;
        }

        var report = new StringBuilder();
        var binaryDumpReport = new StringBuilder();
        report.AppendLine("# GameEventScript Performance Conformance Report");
        report.AppendLine();
        report.AppendLine($"cases={testCases.Count}");
        report.AppendLine($"performanceIterations={PerformanceIterations}");
        report.AppendLine($"performanceWarmupIterations={PerformanceWarmupIterations}");
        report.AppendLine($"performanceCompileWarmupIterations={PerformanceCompileWarmupIterations}");
        report.AppendLine();
        binaryDumpReport.AppendLine("# GameEventScript Performance Binary Dump Report");
        binaryDumpReport.AppendLine();
        binaryDumpReport.AppendLine($"cases={testCases.Count}");
        binaryDumpReport.AppendLine();
        foreach (var testCase in testCases)
        {
            RunPerformanceCase(testCase, report, binaryDumpReport);
        }

        var current = report.ToString();
        var referencePath = GetPerformanceReferencePath();
        var currentPath = GetPerformanceCurrentPath(referencePath);
        File.WriteAllText(currentPath, current);
        var binaryDumpCurrent = binaryDumpReport.ToString();
        var binaryDumpReferencePath = GetPerformanceBinaryDumpReferencePath();
        var binaryDumpCurrentPath = GetPerformanceBinaryDumpCurrentPath(binaryDumpReferencePath);
        File.WriteAllText(binaryDumpCurrentPath, binaryDumpCurrent);

        TestContext.WriteLine($"Performance current:   {currentPath}");
        TestContext.WriteLine($"Performance reference: {referencePath}");
        TestContext.WriteLine($"Performance binary dump current:   {binaryDumpCurrentPath}");
        TestContext.WriteLine($"Performance binary dump reference: {binaryDumpReferencePath}");
        TestContext.WriteLine("Current:");
        TestContext.WriteLine(current);

        var reference = File.Exists(referencePath) ? File.ReadAllText(referencePath) : string.Empty;
        TestContext.WriteLine("Reference:");
        TestContext.WriteLine(reference.Length == 0 ? "<missing>" : reference);
        var binaryDumpReference = File.Exists(binaryDumpReferencePath) ? File.ReadAllText(binaryDumpReferencePath) : string.Empty;
        TestContext.WriteLine("Binary dump current:");
        TestContext.WriteLine(binaryDumpCurrent);
        TestContext.WriteLine("Binary dump reference:");
        TestContext.WriteLine(binaryDumpReference.Length == 0 ? "<missing>" : binaryDumpReference);

        if (!ComparePerformanceReportToReference)
        {
            TestContext.WriteLine("Performance reference comparison is disabled. Set ComparePerformanceReportToReference=true to assert the snapshot.");
            return;
        }

        if (!File.Exists(referencePath))
        {
            Assert.Fail($"Performance reference does not exist. Copy current to reference to approve it: {currentPath} -> {referencePath}");
        }

        if (!File.Exists(binaryDumpReferencePath))
        {
            Assert.Fail($"Performance binary dump reference does not exist. Copy current to reference to approve it: {binaryDumpCurrentPath} -> {binaryDumpReferencePath}");
        }

        if (!PerformanceReportMatchesReference(reference, current, out var performanceDiff))
        {
            Assert.Fail(
                $"Performance report regressed. Current: {currentPath}; Reference: {referencePath}{Environment.NewLine}" +
                performanceDiff);
        }

        if (!string.Equals(
                binaryDumpReference.ReplaceLineEndings("\n"),
                binaryDumpCurrent.ReplaceLineEndings("\n"),
                StringComparison.Ordinal))
        {
            Assert.Fail(
                $"Performance binary dump changed. Current: {binaryDumpCurrentPath}; Reference: {binaryDumpReferencePath}{Environment.NewLine}" +
                BinaryDumpDiff(binaryDumpReference, binaryDumpCurrent));
        }
    }

    private static string GetPerformanceReferencePath()
        => Path.Combine(
            Path.GetDirectoryName(SpecDirectory) ?? throw new DirectoryNotFoundException("Conformance directory was not found."),
            "PerformanceReport.reference.txt");

    private static string GetPerformanceCurrentPath(string referencePath)
        => Path.Combine(
            Path.GetDirectoryName(referencePath) ?? throw new DirectoryNotFoundException("Performance reference directory was not found."),
            "PerformanceReport.current.txt");

    private static string GetPerformanceBinaryDumpReferencePath()
        => Path.Combine(
            Path.GetDirectoryName(SpecDirectory) ?? throw new DirectoryNotFoundException("Conformance directory was not found."),
            "PerformanceBinaryDump.reference.gesa");

    private static string GetPerformanceBinaryDumpCurrentPath(string referencePath)
        => Path.Combine(
            Path.GetDirectoryName(referencePath) ?? throw new DirectoryNotFoundException("Performance binary dump reference directory was not found."),
            "PerformanceBinaryDump.current.gesa");

    private static bool PerformanceReportMatchesReference(string reference, string current, out string diff)
    {
        var referenceLines = reference.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
        var currentLines = current.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
        var builder = new StringBuilder();
        var count = Math.Max(referenceLines.Length, currentLines.Length);
        for (var index = 0; index < count; index++)
        {
            if (index >= referenceLines.Length)
            {
                builder.AppendLine($"line {index + 1}: unexpected current line '{currentLines[index]}'");
                continue;
            }

            if (index >= currentLines.Length)
            {
                builder.AppendLine($"line {index + 1}: missing current line, expected '{referenceLines[index]}'");
                continue;
            }

            var referenceLine = referenceLines[index];
            var currentLine = currentLines[index];
            if (referenceLine == currentLine)
            {
                continue;
            }

            if (TryComparePerformanceMetric(referenceLine, currentLine, out var metricDiff))
            {
                if (metricDiff.Length > 0)
                {
                    builder.AppendLine($"line {index + 1}: {metricDiff}");
                }

                continue;
            }

            builder.AppendLine($"line {index + 1}: expected '{referenceLine}' but was '{currentLine}'");
        }

        diff = builder.ToString();
        return diff.Length == 0;
    }

    private static bool TryComparePerformanceMetric(string referenceLine, string currentLine, out string diff)
    {
        diff = string.Empty;
        var referenceSeparator = referenceLine.IndexOf('=');
        var currentSeparator = currentLine.IndexOf('=');
        if (referenceSeparator <= 0 || currentSeparator <= 0)
        {
            return false;
        }

        var key = referenceLine[..referenceSeparator];
        var currentKey = currentLine[..currentSeparator];
        if (!string.Equals(key, currentKey, StringComparison.Ordinal))
        {
            return TryCompareRenamedPerformanceMetric(key, referenceLine[(referenceSeparator + 1)..], currentKey, currentLine[(currentSeparator + 1)..], out diff);
        }

        var referenceValue = referenceLine[(referenceSeparator + 1)..];
        var currentValue = currentLine[(currentSeparator + 1)..];
        if (key.EndsWith(".elapsedMs", StringComparison.Ordinal) ||
            key.EndsWith(".perInvokeElapsedMs", StringComparison.Ordinal))
        {
            if (!double.TryParse(referenceValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var referenceMs) ||
                !double.TryParse(currentValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var currentMs))
            {
                return false;
            }

            var allowed = referenceMs + Math.Max(
                referenceMs * PerformanceElapsedRegressionTolerance,
                PerformanceElapsedMinimumToleranceMilliseconds);
            if (currentMs > allowed)
            {
                diff = $"{key} regressed: reference={FormatMilliseconds(referenceMs)} ms, " +
                       $"current={FormatMilliseconds(currentMs)} ms, " +
                       $"allowed={FormatMilliseconds(allowed)} ms";
            }

            return true;
        }

        if (key.EndsWith(".allocatedKb", StringComparison.Ordinal) ||
            key.EndsWith(".perInvokeAllocatedKb", StringComparison.Ordinal))
        {
            if (!double.TryParse(referenceValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var referenceKb) ||
                !double.TryParse(currentValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var currentKb))
            {
                return false;
            }

            if (currentKb > referenceKb)
            {
                diff = $"{key} allocated more: reference={FormatKilobytes(referenceKb)} KB, " +
                       $"current={FormatKilobytes(currentKb)} KB";
            }

            return true;
        }

        return false;
    }

    private static bool TryCompareRenamedPerformanceMetric(string referenceKey, string referenceValue, string currentKey, string currentValue, out string diff)
    {
        diff = string.Empty;
        if (!TryNormalizeAllocationKey(referenceKey, out var normalizedReferenceKey) ||
            !TryNormalizeAllocationKey(currentKey, out var normalizedCurrentKey) ||
            !string.Equals(normalizedReferenceKey, normalizedCurrentKey, StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryParseAllocationKilobytes(referenceKey, referenceValue, out var referenceKb) ||
            !TryParseAllocationKilobytes(currentKey, currentValue, out var currentKb))
        {
            return false;
        }

        if (currentKb > referenceKb)
        {
            diff = $"{normalizedReferenceKey} allocated more: reference={FormatKilobytes(referenceKb)} KB, " +
                   $"current={FormatKilobytes(currentKb)} KB";
        }

        return true;
    }

    private static bool TryNormalizeAllocationKey(string key, out string normalizedKey)
    {
        if (key.EndsWith(".allocatedBytes", StringComparison.Ordinal))
        {
            normalizedKey = key[..^".allocatedBytes".Length] + ".allocatedKb";
            return true;
        }

        if (key.EndsWith(".perInvokeAllocatedBytes", StringComparison.Ordinal))
        {
            normalizedKey = key[..^".perInvokeAllocatedBytes".Length] + ".perInvokeAllocatedKb";
            return true;
        }

        if (key.EndsWith(".allocatedKb", StringComparison.Ordinal) ||
            key.EndsWith(".perInvokeAllocatedKb", StringComparison.Ordinal))
        {
            normalizedKey = key;
            return true;
        }

        normalizedKey = string.Empty;
        return false;
    }

    private static bool TryParseAllocationKilobytes(string key, string value, out double kilobytes)
    {
        kilobytes = 0d;
        if (key.EndsWith(".allocatedBytes", StringComparison.Ordinal) ||
            key.EndsWith(".perInvokeAllocatedBytes", StringComparison.Ordinal))
        {
            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes))
            {
                return false;
            }

            kilobytes = bytes / 1024d;
            return true;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out kilobytes);
    }

    private static string BinaryDumpDiff(string reference, string current)
    {
        var referenceLines = reference.ReplaceLineEndings("\n").Split('\n');
        var currentLines = current.ReplaceLineEndings("\n").Split('\n');
        var builder = new StringBuilder();
        var count = Math.Max(referenceLines.Length, currentLines.Length);
        for (var index = 0; index < count; index++)
        {
            if (index >= referenceLines.Length)
            {
                builder.AppendLine($"line {index + 1}: unexpected current line '{currentLines[index]}'");
                continue;
            }

            if (index >= currentLines.Length)
            {
                builder.AppendLine($"line {index + 1}: missing current line, expected '{referenceLines[index]}'");
                continue;
            }

            if (referenceLines[index] == currentLines[index])
            {
                continue;
            }

            builder.AppendLine($"line {index + 1}:");
            builder.AppendLine($"  expected: {referenceLines[index]}");
            builder.AppendLine($"  actual:   {currentLines[index]}");
            if (builder.Length > 4096)
            {
                builder.AppendLine("  ...");
                break;
            }
        }

        return builder.ToString();
    }

    private void RunPerformanceCase(GameEventScriptConformanceCase testCase, StringBuilder report, StringBuilder binaryDumpReport)
    {
        if (!string.Equals(testCase.Test.Kind, "performance", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail($"{testCase}: performance suite contains unsupported test kind '{testCase.Test.Kind}'.");
        }

        if (testCase.Test.Steps is null || testCase.Test.Steps.Count == 0)
        {
            Assert.Fail($"{testCase}: performance tests require at least one step.");
        }

        var iterations = ResolveIterationCount(PerformanceIterations, testCase.Test.Iterations, DefaultIterations);
        var warmupIterations = ResolveIterationCount(PerformanceWarmupIterations, testCase.Test.WarmupIterations, DefaultWarmupIterations);
        var compileOptions = GameEventScriptConformanceRunner.CreateCompileOptionsForTest(testCase.Test);
        WarmupCompilePipeline(testCase, compileOptions, PerformanceCompileWarmupIterations);
        var astBuild = Measure("astBuild", () =>
            GameEventScriptConformanceRunner.CreateScriptBuilderForTest(testCase.Test).BuildModule(compileOptions));
        var binaryBuild = Measure("binaryBuild", () =>
            GesCompiler.Compile(astBuild.Value, compileOptions));
        var programLoad = Measure("programLoad", () =>
        {
            var host = GameEventScriptManager.CreateHostBuilder()
                .WithRegistry(GameEventScriptConformanceExtensionRegistry.Instance)
                .WithExternalTypeRegistry(GameEventScriptConformanceRunner.ExternalTypeRegistry)
                .WithRuntimeLimits(GameEventScriptConformanceRunner.CreateRuntimeLimitsForTest(testCase.Test.RuntimeLimits))
                .Build();
            return host.Load(binaryBuild.Value);
        });
        AppendPerformanceBinaryDumpCase(binaryDumpReport, testCase, binaryBuild.Value);
        if (testCase.Test.DumpBinary)
        {
            TestContext.WriteLine($"Binary dump: {testCase.SuiteName}/{testCase.Test.Name}");
            TestContext.WriteLine(StableBinaryDump(binaryBuild.Value));
        }

        AssertPerformanceCorrectness(testCase, "new vm", binaryBuild.Value);

        var newRun = MeasurePerformanceRun(testCase, binaryBuild.Value, iterations, warmupIterations);

        AppendPerformanceCase(report, testCase, iterations, warmupIterations, astBuild, binaryBuild, programLoad, newRun);
    }

    private static void AppendPerformanceBinaryDumpCase(
        StringBuilder report,
        GameEventScriptConformanceCase testCase,
        GameEventScriptProgram binary)
    {
        report.AppendLine("// -------------------------------------------------------------------------------");
        report.Append("//  Performance Case: ").Append(testCase.SuiteName).Append('/').AppendLine(testCase.Test.Name);
        report.AppendLine("// -------------------------------------------------------------------------------");
        report.AppendLine();
        report.AppendLine(StableBinaryDump(binary));
        report.AppendLine();
    }

    private static string StableBinaryDump(GameEventScriptProgram binary)
    {
        var dump = binary.Dump(includeInstructionAddresses: false);
        var lines = dump.ReplaceLineEndings("\n").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].StartsWith("//  Disassembled at ", StringComparison.Ordinal))
            {
                lines[index] = "//  Disassembled at <stable>";
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendPerformanceCase(
        StringBuilder report,
        GameEventScriptConformanceCase testCase,
        int iterations,
        int warmupIterations,
        Measured<GesSyntaxTreeModule> astBuild,
        Measured<GameEventScriptProgram> binaryBuild,
        Measured<GameEventScriptInstance> programLoad,
        PerformanceRunMetrics run)
    {
        report.AppendLine($"## {testCase.SuiteName}/{testCase.Test.Name}");
        report.AppendLine();
        report.AppendLine($"iterations={iterations}");
        report.AppendLine($"warmupIterations={warmupIterations}");
        report.AppendLine($"steps={testCase.Test.Steps!.Count}");
        AppendMeasured(report, "astBuild", astBuild);
        AppendMeasured(report, "binaryBuild", binaryBuild);
        AppendMeasured(report, "programLoad", programLoad);
        AppendCompileTotal(report, astBuild, binaryBuild, programLoad);
        AppendRun(report, "run", run, iterations);
        report.AppendLine();
    }

    private static void AssertPerformanceCorrectness(
        GameEventScriptConformanceCase testCase,
        string engine,
        GameEventScriptProgram module)
    {
        var emitted = new List<GameEventScriptMessage>();
        var published = new List<GameEventScriptMessage>();
        var host = CreatePerformanceHost(testCase, module, emitted, published);

        for (var stepIndex = 0; stepIndex < testCase.Test.Steps!.Count; stepIndex++)
        {
            emitted.Clear();
            published.Clear();
            var step = testCase.Test.Steps[stepIndex];
            var accepted = host.Receive(GameEventScriptConformanceValueCodec.DecodeMessage(step.Input));
            host.RunToCompletion();
            if (!accepted)
            {
                Assert.Fail($"{testCase} {engine} step {stepIndex + 1}: handler was not found or could not start.");
            }

            AssertMessages(testCase, engine, stepIndex, "emitted messages", step.ExpectedPublished, emitted);
            AssertMessages(testCase, engine, stepIndex, "outbound published messages", step.ExpectedOutboundPublished, published);
        }
    }

    private static PerformanceRunMetrics MeasurePerformanceRun(
        GameEventScriptConformanceCase testCase,
        GameEventScriptProgram module,
        int iterations,
        int warmupIterations)
    {
        var emittedCount = 0;
        var publishedCount = 0;
        var host = CreatePerformanceHost(
            testCase,
            module,
            _ => emittedCount++,
            _ => publishedCount++);

        var inputs = testCase.Test.Steps!
            .Select(step => GameEventScriptConformanceValueCodec.DecodeMessage(step.Input))
            .ToArray();
        RunPerformanceIterations(testCase, host, inputs, warmupIterations);
        ForceFullCollection();
        emittedCount = 0;
        publishedCount = 0;
        var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        RunPerformanceIterations(testCase, host, inputs, iterations);
        stopwatch.Stop();
        return new PerformanceRunMetrics(
            stopwatch.Elapsed,
            GC.GetAllocatedBytesForCurrentThread() - beforeAllocated,
            emittedCount,
            publishedCount);
    }

    private static void RunPerformanceIterations(
        GameEventScriptConformanceCase testCase,
        GameEventScriptHost host,
        IReadOnlyList<GameEventScriptMessage> inputs,
        int iterations)
    {
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            foreach (var input in inputs)
            {
                var accepted = host.Receive(input);
                var result = host.RunToCompletion();
                if (!accepted || result.State == GameEventScriptExecutionState.RuntimeLimitReached)
                {
                    Assert.Fail($"{testCase}: handler was not found or could not start during performance run.");
                }
            }
        }
    }

    private static GameEventScriptHost CreatePerformanceHost(
        GameEventScriptConformanceCase testCase,
        GameEventScriptProgram module,
        List<GameEventScriptMessage> emitted,
        List<GameEventScriptMessage> published)
        => CreatePerformanceHost(testCase, module, emitted.Add, published.Add);

    private static GameEventScriptHost CreatePerformanceHost(
        GameEventScriptConformanceCase testCase,
        GameEventScriptProgram module,
        Action<GameEventScriptMessage> emitted,
        Action<GameEventScriptMessage> published)
    {
        var builder = GameEventScriptManager.CreateHostBuilder()
            .WithRandom(GameEventScriptConformanceRunner.CreateRandomForTest(testCase.Test.RandomSequence))
            .WithRegistry(GameEventScriptConformanceExtensionRegistry.Instance)
            .WithExternalTypeRegistry(GameEventScriptConformanceRunner.ExternalTypeRegistry)
            .WithRuntimeLimits(GameEventScriptConformanceRunner.CreateRuntimeLimitsForTest(testCase.Test.RuntimeLimits))
            .WithRuntimeObserver(TestRuntimeObserver.ObserveMessages(
                messageEmitted: emitted,
                messagePublished: emitted))
            .WithPublishSink(new TestPublishSink(published));
        var host = builder.Build();
        host.Load(module);
        GameEventScriptConformanceRunner.RegisterExternalSubscribers(testCase, host);
        return host;
    }

    private static void AssertMessages(
        GameEventScriptConformanceCase testCase,
        string engine,
        int stepIndex,
        string label,
        IReadOnlyList<JsonElement>? expectedPublished,
        IReadOnlyList<GameEventScriptMessage> actual)
    {
        var expected = (expectedPublished ?? []).Select(GameEventScriptConformanceValueCodec.DecodeMessage).ToArray();
        if (GameEventScriptConformanceValueCodec.ConformanceEquals(expected, actual, ResolveMaxFloatUlps(testCase, stepIndex)))
        {
            return;
        }

        Assert.Fail(
            $"{testCase} {engine} step {stepIndex + 1}: {label} differ.{Environment.NewLine}" +
            $"Expected:{Environment.NewLine}{GameEventScriptConformanceValueCodec.ToPrettyJson(expected)}{Environment.NewLine}" +
            $"Actual:{Environment.NewLine}{GameEventScriptConformanceValueCodec.ToPrettyJson(actual)}");
    }

    private static int ResolveIterationCount(int testOverride, int? jsonValue, int defaultValue)
        => testOverride >= 0 ? testOverride : Math.Max(0, jsonValue ?? defaultValue);

    private static void WarmupCompilePipeline(
        GameEventScriptConformanceCase testCase,
        GameEventScriptCompileOptions compileOptions,
        int iterations)
    {
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var module = GameEventScriptConformanceRunner.CreateScriptBuilderForTest(testCase.Test).BuildModule(compileOptions);
            var binary = GesCompiler.Compile(module, compileOptions);
            var host = GameEventScriptManager.CreateHostBuilder()
                .WithRegistry(GameEventScriptConformanceExtensionRegistry.Instance)
                .WithExternalTypeRegistry(GameEventScriptConformanceRunner.ExternalTypeRegistry)
                .Build();
            _ = host.Load(binary);
        }
    }

    private static Measured<T> Measure<T>(string name, Func<T> action)
    {
        ForceFullCollection();
        var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        var value = action();
        stopwatch.Stop();
        return new Measured<T>(name, value, stopwatch.Elapsed, GC.GetAllocatedBytesForCurrentThread() - beforeAllocated);
    }

    private static void AppendMeasured<T>(StringBuilder report, string label, Measured<T> measured)
    {
        report.Append(label);
        report.Append(".elapsedMs=");
        report.AppendLine(FormatMilliseconds(measured.Elapsed.TotalMilliseconds));
        report.Append(label);
        report.Append(".allocatedKb=");
        report.AppendLine(FormatAllocatedKilobytes(measured.AllocatedBytes));
    }

    private static void AppendCompileTotal(
        StringBuilder report,
        Measured<GesSyntaxTreeModule> astBuild,
        Measured<GameEventScriptProgram> binaryBuild,
        Measured<GameEventScriptInstance> programLoad)
    {
        report.Append("compile.elapsedMs=");
        report.AppendLine(FormatMilliseconds(
            astBuild.Elapsed.TotalMilliseconds +
            binaryBuild.Elapsed.TotalMilliseconds +
            programLoad.Elapsed.TotalMilliseconds));
        report.Append("compile.allocatedKb=");
        report.AppendLine(FormatAllocatedKilobytes(
            astBuild.AllocatedBytes +
            binaryBuild.AllocatedBytes +
            programLoad.AllocatedBytes));
    }

    private static void AppendRun(StringBuilder report, string label, PerformanceRunMetrics run, int iterations)
    {
        var safeIterations = Math.Max(1, iterations);
        report.Append(label);
        report.Append(".elapsedMs=");
        report.AppendLine(FormatMilliseconds(run.Elapsed.TotalMilliseconds));
        report.Append(label);
        report.Append(".allocatedKb=");
        report.AppendLine(FormatAllocatedKilobytes(run.AllocatedBytes));
        report.Append(label);
        report.Append(".perInvokeElapsedMs=");
        report.AppendLine(FormatMilliseconds(run.Elapsed.TotalMilliseconds / safeIterations));
        report.Append(label);
        report.Append(".perInvokeAllocatedKb=");
        report.AppendLine(FormatAllocatedKilobytes((double)run.AllocatedBytes / safeIterations));
        report.Append(label);
        report.Append(".emittedMessages=");
        report.AppendLine(run.EmittedMessages.ToString(CultureInfo.InvariantCulture));
        report.Append(label);
        report.Append(".outboundPublishedMessages=");
        report.AppendLine(run.OutboundPublishedMessages.ToString(CultureInfo.InvariantCulture));
    }

    private static string FormatMilliseconds(double milliseconds)
        => milliseconds.ToString("0.######", CultureInfo.InvariantCulture);

    private static string FormatAllocatedKilobytes(long bytes)
        => FormatKilobytes(bytes / 1024d);

    private static string FormatAllocatedKilobytes(double bytes)
        => FormatKilobytes(bytes / 1024d);

    private static string FormatKilobytes(double kilobytes)
        => kilobytes.ToString("0.###", CultureInfo.InvariantCulture);

    private static void ForceFullCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private sealed record Measured<T>(string Name, T Value, TimeSpan Elapsed, long AllocatedBytes);

    private sealed record PerformanceRunMetrics(
        TimeSpan Elapsed,
        long AllocatedBytes,
        int EmittedMessages,
        int OutboundPublishedMessages);
}

public abstract class GameEventScriptJsonConformanceTestBase
{
    protected static readonly string SpecDirectory = Path.Combine(GetSourceDirectory(), "Specs");

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> ApiMessagesCases()
        => Cases("api/messages.json");

    public static IEnumerable<object[]> CompileBuildErrorsCases()
        => Cases("compile/build-errors.json");

    public static IEnumerable<object[]> CompileSyntaxErrorsCases()
        => Cases("compile/syntax-errors.json");

    public static IEnumerable<object[]> CompileBytecodeLoweringCases()
        => Cases("compile/bytecode-lowering.json");

    public static IEnumerable<object[]> CompileBinaryCompilerCases()
        => Cases("compile/binary-compiler.json");

    public static IEnumerable<object[]> RuntimeAtomicMathMatrixCases()
        => Cases("runtime/atomic/math-matrix.json");

    public static IEnumerable<object[]> RuntimeAtomicTrigNavigationCases()
        => Cases("runtime/atomic/trig-navigation.json");

    public static IEnumerable<object[]> RuntimeAtomicEqualityCases()
        => Cases("runtime/atomic/equality.json");

    public static IEnumerable<object[]> RuntimeAtomicCompareCases()
        => Cases("runtime/atomic/compare.json");

    public static IEnumerable<object[]> RuntimeAtomicExternalAccessCases()
        => Cases("runtime/atomic/external-access.json");

    public static IEnumerable<object[]> RuntimeAtomicMemberIndexAccessCases()
        => Cases("runtime/atomic/member-index-access.json");

    public static IEnumerable<object[]> RuntimeAtomicBooleanLogicCases()
        => Cases("runtime/atomic/boolean-logic.json");

    public static IEnumerable<object[]> RuntimeAtomicCreateValuesCases()
        => Cases("runtime/atomic/create-values.json");

    public static IEnumerable<object[]> RuntimeAtomicCastsCases()
        => Cases("runtime/atomic/casts.json");

    public static IEnumerable<object[]> RuntimeAtomicCustomTypesCases()
        => Cases("runtime/atomic/custom-types.json");

    public static IEnumerable<object[]> RuntimeAtomicCollectionOperatorsCases()
        => Cases("runtime/atomic/collection-operators.json");

    public static IEnumerable<object[]> RuntimeAtomicControlFlowCases()
        => Cases("runtime/atomic/control-flow.json");

    public static IEnumerable<object[]> RuntimeAtomicSeriesCases()
        => Cases("runtime/atomic/series.json");

    public static IEnumerable<object[]> RuntimeAtomicIteratorCoreCases()
        => Cases("runtime/atomic/iterator-core.json");

    public static IEnumerable<object[]> RuntimeAtomicIteratorTerminalsCases()
        => Cases("runtime/atomic/iterator-terminals.json");

    public static IEnumerable<object[]> RuntimeAtomicSortGroupDistinctCases()
        => Cases("runtime/atomic/sort-group-distinct.json");

    public static IEnumerable<object[]> RuntimeAtomicShuffleReverseCases()
        => Cases("runtime/atomic/shuffle-reverse.json");

    public static IEnumerable<object[]> RuntimeAtomicPatternsCases()
        => Cases("runtime/atomic/patterns.json");

    public static IEnumerable<object[]> RuntimeAtomicRandomCases()
        => Cases("runtime/atomic/random.json");

    public static IEnumerable<object[]> RuntimeCollectionsCases()
        => Cases("runtime/collections.json");

    public static IEnumerable<object[]> RuntimeControlFlowCases()
        => Cases("runtime/control-flow.json");

    public static IEnumerable<object[]> RuntimeExtensionsSequencesCases()
        => Cases("runtime/extensions-sequences.json");

    public static IEnumerable<object[]> RuntimeHostDispatchCases()
        => Cases("runtime/host-dispatch.json");

    public static IEnumerable<object[]> RuntimeMessagesHandlersCases()
        => Cases("runtime/messages-handlers.json");

    public static IEnumerable<object[]> RuntimePredicatesFunctionsCases()
        => Cases("runtime/predicates-functions.json");

    public static IEnumerable<object[]> RuntimePublishTagsCases()
        => Cases("runtime/publish-tags.json");

    public static IEnumerable<object[]> RuntimeRandomDiceRangesCases()
        => Cases("runtime/random-dice-ranges.json");

    public static IEnumerable<object[]> RuntimeTypesAndValuesCases()
        => Cases("runtime/types-and-values.json");

    protected static string FormatConformanceCaseDisplayName(MethodInfo methodInfo, object[] data)
        => data is [GameEventScriptConformanceCase testCase]
            ? testCase.Test.Name ?? testCase.ToString()
            : methodInfo.Name;

    protected void RunJsonConformanceCase(GameEventScriptConformanceCase testCase)
    {
        if (string.Equals(testCase.Test.Kind, "scriptApi", StringComparison.OrdinalIgnoreCase))
        {
            RunScriptApiConformanceCase(testCase);
            return;
        }

        switch (testCase.Test.Kind)
        {
            case "compileError":
                GameEventScriptConformanceRunner.RunCompileErrorTest(testCase);
                break;
            case "loadError":
                GameEventScriptConformanceRunner.RunLoadErrorTest(testCase);
                break;
            case "messageApi":
                GameEventScriptConformanceRunner.RunMessageApiTest(testCase);
                break;
            case "compileMetadata":
                GameEventScriptConformanceRunner.RunCompileMetadataTest(testCase);
                break;
            case "bytecode":
                GameEventScriptConformanceRunner.RunBytecodeOpcodeTest(testCase);
                break;
            default:
                Assert.Fail($"{testCase}: unsupported test kind '{testCase.Test.Kind}'.");
                break;
        }
    }

    protected void RunScriptApiConformanceCase(GameEventScriptConformanceCase testCase)
    {
        var outcome = RunScriptApiCase(testCase);
        TestContext.WriteLine($"{outcome.Status}: {testCase}: {outcome.Detail}");
        if (!outcome.Passed)
        {
            TestContext.WriteLine(outcome.DebugDump);
        }

        if (!outcome.Passed)
        {
            Assert.Fail($"{outcome.Status}: {testCase}: {outcome.Detail}");
        }
    }

    protected static ScriptApiConformanceOutcome RunScriptApiCase(
        GameEventScriptConformanceCase testCase,
        bool includeMessageDiff = true)
    {
        IReadOnlyList<GameEventScriptProgram> programs;
        try
        {
            programs = CompileProgramsForTest(testCase.Test);
        }
        catch (Exception exception)
        {
            return ScriptApiConformanceOutcome.CompileFailure(exception.Message);
        }

        try
        {
            return RunScriptApiCase(testCase, programs, includeMessageDiff, out var mismatch, out var debugDump)
                ? ScriptApiConformanceOutcome.Pass()
                : ScriptApiConformanceOutcome.Mismatch(mismatch, debugDump);
        }
        catch (Exception exception)
        {
            return ScriptApiConformanceOutcome.RuntimeFailure(exception.Message);
        }
    }

    private static IReadOnlyList<GameEventScriptProgram> CompileProgramsForTest(GameEventScriptConformanceTest test)
    {
        if (test.Programs is not { Count: > 0 })
            return [GameEventScriptConformanceRunner.CompileBytecodeForTest(test)];

        var options = GameEventScriptConformanceRunner.CreateCompileOptionsForTest(test);
        var programs = new GameEventScriptProgram[test.Programs.Count];
        for (var index = 0; index < programs.Length; index++)
        {
            var source = test.Programs[index];
            programs[index] = GameEventScriptBuilder.Create()
                .WithExternalTypeCatalog(GameEventScriptConformanceRunner.ExternalTypeCatalog)
                .AddScript(source.Text ?? string.Empty, source.SourceName)
                .Compile(options);
        }

        return programs;
    }

    private static IEnumerable<object[]> Cases(string relativeSpecFile)
        => GameEventScriptConformanceRunner.ConformanceCases(SpecDirectory, relativeSpecFile);

    private static string GetSourceDirectory([CallerFilePath] string sourceFile = "")
        => Path.GetDirectoryName(sourceFile)!;

    private static bool RunScriptApiCase(
        GameEventScriptConformanceCase testCase,
        IReadOnlyList<GameEventScriptProgram> programs,
        bool includeMessageDiff,
        out string mismatch,
        out string debugDump)
    {
        mismatch = string.Empty;
        debugDump = string.Empty;
        var test = testCase.Test;
        var hasInitializationExpectations =
            test.ExpectedInitializationPublished is not null ||
            test.ExpectedInitializationOutboundPublished is not null;
        if ((test.Steps is null || test.Steps.Count == 0) && !hasInitializationExpectations)
        {
            mismatch = "scriptApi tests require at least one step or initialization expectation.";
            return false;
        }

        var random = GameEventScriptConformanceRunner.CreateRandomForTest(test.RandomSequence);
        var runtimeLimits = GameEventScriptConformanceRunner.CreateRuntimeLimitsForTest(test.RuntimeLimits);
        var emitted = new List<GameEventScriptMessage>();
        var published = new List<GameEventScriptMessage>();
        var observedRuntimeLimits = new List<TestRuntimeLimitEvent>();
        var observedRuntimeDiagnostics = new List<GameEventScriptDiagnostic>();
        var lastCapturedVmDump = string.Join(
            Environment.NewLine,
            programs.Select(program => program.Dump(includeInstructionAddresses: true)));
        var host = GameEventScriptManager.CreateHostBuilder()
            .WithRandom(random)
            .WithRegistry(GameEventScriptConformanceExtensionRegistry.Instance)
            .WithExternalTypeRegistry(GameEventScriptConformanceRunner.ExternalTypeRegistry)
            .WithRuntimeLimits(runtimeLimits)
            .WithRuntimeObserver(TestRuntimeObserver.ObserveMessages(
                messageEmitted: message =>
                {
                    emitted.Add(message);
                },
                messagePublished: message =>
                {
                    emitted.Add(message);
                },
                runtimeLimitReached: (name, detail, limit) => observedRuntimeLimits.Add(new TestRuntimeLimitEvent(name, detail, limit)),
                runtimeError: observedRuntimeDiagnostics.Add))
            .WithPublishSink(new TestPublishSink(published.Add))
            .Build();
        for (var programIndex = 0; programIndex < programs.Count; programIndex++) host.Load(programs[programIndex]);
        GameEventScriptConformanceRunner.RegisterExternalSubscribers(testCase, host);
        host.RunToCompletion();

        if (test.ExpectedInitializationPublished is not null &&
            !TryMatchMessages(testCase, -1, "initialization emitted messages", test.ExpectedInitializationPublished, emitted, includeMessageDiff, out var initializationEmittedDiff))
        {
            mismatch = $"initialization: emitted messages differ.{Environment.NewLine}{initializationEmittedDiff}";
            debugDump = CaptureVmDump(lastCapturedVmDump);
            return false;
        }

        if (test.ExpectedInitializationOutboundPublished is not null &&
            !TryMatchMessages(testCase, -1, "initialization published messages", test.ExpectedInitializationOutboundPublished, published, includeMessageDiff, out var initializationPublishedDiff))
        {
            mismatch = $"initialization: published messages differ.{Environment.NewLine}{initializationPublishedDiff}";
            debugDump = CaptureVmDump(lastCapturedVmDump);
            return false;
        }

        for (var stepIndex = 0; stepIndex < (test.Steps?.Count ?? 0); stepIndex++)
        {
            var step = test.Steps![stepIndex];
            emitted.Clear();
            published.Clear();
            observedRuntimeLimits.Clear();
            observedRuntimeDiagnostics.Clear();
            var accepted = host.Receive(GameEventScriptConformanceValueCodec.DecodeMessage(step.Input));
            var paused = false;
            if (step.OpcodeBudget is > 0)
            {
                while (!host.IsIdle)
                {
                    var frame = host.ExecuteFrame(step.OpcodeBudget.Value);
                    paused |= frame.State == GameEventScriptExecutionState.Paused;
                    if (frame.State == GameEventScriptExecutionState.RuntimeLimitReached) break;
                }
            }
            else
            {
                host.RunToCompletion();
            }
            if (!accepted)
            {
                mismatch = $"step {stepIndex + 1}: handler was not found or could not start.";
                debugDump = CaptureVmDump(lastCapturedVmDump);
                return false;
            }

            if (step.ExpectedPaused.HasValue && step.ExpectedPaused.Value != paused)
            {
                mismatch = $"step {stepIndex + 1}: expectedPaused was {step.ExpectedPaused.Value} but actual was {paused}.";
                debugDump = CaptureVmDump(lastCapturedVmDump);
                return false;
            }

            if (!TryMatchMessages(testCase, stepIndex, "emitted messages", step.ExpectedPublished, emitted, includeMessageDiff, out var emittedDiff))
            {
                mismatch = $"step {stepIndex + 1}: emitted messages differ.{Environment.NewLine}{emittedDiff}";
                debugDump = CaptureVmDump(lastCapturedVmDump);
                return false;
            }

            if (!TryMatchMessages(testCase, stepIndex, "published messages", step.ExpectedOutboundPublished, published, includeMessageDiff, out var publishedDiff))
            {
                mismatch = $"step {stepIndex + 1}: published messages differ.{Environment.NewLine}{publishedDiff}";
                debugDump = CaptureVmDump(lastCapturedVmDump);
                return false;
            }

            if (!TryMatchRuntimeLimits(testCase, stepIndex, step, observedRuntimeLimits, out var runtimeLimitDiff))
            {
                mismatch = $"step {stepIndex + 1}: runtime limits differ.{Environment.NewLine}{runtimeLimitDiff}";
                debugDump = CaptureVmDump(lastCapturedVmDump);
                return false;
            }

            if (!RuntimeDiagnosticsMatch(step.ExpectedRuntimeDiagnostics, observedRuntimeDiagnostics, out var runtimeDiagnosticDiff))
            {
                mismatch = $"step {stepIndex + 1}: runtime diagnostics differ.{Environment.NewLine}{runtimeDiagnosticDiff}";
                debugDump = CaptureVmDump(lastCapturedVmDump);
                return false;
            }
        }

        return true;
    }

    private static string CaptureVmDump(string lastCapturedVmDump)
        => !string.IsNullOrWhiteSpace(lastCapturedVmDump)
            ? lastCapturedVmDump
            : "//\t<vm state dump unavailable: no message was emitted or published before the assertion failed>";

    private static bool TryMatchMessages(
        GameEventScriptConformanceCase testCase,
        int stepIndex,
        string label,
        IReadOnlyList<JsonElement>? expectedPublished,
        IReadOnlyList<GameEventScriptMessage> actual,
        bool includeMessageDiff,
        out string diff)
    {
        diff = string.Empty;
        var expected = (expectedPublished ?? []).Select(GameEventScriptConformanceValueCodec.DecodeMessage).ToArray();
        if (GameEventScriptConformanceValueCodec.ConformanceEquals(expected, actual, ResolveMaxFloatUlps(testCase, stepIndex)))
        {
            return true;
        }

        diff = includeMessageDiff
            ? BuildMessageDiff(testCase, stepIndex, label, expected, actual)
            : $"expectedMessages={expected.Length}, actualMessages={actual.Count}";
        return false;
    }

    protected static int ResolveMaxFloatUlps(GameEventScriptConformanceCase testCase, int stepIndex)
    {
        var stepValue = stepIndex >= 0 && testCase.Test.Steps is { } steps && stepIndex < steps.Count
            ? steps[stepIndex].MaxFloatUlps
            : null;
        return Math.Max(0, stepValue ?? testCase.Test.MaxFloatUlps ?? GameEventScriptConformanceValueCodec.DefaultMaxFloatUlps);
    }

    private static bool TryMatchRuntimeLimits(
        GameEventScriptConformanceCase testCase,
        int stepIndex,
        GameEventScriptApiStepSpec step,
        IReadOnlyList<TestRuntimeLimitEvent> actual,
        out string diff)
    {
        diff = string.Empty;
        if (!RuntimeLimitsMatch(step.ExpectedRuntimeLimits, step.UnexpectedRuntimeLimits, actual, out var details))
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Test: {testCase.Test.Name ?? testCase.ToString()}");
            builder.AppendLine($"Step: {stepIndex + 1}");
            builder.AppendLine("Channel: runtime limits");
            builder.Append(details);
            diff = builder.ToString();
            return false;
        }

        return true;
    }

    private static bool RuntimeLimitsMatch(
        IReadOnlyList<GameEventScriptRuntimeLimitExpectationSpec>? expectedRuntimeLimits,
        IReadOnlyList<GameEventScriptRuntimeLimitExpectationSpec>? unexpectedRuntimeLimits,
        IReadOnlyList<TestRuntimeLimitEvent> actual,
        out string diff)
    {
        diff = string.Empty;
        var builder = new StringBuilder();
        var nextStart = 0;
        foreach (var expected in expectedRuntimeLimits ?? [])
        {
            var foundIndex = -1;
            for (var i = nextStart; i < actual.Count; i++)
            {
                if (RuntimeLimitMatches(expected, actual[i]))
                {
                    foundIndex = i;
                    break;
                }
            }

            if (foundIndex < 0)
            {
                AppendDiff(builder, "expectedRuntimeLimit", DescribeRuntimeLimitExpectation(expected), DescribeRuntimeLimits(actual));
            }
            else
            {
                nextStart = foundIndex + 1;
            }
        }

        foreach (var unexpected in unexpectedRuntimeLimits ?? [])
        {
            var found = actual.FirstOrDefault(runtimeLimit => RuntimeLimitMatches(unexpected, runtimeLimit));
            if (found is not null)
            {
                AppendDiff(builder, "unexpectedRuntimeLimit", DescribeRuntimeLimitExpectation(unexpected), DescribeRuntimeLimit(found));
            }
        }

        diff = builder.ToString();
        return diff.Length == 0;
    }

    private static bool RuntimeDiagnosticsMatch(
        IReadOnlyList<GameEventScriptExpectedCompileErrorSpec>? expected,
        IReadOnlyList<GameEventScriptDiagnostic> actual,
        out string details)
    {
        expected ??= [];
        if (expected.Count != actual.Count)
        {
            details = $"expected {expected.Count} diagnostic(s), actual {actual.Count}: " +
                      string.Join("; ", actual.Select(value => $"{value.Phase}:{value.Code}"));
            return false;
        }

        for (var index = 0; index < expected.Count; index++)
        {
            var expectedDiagnostic = expected[index];
            var actualDiagnostic = actual[index];
            if ((!string.IsNullOrWhiteSpace(expectedDiagnostic.Phase) &&
                 !string.Equals(expectedDiagnostic.Phase, actualDiagnostic.Phase.ToString(), StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(expectedDiagnostic.Code) &&
                 !string.Equals(expectedDiagnostic.Code, actualDiagnostic.Code, StringComparison.Ordinal)) ||
                (!string.IsNullOrWhiteSpace(expectedDiagnostic.ProgramName) &&
                 !string.Equals(expectedDiagnostic.ProgramName, actualDiagnostic.ProgramName, StringComparison.Ordinal)) ||
                (!string.IsNullOrWhiteSpace(expectedDiagnostic.HandlerName) &&
                 !string.Equals(expectedDiagnostic.HandlerName, actualDiagnostic.HandlerName, StringComparison.Ordinal)))
            {
                details = $"diagnostic[{index}] expected {expectedDiagnostic.Phase}:{expectedDiagnostic.Code}, " +
                          $"actual {actualDiagnostic.Phase}:{actualDiagnostic.Code} " +
                          $"program={actualDiagnostic.ProgramName ?? "-"} handler={actualDiagnostic.HandlerName ?? "-"}.";
                return false;
            }
        }

        details = string.Empty;
        return true;
    }

    private static bool RuntimeLimitMatches(GameEventScriptRuntimeLimitExpectationSpec expected, TestRuntimeLimitEvent actual)
    {
        if (!string.IsNullOrWhiteSpace(expected.Name) &&
            !string.Equals(expected.Name, actual.Name, StringComparison.Ordinal))
        {
            return false;
        }

        if (expected.Limit is not null && expected.Limit.Value != actual.Limit)
        {
            return false;
        }

        return string.IsNullOrEmpty(expected.DetailContains) ||
               actual.Detail.Contains(expected.DetailContains, StringComparison.Ordinal);
    }

    private static string DescribeRuntimeLimitExpectation(GameEventScriptRuntimeLimitExpectationSpec expected)
        => $"name={expected.Name ?? "*"}, limit={expected.Limit?.ToString() ?? "*"}, detailContains={expected.DetailContains ?? "*"}";

    private static string DescribeRuntimeLimits(IEnumerable<TestRuntimeLimitEvent> runtimeLimits)
        => string.Join("; ", runtimeLimits.Select(DescribeRuntimeLimit));

    private static string DescribeRuntimeLimit(TestRuntimeLimitEvent runtimeLimit)
        => $"{runtimeLimit.Name} ({runtimeLimit.Limit}): {runtimeLimit.Detail}";

    private static string BuildMessageDiff(
        GameEventScriptConformanceCase testCase,
        int stepIndex,
        string label,
        IReadOnlyList<GameEventScriptMessage> expected,
        IReadOnlyList<GameEventScriptMessage> actual)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Test: {testCase.Test.Name ?? testCase.ToString()}");
        builder.AppendLine(stepIndex >= 0 ? $"Step: {stepIndex + 1}" : "Step: initialization");
        builder.AppendLine($"Channel: {label}");

        if (expected.Count != actual.Count)
        {
            AppendDiff(builder, "messageCount", expected.Count.ToString(), actual.Count.ToString());
        }

        var count = Math.Max(expected.Count, actual.Count);
        for (var messageIndex = 0; messageIndex < count; messageIndex++)
        {
            if (messageIndex >= expected.Count)
            {
                AppendDiff(
                    builder,
                    $"message[{messageIndex}]",
                    "<missing>",
                    GameEventScriptConformanceValueCodec.ToCanonicalJson(actual[messageIndex]));
                continue;
            }

            if (messageIndex >= actual.Count)
            {
                AppendDiff(
                    builder,
                    $"message[{messageIndex}]",
                    GameEventScriptConformanceValueCodec.ToCanonicalJson(expected[messageIndex]),
                    "<missing>");
                continue;
            }

            AppendMessageDiff(builder, messageIndex, expected[messageIndex], actual[messageIndex]);
        }

        return builder.ToString();
    }

    private static void AppendMessageDiff(
        StringBuilder builder,
        int messageIndex,
        GameEventScriptMessage expected,
        GameEventScriptMessage actual)
    {
        if (!string.Equals(expected.Name, actual.Name, StringComparison.Ordinal))
        {
            AppendDiff(builder, $"message[{messageIndex}].name", Quote(expected.Name), Quote(actual.Name));
        }

        var expectedTags = JsonSerializer.Serialize(expected.Tags);
        var actualTags = JsonSerializer.Serialize(actual.Tags);
        if (!string.Equals(expectedTags, actualTags, StringComparison.Ordinal))
        {
            AppendDiff(builder, $"message[{messageIndex}].tags", expectedTags, actualTags);
        }

        foreach (var key in expected.Arguments.Keys.Concat(actual.Arguments.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var hasExpected = expected.Arguments.ContainsKey(key);
            var hasActual = actual.Arguments.ContainsKey(key);
            var expectedJson = hasExpected ? GameEventScriptConformanceValueCodec.ToCanonicalJson(expected.Arguments[key]) : "<missing>";
            var actualJson = hasActual ? GameEventScriptConformanceValueCodec.ToCanonicalJson(actual.Arguments[key]) : "<missing>";
            if (!string.Equals(expectedJson, actualJson, StringComparison.Ordinal))
            {
                AppendDiff(builder, $"message[{messageIndex}].args.{key}", expectedJson, actualJson);
            }
        }
    }

    private static void AppendDiff(StringBuilder builder, string path, string expected, string actual)
    {
        builder.AppendLine(path);
        builder.AppendLine($"  expected: {expected}");
        builder.AppendLine($"  actual:   {actual}");
    }

    private static string Quote(string value)
        => JsonSerializer.Serialize(value);

    protected enum ScriptApiConformanceStatus
    {
        Passed,
        Mismatch,
        CompileFailure,
        RuntimeFailure
    }

    protected sealed record ScriptApiConformanceOutcome(
        ScriptApiConformanceStatus Status,
        string Detail,
        string DebugDump = "")
    {
        public bool Passed => Status == ScriptApiConformanceStatus.Passed;

        public static ScriptApiConformanceOutcome Pass()
            => new(ScriptApiConformanceStatus.Passed, "passed");

        public static ScriptApiConformanceOutcome Mismatch(string detail, string debugDump)
            => new(ScriptApiConformanceStatus.Mismatch, detail, debugDump);

        public static ScriptApiConformanceOutcome CompileFailure(string detail)
            => new(ScriptApiConformanceStatus.CompileFailure, detail);

        public static ScriptApiConformanceOutcome RuntimeFailure(string detail)
            => new(ScriptApiConformanceStatus.RuntimeFailure, detail);
    }
}
