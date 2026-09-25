// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;
using GameEventScript.SyntaxHighlighter;

[assembly: DoNotParallelize]

namespace GameEventScript.Tests;

[TestClass]
public sealed class HighlightingTests
{
    [TestMethod]
    public void SharedMarkdownCasesMatchAndIncrementalStatesPreserveScopes()
    {
        var markdown = File.ReadAllText(Path.Combine(TestRepositoryPaths.Root, "conformance/highlighting/SyntaxHighlighting.md"));
        var cases = Regex.Matches(markdown, "```highlight\\s*\\n(.*?)\\n```", RegexOptions.Singleline);
        Assert.IsGreaterThanOrEqualTo(11, cases.Count);
        foreach (Match match in cases)
        {
            using var json = JsonDocument.Parse(match.Groups[1].Value);
            var data = json.RootElement;
            var name = data.GetProperty("name").GetString()!;
            var source = data.GetProperty("source").GetString()!;
            var language = data.GetProperty("language").GetString() == "ges" ? GameEventScriptSyntaxLanguage.Ges : GameEventScriptSyntaxLanguage.Gesa;
            var highlighter = new GameEventScriptSyntaxHighlighter(language);
            var result = highlighter.Highlight(source);
            Assert.IsTrue(result.IsComplete, name);
            var units = Expand(result.Spans, source.Length);
            foreach (var expected in data.GetProperty("expect").EnumerateArray())
            {
                var text = expected.GetProperty("text").GetString()!;
                var occurrence = expected.TryGetProperty("occurrence", out var value) ? value.GetInt32() : 0;
                var start = -1;
                for (var i = 0; i <= occurrence; i++) start = source.IndexOf(text, start + 1, StringComparison.Ordinal);
                Assert.IsGreaterThanOrEqualTo(0, start, name);
                var kind = Enum.Parse<GameEventScriptSyntaxKind>(expected.GetProperty("kind").GetString()!, true);
                for (var i = start; i < start + text.Length; i++)
                {
                    Assert.AreEqual(kind, units[i].Kind, name + ": " + text);
                    if (expected.TryGetProperty("scope", out var scope)) Assert.IsTrue(units[i].Scopes.Contains(scope.GetString()!), name);
                }
            }
            var cursor = 0;
            var state = highlighter.CreateState();
            foreach (Match line in Regex.Matches(source, "[^\\r\\n]*(?:\\r\\n|\\r|\\n|$)"))
            {
                if (line.Length == 0 && cursor == source.Length) break;
                var incremental = highlighter.HighlightLine(line.Value, state);
                Assert.IsTrue(incremental.IsComplete, name);
                var parts = Expand(incremental.Spans, line.Length);
                for (var i = 0; i < parts.Length; i++)
                {
                    Assert.AreEqual(units[cursor + i].Kind, parts[i].Kind, name);
                    CollectionAssert.AreEqual(units[cursor + i].Scopes.ToArray(), parts[i].Scopes.ToArray(), name);
                }
                cursor += line.Length;
                state = incremental.NextState!;
            }
            Assert.AreEqual(result.NextState, state, name);
            var ansi = GameEventScriptAnsiRenderer.Render(source, result.Spans);
            Assert.AreEqual(source, Regex.Replace(ansi, "\u001b\\[[0-9;]*m", ""), name);
        }
    }

    [TestMethod]
    public void EditChangesContinuationAndStatesWorkAcrossInstances()
    {
        var highlighter = new GameEventScriptSyntaxHighlighter();
        var open = highlighter.HighlightLine("'open\n");
        var inside = new GameEventScriptSyntaxHighlighter().HighlightLine("emit Done()", open.NextState);
        Assert.IsTrue(inside.Spans.All(span => span.Kind == GameEventScriptSyntaxKind.String));
        var closed = highlighter.HighlightLine("'closed'\n");
        Assert.AreEqual(highlighter.CreateState(), closed.NextState);
        Assert.AreNotEqual(open.NextState, closed.NextState);
        Assert.AreEqual(GameEventScriptSyntaxKind.Keyword, highlighter.HighlightLine("emit Done()", closed.NextState).Spans[0].Kind);
    }

    [TestMethod]
    public void BoundsAndInvalidApiInputsAreExplicit()
    {
        var small = new GameEventScriptSyntaxHighlighter(maxInputLength: 4);
        Assert.IsFalse(small.Highlight("12345").IsComplete);
        Assert.IsNull(small.Highlight("12345").NextState);
        Assert.HasCount(0, small.Highlight("12345").Spans);
        Assert.IsTrue(small.Highlight("").IsComplete);
        Assert.ThrowsExactly<ArgumentException>(() => small.HighlightLine("x\ny"));
        Assert.ThrowsExactly<ArgumentException>(() => small.HighlightLine("x", new GameEventScriptSyntaxHighlighter(GameEventScriptSyntaxLanguage.Gesa).CreateState()));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GameEventScriptSyntaxHighlighter(maxInputLength: 0));
        var spans = small.Highlight("1234").Spans;
        Assert.ThrowsExactly<ArgumentException>(() => GameEventScriptAnsiRenderer.Render("1", spans));
        Assert.ThrowsExactly<ArgumentException>(() => GameEventScriptAnsiRenderer.Render("1234", spans, _ => 123));
        Assert.AreEqual("\u001b[31m1234\u001b[0m", GameEventScriptAnsiRenderer.Render("1234", spans, _ => 31));
    }

    private static GameEventScriptHighlightSpan[] Expand(IReadOnlyList<GameEventScriptHighlightSpan> spans, int length)
    {
        var units = new GameEventScriptHighlightSpan[length];
        var cursor = 0;
        foreach (var span in spans)
        {
            Assert.AreEqual(cursor, span.Start);
            for (var i = 0; i < span.Length; i++) units[cursor++] = span;
        }
        Assert.AreEqual(length, cursor);
        return units;
    }
}
