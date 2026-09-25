<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Portable text and Unicode semantics

This document is the normative cross-language contract for Game Event Script
source text, names, text values, and source positions. Swift, Kotlin, Go, Rust,
C++, C#, and other implementations must expose the same behavior even when
their native string types use different indexing units.

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
- Tags, including script literals, message delivery tags, and Tag-typed message
  arguments, use `TagName` without `_`. They start with a lowercase ASCII letter
  and remain case-sensitive: `#ready` and `#isReady` are valid; `#Ready` is not.
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
| Dice containing rolls `6`, `3`, `1` | `:Dice[6, 3, 1]` |
| empty Dice | `:Dice[]` |
| Vector with components `10`, `20`, `0` | `:Vector(x: 10, y: 20, z: 0)` |
| Point with components `1m`, `2m`, `0m` | `:Point(x: 1m, y: 2m, z: 0m)` |

Dice output lists the stored results in descending dice order, retaining
duplicates, with comma and space separators. Each roll uses decimal integer
digits. It describes results rather than a random draw or a number of sides.

Vector and Point output uses the case-sensitive type name and parentheses,
with all three labeled components in `x`, `y`, `z` order, including zero
components. Labels are followed by colon and space; components are separated
by comma and space. Each component uses the roundtrippable numeric output
defined in [Number semantics](Numbers.md#numeric-text-output-and-roundtrip),
including its unit suffix when present. The examples above do not require a
particular permitted numeric spelling. Signed infinities are preserved.

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
name grammar, Dice result literals, Vector/Point data forms, and recursively
nested Lists/Maps, and the explicit data forms below. A Map recognizes bare `LowerName`
keys or quoted Text keys. Quoted keys follow the same single- or double-quoted
Text rules as values, including doubled delimiters. Map key syntax inside Text
therefore represents arbitrary keys even when they cannot be written as source
field names. A Map permits key-only entries as `true` and resolves duplicate
keys by decoded Text identity with the last value before sorting. Empty List and Map use `[]` and `[:]`. Trailing
commas are invalid. Arbitrary source expressions are not recognized. A non-Text operand produces `nothing`.

Vector and Point recognition uses case-sensitive `:Vector(...)` and
`:Point(...)` with the runtime whitespace rules below. These data forms have
zero through three numeric components, either positional in `x`, `y`, `z`
order or labeled with an ordered subset of `x:`, `y:`, `z:`. Labels cannot be
mixed with positional components, repeated, reordered, or replaced with other
names. Omitted components are zero; an empty form constructs the unitless zero
value. Supplied components must share one unit (or all be unitless); omitted
zero components inherit that unit. Even an explicitly supplied zero must have
the same unit as the other supplied components.

Components use [numeric text](Numbers.md#text-and-number-conversion), including
signs, exponents, underscores, supported quantity suffixes, and signed
infinities. Percentage components contribute their unitless ratios. Components
are stored as binary64 using the existing spatial value representation; zero
is canonicalized to positive zero. `NaN`, `nothing`, non-numeric values, mixed
units, trailing commas, and malformed forms fail recognition of the complete
input. Commas separate components, never digit groups. Variables, constants,
arithmetic, nested constructors, and calls are not evaluated. The source
compiler's general component constructors retain their existing behavior.

Within the parse resource bounds, `parse (value as :Text)` reconstructs the
same Vector/Point kind, exact binary64 components, and unit, including spatial
values nested inside Lists and Maps. Its nesting and explicit components
participate in the shared
[literal parsing limits](../HostRuntime.md#literal-parsing-limits).

Dice recognition uses the `:Dice[...]` data-literal form owned by
[Language](../Language.md#randomness-dice-and-series), with the runtime whitespace
rules below. The type spelling is case-sensitive. Entries use unsigned, unitless
[numeric text](Numbers.md#text-and-number-conversion), including exponent
notation, and must decode to positive Int32 integers; results are
sorted in descending order and duplicates are retained. Commas separate rolls,
never digit groups. Parsing does not draw random values. Invalid rolls or syntax
fail recognition of the complete input, including an enclosing List or Map.
The constructor form `:Dice([1, 2])` is also recognized; lowercase `dice[2, 1]` is not.

Within the parse resource bounds, `parse (value as :Text)` reconstructs the same
Dice value and kind for positive Int32 rolls. The guarantee also applies to Dice
values nested inside Lists and Maps.
Dice nesting and entries participate in the shared
[literal parsing limits](../HostRuntime.md#literal-parsing-limits).

ASCII space/tab and `LF`, `CRLF`, or `CR` may surround a literal and separate
container elements. They are preserved inside quoted Text. Other whitespace,
comments, and BOM markers are not trivia for this operation. Comma grouping is
accepted by explicit numeric casts only: `parse "1,003"` preserves that Text,
while `parse "[1,003]"` yields the List `[1, 3]`.

Independent scalar literals, Vector/Point data forms, stored Dice results, and
recursively nested Lists/Maps are data.
Literal recognition does not evaluate variables, arbitrary expressions, function calls,
selectors or random operations. Explicit known Record construction is the controlled
exception described below. Numeric decoding uses
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
| `:Dice[1, 3, 6, 3]` | Dice containing rolls `6`, `3`, `3`, `1` |
| `[rolls: :Dice[]]` | Map containing empty Dice |
| `:Vector(10, 20)` | Vector with components `10`, `20`, `0` |
| `:Point(x: 1m, z: 3m)` | Point with components `1m`, `0m`, `3m` |
| `:Point(1m, 0)` | unchanged Text because the supplied units differ |
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

### Explicit data forms

The case-sensitive constructor forms in
[Language](../Language.md#explicit-data-constructors) also accept literal arguments
inside Text. This includes Number with an optional unit, Percentage, Boolean,
Text, Tag, Nothing, List, Map, Dice, Range, built-in Series, Handler, Message and
Record data. Existing literals remain accepted, and guessing/fallback behavior is
unchanged. Explicit forms allow disambiguation such as `:Text("123")`.

`as :Text` writes Range as `:Range(from: ..., to: ..., step: ...)`, Series as
`:Series(fibonacci, offset: ...)` or `:Series(factorial, offset: ...)`, Handler as
`:Handler(Done(value, _))`, Message as `:Message(Done(value: ...) with #ready)`,
and Records as `:Record("TypeName", [field: value, ...])`. All nested Text values
are quoted using the ordinary doubled-delimiter grammar, including message
arguments and Record fields. Top-level Text still returns itself unchanged.
Handler identity contains no executable callback; Series text contains no cursor
or host state. These forms reconstruct the same data under the shared limits.

In a running Program, `parse ':Hit(100)'` may resolve that Program's own declared
Record constructor. Recognition first checks the complete input, including nested
syntax and argument labels. A malformed suffix or nested value returns the original
Text without executing any constructor. Unknown type constructors also preserve
Text. No native/external constructor registry is consulted.

After successful recognition, nested arguments are evaluated left to right,
children before parents; map entries evaluate in authored order even when later
entries replace the same key. Known constructors run as normal VM calls, with
ordinary opcode budgets, frame suspension, call-depth guards, errors and side
effects. `parse` can therefore not be discarded merely because its result is unused.
An execution failure is not recognition failure and does not fall back to Text.
`:Record("Hit", [...])` always reconstructs fields directly and bypasses the
constructor, even when that Program declares Hit.

External objects have no standalone source constructor form through `parse`.
The explicit [product JSON export](../MessageFormat.md) projects their declared
readable fields into Record data; it does not recreate native identity.

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
