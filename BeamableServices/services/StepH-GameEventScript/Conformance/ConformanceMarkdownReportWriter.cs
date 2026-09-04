using System;
using System.Collections.Generic;
using System.Text;

namespace StepH.GameEventScript.Conformance;

/// <summary>
/// Represents a conformance markdown report writer.
/// </summary>
public static class ConformanceMarkdownReportWriter
{
    /// <summary>
    /// Converts this value to a text.
    /// </summary>
    /// <param name="report">The report value.</param>
    /// <returns>The result of the operation.</returns>
    public static string ToText(ConformanceRunReport report)
    {
        _ = report ?? throw new ArgumentNullException(nameof(report));
        var text = new StringBuilder();
        text.Append("# GES Conformance Report\n\n");
        text.Append("- Runner: `").Append(Code(report.RunnerId)).Append("` (`").Append(Code(report.RunnerVersion)).Append("`)\n");
        text.Append("- Implementation: `").Append(Code(report.ImplementationId)).Append("` (`").Append(Code(report.ImplementationVersion)).Append("`)\n");
        text.Append("- Performance profile: ").Append(report.PerformanceProfileId is null ? "none" : "`" + Code(report.PerformanceProfileId) + "`").Append('\n');
        text.Append("- Status: **").Append(ConformanceResultJsonWriter.Name(report.Status)).Append("**\n\n");
        text.Append("| Total | Passed | Failed | Skipped | Error |\n| ---: | ---: | ---: | ---: | ---: |\n| ")
            .Append(report.Summary.Total).Append(" | ").Append(report.Summary.Passed).Append(" | ").Append(report.Summary.Failed).Append(" | ")
            .Append(report.Summary.Skipped).Append(" | ").Append(report.Summary.Error).Append(" |\n\n");

        text.Append("## Capabilities\n\nAdvertised: ");
        AppendCodes(text, report.Capabilities);
        var missing = MissingCapabilities(report);
        text.Append("\n\nMissing: ");
        AppendCodes(text, missing);
        text.Append("\n\n## Cases\n\n| Result | ID | Title | Kind | Level | Status | Code |\n| :---: | --- | --- | --- | --- | --- | --- |\n");
        for (var index = 0; index < report.Cases.Count; index++)
        {
            var result = report.Cases[index];
            text.Append("| ").Append(ResultIcon(result.Status)).Append(" | `").Append(Code(result.Id)).Append("` | ").Append(Cell(result.Title)).Append(" | ")
                .Append(ConformanceResultJsonWriter.Name(result.Kind)).Append(" | ").Append(ConformanceResultJsonWriter.Name(result.Level)).Append(" | ")
                .Append(ConformanceResultJsonWriter.Name(result.Status)).Append(" | `").Append(Code(result.Code)).Append("` |\n");
        }

        WritePerformance(text, report);
        WriteProblems(text, report);
        return text.ToString();
    }

    private static string ResultIcon(ConformanceCaseStatus status)
        => status switch
        {
            ConformanceCaseStatus.Passed => "✅",
            ConformanceCaseStatus.Skipped => "⏭️",
            ConformanceCaseStatus.Failed or ConformanceCaseStatus.Error => "❌",
            _ => "❔"
        };

    private static void WritePerformance(StringBuilder text, ConformanceRunReport report)
    {
        var count = 0;
        for (var index = 0; index < report.Cases.Count; index++) count += report.Cases[index].Performance?.Metrics.Count ?? 0;
        if (count == 0) return;
        text.Append("\n## Performance\n\n| Case | Profile | Metric | Measured | Reference | Allowed | Unit | Status |\n| --- | --- | --- | ---: | ---: | ---: | --- | --- |\n");
        for (var caseIndex = 0; caseIndex < report.Cases.Count; caseIndex++)
        {
            var result = report.Cases[caseIndex];
            if (result.Performance is null) continue;
            for (var metricIndex = 0; metricIndex < result.Performance.Metrics.Count; metricIndex++)
            {
                var metric = result.Performance.Metrics[metricIndex];
                text.Append("| `").Append(Code(result.Id)).Append("` | `").Append(Code(result.Performance.ProfileId)).Append("` | `")
                    .Append(Code(metric.Id)).Append("` | ").Append(Cell(metric.Measured)).Append(" | ").Append(Cell(metric.Reference)).Append(" | ")
                    .Append(Cell(metric.Allowed)).Append(" | ").Append(Cell(metric.Unit)).Append(" | ").Append(metric.Passed ? "passed" : "failed").Append(" |\n");
            }
        }
    }

    private static void WriteProblems(StringBuilder text, ConformanceRunReport report)
    {
        var hasProblems = false;
        for (var index = 0; index < report.Cases.Count; index++)
            if (report.Cases[index].Status is ConformanceCaseStatus.Failed or ConformanceCaseStatus.Error) { hasProblems = true; break; }
        if (!hasProblems) return;
        text.Append("\n## Failures and errors\n");
        for (var index = 0; index < report.Cases.Count; index++)
        {
            var result = report.Cases[index];
            if (result.Status is not (ConformanceCaseStatus.Failed or ConformanceCaseStatus.Error)) continue;
            text.Append("\n### `").Append(Code(result.Id)).Append("` — ").Append(Heading(result.Title)).Append("\n\n")
                .Append("Status: **").Append(ConformanceResultJsonWriter.Name(result.Status)).Append("** (`").Append(Code(result.Code)).Append("`)\n");
            if (result.MissingCapabilities.Count > 0) { text.Append("\nMissing capabilities: "); AppendCodes(text, result.MissingCapabilities); text.Append('\n'); }
            if (result.Mismatches.Count > 0)
            {
                text.Append("\n| Path | Code | Expected | Actual |\n| --- | --- | --- | --- |\n");
                for (var mismatchIndex = 0; mismatchIndex < result.Mismatches.Count; mismatchIndex++)
                {
                    var mismatch = result.Mismatches[mismatchIndex];
                    text.Append("| `").Append(Code(mismatch.Path)).Append("` | `").Append(Code(mismatch.Code)).Append("` | ")
                        .Append(Cell(mismatch.Expected ?? "—")).Append(" | ").Append(Cell(mismatch.Actual ?? "—")).Append(" |\n");
                }
            }
            if (result.Diagnostics.Count > 0)
            {
                text.Append("\n| Phase | Code | Message | Location |\n| --- | --- | --- | --- |\n");
                for (var diagnosticIndex = 0; diagnosticIndex < result.Diagnostics.Count; diagnosticIndex++)
                {
                    var diagnostic = result.Diagnostics[diagnosticIndex];
                    var location = diagnostic.SourceName is null ? "—" : diagnostic.SourceName + (diagnostic.Line is null ? "" : ":" + diagnostic.Line + (diagnostic.Column is null ? "" : ":" + diagnostic.Column));
                    text.Append("| ").Append(Cell(diagnostic.Phase)).Append(" | `").Append(Code(diagnostic.Code)).Append("` | ")
                        .Append(Cell(diagnostic.Message)).Append(" | ").Append(Cell(location)).Append(" |\n");
                }
            }
            if (result.RuntimeLimits.Count > 0)
            {
                text.Append("\n| Runtime limit | Limit | Detail |\n| --- | ---: | --- |\n");
                for (var limitIndex = 0; limitIndex < result.RuntimeLimits.Count; limitIndex++)
                {
                    var limit = result.RuntimeLimits[limitIndex];
                    text.Append("| `").Append(Code(limit.Name)).Append("` | ").Append(limit.Limit).Append(" | ").Append(Cell(limit.Detail)).Append(" |\n");
                }
            }
            if (result.TechnicalDetails is not null)
            {
                text.Append("\nTechnical details:\n\n");
                AppendIndented(text, result.TechnicalDetails);
            }
            if (result.ActualAssembler is not null)
            {
                text.Append("\nActual assembler:\n\n```gesa\n").Append(NormalizeLf(result.ActualAssembler));
                if (!result.ActualAssembler.EndsWith("\n", StringComparison.Ordinal) && !result.ActualAssembler.EndsWith("\r", StringComparison.Ordinal)) text.Append('\n');
                text.Append("```\n");
            }
        }
    }

    private static List<string> MissingCapabilities(ConformanceRunReport report)
    {
        var result = new List<string>();
        for (var caseIndex = 0; caseIndex < report.Cases.Count; caseIndex++)
            for (var missingIndex = 0; missingIndex < report.Cases[caseIndex].MissingCapabilities.Count; missingIndex++)
            {
                var value = report.Cases[caseIndex].MissingCapabilities[missingIndex];
                if (!result.Contains(value)) result.Add(value);
            }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private static void AppendCodes(StringBuilder text, IReadOnlyList<string> values)
    {
        if (values.Count == 0) { text.Append("none"); return; }
        for (var index = 0; index < values.Count; index++)
        {
            if (index > 0) text.Append(", ");
            text.Append('`').Append(Code(values[index])).Append('`');
        }
    }

    private static void AppendIndented(StringBuilder text, string value)
    {
        var normalized = NormalizeLf(value);
        var start = 0;
        while (start <= normalized.Length)
        {
            var end = normalized.IndexOf('\n', start);
            if (end < 0) end = normalized.Length;
            text.Append("    ").Append(normalized, start, end - start).Append('\n');
            if (end == normalized.Length) break;
            start = end + 1;
        }
    }

    private static string Cell(string value) => value.Replace("\\", "\\\\").Replace("|", "\\|").Replace("\r\n", "<br>").Replace("\r", "<br>").Replace("\n", "<br>");
    private static string Code(string value) => value.Replace("`", "\\`").Replace("\r", " ").Replace("\n", " ");
    private static string Heading(string value) => value.Replace("\r", " ").Replace("\n", " ").Replace("#", "\\#");
    private static string NormalizeLf(string value) => value.Replace("\r\n", "\n").Replace('\r', '\n');
}
