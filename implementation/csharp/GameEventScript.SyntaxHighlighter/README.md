<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# C# syntax highlighting

> **Since: Unreleased**

`GameEventScript.SyntaxHighlighter` is an optional .NET Standard 2.1 library for
GES/GESA editors and terminals. It uses BCL Regex, with no third-party, Compiler,
Runtime or UI dependency. It shares the canonical TextMate rules with the CLI
and the Swift product.

```csharp
using GameEventScript.SyntaxHighlighter;

var highlighter = new GameEventScriptSyntaxHighlighter(); // GES by default
var result = highlighter.Highlight(source);
if (result.IsComplete)
{
    foreach (var span in result.Spans)
    {
        // UTF-16 offsets; map Kind or the TextMate Scopes to your editor's theme.
        ApplyStyle(span.Start, span.Length, span.Kind);
    }
}
```

Reuse the highlighter. For incremental editing use
`HighlightLine(line, previousState)` with the original line terminator. Cache each
line's text, spans and `NextState`; after edits, reprocess until unchanged text and
state converge. Add the document line offset when applying spans. Incomplete
results have no spans or state: render plain text and do not cache that state.

```csharp
var highlighter = new GameEventScriptSyntaxHighlighter(GameEventScriptSyntaxLanguage.Gesa);
var result = highlighter.Highlight(dump);
var displayed = result.IsComplete
    ? GameEventScriptAnsiRenderer.Render(dump, result.Spans)
    : dump;
```

The ANSI renderer accepts an optional category-to-SGR-color callback. Terminal
capabilities, `NO_COLOR`, redirection and color enablement remain caller decisions.

Run `dotnet test implementation/csharp/GameEventScript.SyntaxHighlighter/tests`.
C# and Swift execute the same [Markdown cases](../../../conformance/highlighting/SyntaxHighlighting.md).
See the [contract](../../../specs/SyntaxHighlighting.md) for ranges, categories,
limits, incremental states and supported TextMate features. The NuGet package is
available from the next release; a source checkout can reference the project now.
