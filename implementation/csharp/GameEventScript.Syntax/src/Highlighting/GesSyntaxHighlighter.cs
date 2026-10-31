// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace GameEventScript.Syntax;

/// <summary>Reusable GES/GESA highlighting from the lossless syntax tree, without a Compiler, Runtime, UI or terminal dependency.</summary>
public sealed class GesSyntaxHighlighter
{
    /// <summary>The language this highlighter reads.</summary>
    public GesSyntaxLanguage Language { get; }
    /// <summary>Maximum UTF-16 input length for one call; larger inputs return an incomplete result.</summary>
    public int MaxInputLength { get; }

    /// <summary>Creates a highlighter for one language.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The language is unknown or maxInputLength is not positive.</exception>
    public GesSyntaxHighlighter(GesSyntaxLanguage language = GesSyntaxLanguage.Ges, int maxInputLength = 262144)
    {
        if (language is not (GesSyntaxLanguage.Ges or GesSyntaxLanguage.Gesa)) throw new ArgumentOutOfRangeException(nameof(language));
        if (maxInputLength <= 0) throw new ArgumentOutOfRangeException(nameof(maxInputLength));
        Language = language;
        MaxInputLength = maxInputLength;
    }

    /// <summary>
    /// Highlights a complete document, preserving CR, LF, CRLF and all UTF-16 offsets. Invalid source remains highlightable;
    /// GES diagnostics report its syntax errors or, without those, its violated single-source rules.
    /// </summary>
    /// <exception cref="ArgumentNullException">text is null.</exception>
    /// <exception cref="ArgumentException">text contains an unpaired UTF-16 surrogate.</exception>
    public GesHighlightResult Highlight(string text)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));
        GesText.RequireValidUnicode(text, nameof(text));
        if (text.Length > MaxInputLength) return GesHighlightResult.Incomplete;
        var spans = new GesSpanBuilder();
        var diagnostics = new List<GesHighlightDiagnostic>();
        if (Language == GesSyntaxLanguage.Ges) GesSourceHighlighting.Highlight(text, 0, spans, diagnostics);
        else GesAssemblyHighlighting.Highlight(text, spans);
        return new GesHighlightResult(spans.Finish(text.Length, diagnostics), diagnostics.ToArray(), true);
    }
}

// Collects the categorized pieces of a text in order, then gives each its diagnostics and merges equal neighbours.
internal sealed class GesSpanBuilder
{
    private readonly List<(int Start, int Length, GesSyntaxKind Kind)> _pieces = [];

    internal void Add(int start, int length, GesSyntaxKind kind)
    {
        if (length > 0) _pieces.Add((start, length, kind));
    }

    // Sorts the diagnostics into source order and attaches each to the pieces it overlaps; one without length overlaps the
    // piece at its position, or the last piece at the end of the text.
    internal GesHighlightSpan[] Finish(int textLength, List<GesHighlightDiagnostic> diagnostics)
    {
        var ordered = new (GesHighlightDiagnostic Diagnostic, int Start, int End, int Order)[diagnostics.Count];
        for (var i = 0; i < ordered.Length; i++)
        {
            var diagnostic = diagnostics[i];
            var start = Math.Min(diagnostic.Start, Math.Max(textLength - 1, 0));
            ordered[i] = (diagnostic, start, Math.Max(diagnostic.Start + diagnostic.Length, start + 1), i);
        }
        Array.Sort(ordered, (left, right) => left.Diagnostic.Start != right.Diagnostic.Start ? left.Diagnostic.Start.CompareTo(right.Diagnostic.Start) : left.Order.CompareTo(right.Order));
        diagnostics.Clear();
        foreach (var entry in ordered) diagnostics.Add(entry.Diagnostic);

        var spans = new List<GesHighlightSpan>(_pieces.Count);
        var active = new List<(GesHighlightDiagnostic Diagnostic, int Start, int End, int Order)>();
        var next = 0;
        foreach (var (start, length, kind) in _pieces)
        {
            var end = start + length;
            active.RemoveAll(entry => entry.End <= start);
            while (next < ordered.Length && ordered[next].Start < end) active.Add(ordered[next++]);
            IReadOnlyList<GesHighlightDiagnostic> covering = Array.Empty<GesHighlightDiagnostic>();
            if (active.Count > 0)
            {
                var found = new List<GesHighlightDiagnostic>(active.Count);
                foreach (var entry in active)
                    if (entry.End > start) found.Add(entry.Diagnostic);
                if (found.Count > 0) covering = found.AsReadOnly();
            }

            if (spans.Count > 0)
            {
                var previous = spans[^1];
                if (previous.Kind == kind && previous.Start + previous.Length == start && SameDiagnostics(previous.Diagnostics, covering))
                {
                    spans[^1] = new GesHighlightSpan(previous.Start, previous.Length + length, kind, previous.Diagnostics);
                    continue;
                }
            }

            spans.Add(new GesHighlightSpan(start, length, kind, covering));
        }

        return spans.ToArray();
    }

    private static bool SameDiagnostics(IReadOnlyList<GesHighlightDiagnostic> left, IReadOnlyList<GesHighlightDiagnostic> right)
    {
        if (left.Count != right.Count) return false;
        for (var i = 0; i < left.Count; i++)
            if (!ReferenceEquals(left[i], right[i])) return false;
        return true;
    }
}

// Categorizes GES source from its syntax tree: a token's kind, the role the parser recorded for it and, for a few
// constructs, the node that owns it.
internal static class GesSourceHighlighting
{
    // Adds the pieces of `text`, which starts at `offset` of the highlighted document, and, when requested, its diagnostics.
    internal static void Highlight(string text, int offset, GesSpanBuilder spans, List<GesHighlightDiagnostic>? diagnostics)
    {
        // Statements outside a handler, as editor input has them, are categorized as statements; the diagnostics remain
        // those the Compiler reports for the source.
        var tree = GesSyntaxParser.Parse(text, fragment: true);
        var tokens = tree.Tokens;
        var kinds = new GesSyntaxKind[tokens.Count];
        for (var i = 0; i < kinds.Length; i++)
            kinds[i] = IsDiceSides(tokens, tree.Roles, i) ? GesSyntaxKind.Number : Classify(tokens[i].Kind, tokens[i].Text, tree.Roles[i]);
        Refine(tree.Root, tree, kinds);

        for (var i = 0; i < tokens.Count; i++)
        {
            AddTrivia(text, tokens[i].FullStart, tokens[i].Start, offset, spans);
            spans.Add(offset + tokens[i].Start, tokens[i].End - tokens[i].Start, kinds[i]);
        }

        if (diagnostics is null) return;
        if (HasStatements(tree)) tree = GesSyntaxParser.Parse(text);
        var found = tree.Diagnostics.Count > 0 ? tree.Diagnostics : GesSyntaxValidator.Validate(tree);
        if (found.Count == 0) return;
        var lines = LineStarts(text);
        foreach (var diagnostic in found)
        {
            var start = Offset(text, lines, diagnostic.Line, diagnostic.Column);
            var end = Math.Max(start, Offset(text, lines, diagnostic.EndLine, diagnostic.EndColumn));
            diagnostics.Add(new GesHighlightDiagnostic(diagnostic.Code, diagnostic.Message, offset + start, end - start));
        }
    }

    // Whether a fragment holds a statement outside a handler, which a source does not parse alike.
    private static bool HasStatements(GesSyntaxTree tree)
    {
        foreach (var node in tree.Root.Children)
            if (tree.Tokens[node.FirstToken].Kind is not (GesTokenKind.Module or GesTokenKind.Constant or GesTokenKind.Record or GesTokenKind.Predicate
                or GesTokenKind.Function or GesTokenKind.On or GesTokenKind.Illegal or GesTokenKind.EndOfFile))
                return true;
        return false;
    }

    // Leading trivia holds inline whitespace, at most one `//` comment running to the end of its line, and a leading BOM.
    private static void AddTrivia(string text, int start, int end, int offset, GesSpanBuilder spans)
    {
        var comment = text.IndexOf("//", start, end - start, StringComparison.Ordinal);
        if (comment < 0) comment = end;
        spans.Add(offset + start, comment - start, GesSyntaxKind.Plain);
        spans.Add(offset + comment, end - comment, GesSyntaxKind.Comment);
    }

    // The `d6` of a compact `2d6` directly follows its count. As a name, which an implicit multiplication such as `2x`
    // reads, it has a role.
    private static bool IsDiceSides(IReadOnlyList<GesToken> tokens, IReadOnlyList<GesSyntaxRole> roles, int index)
    {
        var token = tokens[index];
        if (token.Kind != GesTokenKind.Identifier || roles[index] != GesSyntaxRole.None || index == 0 || tokens[index - 1].Kind != GesTokenKind.Float || token.FullStart != token.Start)
            return false;
        if (token.Text.Length < 2 || token.Text[0] != 'd') return false;
        for (var i = 1; i < token.Text.Length; i++)
            if (!GesText.IsAsciiDigit(token.Text[i])) return false;
        return true;
    }

    private static GesSyntaxKind Classify(GesTokenKind kind, string text, GesSyntaxRole role)
    {
        switch (role)
        {
            case GesSyntaxRole.None:
                break;
            case GesSyntaxRole.Keyword:
                return GesSyntaxKind.Keyword;
            case GesSyntaxRole.Opcode:
                return GesSyntaxKind.Builtin;
            case GesSyntaxRole.AssemblyLabel:
                return GesSyntaxKind.Label;
            case GesSyntaxRole.MessageName:
                return GesSyntaxKind.Message;
            case GesSyntaxRole.Callable or GesSyntaxRole.CallTarget or GesSyntaxRole.PredicateTarget:
                return GesSyntaxKind.Function;
            default:
                return GesSyntaxKind.Identifier;
        }

        return kind switch
        {
            GesTokenKind.EndOfFile or GesTokenKind.NewLine => GesSyntaxKind.Plain,
            // An unterminated string is an illegal token up to the end of the source.
            GesTokenKind.Illegal => text.Length > 0 && text[0] is '\'' or '"' ? GesSyntaxKind.String : GesSyntaxKind.Plain,
            GesTokenKind.Identifier => GesSyntaxKind.Identifier,
            GesTokenKind.Message => GesSyntaxKind.Message,
            GesTokenKind.TypeName => GesSyntaxKind.Type,
            GesTokenKind.ConstantReference => GesSyntaxKind.Constant,
            GesTokenKind.Tag => GesSyntaxKind.Tag,
            GesTokenKind.Float or GesTokenKind.Percentage or GesTokenKind.UnitNumber
                or GesTokenKind.MathConstantPi or GesTokenKind.MathConstantE or GesTokenKind.MathConstantTau or GesTokenKind.MathConstantInfinity => GesSyntaxKind.Number,
            GesTokenKind.Text => GesSyntaxKind.String,
            >= GesTokenKind.IntrinsicAbs and <= GesTokenKind.IntrinsicAngle => GesSyntaxKind.Builtin,
            GesTokenKind.Selector or GesTokenKind.Series or GesTokenKind.Fibonacci or GesTokenKind.Factorial
                or GesTokenKind.Min or GesTokenKind.Max or GesTokenKind.Zip => GesSyntaxKind.Builtin,
            >= GesTokenKind.SelectorAny and <= GesTokenKind.SelectorSort => GesSyntaxKind.Builtin,
            >= GesTokenKind.Dot and <= GesTokenKind.Underscore => GesSyntaxKind.Symbol,
            _ => GesSyntaxKind.Keyword
        };
    }

    // Categories that depend on the owning node: module names, extension names, two-word
    // intrinsics, the units of a quantity type and the directive dot of inline assembly.
    private static void Refine(GesSyntaxNode node, GesSyntaxTree tree, GesSyntaxKind[] kinds)
    {
        var unit = false;
        var position = node.FirstToken;
        foreach (var child in node.Children)
        {
            for (; position < child.FirstToken; position++) Own(node, tree.Tokens, position, kinds, ref unit);
            Refine(child, tree, kinds);
            position = child.EndToken;
        }
        for (; position < node.EndToken; position++) Own(node, tree.Tokens, position, kinds, ref unit);
    }

    // `unit` holds while the node's own tokens are inside the parentheses of a quantity type, such as `:Quantity(m/s)`.
    private static void Own(GesSyntaxNode node, IReadOnlyList<GesToken> tokens, int index, GesSyntaxKind[] kinds, ref bool unit)
    {
        var token = tokens[index];
        if (token.Kind == GesTokenKind.LeftParen && index > 0 && tokens[index - 1].Kind == GesTokenKind.TypeName && tokens[index - 1].Text == ":Quantity")
        {
            unit = true;
            return;
        }
        if (unit)
        {
            if (token.Kind == GesTokenKind.RightParen) unit = false;
            else if (token.Kind != GesTokenKind.NewLine) kinds[index] = GesSyntaxKind.Builtin;
            return;
        }

        switch (node.Kind)
        {
            case GesSyntaxNodeKind.ModuleDeclaration when token.Kind is not (GesTokenKind.Module or GesTokenKind.NewLine or GesTokenKind.EndOfFile):
                kinds[index] = GesSyntaxKind.Module;
                break;
            case GesSyntaxNodeKind.ExtensionCallExpression when token.Kind == GesTokenKind.Dot && index > 0 && tokens[index - 1].Kind == GesTokenKind.Selector:
                kinds[index] = GesSyntaxKind.Builtin;
                if (index + 1 < tokens.Count) kinds[index + 1] = GesSyntaxKind.Builtin;
                break;
            // The second word of an intrinsic such as `angle between`.
            case GesSyntaxNodeKind.IntrinsicCallExpression when kinds[index] == GesSyntaxKind.Keyword && token.Kind == GesTokenKind.Identifier:
                kinds[index] = GesSyntaxKind.Builtin;
                break;
            case GesSyntaxNodeKind.AssemblyBlock when token.Kind == GesTokenKind.Dot:
                kinds[index] = GesSyntaxKind.Keyword;
                break;
        }
    }

    // The offset each line starts at; a BOM occupies no column, so the first line starts after it.
    private static List<int> LineStarts(string text)
    {
        var starts = new List<int> { text.Length > 0 && text[0] == '﻿' ? 1 : 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            if (text[i] is '\r' or '\n') starts.Add(i + 1);
        }
        return starts;
    }

    // Columns count Unicode scalars.
    private static int Offset(string text, List<int> lines, int line, int column)
    {
        if (line < 1) return 0;
        if (line > lines.Count) return text.Length;
        var position = lines[line - 1];
        for (var scalars = column - 1; scalars > 0 && position < text.Length; scalars--) position += GesText.ScalarUtf16LengthAt(text, position);
        return Math.Min(position, text.Length);
    }
}
