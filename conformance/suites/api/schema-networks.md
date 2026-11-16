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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  client sends :Commands reliable\n  server publishes :World latest at most every 50ms\n}\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes :World latest at most every 50ms\n  client sends :Commands reliable\n}\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as { client sends :Commands reliable,, server publishes :World reliable, }\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes :World latest at most every 33.3333ms\n}\n"]
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
  sources: ["constant $tick be 0.25s\nwire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes :World latest at most every $tick\n}\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes :World latest at most every 3600s\n}\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\n", "network :Game as {\n  client sends :Commands bulk\n}\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  client sends :Missing reliable\n}\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nschema :Plain as { a: :Boolean }\nnetwork :Game as {\n  server publishes :Plain latest\n}\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes :Commands reliable\n}\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes :World bulk\n}\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  client sends :Commands latest\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidRoute", "line": 7, "column": 26}}
```

---

## Test: only a topic has a rate

### Case description

```yaml
gesBlock: case
id: network-rate-on-sends
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  client sends :Commands reliable at most every 1s\n}\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidRoute", "line": 7, "column": 49}}
```

---

## Test: a rate is greater than zero

### Case description

```yaml
gesBlock: case
id: network-rate-zero
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes :World latest at most every 0s\n}\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes :World latest at most every 3600.000001s\n}\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes :World latest at most every 5m\n}\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  server publishes :World latest at most every tick\n}\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  client sends :Commands reliable\n  client sends :Commands bulk\n}\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :World as {\n  client sends :Commands reliable\n}\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nwire network :Game as {\n  client sends :Commands reliable\n}\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as {\n}\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n  client sends :Commands reliable\n}\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  client sends :Commands fast\n}\n"]
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.syntax", "line": 7, "column": 26}}
```

---

## Test: a field cannot name a network

### Case description

```yaml
gesBlock: case
id: network-field-names-network
schemaApi:
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\nnetwork :Game as {\n  client sends :Commands reliable\n}\nschema :Holder as { g: :Game }\n"]
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
  sources: ["wire schema :World as {\n  tick: :Number integer between 0 and 65535\n}\nwire messages :Commands as (Move)\nmessage Move()\n"]
  network: "Game"
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.unknownName", "line": 0, "column": 0}}
```

