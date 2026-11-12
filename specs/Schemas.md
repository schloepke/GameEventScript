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
source      = { constant | parameter | tagsdecl | "abstract" schema | [ roles ] ( schema | message | messages ) }
tagsdecl    = "tags" ":" TypeName "as" tagset
roles       = role [ role ]
role        = ( "wire" | "model" ) [ "(" LowerName ")" ]
constant    = "constant" "$" ConstantName "be" literal
parameter   = "parameter" LowerName ":" ( ":Number" | ":Percentage" | quantity )
schema      = "schema" ":" TypeName "as" "{" { field | let | with } "}"
with        = "with" ":" TypeName [ "all" "optional" ] [ "without" "(" LowerName { "," LowerName } ")" ]
let         = "let" LowerName "be" ( literal | "$" ConstantName )
message     = "message" MessageName "(" [ argument { "," argument } ] ")" [ "tags" tagset ]
messages    = "messages" ":" TypeName "as" "(" MessageName { "," MessageName } ")"
field       = LowerName ":" union [ "optional" | "key" ] [ "default" fallback | "computed" "by" expression ]
argument    = [ "_" ] LowerName ":" union [ "optional" ]

union       = alternative { "|" alternative }
alternative = [ LowerName ":" ] type
type        = ":Boolean"
            | numeric
            | spatial
            | ":Tag" [ tagset | "max" maximum ]
            | ":Text" [ "max" maximum ]
            | ":Bytes" [ "max" maximum ]
            | ":List" [ "(" union ")" [ "max" maximum ] [ "depth" depth ] ]
            | ":" TypeName
            | ":Dice" [ "max" maximum ] constraints
            | ":Map" | ":Range" | ":Series" | ":Message" | ":Handler" | ":Nothing"
numeric     = ( ":Number" | ":Percentage" | quantity ) constraints
quantity    = ":Quantity" "(" unit ")"
spatial     = ( ":Point" | ":Vector" | ":Box" ) [ "(" unit ")" ] [ "{" axis { axis } "}" ]
            | ":Poly" [ "(" unit ")" ] [ "max" maximum ] [ "{" axis { axis } "}" ]
axis        = ( "x" | "y" | "z" ) constraints
constraints = [ "integer" ] [ range ] [ "step" value ] [ "delta" "within" value ]
range       = [ "wrapping" ] "between" value "and" value
value       = literal | "$" ConstantName | LowerName
maximum     = integer | LowerName
fallback    = "true" | "false" | "#" TagName | value
tagset      = "(" "#" TagName { "," "#" TagName } ")"

expression  = fallen [ "when" fallen { "," fallen "when" fallen } [ "," ] "otherwise" fallen ]
fallen      = disjunction { "default" disjunction }
disjunction = conjunction { "or" conjunction }
conjunction = equality { "and" equality }
equality    = relation { ( "=" | "<>" ) relation }
relation    = sum { ( "<" | "<=" | ">" | ">=" ) sum | "has" "value" }
sum         = product { ( "+" | "-" ) product }
product     = unary { ( "*" | "/" ) unary }
unary       = ( "-" | "not" | "abs" ) unary | postfix
postfix     = primary { "." LowerName | "[" ":" "count" "]" | "[" ":" "sum" [ LowerName "=>" expression ] "]" }
primary     = literal | "true" | "false" | "$" ConstantName | LowerName | "(" expression ")"
            | ( "min" | "max" ) "of" sum "and" sum { "and" sum }
            | "clamp" sum "between" sum "and" sum
```

`LowerName`, `ConstantName`, `TypeName`, `MessageName` and `TagName` follow the
[portable grammars](Semantics/Text.md#portable-name-grammars). `unit` accepts what
[`:Quantity(unit)`](Language.md#records-and-custom-types) accepts. `literal` is one exact
finite number of the [number grammar](Semantics/Numbers.md#text-and-number-conversion), with an
optional `%` or unit suffix; a Text, a constant or a parameter is no literal. `integer` is
at most 12 decimal digits without a leading zero; a single `_` may separate two digits, as in `max 10_000`. `depth` is
1 to 3 decimal digits without a leading zero, and a [recursive List](#recursion) needs at least 2. A
`LowerName` in a `value` or `maximum` names a `let` of the schema or, only in a range, a parameter of the file; in a
`fallback` it names a `let` or a sibling field. An [expression](#computed-fields) has its own names.

**Layout.** Spaces and tabs may separate any two tokens, and a line comment may end any
line. A source may start with a BOM. Fields, lets and axes are separated by commas or line breaks,
and repeated or trailing separators are allowed. Arguments, members and tags are separated by
exactly one comma. A line break is allowed only in these places: between declarations, after
`as`, inside the parentheses of an argument list, a member list, a tag set or a `:List`, inside
the braces of a schema or an axis block, and after `|`. Each field, with its type and marker,
otherwise stays on one line. A role is written without spaces, as in `wire(world)`, and stands on the line of its
declaration.

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
- **`let name be value`** inside a schema names one value of that schema, a literal or a file constant. Its literal
  gives it its kind and unit: `2s` is a `:Quantity(s)`, `50%` a Percentage, `16` a Number. A whole Number within Int64
  is an integer; any other value is a binary64 in base units and must stay finite and, unless zero, non-zero there, or it
  is `schema.tooWide`. The fields of its schema, and only they, may name it as a bound, step, delta range or `max`, where
  it is replaced by its value; a `max` takes a positive whole Number of at most 12 digits, like a written one. A let is
  never encoded and is no field of a value or document, but it belongs to the [canonical form](#canonical-form) and the
  hash, since both sides of a connection rely on it, and [generated code](#tools) declares it as a static member of the
  schema's types. Its name differs from the fields and lets of its schema and from the parameters of its file, since a
  bound may name either, or it is `schema.duplicateName`. *Since: Unreleased.*

  A let is one value, not an expression: binding and reading evaluate nothing, and arithmetic between lets, such as
  `peakPower * 2s`, would need an expression language for a single convenience; its result is written instead. File
  constants and parameters are not generated, so a bound stays private to its file unless a schema states it as a let.

- **`with :Name`** inside a schema takes the fields of another schema: they are copied where `with` stands, in their
  declared order, with their types, markers and [defaults](#defaults), as if written there. `all optional` makes each
  taken field optional and drops its default, except a union with a `:Nothing` alternative, which already holds an absent
  value; a taken key becomes optional too. `without (a, b)` leaves fields out. Composition is a schema-level thing only:
  the record stays flat on the wire, in values and in documents, and the [canonical form](#canonical-form) and hash list
  the fields as written, so `with` leaves no trace there. A field keeps its own file's constants, parameters and lets,
  and lets are not taken. A taken field named like a field or let of the schema is `schema.duplicateName`, a field
  `without` names that the taken schema lacks `schema.unknownName`, a taken default whose sibling `without` leaves out
  `schema.invalidDefault`, a taken key that does not end up first `schema.invalidKey`, and schemas that take each other
  `schema.cycle`. Only a schema takes fields; a message has no `with`. *Since: Unreleased.*
- **`abstract schema :Name as { ... }`** declares a schema that only lends its fields with `with`, such as the fields many
  unit schemas share. It is no field type, list element, union alternative or root, takes no role, is never bound or
  checked against a document, and is never generated as a type of its own: each of these is `schema.invalidAbstract`, a
  role `schema.invalidRole`. It may take other schemas, abstract or not. It is described like other schemas, with a hash
  over its own fields, but has no hash on the wire: its fields belong to the hash of each schema that takes them. The
  keyword is `abstract` rather than `protocol`, since the ports name what is generated differently and a schema
  describes fields, not behaviour. *Since: Unreleased.*

  `with` is composition within one schema set; taking a schema from another set, such as a mod building on a game's
  contract, would need a version or hash both sides bind and will use another keyword.

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
- **`tags :Name as (#a, #b)`** names a closed tag set, each tag once. A field, argument, list element or union
  alternative of type `:Name` is the `:Tag(#a, #b)` written in place: the same constraints, encoding, values and
  [canonical form](#canonical-form), so the name belongs to no hash and renaming a set changes no bytes. A set
  is no root, and a union cannot hold it beside another Tag. [Generated code](#tools) declares one type per set
  that every field of it uses, instead of one per field. *Since: Unreleased.*

Schemas, messages, message sets and tag sets share one namespace across all sources.

A union alternative may carry a label, as in `units: :List(:Text max 64 | bind: :Loadout_UnitBind)`. Generated code
names the alternative after its label instead of its type (`.bind`, C# `Bind`); the label changes no bytes, values,
paths, canonical form or hash, as alternatives are told apart by the kind of their value. A label stands only in a union
of two or more alternatives and not on `:Nothing`, which has no value, or it is `schema.invalidUnion`; labels and the
type names of unlabeled alternatives, with a label's first letter capitalized, differ within a union, or it is
`schema.duplicateName`. *Since: Unreleased.*

### Roles

> **Since: Unreleased**

A role marks a declaration as a root of a kind of [code generation](#tools), optionally in a named group:

```gess
wire(world) schema :World as { tick: :Number integer between 0 and 1000000, bots: :List(:World_Bot) max 64 }
wire(protocol) messages :GameEvents as (Hit, Died)
model(view) schema :WorldView as { tick: :Number integer, bots: :List(:BotView) }
wire schema :Arena as { size: :Number integer between 1 and 64 }
schema :World_Bot as { id: :Number integer between 0 and 255 key }
```

- **`wire(group)`** makes a schema, message or message set a wire root of the group: code that writes, reads,
  compares, quantizes and deltas its records. **`model(group)`** makes a schema a model root of the group: a plain value
  type that is never encoded. A role without a group belongs to the group `default`. A group is a `LowerName`.
- **Roles belong to roots.** A declaration without a role is a part: it is generated when a root reaches it, through
  fields, union alternatives, list elements or the members of a message set, and takes its form from that root. A part that
  a wire root and a model root both reach is generated in both forms, each in the output of its own generation.
- **A marked declaration can also be a part.** A root of one group that another group's root reaches is generated as a part
  there and is no root of that group, so generating the group pulls in nothing else of the root's own group.
- A declaration takes at most one `wire` and one `model` role, so `wire(world) model(view) schema :Arena` is both. A model
  root is a schema; messages and message sets carry no data model of their own. A role on a constant or a parameter, `model`
  on a message or message set, a word in the place of a role that is neither `wire` nor `model`, and a group that is no
  `LowerName` are `schema.invalidRole`; a second `wire` or `model` role is `schema.duplicateRole`.
- **A wire root must be encodable.** Reading the sources reports `schema.notEncodable` for a wire root that reaches a
  document-only type, as far as it is decided without parameter values: the conditions and the order of
  [binding](#binding) without the bounds, so a `:List` without `max`, a `:Text` without a length or a `:Map` fail at
  `ges schema check`. A bound that depends on a parameter stays a binding error.
- **A model root may use every type** that has a [model type](#model-types), document-only ones included, and needs no
  ranges, steps or parameter values. Its constraints document the values and still serve document checking.
- An abstract schema is no root and takes no role: `schema.invalidRole`.
- **Roles are generation metadata.** They are not part of the [canonical form](#canonical-form), so adding, changing or
  removing a role changes neither the schema hash nor the binding hash, and no wire format changes with them.

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
| `:Box`, with an optional unit and block | an axis-aligned [Box](PublicApi.md#geometry-library) of the corners `lower` and `upper`, each a `:Point` of the unit and block; see [geometry](#geometry) |
| `:Poly max n`, with an optional unit and block | a [Poly](PublicApi.md#geometry-library) of at most `n` points in the plane z = 0; its block names only x and y |
| `:Tag(#a, #b)`, `:Tag max n`, `:Tag` | one tag of the closed set, a tag of at most `n` characters, or any tag |
| `:Flags(#a, #b)` | any subset of 1 to 64 members, as a Map of the members with Boolean values; see [record values](#record-values) |
| `:Text max n`, `:Text` | a text of at most `n` UTF-8 bytes, or any text |
| `:Bytes max n`, `:Bytes` | at most `n` bytes, or any [Bytes](Language.md#bytes) |
| `:List(T) max n`, `:List(T)`, `:List` | at most `n` elements of `T`, any number of them, or any elements |
| `:List(:Self) max n depth d` | a [recursive List](#recursion) of the schema that declares it, at most `d` records deep |
| `:Dice max n between a and b`, `:Dice` | at most `n` rolls, each within the range, or any rolls |
| `:Name` | a Record of the schema `Name` |
| `T \| U` | a value of one of the alternatives; see [rules](#rules) |
| `:Nothing` | `nothing`, which is useful as an alternative |
| `:Map`, `:Range`, `:Series`, `:Message`, `:Handler` | a value of that kind |

A schema, message or message set cannot take the name of a built-in type of this table, nor
`Record`, the language's type of all Records.

### Geometry

> **Since: Unreleased**

`:Box` and `:Poly` hold the shapes of the [geometry library](PublicApi.md#geometry-library), so that a record carries
them as its native types instead of as two `:Point` fields or a `:List(:Point)`.

```gess
schema :Zone as {
  area: :Box(m) {
    x between 0m and 100m step 1cm
    y between 0m and 100m step 1cm
  }
  outline: :Poly(m) max 16 {
    x between 0m and 100m step 1cm
    y between 0m and 100m step 1cm
  }
}
```

- **A Box** is two corners, `lower` and `upper`, each a `:Point` with the unit and the axes of the block, so it
  binds, encodes, deltas and dumps as two nested Points with the paths `area.lower.x` and so on. Its value orders
  the corners on each axis, as creating a `GesBox` from two corners does: a decoder accepts them in any order and gives the
  ordered Box, and writing that Box writes them in order. Requiring order on the wire would need a check across fields in
  every decoder and delta path for data that only a hand-written writer can produce, so the Box orders it instead.
- **A Poly** is a List of Points with the unit and the axes x and y of the block, or x and y as binary64 without one. A
  polygon lies in the plane z = 0, so `z` is not transmitted: a point with another `z` cannot be written
  (`wire.typeMismatch`), and decoding sets it to 0. Like a List, it needs `max` for encoding. Encoding does not check that
  the polygon is [valid](PublicApi.md#geometry-library), since an invalid polygon is a value of its own.
- Their values are the external values `:Box` and `:Poly` of the library with the field's unit; another value or unit is
  `wire.typeMismatch`. A Box or Poly cannot be a key, has no default literal and cannot be read by a computed field.

### Defaults

> **Since: Unreleased**

A schema field may end with **`default`**, the value it takes when a value or document leaves it out.

```gess
schema :DriveUnit as {
  let waypoints be 32
  power: :Quantity(W) between 0W and 5kW default 0W
  maxPower: :Quantity(W) between 0W and 5kW default power
  efficiency: :Percentage between 0% and 100% default 100%
  maxWaypoints: :Number integer between 1 and 1024 default waypoints
  armed: :Boolean default true
  mount: :Tag(#front, #rear) default #front
}
```

- **A literal default** is a number, a file constant or a let of the field's schema for a numeric field, `true` or
  `false` for a `:Boolean`, or a tag for a `:Tag`. It has the field's unit, lies in its range (a wrapping range is
  half-open), on its step, is whole within Int64 for an `integer` field and keeps its meaning in binary64 otherwise, and a
  tag belongs to the set or fits `max`. A bound that a parameter decides checks it at binding. Other types and unions take
  no literal default: their values have no literal in the schema language, and a literal that picks a union alternative by
  its kind would add a rule for a rare case.
- **A sibling default** names another field of the same schema, whose value the field takes, after that field's own
  default; chains resolve in dependency order and must not form a cycle. Both have the same type, constraints included,
  so every value of one is valid for the other without a check on read; only `delta within` may differ, since it constrains
  no value. A type that generated code declares per field, a closed Tag written in place, Flags, a union or a List of
  them, takes no sibling default; a [tag set](#declarations) shares its type, so its fields can. A range that the sibling
  shares only partly is no default but a rule that rejects data, which stays in host code like other such rules.
- **`optional`** with a default only names an optional sibling: the field is absent when the sibling is, and a literal
  would make `optional` meaningless. Both are optional or both are not. A `key` takes no default, since it identifies the
  record. Message arguments take none, since a message holds every argument of its signature.
- **Where it applies:** reading a [value](#record-values) or a model's value, [checking a document](#document-checking),
  and generated model initializers. The wire always carries the field without a presence bit, since the sender holds the
  resolved value, and decoding gives every field. `nothing` given for a required field stays `wire.nothingValue`: only
  a field that is left out takes its default.
- A default belongs to the [canonical form](#canonical-form) and the hash, since both sides read values and documents
  alike. Only sibling fields, literals and lets serve as defaults, not [computed fields](#computed-fields) or
  expressions: a default is a value the sender left out and resolves to a field that the wire carries.

### Computed fields

> **Since: Unreleased**

A schema field may be written **`computed by`** an expression, as a field of a [script record](Language.md#records-and-custom-types)
is: a value derived from the other fields, which is never encoded and never read from a document. Rules that both a server
and its clients need, in different languages, are written once in the schema.

```gess
schema :SoundMade as {
  energy: :Quantity(J) between 0J and 1MJ step 1J
  duration: :Quantity(s) between 0s and 60s step 1ms
  sources: :List(:SoundSource) max 8
  power: :Quantity(W) computed by
    0W when duration <= 0s,
    otherwise energy / duration
  signature: :Quantity(W) computed by sources[:sum s => s.power]
  loud: :Boolean computed by power > 50W and not (duration = 0s)
}
```

- **The expression** is a subset of GES expressions with their spellings and precedence, so the same text means the same
  in a script record: number, percentage and quantity literals, `true`, `false`, file constants, lets, the fields of the
  record and of its schema fields (`main.power`), the computed fields declared before it, `+ - * /`, unary `-`, `abs`,
  `< <= > >= = <>`, `and`, `or`, `not`, `value when condition, …, otherwise fallback`, `min of a and b …`,
  `max of …`, `clamp x between a and b`, `default`, `has value`, and over a list field `[:count]`, `[:sum]` and
  `[:sum item => value]`. A line break may follow an operator, a keyword, a comma or `by`. Names that GES reserves, such as
  `e`, `pi`, `abs` or `on`, cannot be named, since a script record would read them differently.
- **Types and units are checked at binding.** Each operand is a Number with a unit, a Percentage or a Boolean, and the
  operations take the [same-unit and derivation rules](Language.md#quantities): `energy / duration` gives watts, `2N + 3J`
  is `schema.unitMismatch`. A percentage scales a number on its left (`x + 10%` is `x * 1.1`) or combines with another
  percentage; on the left of a number it is `schema.invalidExpression`. The expression gives exactly the field's type and
  unit. The field is a `:Number`, `:Quantity`, `:Percentage` or `:Boolean` without constraints, never a `key`, and takes no
  `default`.
- **Values follow the virtual machine.** Integers stay exact Int64 values and fall back to binary64 on overflow, a binary64
  result that is whole within Int64 becomes an integer, `/` divides in binary64, `0 / 0` is nothing and `1 / 0` infinite,
  `=` compares within two ULPs, `and` and `or` are three-valued, an absent optional field is nothing, and a condition passes
  only when it is true. The value is then converted to the field's type as a record converts it: a Percentage takes a
  finite number as its ratio and is nothing when infinite, and a Boolean is `true` only for true, so a computed Boolean is
  never absent and is not `optional`. A required one that gives nothing, such as `0 / 0`, reads as NaN in generated code.
- **An absent field is handled where it is read.** A required computed field that would give nothing because an optional
  field it reads is absent is `schema.computedMayBeAbsent`: mark it `optional`, guard the read with `has value`, or give a
  fallback with `default`. `x has value` proves `x` and each record on its path present on the right of `and` and in the
  value its `when` condition selects, so `x * 2 when x has value, otherwise 0` and `x has value and x > 1` are required
  fields; a guard on another field proves nothing. NaN, such as `0 / 0`, is no absence and stays allowed. A computed Boolean
  is never optional, so it guards or falls back. `has value` of a field that is never absent is allowed and simply true;
  schemas report errors, not notices, so it gives none.
- **A sum adds from left to right**, starting with its first element; an empty list gives the integer 0, which the field's
  type gives its unit. That 0 has no unit, so a sum of quantities or percentages stands alone, as the expression, a value of
  a choice or a side of `default`; inside arithmetic it is `schema.invalidExpression`. A sum or count reads a list that is
  no optional field and lies in none, and a projection contains no other aggregate.
- **Order.** A computed field reads the stored fields and the computed fields declared before it, so there is no cycle;
  naming a later one is `schema.invalidExpression`. A `with` takes the computed fields of its schema where it stands, checked
  against the fields of the schema that takes them, with the lets and constants of where they were written; `without` may
  leave one out, and leaving out a field one reads is `schema.unknownName`. `all optional` makes a taken number optional.
- **Not encoded.** The wire, deltas, the largest encoding and the binding hash ignore computed fields, and a document that
  sets one fails with `document.unknownField`. Decoding a [value](#record-values) adds them, encoding ignores them, and
  they belong to the [canonical form](#canonical-form) and the schema hash, since both sides derive the same values.
- **Not included:** square roots, trigonometry and powers, whose results differ between platform libraries; the index of a
  tag in its set, which a script cannot compute; text; and functions with parameters, such as a bearing at a given time,
  which are a possible next step.

The rules come from the virtual machine, which is the reference: `scripts/test-computed-fields.py` turns every corpus record
with computed fields into a script record and requires both virtual machines to print the bits that the value paths of both
runtimes and the generated code of both languages give.

### Recursion

> **Since: Unreleased**

A schema cannot hold itself, except through one recursive List: `units: :List(:Loadout_Bind) max 16 depth 3` in the
schema `:Loadout_Bind` holds records of that schema, which hold such a List in turn, so that at most `d` records of the
schema nest in each other where `d` is the `depth`. A tree of binds needs one schema instead of one per level.

- **Where.** `depth` stands only on a List that is the single type of a field and whose element is the schema that
  declares the field: alone, or as the one schema alternative of a union whose other alternatives are scalars, as in
  `units: :List(id: :Text max 64 | bind: :Loadout_Bind) max 16 depth 3`, where a document writes a unit as its id alone or
  as a bind. A scalar holds no schema and no elements, so no List, Dice or Poly. Anything else, such as a second schema
  alternative, a second recursive List in a schema, and a depth of 1, which would leave the List always empty, are
  `schema.invalidConstraint` at `depth`. Every other reference cycle stays `schema.cycle`, and a List
  of the schema itself without `depth` is such a cycle. The depth belongs to the [canonical form](#canonical-form) and the
  hash; a model List may have no `max`, but `depth` still follows it.
- **Levels.** Binding unrolls the recursion: the outermost record of the schema is level 1, and each record in the List
  is one level deeper. Below the depth the List is an ordinary List of its `max`. At the depth it holds no record and no
  other alternative either: it is always empty, takes no bits, has no element and counts toward no list nesting. Each level above it nests one list
  further, so [lists nest at most 4 levels](#rules) bounds the depth together with the other Lists around it, and the
  [largest encoding](#largest-encoding) follows from `max` and `depth` as for any List.
- **Deeper data.** A record in the List at the depth is deeper than the depth: writing fails with `wire.tooDeep` at the
  List's path, with the detail `deeper than depth <d>` in its message, such as
  `wire.tooDeep at 'root.units[].units[].units': deeper than depth 3`, and a document check with `document.tooDeep` at
  the List. Data cannot be deeper, since a decoder reads nothing there. In a union element the record at the depth is a
  value that no alternative holds, so the element around it is `document.fieldType`, as in any union.
- **Taken by `with`.** Where [`with`](#declarations) copies a recursive List into another schema, it is an ordinary List
  of the declaring schema there, whose records nest up to the depth below it. The copy counts as a reference to the
  declaring schema, so a schema that holds it inside the declaring one forms a cycle.
- **Keys and deltas.** A recursive List of a schema with a key is a [keyed list](#keyed-lists) at every level but the
  deepest, which holds nothing and never changes. A delta descends through the levels like through any nested record.

Unrolling keeps the wire format: a recursive record is encoded as if each level had its own schema, and readers and
writers need no stack beyond their running lists. In generated code the item type of a union element takes the level of the
record that holds the List, and the schema alternative's nodes follow the union's other alternatives, so that those keep
their offsets at every level. Only self-recursion is supported, because a schema that holds one
recursive List has one level to count, and generated code of all other schemas stays as it is. A recursion through
several schemas or with several Lists would need a level per path, for no use found so far.

### Constraints

In this order, all optional:

- **`integer`** requires whole values, stored exactly. Not for a `:Percentage`.
- **`between a and b`** is a closed range, **`wrapping between a and b`** a circular one; see
  [rules](#rules) and [wrapping values](#wrapping-values).
- **`step s`** quantizes to multiples of `s`; see [quantization](#quantization).
- **`delta within d`** declares an expected change; see [rules](#rules).

Together, the type and the constraints decide a field's [encoding](#field-encodings).

### Rules

- A bound is a literal, a declared constant, a let of its schema or a parameter; it cannot refer to a field.
  A step and a delta range are literals, constants or lets, never parameters. No expression is evaluated, so reading and
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
- A field with a [default](#defaults) may be left out, in a value and in a document, which gives it its default.
- A field with a `:Nothing` alternative may be left out, in a value and in a document, which
  counts as `nothing`. A Message holds every argument of its signature, with `nothing` where one
  is absent, since its labels and their order are its signature.
- A schema reference names a `schema`; references must not form a cycle, except through a
  [recursive List](#recursion). Nesting is
  limited to 32 levels. Lists nest at most 4 levels for encoding, so that a writer and
  a reader keep their running lists without allocating; deeper lists are
  `schema.notEncodable`.
- A [computed field](#computed-fields) is derived from the other fields and is never encoded.
- Constants and parameters are visible only in the file that declares them, and lets only in their schema. Schema,
  message and message set names are visible across all sources.
- A `max` is positive.
- A position relative to something else, such as a carrier, is a schema of its own
  with its own bounds.
- Names are unique: declarations of schemas, messages and sets together, constants and
  parameters per file, fields and lets within a schema, a let and the parameters of its file, arguments within a
  message, axes within a block, tags within a set or Flags, members within a message set. A Message's labels and their order are part of its
  signature, as everywhere in GES.

### Encodable and document-only types

Every type is valid in a schema. Only a type with a known width or a hard limit can
be encoded; binding a schema that reaches another type for encoding fails with
`schema.notEncodable`. Document checking accepts all of them.

| Type | Encodable | Document check |
| --- | --- | --- |
| `:Boolean`, numeric, `:Point`, `:Vector`, `:Box` | yes | yes |
| `:Tag(#a, #b)`, `:Tag max n` | yes | yes |
| `:Flags(#a, #b)` | yes | yes |
| `:Text max n` | yes | yes |
| `:Bytes max n` | yes | yes |
| `:List(T) max n` with an encodable `T` | yes | yes |
| `:Poly max n` | yes | yes |
| `:Dice max n` | yes | yes |
| `:TypeName` with only encodable fields | yes | yes |
| A union of encodable alternatives, such as `:Text max 8 \| :Unit \| :Nothing` | yes | yes |
| `:Tag`, `:Text`, `:Bytes`, `:List`, `:List(T)`, `:Dice`, `:Poly` without a set or `max` | no | yes |
| `:Map`, `:Range`, `:Series`, `:Message`, `:Handler`, `:Nothing` outside a union | no | yes |

### Source errors

Source errors use the phase `schema` with line and column, in the manner of
[document errors](Documents.md#errors):

| Code | Condition |
| --- | --- |
| `schema.syntax` | the source does not match the grammar |
| `schema.duplicateName` | a name repeats within its kind, or a schema, message or message set takes the name of a built-in type or `Record` |
| `schema.unknownName` | a type, message, constant, let or parameter is not declared, a step, delta range or `max` names no let of its schema, or a default names neither a field nor a let |
| `schema.cycle` | schema references form a cycle, references or `:List` types nest more than 32 levels, or sibling defaults form a cycle |
| `schema.unitMismatch` | a bound, step, delta range or default has another unit than its field, a `max` names a let with a unit, or a computed expression combines units that have no result unit or gives another unit than its field |
| `schema.invalidRange` | a lower bound is not below its upper bound |
| `schema.invalidStep` | a step is not positive, not exact, or a bound or delta range is no multiple of it |
| `schema.invalidConstraint` | `integer` with `step` or on a Percentage, a Dice constraint other than a range, a `max` that is not positive, `step` or `delta within` without a range, `delta within` without a step or not narrower than the field, a `max` that names a let other than a positive whole Number of at most 12 digits, a `depth` other than on a field's List of its own schema, a second `depth` in a schema, or a depth of 1 |
| `schema.notEncodable` | encoding reaches a document-only type, also when the sources are read for a [wire root](#roles) |
| `schema.invalidKey` | a `key` breaks the [key rules](#rules) |
| `schema.invalidUnion` | a union alternative repeats a type name or has `delta within`, or a union with `:Nothing` is `optional` |
| `schema.invalidBound` | an `integer` bound is fractional or outside Int64, or a Dice bound is outside `1` to `2^31 - 1` |
| `schema.tooWide` | a quantized field exceeds the [limits](#quantization), a bound or default of a fractional field or a fractional let becomes infinite or zero in binary64, or Flags have more than 64 members |
| `schema.tooLarge` | the [largest encoding](#largest-encoding) exceeds its limit or overflows |
| `schema.invalidRole` | a role stands on a declaration that cannot take it, is no role word, or names an invalid group |
| `schema.duplicateRole` | a declaration has a second `wire` or a second `model` role |
| `schema.invalidAbstract` | an abstract schema is a field type, list element, union alternative, a bound or checked root, or a model root |
| `schema.computedMayBeAbsent` | a required [computed field](#computed-fields) reads an optional field that may be absent without `has value` or `default` |
| `schema.invalidExpression` | a [computed field](#computed-fields) has another type than its expression, constraints, a key or optional Boolean, reads a type or name it cannot (a later computed field, a union, a reserved GES name, an optional list in an aggregate), combines operands without a value, or a sum of quantities or percentages stands inside arithmetic |
| `schema.invalidDefault` | a [default](#defaults) lies outside its field's range or off its step, is fractional for an `integer` field, is no tag of the set or longer than `max`, has another kind than its field, stands on a key, a union or an optional field with a literal, names a sibling of another type or optionality, or names one for a type that generated code declares per field |

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
present, every Text, Bytes, List and Dice at its `max`, and every union as its index and its largest alternative. A schema counts the larger of its largest
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
Comments, layout, file boundaries, declaration order outside the reached declarations,
[roles](#roles) and the spelling of literals do not change it, so `50kN` and `50000N` hash alike. Field
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
let <name> <kind> <number>
field <name> <type>
computed <name> <kind> [optional] <expression>
message <MessageName> [tags <#tag> ...]
argument <name or _ name> <type>
messages <TypeName>
member <MessageName>
```

A schema writes its lets right after its `schema` line, in declared order, so where they stand among the fields does
not matter; `<kind>` is `Number`, `Percentage` or `Quantity(<portable unit name>)`, as for a parameter. Its computed fields
follow its fields, in the order they read each other, with the kind `Boolean` besides; the expression is a prefix form in
parentheses: `(field a b)` for `a.b`, `(item b)` for a projected element, literals as `(number <n>)`, `(percentage <n>)`,
`(quantity <unit> <n>)`, `(true)` and `(false)` with constants and lets replaced by their values, and every operation as
`(<name> <operands>)`: `negate`, `not`, `abs`, `add`, `subtract`, `multiply`, `divide`, `less`, `lessOrEqual`,
`greater`, `greaterOrEqual`, `equal`, `notEqual`, `and`, `or`, `choice` with values and conditions in pairs and the
fallback last, `min`, `max`, `clamp`, `default`, `hasValue`, `sum` with the list and an optional projection, and `count`.
The variable of a projection is not written, so renaming it keeps the hash.

`<type>` is written as:

| Source | Canonical |
| --- | --- |
| `:Boolean` | `Boolean` |
| `:Number`, `:Percentage` | `Number`, `Percentage` |
| `:Quantity(unit)` | `Quantity(<portable unit name>)`; `:Quantity(none)` is `Number` |
| `:Point(unit)`, `:Vector(unit)`, `:Box(unit)` | `Point(<portable unit name>)`, `Vector(...)`, `Box(...)`; no unit or `none` is `Point(none)` |
| `:Poly(unit) max n` | `Poly(<portable unit name>) max <n>`, `Poly(...)` without `max` |
| axis block | after the type, `{ <axis> <constraints> ; ... }` in order x, y, z |
| `:Tag(#a, #b)`, `:Tag max n`, `:Tag` | `Tag(#a #b)`, `Tag max <n>`, `Tag` |
| `:Flags(#a, #b)` | `Flags(#a #b)` |
| `:Text max n`, `:Text` | `Text max <n>`, `Text` |
| `:Bytes max n`, `:Bytes` | `Bytes max <n>`, `Bytes` |
| `:Dice max n between a and b`, `:Dice` | `Dice max <n> between <a> <b>`, each part only when written; `Dice` |
| `:List(T) max n` | `List(<T>) max <n>`; `List(<T>)` and `List` without `max` or `T`; ` depth <d>` follows a [recursive List](#recursion) |
| `:TypeName`, `:Map`, ... | `TypeName`, `Map`, ... |

A numeric type is followed by its constraints, and any field or argument then by its
marker, in this order: `integer`, `between <a> <b>` or `wrapping between <a> <b>`,
`step <s>`, `delta <d>`, then `optional` or `key`, then `default` and a number, `true`, `false`, `#<tag>`
or `field <name>` for a sibling. A union writes its alternatives in
source order with ` | ` between them, also inside `List(...)`; the marker follows the last
one. Unit names are the
[portable names](Language.md#quantities). A bound is a number or a parameter name.

A number is its exact decimal value in base units, written `<significand>e<exponent>`:
the significand is an integer without trailing zeros, with a leading `-` when negative,
and zero is `0e0`. Prefixes and percent signs are applied first, and constants are
replaced by their values, and so are lets where a field names them, also as a default. So `1cm`, `0.01m` and `10mm` are all `1e-2`, and `135` is
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
| `:Box` | the corner `lower`, then `upper`, each as a `:Point` of the block |
| `:Poly max n` | point count, `width(n + 1)` bits, then each point as a `:Point` of the block, x and y without one |
| `:Tag(...)` with `n` tags | index in declared order, `width(n)` bits |
| `:Flags(...)` with `n` members | a mask of `n` bits; bit `i`, counted from the first bit written, is set for the member at index `i` in declared order |
| `:Tag max n` | as `:Text max n`: the tag name without `#`, its byte count, then its bytes |
| `:Text max n` | UTF-8 byte count, `width(n + 1)` bits, then the bytes |
| `:Bytes max n` | as `:Text max n`: the byte count, then the bytes, which need not be UTF-8 |
| `:List(T) max n` | element count, `width(n + 1)` bits, then the elements; at the depth of a [recursive List](#recursion) nothing |
| `:Dice max n` | roll count, `width(n + 1)` bits, then each roll as an `integer` of its range, `1` to `2^31 - 1` without one. The [value path](#value-path) writes them in descending order and a typed writer in the order of its caller; a decoder accepts any order, since creating a Dice sorts its rolls |
| union of `n` alternatives | index in declared order, `width(n)` bits, then the alternative |
| `:Nothing` as an alternative | no bits |
| `:TypeName` | its fields inline |

`max` counts UTF-8 bytes for Text, characters for a Tag, which is ASCII, elements for a List,
points for a Poly and rolls for a Dice. A range's index never
exceeds `n - 1`; a decoder rejects a larger bit pattern. Units come from the schema.

A fractional field without a step stays at 64 bits, which is a deliberate choice in the
schema; there is no 32-bit form. Independent switches are one `:Flags` field, whose members
cost one bit each, and member order belongs to the hash.

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
| other numeric, `:Tag`, `:Text`, `:Bytes`, `:List` of other elements, `:Dice`, `:Poly` | the full encoding |
| `:List` of a schema with a key | a [keyed list](#keyed-lists) |
| `:Point`, `:Vector` with a block | one change bit per declared axis, then the payloads of the changed axes |
| `:Point`, `:Vector` without a block | three change bits, then a raw 64-bit binary64 for each changed axis; bits are compared |
| `:Box` | a nested delta of its corners: two change bits, then the payload of each changed corner as a `:Point` |
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
This needs no syntax, and the full encoding does not change. A [recursive List](#recursion) is keyed at every level but
its deepest. Lists of other elements, including
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
| `wire.invalidCount` | a Text, Bytes, Tag, List or Dice count exceeds its `max` |
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
| `wire.tooLong` | a Text, Bytes, Tag, List or Dice exceeds its `max` |
| `wire.tooDeep` | a [recursive List](#recursion) holds a record at its depth; the message names the depth |
| `wire.bufferTooSmall` | the caller's buffer is too small |
| `wire.fieldOrder` | a typed writer or reader uses a field out of order |
| `wire.keyMismatch` | a record and its baseline have different keys |
| `wire.overlappingBuffers` | the target buffer of `GesSchemaDelta` overlaps an input, in either direction, or a generated record's baseline shares its storage |

Errors carry the field path but no value from the data, so they can be logged safely. `wire.tooDeep` also carries a
detail from the schema, `deeper than depth <d>`, in `GesWireException.Detail` and its message (Swift `GesWireError.detail`);
other errors have an empty detail.

## Record values

The convenient path converts between `GesValue` and the wire.

- A schema `:Bot` corresponds to a Record with the type name `Bot`. Fields are matched
  by name; a Map works too. Encoding does not convert units.
- A field that a Record or Map leaves out takes its [default](#defaults): its literal, or the value of the sibling it
  names, after that sibling's own default. A chain that ends at a field without a value or default is
  `wire.missingField` at that field, and `nothing` for an optional one.
- Record fields without a schema entry are not transmitted. This covers the computed
  fields of a script record and of the schema. The same holds for the keys of a Map, except when it selects a union
  alternative.
- Decoding builds `:Record("Bot", fields)` without a constructor, as in
  [product JSON](MessageFormat.md), and adds the schema's [computed fields](#computed-fields) with the values a script
  record derives. A host with a Program may still pass the fields to a record's constructor.
- A Message converts in the same way, with its signature and tags.
- `:Flags` take a Map whose keys are members: `true` sets a member, `false` or an absent key
  leaves it unset. Another key is `wire.unknownTag`, a value that is no Boolean or a value that
  is no Map `wire.typeMismatch`. Decoding gives a Map of the set members, each `true`, in key
  order, so `[armed:]` from a script's [flag Map](Language.md#maps) round-trips.
- A `:Box` or `:Poly` field takes the library's external value of its unit, and decoding gives one; a Box orders its
  corners.
- A value selects a union alternative by its kind, with unit, or for a Record by its type name, and for a Box or Poly by
  being one; no other kind converts. Since alternatives repeat no type name, a union value has exactly one encoding. A Map
  has no type name: the first schema or Flags alternative that holds it decides. A schema holds a Map
  when all its fields fit and every key is one of its fields, also in the Maps nested in it, as in
  a [document check](#document-checking); Flags hold it when every key is a member with a Boolean.
  When none holds, the error of the last schema is reported.

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
  `document.fieldType`, `document.outOfRange`, and `document.tooDeep` for a non-empty
  [recursive List](#recursion) at its depth. Units are not cast implicitly.
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

- `:Flags` accept a Map whose keys are members with Boolean values; another key is
  `document.fieldType` at the key, a value that is no Boolean at the value.
- A document has no geometry literal, since `parse` resolves no external constructor. A `:Box` is the Map of its corners,
  `[lower: :Point(1m, 0m), upper: :Point(20m, 3m)]`, or a `:Record("Box", ...)` of them: another key is
  `document.unknownField` and a missing corner `document.missingField`. A `:Poly` is the List of its points. Each corner or
  point is a Point of the unit within the axes of the block.
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
  (index), `Flags` (a 64-bit mask whose bit `i` is the member at index `i`; a writer rejects a bit
  beyond the last member with `wire.unknownTag`), `Text` (validated UTF-8, or for a `:Tag max n` a validated tag name without `#`) and `Bytes`
  (any bytes of a `:Bytes max n`). A reader copies Text and Bytes into a caller's buffer of the
  field's `max`, since a value need not start at a byte boundary.
- `BeginList` writes or reads the element count of a List, Dice or Poly, and the elements follow, each entered at
  the `[]` handle. `BeginNested` enters a nested schema, Point, Vector, Box, corner of a Box (`area.lower`) or point of
  a Poly. There is no end call: the cursor knows where a list or nesting ends.
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
  byte count suffices. `GesBitWriter.Counting()` (Swift `GesBitWriter.counting()`) creates a writer without a buffer that
  accepts every write, stores nothing and returns from `Finish` the byte count the same writes need; its
  `RequiredByteCount` (Swift `requiredByteCount`) is the room a buffer needs on the way, which exceeds that count when a
  `Rewind` took bits back, as a delta does for an element that turned out unchanged.
- `GesBitReader` reads such values from untrusted data with an optional byte limit. Reading
  past the end is `wire.truncated`, or `wire.limitExceeded` when the limit ends first, with
  the path the caller names; `ReadBytes` reads nothing when the bytes do not fit. `Require`
  checks remaining bits without reading, `Seek` moves to a position, and `Finish` checks the
  padding (`wire.invalidPadding`) and returns the bytes consumed.
- `GesWireRules` holds the value rules over plain operands: `IsValidUtf8`, `IsTagName`,
  `QuantizedIndex` and `WrappedQuantizedIndex` with the [quantization](#quantization) operands
  `10^k` and `p`, `QuantizedValue` and `WrappedIndex`, and `WriteDeltaRange` and `ReadDeltaRange` for
  the [delta range](#delta-range) of a changed index, including the canonical checks of a decoder.
  Their errors and paths are those of the writer, the reader and `GesSchemaDelta`.
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
  `IsOptional`, `IsKey`, `Alternatives` and `Default`, a `GesSchemaDefaultInfo` with `Kind` (`Number`, `Boolean`, `Tag`
  or `Field`), the exact `Number`, the `Boolean`, and the `Name` of a tag without `#` or of the sibling, or null;
- `IsAbstract`, and `Takes`, what a schema takes with `with` in declared order: each a `GesSchemaTakeInfo` with
  `Schema`, `IsAllOptional`, `Without` and `IsUnchanged`; `Fields` already holds the taken fields in place;
- `Tags` of a message and `Members` of a message set, in declared order;
- `Bind` and `CheckDocument`.

An alternative is a `GesSchemaType` with `Kind`, `Unit`, `IsInteger`, `IsWrapping`, `Lower`,
`Upper`, `Step`, `Delta`, `Tags`, `Max`, `Element`, `Depth`, 0 except for a [recursive List](#recursion), `Axes` and,
for a reference, `Schema`. An axis
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
ges schema layout world.gess panel.gess --root Bot --root GameAnswers --param worldWidth=2000m
ges schema layout world.gess panel.gess --all-roots --param worldWidth=2000m -o wire.layout.json
ges schema generate world.gess --root Bot --param worldWidth=2000m --language csharp --namespace Game.Net -o Bot.g.cs
ges schema generate world.gess --root Bot --param worldWidth=2000m --language swift --namespace Wire -o Bot.swift --check
ges schema generate world.gess panels.gess --wire world --param worldWidth=2000m --language swift --namespace Wire -o Wire.swift
ges schema generate world.gess panels.gess --model view --language csharp --namespace Game.Model -o Model.g.cs
ges schema generate world.gess panels.gess --wire world --param worldWidth=2000m --language swift --namespace Wire -d Generated/Wire
ges schema generate world.gess panels.gess --model view --language swift --namespace Model -d Generated/Model --split file --check
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
is `true` or `false`, a number, a quoted Text, the [Bytes literal](Language.md#bytes) `:Bytes("…")`, `#<tag> index=<i>`, `#<tag>` for a `:Tag max n`, the set members as `#<member>` separated by spaces, or `none`, then
` mask=<mask>` for Flags, `count=<n>` for a List or Dice,
`<alternative> index=<i>` for a union, with the type name of the alternative, or `absent`. A nested
schema, Point, Vector, Box or Box corner has no line of its own, only its fields; a Poly has the line of a List. A message set starts with
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

`layout` prints the [generation layout](#generation-layout) of one or more roots. `--root` may be
repeated; naming a root twice is a usage error. `--all-roots` makes every declaration of the files
a root, in declaration order. `--wire <group>` makes the [wire roots](#roles) of the group in the given files the roots,
in declaration order, and `--model <group>` the model roots, which `layout` then prints as the [model layout](#model-layout)
and `generate` writes as [model types](#model-types). Exactly one of `--root`, `--all-roots`, `--wire` and `--model` is
given, a run names one group, and a group without a root in the files fails with `generate.usage`; wire and model code go to
separate files. A `--param` that no root reaches fails with `schema.unknownName`, and so does a reached parameter without a
value; models reach no parameter, so every `--param` of a `--model` run fails, and `--checked-exclusivity` is a usage error
there.

`generate` takes the same roots and parameters and writes code in the language of `--language`,
`csharp` (C# 9, for Unity) or `swift` (Swift 6, without Foundation); every CLI writes every language,
and both CLIs write identical text. Each reached schema becomes one type that writes and reads its
records over the [bit primitives](#bit-primitives) with the bytes, `wire.*` codes and field paths of
the writer and reader, with parameters as constants. A type without Text, Bytes, List or Dice, directly or
in a type it holds, is a value type (C# `struct`, Swift `Sendable` struct); otherwise it is a class
whose storage grows when a read or a caller needs more room and is then reused, so that reading and
writing allocate nothing once it has held its largest data. A Text is held as UTF-8 bytes with a
string view, Bytes as bytes (C# a span with a `Set<Field>` method, Swift `[UInt8]`), a List or Dice as a C# `List<T>` or Swift array, and a list element that is a union,
Text, Bytes or List as a generated item type. A closed Tag is an enum `<Field>Tag` nested in its record; a Tag of a
[tag set](#declarations) uses the one enum `Name` that the file declares beside its records, whose raw value is the
index. Flags are a `<Field>Flags` type as in [model types](#model-types), whose
raw value is the wire mask. A `:Point` or `:Vector` is the runtime's
[`GesPoint` or `GesVector`](PublicApi.md#spatial-values), and a `:Box` its [`GesBox`](PublicApi.md#geometry-library), in every
field, list element and alternative; quantization stays per field, and reading or quantizing a Box makes a new, ordered
one. A `:Poly` is the List of its Points, which reading reuses, with a view `<Field>Poly` (Swift `<field>Poly`) that
makes the `GesPoly`: an immutable polygon that checks its validity when it is made could not be read without allocating. An axis that a block leaves out is not transmitted: writing a record or a delta whose omitted axis is not zero fails
with `wire.typeMismatch` at the field's path, as the [value path](#value-path) does, and reading sets it to zero. An `optional` field adds a presence flag and, for a value
type, a nullable view. A message adds a Boolean per tag, and a message set holds the selected
member and one record per message. After a failed read the values of the target are unspecified.
A field named like its type, which C# does not allow, gets the suffix `Value` in every language.

A schema with a [recursive List](#recursion) is a class whose List holds records of itself. Its internal methods take the
level of the record as a last parameter, 1 by default, and pass one more to the records in the List; at the depth the
List writes and reads nothing and fails with `wire.tooDeep` and its detail when it holds a record, in writing, counting,
comparing, quantizing and deltas alike. Its field paths list its own fields first and then the deeper levels, so its own
offsets stay constants at every level. No other schema changes: a schema that holds a recursive one calls it as any
nested record, and every schema without one generates the same code as before recursion existed, which
`scripts/compare-schema-generation.py` checks against a recorded digest of every corpus schema's output.

A required list of class elements gets `Set<Field>Count(count)` (Swift `set<Field>Count(_:)`), which sets the list to
`count` elements the record reuses, as a read does, so a caller fills it without allocating once it has grown; the
elements keep earlier values until their fields are set. A list of Text or `:Tag max n` elements also gets
`<Field>Values` (Swift `<field>Values`), its elements as C# `string[]` or Swift `[String]`; reading and setting it
allocate, and setting reuses the elements. A field whose name collides with either fails with `generate.nameCollision`.

A record writes into a caller's buffer with `Write` (Swift `write(into:)`), for which `MaxByteCount` always suffices, and
into a new array of its exact size with `ToBytes` (Swift `encoded()`). `ByteCount` (Swift `encodedByteCount()`) returns
that exact size without writing, with the same `wire.*` failures; `ToBytes` uses it, so it never allocates the largest
encoding. A record of `:Bytes max 4194304` that holds a kilobyte allocates a kilobyte. Deltas offer the same with
`DeltaByteCount` and `ToDeltaBytes` (Swift `deltaEncodedByteCount(against:)` and `deltaEncoded(against:)`). Counting
runs the write over a counting [`GesBitWriter`](#bit-primitives) and skips the bytes of a Text or Bytes at once, so a full
write costs about one and a half writes and a sparse large record a fraction of the old allocation. A delta writer tries
elements and takes unchanged ones back, so `ToDeltaBytes` sizes its buffer by the counting writer's `RequiredByteCount`
and returns the exact bytes.

Every generated record compares by wire values with `WireEquals`, the indices and stored bits
that it writes, never raw numbers; `Quantize` replaces each quantized number by the value its index
decodes to, and a class copies another record into its storage with `CopyFrom` (Swift
`copy(from:)`). A schema writes a delta against a baseline with `WriteDelta` and reads one with
`ReadDelta`, with the bytes, codes and paths of [`GesSchemaDelta`](#deltas-between-records); the
baseline is never changed, and a target whose read failed must not serve as a baseline. A read
overwrites the target's storage: the record, the class records it holds and the elements its lists
reuse. The baseline must share none of it. Before it reads, `ReadDelta` rejects with
`wire.overlappingBuffers` and an empty path a baseline that is the target and, in checking builds,
one that holds any record of that storage or, in C#, any list of it. Checking builds define `DEBUG`
or `GES_CHECK_SHARING`, and in C# also Unity's `DEVELOPMENT_BUILD` or `UNITY_EDITOR`; other builds
skip that walk for speed, and a shared baseline gives unspecified values there. `CopyFrom` from the
record itself, or in C# from a record with the same list in the same place, keeps its values, and so
does an unchanged list of that kind in `ReadDelta`; `CopyFrom` checks nothing else, so the source
must not hold another record of the target's storage. Lists of keyed elements take the [keyed-list](#keyed-lists) form, like
every delta.
With `--values`, a schema also converts to and from a GES value with `ToValue` and `ReadValue` (Swift
`toValue()` and `read(value:)`), and a message or message set to and from a message with `ToMessage`
and `ReadMessage` (Swift `toMessage()` and `read(message:)`). The conversion goes through the record's
bytes and the [value path](#value-path), so it gives exactly its values, codes and paths; for that the
code embeds the schema sources under their file names and binds them once per type with the
generation's parameters, and a binding hash that differs from the generated one fails the conversion. It
allocates and serves values from scripts, not the hot path. A Swift class marks its stored properties
`@exclusivity(unchecked)`, since the dynamic exclusivity checks would take about half the time of
reading and writing; a caller must therefore not access one record while another access to it is
under way, such as passing a list of it `inout` while the record reads into it, which is undefined
instead of a trap. `--checked-exclusivity` keeps the checks. Bounds and the end of buffers and data stay
checked either way, and classes are not `Sendable`, so Swift 6 does not let threads share them. `--namespace` puts the types into a C#
namespace or nested Swift enums, and `--access internal` hides them from other modules. With
`--check`, `generate` compares its text with the file of `-o` and fails with `cli.outdated` when it
differs or is missing. The text starts with a header that names the roots, parameters and hashes,
without a date or version. A generation request that cannot be carried out fails with a
`generate.*` code: `generate.nameCollision` when two generated names of one type are equal (in Swift a
`:Flags` member `#rawValue` meets the mask of its `OptionSet`) or two [split](#split-output) file names differ only in
case, `generate.invalidNamespace`, and `generate.usage` when the [Generator API](PublicApi.md#schema-code-generation)
receives no root or a split that does not fit the call.

### Split output

> **Since: Unreleased**

`-o` writes one file, and without it `generate` prints that file. `-d <dir>` (`--out-dir`) instead writes several files
into a directory that `generate` owns, which compiles on several cores and turns a schema change into a diff of the files
it touches. `--split schema`, the default, writes one file per declaration: a schema, message or message set with its
nested types (list items, closed tags, Flags and union alternatives), a [tag set](#declarations), and for models the
protocol of an [abstract schema](#declarations) in a file of its own, while the protocol of another schema stands in that
schema's file. `--split file` writes one file per `.gess` source with every type it declares. A type always goes into the
file of its declaration, so a type that another source reaches is never duplicated. `-o` together with `-d`, and `--split`
without `-d`, fail with `generate.usage`.

A file is named after its declaration or after its source file without `.gess`, with the namespace in front:
`Wire.World_Bot.swift`, `Wire.world.swift`, `Game.Net.World_Bot.g.cs`. Swift requires distinct file names within a
module, and wire and model code of one target live in different namespaces, so the namespace keeps their files apart.
Two file names that differ only in ASCII case, which case-insensitive file systems cannot hold, fail with
`generate.nameCollision`, and so do two sources of the same file name in a file split. The shared part goes into a file
named `_` and the namespace (`_Wire.swift`), or `_Generated` without a namespace: the Swift namespace enums, the embedded
schema sources of `--values`, the conversions and helpers of models and the `GesTypes` catalog of `--bindings`. A split file
declares its types in `extension Wire { … }` (C# `namespace Wire { … }`), and helpers that a single file keeps `private`
are internal there and keep their `__` prefix; their names depend only on the namespace, so adding a root or changing a
schema leaves the other files unchanged. A Swift shared file without content is omitted when there is no namespace to
declare, and so is a C# one. Each split file starts with a short header that refers to the manifest instead of naming
roots and hashes, so that it changes only with its own declarations; the single file of `-o` keeps its header.

The directory holds the manifest `.ges-generated.json`, canonical JSON with the members `manifest` (1), `code` (`wire` or
`model`), `language`, `split` (`schema` or `file`), `namespace`, `access`, `values`, `bindings`, `documents` only when given, `checkedExclusivity`,
`roots` (each with `name`, `hash` and for wire code `bindingHash`), `parameters` (as in the
[generation layout](#generation-layout); empty for models) and `files`, the generated file names in ordinal order without
the manifest. Without `--check`, `generate` writes every file whose bytes changed, keeps unchanged files with their time
stamps, removes the files that the previous manifest lists and that are no longer generated, and leaves every other file
alone with the warning `cli.unowned`; a missing or unreadable manifest owns nothing. With `--check` it compares the whole
directory and fails with `cli.outdated` for every changed, missing and surplus file. Names that begin with `.` and Unity's
`.meta` files are neither reported nor removed, so editors and engines may keep their files beside the code; SwiftPM and
Unity both ignore the hidden manifest. Every CLI writes identical files and manifests.

### Model types

> **Since: Unreleased**

`generate --model` writes model types: plain values that a program builds, keeps and compares, such as a client's view of
the world, and that never travel. They have no reading, writing, deltas, quantization or storage reuse, so a model schema
needs no ranges, steps or parameters, and every reached schema becomes one type, in name order.

| Schema | Swift | C# |
| --- | --- | --- |
| schema | `struct`, `Hashable`, `Sendable`, `Codable` | `sealed class` with value equality, `IEquatable<T>`, `==` |
| `:Boolean` | `Bool` | `bool` |
| `:Number`, `:Quantity(unit)` | `Double` | `double` |
| `integer` | `Int` | `long` |
| `:Percentage` | `Double`, the ratio as in wire code | `double` |
| `:Text`, `:Tag` without a set | `String` | `string` |
| `:Bytes` | `[UInt8]` | `byte[]` |
| `:Tag(#a, #b)` | an enum `<Field>Tag` with `String` raw values, `CaseIterable` | an enum `<Field>Tag` |
| `:Name` of a [tag set](#declarations) | one enum `Name` beside the records, as above, for every field of the set | one enum `Name` beside the records |
| `:Flags(#a, #b)` | an `OptionSet` `<Field>Flags` over `UInt64`, one static member per member | a `[Flags]` enum `<Field>Flags` over `ulong`, one value per member |
| `:List(T)` | `[T]` | `List<T>` |
| `:Dice` | `[Int]`, the rolls | `List<int>` |
| `:Point`, `:Vector` | `GesPoint`, `GesVector` | `GesPoint`, `GesVector` |
| `:Box`, `:Poly` | `GesBox`, `GesPoly` | `GesBox`, `GesPoly` |
| `:Name` | the model type `Name` | the model type `Name` |
| union | an enum `<Field>Alternative` with one case and value per alternative | an abstract class `<Field>Alternative` with one sealed subclass per alternative, named after it, holding `Value` |
| `optional`, `:Nothing` alternative | `Optional`; `nil` is absent or `nothing` | nullable; `null` is absent or `nothing` |

A union with a `:Nothing` alternative becomes a nullable value without it, and when one alternative remains, that
alternative's type. A list element type is named `<Field>Item`, so a closed Tag in a list is `<Field>ItemTag`. `:Map`,
`:Range`, `:Series`, `:Message`, `:Handler` and a `:List` without an element type have no model type yet:
`generate.unsupported`. Each property is documented with its declaration, so its unit and documented range stay visible.

- **Swift** stores `var` properties in declared order and has a memberwise `init` whose absent values default to `nil` and
  lists to `[]`. A field with a literal [default](#defaults) has it as its default argument; one with a sibling default takes
  an optional argument that defaults to `nil` and resolves `nil` to the sibling's property, since Swift cannot default one
  argument to another. A schema with a `key` is `Identifiable`; the key is `id`, or a computed `id` returns it. `Codable` is part
  of the standard library, so models still import no Foundation; `GesPoint` and `GesVector` are `Codable` with `x`, `y`,
  `z`. With `--bindings` or `--values`, one internal helper enum named after the header holds the value conversions both
  share.
- **C#** has settable properties in declared order, a constructor with every field and a parameterless one that leaves every
  field at its default or zero value (empty text and lists, new records, the first alternative with its zero value). A
  sibling default takes a nullable parameter that resolves `null` to the sibling's property. C# optional parameters end
  the list, so a default becomes one, `= value` or `= null`, only for the fields after the last field without a default. It uses C# 9 with
  neither `record`, `init` accessors nor Reflection, so Unity and IL2CPP need no shim. Classes keep a copy from sharing a
  list with its original; value equality compares lists element by element, and 0 and -0 hash alike. A file holds one
  internal helper class named after its header; it also holds the value conversions that `--bindings` and `--values`
  share, so a file with both options holds them once.
- Every model type carries `SchemaHash` (Swift `static let schemaHash`), the [schema hash](#schema-hash) of its schema,
  which a wire record of the same schema carries too, so a test notices generated code that a schema change left stale.
  A field that would take its name fails with `generate.nameCollision`.
- Every [let](#declarations) of a schema is a static member of its model and wire types, in base units with a Percentage
  as its ratio: C# `public const long` for an integer and `public const double` otherwise, named in PascalCase; Swift
  `static let` of type `Int` in models, `Int64` in wire records and `Double` otherwise. A member that would take the name
  of a generated member or another member fails with `generate.nameCollision`. *Since: Unreleased.*
- **Composition.** A schema that types take unchanged with `with`, without `all optional` or `without`, becomes a
  protocol (Swift) or interface (C#) with read-only properties, and every model type that takes it adopts it; a plain
  schema that is taken also adopts its own. An abstract schema is generated only as this protocol. Swift names it after
  an abstract schema, since no type takes the name, and `<Name>Fields` for a plain schema beside its struct; C# names it
  `I<Name>`. It refines the protocols of the schemas it takes unchanged and requires its other fields whose type is shared
  by name: every type except a union, Flags and a closed Tag written in place, which generated code declares per field; a
  [tag set](#declarations) shares its type. `all optional` and `without` change the fields' shape, so such a taking adopts
  nothing. A protocol named like a type or tag set of the file is `generate.nameCollision`. Wire records adopt no
  protocol: their fields are storage that a read reuses, with presence flags and UTF-8 text, so a shared read-only
  property would need an allocating view; a program reads shared fields on models. *Since: Unreleased.*
- **Computed fields.** A [computed field](#computed-fields) is a read-only property of models and wire records alike,
  derived on each read: `Double` (C# `double`) for a number or percentage ratio, `Double?` when it is `optional`, and
  `Bool` for a Boolean; a required number that gives nothing reads as NaN. Binding knows each operand's kind, unit,
  optionality and declared range, so a field is one expression in the language's own operators, such as
  `var power: Double { duration <= 0.0 ? 0.0 : energy / duration }`, with every unit and percentage decision made when the
  code is generated. It gives the virtual machine's bits because binary64 `+ - * /` round correctly and the evaluation order
  is kept: NaN stands for nothing, an optional field reads as NaN where it is absent (or directly where `has value` proves
  it present), and a Boolean that may be nothing is an optional Bool only where `not`, `=`, `has value` or `default` tells
  nothing from false. The few rules plain code lacks are helpers of one type `GesComputed` per generation (with the
  generation's suffix without a namespace): no negative zero, where binding cannot exclude one; the equality of GES numbers
  (whole numbers exactly, others within two ULPs); `min`, `max` and `clamp`, which give nothing for nothing; three-valued
  `and`, `or` and comparisons; and the conversions to optional fields and ratios. Each sum is a private method with a loop.
  The virtual machine adds and multiplies whole numbers as exact Int64 values; where the declared ranges cannot exclude such
  a result beyond 2^53 meeting another operand, as for an `integer` field without a range, the field keeps the dynamic
  form: a property over an internal method `__<field>()` that returns the exact GES value through the helpers that repeat
  the virtual machine. The bits hold for the values a record can hold on the wire; a value outside its declared range, which
  no encoding carries, may differ in the last bit of a chain of whole numbers beyond 2^53 or in the sign of a zero. The
  checked-in code of `conformance/fixtures/SchemaGeneration/computed.gess` pins both forms. A computed field is no initializer
  parameter, is not stored, encoded or part of equality and `Codable`, and its name must not collide with a generated
  member, such as the presence flag `hasMain` of a field `main`. With `--values` a model's value carries it as decoding a
  record's bytes does, and reading ignores it; `--bindings` offers it to scripts as a field they read but cannot set.
  *Since: Unreleased.*
- A model is a plain value: an optional field that defaults to a sibling holds what it is given, and reading a value that
  gives `nothing` for it keeps it absent, though its initializer resolves `nil` to the sibling. Swift's synthesized
  `Codable` stores every property and applies no default; defaults belong to GES values and documents.
- Model and wire types of one schema are separate types in separate files; no conversion between both forms is generated,
  since a client's model rarely has the shape of its wire roots. A program converts in its own code.

With `--values`, every model type also converts to and from the plain value that the [value path](#record-values) uses
for a record of its schema, so that wire records and models meet there; no code converts between them directly:

- Swift `func toValue() -> GesValue` and `init(value: GesValue) throws(GesWireError)`; C# `GesValue ToValue()` and
  `static T FromValue(GesValue value)`, which throws `GesWireException`.
- The value is the Record that `GesSchemaValues` decodes from the record's bytes: a Record of the schema's name with every
  field in declared order, nested records as Records, Lists, numbers with their units and integral values as integers,
  Percentages, Tags for closed and open tags, Flags as the Map of their set members, Points and Vectors with their unit,
  Boxes and Polys as the library's external values with their unit, a union as its alternative's value and an absent
  optional or `:Nothing` alternative as `nothing`.
- Reading takes a Record of the schema's name, whose other fields are ignored, or a Map whose keys are all fields of it,
  and converts strictly as the [script bindings](#script-bindings) do. It fails with the codes of the
  [value path](#value-path), and a field it leaves out takes its [default](#defaults): a required field without a key or default is `wire.missingField`, `nothing` where a value is required
  is `wire.nothingValue`, and a value of another type is `wire.typeMismatch`, each at the field's path; a value that is no
  such Record or Map fails at the empty path. A nested record reports the
  path of its own field and a list element its list's path with `[]`, as the value path names them, such as
  `entries[].level`; a union that holds none of its alternatives reports itself.
- So a wire record reads a model with `read(value:)` (C# `ReadValue`), failing as a write would for a value out of range
  or a text too long, and a model reads a wire record's `toValue()` back. A model whose fields have no wire meaning, such
  as an unbounded text, still has a value; only the wire side checks constraints.
- `--values` is independent of `--bindings`; both may be given.

A generated name that collides, such as a field named `id` beside a key named otherwise, fails with
`generate.nameCollision`, as in wire code. With `--values`, that includes `toValue` (C# `ToValue` and `FromValue`).

### Model documents

> **Since: Unreleased**

`--documents` (option `Documents`) lets generated models read GES document text, so a host that loads data files repeats
no parsing, header check, schema check or error mapping. It includes `--values`, concerns only model code and does no file
I/O: text goes in, a model comes out.

- **Per model root:** Swift `init(document text: String) throws(GesDocumentError)`, C# `static T FromDocument(string text)`.
  It requires the header `:Schema(:Name)` of the root; a missing or other header is `document.schemaMismatch` at line 1,
  column 1, with a message naming both. It checks the document against the schema sources the code embeds, as
  [document checking](#document-checking) does, and converts its entries as `init(value:)` does, so fields it leaves out take
  their [defaults](#defaults). A Box written as the Map of its corners and a Poly as the List of its points become the
  geometry library's values first.
- **Per generation:** one type `<Namespace>Document`, named after the last part of the namespace, `ModelDocument` without
  one: a Swift enum with one case per model root, holding its model, and a C# abstract class with one sealed subclass per
  root, named after it and holding `Value`. Its `init(document:)` (C# `FromDocument`) accepts the header of any root and
  chooses the case by it, so a host that reads files of several kinds needs no switch on schema names. A model type of that
  name is `generate.nameCollision`.
- **Errors** are the document error of the port (`GesDocumentError`, `GesDocumentException`) with its code, line, column
  and field path: a reading error with its `document.*` code and no path, a schema check error with its `document.*` code
  and the path of the field as the value path names it, such as `parts[].kind`, and a `wire.*` error of the conversion at
  the deepest entry its path names, a List for an element, since paths name no index. The Runtime's
  [`GesSchemaDocument`](PublicApi.md#schemas) does the reading for generated code.
- The embedded sources live in one internal helper per generation, beside the value conversions; split output puts the
  document type and the helper in the shared file, and the manifest adds `documents: true`.

### Script bindings

> **Since: Unreleased**

`generate --model --bindings` also offers the model types to scripts as
[external types](HostRuntime.md#external-type-boundary). The generated code implements the Runtime's portable
external-type contracts itself: it imports neither the Bridge nor its macros, needs no macro approval in Xcode and uses no
Reflection, so managed code stripping under IL2CPP removes nothing it needs. Without `--bindings` the code is unchanged.

- A generated `GesTypes` class is the compiler's catalog and the host's external-type registry at once: pass one instance
  to `WithExternalTypeCatalog` and `WithExternalTypeRegistry` (Swift `withExternalTypeCatalog`, `withExternalTypeRegistry`).
  It declares every reached model type under its schema name, each after the types it holds, and resolves other types and
  constructors through an optional next catalog and registry. A next catalog that already declares one of its type names
  is rejected when the set is created (C# `ArgumentException`, Swift `GesApiError.invalidArgument`), so two declarations of
  one name never compete.
- Each type gets `ToGesValue()` and `FromGesValue(value)` (Swift `gesValue()` and `init?(gesValue:)`). A script reads its
  fields with their declared kinds and units: a Quantity with its unit, a Percentage, a Text, a Tag (also for a closed tag),
  a Point or Vector with its unit, a Box or Poly as the [geometry library's](PublicApi.md#geometry-library) external value, which
  the host registers, Flags as the Map of their set members, a List, Dice, the nested record as its external value, a union as its alternative's value,
  and `nothing` for an absent value. Integers read as Numbers. A Swift value is the copy that was wrapped; a C# record is read
  when a script reads a field.
- Each type has one constructor, `:Name(fields: map)`, and a field `fields` that returns the record as such a Map, so
  `:World_Body(fields: body.fields)` copies one. External constructors receive every argument present and of a built-in
  type, so a Map carries optional fields, which may be left out or `nothing`, and nested records. Values convert strictly:
  a number needs the field's unit, an integer an integral value, a Tag field also takes a Text, a closed tag one of its
  tags, Flags a Map of members with Boolean values, a list and its elements convert element by element, and a record field takes that record's external value. A value
  that does not convert, a missing required field or a key the type does not declare constructs `nothing`, like other domain
  failures. A schema field named `fields` collides with it: `generate.nameCollision`.
- Wire types get no bindings: a wire record reuses its storage, so a script holding one would see it change with the next
  read. `--values` converts wire records to and from GES values through their exact bytes and models to and from the same
  plain values; `--bindings` without `--model` is a usage error, and the Generator rejects it with `generate.usage`.

### Model layout

`layout --model` prints the language-neutral model layout in the JSON form of the [generation layout](#generation-layout):
`model` (the format version, 1), `roots` and `types`, ordered by name, and when types take schemas unchanged with `with`,
`protocols`, ordered by name. A type has `name`, `hash` and `fields`, a schema with lets `constants` and one with computed
fields `computed` as in the generation layout, and `takes`, the schemas it takes unchanged, when it has any. A protocol has `name`, `abstract`, `takes` (the
protocols it refines) and `fields` (the shared fields it requires besides those). A field has
`name`, `path`, `optional`, `key`, `default` as in the generation layout when it has one, and `type`. A type has `kind` and the members of its kind: `number` adds `type`, `unit`,
`integer`, `wrapping`, `lower`, `upper` and `step`, a bound as its canonical decimal or parameter name or `null`; `tag` adds
`tags` and `max`, `flags` adds `tags`, `text` and `bytes` add `max`, `list` adds `max`, `depth` only for a [recursive List](#recursion), and `element`, `dice` adds `max`, `lower` and `upper`, `point` and
`vector` add `unit` and `axes` (the names of an axis block), so does `box`, `poly` adds `unit`, `axes` and `max`, `schema` adds `name`, and `union` adds `alternatives`;
`boolean` and `nothing` have none. A missing `max` is `null`.

### Generation layout

The generation layout describes everything that code generation needs, independent of any
target language. Every port writes identical bytes for identical schemas and parameters.

Every schema, message and message set reached from a root is a type: through fields, union
alternatives, list elements and the members of a message set. Each type is bound on its own
with the given parameters, which is sound because parameters belong to files: a type binds the
same wherever it is reached, so it appears exactly once even when several roots share it.

The JSON text has two spaces of indentation per level, one member or element per line, a
closing bracket on its own line, `[]` for an empty array, members in the order below and a
final line break. Strings escape only `"`, `\` and control characters (`\u00xx`). Integers are
exact decimals. Hashes are 16 lowercase hexadecimal digits, and a binary64 value is written as
`0x` and the 16 hexadecimal digits of its bit pattern.

- The document has `layout` (the format version, 1), `roots` (the root names in the order
  given, without repetition), `parameters` and `types`.
- A parameter has `name`, `type` (`Number`, `Quantity` or `Percentage`), `unit` (the portable
  unit name, or `none`) and `value`, the exact decimal of its value in base units as the binding
  hash spells it. Parameters are reached by any root and ordered by name.
- Types are ordered by name, comparing UTF-16 code units. A type has `name`, `kind` (`schema`,
  `message` or `messages`), `hash`, `bindingHash`, `bindingHashDepends` (the parameters the type
  reaches), `maxByteCount` and `maxByteCountDepends` (the parameters named in the bounds of its
  fields and of the types it reaches). A message set adds `members` and `memberWidth`; a schema
  or message adds `fields`, a schema with [lets](#declarations) `constants`, a schema with
  [computed fields](#computed-fields) `computed`, and a message `tags`. A constant has `name`, `type` and `unit` as a
  parameter, `value`, its exact decimal in base units, and `integer`. A computed field has `name`, `type` (`Number`,
  `Quantity`, `Percentage` or `Boolean`), `unit`, `optional` and `expression`, a node with `op` (the names of the
  [canonical form](#canonical-form), and `literal`, `field` and `item`), `type` (`Number`, `Percentage`, `Boolean`, `Record`
  or `List`) and `unit`; a literal adds `value` (an exact decimal, `true` or `false`) and `integer`, a path `path`, the four
  arithmetic operations `first`, `second` and `third`, the unit checks the virtual machine makes for them, and an operation
  `children`.
- A field has `name`, `path`, `optional`, `key`, `unlabeled`, with a [default](#defaults) `default` with `kind`
  (`number`, `boolean`, `tag` or `field`) and `value` (an exact decimal in base units, a Boolean, a tag without `#` or
  the sibling's name), and `encoding`. List elements,
  axes and union alternatives are written with `name`, `path` and `encoding` only; an element is
  named `[]` and an alternative by its label or else by its type, as in its path.
- An encoding has `kind` and the members of its kind:
  - `boolean`: none.
  - `integer`, `quantized` and `float`: `type`, `unit`, `range`; `integer` and `quantized` add
    `wrapping`, `lower` and `maxIndex`; then `width`; `quantized` adds `numerator` and `tenPower`,
    the `p` and `10^k` of its step, and with `float` with a range both add `lowerValue` and
    `upperValue`; then `deltaRange`, `deltaWidth` and `depends`, the parameters named in its bounds.
  - `tag`: `tags`, without `#`, `tagSet` with the name of a [tag set](#declarations) the tag takes its members
    from, only then, and `width`.
  - `flags`: `tags`, the members without `#`, and `width`, their count.
  - `text`: `tagName` for a `:Tag max n`, `maxCount` and `width`.
  - `bytes`: `maxCount` and `width`.
  - `list`: `dice`, `maxCount`, `width`, `keyed`, `positionWidth`, `depth` only for a [recursive List](#recursion),
    and `element`.
  - `point` and `vector`: `unit` and `axes`, the transmitted axes in order x, y, z.
  - `box`: `unit` and `corners`, the fields `lower` and `upper`, each a `point`.
  - `poly`: `unit`, `maxCount`, `width` and `element`, a `point` field with the axes x and y.
  - `schema`: `name`, the type it refers to.
  - `union`: `width` and `alternatives`, in declared order.
  - `nothing`: none.

The `depends` lists are not used for generation today; they name the constants that a later
binding of parameters at run time would turn into values.

## Modules

- **Syntax** reads `.gess` into its declarations and reports syntax errors. It has no
  dependencies, and highlighting comes with it.
- **Runtime** holds the source rules, binding, hash, quantization, writer, reader, dump,
  document checking and the value path. Everything except the value path uses neither
  `GesValue` nor the VM, so a client that only encodes pays nothing for them at run time, and
  a linker can remove them. Compiler is not needed.
- **Generator** computes the [generation layout](#generation-layout) and writes the generated code
  for both CLIs and for build integrations through its [API](PublicApi.md#schema-code-generation). It
  depends on Syntax and Runtime only. Neither Runtime nor Compiler depends on it, so a game never
  ships it; it is published as the NuGet package `GameEventScript.Generator` and the SwiftPM product
  `GameEventScriptGenerator`. Generated code needs Runtime only.

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

**Generated code.** Code from `ges schema generate --language csharp` is C# 9 and holds to the same
rules: no `unsafe`, Reflection or runtime code generation, and nothing allocated by writing, reading or
deltas once a class has held its largest data. The Generator itself is a build-time assembly: a Unity
project keeps it, with the Syntax and Runtime assemblies it needs, in an `Editor` folder, so that a player
build never includes it.

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
    `document.fieldType`, `document.outOfRange` and `document.tooDeep`, with their positions;
  - [recursive Lists](#recursion): the canonical form, trees as deep as their depth, deeper data, keyed trees and their
    deltas, a recursive List that `with` copies, and the source errors.
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
- The role suite covers reading and describing roles, every role error, the encodability check of wire roots at reading
  and that roles change neither hash. The role fixture (`conformance/fixtures/SchemaGeneration/roles`) holds wire and model
  groups that reference each other, a part reached from both roles and a marked declaration used as a part; its layouts by
  group and its wire, model and binding code are checked in for both languages, both CLIs must write them identically, and
  the generated-code gate compiles them as C# 9 for Unity and as Swift 6 with warnings as errors. Native tests in both ports
  run spatial wire fields in 2D and 3D, model equality, and scripts that read and construct bound models.
