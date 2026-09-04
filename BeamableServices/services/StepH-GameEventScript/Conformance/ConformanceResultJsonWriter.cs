using System;
using System.Collections.Generic;
using System.Text;

namespace StepH.GameEventScript.Conformance;

/// <summary>
/// Represents a conformance result json writer.
/// </summary>
public static class ConformanceResultJsonWriter
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    /// <summary>
    /// Converts this value to a text.
    /// </summary>
    /// <param name="report">The report value.</param>
    /// <returns>The result of the operation.</returns>
    public static string ToText(ConformanceRunReport report)
    {
        _ = report ?? throw new ArgumentNullException(nameof(report));
        var writer = new ConformanceCanonicalJsonWriter();
        writer.BeginObject();
        writer.Number("schemaVersion", 1);
        writer.Name("runner");
        writer.BeginObject();
        writer.String("id", report.RunnerId);
        writer.String("version", report.RunnerVersion);
        writer.EndObject();
        writer.Name("implementation");
        writer.BeginObject();
        writer.String("id", report.ImplementationId);
        writer.String("version", report.ImplementationVersion);
        writer.EndObject();
        writer.Name("capabilities");
        WriteStrings(writer, report.Capabilities);
        if (report.PerformanceProfileId is null) writer.Null("performanceProfile");
        else writer.String("performanceProfile", report.PerformanceProfileId);
        writer.String("status", Name(report.Status));
        writer.Name("summary");
        writer.BeginObject();
        writer.Number("total", report.Summary.Total);
        writer.Number("passed", report.Summary.Passed);
        writer.Number("failed", report.Summary.Failed);
        writer.Number("skipped", report.Summary.Skipped);
        writer.Number("error", report.Summary.Error);
        writer.EndObject();
        writer.Name("cases");
        writer.BeginArray();
        for (var index = 0; index < report.Cases.Count; index++) WriteCase(writer, report.Cases[index]);
        writer.EndArray();
        writer.EndObject();
        return writer.Complete();
    }

    /// <summary>
    /// Converts this value to an array.
    /// </summary>
    /// <param name="report">The report value.</param>
    /// <returns>The result of the operation.</returns>
    public static byte[] ToArray(ConformanceRunReport report) => Utf8.GetBytes(ToText(report));

    private static void WriteCase(ConformanceCanonicalJsonWriter writer, ConformanceCaseResult result)
    {
        writer.BeginObject();
        writer.String("id", result.Id);
        writer.String("suiteId", result.SuiteId);
        writer.String("caseId", result.CaseId);
        writer.String("title", result.Title);
        writer.String("kind", Name(result.Kind));
        writer.String("level", Name(result.Level));
        writer.Name("categories");
        WriteStrings(writer, result.Categories);
        writer.Name("tags");
        WriteStrings(writer, result.Tags);
        writer.String("status", Name(result.Status));
        writer.String("code", result.Code);
        writer.Name("missingCapabilities");
        WriteStrings(writer, result.MissingCapabilities);
        writer.Name("mismatches");
        writer.BeginArray();
        for (var index = 0; index < result.Mismatches.Count; index++) WriteMismatch(writer, result.Mismatches[index]);
        writer.EndArray();
        writer.Name("diagnostics");
        writer.BeginArray();
        for (var index = 0; index < result.Diagnostics.Count; index++) WriteDiagnostic(writer, result.Diagnostics[index]);
        writer.EndArray();
        writer.Name("runtimeLimits");
        writer.BeginArray();
        for (var index = 0; index < result.RuntimeLimits.Count; index++)
        {
            var limit = result.RuntimeLimits[index];
            writer.BeginObject();
            writer.String("name", limit.Name);
            writer.String("detail", limit.Detail);
            writer.Number("limit", limit.Limit);
            writer.EndObject();
        }
        writer.EndArray();
        if (result.ActualAssembler is null) writer.Null("actualAssembler");
        else writer.String("actualAssembler", result.ActualAssembler);
        if (result.Performance is null) writer.Null("performance");
        else WritePerformance(writer, result.Performance);
        if (result.TechnicalDetails is not null) writer.String("technicalDetails", result.TechnicalDetails);
        writer.EndObject();
    }

    private static void WriteMismatch(ConformanceCanonicalJsonWriter writer, ConformanceMismatch mismatch)
    {
        writer.BeginObject();
        writer.String("path", mismatch.Path);
        writer.String("code", mismatch.Code);
        if (mismatch.Expected is not null) writer.String("expected", mismatch.Expected);
        if (mismatch.Actual is not null) writer.String("actual", mismatch.Actual);
        if (mismatch.Diagnostic is not null)
        {
            writer.Name("diagnostic");
            WriteDiagnosticValue(writer, mismatch.Diagnostic);
        }
        writer.EndObject();
    }

    private static void WriteDiagnostic(ConformanceCanonicalJsonWriter writer, ConformanceResultDiagnostic diagnostic)
        => WriteDiagnosticValue(writer, diagnostic);

    private static void WriteDiagnosticValue(ConformanceCanonicalJsonWriter writer, ConformanceResultDiagnostic diagnostic)
    {
        writer.BeginObject();
        writer.String("phase", diagnostic.Phase);
        writer.String("code", diagnostic.Code);
        writer.String("message", diagnostic.Message);
        Optional(writer, "symbol", diagnostic.Symbol);
        Optional(writer, "symbolKind", diagnostic.SymbolKind);
        Optional(writer, "sourceName", diagnostic.SourceName);
        Optional(writer, "line", diagnostic.Line);
        Optional(writer, "column", diagnostic.Column);
        Optional(writer, "endLine", diagnostic.EndLine);
        Optional(writer, "endColumn", diagnostic.EndColumn);
        Optional(writer, "programName", diagnostic.ProgramName);
        Optional(writer, "handlerName", diagnostic.HandlerName);
        writer.EndObject();
    }

    private static void WritePerformance(ConformanceCanonicalJsonWriter writer, ConformancePerformanceResult performance)
    {
        writer.Name("performance");
        writer.BeginObject();
        writer.String("profile", performance.ProfileId);
        writer.Name("metrics");
        writer.BeginObject();
        for (var index = 0; index < performance.Metrics.Count; index++)
        {
            var metric = performance.Metrics[index];
            writer.Name(metric.Id);
            writer.BeginObject();
            writer.String("measured", metric.Measured);
            writer.String("reference", metric.Reference);
            writer.String("allowed", metric.Allowed);
            writer.String("unit", metric.Unit);
            writer.Boolean("passed", metric.Passed);
            writer.EndObject();
        }
        writer.EndObject();
        writer.EndObject();
    }

    private static void WriteStrings(ConformanceCanonicalJsonWriter writer, IReadOnlyList<string> values)
    {
        writer.BeginArray();
        for (var index = 0; index < values.Count; index++) writer.StringValue(values[index]);
        writer.EndArray();
    }

    private static void Optional(ConformanceCanonicalJsonWriter writer, string name, string? value)
    {
        if (value is not null) writer.String(name, value);
    }

    private static void Optional(ConformanceCanonicalJsonWriter writer, string name, uint? value)
    {
        if (value is not null) writer.Number(name, value.Value);
    }

    internal static string Name(ConformanceCaseStatus value) => value switch
    {
        ConformanceCaseStatus.Passed => "passed",
        ConformanceCaseStatus.Failed => "failed",
        ConformanceCaseStatus.Skipped => "skipped",
        _ => "error"
    };

    internal static string Name(ConformanceTestKind value) => value switch
    {
        ConformanceTestKind.ScriptApi => "scriptApi",
        ConformanceTestKind.CompileError => "compileError",
        ConformanceTestKind.LoadError => "loadError",
        ConformanceTestKind.MessageApi => "messageApi",
        ConformanceTestKind.CompileMetadata => "compileMetadata",
        ConformanceTestKind.Bytecode => "bytecode",
        ConformanceTestKind.Performance => "performance",
        ConformanceTestKind.BytecodeSnapshot => "bytecodeSnapshot",
        ConformanceTestKind.ValueApi => "valueApi",
        ConformanceTestKind.ExternalTypeApi => "externalTypeApi",
        ConformanceTestKind.ProgramBinary => "programBinary",
        _ => "unknown"
    };

    internal static string Name(ConformanceTestLevel value) => value == ConformanceTestLevel.Atomic ? "atomic" : "scenario";

}
