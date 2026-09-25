// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.SyntaxHighlighter;

namespace GameEventScript.Tool;

// CLI presentation policy; all syntax recognition lives in the standalone package.
internal static class RunHighlighting
{
    private static readonly GameEventScriptSyntaxHighlighter Source = new();
    private static readonly GameEventScriptSyntaxHighlighter Assembly = new(GameEventScriptSyntaxLanguage.Gesa);

    internal static string Paint(string text, int color) => $"\u001b[{color}m{text}\u001b[0m";
    internal static string Render(string text) => GameEventScriptAnsiRenderer.Render(text, Source.Highlight(text).Spans);
    internal static string RenderAssembly(string text) => GameEventScriptAnsiRenderer.Render(text, Assembly.Highlight(text).Spans);
    internal static IReadOnlyList<ColorSpan> Highlight(string text) => Source.Highlight(text).Spans
        .Where(span => GameEventScriptAnsiRenderer.DefaultColor(span.Kind) is not null)
        .Select(span => new ColorSpan(span.Start, span.Length, GameEventScriptAnsiRenderer.DefaultColor(span.Kind)!.Value)).ToArray();

    internal readonly record struct ColorSpan(int Start, int Length, int Color);
}
