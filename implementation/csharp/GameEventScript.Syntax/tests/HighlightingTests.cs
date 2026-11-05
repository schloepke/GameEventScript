// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;
using GameEventScript.Syntax;

[assembly: DoNotParallelize]

namespace GameEventScript.Tests;

[TestClass]
public sealed class HighlightingTests
{
    [TestMethod]
    public void SharedMarkdownCasesMatchKindsAndDiagnostics()
    {
        var markdown = File.ReadAllText(Path.Combine(TestRepositoryPaths.Root, "conformance/highlighting/SyntaxHighlighting.md"));
        var cases = Regex.Matches(markdown, "```highlight\\s*\\n(.*?)\\n```", RegexOptions.Singleline);
        Assert.IsGreaterThanOrEqualTo(46, cases.Count);
        foreach (Match match in cases)
        {
            using var json = JsonDocument.Parse(match.Groups[1].Value);
            var data = json.RootElement;
            var name = data.GetProperty("name").GetString()!;
            var source = data.GetProperty("source").GetString()!;
            var language = data.GetProperty("language").GetString() switch { "ges" => GesSyntaxLanguage.Ges, "gesd" => GesSyntaxLanguage.Gesd, "gess" => GesSyntaxLanguage.Gess, _ => GesSyntaxLanguage.Gesa };
            var result = new GesSyntaxHighlighter(language).Highlight(source);
            Assert.IsTrue(result.IsComplete, name);
            var units = Expand(result.Spans, source.Length);
            foreach (var expected in data.GetProperty("expect").EnumerateArray())
            {
                var (start, length) = Select(source, expected, name);
                var kind = Enum.Parse<GesSyntaxKind>(expected.GetProperty("kind").GetString()!, true);
                for (var i = start; i < start + length; i++) Assert.AreEqual(kind, units[i].Kind, name + ": " + expected.GetProperty("text").GetString());
            }

            var diagnostics = data.TryGetProperty("diagnostics", out var list) ? list.EnumerateArray().ToArray() : result.Diagnostics.Select(_ => default(JsonElement)).ToArray();
            Assert.HasCount(diagnostics.Length, result.Diagnostics, name);
            for (var i = 0; i < diagnostics.Length && diagnostics[i].ValueKind != JsonValueKind.Undefined; i++)
            {
                var (start, length) = Select(source, diagnostics[i], name);
                Assert.AreEqual(diagnostics[i].GetProperty("code").GetString(), result.Diagnostics[i].Code, name);
                Assert.AreEqual(start, result.Diagnostics[i].Start, name);
                Assert.AreEqual(length, result.Diagnostics[i].Length, name);
            }

            foreach (var span in result.Spans)
            {
                var overlapping = result.Diagnostics.Where(diagnostic => Overlaps(diagnostic, span, source.Length)).ToArray();
                CollectionAssert.AreEqual(overlapping, span.Diagnostics.ToArray(), name);
            }

            var ansi = GesAnsiRenderer.Render(source, result.Spans);
            Assert.AreEqual(source, Regex.Replace(ansi, "\u001b\\[[0-9;]*m", ""), name);
        }
    }

    [TestMethod]
    public void DiagnosticsWithoutLengthBelongToTheSpanAtTheirPosition()
    {
        var result = new GesSyntaxHighlighter().Highlight("on Start() {");
        Assert.IsNotEmpty(result.Diagnostics);
        Assert.IsTrue(result.Diagnostics.All(diagnostic => diagnostic.Start == 12 && diagnostic.Length == 0));
        CollectionAssert.AreEqual(result.Diagnostics.ToArray(), result.Spans[^1].Diagnostics.ToArray());
        Assert.IsTrue(result.Spans.Take(result.Spans.Count - 1).All(span => span.Diagnostics.Count == 0));
    }

    [TestMethod]
    public void BoundsAndInvalidApiInputsAreExplicit()
    {
        var small = new GesSyntaxHighlighter(maxInputLength: 4);
        Assert.IsFalse(small.Highlight("12345").IsComplete);
        Assert.HasCount(0, small.Highlight("12345").Spans);
        Assert.HasCount(0, small.Highlight("12345").Diagnostics);
        Assert.IsTrue(small.Highlight("").IsComplete);
        Assert.HasCount(0, small.Highlight("").Spans);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GesSyntaxHighlighter(maxInputLength: 0));
        Assert.ThrowsExactly<ArgumentException>(() => small.Highlight("\ud800"));
        var spans = small.Highlight("1234").Spans;
        Assert.ThrowsExactly<ArgumentException>(() => GesAnsiRenderer.Render("1", spans));
        Assert.ThrowsExactly<ArgumentException>(() => GesAnsiRenderer.Render("1234", spans, _ => 123));
        Assert.AreEqual("\u001b[31m1234\u001b[0m", GesAnsiRenderer.Render("1234", spans, _ => 31));
    }

    private static (int Start, int Length) Select(string source, JsonElement expected, string name)
    {
        var text = expected.GetProperty("text").GetString()!;
        var occurrence = expected.TryGetProperty("occurrence", out var value) ? value.GetInt32() : 0;
        var start = -1;
        for (var i = 0; i <= occurrence; i++) start = source.IndexOf(text, start + 1, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start, name + ": " + text);
        if (expected.TryGetProperty("after", out var after) && after.GetBoolean()) start += text.Length;
        return (start, expected.TryGetProperty("length", out var length) ? length.GetInt32() : text.Length);
    }

    private static bool Overlaps(GesHighlightDiagnostic diagnostic, GesHighlightSpan span, int length)
    {
        var start = Math.Min(diagnostic.Start, Math.Max(length - 1, 0));
        var end = Math.Max(diagnostic.Start + diagnostic.Length, start + 1);
        return start < span.Start + span.Length && span.Start < end;
    }

    private static GesHighlightSpan[] Expand(IReadOnlyList<GesHighlightSpan> spans, int length)
    {
        var units = new GesHighlightSpan[length];
        var cursor = 0;
        foreach (var span in spans)
        {
            Assert.AreEqual(cursor, span.Start);
            Assert.IsGreaterThan(0, span.Length);
            for (var i = 0; i < span.Length; i++) units[cursor++] = span;
        }
        Assert.AreEqual(length, cursor);
        return units;
    }
}
