#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Conformance;

public enum ConformanceCaseStatus
{
    Passed = 0,
    Failed = 1,
    Skipped = 2,
    Error = 3
}

public static class ConformanceRunnerCodes
{
    public const string Passed = "conformance.passed";
    public const string AssertionMismatch = "conformance.assertion.mismatch";
    public const string MissingOptionalCapability = "conformance.runner.missingOptionalCapability";
    public const string MissingCoreCapability = "conformance.runner.missingCoreCapability";
    public const string InvalidEnvironment = "conformance.runner.invalidEnvironment";
    public const string InvalidModel = "conformance.runner.invalidModel";
    public const string UnhandledException = "conformance.runner.unhandledException";
    public const string ExpectedCompileError = "conformance.compile.expectedError";
    public const string ExpectedLoadError = "conformance.load.expectedError";
    public const string MissingPerformanceProfile = "conformance.runner.missingPerformanceProfile";
    public const string PerformanceRegression = "conformance.performance.regression";
}

public sealed class ConformanceRunnerLimits
{
    public static ConformanceRunnerLimits Default { get; } = new();

    public int MaxCases { get; init; } = 65535;
    public int MaxFramesPerStep { get; init; } = 1_000_000;
}

public sealed class ConformanceRunnerOptions
{
    public static ConformanceRunnerOptions Default { get; } = new();

    public ConformanceRunnerLimits Limits { get; init; } = ConformanceRunnerLimits.Default;
    public bool IncludeTechnicalDetails { get; init; }
    public bool IncludeActualAssemblerOnSuccess { get; init; }
}

public interface IConformanceResultSink
{
    void CaseCompleted(ConformanceCaseResult result);
}

public interface IConformancePerformanceProvider
{
    ConformancePerformanceMeasurement Measure(ConformanceCase testCase, string profileId);
}

public sealed class ConformancePerformanceMeasurement
{
    public ConformancePerformanceMeasurement(IReadOnlyList<ConformanceMeasuredMetric> metrics)
        => Metrics = ConformanceDocument.Copy(metrics ?? throw new ArgumentNullException(nameof(metrics)));

    public IReadOnlyList<ConformanceMeasuredMetric> Metrics { get; }
}

public sealed class ConformanceMeasuredMetric
{
    public ConformanceMeasuredMetric(string id, string value, string unit)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Unit = unit ?? throw new ArgumentNullException(nameof(unit));
    }

    public string Id { get; }
    public string Value { get; }
    public string Unit { get; }
}

public sealed class ConformanceRunnerEnvironment
{
    public ConformanceRunnerEnvironment(
        string runnerId,
        string runnerVersion,
        string implementationId,
        string implementationVersion,
        IReadOnlyCollection<string> capabilities,
        IGameEventScriptExternalTypeCatalog? externalTypeCatalog = null,
        IGameEventScriptExtensionRegistry? extensionRegistry = null,
        IGameEventScriptExternalTypeRegistry? externalTypeRegistry = null,
        string? performanceProfileId = null,
        IConformancePerformanceProvider? performanceProvider = null)
    {
        RunnerId = runnerId ?? throw new ArgumentNullException(nameof(runnerId));
        RunnerVersion = runnerVersion ?? throw new ArgumentNullException(nameof(runnerVersion));
        ImplementationId = implementationId ?? throw new ArgumentNullException(nameof(implementationId));
        ImplementationVersion = implementationVersion ?? throw new ArgumentNullException(nameof(implementationVersion));
        _ = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        var copy = new string[capabilities.Count];
        var index = 0;
        foreach (var capability in capabilities) copy[index++] = capability;
        Array.Sort(copy, StringComparer.Ordinal);
        Capabilities = Array.AsReadOnly(copy);
        ExternalTypeCatalog = externalTypeCatalog;
        ExtensionRegistry = extensionRegistry;
        ExternalTypeRegistry = externalTypeRegistry;
        PerformanceProfileId = performanceProfileId;
        PerformanceProvider = performanceProvider;
    }

    public string RunnerId { get; }
    public string RunnerVersion { get; }
    public string ImplementationId { get; }
    public string ImplementationVersion { get; }
    public IReadOnlyList<string> Capabilities { get; }
    public IGameEventScriptExternalTypeCatalog? ExternalTypeCatalog { get; }
    public IGameEventScriptExtensionRegistry? ExtensionRegistry { get; }
    public IGameEventScriptExternalTypeRegistry? ExternalTypeRegistry { get; }
    public string? PerformanceProfileId { get; }
    public IConformancePerformanceProvider? PerformanceProvider { get; }
}

public sealed class ConformanceMismatch
{
    internal ConformanceMismatch(string path, string code, string? expected, string? actual, ConformanceResultDiagnostic? diagnostic = null)
    {
        Path = path;
        Code = code;
        Expected = expected;
        Actual = actual;
        Diagnostic = diagnostic;
    }

    public string Path { get; }
    public string Code { get; }
    public string? Expected { get; }
    public string? Actual { get; }
    public ConformanceResultDiagnostic? Diagnostic { get; }
}

public sealed class ConformanceResultDiagnostic
{
    internal ConformanceResultDiagnostic(string phase, string code, string message, string? symbol, string? symbolKind, string? sourceName, uint? line, uint? column, uint? endLine, uint? endColumn, string? programName, string? handlerName)
    {
        Phase = phase;
        Code = code;
        Message = message;
        Symbol = symbol;
        SymbolKind = symbolKind;
        SourceName = sourceName;
        Line = line;
        Column = column;
        EndLine = endLine;
        EndColumn = endColumn;
        ProgramName = programName;
        HandlerName = handlerName;
    }

    public string Phase { get; }
    public string Code { get; }
    public string Message { get; }
    public string? Symbol { get; }
    public string? SymbolKind { get; }
    public string? SourceName { get; }
    public uint? Line { get; }
    public uint? Column { get; }
    public uint? EndLine { get; }
    public uint? EndColumn { get; }
    public string? ProgramName { get; }
    public string? HandlerName { get; }
}

public sealed class ConformancePerformanceMetricResult
{
    internal ConformancePerformanceMetricResult(string id, string measured, string reference, string allowed, string unit, bool passed)
    {
        Id = id;
        Measured = measured;
        Reference = reference;
        Allowed = allowed;
        Unit = unit;
        Passed = passed;
    }

    public string Id { get; }
    public string Measured { get; }
    public string Reference { get; }
    public string Allowed { get; }
    public string Unit { get; }
    public bool Passed { get; }
}

public sealed class ConformanceRuntimeLimitResult
{
    internal ConformanceRuntimeLimitResult(string name, string detail, int limit)
    {
        Name = name;
        Detail = detail;
        Limit = limit;
    }

    public string Name { get; }
    public string Detail { get; }
    public int Limit { get; }
}

public sealed class ConformancePerformanceResult
{
    internal ConformancePerformanceResult(string profileId, IReadOnlyList<ConformancePerformanceMetricResult> metrics)
    {
        ProfileId = profileId;
        Metrics = ConformanceDocument.Copy(metrics);
    }

    public string ProfileId { get; }
    public IReadOnlyList<ConformancePerformanceMetricResult> Metrics { get; }
}

public sealed class ConformanceCaseResult
{
    internal ConformanceCaseResult(
        ConformanceCase testCase,
        ConformanceCaseStatus status,
        string code,
        IReadOnlyList<string> missingCapabilities,
        IReadOnlyList<ConformanceMismatch> mismatches,
        IReadOnlyList<ConformanceResultDiagnostic> diagnostics,
        IReadOnlyList<ConformanceRuntimeLimitResult> runtimeLimits,
        string? actualAssembler,
        ConformancePerformanceResult? performance,
        string? technicalDetails)
    {
        Id = testCase.FullId;
        SuiteId = testCase.SuiteId;
        CaseId = testCase.Id;
        Title = testCase.Title;
        Kind = testCase.Kind;
        Level = testCase.Level;
        Categories = ConformanceDocument.Copy(testCase.Categories);
        Tags = ConformanceDocument.Copy(testCase.Tags);
        Status = status;
        Code = code;
        MissingCapabilities = ConformanceDocument.Copy(missingCapabilities);
        Mismatches = ConformanceDocument.Copy(mismatches);
        Diagnostics = ConformanceDocument.Copy(diagnostics);
        RuntimeLimits = ConformanceDocument.Copy(runtimeLimits);
        ActualAssembler = actualAssembler;
        Performance = performance;
        TechnicalDetails = technicalDetails;
    }

    public string Id { get; }
    public string SuiteId { get; }
    public string CaseId { get; }
    public string Title { get; }
    public ConformanceTestKind Kind { get; }
    public ConformanceTestLevel Level { get; }
    public IReadOnlyList<string> Categories { get; }
    public IReadOnlyList<string> Tags { get; }
    public ConformanceCaseStatus Status { get; }
    public string Code { get; }
    public IReadOnlyList<string> MissingCapabilities { get; }
    public IReadOnlyList<ConformanceMismatch> Mismatches { get; }
    public IReadOnlyList<ConformanceResultDiagnostic> Diagnostics { get; }
    public IReadOnlyList<ConformanceRuntimeLimitResult> RuntimeLimits { get; }
    public string? ActualAssembler { get; }
    public ConformancePerformanceResult? Performance { get; }
    public string? TechnicalDetails { get; }
}

public sealed class ConformanceRunSummary
{
    internal ConformanceRunSummary(int total, int passed, int failed, int skipped, int error)
    {
        Total = total;
        Passed = passed;
        Failed = failed;
        Skipped = skipped;
        Error = error;
    }

    public int Total { get; }
    public int Passed { get; }
    public int Failed { get; }
    public int Skipped { get; }
    public int Error { get; }
}

public sealed class ConformanceRunReport
{
    internal ConformanceRunReport(ConformanceRunnerEnvironment environment, ConformanceCaseStatus status, ConformanceRunSummary summary, IReadOnlyList<ConformanceCaseResult> cases)
    {
        RunnerId = environment.RunnerId;
        RunnerVersion = environment.RunnerVersion;
        ImplementationId = environment.ImplementationId;
        ImplementationVersion = environment.ImplementationVersion;
        Capabilities = ConformanceDocument.Copy(environment.Capabilities);
        PerformanceProfileId = environment.PerformanceProfileId;
        Status = status;
        Summary = summary;
        Cases = ConformanceDocument.Copy(cases);
    }

    public string RunnerId { get; }
    public string RunnerVersion { get; }
    public string ImplementationId { get; }
    public string ImplementationVersion { get; }
    public IReadOnlyList<string> Capabilities { get; }
    public string? PerformanceProfileId { get; }
    public ConformanceCaseStatus Status { get; }
    public ConformanceRunSummary Summary { get; }
    public IReadOnlyList<ConformanceCaseResult> Cases { get; }
}
