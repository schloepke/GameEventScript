// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;

namespace GameEventScript.Syntax;

/// <summary>Optional pure string renderer. Terminal detection, NO_COLOR, console output and diagnostics belong to the embedding.</summary>
public static class GesAnsiRenderer
{
    /// <summary>The CLI's default 16-color foreground palette; null leaves text unchanged.</summary>
    public static int? DefaultColor(GesSyntaxKind kind) => kind switch
    {
        GesSyntaxKind.Comment => 90,
        GesSyntaxKind.String => 32,
        GesSyntaxKind.Number => 34,
        GesSyntaxKind.Keyword => 35,
        GesSyntaxKind.Constant or GesSyntaxKind.Register => 33,
        GesSyntaxKind.Builtin or GesSyntaxKind.Message or GesSyntaxKind.Type or GesSyntaxKind.Tag
            or GesSyntaxKind.Function or GesSyntaxKind.Module or GesSyntaxKind.Label => 36,
        _ => null
    };

    /// <summary>Renders UTF-16 spans using the default palette or a custom category-to-ANSI-color mapping. Source characters are preserved verbatim.</summary>
    /// <exception cref="ArgumentException">Spans overlap, exceed source bounds, split a surrogate pair, or a palette returns a color outside 30..37, 39, 90..97.</exception>
    /// <exception cref="ArgumentNullException">source or spans is null.</exception>
    public static string Render(string source, IReadOnlyList<GesHighlightSpan> spans, Func<GesSyntaxKind, int?>? color = null)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (spans is null) throw new ArgumentNullException(nameof(spans));
        color ??= DefaultColor;
        var result = new StringBuilder();
        var position = 0;
        int? active = null;
        void Switch(int? next)
        {
            if (active == next) return;
            if (active is not null) result.Append("\u001b[0m");
            if (next is not null) result.Append("\u001b[").Append(next.Value).Append('m');
            active = next;
        }
        foreach (var span in spans)
        {
            if (span is null || span.Start < position || span.Length <= 0 || span.Start > source.Length - span.Length || Splits(source, span.Start) || Splits(source, span.Start + span.Length))
                throw new ArgumentException("Invalid UTF-16 spans.", nameof(spans));
            if (span.Start > position)
            {
                Switch(null);
                result.Append(source, position, span.Start - position);
            }
            var next = color(span.Kind);
            if (next is not null && next is not (>= 30 and <= 37) && next != 39 && next is not (>= 90 and <= 97)) throw new ArgumentException("Invalid ANSI foreground color.", nameof(color));
            Switch(next);
            result.Append(source, span.Start, span.Length);
            position = span.Start + span.Length;
        }
        Switch(null);
        return result.Append(source, position, source.Length - position).ToString();
    }

    private static bool Splits(string text, int index) => index > 0 && index < text.Length && char.IsHighSurrogate(text[index - 1]) && char.IsLowSurrogate(text[index]);
}
