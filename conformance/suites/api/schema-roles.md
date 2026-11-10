---
formatVersion: 1
suiteId: api.schema-roles
title: GES schema roles
kind: schemaApi
level: atomic
categories: [conformance]
tags: [portable-api, schemas]
---

# GES schema roles

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies wire and model roles: reading, groups, errors, the encodability of wire roots at reading, and that
roles change no hash.

---

## Test: roles are read and described

A role names its group; a role without one belongs to `default`, and a declaration may hold one wire and one model role.

### Case description

```yaml
gesBlock: case
id: roles-description
schemaApi:
  sources: ["wire(world) model(view) schema :Arena as {\n  size: :Number integer between 1 and 64\n}\n"]
  root: "Arena"
  bind: false
  describe: true
```

### Expectation

```yaml
gesBlock: expect
schema: {"description": {"name": "Arena", "kind": "Schema", "wireGroup": "world", "modelGroup": "view", "fields": [{"name": "size", "alternatives": [{"kind": "Number", "integer": true, "lower": "1e0", "upper": "64e0"}]}]}}
```

---

## Test: a role without a group

`wire` alone is the group `default`.

### Case description

```yaml
gesBlock: case
id: roles-default-group
schemaApi:
  sources: ["wire messages :Events as (Hit)\nmessage Hit()\n"]
  root: "Events"
  bind: false
  describe: true
```

### Expectation

```yaml
gesBlock: expect
schema: {"description": {"name": "Events", "kind": "Messages", "wireGroup": "default", "members": ["Hit"]}}
```

---

## Test: roles do not change the hash a

The canonical form leaves roles out.

### Case description

```yaml
gesBlock: case
id: roles-hash-marked
schemaApi:
  sources: ["wire(world) model(view) schema :A as { x: :Number integer between 0 and 9 }\n"]
  root: "A"
```

### Expectation

```yaml
gesBlock: expect
schema: {"canonical": "gess1\nschema A\nfield x Number integer between 0e0 9e0\n", "hash": "96d255a358e433dc"}
```

---

## Test: roles do not change the hash b

The same schema without a role has the same hash.

### Case description

```yaml
gesBlock: case
id: roles-hash-plain
schemaApi:
  sources: ["schema :A as { x: :Number integer between 0 and 9 }\n"]
  root: "A"
```

### Expectation

```yaml
gesBlock: expect
schema: {"canonical": "gess1\nschema A\nfield x Number integer between 0e0 9e0\n", "hash": "96d255a358e433dc"}
```

---

## Test: roles do not change the binding hash

A parameter's binding is the same with and without the role.

### Case description

```yaml
gesBlock: case
id: roles-binding-hash
schemaApi:
  sources: ["parameter w: :Quantity(m)\nwire schema :P as { x: :Quantity(m) between 0m and w step 1cm }\n"]
  root: "P"
  parameters: {"w": "100m"}
```

### Expectation

```yaml
gesBlock: expect
schema: {"hash": "6dde94dc49fb2e72", "bindingHash": "1dda7821d55be98a"}
```

---

## Test: a marked declaration is a part elsewhere

A wire root of one group binds as a field of a root of another group.

### Case description

```yaml
gesBlock: case
id: roles-marked-part
schemaApi:
  sources: ["wire(world) schema :World as {\n  badge: :Panel_Badge optional\n}\n", "wire(panels) schema :Panel_Badge as { label: :Text max 8 }\nwire(panels) schema :Panel as { badges: :List(:Panel_Badge) max 4 }\n"]
  root: "World"
```

### Expectation

```yaml
gesBlock: expect
schema: {"maxByteCount": 9}
```

---

## Test: a model root may be document-only

A model root needs no limits, so reading accepts it.

### Case description

```yaml
gesBlock: case
id: roles-model-document-only
schemaApi:
  sources: ["model(view) schema :View as {\n  names: :List(:Text)\n  table: :Map\n}\n"]
  root: "View"
  bind: false
  describe: true
```

### Expectation

```yaml
gesBlock: expect
schema: {"description": {"name": "View", "kind": "Schema", "modelGroup": "view", "fields": [{"name": "names", "alternatives": [{"kind": "List", "element": [{"kind": "Text"}]}]}, {"name": "table", "alternatives": [{"kind": "Map"}]}]}}
```

---

## Test: a wire root that is not encodable

Reading the sources reports what binding would, at the type.

### Case description

```yaml
gesBlock: case
id: roles-wire-not-encodable
schemaApi:
  sources: ["wire schema :A as {\n  ok: :Boolean\n  names: :List(:Text max 4)\n}\n"]
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.notEncodable", "line": 3, "column": 10}}
```

---

## Test: a wire root reaches a document-only part

The check follows references, also into a part of another file.

### Case description

```yaml
gesBlock: case
id: roles-wire-part-not-encodable
schemaApi:
  sources: ["wire(world) schema :World as { bot: :Bot }\n", "schema :Bot as {\n  name: :Text\n}\n"]
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.notEncodable", "line": 2, "column": 9}}
```

---

## Test: a wire message set reaches a document-only argument

Members of a message set are checked like fields.

### Case description

```yaml
gesBlock: case
id: roles-wire-message-not-encodable
schemaApi:
  sources: ["wire messages :Events as (Said)\nmessage Said(text: :Text)\n"]
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.notEncodable", "line": 2, "column": 20}}
```

---

## Test: an unmarked part may be document-only

Without a wire root nothing is checked at reading.

### Case description

```yaml
gesBlock: case
id: roles-unmarked-document-only
schemaApi:
  sources: ["schema :Note as { text: :Text }\n"]
  root: "Note"
  bind: false
  describe: true
```

### Expectation

```yaml
gesBlock: expect
schema: {"description": {"name": "Note", "kind": "Schema", "fields": [{"name": "text", "alternatives": [{"kind": "Text"}]}]}}
```

---

## Test: a bound that a parameter decides stays a binding error

Reading accepts a wire root whose range only a parameter value can break.

### Case description

```yaml
gesBlock: case
id: roles-parameter-binding
schemaApi:
  sources: ["parameter w: :Quantity(m)\nwire schema :P as { x: :Quantity(m) between 1m and w step 1m }\n"]
  root: "P"
  parameters: {"w": "0m"}
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidRange"}}
```

---

## Test: a role on a constant

Only a schema, message or message set can be a root.

### Case description

```yaml
gesBlock: case
id: roles-error-constant
schemaApi:
  sources: ["wire constant $a be 1\n"]
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidRole", "line": 1, "column": 1}}
```

---

## Test: a model role on a message

A model root is a schema.

### Case description

```yaml
gesBlock: case
id: roles-error-model-message
schemaApi:
  sources: ["model message M()\n"]
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidRole", "line": 1, "column": 1}}
```

---

## Test: an unknown role word

A word before a declaration is wire or model.

### Case description

```yaml
gesBlock: case
id: roles-error-unknown
schemaApi:
  sources: ["wired(world) schema :A as {}\n"]
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidRole", "line": 1, "column": 1}}
```

---

## Test: an invalid group name

A group is a lowercase name.

### Case description

```yaml
gesBlock: case
id: roles-error-group
schemaApi:
  sources: ["wire(World) schema :A as {}\n"]
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.invalidRole", "line": 1, "column": 6}}
```

---

## Test: a second wire role

A declaration is a wire root of one group.

### Case description

```yaml
gesBlock: case
id: roles-error-duplicate
schemaApi:
  sources: ["wire(a) wire(b) schema :A as {}\n"]
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.duplicateRole", "line": 1, "column": 9}}
```

---

## Test: a second model role

A declaration is a model root of one group.

### Case description

```yaml
gesBlock: case
id: roles-error-duplicate-model
schemaApi:
  sources: ["model model(view) schema :A as {}\n"]
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.duplicateRole", "line": 1, "column": 7}}
```
