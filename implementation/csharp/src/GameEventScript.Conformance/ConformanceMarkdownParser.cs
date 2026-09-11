// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;

namespace GameEventScript.Conformance;

/// <summary>
/// Parses the strict, portable Conformance Markdown V1 subset into an immutable normalized model.
/// </summary>
public static class ConformanceMarkdownParser
{
    /// <summary>
    /// Parses strict UTF-8 Conformance Markdown using the default bounded limits.
    /// </summary>
    /// <param name="utf8Bytes">The complete UTF-8 document bytes.</param>
    /// <returns>The normalized conformance document.</returns>
    public static ConformanceDocument Parse(byte[] utf8Bytes)
        => Parse(utf8Bytes, ConformanceParserLimits.Default);

    /// <summary>
    /// Parses strict UTF-8 Conformance Markdown using explicit bounded limits.
    /// </summary>
    /// <param name="utf8Bytes">The complete UTF-8 document bytes.</param>
    /// <param name="limits">The parser resource limits.</param>
    /// <returns>The normalized conformance document with retained UTF-8 source ranges.</returns>
    /// <exception cref="ConformanceParseException">Thrown with stable diagnostics when syntax, schema, encoding, or a resource limit is invalid.</exception>
    public static ConformanceDocument Parse(byte[] utf8Bytes, ConformanceParserLimits limits)
    {
        if (utf8Bytes is null) throw new ArgumentNullException(nameof(utf8Bytes));
        if (limits is null) throw new ArgumentNullException(nameof(limits));
        try
        {
            var text = ConformanceText.Decode(utf8Bytes, limits);
            var syntax = MarkdownScanner.Scan(text, limits);
            return ConformanceSchemaBinder.Bind(text, syntax, limits);
        }
        catch (ConformanceFailure failure)
        {
            throw new ConformanceParseException(new[] { failure.Diagnostic });
        }
    }

    /// <summary>
    /// Encodes and parses a .NET string as Conformance Markdown using the default limits.
    /// </summary>
    /// <param name="text">The complete Markdown text; unpaired UTF-16 surrogates are rejected.</param>
    /// <returns>The normalized conformance document.</returns>
    public static ConformanceDocument Parse(string text)
        => Parse(text, ConformanceParserLimits.Default);

    /// <summary>
    /// Encodes and parses a .NET string as Conformance Markdown using explicit limits.
    /// </summary>
    /// <param name="text">The complete Markdown text; unpaired UTF-16 surrogates are rejected.</param>
    /// <param name="limits">The parser resource limits.</param>
    /// <returns>The normalized conformance document with retained UTF-8 source ranges.</returns>
    /// <exception cref="ConformanceParseException">Thrown with stable diagnostics when syntax, schema, encoding, or a resource limit is invalid.</exception>
    public static ConformanceDocument Parse(string text, ConformanceParserLimits limits)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));
        try
        {
            return Parse(new UTF8Encoding(false, true).GetBytes(text), limits);
        }
        catch (EncoderFallbackException)
        {
            throw new ConformanceParseException(new[]
            {
                new ConformanceDiagnostic(ConformanceDiagnosticCodes.InvalidUtf8, "The input contains an unpaired UTF-16 surrogate.", new ConformanceSourceRange(0, 0, 1, 1, 1, 1))
            });
        }
    }
}

internal sealed class MarkdownSyntax
{
    internal MarkdownSyntax(List<ConformanceLine> frontmatter, int frontmatterStart, int frontmatterEnd, List<MarkdownCaseSyntax> cases)
    {
        Frontmatter = frontmatter;
        FrontmatterStart = frontmatterStart;
        FrontmatterEnd = frontmatterEnd;
        Cases = cases;
    }

    internal List<ConformanceLine> Frontmatter { get; }
    internal int FrontmatterStart { get; }
    internal int FrontmatterEnd { get; }
    internal List<MarkdownCaseSyntax> Cases { get; }
}

internal sealed class MarkdownCaseSyntax
{
    internal MarkdownCaseSyntax(string title, int startLineIndex)
    {
        Title = title;
        StartLineIndex = startLineIndex;
    }

    internal string Title { get; }
    internal int StartLineIndex { get; }
    internal int EndLineIndex { get; set; }
    internal MarkdownBlockSyntax? CaseBlock { get; set; }
    internal MarkdownBlockSyntax? ExpectBlock { get; set; }
    internal MarkdownBlockSyntax? AssemblerBlock { get; set; }
    internal List<MarkdownBlockSyntax> YamlBlocks { get; } = new();
    internal List<MarkdownBlockSyntax> Sources { get; } = new();
    internal List<MarkdownStepSyntax> Steps { get; } = new();
    internal bool HasStepsTable { get; set; }
    internal ConformanceSourceRange? StepsTableRange { get; set; }
}

internal sealed class MarkdownBlockSyntax
{
    internal MarkdownBlockSyntax(string info, List<ConformanceLine> payloadLines, ConformanceSourceRange blockRange, ConformanceSourceRange payloadRange)
    {
        Info = info;
        PayloadLines = payloadLines;
        BlockRange = blockRange;
        PayloadRange = payloadRange;
    }

    internal string Info { get; }
    internal List<ConformanceLine> PayloadLines { get; }
    internal ConformanceSourceRange BlockRange { get; }
    internal ConformanceSourceRange PayloadRange { get; }
    internal string Payload => ConformanceText.JoinPayload(PayloadLines);
}

internal sealed class MarkdownStepSyntax
{
    internal MarkdownStepSyntax(string id, string receive, string pump, string budget, ConformanceSourceRange range)
    {
        Id = id; Receive = receive; Pump = pump; Budget = budget; Range = range;
    }
    internal string Id { get; }
    internal string Receive { get; }
    internal string Pump { get; }
    internal string Budget { get; }
    internal ConformanceSourceRange Range { get; }
}

internal static class MarkdownScanner
{
    private static readonly HashSet<string> RecognizedFences = new(StringComparer.Ordinal)
    {
        "ges", "gesa"
    };

    internal static MarkdownSyntax Scan(ConformanceText text, ConformanceParserLimits limits)
    {
        var lines = text.Lines;
        if (lines.Count == 0 || lines[0].Text != "---")
            throw Fail(ConformanceDiagnosticCodes.MissingFrontmatter, "The document must start with YAML frontmatter.", lines.Count == 0 ? Start() : lines[0].Range());
        var close = -1;
        for (var index = 1; index < lines.Count; index++)
        {
            if (lines[index].Text == "---") { close = index; break; }
        }
        if (close < 0) throw Fail(ConformanceDiagnosticCodes.UnterminatedFrontmatter, "The YAML frontmatter is not terminated by '---'.", lines[0].Range());

        var frontmatter = lines.GetRange(1, close - 1);
        var cases = new List<MarkdownCaseSyntax>();
        MarkdownCaseSyntax? current = null;
        var fixtureMode = false;
        var fixtureSeen = false;
        for (var index = close + 1; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.Text.StartsWith("```", StringComparison.Ordinal))
            {
                var info = line.Text.Length == 3 ? string.Empty : line.Text.Substring(3);
                var yamlInTest = !fixtureMode && current is not null && info == "yaml";
                var semantic = RecognizedFences.Contains(info) || yamlInTest;
                var suspicious = info.StartsWith("ges", StringComparison.Ordinal)
                    || (!fixtureMode && current is not null && info.StartsWith("yaml ", StringComparison.Ordinal));
                var end = FindFenceEnd(lines, index + 1, semantic);
                if (end < 0 && (semantic || suspicious)) throw Fail(ConformanceDiagnosticCodes.UnterminatedFence, "A recognized semantic fence is not terminated.", line.Range());
                if (end < 0) continue;
                if (!fixtureMode && current is null && semantic)
                    throw Fail(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "A semantic fence must occur inside a test.", line.Range());
                if (!fixtureMode && current is not null)
                {
                    if (!semantic && suspicious) throw Fail(ConformanceDiagnosticCodes.UnknownSemanticFence, $"Unknown semantic fence '{info}'.", line.Range(3));
                    if (semantic)
                    {
                        var payload = lines.GetRange(index + 1, end - index - 1);
                        var block = new MarkdownBlockSyntax(info, payload, text.LinesRange(index, end), ConformanceText.PayloadRange(payload, line));
                        AddBlock(current, block);
                    }
                }
                index = end;
                continue;
            }

            if (line.Text.StartsWith("## Test:", StringComparison.Ordinal))
            {
                if (!line.Text.StartsWith("## Test: ", StringComparison.Ordinal)) throw Fail(ConformanceDiagnosticCodes.InvalidTestHeading, "A test heading must use '## Test: Display title'.", line.Range());
                var title = line.Text.Substring(9).Trim(' ', '\t');
                if (title.Length == 0) throw Fail(ConformanceDiagnosticCodes.InvalidTestHeading, "A test heading requires a display title.", line.Range());
                if (current is not null) current.EndLineIndex = index - 1;
                if (limits.MaxTests <= 0 || cases.Count >= limits.MaxTests) throw Fail(ConformanceDiagnosticCodes.YamlLimitExceeded, "The document exceeds MaxTests.", line.Range());
                current = new MarkdownCaseSyntax(title, index);
                cases.Add(current);
                fixtureMode = false;
                continue;
            }

            if (line.Text == "## Fixtures")
            {
                if (current is not null || fixtureSeen) throw Fail(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "The Fixtures section may occur once and only before the first test.", line.Range());
                fixtureSeen = true;
                fixtureMode = true;
                continue;
            }

            if (current is not null && line.Text == "### Steps")
            {
                if (current.HasStepsTable) throw Fail(ConformanceDiagnosticCodes.InvalidStepsTable, "A test may contain at most one Steps table.", line.Range());
                index = ParseSteps(lines, index, current, limits);
            }
        }

        if (current is not null) current.EndLineIndex = Math.Max(current.StartLineIndex, lines.Count - 1);
        if (cases.Count == 0) throw Fail(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "A suite must contain at least one test.", lines[close].Range());
        return new MarkdownSyntax(frontmatter, 0, close, cases);
    }

    private static int FindFenceEnd(IReadOnlyList<ConformanceLine> lines, int start, bool rejectNested)
    {
        for (var index = start; index < lines.Count; index++)
        {
            if (lines[index].Text == "```") return index;
            if (rejectNested && lines[index].Text.StartsWith("```", StringComparison.Ordinal))
                throw Fail(ConformanceDiagnosticCodes.UnknownSemanticFence, "Semantic fences cannot be nested.", lines[index].Range());
        }
        return -1;
    }

    private static void AddBlock(MarkdownCaseSyntax test, MarkdownBlockSyntax block)
    {
        switch (block.Info)
        {
            case "yaml":
                test.YamlBlocks.Add(block);
                break;
            case "gesa":
                if (test.AssemblerBlock is not null) throw Fail(ConformanceDiagnosticCodes.SchemaInvalidCardinality, "A test may contain at most one gesa block.", block.BlockRange);
                test.AssemblerBlock = block;
                break;
            case "ges":
                test.Sources.Add(block);
                break;
        }
    }

    private static int ParseSteps(IReadOnlyList<ConformanceLine> lines, int headingIndex, MarkdownCaseSyntax test, ConformanceParserLimits limits)
    {
        var index = headingIndex + 1;
        while (index < lines.Count && string.IsNullOrWhiteSpace(lines[index].Text)) index++;
        if (index + 1 >= lines.Count || lines[index].Text != "| step | receive | pump | budget |" || lines[index + 1].Text != "| --- | --- | --- | --- |")
            throw Fail(ConformanceDiagnosticCodes.InvalidStepsTable, "The Steps table header or delimiter is invalid.", index < lines.Count ? lines[index].Range() : Start());
        index += 2;
        while (index < lines.Count && lines[index].Text.Length > 0 && lines[index].Text[0] == '|')
        {
            if (limits.MaxStepsPerTest <= 0 || test.Steps.Count >= limits.MaxStepsPerTest) throw Fail(ConformanceDiagnosticCodes.YamlLimitExceeded, "The Steps table exceeds MaxStepsPerTest.", lines[index].Range());
            var cells = SplitTableRow(lines[index]);
            if (cells.Count != 4) throw Fail(ConformanceDiagnosticCodes.InvalidStepsTable, "Each Steps row must have exactly four cells.", lines[index].Range());
            test.Steps.Add(new MarkdownStepSyntax(cells[0], cells[1], cells[2], cells[3], lines[index].Range()));
            index++;
        }
        if (test.Steps.Count == 0) throw Fail(ConformanceDiagnosticCodes.InvalidStepsTable, "A Steps table must contain at least one row.", lines[index - 1].Range());
        test.HasStepsTable = true;
        var first = lines[headingIndex];
        var last = lines[index - 1];
        test.StepsTableRange = new ConformanceSourceRange(first.ByteOffset, last.ByteOffset + last.ByteLength - first.ByteOffset, first.LineNumber, 1, last.LineNumber, last.Text.Length + 1);
        return index - 1;
    }

    private static List<string> SplitTableRow(ConformanceLine line)
    {
        if (line.Text.Length < 2 || line.Text[^1] != '|') throw Fail(ConformanceDiagnosticCodes.InvalidStepsTable, "A Steps row must start and end with '|'.", line.Range());
        var content = line.Text.Substring(1, line.Text.Length - 2);
        var raw = content.Split('|');
        var result = new List<string>(raw.Length);
        foreach (var cell in raw)
        {
            if (cell.IndexOf('\\') >= 0 || cell.IndexOf('`') >= 0) throw Fail(ConformanceDiagnosticCodes.InvalidStepsTable, "Steps cells do not support escapes or inline Markdown.", line.Range());
            result.Add(cell.Trim(' ', '\t'));
        }
        return result;
    }

    private static ConformanceFailure Fail(string code, string message, ConformanceSourceRange range) => new(code, message, range);
    private static ConformanceSourceRange Start() => new(0, 0, 1, 1, 1, 1);
}
