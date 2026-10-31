// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Syntax;

namespace GameEventScript.Tool;

// CLI presentation policy; all syntax recognition lives in the standalone package.
internal static class RunHighlighting
{
    private static readonly GesSyntaxHighlighter Source = new();
    private static readonly GesSyntaxHighlighter Assembly = new(GesSyntaxLanguage.Gesa);

    internal static string Paint(string text, int color) => $"\u001b[{color}m{text}\u001b[0m";
    internal static string Render(string text) => GesAnsiRenderer.Render(text, Source.Highlight(text).Spans);
    internal static string RenderAssembly(string text) => GesAnsiRenderer.Render(text, Assembly.Highlight(text).Spans);
    internal static IReadOnlyList<ColorSpan> Highlight(string text) => Source.Highlight(text).Spans
        .Where(span => GesAnsiRenderer.DefaultColor(span.Kind) is not null)
        .Select(span => new ColorSpan(span.Start, span.Length, GesAnsiRenderer.DefaultColor(span.Kind)!.Value)).ToArray();

    internal readonly record struct ColorSpan(int Start, int Length, int Color);
}
