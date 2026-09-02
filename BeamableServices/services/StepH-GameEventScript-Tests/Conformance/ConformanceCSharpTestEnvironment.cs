using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Conformance;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Runtime.Values;

namespace StepH_GameEventScript_Tests.Conformance;

internal static class ConformanceCSharpTestEnvironment
{
    internal const string PerformanceProfile = "csharp-dotnet-release-macos-arm64";

    private static readonly string[] Capabilities =
    [
        "bytecode-snapshot", "compiler", "external-types", "host", "message-api",
        "native-handlers", "observer", "performance", "program-binary", "publish-sink", "value-api", "vm"
    ];

    private static readonly Lazy<IReadOnlyList<ConformanceDocument>> LoadedDocuments = new(LoadDocuments);

    internal static IReadOnlyList<ConformanceDocument> Documents => LoadedDocuments.Value;

    internal static ConformanceRunnerEnvironment Deterministic()
        => Create(EchoPerformanceProvider.Instance);

    internal static ConformanceRunnerEnvironment Measured()
        => Create(ConformanceCSharpPerformanceProvider.Instance);

    internal static ConformanceRunReport Report(
        ConformanceRunnerEnvironment environment,
        IReadOnlyList<ConformanceCaseResult> results)
    {
        var passed = results.Count(result => result.Status == ConformanceCaseStatus.Passed);
        var failed = results.Count(result => result.Status == ConformanceCaseStatus.Failed);
        var skipped = results.Count(result => result.Status == ConformanceCaseStatus.Skipped);
        var error = results.Count(result => result.Status == ConformanceCaseStatus.Error);
        var status = error > 0
            ? ConformanceCaseStatus.Error
            : failed > 0
                ? ConformanceCaseStatus.Failed
                : skipped > 0
                    ? ConformanceCaseStatus.Skipped
                    : ConformanceCaseStatus.Passed;
        return new ConformanceRunReport(
            environment,
            status,
            new ConformanceRunSummary(results.Count, passed, failed, skipped, error),
            results);
    }

    private static ConformanceRunnerEnvironment Create(IConformancePerformanceProvider provider)
        => new(
            "steph.ges.conformance.csharp", "0.1.0", "steph.ges.csharp", "0.1.0",
            Capabilities,
            GameEventScriptConformanceExternalTypes.Catalog,
            ConformanceTestExtensionRegistry.Instance,
            GameEventScriptConformanceExternalTypes.Registry,
            PerformanceProfile,
            provider);

    private static IReadOnlyList<ConformanceDocument> LoadDocuments()
    {
        var root = Path.Combine(GetConformanceDirectory(), "Suites");
        return Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => ConformanceMarkdownParser.Parse(File.ReadAllBytes(path)))
            .ToArray();
    }

    internal static string GetConformanceDirectory([CallerFilePath] string sourceFile = "")
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

internal sealed class ConformanceTestExtensionRegistry : IGameEventScriptExtensionRegistry
{
    internal static ConformanceTestExtensionRegistry Instance { get; } = new();

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
        for (var index = 1; index < args.Length; index++) max = Math.Max(max, args.GetAsNumber(index));
        call.SetFloat(max);
    });

    private static readonly IGameEventScriptExtensionFunction NavShortestTurn = new DelegateExtensionFunction((call, args) =>
    {
        if (args.Length != 2)
        {
            call.SetNothing();
            return;
        }

        var delta = (args.GetAsNumber(1) - args.GetAsNumber(0) + 540d) % 360d - 180d;
        call.SetFloat(delta, GameEventScriptBytecodeInstructionUnit.UnitDegree);
    });

    private static readonly IGameEventScriptExtensionFunction NavIsNorth = new DelegateExtensionFunction((call, args) =>
    {
        if (args.Length != 1)
        {
            call.SetBoolean(false);
            return;
        }

        var wrapped = ((args.GetAsNumber(0) % 360d) + 360d) % 360d;
        call.SetBoolean(wrapped is <= 45d or >= 315d);
    });

    private static readonly IGameEventScriptExtensionFunction TestVectorSum = new DelegateExtensionFunction((call, args) =>
    {
        if (args.Length == 1 && args.KindAt(0) == GameEventScriptBytecodeTypeKind.Vector)
            call.SetFloat(args.GetX(0) + args.GetY(0) + args.GetZ(0), args.UnitAt(0));
        else
            call.SetNothing();
    });

    private static readonly IGameEventScriptExtensionFunction TestEcho = new DelegateExtensionFunction((call, args) =>
    {
        if (args.Length == 1) call.SetValue(args[0]);
        else call.SetNothing();
    });

    private static readonly IGameEventScriptExtensionFunction TestTruth = new DelegateExtensionFunction((call, _) => call.SetBoolean(true));
    private static readonly IGameEventScriptExtensionFunction TestFail = new DelegateExtensionFunction((_, _) =>
        throw new InvalidOperationException("Configured conformance extension failure."));

    private ConformanceTestExtensionRegistry()
    {
    }

    public IGameEventScriptExtensionFunction? Resolve(GameEventScriptExtensionReference reference)
    {
        if (Matches(reference, "math", "floor", 1, requireUnlabeled: true)) return MathFloor;
        if (string.Equals(reference.ExtensionName, "math", StringComparison.Ordinal) &&
            string.Equals(reference.FunctionName, "max", StringComparison.Ordinal) &&
            reference.ArgumentLabels.Count > 0 && reference.ArgumentLabels.All(IsUnlabeled)) return MathMax;
        if (string.Equals(reference.ExtensionName, "nav", StringComparison.Ordinal) &&
            string.Equals(reference.FunctionName, "shortestTurn", StringComparison.Ordinal) &&
            reference.ArgumentLabels.SequenceEqual(["from", "to"], StringComparer.Ordinal)) return NavShortestTurn;
        if (Matches(reference, "nav", "isNorth", 1, requireUnlabeled: false)) return NavIsNorth;
        if (Matches(reference, "test", "vectorSum", 1, requireUnlabeled: true)) return TestVectorSum;
        if (Matches(reference, "test", "echo", 1, requireUnlabeled: true)) return TestEcho;
        if (Matches(reference, "test", "truth", 0, requireUnlabeled: false)) return TestTruth;
        if (Matches(reference, "test", "fail", 0, requireUnlabeled: false)) return TestFail;
        return null;
    }

    private static bool Matches(
        GameEventScriptExtensionReference reference,
        string extension,
        string function,
        int argumentCount,
        bool requireUnlabeled)
        => string.Equals(reference.ExtensionName, extension, StringComparison.Ordinal) &&
           string.Equals(reference.FunctionName, function, StringComparison.Ordinal) &&
           reference.ArgumentLabels.Count == argumentCount &&
           (!requireUnlabeled || reference.ArgumentLabels.All(IsUnlabeled));

    private static bool IsUnlabeled(string label)
        => string.Equals(label, GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal);

    private delegate void ExtensionInvoke(GesExtensionCall call, GesValueArguments arguments);

    private sealed class DelegateExtensionFunction(ExtensionInvoke invoke) : IGameEventScriptExtensionFunction
    {
        public void Invoke(GesExtensionCall call) => invoke(call, call.Arguments);
    }
}
