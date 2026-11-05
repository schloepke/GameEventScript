<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Plan: schema deltas

This is a design for deferred work, tracked in the [backlog](../../BACKLOG.md). It is not
implemented and not normative. [Schemas](../../specs/Schemas.md) owns what exists today:
`key`, and `delta within`, which is read, validated and part of the hash. When deltas are
implemented, their contract moves there and this file is removed.

Implementing them also needs:

- errors `wire.keyMismatch` (encoding against a baseline with another key) and
  `wire.invalidDelta` (a delta is not canonical, leaves its range, or its key differs from
  the baseline's), with corpus cases;
- `MaxByteCount` as the larger of the largest full and the largest delta encoding, which also
  moves the 1 MiB limit. A Boolean's delta costs its change bit; a field with a delta range
  costs its change bit, the fallback bit and the larger of both widths;
- `WriteDelta...` with a baseline value and matching reads, with no change to existing calls;
- `--baseline` in `ges schema dump`;
- the delta columns of the worked examples below, in the spec.

## Deltas

The full encoding does not change when deltas are added.

`encode(value, baseline)` writes what differs from a baseline. The caller chooses and
confirms baselines; the format assumes nothing about the transport. A delta and a
full record are different operations and are not marked on the wire, so a caller that
mixes them marks them in its own framing.

### Form

A delta of a schema is one change bit per field in declaration order, followed by the
payload of each changed field in that order. Padding follows at the top level as in
[records](../../specs/Schemas.md#records). Comparison uses wire values: indices, not the original numbers.
A movement smaller than a step is no change and costs nothing. A top-level [key](#keys)
precedes the change bits and has none.

| Type | Payload of a changed field |
| --- | --- |
| `:Boolean` | none; the change bit toggles the baseline |
| numeric with `delta within` | [range form](#delta-range) |
| other numeric, `:Tag`, `:Text`, `:List` | the full encoding |
| `:Point`, `:Vector` with a block | one change bit per declared axis, then the payloads of the changed axes |
| `:Point`, `:Vector` without a block | three change bits, then a raw 64-bit binary64 for each changed axis; bits are compared |
| `:TypeName` | a nested delta: its own change bits and payloads |
| union | index; when the baseline has the same alternative, its delta payload, otherwise its full encoding |
| `:Dice max n` | the full encoding |
| `optional` | presence bit; when present and the baseline also has a value, the field's delta payload, otherwise its full encoding |

A list is replaced as a whole. Tracking list elements requires identities that the
caller owns.

For example, with `worldWidth = worldHeight = 5000m`:

```gess
schema :Beacon as {
  position: :Point(m) {
    x between 0m and worldWidth step 1cm delta within 1.27m
    y between 0m and worldHeight step 1cm delta within 1.27m
  }
  active: :Boolean
}
```

A full record takes 19 + 19 + 1 = 39 bits, 5 bytes. A delta has 2 change bits for the
fields and, when `position` changed, 2 for its axes:

| Situation | Bits | Bytes |
| --- | --- | --- |
| Only `active` toggled | 2 | 1 |
| Moved along `x` | 2 + 2 + 9 = 13 | 2 |
| Moved along both axes | 2 + 2 + 9 + 9 = 22 | 3 |
| `x` through the fallback, `y` in range | 2 + 2 + 20 + 9 = 33 | 5 |

### Keys

A `key` lets a receiver find the object and its baseline before it reads the rest.

- A key is the first field, so it comes first at bit 0 of a full record and of a delta,
  always in its full encoding with the same width. A reader can take it without knowing
  which of the two it holds.
- A delta writes it unconditionally, without a change bit, also when nothing else changed.
- A key identifies the object, so it is not compared like other fields but required to be
  equal: encoding against a baseline with another key is `wire.keyMismatch`, and decoding
  against one is `wire.invalidDelta`. This keeps comparison on wire values and the
  [canonical form](#canonical-delta-form) intact: there is no change bit that could be set
  wrongly, and no second encoding.
- The marker takes effect at the top level. A schema with a key that is reached as a nested
  field or a list element writes it as its ordinary first field, with a change bit in a
  nested delta.
- The caller owns the mapping from keys to baselines. Message arguments have no key.

### Delta range

`delta within D` declares `d = D / step` indices (or `D` for an `integer`), and needs
`width(2d + 1)` to be below the width of the field. The payload is:

- bit `0`, then `delta + d` in `width(2d + 1)` bits, when `|delta| <= d`;
- bit `1`, then the full index otherwise.

`delta` is the signed difference of the indices. For a wrapping field with period `n`
it takes the shorter way: `delta = ((index - base + floor(n / 2)) mod n) - floor(n / 2)`
with a nonnegative remainder, and the decoder computes `(base + delta) mod n`. An
exact half period therefore counts as negative. Differences of `integer` fields are
computed without overflow, also across the whole Int64 range.

### Canonical delta form

A delta has one valid encoding, so ports stay byte-identical. A decoder rejects a set
change bit for an unchanged value (except a Boolean, where it is the change), a nested
delta or axis group whose change bits are all zero, a payload of `bit 1` for a difference
within `d`, a non-wrapping result outside the range, and a key that differs from the
baseline's.

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

Zones of different size use [one binding each](../../specs/Schemas.md#binding). A baseline belongs to its
binding, so an object that moves to a zone of another size is sent as a full record of
the new binding.

## Worked examples

### Bot

At 20 updates per second, a delta range of `8°` covers turning up to 160°/s, above the
150°/s of the fastest units.

| Field | Values `n` | Full bits | Delta range | Range form | Fallback |
| --- | --- | --- | --- | --- | --- |
| `id`, the key | 65536 | 16 | always written | 16 | |
| `x` | 500001 | 19 | ±127 | 1 + 8 | 1 + 19 |
| `y` | 500001 | 19 | ±127 | 1 + 8 | 1 + 19 |
| `heading` | 3600 | 12 | ±80 | 1 + 8 | 1 + 12 |
| `turretBearing` | 3600 | 12 | ±80 | 1 + 8 | 1 + 12 |
| `radarBearing` | 3600 | 12 | ±80 | 1 + 8 | 1 + 12 |
| `health` | 136 | 8 | none | 8 | |
| `firing`, `shielded`, `overheated` | | 3 | | 0 each | |

**Full record:** 16 + 19 + 19 + 12 + 12 + 12 + 8 + 3 = **101 bits, 13 bytes**.

**Delta** with the 16-bit key and 9 change bits, so 25 bits before any payload:

| Situation | Bits | Bytes |
| --- | --- | --- |
| Unchanged; the caller may send nothing | 16 + 9 = 25 | 4 |
| Standing, radar turning | 25 + 9 = 34 | 5 |
| Moving, all angles turning | 25 + 9 + 9 + 9 + 9 + 9 = 70 | 9 |
| Same, hit and starting to fire | 70 + 8 + 0 = 78 | 10 |
| Teleport: `x` and `y` through the fallback | 25 + 20 + 20 = 65 | 9 |

100 visible bots at 20 updates per second take 26 000 bytes/s in full and 18 000 bytes/s
as moving deltas, without framing.

### Drone

| Field | Values `n` | Full bits | Delta range | Range form | Fallback |
| --- | --- | --- | --- | --- | --- |
| `id`, the key | 256 | 8 | always written | 8 | |
| `x` | 8001 | 13 | ±31 | 1 + 6 | 1 + 13 |
| `y` | 8001 | 13 | ±31 | 1 + 6 | 1 + 13 |
| `heading` | 720 | 10 | ±31 | 1 + 6 | 1 + 10 |

**Full record:** 8 + 13 + 13 + 10 = **44 bits, 6 bytes**.

**Delta** with the 8-bit key and 3 change bits, so 11 bits before any payload:

| Situation | Bits | Bytes |
| --- | --- | --- |
| At rest relative to the carrier; the caller may send nothing | 8 + 3 = 11 | 2 |
| Flying straight | 11 + 7 + 7 = 25 | 4 |
| Flying and turning | 11 + 7 + 7 + 7 = 32 | 4 |
| `x` through the fallback | 11 + 14 = 25 | 4 |

The relative position is such a schema of its own, with bounds that fit the carrier, not
the world. A carrier with `drones: :List(:Drone) max 15` packs them inline: 4 + 15 × 44 = 664 bits,
83 bytes instead of 90. As a list it is replaced in full by a delta, so drones that change
often are better sent as separate records.
