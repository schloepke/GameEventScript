---
formatVersion: 1
suiteId: api.schema-set-parameters
title: GES schema set parameters
kind: schemaApi
level: atomic
categories: [conformance]
tags: [portable-api, schemas]
---

# GES schema set parameters

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies message sets with parameters: their encoding, values, paths, lenient read, hash and declaration errors.

---

## Test: a set with parameters encodes them before the member

The parameters come first, as the arguments of a message, then the member index and the member; the value is a message named after the set.

### Case description

```yaml
gesBlock: case
id: set-parameters-encode
schemaApi: {"sources": ["wire message Hit(target: :Number integer between 0 and 15) tags (#crit)\nwire message Stop()\nwire messages Cmd(ids: :List(:Number integer between 0 and 7) max 4 default [], urgent: :Boolean optional) as (Hit, Stop)\n"], "root": "Cmd", "describe": true, "encode": {"type": ":Message", "message": {"name": "Cmd", "args": [{"name": "ids", "value": {"type": ":List", "items": [{"type": ":Number.int64", "value": "3"}, {"type": ":Number.int64", "value": "5"}]}}, {"name": "urgent", "value": {"type": ":Boolean", "value": true}}, {"name": "message", "value": {"type": ":Message", "message": {"name": "Hit", "args": [{"name": "target", "value": {"type": ":Number.int64", "value": "9"}}], "tags": ["crit"]}}}]}}, "decode": "5a9701"}
```

### Expectation

```yaml
gesBlock: expect
schema: {"canonical": "gess1\nmessages Cmd\nargument ids List(Number integer between 0e0 7e0) max 4 default []\nargument urgent Boolean optional\nmember Hit\nmember Stop\nmessage Hit tags #crit\nargument target Number integer between 0e0 15e0\nmessage Stop\n", "hash": "816d67972cf7aa66", "description": {"name": "Cmd", "kind": "Messages", "wireGroup": "default", "fields": [{"name": "ids", "alternatives": [{"kind": "List", "max": 4, "element": [{"kind": "Number", "integer": true, "lower": "0e0", "upper": "7e0"}]}], "default": {"kind": "Empty"}}, {"name": "urgent", "alternatives": [{"kind": "Boolean"}], "optional": true}], "members": ["Hit", "Stop"]}, "bindingHash": "f2dcb1ad1c91a785", "maxByteCount": 3, "encoded": {"bytes": "5a9701"}, "decoded": {"value": {"type": ":Message", "message": {"name": "Cmd", "tags": [], "args": [{"name": "ids", "value": {"type": ":List", "items": [{"type": ":Number.int64", "value": "3"}, {"type": ":Number.int64", "value": "5"}]}}, {"name": "urgent", "value": {"type": ":Boolean", "value": "true"}}, {"name": "message", "value": {"type": ":Message", "message": {"name": "Hit", "tags": ["crit"], "args": [{"name": "target", "value": {"type": ":Number.int64", "value": "9"}}]}}}]}}, "byteCount": 3}}
```

---

## Test: an absent optional parameter

An optional parameter may be nothing.

### Case description

```yaml
gesBlock: case
id: set-parameters-absent
schemaApi: {"sources": ["wire message Hit(target: :Number integer between 0 and 15) tags (#crit)\nwire message Stop()\nwire messages Cmd(ids: :List(:Number integer between 0 and 7) max 4 default [], urgent: :Boolean optional) as (Hit, Stop)\n"], "root": "Cmd", "encode": {"type": ":Message", "message": {"name": "Cmd", "args": [{"name": "ids", "value": {"type": ":List", "items": []}}, {"name": "urgent", "value": {"type": ":Nothing"}}, {"name": "message", "value": {"type": ":Message", "message": {"name": "Stop", "args": []}}}]}}, "decode": "10"}
```

### Expectation

```yaml
gesBlock: expect
schema: {"canonical": "gess1\nmessages Cmd\nargument ids List(Number integer between 0e0 7e0) max 4 default []\nargument urgent Boolean optional\nmember Hit\nmember Stop\nmessage Hit tags #crit\nargument target Number integer between 0e0 15e0\nmessage Stop\n", "hash": "816d67972cf7aa66", "bindingHash": "f2dcb1ad1c91a785", "maxByteCount": 3, "encoded": {"bytes": "10"}, "decoded": {"value": {"type": ":Message", "message": {"name": "Cmd", "tags": [], "args": [{"name": "ids", "value": {"type": ":List", "items": []}}, {"name": "urgent", "value": {"type": ":Nothing"}}, {"name": "message", "value": {"type": ":Message", "message": {"name": "Stop", "tags": [], "args": []}}}]}}, "byteCount": 1}}
```

---

## Test: a carried message outside the set

The path names the argument `message`.

### Case description

```yaml
gesBlock: case
id: error-set-parameters-member
schemaApi: {"sources": ["wire message Hit(target: :Number integer between 0 and 15) tags (#crit)\nwire message Stop()\nwire messages Cmd(ids: :List(:Number integer between 0 and 7) max 4 default [], urgent: :Boolean optional) as (Hit, Stop)\n"], "root": "Cmd", "encode": {"type": ":Message", "message": {"name": "Cmd", "args": [{"name": "ids", "value": {"type": ":List", "items": []}}, {"name": "urgent", "value": {"type": ":Nothing"}}, {"name": "message", "value": {"type": ":Message", "message": {"name": "Other", "args": []}}}]}}}
```

### Expectation

```yaml
gesBlock: expect
schema: {"canonical": "gess1\nmessages Cmd\nargument ids List(Number integer between 0e0 7e0) max 4 default []\nargument urgent Boolean optional\nmember Hit\nmember Stop\nmessage Hit tags #crit\nargument target Number integer between 0e0 15e0\nmessage Stop\n", "hash": "816d67972cf7aa66", "bindingHash": "f2dcb1ad1c91a785", "maxByteCount": 3, "encoded": {"error": {"code": "wire.typeMismatch", "path": "message"}}}
```

---

## Test: an error inside the carried message

The path of the argument `message` comes before the member's own path.

### Case description

```yaml
gesBlock: case
id: error-set-parameters-inner-path
schemaApi: {"sources": ["wire message Hit(target: :Number integer between 0 and 15) tags (#crit)\nwire message Stop()\nwire messages Cmd(ids: :List(:Number integer between 0 and 7) max 4 default [], urgent: :Boolean optional) as (Hit, Stop)\n"], "root": "Cmd", "encode": {"type": ":Message", "message": {"name": "Cmd", "args": [{"name": "ids", "value": {"type": ":List", "items": []}}, {"name": "urgent", "value": {"type": ":Nothing"}}, {"name": "message", "value": {"type": ":Message", "message": {"name": "Hit", "args": [{"name": "target", "value": {"type": ":Number.int64", "value": "99"}}]}}}]}}}
```

### Expectation

```yaml
gesBlock: expect
schema: {"canonical": "gess1\nmessages Cmd\nargument ids List(Number integer between 0e0 7e0) max 4 default []\nargument urgent Boolean optional\nmember Hit\nmember Stop\nmessage Hit tags #crit\nargument target Number integer between 0e0 15e0\nmessage Stop\n", "hash": "816d67972cf7aa66", "bindingHash": "f2dcb1ad1c91a785", "maxByteCount": 3, "encoded": {"error": {"code": "wire.outOfRange", "path": "message.target"}}}
```

---

## Test: a set with parameters needs its own message

A bare member is no value of a set with parameters.

### Case description

```yaml
gesBlock: case
id: error-set-parameters-bare-message
schemaApi: {"sources": ["wire message Hit(target: :Number integer between 0 and 15) tags (#crit)\nwire message Stop()\nwire messages Cmd(ids: :List(:Number integer between 0 and 7) max 4 default [], urgent: :Boolean optional) as (Hit, Stop)\n"], "root": "Cmd", "encode": {"type": ":Message", "message": {"name": "Hit", "args": [{"name": "target", "value": {"type": ":Number.int64", "value": "9"}}], "tags": ["crit"]}}}
```

### Expectation

```yaml
gesBlock: expect
schema: {"canonical": "gess1\nmessages Cmd\nargument ids List(Number integer between 0e0 7e0) max 4 default []\nargument urgent Boolean optional\nmember Hit\nmember Stop\nmessage Hit tags #crit\nargument target Number integer between 0e0 15e0\nmessage Stop\n", "hash": "816d67972cf7aa66", "bindingHash": "f2dcb1ad1c91a785", "maxByteCount": 3, "encoded": {"error": {"code": "wire.typeMismatch", "path": ""}}}
```

---

## Test: the envelope of a set with parameters carries no tags

Only the carried message keeps its tags; a tag on the envelope is unknown.

### Case description

```yaml
gesBlock: case
id: error-set-parameters-envelope-tag
schemaApi: {"sources": ["wire message Hit(target: :Number integer between 0 and 15) tags (#crit)\nwire message Stop()\nwire messages Cmd(ids: :List(:Number integer between 0 and 7) max 4 default [], urgent: :Boolean optional) as (Hit, Stop)\n"], "root": "Cmd", "encode": {"type": ":Message", "message": {"name": "Cmd", "args": [{"name": "ids", "value": {"type": ":List", "items": []}}, {"name": "urgent", "value": {"type": ":Nothing"}}, {"name": "message", "value": {"type": ":Message", "message": {"name": "Hit", "args": [{"name": "target", "value": {"type": ":Number.int64", "value": "9"}}], "tags": ["crit"]}}}], "tags": ["crit"]}}}
```

### Expectation

```yaml
gesBlock: expect
schema: {"canonical": "gess1\nmessages Cmd\nargument ids List(Number integer between 0e0 7e0) max 4 default []\nargument urgent Boolean optional\nmember Hit\nmember Stop\nmessage Hit tags #crit\nargument target Number integer between 0e0 15e0\nmessage Stop\n", "hash": "816d67972cf7aa66", "bindingHash": "f2dcb1ad1c91a785", "maxByteCount": 3, "encoded": {"error": {"code": "wire.unknownTag", "path": ""}}}
```

---

## Test: a lenient read of a set with parameters

Left-out parameters take their defaults and the carried message is completed too.

### Case description

```yaml
gesBlock: case
id: complete-set-parameters
schemaApi: {"sources": ["wire message Hit(target: :Number integer between 0 and 15, damage: :Number integer between 0 and 7 default 2)\nwire message Stop()\nwire messages Cmd(ids: :List(:Number integer between 0 and 7) max 4 default [], urgent: :Boolean optional) as (Hit, Stop)\n"], "root": "Cmd", "complete": {"type": ":Message", "message": {"name": "Cmd", "args": [{"name": "message", "value": {"type": ":Message", "message": {"name": "Hit", "args": [{"name": "target", "value": {"type": ":Number.int64", "value": "4"}}]}}}]}}, "decode": "8004"}
```

### Expectation

```yaml
gesBlock: expect
schema: {"canonical": "gess1\nmessages Cmd\nargument ids List(Number integer between 0e0 7e0) max 4 default []\nargument urgent Boolean optional\nmember Hit\nmember Stop\nmessage Hit\nargument target Number integer between 0e0 15e0\nargument damage Number integer between 0e0 7e0 default 2e0\nmessage Stop\n", "hash": "edcaff4d46af77d2", "bindingHash": "a8eacb76f02b76f1", "maxByteCount": 4, "decoded": {"value": {"type": ":Message", "message": {"name": "Cmd", "tags": [], "args": [{"name": "ids", "value": {"type": ":List", "items": []}}, {"name": "urgent", "value": {"type": ":Nothing"}}, {"name": "message", "value": {"type": ":Message", "message": {"name": "Hit", "tags": [], "args": [{"name": "target", "value": {"type": ":Number.int64", "value": "4"}}, {"name": "damage", "value": {"type": ":Number.int64", "value": "2"}}]}}}]}}, "byteCount": 2}, "completed": {"value": {"type": ":Message", "message": {"name": "Cmd", "tags": [], "args": [{"name": "ids", "value": {"type": ":List", "items": []}}, {"name": "urgent", "value": {"type": ":Nothing"}}, {"name": "message", "value": {"type": ":Message", "message": {"name": "Hit", "tags": [], "args": [{"name": "target", "value": {"type": ":Number.int64", "value": "4"}}, {"name": "damage", "value": {"type": ":Number.int64", "value": "2"}}]}}}]}}, "bytes": "8004"}}
```

---

## Test: a lenient read reports the path of the carried message

An error inside the carried message has the path of the argument `message` first.

### Case description

```yaml
gesBlock: case
id: error-complete-set-parameters-path
schemaApi: {"sources": ["wire message Hit(target: :Number integer between 0 and 15) tags (#crit)\nwire message Stop()\nwire messages Cmd(ids: :List(:Number integer between 0 and 7) max 4 default [], urgent: :Boolean optional) as (Hit, Stop)\n"], "root": "Cmd", "complete": {"type": ":Message", "message": {"name": "Cmd", "args": [{"name": "message", "value": {"type": ":Message", "message": {"name": "Hit", "args": []}}}]}}}
```

### Expectation

```yaml
gesBlock: expect
schema: {"canonical": "gess1\nmessages Cmd\nargument ids List(Number integer between 0e0 7e0) max 4 default []\nargument urgent Boolean optional\nmember Hit\nmember Stop\nmessage Hit tags #crit\nargument target Number integer between 0e0 15e0\nmessage Stop\n", "hash": "816d67972cf7aa66", "bindingHash": "f2dcb1ad1c91a785", "maxByteCount": 3, "completed": {"error": {"code": "wire.missingField", "path": "message.target"}}}
```

---

## Test: a parameter cannot be named message

`message` names the carried message.

### Case description

```yaml
gesBlock: case
id: error-set-parameter-named-message
schemaApi: {"sources": ["wire message Stop()\nwire messages Cmd(message: :Boolean) as (Stop)\n"]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.duplicateName", "line": 2, "column": 19}}
```

---

## Test: parameter names differ

Two parameters with one name are a duplicate.

### Case description

```yaml
gesBlock: case
id: error-set-parameter-duplicate
schemaApi: {"sources": ["wire message Stop()\nwire messages Cmd(a: :Boolean, a: :Boolean) as (Stop)\n"]}
```

### Expectation

```yaml
gesBlock: expect
schema: {"error": {"code": "schema.duplicateName", "line": 2, "column": 32}}
```

---

## Test: parameters change the set's hash only

The members keep their forms and hashes; a set without parameters keeps its form.

### Case description

```yaml
gesBlock: case
id: set-parameters-hash
schemaApi: {"sources": ["wire message Stop()\nwire messages Cmd(urgent: :Boolean) as (Stop)\n"], "root": "Cmd"}
```

### Expectation

```yaml
gesBlock: expect
schema: {"canonical": "gess1\nmessages Cmd\nargument urgent Boolean\nmember Stop\nmessage Stop\n", "hash": "5b9971894fa4e051", "bindingHash": "7fa620a6556b730f", "maxByteCount": 1}
```
