<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Describe data with schemas

> **Since: Unreleased**

A schema (`.gess`) describes what your data looks like: which fields exist, in which range and
how precise they are. From that one description you get two things:

- a very compact binary encoding, for example to send game state from a server to its clients;
- a check of [GES documents](../../specs/Documents.md) (`.gesd`), for example your configuration files.

This guide builds a schema step by step. [Schemas](../../specs/Schemas.md) defines the exact rules.

## 1. A first schema

Save this as `game.gess`:

```gess
schema :Player as {
  id: :Number integer between 0 and 1023 key
  x: :Quantity(m) between 0m and 500m step 10cm
  y: :Quantity(m) between 0m and 500m step 10cm
  heading: :Quantity(°) wrapping between 0° and 360° step 1°
  health: :Number integer between 0 and 100
  shielded: :Boolean
}
```

```bash
ges schema check game.gess
```

A range and a step decide how many bits a field takes. A record holds only these bits, with no
field names, type tags or units:

| Field | Possible values | Bits |
| --- | --- | --- |
| `id` | 1024 | 10 |
| `x` | 5001, every 10 cm from 0 m to 500 m | 13 |
| `y` | 5001 | 13 |
| `heading` | 360, since 360° is the same point as 0° | 9 |
| `health` | 101 | 7 |
| `shielded` | 2 | 1 |

That makes 53 bits, so a `Player` always takes 7 bytes. Sender and receiver can only read this
because both hold the same schema.

## 2. Choose ranges and steps

Each number needs a decision about how exact it has to be:

- `integer` keeps whole numbers exact.
- `step` rounds to a multiple: `x` above is exact to 10 cm. The narrower the range and the coarser
  the step, the fewer bits it takes. This loss is what you declare; both sides round the same way.
- `between a and b` includes both ends. A value outside is an error for the caller. Nothing is
  clamped silently.
- `wrapping between a and b` is a circle, for angles. `-90°` is stored as `270°`.
- A fractional number without a step always takes 64 bits.

Literals are written in their field's unit. Prefixes are only spelling: `:Quantity(N) between 0kN
and 50kN` is valid, `:Quantity(kN)` is not.

## 3. Use the other field types

```gess
schema :Weapon as {
  kind: :Tag(#laser, #cannon, #missile)
  label: :Text max 16
  ammo: :Number integer between 0 and 255 optional
}

schema :Tank as {
  name: :Text max 32
  position: :Point(m) {
    x between 0m and 500m step 10cm
    y between 0m and 500m step 10cm
  }
  weapons: :List(:Weapon) max 4
}
```

- `:Tag(...)` is one tag of a closed set, stored as its index: 2 bits for `kind`. `:Tag max 32` allows any
  tag of up to 32 characters, stored like a Text: 6 bits and 8 per character. Prefer a closed set for data
  you send often.
- `:Text max n` and `:List(T) max n` store a count, then their contents. `max` counts UTF-8 bytes or
  elements, and without it a field cannot be encoded.
- `:Dice max 5 between 1 and 20` stores up to 5 rolls in 5 bits each, here for a d20. Without a range a
  roll takes 31 bits. A Dice value always holds its rolls sorted, whatever their order in the data.
- `optional` adds one presence bit. An absent value reads as `nothing`.
- `:Point` and `:Vector` list the axes they transmit. An axis left out is not sent and must be zero.
- `:Weapon` refers to another schema, whose fields follow inline. References cannot form a cycle.

A boolean flag is simply a `:Boolean` field and takes one bit.

A field can also hold one of several types. Such a union stores the index of the alternative, then
its value. The kind of a value selects the alternative, so alternatives must differ in their type name:

```gess
schema :Target as {
  facing: :Quantity(°) wrapping between 0° and 360° step 1° | :Tag(#north, #south)
  note: :Text max 8 | :Nothing
}
```

`facing` takes 1 + 9 bits for an angle and 1 + 1 bits for a tag. `:Nothing` takes no bits of its own, so
`note` may be left out like an `optional` field, and this also works for list elements:
`:List(:Text max 8 | :Nothing) max 4`.

## 4. Share values with constants and parameters

```gess
constant $maxHealth be 135
parameter worldSize: :Quantity(m)

schema :Bot as {
  x: :Quantity(m) between 0m and worldSize step 1cm
  y: :Quantity(m) between 0m and worldSize step 1cm
  health: :Number integer between 0 and $maxHealth
}
```

A constant is a name for a literal in this file. A parameter is decided later, for example per
map. You supply it when you *bind* the schema:

```bash
ges schema hash game.gess --root Bot --param worldSize=5000m
```

Each binding fixes all widths, here 19 bits for `x` and `y`. You can bind one schema several
times with different values, for two zones of different size in one session. The wire does not
say which binding a record uses, so choose it from your own context, such as the connection.

## 5. Find the object with a key

`key` marks the first field of a schema as the identity of the object, like `id` of `Player`. It
is written at bit 0, so a receiver can read it first and then decide what to do with the rest. A
key is an `integer` number or a `:Tag` with a set, and it cannot be `optional`.

## 6. Describe messages

A message has a name, ordered arguments and optional tags, like a message in a GES script:

```gess
message Hit(target: :Number integer between 0 and 65535, _ damage: :Number integer between 0 and 1000) tags (#critical, #splash)
message Died(target: :Number integer between 0 and 65535)
```

- `target` is a labeled argument, as in `Hit(target: 7, 25)`. With `_`, as in `_ damage`, the
  argument has no label; the name then only identifies it in your code.
- Each declared tag takes one bit, since any combination may be present.
- The name of the message is not transmitted.

A `Hit` takes 16 + 10 + 2 = 28 bits. That only works if the receiver already knows that a `Hit`
comes next, for example on a channel that carries nothing else.

## 7. Choose between messages with a message set

Usually one stream carries different messages, and the receiver has to find out which one comes.
That is what a message set declares:

```gess
message Hit(target: :Number integer between 0 and 65535, _ damage: :Number integer between 0 and 1000) tags (#critical, #splash)
message Died(target: :Number integer between 0 and 65535)
messages :Combat as (Hit, Died)
```

`messages :Combat as (Hit, Died)` says: a record of `Combat` is exactly one of these messages. Its
encoding starts with the index of the message in this list, which takes 1 bit for two messages, and
the message follows:

| Record | Bits | Bytes |
| --- | --- | --- |
| `Hit` in `Combat` | 1 + 28 = 29 | 4 |
| `Died` in `Combat` | 1 + 16 = 17 | 3 |

The order in the list matters, because it decides the index; changing it changes the
[hash](#9-compare-the-hash-at-connection-setup). A message may belong to several sets, for example
one per channel. A set is not a data type: a field cannot have the type `:Combat`. You encode and
decode it as the root of a record.

## 8. How it runs

A schema is data your program reads at run time. GES does not generate code and needs neither a
compiler nor a VM for it:

| Step | C# | Result |
| --- | --- | --- |
| Read the sources, once | `GesSchemaSet.Parse(sources)` | all declarations, checked |
| Pick a declaration | `schemas.Schema("Combat")` | a description with its hash |
| Bind parameters, per session or map | `.Bind(parameters)` | the widths and layout, immutable and shareable |
| Resolve fields, once | `bound.Member("Hit").Field("target")` | handles that cost no lookup later |
| Write or read each record | `GesSchemaWriter`, `GesSchemaReader` | bytes in your buffer, without allocations |

Fields are written and read in declaration order; anything else is the error `wire.fieldOrder`. A
union alternative has its own handle, such as `facing:Tag`, after `SelectAlternative` or `ReadAlternative`.
This writes and reads the `Combat` set in C#:

```csharp
using System;
using GameEventScript.Api;

var schemas = GesSchemaSet.Parse("""
    message Hit(target: :Number integer between 0 and 65535, _ damage: :Number integer between 0 and 1000) tags (#critical, #splash)
    message Died(target: :Number integer between 0 and 65535)
    messages :Combat as (Hit, Died)
    """);
var combat = schemas.Schema("Combat").Bind();
var hit = combat.Member("Hit");
var (target, damage, critical, splash) = (hit.Field("target"), hit.Field("damage"), hit.Field("#critical"), hit.Field("#splash"));

Span<byte> buffer = stackalloc byte[(int)combat.MaxByteCount];
var writer = new GesSchemaWriter(combat, buffer);
writer.SelectMember(combat.MemberIndex("Hit"));
writer.WriteInteger(target, 7);
writer.WriteInteger(damage, 25);
writer.WriteBoolean(critical, true);
writer.WriteBoolean(splash, false);
var length = writer.Finish();

var reader = new GesSchemaReader(combat, buffer.Slice(0, length));
if (reader.ReadMember() == combat.MemberIndex("Hit"))
{
    Console.WriteLine($"{length} bytes: target {reader.ReadInteger(target)}, damage {reader.ReadInteger(damage)}, critical {reader.ReadBoolean(critical)}");
}
```

And the same in Swift, which follows the same steps with its own spelling:

```swift
import GameEventScriptRuntime

let schemas = try GesSchemaSet(parsing: """
    message Hit(target: :Number integer between 0 and 65535, _ damage: :Number integer between 0 and 1000) tags (#critical, #splash)
    message Died(target: :Number integer between 0 and 65535)
    messages :Combat as (Hit, Died)
    """)
let combat = try schemas.schema("Combat").bind()
let hit = combat.member("Hit")!
let (target, damage, critical, splash) = (hit.field("target")!, hit.field("damage")!, hit.field("#critical")!, hit.field("#splash")!)

var buffer = [UInt8](repeating: 0, count: combat.maxByteCount)
try buffer.withUnsafeMutableBytes { bytes in
    var writer = GesSchemaWriter(combat, into: bytes)
    try writer.selectMember(combat.memberIndex("Hit")!)
    try writer.writeInteger(target, 7)
    try writer.writeInteger(damage, 25)
    try writer.writeBoolean(critical, true)
    try writer.writeBoolean(splash, false)
    let length = try writer.finish()

    var reader = GesSchemaReader(combat, data: UnsafeRawBufferPointer(rebasing: UnsafeRawBufferPointer(bytes)[0..<length]))
    if try reader.readMember() == combat.memberIndex("Hit")! {
        print("\(length) bytes: target \(try reader.readInteger(target)), damage \(try reader.readInteger(damage)), critical \(try reader.readBoolean(critical))")
    }
}
```

For rare data where convenience matters more than speed, `GesSchemaValues` encodes and decodes a
whole Record, Map or Message at once. A decoded `Bot` is a Record with the type name `Bot`.

## 9. Compare the hash at connection setup

Both sides must hold the same schema and the same parameter values, because nothing on the wire
describes the data. A record has no header and no length either, so your transport frames it.
Each declaration has a hash for this, which you compare when a connection starts:

- `bound.Hash` changes with field order, types, bounds and steps. It ignores comments, layout and
  the spelling of literals, so `50kN` and `50000N` hash alike.
- `bound.BindingHash` also covers the parameter values.

There is no compatibility between different hashes: if they differ, the two sides cannot talk.
The hash detects mistakes and is no security measure. Data from a peer stays untrusted, and the
decoder rejects anything outside the schema.

## 10. Check documents with the same schema

A schema can also check a `.gesd` file, which names it in its header. This one allows either a name
or a whole unit in a list:

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

Types without a limit, such as `:Map` or `:Text` without `max`, are fine here. They only fail when you
bind such a schema for encoding, with `schema.notEncodable`.

## 11. Look into encoded data

`ges schema dump` makes bytes readable through the schema and names the first error with its bit:

```bash
ges schema dump game.gess --root Combat --data hit.bin
```

```text
message Hit index=0
target bit=1 width=16 value=7
damage bit=17 width=10 value=25
#critical bit=27 width=1 value=true
#splash bit=28 width=1 value=false
bytes 4
```

Also useful are `ges schema format`, which prints the recommended layout, and `ges schema canonical`,
which prints exactly what the hash covers.

## 12. Send only what changed

A record that mostly repeats is cheaper as a delta. `delta within` declares the largest change you
expect between two updates, in the units of the field, and a change within it takes only a few bits:

```gess
schema :Drone as {
  id: :Number integer between 0 and 255 key
  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m
  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m
}
```

A full record takes 8 + 13 + 13 = 34 bits, 5 bytes. A delta has the key and one change bit per field,
and a small move takes 7 bits per axis: 8 + 2 + 7 + 7 = 24 bits, 3 bytes. A larger move still works and
takes 14 bits for that axis.

A delta is made from two full records. The baseline is simply the record the other side holds:

```csharp
var length = GesSchemaDelta.Encode(drone, record, baseline, buffer);
var decoded = GesSchemaDelta.Decode(drone, received.AsSpan(0, length), baseline, recordBuffer);
```

In Swift, the same calls take raw buffers:

```swift
let length = try record.withUnsafeBytes { current in
    try baseline.withUnsafeBytes { held in
        try GesSchemaDelta.encode(drone, record: current, baseline: held, into: buffer)
    }
}
```

`Decode` writes the full record again, so a receiver keeps it as its next baseline. A key comes first in
a delta as well, so a receiver can find the object's baseline with `ReadKey` before it decodes the rest.
Which record counts as confirmed is up to your game, since nothing on the wire marks a delta. For rare
data, `GesSchemaValues` takes a baseline value instead. `ges schema dump --baseline` makes a delta
readable. See [Deltas](../../specs/Schemas.md#deltas) for the exact form.

## What schemas leave to you

Transport, channels, deciding which objects a client receives and splitting large data all belong
to your game. [Extension points](../../specs/Schemas.md#extension-points) names where they attach.
