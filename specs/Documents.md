<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# GES documents

> **Since: 0.5.0**

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
key        = LowerName | quoted Text | handle
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
  spellings, quoted Text, Tags, Lists, Maps, Dice, Bytes, Vector, Point and the
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

### Handles

> **Since: Unreleased**

Documents also read [handles](Language.md#handles), which `parse` does not: a value or a Map key that starts with
`@` is a handle in its [text form](Language.md#handle-text-form).

The document below follows this schema; `unit` is an `@Unit~` field, so its handle may carry a version or a ref:

```gess
schema :Sighting as {
    target: @Target
    seen: :List(@Target) max 16
    unit: @Unit~ optional
    memory: :Map(@Target → :Contact) max 64
}
```

```gesd
:Schema(:Sighting)
target: @Target:3
seen: [@Target:3, @Target:17]
unit: @Unit:drone.gnabblo~#live
memory: [
  @Target:3: [label: 'Alpha', strength: 42]
]
```

```text
handle     = "@" TypeName ":" id [ "~" ( version | ref ) ] [ "/" part ]
id, part   = integer | bare | quoted Text
ref        = "#" TagName | quoted Text
```

- Reading uses no store. An id or part that is an optional `-` followed by decimal digits without a leading zero
  reads as an integer, otherwise as Text; a Text that looks like an integer is therefore quoted, `@Unit:'7'`. A bare
  id consists of ASCII letters, digits, `.`, `_` and `-`; a bare part also contains `/` and runs to the next
  whitespace, comma, closing bracket or comment. A bare part never starts with `/` and never contains `//`, which
  begins a comment; such a part is quoted, `@Unit:drone.gnabblo/'a//b'`.
- A version of exactly 40 or 64 hexadecimal digits is a hash and reads as its bytes; any other version is a decimal
  revision within Int64. A ref names a version through the owner: `~#live` takes the characters of a tag, any other
  ref is quoted Text, `~'1.0'` or `~'feature/x'`, so `/` always separates the part. The document keeps a ref as
  written, and the host resolves it with `ResolveRef` before the value reaches a script.
- A handle key carries neither a version nor a ref, because Map keys compare by identity only. A Map may mix Text and
  handle keys; a Record root takes only field names.
- Everything else about the handle, whether its type exists, its id fits and a version is required or allowed, is
  checked against a schema; plain reading accepts any well-formed handle. Against a schema, only an `@Name~` field
  takes a version or a ref.

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
- A number token that is recognized but constructs `nothing`, such as `NaN`,
  `Infinity%` or a percentage that overflows binary64 like `1e400%`, is an error
  instead of the value `nothing`; `Infinity` and `-Infinity` remain numbers.
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
record definitions or validate fields; that is the job of a schema check. Hosts inspect `GesDocument.Schema` to
decide how to interpret the document.

### Checking against a schema

> **Since: 0.5.0**

A schema is a declaration in a `.gess` file, written in the one schema language of
[GES schemas](Schemas.md), which also describes the compact binary encoding.
`:Schema(:DriveUnit)` refers to a `schema` of a schema set, `:Schema('DriveUnit.gess')`
to a file and its root schema. Plain reading stays as specified here; the separate
operation [`CheckDocument`](Schemas.md#document-checking) of a schema set reports missing,
unknown and mistyped fields and values out of range with their document positions, without
implicit unit casts. A handle whose type, id or version does not fit its field is `document.fieldType`; a version or
a ref on a handle of an `@Name` field, which takes neither, is `document.unexpectedVersion`. A document never sets a schema's [computed fields](Schemas.md#computed-fields); one that does fails
with `document.unknownField`.

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
| `document.invalidLiteral` | any other value or explicit form is not a valid literal, or a number token or explicit form constructs `nothing` | start of the value; the `:` of a form |
| `document.unknownType` | a typed form names an unknown or non-constructible type | the `:` of the form |
| `document.unexpectedToken` | input appears where an entry, separator or closing bracket is expected | that input |
| `document.unclosedBracket` | the text ends inside a container or argument list | the opening `[` or `(` |
| `document.unclosedText` | the text ends inside quoted Text | the opening delimiter |
| `document.invalidSchemaHeader` | the header has no, a non-schema or a labeled argument, or a built-in type name | start of the offending argument |
| `document.misplacedSchemaHeader` | a header follows the first entry or another header | the `:` of that header |
| `document.invalidHandle` | a value or key starting with `@` is not a well-formed handle, or a handle key has a version or ref | the `@` |
| `document.unexpectedModule` | a `module` block appears outside a [store](#stores) file *Since: Unreleased.* | the word `module` |
| `document.limitExceeded` | a literal parsing limit is exceeded | see below |

The token of `document.unquotedText`, `document.unknownUnit` and
`document.invalidLiteral` extends to the next whitespace, comma, bracket,
closing parenthesis or comment. A number token is a number with an unknown unit
when it starts with an optional sign and a decimal digit, or a sign or `.`
followed by a decimal digit, and its longest numeric prefix
`[+-]? (Digits ["." [Digits]] | "." Digits) [("e" | "E") [+-]? Digits]`, where
`Digits` is a decimal digit followed by digits or `_`, is followed by an ASCII
letter, `°` or `/`. A token that starts with an optional sign and `0x` is never
a number with a unit; an invalid one such as `0x10m` or `0x` is
`document.invalidLiteral`. An explicit form whose arguments are themselves valid but do
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
- A handle is written in its text form; a ref is written as read. *Since: Unreleased.*
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
  `t`, `kN`, `kJ`/`MJ`, `mW`/`kW`/`MW`, `kV`, `kHz`, `cm²`/`km²`,
  `mW/m²`/`kW/m²`, `mW/K`/`kW/K`/`MW/K`, `kJ/K`/`MJ/K`, `kPa`/`MPa`,
  `kJ/kg`/`MJ/kg`, `kJ/(kg·K)`, `kN·m` and `mΩ`/`kΩ`/`MΩ`, the spelling whose magnitude lies in [1, 1000), or otherwise
  the one closest to that interval on a logarithmic scale. `cm` and `hPa` are read but
  never chosen, because `mm` and the base symbol or `kPa` cover their magnitudes. Prefixes keep the
  unit symbol rather than its ASCII spelling. `Wh`, `kWh`, `Ah`, `mAh` and `/s` are never
  chosen. Zero, infinities, units without prefixes such as `m/s`, `m³`, `dB` or
  `°/s`, and Vector, Point, Range and Series components use the base symbol.
- Printing options: indentation (default 2 spaces, at most 100), maximum line
  width (default 100 Unicode scalars) and prefixed units (default on). Disabling
  prefixed units prints every Quantity with its base symbol.

The same formatter is available for a single value. Its block form separates
entries with commas as well as line breaks, so the result reads back with `parse`
within the literal parsing limits; it is not a document unless the value is
printed as a document. A value that contains [handles](#handles) reads back only as a document.

## Stores

> **Since: Unreleased**

A store is a set of documents that together describe many objects of one
[handle type](Schemas.md#handles), each under a stable Text id: the units, hulls or
weapons of a game. Every entry is the state of one object, written like a state
document and checked against the type's state schema. Entries refer to each other
by short references instead of complete handles. A store spans files through
imports and is read as a whole; GES never reads files itself, the host passes the
text of each requested file.

```gesd
:Store(@Unit)
import 'drones/heavy.gesd'

scout: [name: 'Scout', hull: @hull.light, escort: nothing]

module drone {
  gnabblo: [name: 'Gnabblo', hull: @hull.light, escort: @scout]
  brakk: import 'drones/brakk.gesd'
}

module hull {
  light: [name: 'Light Hull', armor: 20]
}
```

### The store model

A store is one read API, whatever its entries come from: [files](#reading-a-store), versioned
[sources through handles](#reading-from-handles), or later the cache of a network receiver. Every store
offers the same reads:

- the entry of an id, with its handle, state and origin;
- all entries in id order, and the entries of one module in the same order;
- the index of a [projection](Schemas.md#projections) of its type: every entry's handle in id order with the
  projection's version;
- use as the [resolver](#stores-as-resolvers) of its type.

Every entry has exactly one version, the one its handle carries: the content hash of its state for a
type `versioned by hash(#ges)`; an entry of a type with another version form carries none. A host holds at most one store per
handle type, the one it registers as that type's resolver.

A store has one owner, which is the only party that answers index and resolve requests for its entries; every other
holder of the same type's entries only reads. The owner is the host that reads the store's files or registers its
source, or across a network the side whose route [`owns`](Network.md#declaration) the type. A receiver's cache of
another side's entries is a store of the same type that the receiver reads but does not own.

Scripts do not see stores: they receive handles and read states through the resolver, as for any other handle type.

### Store files

```text
store    = header { item }
header     = ":Store" "(" "@" TypeName ")"
item       = import | module | entry
import     = "import" quoted Text
module     = "module" name "{" { entry } "}"
entry      = key ":" ( value | "import" quoted Text )
name       = key { "." key }
key        = 1*( ASCII letter | digit | "_" | "-" )
```

- A store file starts with its header, which names the handle type of every entry;
  comments and blank lines may precede it. The type is a handle type with a state,
  a Text id and no root. A store file contains no other header.
- Items follow the separator rules of root entries: a comma or a line break separates
  them. A `module` block is one item; a line break directly after its `{` and before its
  `}` is trivia, and its entries follow the same separator rules.
- `import` and `module` start an item only when no `:` follows the word; `import: 5`
  and `module: 5` are ordinary entries.
- A key is a bare name of ASCII letters, digits, `_` and `-`; stores have no quoted
  keys. A module name is one or more such keys joined by `.`.
- Module blocks do not nest. The same module may appear in several blocks and files;
  its entries add up.
- An entry's value is a Map literal of the state's fields, read with the rules of
  documents and with [references](#references); another value is
  `document.unexpectedToken` at its start. Instead of a value, `import 'path'`
  takes the entry's state from a state document: a document whose header is
  `:Schema(:StateName)` of the store's state, which may also contain references.
- A root `import 'path'` adds a further store file of the same type. Its root entries
  become root entries and its modules join the modules of that name. A root import
  stands only at the root, never inside a module.

A document read as a plain document rejects a `:Store` header with
`document.invalidSchemaHeader` and a `module` block with `document.unexpectedModule`;
references are `document.invalidHandle` there.

### Ids

The id of an entry is its module name, a `.` and its key, or only its key for an entry
at the root. `module drone { gnabblo: ... }` declares the id `drone.gnabblo`. Every id
is a Text id of the store's type: one whose UTF-8 length exceeds the id's `max` is
`store.invalidId`. Ids are unique across all files and roots of a store; a repeated
id is `store.duplicateId` at the later entry, which names the first one.

Store modules are names in a data file. They are unrelated to the
[module declaration](Language.md) of a script source and follow their own name rule.

### References

```text
reference  = "@" name [ "~" ]
pinned     = handle-without-version "~"
```

- A reference names an entry of the store's own type by its id. A reference with more
  than one segment, `@hull.light`, is the complete id. A reference with one segment,
  `@scout`, names an entry of the referring entry's module or a root entry; if both
  exist it is `store.ambiguousReference`, if neither exists `store.unknownReference`.
- A complete handle, `@Unit:hull.light`, names the entry with that id as well. A handle of
  another type names an entry of that type's store when the host passed that store
  as a dependency; without one, a handle of another type is not checked.
- `@` followed by a `TypeName`, `:` and directly the start of an id is a handle; every
  other `@` followed by a name is a reference. A reference may be a Map key; then it
  carries no `~`.
- A reference reads as the handle of the named entry, `@Unit:hull.light`, without a
  version. With a trailing `~`, a reference or a complete handle of an entry is pinned:
  it carries the content hash of the named entry as its version. Pinning needs a type
  `versioned by hash(#ges)`, otherwise it is `store.invalidPin`, and the named
  entry's store: the own one or a dependency. Pinned references must not form a cycle,
  which is `store.dependencyCycle`; unpinned references may, because the game resolves
  them.
- The resulting handle is checked against its field like every other handle: an `@Name~`
  field needs a pinned reference.

An unknown reference carries a suggestion: the first id in id order whose last segment
equals the reference's last segment; otherwise the id with the smallest edit distance,
at most 3, to the written id, the first in id order among equals. A one-segment
reference compares with the ids of both the own module and the root. Without such an id
there is no suggestion.

### Import paths

An import path is a non-empty quoted Text of segments separated by `/`. It is relative
to the directory of the importing file; a leading `/` makes it relative to the root.
Empty segments and `.` are ignored and `..` steps up one directory. A path that steps
above the root is `store.importOutsideRoot`. The host interprets the resulting
root-relative path; a file it does not have is `store.importMissing`.

A file that imports a file on its own import chain is `store.importCycle`. A file
reached a second time on another path contributes nothing further, so every file
contributes once. A root import must name a store file and an entry import a state
document of the store's state; another kind of file is `store.importKind`, a
store or state of another type `store.importType`.

### Reading a store

A host reads a store of a handle type from one or more named roots, each with the
path of its root file, and optionally the stores of other types it depends on. The
root file of each root follows the rules of a root import. Roots are read in the given
order and every file in import order: its own items first, an import's file at the
import.

The result holds the type and every entry, sorted by id in ordinal order. An entry has
its id, its handle, its state and its origin. The handle carries the id, and for a type
`versioned by hash(#ges)` the content hash of the state. The state is a Record of
the state type with the schema's defaults applied, exactly as a resolver's state. The
origin names the root, the root-relative path of the file and the line and column of
the entry's key. The entries of one module are those whose id starts with the module
name and `.`, in the same order.

Reading reports every error instead of the first one. Each error has a code, a message
that is not part of the contract, the root, path, line and column of its position, the
field path of a schema error, optionally a second position and, for an unknown
reference, the suggested id. Errors are ordered by file in reading order and by position
within a file. Reading proceeds in three steps; a step runs only when the previous one
found no error:

1. Every file is read. A file reports at most its first reading error, with the codes and
   positions of [documents](#errors); import errors are reported at the import's path.
2. Ids are checked.
3. References are resolved and every entry is checked against the state schema; an entry
   reports at most one schema error.

| Code | Reported when | Position |
| --- | --- | --- |
| `store.importMissing` | the host has no file for a root or an import | the import's path; no line and column for a root file |
| `store.importOutsideRoot` | an import path steps above the root | the import's path |
| `store.importCycle` | an import names a file on its own import chain | the import's path |
| `store.importKind` | a root file or root import is no store file, or an entry import no state document of a type schema | the import's path; the start of a root file |
| `store.importType` | such a file names another handle or state type | the import's path; the header of a root file |
| `store.invalidId` | an id is longer than the id's `max` | the entry's key |
| `store.duplicateId` | an id repeats | the later key; the second position is the first key |
| `store.unknownReference` | a reference or handle names no entry of a known store | the `@` |
| `store.ambiguousReference` | a one-segment reference names both a module and a root entry | the `@` |
| `store.invalidPin` | a pinned reference to a type without content hashes or without a known store | the `@` |
| `store.dependencyCycle` | pinned references form a cycle | the `@` of the reference that closes it, first in reading order |

Each file stays within the document [limits](#limits) on its own. A store has no
further limits; the host decides which files it passes.

### Reading from handles

> **Since: Unreleased**

A store may also be read from versioned sources, such as a repository at a commit. Each
root is then a *root handle* with the path of its root file: a handle of a type without a
root, such as `@Repository:content~#main` of `handle @Repository with id :Text max 64
versioned by hash(#sha1)`. Its id is a logical name of the source, which only the host
interprets; GES reads neither the id nor the files. The root handle's type needs no state.
Every file of that root, the root file included, is requested from the host's *source* by
the root handle and the file's root-relative path:

- the source answers the file's text, or *missing*, which is `store.importMissing` at the
  import's path, and for the root file without line and column;
- a path longer than 256 UTF-8 bytes is `store.importMissing` without asking the source;
- an error the source throws ends the read unchanged.

An answer may carry the version of the root it comes from. A root without a version is
asked as given, a ref included, until an answer passes a version; afterwards it is asked
with that version, so every file of a root comes from one version. A later answer with
another version fails the read with `runtime.handleVersionMismatch`, an answer whose
version is no integer or 20 or 32 bytes with `runtime.handleResolveFailed`. A source
answers an unknown ref as *missing*. A root's name, which origins and errors name, is the
[text form](Language.md#handle-text-form) of the root handle as given, and two roots with
equal text forms are not allowed. The store's *sources* are the root handles with the
version they were read at, in the given order; a store read from named roots has none.

### Stores as resolvers

A store is a [handle resolver](HostRuntime.md#handles) of its type: a host that
registers it lets scripts read the states of its entries. It answers a handle of a
known id with the entry's state and, for a type `versioned by hash(#ges)`, its content hash, and
every other handle as *missing*. A handle that carries another version is checked by
the host as for every resolver and fails with `runtime.handleVersionMismatch`.
