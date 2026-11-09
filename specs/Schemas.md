<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# GES schemas and wire format

> **Since: Unreleased**

This document owns GES schemas: `.gess` files that describe data formats, and the
compact binary encoding derived from them. It is written for server-driven
multiplayer games, where a Swift server sends many small objects to C#/Unity
clients about 20 times per second per client. The same schemas check
[GES documents](Documents.md); there is one schema language.

The encoding is independent of `.gesb`, of [product JSON](MessageFormat.md) and of
the script language. `.gess` is a small language of its own in record notation.
Scripts do not change, and a schema is not a script record. The
[schema guide](../docs/guide/Schemas.md) introduces it step by step.

Not part of this document: transport, publish/subscribe, interest management and
the transfer of large data. [Extension points](#extension-points) names where they
attach.

## Principles

- The wire carries values only: no field names, type tags or units.
- Sender and receiver hold the same schema and the same parameter values. Neither
  is negotiated by this format; the caller exchanges them at connection setup.
- A range and a step determine the width of a field. Widths are fixed once a schema
  is bound and never depend on a single record.
- A quantized value is lossy by declaration. Encoding is exact and identical in
  every port: equal input produces equal bytes, and equal bytes decode to equal
  values.
- A value outside its declared range is an error to the caller. It is never
  clamped silently.
- Data from a peer is untrusted. Every limit is known before decoding starts.

## Source language

The Syntax module reads a schema source into its declarations. It uses the
[lexical rules](Syntax.md) of GES: line comments, the
number and unit literal grammar of
[Numbers](Semantics/Numbers.md#text-and-number-conversion), and the
[name grammars](Semantics/Text.md#portable-name-grammars). GES never reads files;
hosts pass one or several source texts, and references resolve across all of them,
as in a joint compilation.

```gess
// Constants and parameters belong to this file.
constant $maxHealth be 135
parameter worldWidth: :Quantity(m)
parameter worldHeight: :Quantity(m)

schema :Bot as {
  id: :Number integer between 0 and 65535 key
  x: :Quantity(m) between 0m and worldWidth step 1cm delta within 1.27m
  y: :Quantity(m) between 0m and worldHeight step 1cm delta within 1.27m
  heading: :Quantity(°) wrapping between 0° and 360° step 0.1° delta within 8°
  turretBearing: :Quantity(°) wrapping between 0° and 360° step 0.1° delta within 8°
  radarBearing: :Quantity(°) wrapping between 0° and 360° step 0.1° delta within 8°
  health: :Number integer between 0 and $maxHealth
  firing: :Boolean
  shielded: :Boolean
  overheated: :Boolean
}

message Hit(target: :Number integer between 0 and 65535, _ damage: :Number integer between 0 and 1000) tags (#critical, #splash)
message Died(target: :Number integer between 0 and 65535)
messages :Combat as (Hit, Died)
```

### Grammar

```text
source      = { constant | parameter | schema | message | messages }
constant    = "constant" "$" ConstantName "be" literal
parameter   = "parameter" LowerName ":" ( ":Number" | ":Percentage" | quantity )
schema      = "schema" ":" TypeName "as" "{" { field } "}"
message     = "message" MessageName "(" [ argument { "," argument } ] ")" [ "tags" tagset ]
messages    = "messages" ":" TypeName "as" "(" MessageName { "," MessageName } ")"
field       = LowerName ":" union [ "optional" | "key" ]
argument    = [ "_" ] LowerName ":" union [ "optional" ]

union       = type { "|" type }
type        = ":Boolean"
            | numeric
            | spatial
            | ":Tag" [ tagset | "max" integer ]
            | ":Text" [ "max" integer ]
            | ":List" [ "(" union ")" [ "max" integer ] ]
            | ":" TypeName
            | ":Dice" [ "max" integer ] constraints
            | ":Map" | ":Range" | ":Series" | ":Message" | ":Handler" | ":Nothing"
numeric     = ( ":Number" | ":Percentage" | quantity ) constraints
quantity    = ":Quantity" "(" unit ")"
spatial     = ( ":Point" | ":Vector" ) [ "(" unit ")" ] [ "{" axis { axis } "}" ]
axis        = ( "x" | "y" | "z" ) constraints
constraints = [ "integer" ] [ range ] [ "step" value ] [ "delta" "within" value ]
range       = [ "wrapping" ] "between" bound "and" bound
bound       = value | LowerName
value       = literal | "$" ConstantName
tagset      = "(" "#" TagName { "," "#" TagName } ")"
```

`LowerName`, `ConstantName`, `TypeName`, `MessageName` and `TagName` follow the
[portable grammars](Semantics/Text.md#portable-name-grammars). `unit` accepts what
[`:Quantity(unit)`](Language.md#records-and-custom-types) accepts. `literal` is one exact
finite number of the [number grammar](Semantics/Numbers.md#text-and-number-conversion), with an
optional `%` or unit suffix; a Text, a constant or a parameter is no literal. `integer` is
at most 12 decimal digits without a leading zero.

**Layout.** Spaces and tabs may separate any two tokens, and a line comment may end any
line. A source may start with a BOM. Fields and axes are separated by commas or line breaks,
and repeated or trailing separators are allowed. Arguments, members and tags are separated by
exactly one comma. A line break is allowed only in these places: between declarations, after
`as`, inside the parentheses of an argument list, a member list, a tag set or a `:List`, inside
the braces of a schema or an axis block, and after `|`. Each field, with its type and marker,
otherwise stays on one line.

### Declarations

A source is a sequence of declarations in any order; a reference may come before what it names.

- **`constant $name be literal`** names one value of this file. It is replaced where it is used
  and leaves no trace in the [canonical form](#canonical-form).
- **`parameter name: kind`** names a value that is supplied at [binding](#binding), with its
  kind and unit and no other constraint. It serves only as a bound of a range in this file. A
  parameter that a root does not reach is not part of that root.
- **`schema :Name as { ... }`** declares a record type: named fields in order, none or many.
  It can be a root, the type of a field, argument or list element, and the type name of a
  [Record](#record-values). A schema without fields encodes to no bits.
- **`message Name(...) tags (...)`** declares one message signature: its name, its arguments in
  order, and a closed set of tags, of which any subset may be present. An argument has the
  label of its name, as in `target: 5`, or with `_` none, so `_ damage` is passed by position
  and its name serves only the [field handle](#writer-and-reader) and the
  [description](#description). A message can have no arguments. It can be a root, but it is
  not a type: a field cannot name it.
- **`messages :Name as (A, B)`** declares a message set: a closed, ordered choice of one
  of the listed messages, at least one, each once. Its encoding starts with the index of the chosen
  message, so a receiver learns which message follows; see [messages](#messages). A set
  can be a root and nothing else: it is not a type, and a message may belong to several sets.

Schemas, messages and message sets share one namespace across all sources.

A field or argument has one of these markers at most:

- **`optional`** allows the value to be absent, which reads as `nothing`, and takes a presence
  bit. It applies to the whole union, and only to a field or argument, not to a list element.
- **`key`** marks the [key](#keys) of a schema.

Types:

| Type | Values |
| --- | --- |
| `:Boolean` | `true` or `false` |
| `:Number`, `:Percentage`, `:Quantity(unit)` | a number, a percentage, or a number with that unit; `:Quantity(none)` is `:Number`. Limited by [constraints](#constraints) |
| `:Point`, `:Vector`, with an optional unit | three components with one unit. A block lists the axes that are transmitted, in any order, each with its own constraints |
| `:Tag(#a, #b)`, `:Tag max n`, `:Tag` | one tag of the closed set, a tag of at most `n` characters, or any tag |
| `:Text max n`, `:Text` | a text of at most `n` UTF-8 bytes, or any text |
| `:List(T) max n`, `:List(T)`, `:List` | at most `n` elements of `T`, any number of them, or any elements |
| `:Dice max n between a and b`, `:Dice` | at most `n` rolls, each within the range, or any rolls |
| `:Name` | a Record of the schema `Name` |
| `T \| U` | a value of one of the alternatives; see [rules](#rules) |
| `:Nothing` | `nothing`, which is useful as an alternative |
| `:Map`, `:Range`, `:Series`, `:Message`, `:Handler` | a value of that kind |

A schema, message or message set cannot take the name of a built-in type of this table, nor
`Record`, the language's type of all Records.

### Constraints

In this order, all optional:

- **`integer`** requires whole values, stored exactly. Not for a `:Percentage`.
- **`between a and b`** is a closed range, **`wrapping between a and b`** a circular one; see
  [rules](#rules) and [wrapping values](#wrapping-values).
- **`step s`** quantizes to multiples of `s`; see [quantization](#quantization).
- **`delta within d`** declares an expected change; see [rules](#rules).

Together, the type and the constraints decide a field's [encoding](#field-encodings).

### Rules

- A bound is a literal, a declared constant or a parameter; it cannot refer to a field.
  A step and a delta range are literals or constants, never parameters. No expression is evaluated, so reading and
  binding need neither a compiler nor a VM.
- `between a and b` is the closed range `[a, b]`; `wrapping between a and b`
  is the half-open range `[a, b)`. The lower bound must be below the upper bound, and
  both are finite. Nothing is clamped: a value outside a closed range is an error.
- A literal's unit must equal the field's unit. A type names the base unit, and a
  [prefixed spelling](Language.md#quantities) is literal sugar only, as in scripts:
  `:Quantity(N) between 0kN and 50kN step 0.01kN`, never `:Quantity(kN)`. Prefixes scale
  exactly, and no other conversion exists.
- `integer` excludes `step`: an integer field is exact and a fractional value is an
  error. `step` selects a [quantized field](#quantization) and requires a range.
- The bounds of an `integer` field are integral and lie within Int64, `-2^63` to
  `2^63 - 1`, so its width is at most 64 bits. Anything else is `schema.invalidBound`, at
  binding also for a parameter with a fractional value.
- `delta within D` declares the largest change expected between two updates of a value:
  `d = D / step` indices, or `D` for an `integer` field. It requires a range and, unless
  `integer`, a step. `D` is positive and a multiple of the step, and `width(2d + 1)` is below
  the [width](#field-encodings) of the field. It belongs to the description and the hash,
  sets the [range form of a delta](#delta-range) and does not change the encoding of a record.
- Omitted axes of a spatial block are not transmitted and must be zero.
- `key` marks the [key](#keys) of a schema. It is the first field, at most one per schema,
  and an `integer` numeric or a `:Tag` with a set; it excludes `optional` and `delta within`
  and cannot be a union. Anything else is `schema.invalidKey`.
- A union lists the types a value may have:
  `facing: :Quantity(°) between 0° and 360° | :Tag(#north, #south)` or
  `units: :List(:Text max 8 | :Unit | :Nothing) max 4`. Each alternative has its own constraints;
  `optional` applies to the whole union. An alternative that repeats a type name, or that has
  `delta within`, is `schema.invalidUnion`, and so is `optional` on a union with `:Nothing`, which
  would give `nothing` two encodings. `:Quantity(m) | :Quantity(s)` repeats
  the type name `:Quantity`: a unit does not distinguish alternatives.
- A Dice has no constraint other than a range, whose bounds lie within `1` to `2^31 - 1`, as a roll is a
  positive Int32. Anything else is `schema.invalidConstraint` or `schema.invalidBound`.
- A field with a `:Nothing` alternative may be left out, in a value and in a document, which
  counts as `nothing`. A Message holds every argument of its signature, with `nothing` where one
  is absent, since its labels and their order are its signature.
- A schema reference names a `schema`; references must not form a cycle. Nesting is
  limited to 32 levels. Lists nest at most 4 levels for encoding, so that a writer and
  a reader keep their running lists without allocating; deeper lists are
  `schema.notEncodable`.
- There is no `computed by`. A receiver derives such values in its own code; see
  [Records](#record-values).
- Constants and parameters are visible only in the file that declares them. Schema,
  message and message set names are visible across all sources.
- A `max` is positive.
- A position relative to something else, such as a carrier, is a schema of its own
  with its own bounds.
- Names are unique: declarations of schemas, messages and sets together, constants and
  parameters per file, fields within a schema, arguments within a
  message, axes within a block, tags within a set, members within a message set. A Message's labels and their order are part of its
  signature, as everywhere in GES.

### Encodable and document-only types

Every type is valid in a schema. Only a type with a known width or a hard limit can
be encoded; binding a schema that reaches another type for encoding fails with
`schema.notEncodable`. Document checking accepts all of them.

| Type | Encodable | Document check |
| --- | --- | --- |
| `:Boolean`, numeric, `:Point`, `:Vector` | yes | yes |
| `:Tag(#a, #b)`, `:Tag max n` | yes | yes |
| `:Text max n` | yes | yes |
| `:List(T) max n` with an encodable `T` | yes | yes |
| `:Dice max n` | yes | yes |
| `:TypeName` with only encodable fields | yes | yes |
| A union of encodable alternatives, such as `:Text max 8 \| :Unit \| :Nothing` | yes | yes |
| `:Tag`, `:Text`, `:List`, `:List(T)`, `:Dice` without a set or `max` | no | yes |
| `:Map`, `:Range`, `:Series`, `:Message`, `:Handler`, `:Nothing` outside a union | no | yes |

### Source errors

Source errors use the phase `schema` with line and column, in the manner of
[document errors](Documents.md#errors):

| Code | Condition |
| --- | --- |
| `schema.syntax` | the source does not match the grammar |
| `schema.duplicateName` | a name repeats within its kind, or a schema, message or message set takes the name of a built-in type or `Record` |
| `schema.unknownName` | a type, message, constant or parameter is not declared |
| `schema.cycle` | schema references form a cycle, or references or `:List` types nest more than 32 levels |
| `schema.unitMismatch` | a bound, step or delta range has another unit than its field |
| `schema.invalidRange` | a lower bound is not below its upper bound |
| `schema.invalidStep` | a step is not positive, not exact, or a bound or delta range is no multiple of it |
| `schema.invalidConstraint` | `integer` with `step` or on a Percentage, a Dice constraint other than a range, a `max` that is not positive, `step` or `delta within` without a range, `delta within` without a step or not narrower than the field |
| `schema.notEncodable` | encoding reaches a document-only type |
| `schema.invalidKey` | a `key` breaks the [key rules](#rules) |
| `schema.invalidUnion` | a union alternative repeats a type name or has `delta within`, or a union with `:Nothing` is `optional` |
| `schema.invalidBound` | an `integer` bound is fractional or outside Int64, or a Dice bound is outside `1` to `2^31 - 1` |
| `schema.tooWide` | a quantized field exceeds the [limits](#quantization), or a bound of a fractional field becomes infinite or zero in binary64 |
| `schema.tooLarge` | the [largest encoding](#largest-encoding) exceeds its limit or overflows |

`schema.notEncodable`, `schema.invalidRange`, `schema.invalidStep`, `schema.invalidBound`,
`schema.invalidConstraint`, `schema.tooWide` and `schema.tooLarge` can also arise at binding,
when a parameter determines the range.

## Binding

Binding fixes every parameter that a root schema reaches:

```text
bound = schemas.Schema(root).Bind({ worldWidth: 5000m, worldHeight: 5000m })
```

Parameters are never bound per record. A bound schema is immutable, holds every width
and layout, and can be shared by any number of encoders and decoders. Binding needs
neither a compiler nor a VM.

A file can be bound any number of times with different values, also within one
session, and each binding is an independent encoder and decoder. A session can thus
serve two zones of different size at once:

```text
smallZone = schemas.Schema("ZonedPosition").Bind({ zoneSize: 500m })
largeZone = schemas.Schema("ZonedPosition").Bind({ zoneSize: 4km })
```

The wire does not say which binding a record uses; the caller knows it from its
context, such as the zone of a connection or channel. A binding has its own widths, so
a record belongs to the binding that wrote it. Bindings
share nothing mutable and cost nothing per record.

Parameters are supplied by name, as an exact literal such as `5km` or as a number in base
units. A parameter without a value fails binding with `schema.unknownName`. When several files reached by a root declare a
parameter of the same name, they must declare the same kind and unit, and share the
value; otherwise reading the sources fails with `schema.duplicateName`.

Each parameter value must have the declared kind and unit. It converts to an exact
decimal: an Int64 directly, a Binary64 through its shortest
[roundtrip text](Semantics/Numbers.md#numeric-text-output-and-roundtrip). Binding
checks that every range is valid, every bound a multiple of its step, every delta range
narrower than its field, and the [largest encoding](#largest-encoding) within its limit.
GES sets no world size; only the limits apply.

### Largest encoding

A bound schema has `MaxByteCount`, the size of its largest top-level encoding. It is computed at binding with checked arithmetic, counting every optional as
present, every Text, List and Dice at its `max`, and every union as its index and its largest alternative. A schema counts the larger of its largest
full record and its largest [delta](#deltas), so one buffer serves both: a Boolean's delta costs its change bit, and a field with a delta range its change bit, the
fallback bit and the larger of both widths. It may be at most 10 485 760 bytes (10 MiB).
Exceeding this, or an overflow while computing it, fails binding with `schema.tooLarge`.
A [keyed list](#keyed-lists) counts its form bit and the larger of the whole list and its
element form: three counts and `max · (position + the larger of the largest element and its largest
delta without the key)`. That is the worst case for `removed + changed <= max` and
`changed + added <= max`, because the size is linear in `changed`, so one of both ends gives the largest.
A message set adds its index to the bits of its largest member, before rounding up to bytes.
A bound layout has at most 1 048 576 fields, counting each use of a referenced schema and each
List element type once; more also fails with `schema.tooLarge`.

A List element must have at least one bit, or `schema.notEncodable` fails binding: only then
does the data limit the count. A `:Tag` with one tag and a schema without fields have no bits.

## Schema hash

Every schema, message and message set has a 64-bit hash that checks version equality.
It is not sent with records: the caller compares it at connection setup or stores it in
its own header, and there is no compatibility between different hashes. It is no
security measure.

The hash runs over a [canonical form](#canonical-form), never over source text.
Comments, layout, file boundaries, declaration order outside the reached declarations
and the spelling of literals do not change it, so `50kN` and `50000N` hash alike. Field
order, types, bounds, steps and delta ranges do.

The hash is FNV-1a 64 (offset basis `14695981039346656037`, prime
`1099511628211`) over the UTF-8 bytes of the canonical form. On the wire it takes 8
bytes in little-endian order; as text it is 16 lowercase hexadecimal digits of the
number.

### Canonical form

The canonical form is UTF-8 text. Each line ends with one line feed, tokens are
separated by one space, and nothing else is added.

1. The first line is `gess1`.
2. One `parameter` line follows for every parameter reached, in ascending
   [scalar order](Semantics/Text.md) of the name.
3. The declarations follow, starting with the root. Each one reached is written once,
   when first reached in a depth-first walk through its fields in order.

```text
parameter <name> <kind>
schema <TypeName>
field <name> <type>
message <MessageName> [tags <#tag> ...]
argument <name or _ name> <type>
messages <TypeName>
member <MessageName>
```

`<type>` is written as:

| Source | Canonical |
| --- | --- |
| `:Boolean` | `Boolean` |
| `:Number`, `:Percentage` | `Number`, `Percentage` |
| `:Quantity(unit)` | `Quantity(<portable unit name>)`; `:Quantity(none)` is `Number` |
| `:Point(unit)`, `:Vector(unit)` | `Point(<portable unit name>)`, `Vector(...)`; no unit or `none` is `Point(none)` |
| axis block | after the type, `{ <axis> <constraints> ; ... }` in order x, y, z |
| `:Tag(#a, #b)`, `:Tag max n`, `:Tag` | `Tag(#a #b)`, `Tag max <n>`, `Tag` |
| `:Text max n`, `:Text` | `Text max <n>`, `Text` |
| `:Dice max n between a and b`, `:Dice` | `Dice max <n> between <a> <b>`, each part only when written; `Dice` |
| `:List(T) max n` | `List(<T>) max <n>`; `List(<T>)` and `List` without `max` or `T` |
| `:TypeName`, `:Map`, ... | `TypeName`, `Map`, ... |

A numeric type is followed by its constraints, and any field or argument then by its
marker, in this order: `integer`, `between <a> <b>` or `wrapping between <a> <b>`,
`step <s>`, `delta <d>`, then `optional` or `key`. A union writes its alternatives in
source order with ` | ` between them, also inside `List(...)`; the marker follows the last
one. Unit names are the
[portable names](Language.md#quantities). A bound is a number or a parameter name.

A number is its exact decimal value in base units, written `<significand>e<exponent>`:
the significand is an integer without trailing zeros, with a leading `-` when negative,
and zero is `0e0`. Prefixes and percent signs are applied first, and constants are
replaced by their values. So `1cm`, `0.01m` and `10mm` are all `1e-2`, and `135` is
`135e0`, `5km` is `5e3`.

```text
:Quantity(N) between 0kN and 50kN step 0.01kN
:Quantity(N) between 0N and 50000N step 10N
```

are both `Quantity(newton) between 0e0 5e4 step 1e1`.

### Binding hash

Parameter values are not part of the schema hash, because they change per map. The
caller exchanges them and passes them to both sides. A bound schema also offers a
binding hash, so that differing values show up in one comparison. It is FNV-1a 64 over
one line with the schema hash as text, then one line `<name> <number>` for every
parameter in ascending scalar order.

### Hash vectors

The `Drone` of the [worked examples](#drone) has this canonical form and the hash
`585afee9e1eddd77`:

```text
gess1
schema Drone
field id Number integer between 0e0 255e0 key
field x Quantity(meter) between -2e2 2e2 step 5e-2 delta 155e-2
field y Quantity(meter) between -2e2 2e2 step 5e-2 delta 155e-2
field heading Quantity(degree) wrapping between 0e0 36e1 step 5e-1 delta 155e-1
```

The `Bot` of the [source example](#source-language) has the hash `baecf12abb34e7c1`.
Bound with `worldWidth = worldHeight = 5000m` its binding hash is `551dd726cf2392d4`, with
`worldHeight = 2km` it is `358d9465639111b1`:

```text
gess1
parameter worldHeight Quantity(meter)
parameter worldWidth Quantity(meter)
schema Bot
field id Number integer between 0e0 65535e0 key
field x Quantity(meter) between 0e0 worldWidth step 1e-2 delta 127e-2
field y Quantity(meter) between 0e0 worldHeight step 1e-2 delta 127e-2
field heading Quantity(degree) wrapping between 0e0 36e1 step 1e-1 delta 8e0
field turretBearing Quantity(degree) wrapping between 0e0 36e1 step 1e-1 delta 8e0
field radarBearing Quantity(degree) wrapping between 0e0 36e1 step 1e-1 delta 8e0
field health Number integer between 0e0 135e0
field firing Boolean
field shielded Boolean
field overheated Boolean
```

## Wire format

### Bit order

The stream is a sequence of bits. Bit `i` is bit `i mod 8` (least significant first)
of byte `i div 8`. A value of width `w` is written with its least significant bit
first. Implementations may read and write whole little-endian words.

### Records

A record encodes its fields in declaration order without gaps. A nested schema and
list elements follow inline, without alignment. After a top-level record or message,
zero bits fill up to the next byte boundary, so an encoded record can be reused for
several receivers and records can be concatenated. A decoder rejects nonzero
padding. An encoding contains no header and no length; the caller frames records.
The decoder reports the bytes it consumed.

### Keys

A `key` lets a receiver find the object a record belongs to before it reads the rest. A key
is the first field, so it starts at bit 0 of a top-level record, in its ordinary encoding.
Only the width of the key is needed to read it. The marker takes effect at the top level and for
the elements of a [keyed list](#keyed-lists): a schema with a key that is reached as a nested field
or a list element writes the same bits in a full record, and no key can be read from there. The
caller owns the mapping from keys to objects. Message arguments have no key. A
[delta](#keys-in-a-delta) starts with the same key.

### Field encodings

`width(n)` is the smallest `w` with `2^w >= n`, and `width(1)` is 0.

| Type | Encoding |
| --- | --- |
| `:Boolean` | 1 bit |
| `optional` | 1 presence bit, then the value when present; absent is `nothing` |
| `integer` with a range of `n` values | index `value - lower`, `width(n)` bits, computed modulo `2^64` |
| `integer` without a range | two's complement, 64 bits |
| quantized | [index](#quantization), `width(n)` bits |
| wrapping | [index](#wrapping-values), `width(n)` bits |
| fractional without a step | IEEE 754 bits of the binary64, 64 bits; a range is checked but does not narrow it |
| `:Point`, `:Vector` | declared axes in order x, y, z, each as a numeric; without a block x, y, z as 64-bit binary64 |
| `:Tag(...)` with `n` tags | index in declared order, `width(n)` bits |
| `:Tag max n` | as `:Text max n`: the tag name without `#`, its byte count, then its bytes |
| `:Text max n` | UTF-8 byte count, `width(n + 1)` bits, then the bytes |
| `:List(T) max n` | element count, `width(n + 1)` bits, then the elements |
| `:Dice max n` | roll count, `width(n + 1)` bits, then each roll as an `integer` of its range, `1` to `2^31 - 1` without one. The [value path](#value-path) writes them in descending order and a typed writer in the order of its caller; a decoder accepts any order, since creating a Dice sorts its rolls |
| union of `n` alternatives | index in declared order, `width(n)` bits, then the alternative |
| `:Nothing` as an alternative | no bits |
| `:TypeName` | its fields inline |

`max` counts UTF-8 bytes for Text, characters for a Tag, which is ASCII, elements for a List and
rolls for a Dice. A range's index never
exceeds `n - 1`; a decoder rejects a larger bit pattern. Units come from the schema.

A fractional field without a step stays at 64 bits, which is a deliberate choice in the
schema; there is no 32-bit form. Flags are one `:Boolean` per flag.

### Messages

A message encodes its arguments in signature order, followed by one bit per declared
tag in declared order. The name and labels are not transmitted. A message with a tag
outside its set cannot be encoded. Tag order is not transmitted: a decoded message
holds its tags in declared order.

A message set encodes the index of the message in declared order in `width(n)` bits,
followed by the message. A `message` encoded outside a set has no index; the caller
knows what it receives. Message priorities and send IDs belong to the host and are not transmitted.

## Quantization

A quantized field has a range and a step. This section defines it exactly, so that
C# and Swift produce identical bytes and decode identical values.

### Decimal form

A step is an exact decimal `p / 10^k` in lowest terms, with integer `p > 0` and
`0 <= k <= 15`, taken after prefixes are applied. `1cm` is `p = 1, k = 2`, `5cm` is
`p = 5, k = 2`, `5km` is `p = 5000, k = 0`, `0.01kN` is `p = 10, k = 0`. Each bound must be a whole multiple of the step, which gives the
exact integers `lowerN` and `upperN`. For a closed range `n = upperN - lowerN + 1`.

Limits, checked with exact integer arithmetic at reading or binding:

- `|lowerN|` and `|upperN|` are at most `2^50`;
- `|upperN * p|` and `|lowerN * p|` are at most `2^53`.

`S` is the binary64 value of `10^k`, which is exact, and `P` the binary64 value of
`p`, which is exact as well.

### Encoding

For a finite binary64 `v`:

1. Reject NaN and infinities. Convert an Int64 input with a magnitude below `2^53`
   to binary64; a larger one is out of range.
2. For a closed range, reject `v` when it lies outside the
   [decoded](#decoding) values of `lowerN` and `upperN`. The comparison uses
   binary64 values, without rounding `v` into the range.
3. Compute `q = (v * S) / P` in binary64, in exactly this order, each operation
   rounded to nearest-even as IEEE 754 requires.
4. Reject `|q| > 2^50`. Otherwise `N = round(q)` with ties to even.
5. The index is `N - lowerN`.

The rounding of step 3 applies to the binary64 value, not to its decimal spelling:
`1.005` is below its spelling and rounds down, as the vectors below show.

### Decoding

For an index `i`, with `N = i + lowerN`:

- reject `i >= n`;
- compute `M = N * p` in Int64, which the limits keep exact;
- the value is `M / S` in binary64, a single correctly rounded division. It is thus
  the binary64 closest to the exact decimal, equal to the same number written as a
  literal.

A Number or Quantity then follows the
[storage rule](Semantics/Numbers.md#numeric-representations): an integral result is
stored as Int64, so `M` divisible by `10^k` gives the exact integer `M / 10^k`.
Percentages and spatial components remain binary64. Zero is positive zero.

### Why this is exact

Neither formula contains an addition next to a multiplication, so a compiler cannot
fuse them into one operation with different rounding, including under IL2CPP.
Every operation is a single IEEE 754 operation with one defined result.

Decoding followed by encoding returns the same index. A decoded value carries a
relative error of at most `2^-53`; steps 3 and 4 add two more. For `|N| <= 2^50` the
absolute error is at most `3/8`, less than the `1/2` that rounding tolerates. For the
same reason `v` inside the range cannot round to an index outside it, since rounding
is monotonic.

### Vectors

Step `1cm` (`p = 1, k = 2`) unless noted, closed range `[0, 5000]` unless noted:

| Input `v` | `q` | `N` | Decoded | Note |
| --- | --- | --- | --- | --- |
| `1234.565` | `123456.5` | `123456` | `1234.56` | tie to even |
| `0.135` | `13.5` | `14` | `0.14` | tie to even |
| `1.005` | `100.49999999999999` | `100` | `1` | binary64 lies below the spelling |
| `0.30000000000000004` | `30.000000000000004` | `30` | `0.3` | |
| `4999.996` | `499999.60000000003` | `500000` | `5000` | rounds to the upper bound |
| `5000.004` | | | | error: above the upper bound |
| `-0.004` | | | | error: below the lower bound |
| `12.34`, step `5cm` | `246.8` | `247` | `12.35` | `p = 5` |

## Wrapping values

A wrapping field describes a circle such as an angle. Its period holds
`n = upperN - lowerN` indices, and the upper bound is the same point as the lower
one. A finite value of at most `2^50` steps is valid, since it denotes a point on the
circle. A larger or non-finite value is `wire.outOfRange`, because its rounding would no
longer be exact:

1. Compute `N` as in [encoding](#encoding), without the range check of step 2.
2. The index is `(N - lowerN) mod n` with a nonnegative remainder.

All of it after step 1 is integer arithmetic. An `integer` field wraps the same way
with `N = value`, for every Int64. A decoder rejects `i >= n` and returns `lowerN + i` in the
[decoded form](#decoding), always within `[lower, upper)`.

| Input, `[0°, 360°)` step `0.01°` | `N` | Index | Decoded |
| --- | --- | --- | --- |
| `359.9949` | `35999` | `35999` | `359.99°` |
| `359.995` | `36000` | `0` | `0°` |
| `360` | `36000` | `0` | `0°` |
| `-0.004` | `0` | `0` | `0°` |
| `-90` | `-9000` | `27000` | `270°` |

## Non-quantized numbers

- An exact `integer` input must be an Int64 or an integral binary64 in the Int64
  range. Ranges compare exactly, without passing through binary64.
- A fractional field without a step stores the IEEE 754 bits. A Number stored as
  Int64 must be exactly representable in binary64, otherwise it is
  `wire.notRepresentable`. Zero is written as positive zero. A decoder rejects NaN
  patterns and negative zero, because neither is a canonical value. Infinities are
  valid only without a range.

## Deltas

A delta writes what differs from a baseline, the full record the receiver already holds. The
full encoding does not change, and a delta is an operation on two full records: the caller
chooses and confirms baselines, and the format assumes nothing about the transport. A delta and
a full record are not marked on the wire, so a caller that mixes them marks them in its own
framing. Only a `schema` has deltas; a message or message set is `wire.typeMismatch`.

### Form

A delta of a schema is one change bit per field in declaration order, followed by the
payload of each changed field in that order. Padding follows at the top level as in
[records](#records). Comparison uses wire values: indices, not the original numbers, so a
movement smaller than a step is no change and costs nothing.

| Type | Payload of a changed field |
| --- | --- |
| `:Boolean` | none; the change bit toggles the baseline |
| numeric with `delta within` | [range form](#delta-range) |
| other numeric, `:Tag`, `:Text`, `:List` of other elements, `:Dice` | the full encoding |
| `:List` of a schema with a key | a [keyed list](#keyed-lists) |
| `:Point`, `:Vector` with a block | one change bit per declared axis, then the payloads of the changed axes |
| `:Point`, `:Vector` without a block | three change bits, then a raw 64-bit binary64 for each changed axis; bits are compared |
| `:TypeName` | a nested delta: its own change bits and payloads |
| union | index; when the baseline has the same alternative, its payload, otherwise its full encoding |
| `optional` | presence bit; when present and the baseline also has a value, the field's payload, otherwise its full encoding |

A list is replaced as a whole, unless its elements have a key, which is what identifies them.

With `worldWidth = worldHeight = 5000m`:

```gess
schema :Beacon as {
  position: :Point(m) {
    x between 0m and worldWidth step 1cm delta within 1.27m
    y between 0m and worldHeight step 1cm delta within 1.27m
  }
  active: :Boolean
}
```

A full record takes 19 + 19 + 1 = 39 bits, 5 bytes. A delta has 2 change bits for the fields
and, when `position` changed, 2 for its axes:

| Situation | Bits | Bytes |
| --- | --- | --- |
| Only `active` toggled | 2 | 1 |
| Moved along `x` | 2 + 2 + 9 = 13 | 2 |
| Moved along both axes | 2 + 2 + 9 + 9 = 22 | 3 |
| `x` through the fallback, `y` in range | 2 + 2 + 20 + 9 = 33 | 5 |

### Keys in a delta

A top-level [key](#keys) comes first, in its full encoding with the same width, so a reader can
take it without knowing whether a full record or a delta follows. It has no change bit and is
written also when nothing else changed. A key identifies the object, so it is not compared like
other fields but required to be equal: encoding against a baseline with another key is
`wire.keyMismatch`, and decoding against one is `wire.invalidDelta`. A schema with a key that is
reached as a nested field writes it as its ordinary first field, with a change bit in a nested
delta. In a [keyed list](#keyed-lists), an element's key has neither.

### Keyed lists

> **Since: Unreleased**

A list is keyed when its element type is a single `:TypeName` whose schema has a `key`, at any depth.
This needs no syntax, and the full encoding does not change. Lists of other elements, including
unions, are replaced as a whole.

The payload of a changed keyed list starts with a form bit:

- **`1`, whole list:** its full encoding.
- **`0`, by element:** in this order, with counts in `width(max + 1)` bits and positions in the
  baseline in `width(max)` bits:
  1. `removedCount`, then the positions of the removed elements, ascending;
  2. `addedCount`, then the added elements in their full encoding, key included, ascending by key;
  3. `changedCount`, then for each changed element its position, ascending, and its nested delta, in
     which the key is neither written nor has a change bit.

Keys compare by value: an `integer` numerically, and a `:Tag` in declared order, so a sender gets the
by-element form by keeping its lists sorted by key. Elements with the same key in baseline and
record are kept, which makes them changed or unchanged. A key only in the baseline is removed, and a
key only in the record is added. The result is the merge of kept and added elements by key. A key
that the game reuses, such as a wrapped id, counts as a change of the same object.

By-element form requires the keys of **both baseline and record to be strictly ascending**, and
whole-list form is used if and only if one of them is not. Duplicate keys need no error of their
own: they are simply not ascending. There is no size comparison between the forms; a caller who wants
the smaller of a delta and a full record compares those at the top level. A field whose baseline
holds no value for it, in an `optional` or another union alternative, takes its full encoding, as
every field does, without a form bit.

With this schema, a full `World` takes 8 + 3 bits and 13 bits per element:

```gess
schema :Item as {
  id: :Number integer between 0 and 255 key
  hp: :Number integer between 0 and 15
  on: :Boolean
}

schema :World as {
  tick: :Number integer between 0 and 255
  items: :List(:Item) max 5
}
```

Against a baseline with the `(id, hp, on)` elements `(1, 5, off)`, `(2, 6, off)`, `(3, 7, off)`,
`(5, 8, off)` and `(7, 9, off)`, take a record with the same `tick` that removes key 2, adds
`(4, 3, on)` and sets `hp` of key 5 to 1:

| Part | Bits |
| --- | --- |
| Change bits `0`, `1` of `World` | 2 |
| Form bit `0` | 1 |
| `removedCount` 1, position 1 | 3 + 3 |
| `addedCount` 1, the element `(4, 3, on)` | 3 + 13 |
| `changedCount` 1, position 3 | 3 + 3 |
| Nested change bits `1`, `0` and `hp` 1 | 2 + 4 |

That is 37 bits, 5 bytes: `4a 42 30 b3 02`. The full record with its five elements takes 76 bits, 10 bytes.

### Delta range

`delta within D` declares `d = D / step` indices, or `D` for an `integer`. The payload is:

- bit `0`, then `delta + d` in `width(2d + 1)` bits, when `|delta| <= d`;
- bit `1`, then the full index otherwise.

`delta` is the signed difference of the indices. For a wrapping field with period `n` it takes
the shorter way: `delta = ((index - base + floor(n / 2)) mod n) - floor(n / 2)` with a nonnegative
remainder, and the decoder computes `(base + delta) mod n`. Differences of `integer` fields are
computed without overflow, also across the whole Int64 range.

### Canonical delta form

A delta has one valid encoding, so ports stay byte-identical. A decoder rejects with
`wire.invalidDelta` a set change bit for an unchanged value (except a Boolean, where it is the
change), a nested delta or axis group whose change bits are all zero, a range form with a
difference of zero or above `d`, a full index for a difference within `d`, a non-wrapping result
outside the range, and a key that differs from the baseline's. All other values are validated as
in a [full record](#decoding-untrusted-data).

In a [keyed list](#keyed-lists) it also rejects:

- whole-list form when both lists are strictly ascending;
- by-element form against a baseline that is not strictly ascending;
- positions that do not ascend strictly or lie outside the baseline, or the same position both
  removed and changed;
- added keys that do not ascend strictly, or that equal the key of a baseline element, removed or
  kept, since an element that stays is a change, not a removal and an addition;
- all three counts zero, since the field's change bit would then be set for an unchanged value;
- a result longer than the list's `max`.

A count above `max` is `wire.invalidCount`, as in a full record. A decoder checks in wire order, and a
check that needs the baseline takes place at the item that triggers it:

- whether the baseline ascends strictly: at the form bit, before any section;
- whether the baseline holds an added key: at that added element;
- whether a position is both removed and changed: at the changed position.

### Zones

Very large worlds are split by the game into zones, which needs no feature here:

```gess
parameter zoneSize: :Quantity(m)

schema :LocalPosition as {
  x: :Quantity(m) between 0m and zoneSize step 1cm delta within 1.27m
  y: :Quantity(m) between 0m and zoneSize step 1cm delta within 1.27m
}

schema :ZonedPosition as {
  zone: :Number integer between 0 and 65535
  local: :LocalPosition
}
```

With `zoneSize = 1000m`, `x` and `y` take 17 bits each, and a full `ZonedPosition` takes 50 bits,
7 bytes. A move inside a zone takes 2 change bits, 2 nested change bits and two 9-bit range forms:
22 bits, 3 bytes. A zone change needs nothing special: `zone` is written in full, and the local
coordinates are compared with the baseline as usual and leave their delta range through the
fallback bit, 1 + 17 bits each: 2 + 2 + 16 + 18 + 18 = 56 bits, 7 bytes.

Zones of different size use [one binding each](#binding). A baseline belongs to its binding, so an
object that moves to a zone of another size is sent as a full record of the new binding.

## Decoding untrusted data

A bound schema knows its largest encoding, so a caller can size buffers and a decoder
can bound its work before reading. Decoders also accept a caller limit on the bytes of
one record. Decoding fails without partial results for:

| Code | Condition |
| --- | --- |
| `wire.truncated` | the data ends before the record does |
| `wire.invalidIndex` | an index is at least `n`, for a range, tag, union or message set |
| `wire.invalidCount` | a Text, Tag, List or Dice count exceeds its `max` |
| `wire.invalidText` | Text is not valid UTF-8 |
| `wire.invalidTag` | a `:Tag max n` holds no valid [tag name](Semantics/Text.md#portable-name-grammars) |
| `wire.invalidNumber` | NaN or negative zero, or an infinity with a range |
| `wire.invalidPadding` | a padding bit is not zero |
| `wire.limitExceeded` | the record exceeds the caller's limit |
| `wire.invalidDelta` | a delta is not [canonical](#canonical-delta-form), leaves its range, or its key differs from the baseline's |

Encoding reports to the caller, and writes no partial record that is reported as
complete:

| Code | Condition |
| --- | --- |
| `wire.outOfRange` | a value is outside its range, not finite in a quantized field, or more than `2^50` steps from zero, also for a wrapping field |
| `wire.notRepresentable` | an integer field receives a fractional value, or a value does not fit its stored form |
| `wire.missingField` | a Record or Map has no key for a field that is neither `optional` nor has a `:Nothing` alternative, a Message lacks trailing arguments of its signature, or a typed writer leaves such a field unwritten or absent |
| `wire.nothingValue` | a field, argument or list element is `nothing` without being `optional` or having a `:Nothing` alternative |
| `wire.typeMismatch` | a value has another kind or unit than its field, a nonzero omitted axis, a Message whose labels differ from its signature, or a Map with a key that a union alternative does not have |
| `wire.unknownTag` | a tag is not in its set |
| `wire.invalidTag` | a typed writer gives a `:Tag max n` no valid tag name |
| `wire.tooLong` | a Text, Tag, List or Dice exceeds its `max` |
| `wire.bufferTooSmall` | the caller's buffer is too small |
| `wire.fieldOrder` | a typed writer or reader uses a field out of order |
| `wire.keyMismatch` | a record and its baseline have different keys |
| `wire.overlappingBuffers` | the target buffer of `GesSchemaDelta` overlaps an input, in either direction |

Errors carry the field path but no value from the data, so they can be logged safely.

## Record values

The convenient path converts between `GesValue` and the wire.

- A schema `:Bot` corresponds to a Record with the type name `Bot`. Fields are matched
  by name; a Map works too. Encoding does not convert units.
- Record fields without a schema entry are not transmitted. This covers the computed
  fields of a script record. The same holds for the keys of a Map, except when it selects a union
  alternative.
- Decoding builds `:Record("Bot", fields)` without a constructor, as in
  [product JSON](MessageFormat.md). A host with a Program may pass the fields to the
  record's constructor, which derives its computed fields on the normal VM.
- A Message converts in the same way, with its signature and tags.
- A value selects a union alternative by its kind, with unit, or for a Record by its type name; no
  other kind converts. Since alternatives repeat no type name, a union value has exactly one encoding. A Map
  has no type name: the first schema alternative that holds it decides. An alternative holds a Map
  when all its fields fit and every key is one of its fields, also in the Maps nested in it, as in
  a [document check](#document-checking). When none holds, the error of the last one is reported.

## Document checking

`.gesd` documents use the same schemas through their
[schema header](Documents.md#schema-header). Plain reading stays as specified there; a
separate operation, `CheckDocument`, belongs to the schema set, which follows the header, or
to a `GesSchema`, which ignores it, and takes optional parameters.

- `:Schema(:DriveUnit)` names a `schema` of the set. `:Schema('DriveUnit.gess')` names
  the source with exactly that name, whose declarations are available and that holds exactly
  one schema no other schema of it refers to, which is the root. A reference from a message
  does not count. Without such a source or root the check fails with `schema.unknownName`.
- Errors use document positions: `document.missingField`, `document.unknownField`,
  `document.fieldType`, `document.outOfRange`. Units are not cast implicitly.
- An explicit form counts as the value it constructs, so `:Number(3, 'm')` checks like `3m`.
  A position inside such a value is reported at the form. A form that constructs `nothing`
  is `document.invalidLiteral`, as in [reading](Documents.md).
- A union accepts a value that satisfies one of its alternatives, tried in source order;
  the first one decides. `document.fieldType` reports a value that satisfies none. A Map
  or a Record of that type satisfies a schema alternative when all its fields check.
  Reading of the document itself stays unchanged:

```gess
schema :Unit as {
  name: :Text
  count: :Number integer between 1 and 12
}

schema :Loadout as {
  units: :List(:Text | :Unit)
  facing: :Quantity(°) wrapping between 0° and 360° | :Tag(#north, #east, #south, #west)
}
```

```gesd
:Schema(:Loadout)
units: ['scout', [name: 'tank', count: 2]]
facing: #north
```

- A value outside a closed range is an error. A wrapping field accepts any finite
  value. `max` and `integer` are checked; `step` and `delta within` are not.
- A bound that uses an unbound parameter is not checked. A value that is given must have the
  declared kind and unit, otherwise the check fails with `schema.unitMismatch`.
- Printing a schema prints `.gess` source; printing a document is unchanged.

## API

### Writer and reader

The writer and reader encode into a caller's buffer and decode without `GesValue` or boxing.
One interpreted step runs per field, and the same API serves every schema, with no build step,
on every platform including [Unity](#unity). The dump tool and the value path use them too.

Names follow [port conventions](../implementation/PortConventions.md); the concepts are
the same in every port. Errors of schemas are their own types: C# `GesSchemaException`
and `GesWireException`, Swift `GesSchemaError` and `GesWireError`. In C#:

```csharp
var schemas = GesSchemaSet.Parse(sources);                        // once
var bot = schemas.Schema("Bot").Bind(new GesSchemaParameters()           // per session or map
    .With("worldWidth", "5000m").With("worldHeight", "5km"));
var x = bot.Field("x");                                           // field handles, once
var health = bot.Field("health");
var firing = bot.Field("firing");

var writer = new GesSchemaWriter(bot, buffer);                    // borrows the buffer
writer.WriteNumber(x, position.X);
// ... remaining fields in declaration order ...
writer.WriteInteger(health, 120);
writer.WriteBoolean(firing, true);
var length = writer.Finish();

var reader = new GesSchemaReader(bot, received);
var positionX = reader.ReadNumber(x);                             // double, without boxing
```

- `GesSchemaSet` holds the validated sources, `GesSchema` the description of one
  declaration and `GesBoundSchema` its bound layout. `GesSchema` and `GesBoundSchema` are
  immutable and can be shared between threads; a writer or reader belongs to one caller
  at a time. No port requires locks or threads.
- Field handles are resolved once by path, such as `x`, `position.x`, `units[]`,
  `units[].name`, `#critical` for a message tag, `facing:Tag` for a union alternative or
  `units[]:Unit.name` inside one. Reusing a handle costs no lookup.
- Typed operations: `Boolean`, `Integer` (Int64), `Number` (binary64), `Index` (the raw
  wire index, for game code that stores quantized values and skips the conversion), `Tag`
  (index) and `Text` (validated UTF-8, or for a `:Tag max n` a validated tag name without `#`). A reader copies Text into a caller's buffer of the
  field's `max`, since a value need not start at a byte boundary.
- `BeginList` writes or reads the element count of a List or Dice, and the elements follow, each entered at
  the `[]` handle. `BeginNested` enters a nested schema, Point or Vector. There is no end
  call: the cursor knows where a list or nesting ends.
- `WriteAbsent` marks an `optional` field as absent; any other write sets its presence bit.
  `ReadPresence` reads the bit, and a reader calls it before an `optional` field.
- `SelectAlternative` writes the index of a union's alternative, in declared order, and `ReadAlternative`
  reads it. The alternative's own handle follows, except for `:Nothing`, which is then complete.
- For a message set, `SelectMember` and `ReadMember` write and read the index and move on
  to the member, whose handles come from `bound.Member("Hit")`.
- Fields are used in declaration order; anything else is `wire.fieldOrder`.
- `bot.MaxByteCount` is the [largest encoding](#largest-encoding), for sizing buffers.
- For a schema with a [key](#keys), `reader.ReadKey()` returns it first, as an Int64 or a
  tag index, before the rest is read.
- `Finish` ends the record. A writer adds the padding and returns the bytes written; a
  reader checks the padding and returns the bytes consumed.
- Success allocates nothing and boxes nothing. A failure is reported through the normal
  error mechanism of the port and may allocate.

### Deltas between records

`GesSchemaDelta` converts between full records and [deltas](#deltas), on bytes:

```csharp
var length = GesSchemaDelta.Encode(bot, record, baseline, buffer);        // what differs
var decoded = GesSchemaDelta.Decode(bot, received, baseline, recordBuffer); // the full record again
```

The baseline is simply the full record that the other side holds, so a sender keeps the last
record a receiver confirmed, and a receiver keeps what `Decode` returned. `bot.MaxByteCount`
sizes both buffers. Encoding also uses its buffer as scratch space, for change bits it may drop and
for the [keyed list](#keyed-lists) section it writes before choosing the form, so a smaller buffer can
fail with `wire.bufferTooSmall` even when the delta would fit. A target buffer must be separate from the inputs, because it is written while they are read:
overlapping is `wire.overlappingBuffers`, also for records too short to be damaged. `Decode` returns the bytes of the record and those the delta occupied, and
takes an optional limit on the latter. Writer and reader are unchanged and produce and consume
the full records, and `ReadKey` also reads the key of a delta. Encoding reads both records
and rejects an invalid one with the errors of a [full record](#decoding-untrusted-data). Success
allocates nothing.

### Bit primitives

The writer and reader rest on public primitives that know no schema. Generated schema code
calls them with constant widths and operands, so they are part of the contract:

- `GesBitWriter` writes values of 0 to 64 bits in the [bit order](#bit-order) into a caller's
  buffer, `WriteBytes` writes the bytes of a Text, and `Finish` pads to a byte boundary and
  returns the byte count. A value has no bits above its width. `BitPosition`, `SetBit` for a
  bit already written as zero and `Rewind` to an earlier position serve encoders that learn a
  flag or count only later. A full buffer is `wire.bufferTooSmall`; a buffer of the record's
  byte count suffices.
- `GesBitReader` reads such values from untrusted data with an optional byte limit. Reading
  past the end is `wire.truncated`, or `wire.limitExceeded` when the limit ends first, with
  the path the caller names; `ReadBytes` reads nothing when the bytes do not fit. `Require`
  checks remaining bits without reading, `Seek` moves to a position, and `Finish` checks the
  padding (`wire.invalidPadding`) and returns the bytes consumed.
- `GesWireRules` holds the value rules over plain operands: `IsValidUtf8`, `IsTagName`,
  `QuantizedIndex` and `WrappedQuantizedIndex` with the [quantization](#quantization) operands
  `10^k` and `p`, `QuantizedValue` and `WrappedIndex`. Their errors and paths are those of the
  writer.
- `GesWireException` and Swift `GesWireError` are created from a code and a path; every
  `wire.*` code is a public constant.

Success allocates nothing.

### Description

`GesSchemaSet.Schemas` lists every declaration in source order and `Schema(name)` finds one;
an unknown name is `schema.unknownName`. A `GesSchema` describes what a source declares,
independent of parameter values, so it holds no widths or indices, and writer and reader do
not use it:

- `Name`, `Kind` (`Schema`, `Message` or `Messages`), `Hash` and `CanonicalForm`;
- `Parameters`, the reached parameters in ascending name order, each with `Name`, `Kind` and `Unit`;
- `Fields`, the fields or arguments in declared order, each with `Name`, `IsUnlabeled`,
  `IsOptional`, `IsKey` and `Alternatives`;
- `Tags` of a message and `Members` of a message set, in declared order;
- `Bind` and `CheckDocument`.

An alternative is a `GesSchemaType` with `Kind`, `Unit`, `IsInteger`, `IsWrapping`, `Lower`,
`Upper`, `Step`, `Delta`, `Tags`, `Max`, `Element`, `Axes` and, for a reference, `Schema`. An axis
has `Name` and the same limits. Absent limits are absent, and an empty `Tags`, `Element` or `Axes`
means no closed set, no element type and no axis block. A limit is a `GesSchemaBound`: a parameter's
name, or the exact decimal `Significand` and `Exponent` of the [canonical form](#schema-hash),
with a conversion to the closest binary64. `GesBoundSchema.Schema` leads back to the description.

### Value path

For rare data, Runtime offers `GesSchemaValues`: `Encode(bot, value, buffer)` encodes a
[Record, Map or Message](#record-values), `Decode(bot, bytes)` returns the value and the
bytes it occupied, and `ReadKey(bot, bytes)` returns a [key](#keys) without decoding the
rest. An external value of the schema's type name encodes through its materialized fields, also as a nested Record or a baseline, and a failing getter propagates as the [runtime failure of a field read](PublicApi.md), not as a wire error. `Encode(bot, value, baseline, buffer)` and `Decode(bot, bytes, baseline)` do the same for a
[delta](#deltas) against a baseline value. Swift uses the idiomatic spelling of the same concepts. Kinds do not convert implicitly:
the language computes with Boolean and Dice as numbers, but a Number, Quantity or Percentage
field rejects them with `wire.typeMismatch`, because decoding could not return them. A script
converts them explicitly with `as :Number`.

## Tools

Both CLIs provide:

```bash
ges schema check bot.gess
ges schema format bot.gess
ges schema hash bot.gess --root Bot --param worldWidth=5000m --param worldHeight=5000m
ges schema canonical bot.gess --root Bot
ges schema dump bot.gess --root Bot --param worldWidth=5000m --param worldHeight=5000m --data data.bin
ges schema dump bot.gess --root Bot --param worldWidth=5000m --param worldHeight=5000m --data delta.bin --baseline record.bin
```

`format` prints the recommended layout and, like document formatting, drops comments; input
and output must differ. An option a command does not use is a usage error.
`hash` prints the schema hash and, with parameters, the binding hash. A `--param` of `hash`
or `dump` for a parameter the root does not reach fails with `schema.unknownName`, so that a
misspelled name cannot go unnoticed; naming a parameter twice is a usage error. `dump` makes binary
data readable through the schema: it lists each field with path, bit offset, width and
decoded value, and ends with the first error, its field path and bit offset; invalid data
gives exit code 1. Its text is the same in every port. Each field is one line,
`<path> bit=<offset> width=<bits> value=<value>`, whose width includes a presence bit. The value
is `true` or `false`, a number, a quoted Text, `#<tag> index=<i>`, `#<tag>` for a `:Tag max n`, `count=<n>` for a List or Dice,
`<alternative> index=<i>` for a union, with the type name of the alternative, or `absent`. A nested
schema, Point or Vector has no line of its own, only its fields. A message set starts with
`message <name> index=<i>`. A valid record ends with `bytes <n>`, an invalid one with
`error <code> path=<path> bit=<offset>`. With `--baseline` the data is a [delta](#deltas): a first
line `delta bytes <n>` is followed by the lines of the resulting record, or the error ends the text
with its bit offset in the delta. An invalid baseline is the single line `baseline error <code> path=<path> bit=<offset>`
with its offset in the baseline. A Text is quoted with `'` on one line:
`\\`, `\'`, `\n`, `\r` and `\t` are escaped, and other controls are written `\u{hex}`. A
decoded number uses its shortest digits. It is positional, without a trailing `.0`, when it
has at most 21 digits before the decimal point or at most 3 zeros directly after `0.`,
otherwise `d.ddde+x` or `d.ddde-x`. `:show` in the
console and the highlighter know `.gess`, and the editor bundles have a grammar for it.

## Modules

- **Syntax** reads `.gess` into its declarations and reports syntax errors. It has no
  dependencies, and highlighting comes with it.
- **Runtime** holds the source rules, binding, hash, quantization, writer, reader, dump,
  document checking and the value path. Everything except the value path uses neither
  `GesValue` nor the VM, so a client that only encodes pays nothing for them at run time, and
  a linker can remove them. Compiler is not needed.

Schemas add no filesystem, thread or async dependency and import no Foundation.

## Unity

The C# part runs in Unity. It is delivered as a precompiled `netstandard2.1` assembly and
holds to the following, verified against the reference assembly and compiler of Unity
6000.3.

**Profile.** Unity's default API compatibility level is .NET Standard 2.1, and it is the
one with the broadest platform support. Runtime targets it without further
packages, so it also works under the .NET Framework level, which includes those APIs.

| Used | Not available in this profile |
| --- | --- |
| `Span<T>`, `ReadOnlySpan<T>`, `stackalloc` | `System.Numerics.BitOperations` |
| `BinaryPrimitives`, `MemoryMarshal` | `System.Runtime.CompilerServices.Unsafe` |
| `Math.Floor`, `Math.Round(double, MidpointRounding)` | `UInt128` |
| `BitConverter.DoubleToInt64Bits` | `System.Text.Unicode.Utf8`, `System.Text.Rune` |
| `UTF8Encoding` for the value path | `Math.FusedMultiplyAdd`, `Math.BitIncrement` |

Width calculations and UTF-8 validation are therefore written by hand. Exact arithmetic
at binding uses checked Int64 within the [limits](#quantization), without `UInt128`.

**Language.** Unity compiles scripts as C# 9, so:

- the public API can be used from C# 9, without `init` setters, `required` members, ref
  fields or static abstract members. A `ref struct` writer over `Span<byte>` is fine;
- `unsafe` is not part of the public API, so a project needs no special player setting.

**IL2CPP.** There is no code generation at runtime, and the schema code needs none. It
uses no Reflection, so managed code stripping removes nothing it needs, and no
generic virtual methods over value types. A cast from binary64 to Int64 happens only
after the range check of [encoding](#encoding), because an out-of-range cast has no
defined result in the generated C++. The rounding of [quantization](#why-this-is-exact)
has no multiplication next to an addition, so it needs no compiler setting.

**Without allocations.** On success the writer and reader allocate nothing under Mono and
IL2CPP: field handles are structs, Text moves between caller's buffers, and no enum, delegate
or closure takes part. A caller can pass `stackalloc` memory, a pooled array or a
`NativeArray<byte>` through `AsSpan()`. Schema code is not Burst-compatible: a bound schema
is a managed object. Managed worker threads can share it.

## Extension points

These build on this format and are not part of it.

- **Transport** frames top-level records, adds its own type and sequence fields, and
  carries the hash at connection setup. The byte-aligned, reusable records allow
  concatenation into packets.
- **Interest management** decides which records a client receives. It owns object
  identities; a list or a packet then holds only what it selected.
- **Publish/subscribe** maps channels to message sets or schemas.
- **Large data** is split into records of bounded size by a layer above, because every
  record has a hard maximum.

## Worked examples

Bytes are the size of the record alone, without transport framing.

### Bot

Schema [above](#source-language), with `worldWidth = worldHeight = 5000m`.

| Field | Values `n` | Bits |
| --- | --- | --- |
| `id`, the key | 65536 | 16 |
| `x` | 500001 | 19 |
| `y` | 500001 | 19 |
| `heading` | 3600 | 12 |
| `turretBearing` | 3600 | 12 |
| `radarBearing` | 3600 | 12 |
| `health` | 136 | 8 |
| `firing`, `shielded`, `overheated` | | 3 |

16 + 19 + 19 + 12 + 12 + 12 + 8 + 3 = **101 bits, 13 bytes**. 100 visible bots at 20 updates
per second take 26 000 bytes/s, without framing. Its `MaxByteCount` is 14, for the largest delta.

At 20 updates per second, a delta range of `8°` covers turning up to 160°/s, above the 150°/s of
the fastest units. With the 16-bit key and 9 change bits, a [delta](#deltas) takes 25 bits before
any payload. A range form takes 1 + 8 bits, the fallback of `x` and `y` 1 + 19, and `health` 8:

| Situation | Bits | Bytes |
| --- | --- | --- |
| Unchanged; the caller may send nothing | 16 + 9 = 25 | 4 |
| Standing, radar turning | 25 + 9 = 34 | 5 |
| Moving, all angles turning | 25 + 9 + 9 + 9 + 9 + 9 = 70 | 9 |
| Same, hit and starting to fire | 70 + 8 + 0 = 78 | 10 |
| Teleport: `x` and `y` through the fallback | 25 + 20 + 20 = 65 | 9 |

100 visible bots at 20 updates per second take 18 000 bytes/s as moving deltas.

### Drone

Positions are relative to a carrier, so a drone escorting a moving carrier changes
nothing on the wire. Adding the carrier's position is the game's business.

```gess
schema :Drone as {
  id: :Number integer between 0 and 255 key
  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m
  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m
  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°
}
```

| Field | Values `n` | Bits |
| --- | --- | --- |
| `id`, the key | 256 | 8 |
| `x` | 8001 | 13 |
| `y` | 8001 | 13 |
| `heading` | 720 | 10 |

8 + 13 + 13 + 10 = **44 bits, 6 bytes**. The relative position is a schema of its own, with
bounds that fit the carrier, not the world. A carrier with `drones: :List(:Drone) max 15` packs
them inline: 4 + 15 × 44 = 664 bits, 83 bytes instead of 90. As a list it is replaced in full by a
delta, so drones that change often are better sent as separate records.

With the 8-bit key and 3 change bits, a delta takes 11 bits before any payload:

| Situation | Bits | Bytes |
| --- | --- | --- |
| At rest relative to the carrier; the caller may send nothing | 8 + 3 = 11 | 2 |
| Flying straight | 11 + 7 + 7 = 25 | 4 |
| Flying and turning | 11 + 7 + 7 + 7 = 32 | 4 |
| `x` through the fallback | 11 + 14 = 25 | 4 |

## Verification

Parity is verified like every GES feature.

- `.gess` syntax changes update the TextMate grammars.
- Markdown Conformance suites cover widths, encodings as exact bytes, decoding, the
  vectors above, the worked examples and these rules explicitly:
  - every `schema.*` error, including `schema.invalidKey`, `schema.invalidUnion`,
    `schema.invalidBound`, `schema.tooLarge` and a parameter with a fractional value as an
    `integer` bound;
  - every `wire.*` error, except `wire.fieldOrder` and `wire.overlappingBuffers`, which need calls
    that the corpus cannot express and are covered by native tests in both ports;
  - `key`: its position, a key read before the rest, and its place in the canonical form;
  - deltas: each payload form, the shorter way of a wrapping field, a key, every rejection of the
    [canonical form](#canonical-delta-form) and more change bits than fit one word;
  - [keyed lists](#keyed-lists): each section, positions and key order, whole-list fallback, nested,
    optional and union places, and the largest encoding of each form;
  - unions: first-match order, `:Quantity(m) | :Quantity(s)` rejected, the index, `:Nothing`,
    lists of unions, a field after a union, and rejection of a document-only alternative with
    `schema.notEncodable`;
  - the document errors `document.missingField`, `document.unknownField`,
    `document.fieldType` and `document.outOfRange`, with their positions.
- `scripts/test-schema-roundtrip.py` exchanges real
  bytes between C# and Swift, with random values, edge values at each bound and half-step
  ties, and compares bytes and decoded binary64 bits in both directions. Negative controls
  qualify the comparison.
- The same script compares the canonical form and both hashes of every schema in the
  corpus, byte for byte between the ports, against the [hash vectors](#hash-vectors) and against
  respelled sources that must not change them: comments, layout, `50kN` and `50000N`.
- A fuzz run feeds random and mutated bytes to the decoders of both ports, which must
  agree on accept or reject, for full records and for deltas. Random deltas also match an
  independent oracle.
- Allocation tests prove that the writer, the reader and `GesSchemaDelta` allocate nothing on
  success in both ports, as for the existing hot paths.
