<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Syntax highlighting

This document owns the optional presentation API. Highlighting is tolerant lexical
classification, not source validation or a replacement for Compiler diagnostics.
It never executes source. Runtime, Compiler and native Bridges do not depend on it.

## Grammar and matching

The canonical GES and GESA TextMate JSON grammars under `tools/editors/TextMate`
are the sole rule source. The Classic plist grammars represent the same rules.
`scripts/sync-highlighter-grammars.py` embeds immutable tables into C# and Swift;
consumer builds need neither the generator nor grammar files on disk.

The bundled evaluator supports ordered `match` and `begin`/`end` rules, numbered
named captures, `name`, `contentName`, local includes, embedded GES and
`applyEndPatternLast`. The earliest match wins; rule order breaks ties. A current
region's end wins an equal-position tie unless `applyEndPatternLast` is enabled.
Strings use that exception so doubled delimiters remain escapes. Captures append
scopes from the outermost range inward. Zero-width ends may close a region without
consuming the next token, as used by GESA source blocks.

This is not a general-purpose TextMate/Oniguruma engine. C# uses BCL Regex; Swift
uses Foundation NSRegularExpression. Changes to the bundled grammar must work in
both engines and pass the shared highlighting corpus. Unsupported structural
features fail generation instead of silently disappearing. No separate keyword
list is maintained by the CLI or highlighter.

## Input, spans and categories

A reusable highlighter selects GES or GESA. `Highlight`/`highlight` processes a
whole document. `HighlightLine`/`highlightLine` processes exactly one physical
line with an optional final LF, CR or CRLF. Interior line endings are an API error.
An empty document or line is valid. Input is never normalized or expanded.
Only CR, LF and CRLF delimit physical lines. Regex line anchors and dot matching
must not treat U+0085, U+2028, U+2029, vertical tab or form feed as additional line
boundaries. These characters remain content, including inside comments and
strings in GESA source annotations.

A complete result supplies ordered, nonoverlapping, positive-length spans covering
all input, including whitespace and line endings. Empty input has no spans.
`Start`/`start` and `Length`/`length` count UTF-16 code units relative to the supplied
document or line; they are directly usable as .NET string offsets and `NSRange`.
Spans carry an immutable outer-to-inner TextMate scope stack, including the root
language scope, plus a theme-independent category. Editors may theme either the
category or the complete scope stack. No colors or UI objects occur in spans.

Categories are Plain, Keyword, Builtin, Identifier, Message, Type, Tag, Constant,
Number, String, Comment, Symbol, Function, Module, Label and Register. The innermost
recognized scope determines the category. String escapes belong to String;
Boolean literals and word operators belong to Keyword. Unknown/unclassified text
belongs to Plain. Declaration keywords retain their keyword category even when
the declared name is on the next line or has not been entered yet. Contextual
declaration rules take precedence when their complete pattern matches.
Classification does not prove that text is valid GES.

## Incremental editing

Every complete result supplies an immutable, equatable continuation state after
its final processed line. An initial state starts outside any region. Open strings
and embedded GES regions survive line boundaries; line terminators retain the
scope active after matching the line's content. A GESA `.source-line` annotation
is limited to that line, including unfinished strings; source blocks retain normal
multiline GES strings.

States are opaque, belong to one language and may be used by another highlighter
instance of the same library version and language. They are not a persistence
format. Passing a GES state to a GESA highlighter, or vice versa, is an API error.

An editor caches each line's text, spans and exit state. After an edit, start with
the preceding line's state and reprocess subsequent lines until both the unchanged
text and exit state converge. Shift line-relative spans by the document line
start when applying styles. Including original line terminators permits exact
comparison with whole-document highlighting. The state is for the next line,
not for appending another chunk to the same line.

## Resource bounds and failure

Each call accepts at most a configurable positive UTF-16 length, default 262144.
The evaluator limits nesting to 128 regions and uses a cooperative 1000 ms matching
budget. C# additionally uses a 100 ms timeout per regex; Swift observes the budget
through Foundation progress callbacks. These are responsiveness safeguards, not
hard real-time guarantees or cross-platform performance requirements.

Exceeding a bound returns `IsComplete == false` / `isComplete == false`, no spans
and no continuation state. The caller displays unchanged plain text and must not
cache that result as a successful lexical state. Large documents should use the
line API. An unfinished source construct alone is not failure.

## ANSI presentation

The optional ANSI renderer accepts the unchanged source and its spans, preserving
every character. It supports a default category palette and an optional callback
returning a basic/bright foreground SGR code (30–37, 39, 90–97) or no color.
It validates range order, bounds and UTF-16 surrogate boundaries, coalesces adjacent
same-color spans and resets color after painted text. Invalid ranges or colors
are API errors. Terminal detection, `NO_COLOR`, redirection, tab expansion and
whether coloring is enabled belong to the embedding, not this library.

## Mapping and verification

- C#: `GameEventScript.SyntaxHighlighter` package and namespace, .NET Standard 2.1,
  no third-party or other GES library dependency.
- Swift: `GameEventScriptSyntaxHighlighter` SwiftPM product; Foundation supplies
  regex and embedded grammar decoding. No AppKit, UIKit, Runtime or Compiler
  dependency. Foundation is also available on supported non-Apple Swift platforms;
  platform availability is verified separately from the API contract.

Both implementations expose `GameEventScriptSyntaxHighlighter`,
`GameEventScriptSyntaxLanguage`, `GameEventScriptSyntaxKind`,
`GameEventScriptHighlightSpan`, `GameEventScriptHighlightResult`,
`GameEventScriptHighlightState` and `GameEventScriptAnsiRenderer`.
Swift reports invalid API input through `GameEventScriptHighlightingError`.

The shared executable examples live in
[`conformance/highlighting/SyntaxHighlighting.md`](../conformance/highlighting/SyntaxHighlighting.md).
Native package adapters run those Markdown cases without Runtime or Compiler and
verify categories, scope stacks, Unicode offsets, whole-document/line equivalence
and ANSI text preservation. They are presentation conformance, separate from the
VM capability corpus; optional highlighting does not add a Runtime dependency.
