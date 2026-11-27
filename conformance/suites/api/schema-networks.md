---
formatVersion: 1
suiteId: api.schema-networks
title: GES network declarations
kind: schemaApi
level: atomic
categories: [conformance]
tags: [portable-api, schemas, network]
---

# GES network declarations

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies `network` declarations in `.gess` sources: reading, route rules, rates, the canonical form and the
network hash ([GES network](../../../specs/Network.md#declaration)).

---

## Test: the canonical form and hash

Each route line names side, verb, root, class, rate in microseconds and the root's binding hash; a route's position is its index.

### Case description

```yaml
gesBlock: case
id: network-canonical
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  client sends Commands reliable\n  server publishes :World latest at most every 50ms\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"network": {"canonical": "gesn1\nnetwork Game\nroute client sends Commands reliable 0 7f45f3e117777670\nroute server publishes World latest 50000 777b346c4d18936f\n", "hash": "f8fa17ba7d5cebcc"}}
```

---

## Test: route order changes the hash

The same routes in another order give another canonical form and hash.

### Case description

```yaml
gesBlock: case
id: network-order
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes :World latest at most every 50ms\n  client sends Commands reliable\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"network": {"canonical": "gesn1\nnetwork Game\nroute server publishes World latest 50000 777b346c4d18936f\nroute client sends Commands reliable 0 7f45f3e117777670\n", "hash": "9b2c4a133121f1fc"}}
```

---

## Test: commas separate routes

Routes may share a line separated by commas; repeated and trailing separators are allowed.

### Case description

```yaml
gesBlock: case
id: network-separators
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as { client sends Commands reliable,, server publishes :World reliable, }\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"network": {"canonical": "gesn1\nnetwork Game\nroute client sends Commands reliable 0 7f45f3e117777670\nroute server publishes World reliable 0 777b346c4d18936f\n", "hash": "1186b072829f3182"}}
```

---

## Test: a rate rounds up to microseconds

33.3333ms is 33333.3 microseconds and rounds up to 33334.

### Case description

```yaml
gesBlock: case
id: network-rate-rounding
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes :World latest at most every 33.3333ms\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"network": {"canonical": "gesn1\nnetwork Game\nroute server publishes World latest 33334 777b346c4d18936f\n", "hash": "966e9fd17169fe3b"}}
```

---

## Test: a rate may be a constant

A file constant with a time value gives the rate; seconds and milliseconds give the same microseconds.

### Case description

```yaml
gesBlock: case
id: network-rate-constant
schemaApi:
  sources: ["constant $tick be 0.25s\nwire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes :World latest at most every $tick\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"network": {"canonical": "gesn1\nnetwork Game\nroute server publishes World latest 250000 777b346c4d18936f\n", "hash": "3b41f3168fbeb4ac"}}
```

---

## Test: the largest rate

3600s is the largest rate.

### Case description

```yaml
gesBlock: case
id: network-rate-maximum
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes :World latest at most every 3600s\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"network": {"canonical": "gesn1\nnetwork Game\nroute server publishes World latest 3600000000 777b346c4d18936f\n", "hash": "a99484724b46d860"}}
```

---

## Test: parameter values change the hash

Every root binds with the same parameter values; they enter the network hash through the binding hash.

### Case description

```yaml
gesBlock: case
id: network-parameters
schemaApi:
  sources: ["parameter top: :Number\nwire schema :World as {\n  tick: :Number integer between 0 and top\n}\nnetwork :Game as {\n  server publishes :World latest\n}\n"]
  parameters:
    top: "255"
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"network": {"canonical": "gesn1\nnetwork Game\nroute server publishes World latest 0 33a0441c35b8375e\n", "hash": "754c5e918b47ad2a"}}
```

---

## Test: a network names roots of other sources

Names are shared across all sources.

### Case description

```yaml
gesBlock: case
id: network-sources
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\n", "network :Game as {\n  client sends Commands bulk\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"network": {"canonical": "gesn1\nnetwork Game\nroute client sends Commands bulk 0 7f45f3e117777670\n", "hash": "6b8524a6d44fc3d5"}}
```

---

## Test: an unknown root

### Case description

```yaml
gesBlock: case
id: network-unknown-root
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  client sends :Missing reliable\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.unknownName", "line": 7, "column": 16}}
```

---

## Test: a root without a wire role

### Case description

```yaml
gesBlock: case
id: network-no-wire-role
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nschema :Plain as { a: :Boolean }\nnetwork :Game as {\n  server publishes :Plain latest\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidRoute", "line": 8, "column": 20}}
```

---

## Test: only a schema is published

### Case description

```yaml
gesBlock: case
id: network-publishes-messages
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes Commands reliable\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidRoute", "line": 7, "column": 20}}
```

---

## Test: a topic is not bulk

### Case description

```yaml
gesBlock: case
id: network-publishes-bulk
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes :World bulk\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidRoute", "line": 7, "column": 27}}
```

---

## Test: a sent root is not latest

### Case description

```yaml
gesBlock: case
id: network-sends-latest
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  client sends Commands latest\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidRoute", "line": 7, "column": 25}}
```

---

## Test: only a topic has a rate

### Case description

```yaml
gesBlock: case
id: network-rate-on-sends
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  client sends Commands reliable at most every 1s\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidRoute", "line": 7, "column": 48}}
```

---

## Test: a rate is greater than zero

### Case description

```yaml
gesBlock: case
id: network-rate-zero
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes :World latest at most every 0s\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidRoute", "line": 7, "column": 48}}
```

---

## Test: a rate is at most 3600s

### Case description

```yaml
gesBlock: case
id: network-rate-too-large
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes :World latest at most every 3600.000001s\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidRoute", "line": 7, "column": 48}}
```

---

## Test: a rate is a time

### Case description

```yaml
gesBlock: case
id: network-rate-not-time
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes :World latest at most every 5m\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidRoute", "line": 7, "column": 48}}
```

---

## Test: a rate is no name

### Case description

```yaml
gesBlock: case
id: network-rate-name
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes :World latest at most every tick\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.syntax", "line": 7, "column": 48}}
```

---

## Test: a side names a root once per verb

### Case description

```yaml
gesBlock: case
id: network-duplicate-route
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  client sends Commands reliable\n  client sends Commands bulk\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.duplicateRoute", "line": 8, "column": 3}}
```

---

## Test: a network shares the namespace

### Case description

```yaml
gesBlock: case
id: network-duplicate-name
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :World as {\n  client sends Commands reliable\n}\n"]
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.duplicateName", "line": 6, "column": 1}}
```

---

## Test: a network takes no role

### Case description

```yaml
gesBlock: case
id: network-role
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nwire network :Game as {\n  client sends Commands reliable\n}\n"]
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidRole", "line": 6, "column": 1}}
```

---

## Test: a network has a route

### Case description

```yaml
gesBlock: case
id: network-empty
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as {\n}\n"]
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.syntax", "line": 7, "column": 1}}
```

---

## Test: a network has at most 64 routes

### Case description

```yaml
gesBlock: case
id: network-too-many
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n  client sends Commands reliable\n}\n"]
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.tooLarge", "line": 71, "column": 3}}
```

---

## Test: an unknown class

### Case description

```yaml
gesBlock: case
id: network-bad-class
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  client sends Commands fast\n}\n"]
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.syntax", "line": 7, "column": 25}}
```

---

## Test: a field cannot name a network

### Case description

```yaml
gesBlock: case
id: network-field-names-network
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  client sends Commands reliable\n}\nschema :Holder as { g: :Game }\n"]
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.unknownName", "line": 9, "column": 24}}
```

---

## Test: an unknown network

### Case description

```yaml
gesBlock: case
id: network-unknown-network
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages Commands as (Move)\nmessage Move()\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.unknownName", "line": 0, "column": 0}}
```

---

## Test: a route that names a message

`client sends Name` names a message.

### Case description

```yaml
gesBlock: case
id: network-message-route
schemaApi:
  sources: ["wire schema :Shield as { strength: :Number integer between 0 and 100 }\nwire message Shield(level: :Number integer between 0 and 10)\nnetwork :Game as {\n  client sends Shield reliable\n  server sends :Shield reliable\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"network": {"canonical": "gesn1\nnetwork Game\nroute client sends Shield reliable 0 9521a6a2e5eb94fb\nroute server sends Shield reliable 0 d40bdebbb3de923b\n", "hash": "1cb688722ef75d4f"}}
```

---

## Test: a route with a colon that names only a message

`:Name` routes a schema or set.

### Case description

```yaml
gesBlock: case
id: network-colon-message-route
schemaApi:
  sources: ["wire message Move(x: :Number integer between 0 and 255)\nnetwork :Game as {\n  client sends :Move reliable\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.unknownName", "line": 3, "column": 16}}
```

---

## Test: a route that names an unknown message

The message must be declared.

### Case description

```yaml
gesBlock: case
id: network-unknown-message-route
schemaApi:
  sources: ["wire message Move(x: :Number integer between 0 and 255)\nnetwork :Game as {\n  client sends Missing reliable\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.unknownName", "line": 3, "column": 16}}
```


---

## Test: owns routes after the routes

Each `owns` route writes one line after the routes, in ascending order of the handle type, with the side, the handle type's hash and its state schema's hash; it has no index.

### Case description

```yaml
gesBlock: case
id: network-owns
schemaApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nschema :Contact as { label: :Text max 16 }\nschema :Composition as { name: :Text max 64 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @UnitSlot of :Contact composite @Unit as { part: :Number integer between 0 and 15 }\nnetwork :Game as {\n  server publishes :World latest\n  server owns @Unit\n  client owns @Target\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"network": {"canonical": "gesn1\nnetwork Game\nroute server publishes World latest 0 777b346c4d18936f\nowns client Target dd9da4ea4ed63220 5d824ec98db00017\nowns server Unit 84dc8d47579275f1 a40cb19f52bc920b\n", "hash": "87c04ab685dd8d0d"}}
```

---

## Test: the state schema is part of the network hash

Another state schema changes the `owns` line and the network hash; compare the previous case.

### Case description

```yaml
gesBlock: case
id: network-owns-state
schemaApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nschema :Contact as { label: :Text max 32 }\nschema :Composition as { name: :Text max 64 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @UnitSlot of :Contact composite @Unit as { part: :Number integer between 0 and 15 }\nnetwork :Game as {\n  server publishes :World latest\n  server owns @Unit\n  client owns @Target\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"network": {"canonical": "gesn1\nnetwork Game\nroute server publishes World latest 0 777b346c4d18936f\nowns client Target dd9da4ea4ed63220 6f41f8c997dc4e29\nowns server Unit 84dc8d47579275f1 a40cb19f52bc920b\n", "hash": "5a21ea8473a6b7d9"}}
```

---

## Test: a composite has no owner of its own

An `owns` route of a composite type is `schema.invalidHandle`.

### Case description

```yaml
gesBlock: case
id: network-owns-composite
schemaApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nschema :Contact as { label: :Text max 16 }\nschema :Composition as { name: :Text max 64 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @UnitSlot of :Contact composite @Unit as { part: :Number integer between 0 and 15 }\nnetwork :Game as {\n  server publishes :World latest\n  server owns @UnitSlot\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidHandle", "line": 9, "column": 15}}
```

---

## Test: a type owned twice

A second `owns` of the same type is `schema.duplicateRoute`, even from the other side.

### Case description

```yaml
gesBlock: case
id: network-owns-twice
schemaApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nschema :Contact as { label: :Text max 16 }\nschema :Composition as { name: :Text max 64 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @UnitSlot of :Contact composite @Unit as { part: :Number integer between 0 and 15 }\nnetwork :Game as {\n  server publishes :World latest\n  server owns @Target\n  client owns @Target\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.duplicateRoute", "line": 10, "column": 3}}
```

---

## Test: an owned type that is not declared

The handle type must be declared.

### Case description

```yaml
gesBlock: case
id: network-owns-unknown
schemaApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nschema :Contact as { label: :Text max 16 }\nschema :Composition as { name: :Text max 64 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @UnitSlot of :Contact composite @Unit as { part: :Number integer between 0 and 15 }\nnetwork :Game as {\n  server publishes :World latest\n  server owns @Missing\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.unknownName", "line": 9, "column": 15}}
```

---

## Test: a network of owns routes alone

A network needs at least one route that sends or publishes.

### Case description

```yaml
gesBlock: case
id: network-owns-only
schemaApi:
  sources: ["wire schema :World as { tick: :Number integer between 0 and 65535 }\nschema :Contact as { label: :Text max 16 }\nschema :Composition as { name: :Text max 64 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @UnitSlot of :Contact composite @Unit as { part: :Number integer between 0 and 15 }\nnetwork :Game as {\n  server owns @Target\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.syntax", "line": 7, "column": 1}}
```

---

## Test: owns routes with a projection and sending state

`as :P` and `sending state` extend the `owns` line with the projection's name and schema hash and `sending`. Handles the owner sends read the projection or one that narrows it; a route towards the owner may carry the full view.

### Case description

```yaml
gesBlock: case
id: network-owns-projection
schemaApi:
  sources: ["schema :Composition as { name: :Text max 64, mass: :Number integer between 0 and 999 }\nschema :Contact as { label: :Text max 16 }\nprojection :UnitPublic of :Composition as { name }\nprojection :UnitName of :UnitPublic as { name }\nprojection :ContactLabel of :Contact as { label }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @Drone of :Composition as { id: :Number integer between 0 and 99, version: :Number integer between 0 and 65535 }\nwire schema :World as { units: :List(@Unit(:UnitPublic)) max 8, names: :List(@Unit(:UnitName)) max 8, drone: @Drone(:UnitPublic) }\nwire schema :Order as { unit: @Unit, drone: @Drone~ }\nnetwork :Game as {\n  server publishes :World latest\n  client sends :Order reliable\n  server owns @Unit as :UnitPublic sending state\n  server owns @Drone as :UnitPublic\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"network": {"canonical": "gesn1\nnetwork Game\nroute server publishes World latest 0 bd34856e6c61272a\nroute client sends Order reliable 0 36c5c80fa57d409d\nowns server Drone 7472eebcfd7fed1f c72fa4103594b877 as UnitPublic c4809d16c5c44c68\nowns server Unit 84dc8d47579275f1 c72fa4103594b877 as UnitPublic c4809d16c5c44c68 sending\n", "hash": "c838f5bcf06f1b45"}}
```

---

## Test: the projection is part of the network hash

Without `as` and `sending state` the `owns` line keeps its earlier form; compare the previous case.

### Case description

```yaml
gesBlock: case
id: network-owns-projection-hash
schemaApi:
  sources: ["schema :Composition as { name: :Text max 64, mass: :Number integer between 0 and 999 }\nschema :Contact as { label: :Text max 16 }\nprojection :UnitPublic of :Composition as { name }\nprojection :UnitName of :UnitPublic as { name }\nprojection :ContactLabel of :Contact as { label }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @Drone of :Composition as { id: :Number integer between 0 and 99, version: :Number integer between 0 and 65535 }\nwire schema :World as { units: :List(@Unit(:UnitPublic)) max 8, names: :List(@Unit(:UnitName)) max 8, drone: @Drone(:UnitPublic) }\nwire schema :Order as { unit: @Unit, drone: @Drone~ }\nnetwork :Game as {\n  server publishes :World latest\n  client sends :Order reliable\n  server owns @Unit\n  server owns @Drone\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"network": {"canonical": "gesn1\nnetwork Game\nroute server publishes World latest 0 bd34856e6c61272a\nroute client sends Order reliable 0 36c5c80fa57d409d\nowns server Drone 7472eebcfd7fed1f c72fa4103594b877\nowns server Unit 84dc8d47579275f1 c72fa4103594b877\n", "hash": "47c16d0cdd8a1a71"}}
```

---

## Test: the owner sends a full view to a projected side

A handle of the owned type in a route from its owner that reads more than the other side's projection is `schema.invalidProjection` at the route's root, also inside a List.

### Case description

```yaml
gesBlock: case
id: network-owns-projection-full-view
schemaApi:
  sources: ["schema :Composition as { name: :Text max 64, mass: :Number integer between 0 and 999 }\nschema :Contact as { label: :Text max 16 }\nprojection :UnitPublic of :Composition as { name }\nprojection :UnitName of :UnitPublic as { name }\nprojection :ContactLabel of :Contact as { label }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @Drone of :Composition as { id: :Number integer between 0 and 99, version: :Number integer between 0 and 65535 }\nwire schema :World as { units: :List(@Unit) max 8 }\nnetwork :Game as {\n  server publishes :World latest\n  server owns @Unit as :UnitPublic\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidProjection", "line": 10, "column": 20}}
```

---

## Test: the owner sends another projection

A projection the other side's projection does not narrow is `schema.invalidProjection`, even when it narrows the state.

### Case description

```yaml
gesBlock: case
id: network-owns-projection-other-view
schemaApi:
  sources: ["schema :Composition as { name: :Text max 64, mass: :Number integer between 0 and 999 }\nschema :Contact as { label: :Text max 16 }\nprojection :UnitPublic of :Composition as { name }\nprojection :UnitName of :UnitPublic as { name }\nprojection :ContactLabel of :Contact as { label }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @Drone of :Composition as { id: :Number integer between 0 and 99, version: :Number integer between 0 and 65535 }\nwire schema :World as { unit: @Unit(:UnitPublic) }\nnetwork :Game as {\n  server publishes :World latest\n  server owns @Unit as :UnitName\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidProjection", "line": 10, "column": 20}}
```

---

## Test: a projection of another schema

The projection of an `owns` route narrows the state of the handle type; otherwise it is `schema.invalidProjection` at the projection.

### Case description

```yaml
gesBlock: case
id: network-owns-projection-foreign
schemaApi:
  sources: ["schema :Composition as { name: :Text max 64, mass: :Number integer between 0 and 999 }\nschema :Contact as { label: :Text max 16 }\nprojection :UnitPublic of :Composition as { name }\nprojection :UnitName of :UnitPublic as { name }\nprojection :ContactLabel of :Contact as { label }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @Drone of :Composition as { id: :Number integer between 0 and 99, version: :Number integer between 0 and 65535 }\nwire schema :World as { tick: :Number integer between 0 and 65535 }\nnetwork :Game as {\n  server publishes :World latest\n  server owns @Unit as :ContactLabel\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidProjection", "line": 11, "column": 24}}
```

---

## Test: a projection that is not declared

The projection must be declared.

### Case description

```yaml
gesBlock: case
id: network-owns-projection-unknown
schemaApi:
  sources: ["schema :Composition as { name: :Text max 64, mass: :Number integer between 0 and 999 }\nschema :Contact as { label: :Text max 16 }\nprojection :UnitPublic of :Composition as { name }\nprojection :UnitName of :UnitPublic as { name }\nprojection :ContactLabel of :Contact as { label }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @Drone of :Composition as { id: :Number integer between 0 and 99, version: :Number integer between 0 and 65535 }\nwire schema :World as { tick: :Number integer between 0 and 65535 }\nnetwork :Game as {\n  server publishes :World latest\n  server owns @Unit as :Missing\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.unknownName", "line": 11, "column": 24}}
```

---

## Test: a pinned revision towards a projected side

A projected side has no revision of the owned type, so a pinned `@Drone(:UnitPublic)~` of a revision type in a route from its owner is `schema.invalidHandle`; without `~` it is accepted.

### Case description

```yaml
gesBlock: case
id: network-owns-projection-pinned-revision
schemaApi:
  sources: ["schema :Composition as { name: :Text max 64, mass: :Number integer between 0 and 999 }\nschema :Contact as { label: :Text max 16 }\nprojection :UnitPublic of :Composition as { name }\nprojection :UnitName of :UnitPublic as { name }\nprojection :ContactLabel of :Contact as { label }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\nhandle @Drone of :Composition as { id: :Number integer between 0 and 99, version: :Number integer between 0 and 65535 }\nwire schema :World as { drone: @Drone(:UnitPublic)~ }\nnetwork :Game as {\n  server publishes :World latest\n  server owns @Drone as :UnitPublic\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidHandle", "line": 10, "column": 20}}
```
