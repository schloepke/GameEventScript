# Portable Text and Unicode Semantics

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
NonZeroDigit       = "1".."9"
NumericSuffix      = "_" ("0" | NonZeroDigit { "0".."9" })
Identifier         = LowerLetter { AsciiLetter } [ NumericSuffix ]
SourceMessageName  = "A".."Z" { AsciiLetter }
HostMessageName    = AsciiLetter { AsciiLetter }
TagName            = Identifier
TypeName           = LowerLetter { AsciiLetter }
UnlabeledArgument  = "_"
```

Consequences:

- Ordinary identifiers, parameter names, function and predicate names use
  `Identifier`. `_` is reserved for an unlabeled argument.
- Regular message names written in source use `SourceMessageName`. Host APIs
  accept `HostMessageName` because reserved endpoints such as `initialization`
  and `undeliverable` are lowercase.
- Script tag literals and message delivery tags use lowercase `TagName`.
  Tag-typed values supplied as message arguments may start with either ASCII
  case, but remain case-sensitive.
- Type names use `TypeName`. Extension and extension-function names use
  `Identifier`; a linked extension binding is `Identifier.Identifier`.
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
