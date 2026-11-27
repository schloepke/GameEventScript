---
formatVersion: 1
suiteId: runtime.stores
title: Store reads in scripts
kind: scriptApi
level: scenario
categories: [conformance]
tags: [handles, stores, host]
---

# Store reads in scripts

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

> **Since: Unreleased**

These cases verify [store reads](../../../specs/Language.md#stores): `@X[:count]`, `@X[:keys]`, `@X[:values]` and
`@X[:entries]` over every object of a handle type, the same over the parts of one root with `@X(root)`, and a type whose
resolver does not enumerate ([Host runtime](../../../specs/HostRuntime.md#stores)). Each case declares its handle setup,
the stores read from files and the scripted listings in a `handles` block
([Markdown format](../../../specs/Conformance/MarkdownFormat.md#handle-setup)).

---

## Test: a store read from files lists its entries in key order

A store answers for its type: the count, the handles without version, the states with the schema defaults applied and
the `key`/`value` entries, all in key order.

### Case description

```yaml
gesBlock: case
id: store-read-from-files
sources:
  - name: store-read-from-files.ges
    program: main
handles:
  schemas: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 default 10 }\nhandle @Hull of :Hull as { id: :Text max 32, version: :Nothing }\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Fleet of :Contact as { id: :Number integer between 0 and 255, version: :Nothing }\nhandle @Ship of :Contact composite @Fleet as { part: :Number integer between 0 and 15 }\n"]
  resolvers: []
  stores:
    files:
      - root: "content"
        path: "hulls.gesd"
        text: ":Store(@Hull)\nlight: [name: 'Light', armor: 20]\nheavy: [name: 'Heavy']\n"
    stores:
      - type: "Hull"
        roots:
          - name: "content"
            path: "hulls.gesd"
```

### Source code under test

```ges
module main
on Look {
  emit Out(count: @Hull[:count], keys: @Hull[:keys], values: @Hull[:values], entries: @Hull[:entries])
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| look | Look | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  look:
    input:
      args: []
    local:
      - name: Out
        args:
          - name: count
            value: { type: ":Number.int64", value: "2" }
          - name: keys
            value: { type: ":List", items: [{ type: ":Handle", value: "@Hull:heavy" }, { type: ":Handle", value: "@Hull:light" }] }
          - name: values
            value: { type: ":List", items: [{ type: ":Hull", entries: [{ key: "armor", value: { type: ":Number.int64", value: "10" } }, { key: "name", value: { type: ":Text", value: "Heavy" } }] }, { type: ":Hull", entries: [{ key: "armor", value: { type: ":Number.int64", value: "20" } }, { key: "name", value: { type: ":Text", value: "Light" } }] }] }
          - name: entries
            value: { type: ":List", items: [{ type: ":Map", entries: [{ key: "key", value: { type: ":Handle", value: "@Hull:heavy" } }, { key: "value", value: { type: ":Hull", entries: [{ key: "armor", value: { type: ":Number.int64", value: "10" } }, { key: "name", value: { type: ":Text", value: "Heavy" } }] } }] }, { type: ":Map", entries: [{ key: "key", value: { type: ":Handle", value: "@Hull:light" } }, { key: "value", value: { type: ":Hull", entries: [{ key: "armor", value: { type: ":Number.int64", value: "20" } }, { key: "name", value: { type: ":Text", value: "Light" } }] } }] }] }
```

---

## Test: store values compose with collection selectors

A store read is a List: further selectors, `for` loops and members of each state work on it like on any List.

### Case description

```yaml
gesBlock: case
id: store-read-composes
sources:
  - name: store-read-composes.ges
    program: main
handles:
  schemas: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 default 10 }\nhandle @Hull of :Hull as { id: :Text max 32, version: :Nothing }\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Fleet of :Contact as { id: :Number integer between 0 and 255, version: :Nothing }\nhandle @Ship of :Contact composite @Fleet as { part: :Number integer between 0 and 15 }\n"]
  resolvers: []
  stores:
    files:
      - root: "content"
        path: "hulls.gesd"
        text: ":Store(@Hull)\nlight: [name: 'Light', armor: 20]\nheavy: [name: 'Heavy']\n"
    stores:
      - type: "Hull"
        roots:
          - name: "content"
            path: "hulls.gesd"
```

### Source code under test

```ges
module main
on Look {
  for hull in @Hull[:values] {
    emit Hull(name: hull.name, armor: hull.armor)
  }
  emit Out(first: @Hull[:keys][:first], isEmpty: @Hull[:count] = 0)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| look | Look | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  look:
    input:
      args: []
    local:
      - name: Hull
        args:
          - name: name
            value: { type: ":Text", value: "Heavy" }
          - name: armor
            value: { type: ":Number.int64", value: "10" }
      - name: Hull
        args:
          - name: name
            value: { type: ":Text", value: "Light" }
          - name: armor
            value: { type: ":Number.int64", value: "20" }
      - name: Out
        args:
          - name: first
            value: { type: ":Handle", value: "@Hull:heavy" }
          - name: isEmpty
            value: { type: ":Boolean", value: "false" }
```

---

## Test: the parts of one root

`@Ship(fleet)` lists the parts of one root as the resolver lists them, sorted by part and without version; their states
resolve like any state read. A root without a listing reads as nothing, and so does a value that is no handle of the root
type.

### Case description

```yaml
gesBlock: case
id: store-parts-of-root
sources:
  - name: store-parts-of-root.ges
    program: main
handles:
  schemas: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 default 10 }\nhandle @Hull of :Hull as { id: :Text max 32, version: :Nothing }\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Fleet of :Contact as { id: :Number integer between 0 and 255, version: :Nothing }\nhandle @Ship of :Contact composite @Fleet as { part: :Number integer between 0 and 15 }\n"]
  lists:
    - type: "Ship"
      root: "@Fleet:1"
      handles: ["@Ship:1/7", "@Ship:1/3"]
  answers:
    - { handle: "@Ship:1/3", document: ":Schema(:Contact)\nlabel: 'Corvette'\n" }
    - { handle: "@Ship:1/7", document: ":Schema(:Contact)\nlabel: 'Frigate'\n" }
```

### Source code under test

```ges
module main
on Look(fleet as @Fleet, other as @Fleet, target as @Target) {
  emit Out(count: @Ship(fleet)[:count], keys: @Ship(fleet)[:keys], values: @Ship(fleet)[:values], unlisted: @Ship(other)[:count], wrong: @Ship(target)[:count])
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| look | Look | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  look:
    input:
      args:
        - name: fleet
          value: { type: ":Handle", value: "@Fleet:1" }
        - name: other
          value: { type: ":Handle", value: "@Fleet:2" }
        - name: target
          value: { type: ":Handle", value: "@Target:1" }
    local:
      - name: Out
        args:
          - name: count
            value: { type: ":Number.int64", value: "2" }
          - name: keys
            value: { type: ":List", items: [{ type: ":Handle", value: "@Ship:1/3" }, { type: ":Handle", value: "@Ship:1/7" }] }
          - name: values
            value: { type: ":List", items: [{ type: ":Contact", entries: [{ key: "label", value: { type: ":Text", value: "Corvette" } }] }, { type: ":Contact", entries: [{ key: "label", value: { type: ":Text", value: "Frigate" } }] }] }
          - name: unlisted
            value: { type: ":Nothing" }
          - name: wrong
            value: { type: ":Nothing" }
```

---

## Test: a type whose resolver does not enumerate reads as nothing

A resolver that answers state reads but lists no handles makes every store read of its type `nothing`, as does a type
without a resolver.

### Case description

```yaml
gesBlock: case
id: store-not-enumerable
sources:
  - name: store-not-enumerable.ges
    program: main
handles:
  schemas: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 default 10 }\nhandle @Hull of :Hull as { id: :Text max 32, version: :Nothing }\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Fleet of :Contact as { id: :Number integer between 0 and 255, version: :Nothing }\nhandle @Ship of :Contact composite @Fleet as { part: :Number integer between 0 and 15 }\n"]
  resolvers: ["Target"]
```

### Source code under test

```ges
module main
on Look {
  emit Out(count: @Target[:count], keys: @Target[:keys], values: @Target[:values], entries: @Target[:entries], none: @Hull[:count])
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| look | Look | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  look:
    input:
      args: []
    local:
      - name: Out
        args:
          - name: count
            value: { type: ":Nothing" }
          - name: keys
            value: { type: ":Nothing" }
          - name: values
            value: { type: ":Nothing" }
          - name: entries
            value: { type: ":Nothing" }
          - name: none
            value: { type: ":Nothing" }
```

---

## Test: an invalid listing stops the handler

A listing that holds a handle of another root, of another type or one handle twice ends the read with
`runtime.handleResolveFailed`.

### Case description

```yaml
gesBlock: case
id: store-invalid-listing
sources:
  - name: store-invalid-listing.ges
    program: main
handles:
  schemas: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 default 10 }\nhandle @Hull of :Hull as { id: :Text max 32, version: :Nothing }\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Fleet of :Contact as { id: :Number integer between 0 and 255, version: :Nothing }\nhandle @Ship of :Contact composite @Fleet as { part: :Number integer between 0 and 15 }\n"]
  lists:
    - type: "Ship"
      root: "@Fleet:1"
      handles: ["@Ship:1/3", "@Ship:2/4"]
    - type: "Target"
      handles: ["@Target:1", "@Target:1"]
```

### Source code under test

```ges
module main
on Parts(fleet as @Fleet) {
  emit Out(count: @Ship(fleet)[:count])
}
on Targets {
  emit Out(count: @Target[:count])
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| parts | Parts | completion | |
| targets | Targets | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  parts:
    input:
      args:
        - name: fleet
          value: { type: ":Handle", value: "@Fleet:1" }
    diagnostics:
      - phase: runtime
        code: runtime.handleResolveFailed
        symbol: "@Ship"
        programName: "main"
        handlerName: "Parts(fleet)"
  targets:
    input:
      args: []
    diagnostics:
      - phase: runtime
        code: runtime.handleResolveFailed
        symbol: "@Target"
        programName: "main"
        handlerName: "Targets()"
```
