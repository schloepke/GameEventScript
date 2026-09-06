// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Conformance;

/// <summary>
/// Defines the supported conformance case status values.
/// </summary>
public enum ConformanceCaseStatus
{
    /// <summary>
    /// Identifies the passed value.
    /// </summary>
    Passed = 0,
    /// <summary>
    /// Identifies the failed value.
    /// </summary>
    Failed = 1,
    /// <summary>
    /// Identifies the skipped value.
    /// </summary>
    Skipped = 2,
    /// <summary>
    /// Identifies the error value.
    /// </summary>
    Error = 3
}

/// <summary>
/// Represents a conformance runner codes.
/// </summary>
public static class ConformanceRunnerCodes
{
    /// <summary>
    /// Defines the passed value.
    /// </summary>
    public const string Passed = "conformance.passed";
    /// <summary>
    /// Defines the assertion mismatch value.
    /// </summary>
    public const string AssertionMismatch = "conformance.assertion.mismatch";
    /// <summary>
    /// Defines the missing optional capability value.
    /// </summary>
    public const string MissingOptionalCapability = "conformance.runner.missingOptionalCapability";
    /// <summary>
    /// Defines the missing core capability value.
    /// </summary>
    public const string MissingCoreCapability = "conformance.runner.missingCoreCapability";
    /// <summary>
    /// Defines the invalid environment value.
    /// </summary>
    public const string InvalidEnvironment = "conformance.runner.invalidEnvironment";
    /// <summary>
    /// Defines the invalid model value.
    /// </summary>
    public const string InvalidModel = "conformance.runner.invalidModel";
    /// <summary>
    /// Defines the unhandled exception value.
    /// </summary>
    public const string UnhandledException = "conformance.runner.unhandledException";
    /// <summary>
    /// Defines the expected compile error value.
    /// </summary>
    public const string ExpectedCompileError = "conformance.compile.expectedError";
    /// <summary>
    /// Defines the expected load error value.
    /// </summary>
    public const string ExpectedLoadError = "conformance.load.expectedError";
    /// <summary>
    /// Defines the missing performance profile value.
    /// </summary>
    public const string MissingPerformanceProfile = "conformance.runner.missingPerformanceProfile";
    /// <summary>
    /// Defines the performance regression value.
    /// </summary>
    public const string PerformanceRegression = "conformance.performance.regression";
    /// <summary>
    /// Defines the resource unavailable value.
    /// </summary>
    public const string ResourceUnavailable = "conformance.resource.unavailable";
    /// <summary>
    /// Defines the resource limit exceeded value.
    /// </summary>
    public const string ResourceLimitExceeded = "conformance.resource.limitExceeded";
    /// <summary>
    /// Defines the resource integrity mismatch value.
    /// </summary>
    public const string ResourceIntegrityMismatch = "conformance.resource.integrityMismatch";
}

/// <summary>
/// Represents a conformance runner limits.
/// </summary>
public sealed class ConformanceRunnerLimits
{
    /// <summary>
    /// Gets the default.
    /// </summary>
    public static ConformanceRunnerLimits Default { get; } = new();

    /// <summary>
    /// Gets the max cases.
    /// </summary>
    public int MaxCases { get; init; } = 65535;
    /// <summary>
    /// Gets the max frames per step.
    /// </summary>
    public int MaxFramesPerStep { get; init; } = 1_000_000;
    /// <summary>
    /// Gets the max resource bytes.
    /// </summary>
    public int MaxResourceBytes { get; init; } = 64 * 1024 * 1024;
}

/// <summary>
/// Represents a conformance runner options.
/// </summary>
public sealed class ConformanceRunnerOptions
{
    /// <summary>
    /// Gets the default.
    /// </summary>
    public static ConformanceRunnerOptions Default { get; } = new();

    /// <summary>
    /// Gets the limits.
    /// </summary>
    public ConformanceRunnerLimits Limits { get; init; } = ConformanceRunnerLimits.Default;
    /// <summary>
    /// Gets the include technical details.
    /// </summary>
    public bool IncludeTechnicalDetails { get; init; }
    /// <summary>
    /// Gets the include actual assembler on success.
    /// </summary>
    public bool IncludeActualAssemblerOnSuccess { get; init; }
}

/// <summary>
/// Defines the contract for i conformance result sink.
/// </summary>
public interface IConformanceResultSink
{
    /// <summary>
    /// Performs the case completed operation.
    /// </summary>
    /// <param name="result">The result value.</param>
    void CaseCompleted(ConformanceCaseResult result);
}

/// <summary>
/// Defines the contract for i conformance performance provider.
/// </summary>
public interface IConformancePerformanceProvider
{
    /// <summary>
    /// Performs the measure operation.
    /// </summary>
    /// <param name="testCase">The test case value.</param>
    /// <param name="profileId">The profile id value.</param>
    /// <returns>The result of the operation.</returns>
    ConformancePerformanceMeasurement Measure(ConformanceCase testCase, string profileId);
}

/// <summary>
/// Defines the supported conformance resource status values.
/// </summary>
public enum ConformanceResourceStatus
{
    /// <summary>
    /// Identifies the found value.
    /// </summary>
    Found = 0,
    /// <summary>
    /// Identifies the not found value.
    /// </summary>
    NotFound = 1,
    /// <summary>
    /// Identifies the limit exceeded value.
    /// </summary>
    LimitExceeded = 2,
    /// <summary>
    /// Identifies the error value.
    /// </summary>
    Error = 3
}

/// <summary>
/// Defines the contract for i conformance resource resolver.
/// </summary>
public interface IConformanceResourceResolver
{
    /// <summary>
    /// Resolves the value.
    /// </summary>
    /// <param name="resourceId">The resource id value.</param>
    /// <param name="maximumByteCount">The maximum byte count value.</param>
    /// <returns>The result of the operation.</returns>
    ConformanceResourceResult Resolve(string resourceId, int maximumByteCount);
}

/// <summary>
/// Represents a conformance resource result.
/// </summary>
public sealed class ConformanceResourceResult
{
    /// <summary>
    /// Initializes a new instance of Conformance Resource Result.
    /// </summary>
    /// <param name="status">The status value.</param>
    /// <param name="bytes">The bytes value.</param>
    /// <param name="errorCode">The error code value.</param>
    public ConformanceResourceResult(ConformanceResourceStatus status, IReadOnlyList<byte>? bytes = null, string? errorCode = null)
    {
        Status = status;
        if (status == ConformanceResourceStatus.Found)
        {
            if (bytes is null) throw new ArgumentNullException(nameof(bytes));
            var copy = new byte[bytes.Count];
            for (var index = 0; index < copy.Length; index++) copy[index] = bytes[index];
            Bytes = Array.AsReadOnly(copy);
        }
        else
        {
            if (bytes is not null) throw new ArgumentException("A failed resource result cannot contain bytes.", nameof(bytes));
            Bytes = Array.AsReadOnly(Array.Empty<byte>());
        }
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the status.
    /// </summary>
    public ConformanceResourceStatus Status { get; }
    /// <summary>
    /// Gets the bytes.
    /// </summary>
    public IReadOnlyList<byte> Bytes { get; }
    /// <summary>
    /// Gets the error code.
    /// </summary>
    public string? ErrorCode { get; }
}

/// <summary>
/// Represents a conformance performance measurement.
/// </summary>
public sealed class ConformancePerformanceMeasurement
{
    /// <summary>
    /// Initializes a new instance of Conformance Performance Measurement.
    /// </summary>
    /// <param name="metrics">The metrics value.</param>
    public ConformancePerformanceMeasurement(IReadOnlyList<ConformanceMeasuredMetric> metrics)
        => Metrics = ConformanceDocument.Copy(metrics ?? throw new ArgumentNullException(nameof(metrics)));

    /// <summary>
    /// Gets the metrics.
    /// </summary>
    public IReadOnlyList<ConformanceMeasuredMetric> Metrics { get; }
}

/// <summary>
/// Represents a conformance measured metric.
/// </summary>
public sealed class ConformanceMeasuredMetric
{
    /// <summary>
    /// Initializes a new instance of Conformance Measured Metric.
    /// </summary>
    /// <param name="id">The id value.</param>
    /// <param name="value">The value value.</param>
    /// <param name="unit">The unit value.</param>
    public ConformanceMeasuredMetric(string id, string value, string unit)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Unit = unit ?? throw new ArgumentNullException(nameof(unit));
    }

    /// <summary>
    /// Gets the id.
    /// </summary>
    public string Id { get; }
    /// <summary>
    /// Gets the value.
    /// </summary>
    public string Value { get; }
    /// <summary>
    /// Gets the unit.
    /// </summary>
    public string Unit { get; }
}

/// <summary>
/// Represents a conformance runner environment.
/// </summary>
public sealed class ConformanceRunnerEnvironment
{
    /// <summary>
    /// Initializes a new instance of Conformance Runner Environment.
    /// </summary>
    /// <param name="runnerId">The runner id value.</param>
    /// <param name="runnerVersion">The runner version value.</param>
    /// <param name="implementationId">The implementation id value.</param>
    /// <param name="implementationVersion">The implementation version value.</param>
    /// <param name="capabilities">The capabilities value.</param>
    /// <param name="externalTypeCatalog">The external type catalog value.</param>
    /// <param name="extensionRegistry">The extension registry value.</param>
    /// <param name="externalTypeRegistry">The external type registry value.</param>
    /// <param name="performanceProfileId">The performance profile id value.</param>
    /// <param name="performanceProvider">The performance provider value.</param>
    /// <param name="resourceResolver">The resource resolver value.</param>
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
        IConformancePerformanceProvider? performanceProvider = null,
        IConformanceResourceResolver? resourceResolver = null)
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
        ResourceResolver = resourceResolver;
    }

    /// <summary>
    /// Gets the runner id.
    /// </summary>
    public string RunnerId { get; }
    /// <summary>
    /// Gets the runner version.
    /// </summary>
    public string RunnerVersion { get; }
    /// <summary>
    /// Gets the implementation id.
    /// </summary>
    public string ImplementationId { get; }
    /// <summary>
    /// Gets the implementation version.
    /// </summary>
    public string ImplementationVersion { get; }
    /// <summary>
    /// Gets the capabilities.
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; }
    /// <summary>
    /// Gets the external type catalog.
    /// </summary>
    public IGameEventScriptExternalTypeCatalog? ExternalTypeCatalog { get; }
    /// <summary>
    /// Gets the extension registry.
    /// </summary>
    public IGameEventScriptExtensionRegistry? ExtensionRegistry { get; }
    /// <summary>
    /// Gets the external type registry.
    /// </summary>
    public IGameEventScriptExternalTypeRegistry? ExternalTypeRegistry { get; }
    /// <summary>
    /// Gets the performance profile id.
    /// </summary>
    public string? PerformanceProfileId { get; }
    /// <summary>
    /// Gets the performance provider.
    /// </summary>
    public IConformancePerformanceProvider? PerformanceProvider { get; }
    /// <summary>
    /// Gets the resource resolver.
    /// </summary>
    public IConformanceResourceResolver? ResourceResolver { get; }
}

/// <summary>
/// Represents a conformance mismatch.
/// </summary>
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

    /// <summary>
    /// Gets the path.
    /// </summary>
    public string Path { get; }
    /// <summary>
    /// Gets the code.
    /// </summary>
    public string Code { get; }
    /// <summary>
    /// Gets the expected.
    /// </summary>
    public string? Expected { get; }
    /// <summary>
    /// Gets the actual.
    /// </summary>
    public string? Actual { get; }
    /// <summary>
    /// Gets the diagnostic.
    /// </summary>
    public ConformanceResultDiagnostic? Diagnostic { get; }
}

/// <summary>
/// Represents a conformance result diagnostic.
/// </summary>
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

    /// <summary>
    /// Gets the phase.
    /// </summary>
    public string Phase { get; }
    /// <summary>
    /// Gets the code.
    /// </summary>
    public string Code { get; }
    /// <summary>
    /// Gets the message.
    /// </summary>
    public string Message { get; }
    /// <summary>
    /// Gets the symbol.
    /// </summary>
    public string? Symbol { get; }
    /// <summary>
    /// Gets the symbol kind.
    /// </summary>
    public string? SymbolKind { get; }
    /// <summary>
    /// Gets the source name.
    /// </summary>
    public string? SourceName { get; }
    /// <summary>
    /// Gets the line.
    /// </summary>
    public uint? Line { get; }
    /// <summary>
    /// Gets the column.
    /// </summary>
    public uint? Column { get; }
    /// <summary>
    /// Gets the end line.
    /// </summary>
    public uint? EndLine { get; }
    /// <summary>
    /// Gets the end column.
    /// </summary>
    public uint? EndColumn { get; }
    /// <summary>
    /// Gets the program name.
    /// </summary>
    public string? ProgramName { get; }
    /// <summary>
    /// Gets the handler name.
    /// </summary>
    public string? HandlerName { get; }
}

/// <summary>
/// Represents a conformance performance metric result.
/// </summary>
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

    /// <summary>
    /// Gets the id.
    /// </summary>
    public string Id { get; }
    /// <summary>
    /// Gets the measured.
    /// </summary>
    public string Measured { get; }
    /// <summary>
    /// Gets the reference.
    /// </summary>
    public string Reference { get; }
    /// <summary>
    /// Gets the allowed.
    /// </summary>
    public string Allowed { get; }
    /// <summary>
    /// Gets the unit.
    /// </summary>
    public string Unit { get; }
    /// <summary>
    /// Gets the passed.
    /// </summary>
    public bool Passed { get; }
}

/// <summary>
/// Represents a conformance runtime limit result.
/// </summary>
public sealed class ConformanceRuntimeLimitResult
{
    internal ConformanceRuntimeLimitResult(string name, string detail, int limit)
    {
        Name = name;
        Detail = detail;
        Limit = limit;
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the detail.
    /// </summary>
    public string Detail { get; }
    /// <summary>
    /// Gets the limit.
    /// </summary>
    public int Limit { get; }
}

/// <summary>
/// Represents a conformance performance result.
/// </summary>
public sealed class ConformancePerformanceResult
{
    internal ConformancePerformanceResult(string profileId, IReadOnlyList<ConformancePerformanceMetricResult> metrics)
    {
        ProfileId = profileId;
        Metrics = ConformanceDocument.Copy(metrics);
    }

    /// <summary>
    /// Gets the profile id.
    /// </summary>
    public string ProfileId { get; }
    /// <summary>
    /// Gets the metrics.
    /// </summary>
    public IReadOnlyList<ConformancePerformanceMetricResult> Metrics { get; }
}

/// <summary>
/// Represents a conformance case result.
/// </summary>
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

    /// <summary>
    /// Gets the id.
    /// </summary>
    public string Id { get; }
    /// <summary>
    /// Gets the suite id.
    /// </summary>
    public string SuiteId { get; }
    /// <summary>
    /// Gets the case id.
    /// </summary>
    public string CaseId { get; }
    /// <summary>
    /// Gets the title.
    /// </summary>
    public string Title { get; }
    /// <summary>
    /// Gets the kind.
    /// </summary>
    public ConformanceTestKind Kind { get; }
    /// <summary>
    /// Gets the level.
    /// </summary>
    public ConformanceTestLevel Level { get; }
    /// <summary>
    /// Gets the categories.
    /// </summary>
    public IReadOnlyList<string> Categories { get; }
    /// <summary>
    /// Gets the tags.
    /// </summary>
    public IReadOnlyList<string> Tags { get; }
    /// <summary>
    /// Gets the status.
    /// </summary>
    public ConformanceCaseStatus Status { get; }
    /// <summary>
    /// Gets the code.
    /// </summary>
    public string Code { get; }
    /// <summary>
    /// Gets the missing capabilities.
    /// </summary>
    public IReadOnlyList<string> MissingCapabilities { get; }
    /// <summary>
    /// Gets the mismatches.
    /// </summary>
    public IReadOnlyList<ConformanceMismatch> Mismatches { get; }
    /// <summary>
    /// Gets the diagnostics.
    /// </summary>
    public IReadOnlyList<ConformanceResultDiagnostic> Diagnostics { get; }
    /// <summary>
    /// Gets the runtime limits.
    /// </summary>
    public IReadOnlyList<ConformanceRuntimeLimitResult> RuntimeLimits { get; }
    /// <summary>
    /// Gets the actual assembler.
    /// </summary>
    public string? ActualAssembler { get; }
    /// <summary>
    /// Gets the performance.
    /// </summary>
    public ConformancePerformanceResult? Performance { get; }
    /// <summary>
    /// Gets the technical details.
    /// </summary>
    public string? TechnicalDetails { get; }
}

/// <summary>
/// Represents a conformance run summary.
/// </summary>
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

    /// <summary>
    /// Gets the total.
    /// </summary>
    public int Total { get; }
    /// <summary>
    /// Gets the passed.
    /// </summary>
    public int Passed { get; }
    /// <summary>
    /// Gets the failed.
    /// </summary>
    public int Failed { get; }
    /// <summary>
    /// Gets the skipped.
    /// </summary>
    public int Skipped { get; }
    /// <summary>
    /// Gets the error.
    /// </summary>
    public int Error { get; }
}

/// <summary>
/// Represents a conformance run report.
/// </summary>
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

    /// <summary>
    /// Gets the runner id.
    /// </summary>
    public string RunnerId { get; }
    /// <summary>
    /// Gets the runner version.
    /// </summary>
    public string RunnerVersion { get; }
    /// <summary>
    /// Gets the implementation id.
    /// </summary>
    public string ImplementationId { get; }
    /// <summary>
    /// Gets the implementation version.
    /// </summary>
    public string ImplementationVersion { get; }
    /// <summary>
    /// Gets the capabilities.
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; }
    /// <summary>
    /// Gets the performance profile id.
    /// </summary>
    public string? PerformanceProfileId { get; }
    /// <summary>
    /// Gets the status.
    /// </summary>
    public ConformanceCaseStatus Status { get; }
    /// <summary>
    /// Gets the summary.
    /// </summary>
    public ConformanceRunSummary Summary { get; }
    /// <summary>
    /// Gets the cases.
    /// </summary>
    public IReadOnlyList<ConformanceCaseResult> Cases { get; }
}
