<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# C# Syntax module

> **Since: Unreleased**

`GameEventScript.Syntax` is the .NET Standard 2.1 module with the GES lexer,
parser, single-source validator and GES document reader that Runtime and
Compiler build on. For editors and terminals it highlights GES and GESA from the
lossless syntax tree and GES documents (`GesSyntaxLanguage.Gesd`) with their
first document error, with no third-party, Compiler, Runtime or UI dependency. The CLI and the Swift
product classify alike.

```csharp
using GameEventScript.Syntax;

var highlighter = new GesSyntaxHighlighter(); // GES by default
var result = highlighter.Highlight(source);
if (result.IsComplete)
{
    foreach (var span in result.Spans)
    {
        // UTF-16 offsets; map Kind to your editor's theme.
        ApplyStyle(span.Start, span.Length, span.Kind);
        // Diagnostics overlapping the span, such as a syntax error to underline.
        if (span.Diagnostics.Count > 0) Underline(span.Start, span.Length);
    }
}
```

Reuse the highlighter and highlight the whole document after each edit; input
may be a fragment with statements outside a handler, as console input is.
`result.Diagnostics` lists the syntax errors, or else the violated single-source
rules, with UTF-16 ranges. An incomplete result, for an input above the length
bound, has no spans: render plain text.

```csharp
var highlighter = new GesSyntaxHighlighter(GesSyntaxLanguage.Gesa);
var result = highlighter.Highlight(dump);
var displayed = result.IsComplete
    ? GesAnsiRenderer.Render(dump, result.Spans)
    : dump;
```

The ANSI renderer accepts an optional category-to-SGR-color callback. Terminal
capabilities, `NO_COLOR`, redirection and color enablement remain caller decisions.

Run `dotnet test implementation/csharp/GameEventScript.Syntax/tests`.
C# and Swift execute the same [Markdown cases](../../../conformance/highlighting/SyntaxHighlighting.md).
See the [contract](../../../specs/SyntaxHighlighting.md) for ranges, categories,
diagnostics and limits. The NuGet package is
available from the next release; a source checkout can reference the project now.
