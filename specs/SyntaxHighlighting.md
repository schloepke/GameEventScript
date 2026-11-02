<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Syntax highlighting

> **Since: 0.2.0**

This document owns the optional presentation API of the Syntax module.
Highlighting classifies source text for display and reports the diagnostics a
single source can know. It never executes source and does not replace the
Compiler's diagnostics. Runtime, Compiler and native Bridges do not depend on it.

> **Since: Unreleased** — tree-based highlighting, span diagnostics

## Input and result

A reusable highlighter selects GES, GESA or GES documents (`.gesd`).
`Highlight`/`highlight` processes a whole document; there is no line API and no continuation state. An empty
document is valid. Input is never normalized or expanded. CR, LF and CRLF are
line breaks; U+0085, U+2028, U+2029, vertical tab and form feed are content.

A complete result supplies ordered, nonoverlapping, positive-length spans
covering all input, including whitespace and line breaks. Empty input has no
spans. `Start`/`start` and `Length`/`length` count UTF-16 code units of the
supplied document; they are directly usable as .NET string offsets and
`NSRange`. Adjacent ranges with the same category and the same diagnostics form
one span. No colors or UI objects occur in spans.

The result also lists the document's diagnostics in source order, each with its
stable code, a message whose wording is not stable, and a UTF-16 range; a range
of length zero marks a position. Each span lists the diagnostics whose range
overlaps it. A diagnostic of length zero overlaps the span at its position, or
the last span when it lies at the end of the document.

## GES

GES is categorized from the lossless syntax tree: each token's kind, the role
the parser recorded for it, and for a few constructs the node that owns it.
Leading trivia contributes Plain whitespace, a leading BOM as Plain and `//`
comments as Comment.

Input is parsed as a fragment: besides declarations, it may hold statements
outside a handler, as editor and console input does, and those are categorized
as statements. Parsing recovers after each failed declaration or statement, so
the rest of the document keeps its categories; the tokens of a failed one keep
their token-kind category.

Diagnostics are those the Compiler reports for the document as one source: its
syntax errors or, when there are none, the violated rules that need only this
source, such as name spelling, repeated parameters, fields or bindings, and
shadowing (`specs/Diagnostics.md`). Rules that need other sources, catalogs or
a host are not reported.

## GESA

GESA is categorized line by line. A dumper string may contain line breaks and
then continues its line. A `.segment source` line opens embedded GES that ends
before the first line, outside a GES token, that starts with `.region-end` or
`.segment`; a `.source-line` annotation holds one GES line after its `|`. Both
are categorized as GES without diagnostics. `.region` and `.region-end` lines
are Comment. GESA has no diagnostics.

## GES documents

> **Since: Unreleased** — GES document highlighting

A GES document ([Documents](Documents.md)) is categorized token by token, so
text after an error keeps its categories. Whitespace, line breaks and a leading
BOM are Plain and `//` comments are Comment. The `:Schema` of a header is
Keyword; every other `:Name` of a typed form is Type. A word or quoted Text
followed by `:` on its line is a key or label: a word is Identifier, quoted
Text stays String. The first word inside `:Message(` or `:Handler(` that starts
with an uppercase letter, or is `initialization` or `undeliverable`, is Message;
the parameter names of a handler are Identifier, and `with` after a message's
arguments is Keyword. `fibonacci` and `factorial` as the first argument of
`:Series(` are Builtin. `true`, `false` and `nothing` are Keyword; `#name` is
Tag; quoted Text, also when unterminated, is String. An unquoted token that
starts with a digit, or with a sign or `.` before a digit, and `Infinity` and
`NaN` are Number, also with an unknown unit. Brackets, parentheses, commas and
colons are Symbol. Other words, such as unquoted Text, are Plain.

The diagnostics hold the document's first reading error, with the code and
position [Documents](Documents.md#errors) defines. Its range covers the token
of `document.unquotedText`, `document.unknownUnit` and a token's
`document.invalidLiteral`, and the `:Name` of an error at a form; otherwise it
covers the scalar at the position, or has length zero at a line break or the
end of the document. Errors of the construction that follows a complete read,
such as `:Range(1)` constructing nothing or the Range materialization total,
are reported only by reading the document (`GesDocument`).

## Categories

Categories are Plain, Keyword, Builtin, Identifier, Message, Type, Tag,
Constant, Number, String, Comment, Symbol, Function, Module, Label and Register.

| Category | GES | GESA | GES documents |
| --- | --- | --- | --- |
| Plain | whitespace, line breaks, a BOM, unrecognized text | whitespace, other words | whitespace, line breaks, a BOM, unquoted words |
| Keyword | keywords, contextual keywords such as `between`, `from`, `first`, operators, `true`, `false`, `nothing` | directives, mnemonics, `unit`, `=` | `:Schema`, `true`, `false`, `nothing`, `with` |
| Builtin | intrinsics, selectors, `:namespace.name` extension names, units of `:Quantity(...)`, inline-assembly opcodes | — | `fibonacci` and `factorial` of `:Series` |
| Identifier | variables, bindings, parameters, fields, members, argument labels, map keys | attribute names before `=` | keys, labels, handler parameters |
| Message | message and handler names | — | message names of `:Message` and `:Handler` |
| Type | type names such as `:Number` and record types | capitalized words that name no label | `:Name` of typed forms and the schema type |
| Tag | tags | segment names, units after `unit:` | tags |
| Constant | `$constant` references | — | — |
| Number | numeric literals with their whole unit spelling, such as `2m²`, `2m^2` and `4/s`, compact dice such as `2d6`, math constants | numbers, `#` immediates | numbers with units, `Infinity`, `NaN` |
| String | strings, including an unterminated string to the end of the document | dumper strings | quoted Text and keys, also unterminated |
| Comment | `//` comments | comments, `@` addresses, region lines | `//` comments |
| Symbol | punctuation, `_` | punctuation | brackets, parentheses, commas, colons |
| Function | function and predicate names where declared, called or tested | — | — |
| Module | the module name of a module declaration | — | — |
| Label | inline-assembly labels | label definitions and references, symbolic register names | — |
| Register | — | registers such as `r1` | — |

Classification does not prove that text is valid GES.

## Resource bounds and failure

Each call accepts at most a configurable positive UTF-16 length, default 262144.
A longer input returns `IsComplete == false` / `isComplete == false`, no spans
and no diagnostics; the caller displays unchanged plain text. C# rejects text
with an unpaired surrogate with `ArgumentException`; Swift strings cannot hold
one. Malformed source is never an API error.

## ANSI presentation

The optional ANSI renderer accepts the unchanged source and its spans, preserving
every character. It supports a default category palette and an optional callback
returning a basic/bright foreground SGR code (30–37, 39, 90–97) or no color.
It validates range order, bounds and UTF-16 surrogate boundaries, coalesces adjacent
same-color spans and resets color after painted text. Invalid ranges or colors
are API errors. Terminal detection, `NO_COLOR`, redirection, tab expansion and
whether coloring is enabled belong to the embedding, not this library.

## Mapping and verification

- C#: `GameEventScript.Syntax` package and namespace, .NET Standard 2.1,
  no third-party or other GES library dependency.
- Swift: `GameEventScriptSyntax` SwiftPM product without Foundation, Runtime or
  Compiler dependency.

Both implementations expose `GesSyntaxHighlighter`, `GesSyntaxLanguage`,
`GesSyntaxKind`, `GesHighlightSpan`, `GesHighlightDiagnostic`,
`GesHighlightResult` and `GesAnsiRenderer`. Swift reports invalid API input
through `GesHighlightingError`.

The shared executable examples live in
[`conformance/highlighting/SyntaxHighlighting.md`](../conformance/highlighting/SyntaxHighlighting.md).
Native package tests run those Markdown cases without Runtime or Compiler and
verify categories, diagnostics, span coverage and ANSI text preservation.
`scripts/test-syntax-roundtrip.py --trees` additionally requires both ports to
produce identical spans and diagnostics for every corpus source, every
document text of the document suite and every shared case. These are presentation conformance, separate from the VM
capability corpus.

The TextMate grammars under `tools/editors` serve editor bundles only; the
highlighter does not read them.
