#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StepH.GameEventScript.Conformance;

public static class ConformanceResultJsonWriter
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static string ToText(ConformanceRunReport report)
    {
        _ = report ?? throw new ArgumentNullException(nameof(report));
        var writer = new CanonicalJsonWriter();
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

    public static byte[] ToArray(ConformanceRunReport report) => Utf8.GetBytes(ToText(report));

    private static void WriteCase(CanonicalJsonWriter writer, ConformanceCaseResult result)
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

    private static void WriteMismatch(CanonicalJsonWriter writer, ConformanceMismatch mismatch)
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

    private static void WriteDiagnostic(CanonicalJsonWriter writer, ConformanceResultDiagnostic diagnostic)
        => WriteDiagnosticValue(writer, diagnostic);

    private static void WriteDiagnosticValue(CanonicalJsonWriter writer, ConformanceResultDiagnostic diagnostic)
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

    private static void WritePerformance(CanonicalJsonWriter writer, ConformancePerformanceResult performance)
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

    private static void WriteStrings(CanonicalJsonWriter writer, IReadOnlyList<string> values)
    {
        writer.BeginArray();
        for (var index = 0; index < values.Count; index++) writer.StringValue(values[index]);
        writer.EndArray();
    }

    private static void Optional(CanonicalJsonWriter writer, string name, string? value)
    {
        if (value is not null) writer.String(name, value);
    }

    private static void Optional(CanonicalJsonWriter writer, string name, uint? value)
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
        _ => "unknown"
    };

    internal static string Name(ConformanceTestLevel value) => value == ConformanceTestLevel.Atomic ? "atomic" : "scenario";

    private sealed class CanonicalJsonWriter
    {
        private readonly StringBuilder _text = new();
        private readonly List<bool> _first = new();
        private int _depth;
        private bool _afterName;

        internal void BeginObject() { BeforeValue(); _text.Append('{'); Open(); }
        internal void EndObject() { Close(); _text.Append('}'); }
        internal void BeginArray() { BeforeValue(); _text.Append('['); Open(); }
        internal void EndArray() { Close(); _text.Append(']'); }
        internal void Name(string name) { Next(); AppendString(name); _text.Append(": "); _afterName = true; }
        internal void String(string name, string value) { Name(name); StringValueRaw(value); _afterName = false; }
        internal void Number(string name, int value) { Name(name); _text.Append(value.ToString(CultureInfo.InvariantCulture)); _afterName = false; }
        internal void Number(string name, uint value) { Name(name); _text.Append(value.ToString(CultureInfo.InvariantCulture)); _afterName = false; }
        internal void Boolean(string name, bool value) { Name(name); _text.Append(value ? "true" : "false"); _afterName = false; }
        internal void Null(string name) { Name(name); _text.Append("null"); _afterName = false; }
        internal void StringValue(string value) { BeforeValue(); StringValueRaw(value); }
        internal string Complete() => _text.Append('\n').ToString();

        private void Open() { _depth++; _first.Add(true); }
        private void Close()
        {
            var hadValues = !_first[^1];
            _first.RemoveAt(_first.Count - 1);
            _depth--;
            if (hadValues) NewLine();
        }
        private void BeforeValue()
        {
            if (_afterName) { _afterName = false; return; }
            if (_first.Count == 0) return;
            Next();
        }
        private void Next()
        {
            if (_first[^1]) _first[^1] = false;
            else _text.Append(',');
            NewLine();
        }
        private void NewLine() { _text.Append('\n'); _text.Append(' ', _depth * 2); }
        private void StringValueRaw(string value) => AppendString(value);
        private void AppendString(string value)
        {
            _text.Append('"');
            for (var index = 0; index < value.Length; index++)
            {
                var c = value[index];
                switch (c)
                {
                    case '"': _text.Append("\\\""); break;
                    case '\\': _text.Append("\\\\"); break;
                    case '\b': _text.Append("\\b"); break;
                    case '\f': _text.Append("\\f"); break;
                    case '\n': _text.Append("\\n"); break;
                    case '\r': _text.Append("\\r"); break;
                    case '\t': _text.Append("\\t"); break;
                    default:
                        if (c < 0x20) _text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else _text.Append(c);
                        break;
                }
            }
            _text.Append('"');
        }
    }
}
