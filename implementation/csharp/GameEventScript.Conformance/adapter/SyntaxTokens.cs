// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using GameEventScript.Api;
using GameEventScript.Compiler;
using GameEventScript.Syntax;

// Lexes every source of a request losslessly. Offsets are Unicode scalar offsets, so both ports report the same values.
internal static class SyntaxTokens
{
    // Parses every source; the lossless syntax tree also exists for sources with errors. A source named `*.gesa` is
    // highlighted as GESA and one named `*.gesd` as a GES document.
    internal static object RunTrees(JsonElement cases)
    {
        var sources = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var test in cases.EnumerateArray())
        {
            var index = 0;
            foreach (var source in test.GetProperty("sources").EnumerateArray())
            {
                var text = source.GetProperty("text").GetString()!;
                var tree = GesSyntaxParser.Parse(text);

                var fragment = GesSyntaxParser.Parse(text, fragment: true);
                var sourceName = source.TryGetProperty("name", out var name) ? name.GetString()! : "";
                var language = sourceName.EndsWith(".gesa", StringComparison.Ordinal) ? GesSyntaxLanguage.Gesa
                    : sourceName.EndsWith(".gesd", StringComparison.Ordinal) ? GesSyntaxLanguage.Gesd
                    : sourceName.EndsWith(".gess", StringComparison.Ordinal) ? GesSyntaxLanguage.Gess : GesSyntaxLanguage.Ges;
                var highlight = new GesSyntaxHighlighter(language, int.MaxValue).Highlight(text);
                var scalarOffsets = ScalarOffsets(text);
                sources[test.GetProperty("id").GetString()! + "#" + index++] = new
                {
                    tokens = tree.Tokens.Select(token => (object)new object[] { token.Kind.ToString(), scalarOffsets[token.FullStart], scalarOffsets[token.Start], scalarOffsets[token.End], token.Text }).ToArray(),
                    roles = tree.Roles.Select(role => role.ToString()).ToArray(),
                    tree = Nodes(tree.Root),
                    fragmentRoles = fragment.Roles.Select(role => role.ToString()).ToArray(),
                    fragmentTree = Nodes(fragment.Root),
                    // UTF-16 spans as [kind, start, length, diagnostic indices] and diagnostics as [code, start, length].
                    highlight = new
                    {
                        spans = highlight.Spans.Select(span => (object)new object[] { span.Kind.ToString(), span.Start, span.Length, span.Diagnostics.Select(diagnostic => IndexOf(highlight.Diagnostics, diagnostic)).ToArray() }).ToArray(),
                        diagnostics = highlight.Diagnostics.Select(diagnostic => (object)new object[] { diagnostic.Code, diagnostic.Start, diagnostic.Length }).ToArray()
                    }
                };
            }
        }

        return sources;
    }

    private static int IndexOf(IReadOnlyList<GesHighlightDiagnostic> diagnostics, GesHighlightDiagnostic diagnostic)
    {
        // Equal diagnostics map to the first, as in the Swift adapter, which compares values.
        for (var i = 0; i < diagnostics.Count; i++)
            if (diagnostics[i].Code == diagnostic.Code && diagnostics[i].Start == diagnostic.Start && diagnostics[i].Length == diagnostic.Length && diagnostics[i].Message == diagnostic.Message) return i;
        return -1;
    }

    // Nodes in preorder as [kind, first token, end token, parent index, range start, range end]; flat, so deep trees stay
    // within JSON depth limits.
    private static object[] Nodes(GesSyntaxNode root)
    {
        var nodes = new List<object>();
        var pending = new Stack<(GesSyntaxNode Node, int Parent)>();
        pending.Push((root, -1));
        while (pending.Count > 0)
        {
            var (node, parent) = pending.Pop();
            var index = nodes.Count;
            nodes.Add(new object[] { node.Kind.ToString(), node.FirstToken, node.EndToken, parent, node.RangeStart, node.RangeEnd });
            for (var child = node.Children.Count - 1; child >= 0; child--) pending.Push((node.Children[child], index));
        }

        return nodes.ToArray();
    }

    internal static object Run(JsonElement cases)
    {
        var sources = new Dictionary<string, object[]>(StringComparer.Ordinal);
        foreach (var test in cases.EnumerateArray())
        {
            var index = 0;
            foreach (var source in test.GetProperty("sources").EnumerateArray())
            {
                var text = source.GetProperty("text").GetString()!;
                var scalarOffsets = ScalarOffsets(text);
                sources[test.GetProperty("id").GetString()! + "#" + index++] = GesLexer.Tokenize(text)
                    .Select(token => (object)new object[] { token.Kind.ToString(), scalarOffsets[token.FullStart], scalarOffsets[token.Start], scalarOffsets[token.End], token.Text })
                    .ToArray();
            }
        }

        return sources;
    }

    // The scalar offset of every UTF-16 offset, including the end of the text.
    private static int[] ScalarOffsets(string text)
    {
        var offsets = new int[text.Length + 1];
        var scalar = 0;
        for (var index = 0; index < text.Length; index++)
        {
            offsets[index] = scalar;
            if (!char.IsHighSurrogate(text[index]) || index + 1 == text.Length || !char.IsLowSurrogate(text[index + 1])) scalar++;
        }

        offsets[text.Length] = scalar;
        return offsets;
    }
}
