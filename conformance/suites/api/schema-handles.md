---
formatVersion: 1
suiteId: api.schema-handles
title: GES handle types
kind: schemaApi
level: atomic
categories: [conformance]
tags: [portable-api, schemas, handles]
---

# GES handle types

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies `handle` declarations in `.gess` sources: their rules, canonical form and hash, handle fields and
handle Maps on the wire, and the content hash ([Handles](../../../specs/Schemas.md#handles),
[Content hash](../../../specs/Schemas.md#content-hash)).

---

## Test: the canonical form and hash of a handle type

A handle type writes its `handle` line, its id and its version; the state schema is not written.

### Case description

```yaml
gesBlock: case
id: handle-canonical
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\n"]
  handle: "Target"
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle Target\nid Number integer between 0e0 65535e0\nversion Nothing\n", "hash": "dd9da4ea4ed63220"}}
```

---

## Test: a revision with a range

A revision version writes its range.

### Case description

```yaml
gesBlock: case
id: handle-revision
schemaApi:
  sources: ["schema :DroneState as { x: :Quantity(m) between -200m and 200m step 0.05m }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\n"]
  handle: "@Drone"
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle Drone\nid Number integer between 0e0 255e0\nversion Number integer between 0e0 1e3\n", "hash": "9478731644d8e1e5"}}
```

---

## Test: a composite writes its root

A composite writes its part and then its root's declaration.

### Case description

```yaml
gesBlock: case
id: handle-composite
schemaApi:
  sources: ["schema :Composition as { name: :Text max 64, slots: :Number integer between 0 and 16 }\nschema :Source as { text: :Text }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @UnitFile of :Composition composite @Repository as { part: :Text max 256 }\n"]
  handle: "UnitFile"
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle UnitFile composite Repository\npart Text max 256\nhandle Repository\nid Text max 64\nversion Hash(#sha1)\n", "hash": "d16150f5cc7d628d"}}
```

---

## Test: a part-less composite writes no part

A composite without a part writes its `handle` line and then its root's declaration.

### Case description

```yaml
gesBlock: case
id: handle-partless-composite
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 9 }\nschema :BrainState as { mood: :Number integer between 0 and 3 }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Brain of :BrainState composite @Drone\n"]
  handle: "Brain"
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle Brain composite Drone\nhandle Drone\nid Number integer between 0e0 255e0\nversion Number integer between 0e0 1e3\n", "hash": "25fc8682a3249886"}}
```

---

## Test: a part-less composite of a composite

A part-less composite has one level too.

### Case description

```yaml
gesBlock: case
id: handle-partless-composite-of-composite
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 9 }\nschema :BrainState as { mood: :Number integer between 0 and 3 }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Brain of :BrainState composite @Drone\nhandle @Mind of :BrainState composite @Brain\n"]
  handle: "Mind"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidHandle"}}
```

---

## Test: a part-less composite on the wire

A part-less composite sends its root's id and, as `@Name~`, its root's version, exactly like its root.

### Case description

```yaml
gesBlock: case
id: handle-partless-encode
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 9 }\nschema :BrainState as { mood: :Number integer between 0 and 3 }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Brain of :BrainState composite @Drone\nwire schema :Seen as { brain: @Brain, pinned: @Brain~, drone: @Drone~ }\n"]
  root: "Seen"
  encode: {"type": ":Seen", "entries": [{"key": "brain", "value": {"type": ":Handle", "value": "@Brain:3"}}, {"key": "drone", "value": {"type": ":Handle", "value": "@Drone:3~7"}}, {"key": "pinned", "value": {"type": ":Handle", "value": "@Brain:3~7"}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"encoded": {"bytes": "0303070c1c00"}}
```

---

## Test: decoding a part-less composite

Decoding gives the part-less composite's handles with the root's id and version.

### Case description

```yaml
gesBlock: case
id: handle-partless-decode
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 9 }\nschema :BrainState as { mood: :Number integer between 0 and 3 }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Brain of :BrainState composite @Drone\nwire schema :Seen as { brain: @Brain, pinned: @Brain~, drone: @Drone~ }\n"]
  root: "Seen"
  decode: "0303070c1c00"
```

### Expectation

```yaml
gesBlock: expect
schema: {"decoded": {"value": {"type": ":Seen", "entries": [{"key": "brain", "value": {"type": ":Handle", "value": "@Brain:3"}}, {"key": "drone", "value": {"type": ":Handle", "value": "@Drone:3~7"}}, {"key": "pinned", "value": {"type": ":Handle", "value": "@Brain:3~7"}}]}, "byteCount": 6}}
```

---

## Test: the state schema is not part of a sending schema's hash

A wire schema that sends handles keeps its hash when the state schema changes; compare the next case.

### Case description

```yaml
gesBlock: case
id: handle-state-not-hashed
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nschema :Composition as { name: :Text max 64, slots: :Number integer between 0 and 16 }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nwire schema :Sighting as { target: @Target, seen: :List(@Target) max 4, unit: @Unit~ optional }\n"]
  root: "Sighting"
```

### Expectation

```yaml
gesBlock: expect
schema: {"hash": "c3fe29a8a7dcad86"}
```

---

## Test: another state schema, the same hash

The same declarations with another `:Contact` give the hash of the previous case.

### Case description

```yaml
gesBlock: case
id: handle-state-changed
schemaApi:
  sources: ["schema :Contact as { label: :Text max 32 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nschema :Composition as { name: :Text max 64, slots: :Number integer between 0 and 16 }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nwire schema :Sighting as { target: @Target, seen: :List(@Target) max 4, unit: @Unit~ optional }\n"]
  root: "Sighting"
```

### Expectation

```yaml
gesBlock: expect
schema: {"hash": "c3fe29a8a7dcad86"}
```

---

## Test: an undeclared handle type

Naming an undeclared handle type is `schema.unknownName`.

### Case description

```yaml
gesBlock: case
id: handle-unknown
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nwire schema :S as { t: @Target }\n"]
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.unknownName"}}
```

---

## Test: a composite of a composite

Composites have one level.

### Case description

```yaml
gesBlock: case
id: handle-composite-of-composite
schemaApi:
  sources: ["schema :Composition as { name: :Text max 64, slots: :Number integer between 0 and 16 }\nschema :Source as { text: :Text }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @UnitFile of :Composition composite @Repository as { part: :Text max 256 }\nhandle @Deep of :Composition composite @UnitFile as { part: :Number integer between 0 and 3 }\n"]
  handle: "Deep"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidHandle"}}
```

---

## Test: a state field named id

A state field named like a handle member collides with it.

### Case description

```yaml
gesBlock: case
id: handle-state-id
schemaApi:
  sources: ["schema :S as { id: :Text max 4 }\nhandle @X of :S as { id: :Number integer between 0 and 9, version: :Nothing }\n"]
  handle: "X"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidHandle"}}
```

---

## Test: version before id

A handle type has `id` and `version` in this order.

### Case description

```yaml
gesBlock: case
id: handle-field-order
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @X of :Contact as { version: :Nothing, id: :Number integer between 0 and 9 }\n"]
  handle: "X"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidHandle"}}
```

---

## Test: an id bound from a parameter

A handle type is no schema, so its bounds are literals or file constants.

### Case description

```yaml
gesBlock: case
id: handle-id-parameter
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nparameter n: :Number\nhandle @X of :Contact as { id: :Number integer between 0 and n, version: :Nothing }\n"]
  handle: "X"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidHandle"}}
```

---

## Test: an id bound from a file constant

A file constant may bound an id.

### Case description

```yaml
gesBlock: case
id: handle-constant-bound
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nconstant $n be 9\nhandle @X of :Contact as { id: :Number integer between 0 and $n, version: :Nothing }\n"]
  handle: "X"
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle X\nid Number integer between 0e0 9e0\nversion Nothing\n", "hash": "debb401176b9257a"}}
```

---

## Test: an abstract state schema

The state is a declared, non-abstract schema.

### Case description

```yaml
gesBlock: case
id: handle-abstract-state
schemaApi:
  sources: ["abstract schema :S as { a: :Text max 4 }\nhandle @X of :S as { id: :Number integer between 0 and 9, version: :Nothing }\n"]
  handle: "X"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidHandle"}}
```

---

## Test: a pinned handle of a type without versions

`@Name~` exists only for a type with a version.

### Case description

```yaml
gesBlock: case
id: handle-pinned-unversioned
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nwire schema :S as { t: @Target~ }\n"]
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidHandle"}}
```

---

## Test: a pinned composite of a versioned root

A composite carries its root's version, so `@UnitFile~` exists when `@Repository` has one.

### Case description

```yaml
gesBlock: case
id: handle-pinned-composite
schemaApi:
  sources: ["schema :Composition as { name: :Text max 64, slots: :Number integer between 0 and 16 }\nschema :Source as { text: :Text }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @UnitFile of :Composition composite @Repository as { part: :Text max 256 }\nwire schema :S as { f: @UnitFile~ }\n"]
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema: {"hash": "c83b7aae00bdf393"}
```

---

## Test: a handle beside its pinned form in a union

`@Name` and `@Name~` in one union are `schema.invalidUnion`.

### Case description

```yaml
gesBlock: case
id: handle-union-pinned
schemaApi:
  sources: ["schema :DroneState as { x: :Quantity(m) between -200m and 200m step 0.05m }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nwire schema :S as { d: @Drone | @Drone~ }\n"]
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidUnion"}}
```

---

## Test: a pinned Map key

A Map key is a handle type without `~`.

### Case description

```yaml
gesBlock: case
id: handle-map-pinned-key
schemaApi:
  sources: ["schema :DroneState as { x: :Quantity(m) between -200m and 200m step 0.05m }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nwire schema :S as { m: :Map(@Drone~ → :Text max 4) max 2 }\n"]
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidHandle"}}
```

---

## Test: a handle field as a key

A handle field cannot be a `key`.

### Case description

```yaml
gesBlock: case
id: handle-key-field
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nwire schema :S as { t: @Target key }\n"]
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidKey"}}
```

---

## Test: a composite inside its root's state

A composite reached from its own root's state is not encodable until a relative encoding is specified.

### Case description

```yaml
gesBlock: case
id: handle-inside-root
schemaApi:
  sources: ["schema :UnitSlotState as { weapon: :Text max 8 }\nschema :Composition as { slots: :List(@UnitSlot) max 4 }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @UnitSlot of :UnitSlotState composite @Unit as { part: :Number integer between 0 and 15 }\nwire schema :S as { u: @Unit~ }\n"]
  root: "Composition"
  bind: true
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.notEncodable"}}
```

---

## Test: handles on the wire

A handle sends its identity; an `@Name~` field also its version.

### Case description

```yaml
gesBlock: case
id: handle-encode
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nschema :Composition as { name: :Text max 64, slots: :Number integer between 0 and 16 }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nwire schema :Sighting as { target: @Target, seen: :List(@Target) max 4, unit: @Unit~ optional }\n"]
  root: "Sighting"
  encode: {"type": ":Sighting", "entries": [{"key": "target", "value": {"type": ":Handle", "value": "@Target:3"}}, {"key": "seen", "value": {"type": ":List", "items": [{"type": ":Handle", "value": "@Target:1"}, {"type": ":Handle", "value": "@Target:65535"}]}}, {"key": "unit", "value": {"type": ":Handle", "value": "@Unit:drone.gnabblo~a2951261887894b7bc7f72d4af023c7b7576677f76283d8a524f2496b19cdc50"}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"encoded": {"bytes": "03000a00f8ffdf20937b732b7339730b1313637b13ad940843c4a3bce5fd93a37e15e0d9abb33bfbb343e951947a22b18ce5e48602"}}
```

---

## Test: decoding handles

Decoding gives handles without state; an `@Name~` field gives its version.

### Case description

```yaml
gesBlock: case
id: handle-decode
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nschema :Composition as { name: :Text max 64, slots: :Number integer between 0 and 16 }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nwire schema :Sighting as { target: @Target, seen: :List(@Target) max 4, unit: @Unit~ optional }\n"]
  root: "Sighting"
  decode: "03000a00f8ffdf20937b732b7339730b1313637b13ad940843c4a3bce5fd93a37e15e0d9abb33bfbb343e951947a22b18ce5e48602"
```

### Expectation

```yaml
gesBlock: expect
schema: {"decoded": {"value": {"type": ":Sighting", "entries": [{"key": "seen", "value": {"type": ":List", "items": [{"type": ":Handle", "value": "@Target:1"}, {"type": ":Handle", "value": "@Target:65535"}]}}, {"key": "target", "value": {"type": ":Handle", "value": "@Target:3"}}, {"key": "unit", "value": {"type": ":Handle", "value": "@Unit:drone.gnabblo~a2951261887894b7bc7f72d4af023c7b7576677f76283d8a524f2496b19cdc50"}}]}, "byteCount": 53}}
```

---

## Test: a pinned field without a version

An `@Name~` field that receives a handle without a version is `wire.missingVersion`.

### Case description

```yaml
gesBlock: case
id: handle-missing-version
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nschema :Composition as { name: :Text max 64, slots: :Number integer between 0 and 16 }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nwire schema :Sighting as { target: @Target, seen: :List(@Target) max 4, unit: @Unit~ optional }\n"]
  root: "Sighting"
  encode: {"type": ":Sighting", "entries": [{"key": "target", "value": {"type": ":Handle", "value": "@Target:3"}}, {"key": "seen", "value": {"type": ":List", "items": []}}, {"key": "unit", "value": {"type": ":Handle", "value": "@Unit:drone.gnabblo"}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"encoded": {"error": {"code": "wire.missingVersion", "path": "unit.version"}}}
```

---

## Test: a handle of another type

A handle of another type is `wire.typeMismatch`.

### Case description

```yaml
gesBlock: case
id: handle-other-type
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nschema :Composition as { name: :Text max 64, slots: :Number integer between 0 and 16 }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nwire schema :Sighting as { target: @Target, seen: :List(@Target) max 4, unit: @Unit~ optional }\n"]
  root: "Sighting"
  encode: {"type": ":Sighting", "entries": [{"key": "target", "value": {"type": ":Handle", "value": "@Unit:a"}}, {"key": "seen", "value": {"type": ":List", "items": []}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"encoded": {"error": {"code": "wire.typeMismatch", "path": "target"}}}
```

---

## Test: an id outside its range

An id outside its range is `wire.outOfRange`.

### Case description

```yaml
gesBlock: case
id: handle-id-range
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nschema :Composition as { name: :Text max 64, slots: :Number integer between 0 and 16 }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nwire schema :Sighting as { target: @Target, seen: :List(@Target) max 4, unit: @Unit~ optional }\n"]
  root: "Sighting"
  encode: {"type": ":Sighting", "entries": [{"key": "target", "value": {"type": ":Handle", "value": "@Target:65536"}}, {"key": "seen", "value": {"type": ":List", "items": []}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"encoded": {"error": {"code": "wire.outOfRange"}}}
```

---

## Test: a handle Map on the wire

A handle Map sends its entry count and each entry in key order.

### Case description

```yaml
gesBlock: case
id: handle-map-encode
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nwire schema :Memory as { lastSeen: :Map(@Target → :Number integer between 0 and 100) max 4 }\n"]
  root: "Memory"
  encode: {"type": ":Memory", "entries": [{"key": "lastSeen", "value": {"type": ":Map", "entries": [{"handle": "@Target:9", "value": {"type": ":Number.int64", "value": "5"}}, {"handle": "@Target:2", "value": {"type": ":Number.int64", "value": "7"}}]}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"encoded": {"bytes": "12003824001400"}}
```

---

## Test: decoding a handle Map

Decoding gives a Map with handle keys.

### Case description

```yaml
gesBlock: case
id: handle-map-decode
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nwire schema :Memory as { lastSeen: :Map(@Target → :Number integer between 0 and 100) max 4 }\n"]
  root: "Memory"
  decode: "12003824001400"
```

### Expectation

```yaml
gesBlock: expect
schema: {"decoded": {"value": {"type": ":Memory", "entries": [{"key": "lastSeen", "value": {"type": ":Map", "entries": [{"handle": "@Target:2", "value": {"type": ":Number.int64", "value": "7"}}, {"handle": "@Target:9", "value": {"type": ":Number.int64", "value": "5"}}]}}]}, "byteCount": 7}}
```

---

## Test: handle Map keys out of order

Keys that are not strictly ascending are `wire.invalidKeyOrder`.

### Case description

```yaml
gesBlock: case
id: handle-map-key-order
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nwire schema :Memory as { lastSeen: :Map(@Target → :Number integer between 0 and 100) max 4 }\n"]
  root: "Memory"
  decode: "4a002808001c00"
```

### Expectation

```yaml
gesBlock: expect
schema: {"decoded": {"error": {"code": "wire.invalidKeyOrder"}}}
```

---

## Test: a repeated handle Map key

A repeated key is not strictly ascending either.

### Case description

```yaml
gesBlock: case
id: handle-map-duplicate
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nwire schema :Memory as { lastSeen: :Map(@Target → :Number integer between 0 and 100) max 4 }\n"]
  root: "Memory"
  decode: "12003808001400"
```

### Expectation

```yaml
gesBlock: expect
schema: {"decoded": {"error": {"code": "wire.invalidKeyOrder"}}}
```

---

## Test: the content hash of a state

The vector of the specification.

### Case description

```yaml
gesBlock: case
id: content-hash
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\n"]
  handle: "Contacted"
  contentHash: {"type": ":Contact", "entries": [{"key": "label", "value": {"type": ":Text", "value": "Alpha"}}, {"key": "strength", "value": {"type": ":Number.int64", "value": "42"}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle Contacted\nid Number integer between 0e0 65535e0\nversion ContentHash\n", "hash": "f1e5d04c028935fc"}, "contentHash": "a2951261887894b7bc7f72d4af023c7b7576677f76283d8a524f2496b19cdc50"}
```

---

## Test: a binary64 number

`42.0` hashes like `42`.

### Case description

```yaml
gesBlock: case
id: content-hash-float
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\n"]
  handle: "Contacted"
  contentHash: {"type": ":Contact", "entries": [{"key": "label", "value": {"type": ":Text", "value": "Alpha"}}, {"key": "strength", "value": {"type": ":Number.binary64", "value": "42"}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle Contacted\nid Number integer between 0e0 65535e0\nversion ContentHash\n", "hash": "f1e5d04c028935fc"}, "contentHash": "a2951261887894b7bc7f72d4af023c7b7576677f76283d8a524f2496b19cdc50"}
```

---

## Test: a Map in another order with an extra key

Field order and undeclared keys do not change the hash.

### Case description

```yaml
gesBlock: case
id: content-hash-map
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\n"]
  handle: "Contacted"
  contentHash: {"type": ":Map", "entries": [{"key": "strength", "value": {"type": ":Number.int64", "value": "42"}}, {"key": "label", "value": {"type": ":Text", "value": "Alpha"}}, {"key": "extra", "value": {"type": ":Number.int64", "value": "1"}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle Contacted\nid Number integer between 0e0 65535e0\nversion ContentHash\n", "hash": "f1e5d04c028935fc"}, "contentHash": "a2951261887894b7bc7f72d4af023c7b7576677f76283d8a524f2496b19cdc50"}
```

---

## Test: Text is hashed as it is

A precomposed `é` and `e` with a combining accent give different hashes; compare the next case.

### Case description

```yaml
gesBlock: case
id: content-hash-text
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\n"]
  handle: "Contacted"
  contentHash: {"type": ":Contact", "entries": [{"key": "label", "value": {"type": ":Text", "value": "Café"}}, {"key": "strength", "value": {"type": ":Number.int64", "value": "1"}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle Contacted\nid Number integer between 0e0 65535e0\nversion ContentHash\n", "hash": "f1e5d04c028935fc"}, "contentHash": "718e6c1484255d356972adcd0899ebee7bc413d3ef044b302ebd6986c2e11534"}
```

---

## Test: a decomposed accent

The decomposed spelling of the previous case.

### Case description

```yaml
gesBlock: case
id: content-hash-decomposed
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\n"]
  handle: "Contacted"
  contentHash: {"type": ":Contact", "entries": [{"key": "label", "value": {"type": ":Text", "value": "Café"}}, {"key": "strength", "value": {"type": ":Number.int64", "value": "1"}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle Contacted\nid Number integer between 0e0 65535e0\nversion ContentHash\n", "hash": "f1e5d04c028935fc"}, "contentHash": "35debbb8c33ced6e7ac5a9f82ddd3469740620e23c5fdb1f480066a6d21cfa2a"}
```

---

## Test: a missing required field

A state without a required field is `wire.missingField`.

### Case description

```yaml
gesBlock: case
id: content-hash-missing
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\n"]
  handle: "Contacted"
  contentHash: {"type": ":Contact", "entries": [{"key": "label", "value": {"type": ":Text", "value": "Alpha"}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle Contacted\nid Number integer between 0e0 65535e0\nversion ContentHash\n", "hash": "f1e5d04c028935fc"}, "contentHash": {"error": {"code": "wire.missingField", "path": "strength"}}}
```

---

## Test: a scalar of another kind

Scalar kinds and ranges are not checked; the hash covers the values as given.

### Case description

```yaml
gesBlock: case
id: content-hash-scalar
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\n"]
  handle: "Contacted"
  contentHash: {"type": ":Contact", "entries": [{"key": "label", "value": {"type": ":Number.int64", "value": "3"}}, {"key": "strength", "value": {"type": ":Number.int64", "value": "1"}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle Contacted\nid Number integer between 0e0 65535e0\nversion ContentHash\n", "hash": "f1e5d04c028935fc"}, "contentHash": "417d88491e8eb7e94c95ee02abb1899a3ed5c524ce1199b2671e66cddecd50c2"}
```

---

## Test: absent optional and default fields

A state that leaves out an optional field and a field with a default; the next two cases give the same hash.

### Case description

```yaml
gesBlock: case
id: content-hash-absent
schemaApi:
  sources: ["schema :Note as { text: :Text max 8, mood: :Text max 8 optional, level: :Number integer between 0 and 9 default 4 }\nhandle @Noted of :Note as { id: :Number integer between 0 and 9, version: :ContentHash }\n"]
  handle: "Noted"
  contentHash: {"type": ":Note", "entries": [{"key": "text", "value": {"type": ":Text", "value": "hi"}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle Noted\nid Number integer between 0e0 9e0\nversion ContentHash\n", "hash": "3fe1f662e3c2871a"}, "contentHash": "0d5907264da816969b229f223b31daedb766af4b74dea8ba88c638436a3f4ce1"}
```

---

## Test: an optional field of nothing

`nothing` in an optional field equals leaving it out.

### Case description

```yaml
gesBlock: case
id: content-hash-nothing
schemaApi:
  sources: ["schema :Note as { text: :Text max 8, mood: :Text max 8 optional, level: :Number integer between 0 and 9 default 4 }\nhandle @Noted of :Note as { id: :Number integer between 0 and 9, version: :ContentHash }\n"]
  handle: "Noted"
  contentHash: {"type": ":Note", "entries": [{"key": "text", "value": {"type": ":Text", "value": "hi"}}, {"key": "mood", "value": {"type": ":Nothing"}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle Noted\nid Number integer between 0e0 9e0\nversion ContentHash\n", "hash": "3fe1f662e3c2871a"}, "contentHash": "0d5907264da816969b229f223b31daedb766af4b74dea8ba88c638436a3f4ce1"}
```

---

## Test: a written default

Writing the default equals leaving it out.

### Case description

```yaml
gesBlock: case
id: content-hash-default
schemaApi:
  sources: ["schema :Note as { text: :Text max 8, mood: :Text max 8 optional, level: :Number integer between 0 and 9 default 4 }\nhandle @Noted of :Note as { id: :Number integer between 0 and 9, version: :ContentHash }\n"]
  handle: "Noted"
  contentHash: {"type": ":Note", "entries": [{"key": "text", "value": {"type": ":Text", "value": "hi"}}, {"key": "level", "value": {"type": ":Number.int64", "value": "4"}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle Noted\nid Number integer between 0e0 9e0\nversion ContentHash\n", "hash": "3fe1f662e3c2871a"}, "contentHash": "0d5907264da816969b229f223b31daedb766af4b74dea8ba88c638436a3f4ce1"}
```

---

## Test: another value than the default

A value other than the default changes the hash.

### Case description

```yaml
gesBlock: case
id: content-hash-other-level
schemaApi:
  sources: ["schema :Note as { text: :Text max 8, mood: :Text max 8 optional, level: :Number integer between 0 and 9 default 4 }\nhandle @Noted of :Note as { id: :Number integer between 0 and 9, version: :ContentHash }\n"]
  handle: "Noted"
  contentHash: {"type": ":Note", "entries": [{"key": "text", "value": {"type": ":Text", "value": "hi"}}, {"key": "level", "value": {"type": ":Number.int64", "value": "5"}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle Noted\nid Number integer between 0e0 9e0\nversion ContentHash\n", "hash": "3fe1f662e3c2871a"}, "contentHash": "538ff32f118708b62e912dd9ae6d8e9694d5843cf0d0db02ebba7ba962302344"}
```

---

## Test: a Quantity and a nested Record

A Quantity writes its canonical unit symbol and nested Records are normalized too.

### Case description

```yaml
gesBlock: case
id: content-hash-nested
schemaApi:
  sources: ["schema :Spot as { x: :Quantity(m) between -10m and 10m step 0.5m, inner: :Inner }\nschema :Inner as { flag: :Boolean }\nhandle @Spotted of :Spot as { id: :Number integer between 0 and 9, version: :ContentHash }\n"]
  handle: "Spotted"
  contentHash: {"type": ":Spot", "entries": [{"key": "x", "value": {"type": ":Quantity.binary64", "value": "1.5", "unit": "m"}}, {"key": "inner", "value": {"type": ":Inner", "entries": [{"key": "flag", "value": {"type": ":Boolean", "value": "true"}}, {"key": "extra", "value": {"type": ":Number.int64", "value": "1"}}]}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle Spotted\nid Number integer between 0e0 9e0\nversion ContentHash\n", "hash": "1a4fd25535677a5d"}, "contentHash": "bbaf7580ed3b155093c280889c0f73c347a8737385c67d7b01880e647314909f"}
```

---

## Test: a nested value that is no Record

A field that only takes Records rejects another value with `wire.typeMismatch`.

### Case description

```yaml
gesBlock: case
id: content-hash-not-record
schemaApi:
  sources: ["schema :Spot as { x: :Quantity(m) between -10m and 10m step 0.5m, inner: :Inner }\nschema :Inner as { flag: :Boolean }\nhandle @Spotted of :Spot as { id: :Number integer between 0 and 9, version: :ContentHash }\n"]
  handle: "Spotted"
  contentHash: {"type": ":Spot", "entries": [{"key": "x", "value": {"type": ":Quantity.binary64", "value": "1.5", "unit": "m"}}, {"key": "inner", "value": {"type": ":Text", "value": "flag"}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle Spotted\nid Number integer between 0e0 9e0\nversion ContentHash\n", "hash": "1a4fd25535677a5d"}, "contentHash": {"error": {"code": "wire.typeMismatch", "path": "inner"}}}
```

---

## Test: a type without a content hash

Any handle type's state can be hashed.

### Case description

```yaml
gesBlock: case
id: content-hash-unversioned
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\n"]
  handle: "Target"
  contentHash: {"type": ":Contact", "entries": [{"key": "label", "value": {"type": ":Text", "value": "Alpha"}}, {"key": "strength", "value": {"type": ":Number.int64", "value": "42"}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle Target\nid Number integer between 0e0 65535e0\nversion Nothing\n", "hash": "dd9da4ea4ed63220"}, "contentHash": "a2951261887894b7bc7f72d4af023c7b7576677f76283d8a524f2496b19cdc50"}
```

---

## Test: a composite, a union and a revision on the wire

A composite sends its root's id and version and its part; a union its alternative; a revision as an integer.

### Case description

```yaml
gesBlock: case
id: handle-encode-composite
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 100 }\nschema :Contact as { label: :Text max 16 }\nschema :Composition as { name: :Text max 64 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @UnitFile of :Composition composite @Repository as { part: :Text max 256 }\nschema :Sighting as {\n  target: @Target\n  file: @UnitFile~\n  subject: @Drone | @Target\n  pilot: @Drone~ | :Nothing\n}\nschema :Memory as { pinned: :Map(@Drone → @Unit~ | :Nothing) max 2 }\n"]
  root: "Sighting"
  encode: {"type": ":Sighting", "entries": [{"key": "target", "value": {"type": ":Handle", "value": "@Target:3"}}, {"key": "file", "value": {"type": ":Handle", "value": "@UnitFile:content~3f2a9c41d0e7b85a6c1f4e2d9b0a7c3e5f8d1b24/Units/radar.gesd"}}, {"key": "subject", "value": {"type": ":Handle", "value": "@Drone:7"}}, {"key": "pilot", "value": {"type": ":Handle", "value": "@Drone:7~120"}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"encoded": {"bytes": "030087b13737ba3237ba1f15ce20e8735c2db60fa7964d053e9fafc60d1208556e6974732f72616461722e676573640e1ce001"}}
```

---

## Test: decoding a composite, a union and a revision

Decoding gives the composite with its root's version and the alternative's handle.

### Case description

```yaml
gesBlock: case
id: handle-decode-composite
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 100 }\nschema :Contact as { label: :Text max 16 }\nschema :Composition as { name: :Text max 64 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @UnitFile of :Composition composite @Repository as { part: :Text max 256 }\nschema :Sighting as {\n  target: @Target\n  file: @UnitFile~\n  subject: @Drone | @Target\n  pilot: @Drone~ | :Nothing\n}\nschema :Memory as { pinned: :Map(@Drone → @Unit~ | :Nothing) max 2 }\n"]
  root: "Sighting"
  decode: "030087b13737ba3237ba1f15ce20e8735c2db60fa7964d053e9fafc60d1208556e6974732f72616461722e676573640e1ce001"
```

### Expectation

```yaml
gesBlock: expect
schema: {"decoded": {"value": {"type": ":Sighting", "entries": [{"key": "target", "value": {"type": ":Handle", "value": "@Target:3"}}, {"key": "file", "value": {"type": ":Handle", "value": "@UnitFile:content~3f2a9c41d0e7b85a6c1f4e2d9b0a7c3e5f8d1b24/Units/radar.gesd"}}, {"key": "subject", "value": {"type": ":Handle", "value": "@Drone:7"}}, {"key": "pilot", "value": {"type": ":Handle", "value": "@Drone:7~120"}}]}, "byteCount": 51}}
```

---

## Test: a handle union without its optional alternative

An absent union value sends its Nothing alternative.

### Case description

```yaml
gesBlock: case
id: handle-encode-union-nothing
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 100 }\nschema :Contact as { label: :Text max 16 }\nschema :Composition as { name: :Text max 64 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @UnitFile of :Composition composite @Repository as { part: :Text max 256 }\nschema :Sighting as {\n  target: @Target\n  file: @UnitFile~\n  subject: @Drone | @Target\n  pilot: @Drone~ | :Nothing\n}\nschema :Memory as { pinned: :Map(@Drone → @Unit~ | :Nothing) max 2 }\n"]
  root: "Sighting"
  encode: {"type": ":Sighting", "entries": [{"key": "target", "value": {"type": ":Handle", "value": "@Target:3"}}, {"key": "file", "value": {"type": ":Handle", "value": "@UnitFile:content~3f2a9c41d0e7b85a6c1f4e2d9b0a7c3e5f8d1b24/Units/radar.gesd"}}, {"key": "subject", "value": {"type": ":Handle", "value": "@Drone:7"}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"encoded": {"bytes": "030087b13737ba3237ba1f15ce20e8735c2db60fa7964d053e9fafc60d1208556e6974732f72616461722e676573640e02"}}
```

---

## Test: a delta of handles

A changed part, alternative and revision send only those parts.

### Case description

```yaml
gesBlock: case
id: handle-delta-encode
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 100 }\nschema :Contact as { label: :Text max 16 }\nschema :Composition as { name: :Text max 64 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @UnitFile of :Composition composite @Repository as { part: :Text max 256 }\nschema :Sighting as {\n  target: @Target\n  file: @UnitFile~\n  subject: @Drone | @Target\n  pilot: @Drone~ | :Nothing\n}\nschema :Memory as { pinned: :Map(@Drone → @Unit~ | :Nothing) max 2 }\n"]
  root: "Sighting"
  deltaEncode:
    record: "030087b13737ba3237ba1f15ce20e8735c2db60fa7964d053e9fafc60d1208556e6974732f736f6e61722e6765736413001ce401"
    baseline: "030087b13737ba3237ba1f15ce20e8735c2db60fa7964d053e9fafc60d1208556e6974732f72616461722e676573640e1ce001"
```

### Expectation

```yaml
gesBlock: expect
schema: {"deltaEncoded": {"bytes": "4e08556e6974732f736f6e61722e6765736413009807"}}
```

---

## Test: a delta of handles gives the record back

The delta of handles reads into the changed record.

### Case description

```yaml
gesBlock: case
id: handle-delta-decode
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 100 }\nschema :Contact as { label: :Text max 16 }\nschema :Composition as { name: :Text max 64 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @UnitFile of :Composition composite @Repository as { part: :Text max 256 }\nschema :Sighting as {\n  target: @Target\n  file: @UnitFile~\n  subject: @Drone | @Target\n  pilot: @Drone~ | :Nothing\n}\nschema :Memory as { pinned: :Map(@Drone → @Unit~ | :Nothing) max 2 }\n"]
  root: "Sighting"
  deltaDecode:
    delta: "4e08556e6974732f736f6e61722e6765736413009807"
    baseline: "030087b13737ba3237ba1f15ce20e8735c2db60fa7964d053e9fafc60d1208556e6974732f72616461722e676573640e1ce001"
```

### Expectation

```yaml
gesBlock: expect
schema: {"deltaDecoded": {"record": "030087b13737ba3237ba1f15ce20e8735c2db60fa7964d053e9fafc60d1208556e6974732f736f6e61722e6765736413001ce401", "deltaByteCount": 22}}
```

---

## Test: a handle Map with pinned values

A handle Map's values may be pinned handles or nothing.

### Case description

```yaml
gesBlock: case
id: handle-map-pinned-encode
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 100 }\nschema :Contact as { label: :Text max 16 }\nschema :Composition as { name: :Text max 64 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @UnitFile of :Composition composite @Repository as { part: :Text max 256 }\nschema :Sighting as {\n  target: @Target\n  file: @UnitFile~\n  subject: @Drone | @Target\n  pilot: @Drone~ | :Nothing\n}\nschema :Memory as { pinned: :Map(@Drone → @Unit~ | :Nothing) max 2 }\n"]
  root: "Memory"
  encode: {"type": ":Memory", "entries": [{"key": "pinned", "value": {"type": ":Map", "entries": [{"handle": "@Drone:200", "value": {"type": ":Nothing"}}, {"handle": "@Drone:4", "value": {"type": ":Handle", "value": "@Unit:drone.gnabblo~a2951261887894b7bc7f72d4af023c7b7576677f76283d8a524f2496b19cdc50"}}]}}]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"encoded": {"bytes": "126890c9bdb995b99cb9858989b1bd89564a8421e251def2fec951bf0af0ecd5d99dfdd9a1f4284a3d9158c67272432107"}}
```

---

## Test: decoding a handle Map with pinned values

Decoding gives the entries in key order.

### Case description

```yaml
gesBlock: case
id: handle-map-pinned-decode
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 100 }\nschema :Contact as { label: :Text max 16 }\nschema :Composition as { name: :Text max 64 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @UnitFile of :Composition composite @Repository as { part: :Text max 256 }\nschema :Sighting as {\n  target: @Target\n  file: @UnitFile~\n  subject: @Drone | @Target\n  pilot: @Drone~ | :Nothing\n}\nschema :Memory as { pinned: :Map(@Drone → @Unit~ | :Nothing) max 2 }\n"]
  root: "Memory"
  decode: "126890c9bdb995b99cb9858989b1bd89564a8421e251def2fec951bf0af0ecd5d99dfdd9a1f4284a3d9158c67272432107"
```

### Expectation

```yaml
gesBlock: expect
schema: {"decoded": {"value": {"type": ":Memory", "entries": [{"key": "pinned", "value": {"type": ":Map", "entries": [{"handle": "@Drone:4", "value": {"type": ":Handle", "value": "@Unit:drone.gnabblo~a2951261887894b7bc7f72d4af023c7b7576677f76283d8a524f2496b19cdc50"}}, {"handle": "@Drone:200", "value": {"type": ":Nothing"}}]}}]}, "byteCount": 49}}
```

---

## Test: a document with handles

Document checking accepts handles of the field's type, a pinned handle with a version and a handle Map. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: document-handles
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 100 }\nschema :Contact as { label: :Text max 16 }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nschema :Sighting as {\n  target: @Target\n  pilot: @Drone~ | :Nothing\n  seen: :Map(@Drone → :Boolean) max 2\n}\n"]
  document: ":Schema(:Sighting)\ntarget: @Target:9\npilot: @Drone:4~7\nseen: [@Drone:1: true, @Drone:2: false]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a document handle of another type

A handle whose type is not the field's is `document.fieldType` at the handle. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: document-handle-type
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 100 }\nschema :Contact as { label: :Text max 16 }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nschema :Sighting as {\n  target: @Target\n  pilot: @Drone~ | :Nothing\n  seen: :Map(@Drone → :Boolean) max 2\n}\n"]
  document: ":Schema(:Sighting)\ntarget: @Drone:9\nseen: []\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.fieldType"
      line: 2
      column: 9
```

---

## Test: a document handle id outside its range

An id outside the id's range is `document.outOfRange` at the handle. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: document-handle-range
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 100 }\nschema :Contact as { label: :Text max 16 }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nschema :Sighting as {\n  target: @Target\n  pilot: @Drone~ | :Nothing\n  seen: :Map(@Drone → :Boolean) max 2\n}\n"]
  document: ":Schema(:Sighting)\ntarget: @Target:70000\nseen: []\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.outOfRange"
      line: 2
      column: 9
```

---

## Test: a document pinned handle without a version

A pinned field needs a version or a ref. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: document-handle-unpinned
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 100 }\nschema :Contact as { label: :Text max 16 }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nschema :Sighting as {\n  target: @Target\n  pilot: @Drone~ | :Nothing\n  seen: :Map(@Drone → :Boolean) max 2\n}\n"]
  document: ":Schema(:Sighting)\ntarget: @Target:9\npilot: @Drone:4\nseen: []\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.fieldType"
      line: 3
      column: 8
```

---

## Test: a document version of a type without versions

A type with `version: :Nothing` takes no version. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: document-handle-unversioned
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 100 }\nschema :Contact as { label: :Text max 16 }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nschema :Sighting as {\n  target: @Target\n  pilot: @Drone~ | :Nothing\n  seen: :Map(@Drone → :Boolean) max 2\n}\n"]
  document: ":Schema(:Sighting)\ntarget: @Target:9~3\nseen: []\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.fieldType"
      line: 2
      column: 9
```

---

## Test: a document handle Map with a Text key

Every key of a handle Map is a handle of its key type. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: document-handle-map-key
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 100 }\nschema :Contact as { label: :Text max 16 }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nschema :Sighting as {\n  target: @Target\n  pilot: @Drone~ | :Nothing\n  seen: :Map(@Drone → :Boolean) max 2\n}\n"]
  document: ":Schema(:Sighting)\ntarget: @Target:9\nseen: [a: true]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.fieldType"
      line: 3
      column: 8
```

---

## Test: a document version on an unpinned field

Only an `@Name~` field takes a version; one on an `@Name` field is `document.unexpectedVersion` at the handle. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: document-handle-unexpected-version
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 100 }\nschema :Contact as { label: :Text max 16 }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nschema :Escort as {\n  target: @Target\n  escort: @Drone optional\n  pilot: @Drone~ optional\n  wing: :Map(@Target → @Drone) max 2\n  free: :Map optional\n}\n"]
  document: ":Schema(:Escort)\ntarget: @Target:9\nescort: @Drone:4~7\nwing: [:]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.unexpectedVersion"
      line: 3
      column: 9
```

---

## Test: a document ref on an unpinned field

A ref is a version named through the owner, so an `@Name` field takes none either. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: document-handle-unexpected-ref
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 100 }\nschema :Contact as { label: :Text max 16 }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nschema :Escort as {\n  target: @Target\n  escort: @Drone optional\n  pilot: @Drone~ optional\n  wing: :Map(@Target → @Drone) max 2\n  free: :Map optional\n}\n"]
  document: ":Schema(:Escort)\ntarget: @Target:9\nescort: @Drone:4~'feature/x'\nwing: [:]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.unexpectedVersion"
      line: 3
      column: 9
```

---

## Test: a document ref on a pinned field

A pinned field takes a tag ref and a quoted ref as well as a version. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: document-handle-pinned-ref
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 100 }\nschema :Contact as { label: :Text max 16 }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nschema :Escort as {\n  target: @Target\n  escort: @Drone optional\n  pilot: @Drone~ optional\n  wing: :Map(@Target → @Drone) max 2\n  free: :Map optional\n}\n"]
  document: ":Schema(:Escort)\ntarget: @Target:9\nescort: @Drone:4\npilot: @Drone:4~#live\nwing: [@Target:1: @Drone:2, @Target:2: @Drone:3]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a document version in a handle Map value

The values of a handle Map are checked like every other field: a version on an `@Name` value is `document.unexpectedVersion`. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: document-handle-map-value-version
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 100 }\nschema :Contact as { label: :Text max 16 }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nschema :Escort as {\n  target: @Target\n  escort: @Drone optional\n  pilot: @Drone~ optional\n  wing: :Map(@Target → @Drone) max 2\n  free: :Map optional\n}\n"]
  document: ":Schema(:Escort)\ntarget: @Target:9\nwing: [@Target:1: @Drone:2~5]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.unexpectedVersion"
      line: 3
      column: 19
```

---

## Test: a document version in a free Map

A free `:Map` holds data without a field type, so a handle with a version passes there. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: document-handle-free-map
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 100 }\nschema :Contact as { label: :Text max 16 }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nschema :Escort as {\n  target: @Target\n  escort: @Drone optional\n  pilot: @Drone~ optional\n  wing: :Map(@Target → @Drone) max 2\n  free: :Map optional\n}\n"]
  document: ":Schema(:Escort)\ntarget: @Target:9\nwing: [:]\nfree: [a: @Drone:2~5]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a document handle Map takes the alternatives its check chose

A handle Map orders its keys, so each value takes the alternative the check chose for its key, not for its place in the
document. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: read-document-handle-map-union
schemaApi:
  sources: ["schema :DroneState as { x: :Number integer between 0 and 100 }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Nothing }\nschema :Small as { a: :Number integer }\nschema :Large as { a: :Number integer, b: :Text max 4 }\nschema :Fleet as { crew: :Map(@Drone → :Small | :Large) max 4 }\n"]
  readDocument:
    text: ":Schema(:Fleet)\ncrew: [@Drone:9: [a: 1, b: 'x'], @Drone:2: [a: 2]]\n"
    expected: ["Fleet"]
```

### Expectation

```yaml
gesBlock: expect
schema:
  readDocument:
    schema: "Fleet"
    value:
      type: ":Fleet"
      entries:
        - key: "crew"
          value:
            type: ":Map"
            entries:
              - handle: "@Drone:2"
                value:
                  type: ":Small"
                  entries:
                    - key: "a"
                      value:
                        type: ":Number.int64"
                        value: "2"
              - handle: "@Drone:9"
                value:
                  type: ":Large"
                  entries:
                    - key: "a"
                      value:
                        type: ":Number.int64"
                        value: "1"
                    - key: "b"
                      value:
                        type: ":Text"
                        value: "x"
```

---

## Test: the guide's document of handles

The document of the [handles guide](../../../docs/guide/Handles.md#write-handles-as-text-and-in-documents) passes its
schema: its ref stands in an `@Unit~` field. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: document-handles-guide
schemaApi:
  sources: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nschema :Composition as { name: :Text max 64, slots: :Number integer between 0 and 16 }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nschema :Sighting as {\n  target: @Target\n  seen: :List(@Target) max 16\n  unit: @Unit~ optional\n  memory: :Map(@Target → :Contact) max 64\n}\n"]
  document: ":Schema(:Sighting)\ntarget: @Target:3\nseen: [@Target:3, @Target:17]\nunit: @Unit:drone.gnabblo~#live\nmemory: [\n  @Target:3: [label: 'Alpha', strength: 42]\n]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a document ref in a List element

The elements of a `:List(@Name)` take neither a version nor a ref: the ref the element names is `document.unexpectedVersion` at the element. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: document-handle-list-element-ref
schemaApi:
  sources: ["schema :Composition as { name: :Text max 64 }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nschema :Slot as { unit: @Unit }\nschema :Squad as {\n  parts: :List(@Unit) max 4\n  pinned: :List(@Unit~) max 4 optional\n  either: @Unit | :Number optional\n  slot: :Slot optional\n  slots: :List(:Slot) max 2 optional\n  byUnit: :Map(@Unit → :Number) max 2 optional\n}\n"]
  document: ":Schema(:Squad)\nparts: [@Unit:x~#live]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.unexpectedVersion"
      line: 2
      column: 9
```

---

## Test: a document version in a List element

A version on an element of a `:List(@Name)` is `document.unexpectedVersion` as well. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: document-handle-list-element-version
schemaApi:
  sources: ["schema :Composition as { name: :Text max 64 }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nschema :Slot as { unit: @Unit }\nschema :Squad as {\n  parts: :List(@Unit) max 4\n  pinned: :List(@Unit~) max 4 optional\n  either: @Unit | :Number optional\n  slot: :Slot optional\n  slots: :List(:Slot) max 2 optional\n  byUnit: :Map(@Unit → :Number) max 2 optional\n}\n"]
  document: ":Schema(:Squad)\nparts: [@Unit:x, @Unit:y~'1.0']\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.unexpectedVersion"
      line: 2
      column: 18
```

---

## Test: a document ref in a union alternative

A handle alternative of a union is checked as the field type it names: `@Unit` takes no ref, so the alternative stays unsatisfied, and a value that satisfies no alternative is `document.fieldType`. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: document-handle-union-ref
schemaApi:
  sources: ["schema :Composition as { name: :Text max 64 }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nschema :Slot as { unit: @Unit }\nschema :Squad as {\n  parts: :List(@Unit) max 4\n  pinned: :List(@Unit~) max 4 optional\n  either: @Unit | :Number optional\n  slot: :Slot optional\n  slots: :List(:Slot) max 2 optional\n  byUnit: :Map(@Unit → :Number) max 2 optional\n}\n"]
  document: ":Schema(:Squad)\nparts: []\neither: @Unit:x~#live\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.fieldType"
      line: 3
      column: 9
```

---

## Test: a document ref in a nested record

A handle field of a nested record is checked against its own declaration. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: document-handle-nested-ref
schemaApi:
  sources: ["schema :Composition as { name: :Text max 64 }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nschema :Slot as { unit: @Unit }\nschema :Squad as {\n  parts: :List(@Unit) max 4\n  pinned: :List(@Unit~) max 4 optional\n  either: @Unit | :Number optional\n  slot: :Slot optional\n  slots: :List(:Slot) max 2 optional\n  byUnit: :Map(@Unit → :Number) max 2 optional\n}\n"]
  document: ":Schema(:Squad)\nparts: []\nslot: [unit: @Unit:x~#live]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.unexpectedVersion"
      line: 3
      column: 14
```

---

## Test: a document ref in a record of a List

So is the handle field of a record in a List. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: document-handle-nested-list-ref
schemaApi:
  sources: ["schema :Composition as { name: :Text max 64 }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nschema :Slot as { unit: @Unit }\nschema :Squad as {\n  parts: :List(@Unit) max 4\n  pinned: :List(@Unit~) max 4 optional\n  either: @Unit | :Number optional\n  slot: :Slot optional\n  slots: :List(:Slot) max 2 optional\n  byUnit: :Map(@Unit → :Number) max 2 optional\n}\n"]
  document: ":Schema(:Squad)\nparts: []\nslots: [[unit: @Unit:x], [unit: @Unit:y~#live]]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.unexpectedVersion"
      line: 3
      column: 33
```

---

## Test: a document ref on a handle Map key

A handle key carries only the identity: reading already rejects a key with a version or ref as `document.invalidHandle` at the `@`. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: document-handle-map-key-ref
schemaApi:
  sources: ["schema :Composition as { name: :Text max 64 }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nschema :Slot as { unit: @Unit }\nschema :Squad as {\n  parts: :List(@Unit) max 4\n  pinned: :List(@Unit~) max 4 optional\n  either: @Unit | :Number optional\n  slot: :Slot optional\n  slots: :List(:Slot) max 2 optional\n  byUnit: :Map(@Unit → :Number) max 2 optional\n}\n"]
  document: ":Schema(:Squad)\nparts: []\nbyUnit: [@Unit:x~#live: 1]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.invalidHandle"
      line: 3
      column: 10
```

---

## Test: document refs in a pinned List

The elements of a `:List(@Name~)` take versions and refs, and the unpinned fields beside them take bare handles. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: document-handle-pinned-list-ref
schemaApi:
  sources: ["schema :Composition as { name: :Text max 64 }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nschema :Slot as { unit: @Unit }\nschema :Squad as {\n  parts: :List(@Unit) max 4\n  pinned: :List(@Unit~) max 4 optional\n  either: @Unit | :Number optional\n  slot: :Slot optional\n  slots: :List(:Slot) max 2 optional\n  byUnit: :Map(@Unit → :Number) max 2 optional\n}\n"]
  document: ":Schema(:Squad)\nparts: [@Unit:x]\npinned: [@Unit:x~#live, @Unit:y~'1.0']\neither: @Unit:z\nslot: [unit: @Unit:x]\nbyUnit: [@Unit:x: 1]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: reading a model document with a ref in a List element

Reading a document into a model checks it as `CheckDocument` does, so the ref in an element of a `:List(@Name)` is
`document.unexpectedVersion` there too, beside a pinned field that takes the same ref. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: read-document-handle-list-element-ref
schemaApi:
  sources: ["schema :Composition as { name: :Text max 64 }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nschema :DroneState as { x: :Number integer between 0 and 9, unit: @Unit }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Nothing }\nmodel(commands) schema :Move as { drone: @Drone, unit: @Unit~, parts: :List(@Unit) max 4 }\n"]
  readDocument:
    text: ":Schema(:Move)\ndrone: @Drone:3\nunit: @Unit:x~#live\nparts: [@Unit:x~#live]\n"
    expected: ["Move"]
```

### Expectation

```yaml
gesBlock: expect
schema:
  readDocument:
    error:
      code: "document.unexpectedVersion"
      line: 4
      column: 9
      path: "parts[]"
```

---

## Test: the canonical form of a store file type

A store file type writes a `store` line with the handle type of its store after its `handle` line. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: store-file-canonical
schemaApi:
  sources: ["schema :Composition as { name: :Text max 16 }\nhandle @Unit of :Composition as { id: :Text max 16, version: :ContentHash }\nschema :Source as { text: :Text }\nhandle @Repository of :Source as { id: :Text max 16, version: :Hash(#sha1) }\nhandle @UnitFiles of :Store(@Unit) composite @Repository as { part: :Text max 64 }\n"]
  handle: "UnitFiles"
```

### Expectation

```yaml
gesBlock: expect
schema: {"handle": {"canonical": "gess1\nhandle UnitFiles composite Repository\nstore Unit\npart Text max 64\nhandle Repository\nid Text max 16\nversion Hash(#sha1)\n", "hash": "e20c20e2054996ff"}}
```

---

## Test: a store file type that is no composite

A store file lies below a root, so its type must be composite. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: store-file-not-composite
schemaApi:
  sources: ["schema :Composition as { name: :Text max 16 }\nhandle @Unit of :Composition as { id: :Text max 16, version: :ContentHash }\nschema :Source as { text: :Text }\nhandle @Repository of :Source as { id: :Text max 16, version: :Hash(#sha1) }\nhandle @UnitFiles of :Store(@Unit) as { id: :Text max 64, version: :Nothing }\n"]
  handle: "UnitFiles"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidHandle"}}
```

---

## Test: a store file type without a part

A store file type names a file by its path, so a part-less composite is none. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: store-file-partless
schemaApi:
  sources: ["schema :Composition as { name: :Text max 16 }\nhandle @Unit of :Composition as { id: :Text max 16, version: :ContentHash }\nschema :Source as { text: :Text }\nhandle @Repository of :Source as { id: :Text max 16, version: :Hash(#sha1) }\nhandle @UnitFiles of :Store(@Unit) composite @Repository\n"]
  handle: "UnitFiles"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidHandle"}}
```

---

## Test: a store file type with a Number part

The part of a store file type is its path and must be Text. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: store-file-number-part
schemaApi:
  sources: ["schema :Composition as { name: :Text max 16 }\nhandle @Unit of :Composition as { id: :Text max 16, version: :ContentHash }\nschema :Source as { text: :Text }\nhandle @Repository of :Source as { id: :Text max 16, version: :Hash(#sha1) }\nhandle @UnitFiles of :Store(@Unit) composite @Repository as { part: :Number integer between 0 and 9 }\n"]
  handle: "UnitFiles"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidHandle"}}
```

---

## Test: only `:Store` takes a handle type as state

A state written with an argument names a type only as `:Store(@Type)`; any other name, such as the removed
`:Catalog(@Unit)`, is an unknown type, `schema.unknownName` at the name. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: store-file-unknown-state-type
schemaApi:
  sources: ["schema :Composition as { name: :Text max 16 }\nhandle @Unit of :Composition as { id: :Text max 16, version: :ContentHash }\nschema :Source as { text: :Text }\nhandle @Repository of :Source as { id: :Text max 16, version: :Hash(#sha1) }\nhandle @UnitFiles of :Catalog(@Unit) composite @Repository as { part: :Text max 64 }\n"]
  handle: "UnitFiles"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.unknownName"}}
```

---

## Test: a store of an undeclared handle type

Naming an undeclared handle type as a store is `schema.unknownName`, as for any other use. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: store-file-unknown-target
schemaApi:
  sources: ["schema :Composition as { name: :Text max 16 }\nhandle @Unit of :Composition as { id: :Text max 16, version: :ContentHash }\nschema :Source as { text: :Text }\nhandle @Repository of :Source as { id: :Text max 16, version: :Hash(#sha1) }\nhandle @UnitFiles of :Store(@Ghost) composite @Repository as { part: :Text max 64 }\n"]
  handle: "UnitFiles"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.unknownName"}}
```

---

## Test: a store of a handle type with a Number id

Store entries are keyed by Text, so the store's handle type needs a Text id. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: store-file-number-target
schemaApi:
  sources: ["schema :Composition as { name: :Text max 16 }\nhandle @Unit of :Composition as { id: :Text max 16, version: :ContentHash }\nschema :Source as { text: :Text }\nhandle @Repository of :Source as { id: :Text max 16, version: :Hash(#sha1) }\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 99, version: :Nothing }\nhandle @TargetFiles of :Store(@Target) composite @Repository as { part: :Text max 64 }\n"]
  handle: "TargetFiles"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidHandle"}}
```

---

## Test: a store of a composite handle type

A store holds the states of a handle type of its own, never of a composite. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: store-file-composite-target
schemaApi:
  sources: ["schema :Composition as { name: :Text max 16 }\nhandle @Unit of :Composition as { id: :Text max 16, version: :ContentHash }\nschema :Source as { text: :Text }\nhandle @Repository of :Source as { id: :Text max 16, version: :Hash(#sha1) }\nhandle @UnitFile of :Composition composite @Repository as { part: :Text max 64 }\nhandle @UnitFiles of :Store(@UnitFile) composite @Repository as { part: :Text max 64 }\n"]
  handle: "UnitFiles"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidHandle"}}
```

---

## Test: a store of store files

A store file type is no store type itself. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: store-file-file-target
schemaApi:
  sources: ["schema :Composition as { name: :Text max 16 }\nhandle @Unit of :Composition as { id: :Text max 16, version: :ContentHash }\nschema :Source as { text: :Text }\nhandle @Repository of :Source as { id: :Text max 16, version: :Hash(#sha1) }\nhandle @UnitFiles of :Store(@Unit) composite @Repository as { part: :Text max 64 }\nhandle @Deeper of :Store(@UnitFiles) composite @Repository as { part: :Text max 64 }\n"]
  handle: "Deeper"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidHandle"}}
```

---

## Test: a store file handle as a field type

Store file types serve only reading; a field cannot hold one. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: store-file-field
schemaApi:
  sources: ["schema :Composition as { name: :Text max 16 }\nhandle @Unit of :Composition as { id: :Text max 16, version: :ContentHash }\nschema :Source as { text: :Text }\nhandle @Repository of :Source as { id: :Text max 16, version: :Hash(#sha1) }\nhandle @UnitFiles of :Store(@Unit) composite @Repository as { part: :Text max 64 }\nwire schema :S as { file: @UnitFiles }\n"]
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidHandle"}}
```

---

## Test: a store file handle as a Map key

A Map cannot be keyed by a store file type either. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: store-file-map-key
schemaApi:
  sources: ["schema :Composition as { name: :Text max 16 }\nhandle @Unit of :Composition as { id: :Text max 16, version: :ContentHash }\nschema :Source as { text: :Text }\nhandle @Repository of :Source as { id: :Text max 16, version: :Hash(#sha1) }\nhandle @UnitFiles of :Store(@Unit) composite @Repository as { part: :Text max 64 }\nwire schema :S as { files: :Map(@UnitFiles → :Number integer between 0 and 9) max 4 }\n"]
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidHandle"}}
```
