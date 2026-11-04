<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# GES schemas and wire format

> **Draft** — not implemented and not yet normative, until it is accepted and
> implemented.

This document owns GES schemas: `.gess` files that describe data formats, and the
compact binary encoding derived from them. It is written for server-driven
multiplayer games, where a Swift server sends many small objects to C#/Unity
clients about 20 times per second per client. The same schemas check
[GES documents](Documents.md); there is one schema language.

The encoding is independent of `.gesb`, of [product JSON](MessageFormat.md) and of
the script language. `.gess` is a small language of its own in record notation.
Scripts do not change, and a schema is not a script record.

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

A schema source uses the [lexical rules](Syntax.md) of GES: line comments, the
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
  x: :Quantity(m) between 0m and worldWidth step 1cm delta within 1.27m
  y: :Quantity(m) between 0m and worldHeight step 1cm delta within 1.27m
  heading: :Quantity(°) wrapping between 0° and 360° step 0.01° delta within 1.27°
  turretBearing: :Quantity(°) wrapping between 0° and 360° step 0.01° delta within 1.27°
  radarBearing: :Quantity(°) wrapping between 0° and 360° step 0.01° delta within 2.55°
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
field       = LowerName ":" type [ "optional" ]
argument    = [ "_" ] LowerName ":" type [ "optional" ]

type        = ":Boolean"
            | numeric
            | spatial
            | ":Tag" [ tagset ]
            | ":Text" [ "max" integer ]
            | ":List" [ "(" type ")" [ "max" integer ] ]
            | ":" TypeName
            | ":Map" | ":Range" | ":Series" | ":Dice" | ":Message" | ":Handler" | ":Nothing"
numeric     = ( ":Number" | ":Percentage" | quantity ) constraints
quantity    = ":Quantity" "(" unit ")"
spatial     = ( ":Point" | ":Vector" ) [ "(" unit ")" ] [ "{" axis { axis } "}" ]
axis        = ( "x" | "y" | "z" ) constraints
constraints = [ "integer" ] [ range ] [ "step" literal ] [ "delta" "within" literal ]
range       = [ "wrapping" ] "between" bound "and" bound
bound       = literal | "$" ConstantName | LowerName
tagset      = "(" "#" TagName { "," "#" TagName } ")"
```

Fields and axes are separated by commas or line breaks. Message names and tags
follow the [portable grammars](Semantics/Text.md#portable-name-grammars). `unit`
accepts what [`:Quantity(unit)`](Language.md#records-and-custom-types) accepts.

### Rules

- Bounds and steps are literals, declared constants or parameters. A bound cannot
  refer to a field. No expression is evaluated, so reading and binding need neither
  a compiler nor a VM.
- `between a and b` is the closed range `[a, b]`; `wrapping between a and b`
  is the half-open range `[a, b)`. The lower bound must be below the upper bound, and
  both are finite. Nothing is clamped: a value outside a closed range is an error.
- A literal's unit must equal the field's unit. A type names the base unit, and a
  [prefixed spelling](Language.md#quantities) is literal sugar only, as in scripts:
  `:Quantity(N) between 0kN and 50kN step 0.01kN`, never `:Quantity(kN)`. Prefixes scale
  exactly, and no other conversion exists.
- `integer` excludes `step`: an integer field is exact and a fractional value is an
  error. `step` selects a [quantized field](#quantization) and requires a range.
- `delta within` requires a range and, unless `integer`, a step. See [Deltas](#deltas).
- Omitted axes of a spatial block are not transmitted and must be zero.
- A schema reference names a `schema`; references must not form a cycle. Nesting is
  limited to 32 levels.
- There is no `computed by`. A receiver derives such values in its own code; see
  [Records](#record-values).
- Constants and parameters are visible only in the file that declares them. Schema,
  message and message set names are visible across all sources.
- A position relative to something else, such as a carrier, is a schema of its own
  with its own bounds.
- Names are unique within their kind: fields within a schema, arguments within a
  message, axes within a block. A Message's labels and their order are part of its
  signature, as everywhere in GES.

### Encodable and document-only types

Every type is valid in a schema. Only a type with a known width or a hard limit can
be encoded; binding a schema that reaches another type for encoding fails with
`schema.notEncodable`. Document checking accepts all of them.

| Type | Encodable | Document check |
| --- | --- | --- |
| `:Boolean`, numeric, `:Point`, `:Vector` | yes | yes |
| `:Tag(#a, #b)` | yes | yes |
| `:Text max n` | yes | yes |
| `:List(T) max n` with an encodable `T` | yes | yes |
| `:TypeName` with only encodable fields | yes | yes |
| `:Tag`, `:Text`, `:List`, `:List(T)` without a set or `max` | no | yes |
| `:Map`, `:Range`, `:Series`, `:Dice`, `:Message`, `:Handler`, `:Nothing` | no | yes |

### Source errors

Source errors use the phase `schema` with line and column, in the manner of
[document errors](Documents.md#errors):

| Code | Condition |
| --- | --- |
| `schema.syntax` | the source does not match the grammar |
| `schema.duplicateName` | a name repeats within its kind |
| `schema.unknownName` | a type, message, constant or parameter is not declared |
| `schema.cycle` | schema references form a cycle, or nesting exceeds its limit |
| `schema.unitMismatch` | a bound, step or delta range has another unit than its field |
| `schema.invalidRange` | a lower bound is not below its upper bound |
| `schema.invalidStep` | a step is not positive, not exact, or a bound or delta range is no multiple of it |
| `schema.invalidConstraint` | `integer` with `step`, `step` or `delta within` without a range, `delta within` without a step or not narrower than the field |
| `schema.notEncodable` | encoding reaches a document-only type |
| `schema.tooWide` | a quantized field exceeds the [limits](#quantization) |

The last two and `schema.invalidStep` can also arise at binding, when a parameter
determines the range.

## Binding

Binding fixes every parameter that a root schema reaches:

```text
bound = schemas.Bind(root, { worldWidth: 5000m, worldHeight: 5000m })
```

Parameters are never bound per record. A bound schema is immutable, holds every width
and layout, and can be shared by any number of encoders and decoders. Binding needs
neither a compiler nor a VM.

A file can be bound any number of times with different values, also within one
session, and each binding is an independent encoder and decoder. A session can thus
serve two zones of different size at once:

```text
smallZone = schemas.Bind("ZonedPosition", { zoneSize: 500m })
largeZone = schemas.Bind("ZonedPosition", { zoneSize: 4km })
```

The wire does not say which binding a record uses; the caller knows it from its
context, such as the zone of a connection or channel. A binding has its own widths, so
records and [delta baselines](#deltas) belong to the binding that wrote them. Bindings
share nothing mutable and cost nothing per record.

Parameters are supplied by name. When several files reached by a root declare a
parameter of the same name, they must declare the same kind and unit, and share the
value; otherwise binding fails with `schema.duplicateName`.

Each parameter value must have the declared kind and unit. It converts to an exact
decimal: an Int64 directly, a Binary64 through its shortest
[roundtrip text](Semantics/Numbers.md#numeric-text-output-and-roundtrip). Binding
checks that every range is valid and every bound a multiple of its step.
GES sets no world size; only the [limits](#quantization) apply.

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
| `:Tag(#a, #b)`, `:Tag` | `Tag(#a #b)`, `Tag` |
| `:Text max n`, `:Text` | `Text max <n>`, `Text` |
| `:List(T) max n` | `List(<T>) max <n>`; `List(<T>)` and `List` without `max` or `T` |
| `:TypeName`, `:Map`, ... | `TypeName`, `Map`, ... |

A numeric type is followed by its constraints, and any type then by `optional`, in this
order: `integer`, `between <a> <b>` or `wrapping between <a> <b>`, `step <s>`,
`delta <d>`, `optional`. Unit names are the
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
`3cd1ac76fe075c9a`:

```text
gess1
schema Drone
field id Number integer between 0e0 255e0
field x Quantity(meter) between -2e2 2e2 step 5e-2 delta 155e-2
field y Quantity(meter) between -2e2 2e2 step 5e-2 delta 155e-2
field heading Quantity(degree) wrapping between 0e0 36e1 step 5e-1 delta 155e-1
```

The `Bot` of the [source example](#source-language) has the hash `80fc9057d3fe33f8`.
Bound with `worldWidth = worldHeight = 5000m` its binding hash is `1fe8387c11521de4`, with
`worldHeight = 2km` it is `f249f41bc1461c41`:

```text
gess1
parameter worldHeight Quantity(meter)
parameter worldWidth Quantity(meter)
schema Bot
field x Quantity(meter) between 0e0 worldWidth step 1e-2 delta 127e-2
field y Quantity(meter) between 0e0 worldHeight step 1e-2 delta 127e-2
field heading Quantity(degree) wrapping between 0e0 36e1 step 1e-2 delta 127e-2
field turretBearing Quantity(degree) wrapping between 0e0 36e1 step 1e-2 delta 127e-2
field radarBearing Quantity(degree) wrapping between 0e0 36e1 step 1e-2 delta 255e-2
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

### Field encodings

`width(n)` is the smallest `w` with `2^w >= n`, and `width(1)` is 0.

| Type | Encoding |
| --- | --- |
| `:Boolean` | 1 bit |
| `optional` | 1 presence bit, then the value when present; absent is `nothing` |
| `integer` with a range of `n` values | index `value - lower`, `width(n)` bits |
| `integer` without a range | two's complement, 64 bits |
| quantized | [index](#quantization), `width(n)` bits |
| wrapping | [index](#wrapping-values), `width(n)` bits |
| fractional without a step | IEEE 754 bits of the binary64, 64 bits; a range is checked but does not narrow it |
| `:Point`, `:Vector` | declared axes in order x, y, z, each as a numeric; without a block x, y, z as 64-bit binary64 |
| `:Tag(...)` with `n` tags | index in declared order, `width(n)` bits |
| `:Text max n` | UTF-8 byte count, `width(n + 1)` bits, then the bytes |
| `:List(T) max n` | element count, `width(n + 1)` bits, then the elements |
| `:TypeName` | its fields inline |

`max` counts UTF-8 bytes for Text and elements for a List. A range's index never
exceeds `n - 1`; a decoder rejects a larger bit pattern. Units come from the schema.

A fractional field without a step stays at 64 bits, which is a deliberate choice in the
schema; there is no 32-bit form. Flags are one `:Boolean` per flag. A later
`:Flags(#a, #b)` type would look identical on the wire, one bit per flag, and change only
how a program sees them.

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
one. Any finite value is valid, since it denotes a point on the circle:

1. Compute `N` as in [encoding](#encoding), without the range check of step 2.
2. The index is `(N - lowerN) mod n` with a nonnegative remainder.

All of it after step 1 is integer arithmetic. An `integer` field wraps the same way
with `N = value`. A decoder rejects `i >= n` and returns `lowerN + i` in the
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

> **Planned** — specified so that the format needs no rework, implemented later. The
> full encoding above does not change when deltas are added.

`encode(value, baseline)` writes what differs from a baseline. The caller chooses and
confirms baselines; the format assumes nothing about the transport. A delta and a
full record are different operations and are not marked on the wire, so a caller that
mixes them marks them in its own framing.

### Form

A delta of a schema is one change bit per field in declaration order, followed by the
payload of each changed field in that order. Padding follows at the top level as in
[records](#records). Comparison uses wire values: indices, not the original numbers.
A movement smaller than a step is no change and costs nothing.

| Type | Payload of a changed field |
| --- | --- |
| `:Boolean` | none; the change bit toggles the baseline |
| numeric with `delta within` | [range form](#delta-range) |
| other numeric, `:Tag`, `:Text`, `:List` | the full encoding |
| `:Point`, `:Vector` block | the change bits and payloads of its axes |
| `:TypeName` | a nested delta: its own change bits and payloads |
| `optional` | presence bit; when present and the baseline also has a value, the field's delta payload, otherwise its full encoding |

A list is replaced as a whole. Tracking list elements requires identities that the
caller owns.

### Delta range

`delta within D` declares `d = D / step` indices (or `D` for an `integer`), and needs
`width(2d + 1)` to be below the width of the field. The payload is:

- bit `0`, then `delta + d` in `width(2d + 1)` bits, when `|delta| <= d`;
- bit `1`, then the full index otherwise.

`delta` is the signed difference of the indices. For a wrapping field with period `n`
it takes the shorter way: `delta = ((index - base + floor(n / 2)) mod n) - floor(n / 2)`
with a nonnegative remainder, and the decoder computes `(base + delta) mod n`. An
exact half period therefore counts as negative.

### Canonical form

A delta has one valid encoding, so ports stay byte-identical. A decoder rejects a set
change bit for an unchanged value (except a Boolean, where it is the change), a
payload of `bit 1` for a difference within `d`, and a non-wrapping result outside the
range.

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

With `zoneSize = 1000m`, `x` and `y` take 17 bits each, and a full `ZonedPosition`
takes 50 bits, 7 bytes. A move inside a zone takes 2 change bits, 2 nested change
bits and two 9-bit range forms: 22 bits, 3 bytes. A zone change needs nothing special:
`zone` is written in full, and the local coordinates are compared with the baseline as
usual and leave their delta range through the fallback bit, 1 + 17 bits each: 2 + 2 +
16 + 18 + 18 = 56 bits, 7 bytes.

Zones of different size use [one binding each](#binding). A baseline belongs to its
binding, so an object that moves to a zone of another size is sent as a full record of
the new binding.

## Decoding untrusted data

A bound schema knows its largest encoding, so a caller can size buffers and a decoder
can bound its work before reading. Decoders also accept a caller limit on the bytes of
one record. Decoding fails without partial results for:

| Code | Condition |
| --- | --- |
| `wire.truncated` | the data ends before the record does |
| `wire.invalidIndex` | an index is at least `n`, for a range, tag or message set |
| `wire.invalidCount` | a Text or List count exceeds its `max` |
| `wire.invalidText` | Text is not valid UTF-8 |
| `wire.invalidNumber` | NaN or negative zero, or an infinity with a range |
| `wire.invalidPadding` | a padding bit is not zero |
| `wire.invalidDelta` | a delta is not [canonical](#canonical-form) or leaves its range |
| `wire.limitExceeded` | the record exceeds the caller's limit |

Encoding reports to the caller, and writes no partial record that is reported as
complete:

| Code | Condition |
| --- | --- |
| `wire.outOfRange` | a value is outside its range or a quantized value exceeds `2^50` steps |
| `wire.notRepresentable` | an integer field receives a fractional value, or a value does not fit its stored form |
| `wire.missingField` | a field that is not `optional` has no value |
| `wire.typeMismatch` | a value has another kind or unit than its field, or a nonzero omitted axis |
| `wire.unknownTag` | a tag is not in its set |
| `wire.tooLong` | a Text or List exceeds its `max` |
| `wire.bufferTooSmall` | the caller's buffer is too small |
| `wire.fieldOrder` | a typed writer or reader uses a field out of order |

Errors carry the field path but no value from the data, so they can be logged safely.

## Record values

The convenient path converts between `GesValue` and the wire.

- A schema `:Bot` corresponds to a Record with the type name `Bot`. Fields are matched
  by name; a Map works too. Encoding does not convert units.
- Record fields without a schema entry are not transmitted. This covers the computed
  fields of a script record.
- Decoding builds `:Record("Bot", fields)` without a constructor, as in
  [product JSON](MessageFormat.md). A host with a Program may pass the fields to the
  record's constructor, which derives its computed fields on the normal VM.
- A Message converts in the same way, with its signature and tags.

## Document checking

`.gesd` documents use the same schemas through their
[schema header](Documents.md#schema-header). Plain reading stays as specified there; a
separate operation receives the schema set and optional parameters.

- `:Schema(:DriveUnit)` names a `schema` of the set. `:Schema('DriveUnit.gess')` names
  a source whose declarations are available and that holds exactly one schema no other
  schema of it refers to, which is the root.
- Errors use document positions: `document.missingField`, `document.unknownField`,
  `document.fieldType`, `document.outOfRange`. Units are not cast implicitly.
- A value outside a closed range is an error. A wrapping field accepts any finite
  value. `max` and `integer` are checked; `step` and `delta within` only concern the
  encoding and are not.
- A bound that uses an unbound parameter is not checked.
- Printing a schema prints `.gess` source; printing a document is unchanged.

## API

### Fast path: variants

The fast path encodes into a caller's buffer and decodes without `GesValue` or boxing.

| Variant | Allocation free | Speed | Needs a build step | Works under IL2CPP | Remarks |
| --- | --- | --- | --- | --- | --- |
| A. Schema-bound writer and reader over field handles | yes | good; one interpreted step per field | no | yes | Same API for all schemas; misuse found at runtime; also carries the dump tool and the value path |
| B. Generated code per schema (`ges schema generate`) | yes | best; constants inlined, typed structs | yes | yes | Compile-time field names; generated code must stay in step with the schema, guarded by the hash |
| C. Field visitor with callbacks | only with care | medium | no | yes | A delegate per field or a protocol witness; awkward with allocation-free decoding |
| D. Code emitted at runtime (`Reflection.Emit`, expression trees) | yes | best | no | **no** | IL2CPP has no runtime code generation; ruled out |

**Recommendation: A now, B later on top of it.** A is needed in any case, has no build
step, and works on every platform, including [Unity](#unity). B then adds typed structs and the highest speed for
the few types that dominate a profile. It shares A's bit primitives and is verified by
the same byte comparison. Whether B is needed is decided by measurement.

### Variant A

Names follow [port conventions](../implementation/PortConventions.md); the concepts are
the same in every port. Errors of this module are its own types (C# `GesSchemaException`
and `GesWireException`, Swift `GesSchemaError` and `GesWireError`), since it has no
dependency on Runtime. In C#:

```csharp
var schemas = GesSchemaSet.Parse(sources);                        // once
var bot = schemas.Bind("Bot", new GesSchemaParameters()           // per session or map
    .With("worldWidth", 5000.0).With("worldHeight", 5000.0));
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

- `GesSchemaSet` holds the parsed sources; `GesBoundSchema` the bound layout.
  `GesBoundSchema` is immutable and can be shared between threads; a writer or reader
  belongs to one caller at a time. No port requires locks or threads.
- Typed operations: `Boolean`, `Integer` (Int64), `Number` (binary64), `Index` (the raw
  wire index, for game code that stores quantized values and skips the conversion),
  `Tag` (index), `Text` (UTF-8 bytes over a borrowed view), `BeginList`/`EndList`,
  `BeginOptional` and `BeginNested`/`EndNested`.
- Fields are used in declaration order. Handles are resolved once; reusing a handle
  costs no lookup.
- `bot.MaxByteCount` is the largest encoding, for sizing buffers.
- Success allocates nothing and boxes nothing. A failure is reported through the normal
  error mechanism of the port and may allocate.
- Deltas add `WriteDelta...` with a baseline value and matching reads, with no change to
  existing calls.

### Value path

For rare data: `GesSchemaValues.Encode(bot, value, buffer)` and
`GesSchemaValues.Decode(bot, bytes)` convert [Records and Messages](#record-values).
Swift uses the idiomatic spelling of the same concepts.

## Tools

Both CLIs gain:

```bash
ges schema check bot.gess
ges schema format bot.gess
ges schema hash bot.gess --root Bot
ges schema canonical bot.gess --root Bot
ges schema dump bot.gess --root Bot --param worldWidth=5000m --param worldHeight=5000m data.bin
```

`dump` makes binary data readable through the schema. It lists each field with path,
bit offset, width, wire index and decoded value, and reports the first error with its
field path and bit offset. `--baseline` reads a delta. `.gess` joins the highlighter and
the editor grammars.

## Modules

- **Syntax** reads `.gess` into its lossless tree and reports source errors, like GES and
  GESD. It has no dependencies, and highlighting comes with it.
- **Schema** (`GameEventScript.Schema`, Swift `GameEventScriptSchema`), a new module
  depending only on Syntax: binding, hash, quantization, writer and reader. A client needs
  only Syntax and Schema, without Runtime, Compiler or VM.
- **Runtime** gains the value path and document checking, depending on Schema.

The module adds no filesystem, thread or async dependency and imports no Foundation.

## Unity

The C# part runs in Unity. It is delivered as a precompiled `netstandard2.1` assembly and
holds to the following, verified against the reference assembly and compiler of Unity
6000.3.

**Profile.** Unity's default API compatibility level is .NET Standard 2.1, and it is the
one with the broadest platform support. The module targets it without further
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
- generated code (variant B) is itself C# 9, since Unity compiles it;
- `unsafe` is not part of the public API, so a project needs no special player setting.

**IL2CPP.** There is no code generation at runtime, which rules out variant D. The
module uses no Reflection, so managed code stripping removes nothing it needs, and no
generic virtual methods over value types. A cast from binary64 to Int64 happens only
after the range check of [encoding](#encoding), because an out-of-range cast has no
defined result in the generated C++. The rounding of [quantization](#why-this-is-exact)
has no multiplication next to an addition, so it needs no compiler setting.

**Without allocations.** On success the writer and reader allocate nothing under Mono and
IL2CPP: field handles are structs, Text is a borrowed UTF-8 view, and no enum, delegate or
closure takes part. A caller can pass `stackalloc` memory, a pooled array or a
`NativeArray<byte>` through `AsSpan()`. The module is not Burst-compatible: a bound schema
is a managed object. Managed worker threads can share it.

## Extension points

These build on this format and are not part of it.

- **Transport** frames top-level records, adds its own type and sequence fields, and
  carries the hash at connection setup. The byte-aligned, reusable records allow
  concatenation into packets.
- **Delta baselines** are selected by acknowledgements in the transport. Receivers that
  acknowledged the same baseline can share one encoded delta.
- **Interest management** decides which records a client receives. It owns object
  identities; a list or a packet then holds only what it selected.
- **Publish/subscribe** maps channels to message sets or schemas.
- **Large data** is split into records of bounded size by a layer above, because every
  record has a hard maximum.

## Worked examples

Bytes are the size of the record alone, without transport framing.

### Bot

Schema [above](#source-language), with `worldWidth = worldHeight = 5000m`.

| Field | Values `n` | Full bits | Delta range | Range form | Fallback |
| --- | --- | --- | --- | --- | --- |
| `x` | 500001 | 19 | ±127 | 1 + 8 | 1 + 19 |
| `y` | 500001 | 19 | ±127 | 1 + 8 | 1 + 19 |
| `heading` | 36000 | 16 | ±127 | 1 + 8 | 1 + 16 |
| `turretBearing` | 36000 | 16 | ±127 | 1 + 8 | 1 + 16 |
| `radarBearing` | 36000 | 16 | ±255 | 1 + 9 | 1 + 16 |
| `health` | 136 | 8 | none | 8 | |
| `firing`, `shielded`, `overheated` | | 3 | | 0 each | |

**Full record:** 19 + 19 + 16 + 16 + 16 + 8 + 3 = **97 bits, 13 bytes**.

**Delta** with 9 change bits:

| Situation | Bits | Bytes |
| --- | --- | --- |
| Unchanged; the caller may send nothing | 9 | 2 |
| Standing, radar turning | 9 + 10 = 19 | 3 |
| Moving, all angles turning | 9 + 9 + 9 + 9 + 9 + 10 = 55 | 7 |
| Same, hit and starting to fire | 55 + 8 + 0 = 63 | 8 |
| Teleport: `x` and `y` through the fallback | 9 + 20 + 20 = 49 | 7 |

100 visible bots at 20 updates per second take 26 000 bytes/s in full and 14 000 bytes/s
as moving deltas, without framing.

### Drone

Positions are relative to a carrier, so a drone escorting a moving carrier changes
nothing on the wire. Adding the carrier's position is the game's business.

```gess
schema :Drone as {
  id: :Number integer between 0 and 255
  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m
  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m
  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°
}
```

| Field | Values `n` | Full bits | Delta range | Range form | Fallback |
| --- | --- | --- | --- | --- | --- |
| `id` | 256 | 8 | none | 8 | |
| `x` | 8001 | 13 | ±31 | 1 + 6 | 1 + 13 |
| `y` | 8001 | 13 | ±31 | 1 + 6 | 1 + 13 |
| `heading` | 720 | 10 | ±31 | 1 + 6 | 1 + 10 |

**Full record:** 8 + 13 + 13 + 10 = **44 bits, 6 bytes**.

**Delta** with 4 change bits:

| Situation | Bits | Bytes |
| --- | --- | --- |
| At rest relative to the carrier; the caller may send nothing | 4 | 1 |
| Flying straight | 4 + 7 + 7 = 18 | 3 |
| Flying and turning | 4 + 7 + 7 + 7 = 25 | 4 |
| `x` through the fallback | 4 + 14 = 18 | 3 |

The relative position is such a schema of its own, with bounds that fit the carrier, not
the world. A carrier with `drones: :List(:Drone) max 15` packs them inline: 4 + 15 × 44 = 664 bits,
83 bytes instead of 90. As a list it is replaced in full by a delta, so drones that change
often are better sent as separate records.

## Verification

Parity is verified like every GES feature.

- `.gess` syntax changes update the TextMate grammars.
- Markdown Conformance suites cover source errors, widths, encodings as exact bytes,
  decoding, the vectors above, every `wire.*` error and the worked examples.
- A cross-language script such as `scripts/test-message-json-roundtrip.py` exchanges real
  bytes between C# and Swift, with random values, edge values at each bound and half-step
  ties, and compares bytes and decoded binary64 bits in both directions. Negative controls
  qualify the comparison.
- The same script compares the canonical form and both hashes of every schema in the
  corpus, byte for byte between the ports, against the [hash vectors](#hash-vectors) and against
  respelled sources that must not change them: comments, layout, `50kN` and `50000N`.
- A real Unity project runs the byte and vector suites in an IL2CPP player on arm64 and x64
  and under Mono in the editor, and measures allocations. This is a manual gate, because CI
  has no Unity.
- A fuzz run feeds random and mutated bytes to the decoders of both ports, which must
  agree on accept or reject.
- Allocation tests prove that the writer and reader allocate nothing on success in both
  ports, as for the existing hot paths; performance references follow by measurement.
