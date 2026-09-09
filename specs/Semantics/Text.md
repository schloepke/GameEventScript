<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Portable text and Unicode semantics

This document is the normative cross-language contract for Game Event Script
source text, names, text values, and source positions. Swift, Kotlin, C++, C#,
and other implementations must expose the same behavior even when their native
string types use different indexing units.

## Unicode model and source input

- A GES source is a sequence of Unicode scalar values. Unpaired UTF-16
  surrogates and other representations that cannot describe such a sequence are
  rejected; they are never silently replaced.
- File-based source is strict UTF-8. Invalid UTF-8 is rejected.
- One optional initial UTF-8 BOM is accepted and removed before lexing. It is
  not part of the logical source and is not stored in `SourceArchiveSegment`.
- No Unicode normalization is performed. Canonically equivalent NFC/NFD text
  therefore remains observably different.
- `LF`, `CRLF`, and `CR` each represent one logical newline. `CRLF` is never two
  lines.
- Outside text literals and comments, only ASCII space (`U+0020`) and tab
  (`U+0009`) are horizontal whitespace. Other Unicode whitespace, including
  non-breaking space, is illegal source input.

Source names used by diagnostics and debug segments are metadata rather than
language identifiers. They may contain any valid Unicode scalar sequence but
must be non-empty.

## Portable name grammars

Language and host names do not depend on a platform Unicode database. The
following productions use ASCII only:

```text
AsciiLetter        = "A".."Z" | "a".."z"
LowerLetter        = "a".."z"
UpperLetter        = "A".."Z"
Digit              = "0".."9"
NonZeroDigit       = "1".."9"
NumericSuffix      = "_" ("0" | NonZeroDigit { "0".."9" })
LowerName          = LowerLetter { AsciiLetter | Digit }
VariableName       = LowerName [ NumericSuffix ]
MessageName        = UpperLetter { AsciiLetter | Digit }
TagName            = LowerName
ConstantName       = LowerName
TypeName           = UpperLetter { AsciiLetter | Digit }
ModuleSegment      = LowerLetter { LowerLetter | Digit }
ModuleName         = ModuleSegment { "." ModuleSegment }
UnlabeledArgument  = "_"
```

Consequences:

- Function/predicate names, argument labels, field names, map keys, extension
  components, and the local name of a labeled parameter use `LowerName`.
- Local variables, loop/selector variables, message-name bindings, and the local
  name of an unlabeled parameter use `VariableName`. `_` is reserved for an
  unlabeled argument and `_number` is permitted nowhere except a variable name.
- Regular message names use `MessageName`. The lowercase system endpoint names
  `initialization` and `undeliverable` are reserved grammar tokens rather than
  ordinary message names.
- Script tag literals and message delivery tags use `TagName` without `_`.
  Tag-typed values supplied as message arguments may start with either ASCII
  case, but remain case-sensitive.
- Declared constants are written as `$ConstantName`. Type names use `TypeName`.
  A linked extension binding is `LowerName.LowerName`.
- Module declarations use `ModuleName`; every dot-separated component is
  entirely lowercase apart from optional digits after its first letter.
- API normalization trims ASCII space and tab only. It does not reinterpret
  Unicode whitespace.

The lexer additionally recognizes a fixed set of explicitly assigned Unicode
operator aliases such as `×`, `÷`, `−`, `∞`, `π`, `τ`, `√`, `∧`, `∨`, `∈`,
`≤`, `≥`, `≠`, `→`, and `⇒`. These are individual grammar tokens and do not
make Unicode letters valid identifiers.

## Runtime text values

Text stores the exact Unicode scalar sequence supplied by the source, host, or
binary. No case folding or normalization occurs.

- Equality is exact scalar-sequence equality.
- Ordering is lexicographic by Unicode scalar value. For valid Unicode this is
  also the order obtained by lexicographically comparing canonical UTF-8 bytes.
- Text length and `:count` count Unicode scalar values.
- Positional text indexing is 1-based and selects one Unicode scalar value.
- Text iteration and text-to-list conversion yield one text value per Unicode
  scalar.
- Operations based on sequence boundaries or terminals, including first, last,
  and single, use the same scalar unit.
- Substring, starts-with, and ends-with operations compare exact scalar
  sequences.

For example, `A😀é` contains four scalar values: `A`, `😀`, `e`, and the
combining acute accent. The precomposed `é` is one scalar and is not equal to
`é`.

Implementations may store strings as UTF-8, UTF-16, UTF-32, or another internal
representation. That choice must not change the observable unit above. A host
may precompute scalar counts for linked string constants; this metadata is a
runtime cache and never part of `GameEventScriptProgram`.

## Casting Text to Text

An explicit `as :Text` cast of a Text value preserves its exact Unicode scalar
sequence. It adds no surrounding quote characters and performs no escaping.
Existing quotes, whitespace, and logical newlines remain part of the value
unchanged. In particular, casting the Text value `Hello` yields the same Text
value `Hello`, rather than a text containing a quoted source literal.

Numeric output and its roundtrip guarantees are owned by
[Number semantics](Numbers.md#numeric-text-output-and-roundtrip).

## Text output of data values

For the following data values, `as :Text` produces the representation below.
The output column describes the actual Text contents, not an additional layer
of source quoting.

| Value | Text contents |
| --- | --- |
| Text `Hello` | `Hello` |
| Text `123` | `123` |
| empty Text | empty Text |
| Number `123` | `123`, or an equivalent permitted numeric spelling |
| Percentage `10%` | percentage magnitude followed by `%`, such as `10%` |
| Quantity `10m` | numeric magnitude followed by the unit, such as `10m` |
| Tag `#ready` | `#ready` |
| Boolean true / false | `true` / `false` |
| `nothing` | `nothing` |
| List containing Number `1` and Text `Hello` | `[1, "Hello"]` |
| Map with field `name` containing Text `Ada` | `[name: "Ada"]` |
| empty List | `[]` |
| empty Map | `[:]` |

List and Map output recursively formats its values. Text values inside either
container are written as double-quoted source text literals, with each contained
double quote doubled. Backslash retains its ordinary text meaning. Container
formatting must distinguish Text from similarly spelled numbers, booleans, and
`nothing`, and must preserve commas or brackets contained in Text values.
For example, a List containing the two Text values `1` and `say "hi"` is written
as `["1", "say ""hi"""]`.

Lists separate items with comma and space. Non-empty Maps use the Map
form `[key: value, ...]`, with explicit values and ascending key order as defined
by [Determinism](Determinism.md#ordering-and-stable-sorting). Keys matching
`LowerName` are written bare; every other key is written as a double-quoted Text
literal using the same escaping as nested Text values. This preserves arbitrary
Text keys produced by host data or `:group by`, including empty keys and keys
containing punctuation, whitespace, quotes, or non-ASCII scalars. For example,
the single key `a: 1, b` with value `2` is written as `["a: 1, b": 2]`.
The identity rule
for casting an existing Text value still applies outside container formatting.
Formatting a nested Text value does not perform or change that identity cast.

## Literal recognition from Text

The `parse` expression is a built-in language operation with a dynamically
determined result kind. It recognizes one complete data literal in its Text
operand. Its source expression syntax is owned by
[Language](../Language.md#parse-expression).

Recognition follows an all-or-nothing rule:

- A fully recognized literal produces that literal's value and kind.
- If the input is not a complete valid literal, the result is the original Text
  unchanged, including whitespace, quote characters, and logical newlines.
- A successfully recognized `nothing` is a value, not a recognition failure.
- Trailing non-whitespace input, malformed quoting, and malformed containers
  fail recognition of the whole input. There is no partial result and no
  implicit conversion of an invalid nested literal to a Text element.

Quoted Text follows the complete source text-literal grammar: matching single
or double delimiters, with the chosen delimiter escaped by doubling it. Quote
characters merely appearing at both ends do not establish a valid literal.
Recognition decodes exactly one layer. The contents of a recognized Text literal
are returned as Text and are not recursively parsed a second time.

Recognized data literals are `nothing`, lowercase `true`/`false`, Number,
Percentage, scalar Quantity, quoted Text, `#` Tag names following the source
name grammar, and recursively nested Lists/Maps. A Map recognizes bare `LowerName`
keys or quoted Text keys. Quoted keys follow the same single- or double-quoted
Text rules as values, including doubled delimiters. Map key syntax inside Text
therefore represents arbitrary keys even when they cannot be written as source
field names. A Map permits key-only entries as `true` and resolves duplicate
keys by decoded Text identity with the last value before sorting. Empty List and Map use `[]` and `[:]`. Trailing
commas are invalid. Other value representations, constructors, and literal
forms are not recognized. A non-Text operand produces `nothing`.

ASCII space/tab and `LF`, `CRLF`, or `CR` may surround a literal and separate
container elements. They are preserved inside quoted Text. Other whitespace,
comments, and BOM markers are not trivia for this operation. Comma grouping is
accepted by explicit numeric casts only: `parse "1,003"` preserves that Text,
while `parse "[1,003]"` yields the List `[1, 3]`.

Independent scalar literals and recursively nested Lists/Maps are data.
Literal recognition does not evaluate variables, expressions, calls, selectors,
or random operations. Numeric decoding uses
[Number semantics](Numbers.md#text-and-number-conversion), including its exact
rounding and Percentage scaling rules. A `%` literal produces Percentage;
an explicit `as :Number` cast instead reads its unitless ratio. Numeric output
spellings, including exponent notation, must be recognized. Commas in literal
containers remain structural separators.

The following examples show the actual input Text contents:

| Input contents | Result |
| --- | --- |
| `Hello` | Text `Hello` |
| `123` | Number `123` |
| `true` | Boolean true |
| `"123"` | Text `123` |
| `"true"` | Text `true` |
| `""` | empty Text |
| `"say ""hi"""` | Text `say "hi"` |
| `nothing` | `nothing` |
| `[1, "1"]` | List containing Number `1` and Text `1` |
| `[1, broken]` | unchanged Text `[1, broken]` |
| `"Hello` | unchanged Text including the opening double quote |
| `1 extra` | unchanged Text `1 extra` |

There is no universal type-preserving roundtrip for arbitrary top-level Text.
Text `123` and Number `123` can have the same `as :Text` output, which `parse`
recognizes as Number. The exact numeric roundtrip guarantees remain in force.
Explicit `as :Number` and `as :Percentage` casts retain their own failure result
of `nothing`; the Text fallback belongs to `parse`. Resource-limit handling is
separate from literal-recognition failure and follows the fixed
[Host runtime parse limits](../HostRuntime.md#literal-parsing-limits).

## Compiler and debug positions

- Diagnostic lines and columns are 1-based.
- Columns count Unicode scalar values, not UTF-8 bytes, UTF-16 code units, or
  grapheme clusters.
- A tab advances the column by one. There is no implicit tab-stop expansion.
- The end position is exclusive.
- `LF`, `CRLF`, and `CR` advance to column 1 of the next line.
- `.gesb` `SourceMapSegment` offsets and lengths remain zero-based UTF-8 byte
  offsets into the uncompressed logical source.
- Source-map boundaries must lie on UTF-8 scalar boundaries. When a source
  archive is present, line-start offsets must exactly match its logical newline
  sequence.

The split is deliberate: user-facing compiler locations have readable scalar
columns, while serialized source maps have compact, language-neutral byte
offsets.
