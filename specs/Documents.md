<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# GES documents

> **Since: Unreleased**

This document owns GES documents: human-editable data files whose values are
written exactly like GES data literals. The standard file extension is `.gesd`.
A document is read into an immutable `GesDocument` whose root is always a Map or
a Record, and a `GesDocument` can be printed back to document text.

Documents reuse [literal recognition](Semantics/Text.md#literal-recognition-from-text)
of `parse` for every value. They add a bracket-free root, line breaks as entry
separators, comments, a schema header and strict error reporting. The `parse`
expression and the public literal parsing API are unchanged. GES never reads or
writes files: hosts pass document text in and receive document text back.

```gesd
:Schema(:DriveUnit)
// Engine and gearbox of a light tracked drone.
id: 'drive.tracked.basic'
kind: #drive
tier: 1
name: 'Basic Track Drive'
cost: [buildCost: 80, weight: 550kg, slotSize: 1]
heatModel: [
  heatCapacity: 100
  passiveCoolingPerSecond: 2   // per second
  shutdownHeat: 90
  criticalHeat: 100
]
maxSpeed: 10m/s
maxForce: 21.4kN
efficiency: 90%
mount: :Vector(x: 0m, y: -0.8m)
```

## Text and trivia

Document text is a sequence of Unicode scalars. One leading U+FEFF byte order
mark is ignored. Lines end at `LF`, `CRLF` or `CR`; each counts as one line
break. Whitespace is ASCII space and tab plus line breaks; other Unicode
whitespace is not trivia.

A comment starts with `//` outside quoted Text and extends to the end of its
line, excluding the line break. A comment may appear wherever whitespace is
allowed: on its own line, after the schema header, after a key, after a value
or inside a container. `//` also ends an unquoted value token, so `90%// note`
is the Percentage `90%` followed by a comment. Comments and layout are trivia;
they are not part of the document value.

## Root and entries

The root is written without brackets as a sequence of entries. Text containing
only trivia, with or without a schema header, is an empty root.

```text
document   = [ header ] { entry }
entry      = key ":" [ value ]
key        = LowerName | quoted Text
```

- Keys follow the Map literal rules of `parse`: a bare
  [`LowerName`](Semantics/Text.md#portable-name-grammars) or a quoted Text
  key with single or double delimiters, doubled to escape. Quoted keys may
  contain any Text, including empty Text.
- The value starts on the same line as its key. An entry whose `:` is followed
  only by trivia up to the end of the line, a comma, a closing bracket or the end
  of the text is a key-only entry with the value `true`, as in Map literals.
- Every value is one complete data literal as recognized by `parse`:
  `nothing`, Booleans, Numbers, Percentages, Quantities including prefixed unit
  spellings, quoted Text, Tags, Lists, Maps, Dice, Vector, Point and the
  [explicit data forms](Semantics/Text.md#explicit-data-forms). Explicit forms
  construct their values with the same rules as `parse`, except that a failed
  construction is an error rather than `nothing` (see [Strictness](#strictness)).
- Program Record constructors such as `:Hit(5)` are not resolved: documents
  have no Program, so the form is an unknown type. `:Record("Hit", [...])`
  writes Record data directly.
- Entries in the root and inside every container are separated by a comma or a
  line break. Several entries on one line need commas. One comma may follow the
  last entry of the root or of a container, also at the end of a line; an empty
  entry between two commas is invalid.
- A line break directly after an opening `[` or `(`, before a closing `]` or
  `)`, and between entries is trivia. Containers may therefore span lines. A
  value never continues on the next line unless it is inside a container or a
  quoted Text.

These separator rules apply in the root and in Lists, Maps, Dice, Vector/Point
components and explicit constructor arguments, including Message and Handler
forms. Inside a Message form, `with` and its tag list follow the same rules.
Argument labels and Vector/Point component labels stay on the line of their
`:`; a labeled argument's value may follow on a later line because argument
lists have no key-only entries. The type name of a typed form follows its `:`
directly, as in `:Vector(...)` or `:Record(...)`.

## Strictness

A document is valid or invalid as a whole; reading never produces a partial
value and never falls back to Text.

- Text values must be quoted. An unquoted word such as `name: Basic Track Drive`
  is an error.
- Duplicate keys are an error at every level, including the root, Maps, Record
  fields and argument labels of explicit forms. Keys compare by decoded Text
  identity, so `a` and `'a'` are duplicates.
- Number tokens with an unknown unit suffix, invalid Tag names, malformed
  containers, unclosed brackets or quoted Text and trailing input are errors.
- An explicit form other than `:Nothing(...)` whose arguments are recognized but
  construct `nothing`, such as `:Range(1)`, `:Number("abc")` or
  `:Message(Done(v: 1) with 5)`, is an error instead of the value `nothing`.

The result is plain immutable data. Maps sort their keys as usual. Reading has no
access to host bindings, extensions, registries or scripts and evaluates no
expressions.

## Schema header

An optional header names the document's schema. It is the first non-trivia item
of the document; comments and blank lines may precede it and a comment may
follow it on the same line. The first entry starts on a later line.

```text
header     = ":Schema" "(" schema { "," label ":" value } ")"
schema     = ":" TypeName | quoted Text
```

- `:Schema(:DriveUnit)` references a record type, written in the source type
  syntax. The type name follows [`TypeName`](Semantics/Text.md#portable-name-grammars)
  and must not be a built-in type name such as `List`, `Map`, `Record` or
  `Number`. The root becomes a Record of that type whose fields are the
  entries; every key must then be a `LowerName` field name.
- `:Schema('DriveUnit.gess')` references a schema file by a non-empty quoted
  Text path. The host interprets the path, typically relative to the document.
  The root is a Map carrying that reference.
- The header follows the separator rules of constructor arguments. No labeled
  arguments are defined yet; any labeled argument is an error, so later versions
  can add labels such as a version without changing the meaning of existing
  documents.
- At most one header is allowed. A header after the first entry or a second
  header is an error.

The schema is a reference only. Reading does not load `.gess` files, look up
record definitions or validate fields. Hosts inspect `GesDocument.Schema` to
decide how to interpret the document.

### Planned schemas

> **Planned** — informative, not implemented.

A schema will be a GES record definition, and a `.gess` file GES source that
holds record declarations. `:Schema(:DriveUnit)` will refer to such a record
from a `.gess` file or a Program, `:Schema('DriveUnit.gess')` to a file and the
record it declares. A separate reading operation that receives the available
schemas will construct and check the root record; plain reading stays as
specified here. Validation is expected to report missing, unknown and
mistyped fields with document positions and to reject computed fields in
documents. The header syntax above already admits these additions.

## Errors

An invalid document fails with a document error (C# `GesDocumentException`,
Swift `GesDocumentError`). It carries a stable `code`, a diagnostic message
that is not part of the contract, and the 1-based `line` and `column` of the
offending position, counted in Unicode scalars from the start of the text,
excluding a leading byte order mark. A line break ends its line; the column of the
first scalar after it is 1. Reading reports exactly one error. Errors detected
while reading the text, including the input, nesting and item limits, come first,
in reading order. Only a completely read text is then constructed: failed
explicit forms and the Range materialization total are reported in construction
order, which evaluates arguments before their form and siblings left to right.

| Code | Reported when | Position |
| --- | --- | --- |
| `document.invalidKey` | an entry does not start with a key, or a Record root key is not a field name | start of the entry |
| `document.missingColon` | a key is not followed by `:` on its line | first position after the key and any spaces and comments on its line |
| `document.duplicateKey` | a key or label repeats in the same container | start of the repeated key |
| `document.unquotedText` | a value token that starts with an ASCII letter or `_` is not a literal | start of the token |
| `document.unknownUnit` | a number is followed by a suffix that is not a unit spelling | start of the token |
| `document.invalidLiteral` | any other value or explicit form is not a valid literal, or an explicit form constructs `nothing` | start of the value; the `:` of a form |
| `document.unknownType` | a typed form names an unknown or non-constructible type | the `:` of the form |
| `document.unexpectedToken` | input appears where an entry, separator or closing bracket is expected | that input |
| `document.unclosedBracket` | the text ends inside a container or argument list | the opening `[` or `(` |
| `document.unclosedText` | the text ends inside quoted Text | the opening delimiter |
| `document.invalidSchemaHeader` | the header has no, a non-schema or a labeled argument, or a built-in type name | start of the offending argument |
| `document.misplacedSchemaHeader` | a header follows the first entry or another header | the `:` of that header |
| `document.limitExceeded` | a literal parsing limit is exceeded | see below |

The token of `document.unquotedText`, `document.unknownUnit` and
`document.invalidLiteral` extends to the next whitespace, comma, bracket,
closing parenthesis or comment. A number token is a number with an unknown unit
when it starts with an optional sign and a decimal digit, or a sign or `.`
followed by a decimal digit, and its longest numeric prefix
`[+-]? (Digits ["." [Digits]] | "." Digits) [("e" | "E") [+-]? Digits]`, where
`Digits` is a decimal digit followed by digits or `_`, is followed by an ASCII
letter, `°` or `/`. An explicit form whose arguments are themselves valid but do
not form a valid constructor call, such as a Vector with mixed units, reports
`document.invalidLiteral` at the `:` of the form; an invalid nested argument
reports its own error first.

### Limits

Documents use the fixed [literal parsing limits](HostRuntime.md#literal-parsing-limits)
of the public literal parsing API: at most 1,048,576 input scalars, 64 nested
containers, 65,536 entries and items in total and a total of 10,000 Range items
materialized into Lists. Root entries count as items; the root itself does not
consume a nesting level. Exceeding a limit fails with `document.limitExceeded`,
which additionally exposes the stable limit name and its value. The position is
line 1, column 1 for the input length, the container or item that exceeds the
bound for nesting and items, and the `:` of the List form whose materialization
exceeds the Range total. No additional limits apply.

## The document value

A `GesDocument` holds its root value and an optional schema reference.

- The root is a Map or a Record. A document with a record type schema has a
  Record root of that type; a document with a file schema or without a schema
  has a Map root.
- The schema reference has a kind, record type or file, and a name: the type
  name or the path.
- Hosts build a document from a Map or Record value. A Record's type name
  becomes the record type schema and must be a valid, non-built-in `TypeName`;
  its field keys must be `LowerName` field names, as when reading.
  A Map has no schema unless a file schema is attached; a record type schema
  cannot be attached to a Map, and a Record cannot carry a different schema.
  Every other root kind is rejected, and so is any value inside the root whose
  literal text does not read back: External values, Dice rolls below 1 and
  nested Records whose type name does not follow `TypeName`.
- Two documents are equal when their root values and schema references are
  equal. Comments and layout are never part of a document.

## Printing

Printing writes document text for a document. The output is normative only
through the round trip below; its layout is a recommendation.

### Round trip

Reading the printed text of a document with any conforming implementation
produces an equal document: the same schema reference and a bit-identical root
value. Every scalar keeps its kind, Int64 or Binary64 storage, exact bits after
zero and integral-storage canonicalization, unit and Percentage kind, and every
nested value keeps its kind and structure. The guarantee holds when one
implementation prints and another reads, for every document whose printed text
stays within the [limits](#limits). Documents contain no value that cannot be
printed.

Printed text must satisfy these rules so that every implementation can read it:

- A document with a schema reference starts with its header, `:Schema(:TypeName)`
  or `:Schema("path")`; a document without one prints no header.
- Each root entry starts on its own line. Lines end with `LF`; non-empty output
  ends with exactly one line break. An empty document without a header prints
  empty text.
- Keys that are `LowerName` are written bare; other keys are written as quoted
  Text. Text values are quoted with doubled delimiters. Text containing line
  breaks is written with its line breaks inside the quotes.
- A Quantity may be written with a prefixed unit spelling only if reading that
  spelling yields exactly the same value; otherwise it uses the base symbol.

### Recommended layout

> Informative. Implementations may differ in these details, including their
> numeric spelling and the switch to exponent notation.

- Print the header, then one root entry per line as `key: value`, in Map order.
- Print a List, Map or the field Map of a `:Record(...)` form inline, such as
  `[a: 1, b: 2]`, when the complete line, including indentation and key, fits the
  line width and the container contains no Text with a line break. Otherwise use
  block form: the opening bracket ends the line, each entry follows on its own
  line one indentation level deeper without commas, and the closing bracket
  stands on its own line at the parent's indentation. Empty containers and all
  other values are always printed inline.
- Write values with the canonical text of `as :Text`, Text quoted with double
  delimiters, except for scalar Quantities, including those inside Lists, Maps,
  Records and Message arguments. With prefixed units enabled, choose among the
  base symbol and the decimal prefixes accepted for that unit, `km`/`mm`, `ms`,
  `kN`, `kJ`/`MJ`, `kW`/`MW`, `kV` and `kHz`, the spelling whose magnitude lies
  in [1, 1000), or otherwise the one closest to that interval on a logarithmic
  scale. `Wh` and `kWh` are never chosen. Zero, infinities, units without
  prefixes such as `m/s`, `kg` or `°/s`, and Vector, Point, Range and Series
  components use the base symbol.
- Printing options: indentation (default 2 spaces, at most 100), maximum line
  width (default 100 Unicode scalars) and prefixed units (default on). Disabling
  prefixed units prints every Quantity with its base symbol.

The same formatter is available for a single value. Its block form separates
entries with commas as well as line breaks, so the result reads back with `parse`
within the literal parsing limits; it is not a document unless the value is
printed as a document.
