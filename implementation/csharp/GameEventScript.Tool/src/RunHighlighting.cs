// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameEventScript.Tool;

// Presentation only: incomplete input must remain editable. Compilation still uses the normal compiler.
internal static class RunHighlighting
{
    private static readonly Regex Literals = new("//[^\\r\\n]*|'(?:''|[^'])*'?|\"(?:\"\"|[^\"])*\"?", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Rule[] Rules = LoadRules("GameEventScript.Grammar.json");

    internal static string Paint(string text, int color) => $"\u001b[{color}m{text}\u001b[0m";

    internal static string Render(string text) => Render(text, Highlight(text));

    internal static string RenderAssembly(string text)
    {
        try
        {
            var result = new StringBuilder();
            var position = 0;
            foreach (Match match in AssemblyGrammar.Embedded.Matches(text))
            {
                if (match.Index < position) continue;
                var start = match.Index + match.Length;
                var prefix = text[position..start];
                result.Append(Render(prefix, Highlight(prefix, AssemblyGrammar.Literals, AssemblyGrammar.Rules)));
                var end = match.Groups["sourceBlock"].Success ? SourceEnd(text, start) : text.IndexOf('\n', start);
                if (end < 0) end = text.Length;
                result.Append(Render(text[start..end]));
                position = end;
            }
            var tail = text[position..];
            return result.Append(Render(tail, Highlight(tail, AssemblyGrammar.Literals, AssemblyGrammar.Rules))).ToString();
        }
        catch (RegexMatchTimeoutException)
        {
            return text;
        }
    }

    private static int SourceEnd(string text, int start)
    {
        var literal = Literals.Match(text, start);
        foreach (Match boundary in AssemblyGrammar.SourceEnd.Matches(text, start))
        {
            while (literal.Success && literal.Index + literal.Length <= boundary.Index) literal = literal.NextMatch();
            // A multiline GES string can contain text that looks like a GESA boundary.
            if (!literal.Success || boundary.Index < literal.Index) return boundary.Index;
        }
        return text.Length;
    }

    private static string Render(string text, IReadOnlyList<ColorSpan> spans)
    {
        var result = new StringBuilder();
        var position = 0;
        foreach (var span in spans)
        {
            result.Append(text, position, span.Start - position);
            result.Append(Paint(text.Substring(span.Start, span.Length), span.Color));
            position = span.Start + span.Length;
        }
        return result.Append(text, position, text.Length - position).ToString();
    }

    internal static IReadOnlyList<ColorSpan> Highlight(string text) => Highlight(text, Literals, Rules);

    private static IReadOnlyList<ColorSpan> Highlight(string text, Regex literals, Rule[] rules)
    {
        var colors = new int[text.Length];
        try
        {
            // Protect quotes (including doubled quotes and unfinished strings) and comments before token rules.
            foreach (Match match in literals.Matches(text)) Array.Fill(colors, match.Value.StartsWith("//", StringComparison.Ordinal) ? 90 : 32, match.Index, match.Length);
            foreach (var rule in rules)
            {
                foreach (Match match in rule.Pattern.Matches(text))
                {
                    if (rule.Captures.Length == 0)
                    {
                        if (!colors.AsSpan(match.Index, match.Length).ContainsAnyExcept(0)) Array.Fill(colors, rule.Color, match.Index, match.Length);
                    }
                    else
                    {
                        foreach (var capture in rule.Captures)
                        {
                            var group = match.Groups[capture.Group];
                            if (group.Success && !colors.AsSpan(group.Index, group.Length).ContainsAnyExcept(0)) Array.Fill(colors, capture.Color, group.Index, group.Length);
                        }
                    }
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Highlighting is optional; a long/incomplete input must never prevent editing or execution.
            return [];
        }
        var spans = new List<ColorSpan>();
        for (var start = 0; start < colors.Length;)
        {
            var end = start + 1;
            while (end < colors.Length && colors[end] == colors[start]) end++;
            if (colors[start] != 0) spans.Add(new ColorSpan(start, end - start, colors[start]));
            start = end;
        }
        return spans;
    }

    private static JsonDocument LoadGrammar(string resource)
    {
        using var stream = typeof(RunHighlighting).Assembly.GetManifestResourceStream(resource)!;
        return JsonDocument.Parse(stream);
    }

    private static Rule[] LoadRules(string resource)
    {
        using var document = LoadGrammar(resource);
        return LoadRules(document.RootElement, false);
    }

    private static Rule[] LoadRules(JsonElement root, bool embedded)
    {
        var rules = new List<Rule>();
        var repository = root.GetProperty("repository");
        foreach (var include in root.GetProperty("patterns").EnumerateArray())
        {
            var name = include.GetProperty("include").GetString()![1..];
            foreach (var pattern in repository.GetProperty(name).GetProperty("patterns").EnumerateArray())
            {
                var captureProperty = "captures";
                if (!pattern.TryGetProperty("match", out var expression))
                {
                    if (!embedded || !pattern.TryGetProperty("beginCaptures", out _) || !pattern.TryGetProperty("begin", out expression)) continue;
                    captureProperty = "beginCaptures";
                }
                var color = ScopeColor(pattern.TryGetProperty("name", out var scope) ? scope.GetString()! : string.Empty);
                var captures = new List<(int Group, int Color)>();
                if (pattern.TryGetProperty(captureProperty, out var groups))
                    foreach (var group in groups.EnumerateObject())
                        captures.Add((int.Parse(group.Name, CultureInfo.InvariantCulture), ScopeColor(group.Value.GetProperty("name").GetString()!)));
                rules.Add(new Rule(Pattern(expression.GetString()!), color, captures.ToArray()));
            }
        }
        return rules.ToArray();
    }

    private static int ScopeColor(string scope)
    {
        if (scope.StartsWith("comment", StringComparison.Ordinal)) return 90;
        if (scope.StartsWith("string", StringComparison.Ordinal)) return 32;
        if (scope.StartsWith("constant.numeric", StringComparison.Ordinal) || scope.StartsWith("constant.language.numeric", StringComparison.Ordinal)) return 34;
        if (scope.StartsWith("keyword", StringComparison.Ordinal) || scope.StartsWith("constant.language.boolean", StringComparison.Ordinal)) return 35;
        if (scope.StartsWith("entity", StringComparison.Ordinal) || scope.StartsWith("support", StringComparison.Ordinal)) return 36;
        if (scope.StartsWith("variable.other.constant", StringComparison.Ordinal)) return 33;
        if (scope.StartsWith("variable.other.register", StringComparison.Ordinal)) return 33;
        if (scope.StartsWith("variable.other.label", StringComparison.Ordinal) || scope.StartsWith("constant.other.symbol", StringComparison.Ordinal)) return 36;
        return 0;
    }

    private static Regex Pattern(string expression) => new(expression, RegexOptions.CultureInvariant | RegexOptions.Multiline, TimeSpan.FromMilliseconds(100));

    private static class AssemblyGrammar
    {
        internal static readonly Regex Literals = Pattern("//[^\\r\\n]*|\"(?:\\\\.|[^\"\\\\])*\"?");
        internal static readonly Rule[] Rules;
        internal static readonly Regex Embedded;
        internal static readonly Regex SourceEnd;

        static AssemblyGrammar()
        {
            using var document = LoadGrammar("GameEventScript.AssemblerGrammar.json");
            var root = document.RootElement;
            Rules = LoadRules(root, true);
            var sources = root.GetProperty("repository").GetProperty("embedded-source").GetProperty("patterns");
            Embedded = Pattern("(?<sourceBlock>" + sources[0].GetProperty("begin").GetString() + ")|(?:" + sources[1].GetProperty("begin").GetString() + ")");
            SourceEnd = Pattern(sources[0].GetProperty("end").GetString()!);
        }
    }

    internal readonly record struct ColorSpan(int Start, int Length, int Color);
    private sealed record Rule(Regex Pattern, int Color, (int Group, int Color)[] Captures);
}
