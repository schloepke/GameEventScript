---
formatVersion: 1
suiteId: api.stores
title: GES stores
kind: schemaApi
level: atomic
categories: [conformance]
tags: [portable-api, documents, handles]
---

# GES stores

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies reading [stores](../../../specs/Documents.md#stores): store files with modules and imports,
references and pins, the entries with their handles, states and origins, and every error with its position. Each case
reads its stores in order from one file table; a later store depends on every earlier one. A store reads named
roots or, [from sources](../../../specs/Documents.md#reading-from-handles), root handles with the paths of their root
files: the table's root is then the root handle's id, and a file answers with its own `version`, else the root handle's,
else `latest`. The expectation holds the entries of the last store with its
`sources`, the errors of the first store that fails, or the `failure` code of a handle read.

---

## Test: the entries of a store with modules, imports, references and pins

A store reads its root entries, module blocks, a root import whose modules join and an entry import; references read as handles of the named entries, `~` pins them with the content hash, and the entries come sorted by id with their origin and the schema defaults applied.

### Case description

```yaml
gesBlock: case
id: store-entries
schemaApi:
  sources: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 }\nhandle @Hull of :Hull with id :Text max 32 versioned by hash(#ges)\nschema :Composition as { name: :Text max 64, hull: @Hull~ optional, escort: @Unit optional, leader: @Unit~ optional, cost: :Number integer between 0 and 1000 default 10, tag: @Tag optional, rivals: :Map(@Unit → :Number integer between 0 and 9) max 4 optional }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Tag of :Contact with id :Text max 16\nhandle @Slot of :Contact attached to @Tag with part :Number integer between 0 and 15\n"]
  store:
    files:
      - root: "content"
        path: "hulls.gesd"
        text: ":Store(@Hull)\nlight: [name: 'Light', armor: 20]\nheavy: [name: 'Heavy', armor: 80]\n"
      - root: "content"
        path: "units.gesd"
        text: ":Store(@Unit)\n// Units of the first campaign.\nimport 'drones/all.gesd'\n\nscout: [name: 'Scout', hull: @Hull:light~, escort: @drone.gnabblo]\nmodule drone {\n  gnabblo: [name: 'Gnabblo', hull: @Hull:heavy~, escort: @scout, cost: 40]\n}\n"
      - root: "content"
        path: "drones/all.gesd"
        text: ":Store(@Unit)\nmodule drone {\n  brakk: import 'brakk.gesd'\n}\nmodule drone.heavy { tank: [name: 'Tank', leader: @drone.brakk~] }\n"
      - root: "content"
        path: "drones/brakk.gesd"
        text: ":Schema(:Composition)\nname: 'Brakk', hull: @Hull:light~, leader: @gnabblo~\n"
    stores:
      - type: "Hull"
        roots:
          - name: "content"
            path: "hulls.gesd"
      - type: "Unit"
        roots:
          - name: "content"
            path: "units.gesd"
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    entries:
      - id: "drone.brakk"
        handle: "@Unit:drone.brakk~822b0b62a64a6eea8343fe03ff814525c0114289d95acae21075f4913af48bf1"
        origin:
          root: "content"
          path: "drones/all.gesd"
          line: 3
          column: 3
        value:
          type: ":Composition"
          entries:
            - key: "cost"
              value:
                type: ":Number.int64"
                value: "10"
            - key: "hull"
              value:
                type: ":Handle"
                value: "@Hull:light~ceb617dc154ff63867314365e0b1b213314376f154fccf932aac68e3a3096966"
            - key: "leader"
              value:
                type: ":Handle"
                value: "@Unit:drone.gnabblo~ffa73fcc3130e3a7abb6c1f80cf81fa3f28e8deb43907642871c037126a168ff"
            - key: "name"
              value:
                type: ":Text"
                value: "Brakk"
      - id: "drone.gnabblo"
        handle: "@Unit:drone.gnabblo~ffa73fcc3130e3a7abb6c1f80cf81fa3f28e8deb43907642871c037126a168ff"
        origin:
          root: "content"
          path: "units.gesd"
          line: 7
          column: 3
        value:
          type: ":Composition"
          entries:
            - key: "cost"
              value:
                type: ":Number.int64"
                value: "40"
            - key: "escort"
              value:
                type: ":Handle"
                value: "@Unit:scout"
            - key: "hull"
              value:
                type: ":Handle"
                value: "@Hull:heavy~32d3077cc8c29cd23f92b29e7152a189118eb86d22d6efad853e4484c6d79a95"
            - key: "name"
              value:
                type: ":Text"
                value: "Gnabblo"
      - id: "drone.heavy.tank"
        handle: "@Unit:drone.heavy.tank~98c7cf9b2b47cf0cb4473075571ecd9ecc07802d033b4e10dc8f24a3e1a8bc2d"
        origin:
          root: "content"
          path: "drones/all.gesd"
          line: 5
          column: 22
        value:
          type: ":Composition"
          entries:
            - key: "cost"
              value:
                type: ":Number.int64"
                value: "10"
            - key: "leader"
              value:
                type: ":Handle"
                value: "@Unit:drone.brakk~822b0b62a64a6eea8343fe03ff814525c0114289d95acae21075f4913af48bf1"
            - key: "name"
              value:
                type: ":Text"
                value: "Tank"
      - id: "scout"
        handle: "@Unit:scout~1bd852797dd66750007be01e50904c63a8b5d6787d9a99081feef06e58cf7e0f"
        origin:
          root: "content"
          path: "units.gesd"
          line: 5
          column: 1
        value:
          type: ":Composition"
          entries:
            - key: "cost"
              value:
                type: ":Number.int64"
                value: "10"
            - key: "escort"
              value:
                type: ":Handle"
                value: "@Unit:drone.gnabblo"
            - key: "hull"
              value:
                type: ":Handle"
                value: "@Hull:light~ceb617dc154ff63867314365e0b1b213314376f154fccf932aac68e3a3096966"
            - key: "name"
              value:
                type: ":Text"
                value: "Scout"
```

---

## Test: a one-segment reference names the own module or the root

Inside a module `@b` names the entry of that module and `@s` a root entry the module lacks, at the root `@s` names a root entry; a reference as a Map key reads as the handle without a version, and a handle of a type without a store is not checked.

### Case description

```yaml
gesBlock: case
id: store-references-in-module
schemaApi:
  sources: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 }\nhandle @Hull of :Hull with id :Text max 32 versioned by hash(#ges)\nschema :Composition as { name: :Text max 64, hull: @Hull~ optional, escort: @Unit optional, leader: @Unit~ optional, cost: :Number integer between 0 and 1000 default 10, tag: @Tag optional, rivals: :Map(@Unit → :Number integer between 0 and 9) max 4 optional }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Tag of :Contact with id :Text max 16\nhandle @Slot of :Contact attached to @Tag with part :Number integer between 0 and 15\n"]
  store:
    files:
      - root: "content"
        path: "units.gesd"
        text: ":Store(@Unit)\nr: [name: 'Root R', escort: @s, tag: @Tag:anything]\ns: [name: 'Root S']\nmodule m {\n  a: [name: 'M A', escort: @b, rivals: [@a: 1, @Unit:s: 2]]\n  b: [name: 'M B', escort: @s]\n}\n"
    stores:
      - type: "Unit"
        roots:
          - name: "content"
            path: "units.gesd"
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    entries:
      - id: "m.a"
        handle: "@Unit:m.a~64009cbcda53278c1372c82c920919b23cefd2f53bbee6ae76df7b20de7a8c2b"
        origin:
          root: "content"
          path: "units.gesd"
          line: 5
          column: 3
        value:
          type: ":Composition"
          entries:
            - key: "cost"
              value:
                type: ":Number.int64"
                value: "10"
            - key: "escort"
              value:
                type: ":Handle"
                value: "@Unit:m.b"
            - key: "name"
              value:
                type: ":Text"
                value: "M A"
            - key: "rivals"
              value:
                type: ":Map"
                entries:
                  - handle: "@Unit:m.a"
                    value:
                      type: ":Number.int64"
                      value: "1"
                  - handle: "@Unit:s"
                    value:
                      type: ":Number.int64"
                      value: "2"
      - id: "m.b"
        handle: "@Unit:m.b~7c20d416621df5a3c33e76fafe8722df44b6cf3a568975a417b4adcffdd740ef"
        origin:
          root: "content"
          path: "units.gesd"
          line: 6
          column: 3
        value:
          type: ":Composition"
          entries:
            - key: "cost"
              value:
                type: ":Number.int64"
                value: "10"
            - key: "escort"
              value:
                type: ":Handle"
                value: "@Unit:s"
            - key: "name"
              value:
                type: ":Text"
                value: "M B"
      - id: "r"
        handle: "@Unit:r~e2ebe64ba7bf0296431a8b88d945b87cb6ed5e84997e20b60fe92e362f7cabf0"
        origin:
          root: "content"
          path: "units.gesd"
          line: 2
          column: 1
        value:
          type: ":Composition"
          entries:
            - key: "cost"
              value:
                type: ":Number.int64"
                value: "10"
            - key: "escort"
              value:
                type: ":Handle"
                value: "@Unit:s"
            - key: "name"
              value:
                type: ":Text"
                value: "Root R"
            - key: "tag"
              value:
                type: ":Handle"
                value: "@Tag:anything"
      - id: "s"
        handle: "@Unit:s~b00360ffa244e3978ffbcca04e28960f1a777e53335eda6ed6694f7a16f54fa4"
        origin:
          root: "content"
          path: "units.gesd"
          line: 3
          column: 1
        value:
          type: ":Composition"
          entries:
            - key: "cost"
              value:
                type: ":Number.int64"
                value: "10"
            - key: "name"
              value:
                type: ":Text"
                value: "Root S"
```

---

## Test: unpinned references may form a cycle

Two entries that refer to each other without `~` read, because the game resolves them.

### Case description

```yaml
gesBlock: case
id: store-unpinned-cycle
schemaApi:
  sources: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 }\nhandle @Hull of :Hull with id :Text max 32 versioned by hash(#ges)\nschema :Composition as { name: :Text max 64, hull: @Hull~ optional, escort: @Unit optional, leader: @Unit~ optional, cost: :Number integer between 0 and 1000 default 10, tag: @Tag optional, rivals: :Map(@Unit → :Number integer between 0 and 9) max 4 optional }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Tag of :Contact with id :Text max 16\nhandle @Slot of :Contact attached to @Tag with part :Number integer between 0 and 15\n"]
  store:
    files:
      - root: "content"
        path: "units.gesd"
        text: ":Store(@Unit)\na: [name: 'A', escort: @b]\nb: [name: 'B', escort: @a]\n"
    stores:
      - type: "Unit"
        roots:
          - name: "content"
            path: "units.gesd"
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    entries:
      - id: "a"
        handle: "@Unit:a~9cee5ba9c1de05699fad9ea5501fd79eee27838aeb241603e1d24b5e995ca2d0"
        origin:
          root: "content"
          path: "units.gesd"
          line: 2
          column: 1
        value:
          type: ":Composition"
          entries:
            - key: "cost"
              value:
                type: ":Number.int64"
                value: "10"
            - key: "escort"
              value:
                type: ":Handle"
                value: "@Unit:b"
            - key: "name"
              value:
                type: ":Text"
                value: "A"
      - id: "b"
        handle: "@Unit:b~69bef3123bb981e7904e51f03781750fb4a09dff73ca94fc90d4ed673b7a50d8"
        origin:
          root: "content"
          path: "units.gesd"
          line: 3
          column: 1
        value:
          type: ":Composition"
          entries:
            - key: "cost"
              value:
                type: ":Number.int64"
                value: "10"
            - key: "escort"
              value:
                type: ":Handle"
                value: "@Unit:a"
            - key: "name"
              value:
                type: ":Text"
                value: "B"
```

---

## Test: several roots, and a file reached twice contributes once

Roots read in the given order; a file imported again on another path adds no entries a second time, and origins name the root of their file.

### Case description

```yaml
gesBlock: case
id: store-roots-and-shared-file
schemaApi:
  sources: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 }\nhandle @Hull of :Hull with id :Text max 32 versioned by hash(#ges)\nschema :Composition as { name: :Text max 64, hull: @Hull~ optional, escort: @Unit optional, leader: @Unit~ optional, cost: :Number integer between 0 and 1000 default 10, tag: @Tag optional, rivals: :Map(@Unit → :Number integer between 0 and 9) max 4 optional }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Tag of :Contact with id :Text max 16\nhandle @Slot of :Contact attached to @Tag with part :Number integer between 0 and 15\n"]
  store:
    files:
      - root: "base"
        path: "units.gesd"
        text: ":Store(@Unit)\nimport 'shared.gesd'\nimport 'more.gesd'\nscout: [name: 'Scout']\n"
      - root: "base"
        path: "more.gesd"
        text: ":Store(@Unit)\nimport '/shared.gesd'\nmodule extra { one: [name: 'One'] }\n"
      - root: "base"
        path: "shared.gesd"
        text: ":Store(@Unit)\nshared: [name: 'Shared']\n"
      - root: "mod"
        path: "mod.gesd"
        text: ":Store(@Unit)\nmodule mod { }\nmodule mod { two: [name: 'Two', escort: @scout] }\n"
    stores:
      - type: "Unit"
        roots:
          - name: "base"
            path: "units.gesd"
          - name: "mod"
            path: "mod.gesd"
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    entries:
      - id: "extra.one"
        handle: "@Unit:extra.one~e2f981aa05f671c1e19db3039dcf4839dfd7e28e10d614e05019a7f885f6c334"
        origin:
          root: "base"
          path: "more.gesd"
          line: 3
          column: 16
        value:
          type: ":Composition"
          entries:
            - key: "cost"
              value:
                type: ":Number.int64"
                value: "10"
            - key: "name"
              value:
                type: ":Text"
                value: "One"
      - id: "mod.two"
        handle: "@Unit:mod.two~1ed894930b4137e0564132431a31a4261ae73522b560b42e428c284d0c2f43c5"
        origin:
          root: "mod"
          path: "mod.gesd"
          line: 3
          column: 14
        value:
          type: ":Composition"
          entries:
            - key: "cost"
              value:
                type: ":Number.int64"
                value: "10"
            - key: "escort"
              value:
                type: ":Handle"
                value: "@Unit:scout"
            - key: "name"
              value:
                type: ":Text"
                value: "Two"
      - id: "scout"
        handle: "@Unit:scout~57e7785f280886f1de095e4178244d517c5d7606b9e07af7dc11e69c379de34f"
        origin:
          root: "base"
          path: "units.gesd"
          line: 4
          column: 1
        value:
          type: ":Composition"
          entries:
            - key: "cost"
              value:
                type: ":Number.int64"
                value: "10"
            - key: "name"
              value:
                type: ":Text"
                value: "Scout"
      - id: "shared"
        handle: "@Unit:shared~2078cc7e4422558837c44d5e4caec3dc63ca407d24a9bc39bea83ac2d089c365"
        origin:
          root: "base"
          path: "shared.gesd"
          line: 2
          column: 1
        value:
          type: ":Composition"
          entries:
            - key: "cost"
              value:
                type: ":Number.int64"
                value: "10"
            - key: "name"
              value:
                type: ":Text"
                value: "Shared"
```

---

## Test: a store without entries

A store file with only its header reads to a store without entries.

### Case description

```yaml
gesBlock: case
id: store-empty
schemaApi:
  sources: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 }\nhandle @Hull of :Hull with id :Text max 32 versioned by hash(#ges)\nschema :Composition as { name: :Text max 64, hull: @Hull~ optional, escort: @Unit optional, leader: @Unit~ optional, cost: :Number integer between 0 and 1000 default 10, tag: @Tag optional, rivals: :Map(@Unit → :Number integer between 0 and 9) max 4 optional }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Tag of :Contact with id :Text max 16\nhandle @Slot of :Contact attached to @Tag with part :Number integer between 0 and 15\n"]
  store:
    files:
      - root: "content"
        path: "units.gesd"
        text: "// nothing yet\n:Store(@Unit)\n"
    stores:
      - type: "Unit"
        roots:
          - name: "content"
            path: "units.gesd"
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    entries: []
```

---

## Test: import errors are all reported at their paths

A missing root has no line, a missing import, an import above the root, an import cycle and imports of the wrong kind and type are each reported at the import path, in reading order.

### Case description

```yaml
gesBlock: case
id: store-import-errors
schemaApi:
  sources: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 }\nhandle @Hull of :Hull with id :Text max 32 versioned by hash(#ges)\nschema :Composition as { name: :Text max 64, hull: @Hull~ optional, escort: @Unit optional, leader: @Unit~ optional, cost: :Number integer between 0 and 1000 default 10, tag: @Tag optional, rivals: :Map(@Unit → :Number integer between 0 and 9) max 4 optional }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Tag of :Contact with id :Text max 16\nhandle @Slot of :Contact attached to @Tag with part :Number integer between 0 and 15\n"]
  store:
    files:
      - root: "content"
        path: "hulls.gesd"
        text: ":Store(@Hull)\nlight: [name: 'Light', armor: 20]\nheavy: [name: 'Heavy', armor: 80]\n"
      - root: "content"
        path: "units.gesd"
        text: ":Store(@Unit)\nimport 'missing.gesd'\nimport '../outside.gesd'\nimport 'loop.gesd'\nimport 'state.gesd'\nimport 'hulls.gesd'\nmodule m { a: import 'units.gesd', b: import 'hull-state.gesd' }\n"
      - root: "content"
        path: "loop.gesd"
        text: ":Store(@Unit)\nimport 'units.gesd'\n"
      - root: "content"
        path: "state.gesd"
        text: ":Schema(:Composition)\nname: 'State'\n"
      - root: "content"
        path: "hull-state.gesd"
        text: ":Schema(:Hull)\nname: 'H', armor: 1\n"
    stores:
      - type: "Hull"
        roots:
          - name: "content"
            path: "hulls.gesd"
      - type: "Unit"
        roots:
          - name: "content"
            path: "units.gesd"
          - name: "other"
            path: "none.gesd"
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    errors:
      - code: "store.importMissing"
        location:
          root: "content"
          path: "units.gesd"
          line: 2
          column: 8
      - code: "store.importOutsideRoot"
        location:
          root: "content"
          path: "units.gesd"
          line: 3
          column: 8
      - code: "store.importKind"
        location:
          root: "content"
          path: "units.gesd"
          line: 5
          column: 8
      - code: "store.importType"
        location:
          root: "content"
          path: "units.gesd"
          line: 6
          column: 8
      - code: "store.importKind"
        location:
          root: "content"
          path: "units.gesd"
          line: 7
          column: 22
      - code: "store.importType"
        location:
          root: "content"
          path: "units.gesd"
          line: 7
          column: 46
      - code: "store.importCycle"
        location:
          root: "content"
          path: "loop.gesd"
          line: 2
          column: 8
      - code: "store.importMissing"
        location:
          root: "other"
          path: "none.gesd"
```

---

## Test: a root file must be a store of the read type

A root file that is a state document is `store.importKind` at its start, a store of another type `store.importType` at its header.

### Case description

```yaml
gesBlock: case
id: store-root-kind-and-type
schemaApi:
  sources: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 }\nhandle @Hull of :Hull with id :Text max 32 versioned by hash(#ges)\nschema :Composition as { name: :Text max 64, hull: @Hull~ optional, escort: @Unit optional, leader: @Unit~ optional, cost: :Number integer between 0 and 1000 default 10, tag: @Tag optional, rivals: :Map(@Unit → :Number integer between 0 and 9) max 4 optional }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Tag of :Contact with id :Text max 16\nhandle @Slot of :Contact attached to @Tag with part :Number integer between 0 and 15\n"]
  store:
    files:
      - root: "a"
        path: "state.gesd"
        text: "\n:Schema(:Composition)\nname: 'State'\n"
      - root: "b"
        path: "hulls.gesd"
        text: "// hulls\n:Store(@Hull)\n"
    stores:
      - type: "Unit"
        roots:
          - name: "a"
            path: "state.gesd"
          - name: "b"
            path: "hulls.gesd"
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    errors:
      - code: "store.importKind"
        location:
          root: "a"
          path: "state.gesd"
          line: 2
          column: 1
      - code: "store.importType"
        location:
          root: "b"
          path: "hulls.gesd"
          line: 2
          column: 1
```

---

## Test: every file reports its first reading error and stops the later steps

Each file reports at most its first document error with document codes; while any file fails, ids and references are not checked.

### Case description

```yaml
gesBlock: case
id: store-reading-errors
schemaApi:
  sources: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 }\nhandle @Hull of :Hull with id :Text max 32 versioned by hash(#ges)\nschema :Composition as { name: :Text max 64, hull: @Hull~ optional, escort: @Unit optional, leader: @Unit~ optional, cost: :Number integer between 0 and 1000 default 10, tag: @Tag optional, rivals: :Map(@Unit → :Number integer between 0 and 9) max 4 optional }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Tag of :Contact with id :Text max 16\nhandle @Slot of :Contact attached to @Tag with part :Number integer between 0 and 15\n"]
  store:
    files:
      - root: "content"
        path: "units.gesd"
        text: ":Store(@Unit)\nimport 'bad.gesd'\nimport 'bad2.gesd'\nscout: [name: 'Scout', escort: @nowhere]\n"
      - root: "content"
        path: "bad.gesd"
        text: ":Store(@Unit)\nscout: [name: 'Again'] module\nother: [name: 1 2]\n"
      - root: "content"
        path: "bad2.gesd"
        text: ":Store(@Unit)\nmodule m {\n  import 'x.gesd'\n}\n"
    stores:
      - type: "Unit"
        roots:
          - name: "content"
            path: "units.gesd"
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    errors:
      - code: "document.unexpectedToken"
        location:
          root: "content"
          path: "bad.gesd"
          line: 2
          column: 24
      - code: "document.unexpectedToken"
        location:
          root: "content"
          path: "bad2.gesd"
          line: 3
          column: 3
```

---

## Test: ids that are too long or repeat

An id longer than the id max is `store.invalidId` at its key; a repeated id is `store.duplicateId` at the later key and names the first; references are not checked while ids fail.

### Case description

```yaml
gesBlock: case
id: store-id-errors
schemaApi:
  sources: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 }\nhandle @Hull of :Hull with id :Text max 32 versioned by hash(#ges)\nschema :Composition as { name: :Text max 64, hull: @Hull~ optional, escort: @Unit optional, leader: @Unit~ optional, cost: :Number integer between 0 and 1000 default 10, tag: @Tag optional, rivals: :Map(@Unit → :Number integer between 0 and 9) max 4 optional }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Tag of :Contact with id :Text max 16\nhandle @Slot of :Contact attached to @Tag with part :Number integer between 0 and 15\n"]
  store:
    files:
      - root: "content"
        path: "units.gesd"
        text: ":Store(@Unit)\nscout: [name: 'Scout', escort: @nowhere]\nmodule drone.long { name-x: [name: 'Long'] }\nmodule a { b: [name: 'AB'] }\n"
      - root: "content2"
        path: "units2.gesd"
        text: ":Store(@Unit)\nmodule a {\n  b: [name: 'Again']\n}\nscout: [name: 'Twice']\n"
    stores:
      - type: "Unit"
        roots:
          - name: "content"
            path: "units.gesd"
          - name: "content2"
            path: "units2.gesd"
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    errors:
      - code: "store.invalidId"
        location:
          root: "content"
          path: "units.gesd"
          line: 3
          column: 21
      - code: "store.duplicateId"
        location:
          root: "content2"
          path: "units2.gesd"
          line: 3
          column: 3
        other:
          root: "content"
          path: "units.gesd"
          line: 4
          column: 12
      - code: "store.duplicateId"
        location:
          root: "content2"
          path: "units2.gesd"
          line: 5
          column: 1
        other:
          root: "content"
          path: "units.gesd"
          line: 2
          column: 1
```

---

## Test: unknown references carry a suggestion

An unknown reference suggests the first id whose last segment equals the written one, else the closest id within an edit distance of 3, else nothing; a handle of a type with a store is checked as well.

### Case description

```yaml
gesBlock: case
id: store-unknown-references
schemaApi:
  sources: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 }\nhandle @Hull of :Hull with id :Text max 32 versioned by hash(#ges)\nschema :Composition as { name: :Text max 64, hull: @Hull~ optional, escort: @Unit optional, leader: @Unit~ optional, cost: :Number integer between 0 and 1000 default 10, tag: @Tag optional, rivals: :Map(@Unit → :Number integer between 0 and 9) max 4 optional }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Tag of :Contact with id :Text max 16\nhandle @Slot of :Contact attached to @Tag with part :Number integer between 0 and 15\n"]
  store:
    files:
      - root: "content"
        path: "hulls.gesd"
        text: ":Store(@Hull)\nlight: [name: 'Light', armor: 20]\nheavy: [name: 'Heavy', armor: 80]\n"
      - root: "content"
        path: "units.gesd"
        text: ":Store(@Unit)\nscout: [name: 'Scout', hull: @Hull:hevy~, escort: @scuot]\nmodule drone {\n  gnabblo: [name: 'G', escort: @brakk]\n  x: [name: 'X', escort: @drone.gnablo, rivals: [@zzzzzzzz: 1]]\n}\nmodule heavy { brakk: [name: 'B', escort: @Unit:scot] }\n"
    stores:
      - type: "Hull"
        roots:
          - name: "content"
            path: "hulls.gesd"
      - type: "Unit"
        roots:
          - name: "content"
            path: "units.gesd"
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    errors:
      - code: "store.unknownReference"
        location:
          root: "content"
          path: "units.gesd"
          line: 2
          column: 30
        suggestion: "heavy"
      - code: "store.unknownReference"
        location:
          root: "content"
          path: "units.gesd"
          line: 2
          column: 51
        suggestion: "scout"
      - code: "store.unknownReference"
        location:
          root: "content"
          path: "units.gesd"
          line: 4
          column: 32
        suggestion: "heavy.brakk"
      - code: "store.unknownReference"
        location:
          root: "content"
          path: "units.gesd"
          line: 5
          column: 26
        suggestion: "drone.gnabblo"
      - code: "store.unknownReference"
        location:
          root: "content"
          path: "units.gesd"
          line: 5
          column: 50
      - code: "store.unknownReference"
        location:
          root: "content"
          path: "units.gesd"
          line: 7
          column: 43
        suggestion: "scout"
```

---

## Test: a one-segment reference that names both a module entry and a root entry

When the own module and the root both have the key, the reference is `store.ambiguousReference` at its `@`; the complete id resolves it.

### Case description

```yaml
gesBlock: case
id: store-ambiguous-reference
schemaApi:
  sources: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 }\nhandle @Hull of :Hull with id :Text max 32 versioned by hash(#ges)\nschema :Composition as { name: :Text max 64, hull: @Hull~ optional, escort: @Unit optional, leader: @Unit~ optional, cost: :Number integer between 0 and 1000 default 10, tag: @Tag optional, rivals: :Map(@Unit → :Number integer between 0 and 9) max 4 optional }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Tag of :Contact with id :Text max 16\nhandle @Slot of :Contact attached to @Tag with part :Number integer between 0 and 15\n"]
  store:
    files:
      - root: "content"
        path: "units.gesd"
        text: ":Store(@Unit)\nb: [name: 'Root B']\nmodule m {\n  b: [name: 'M B']\n  a: [name: 'M A', escort: @b]\n  c: [name: 'M C', escort: @m.b]\n}\n"
    stores:
      - type: "Unit"
        roots:
          - name: "content"
            path: "units.gesd"
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    errors:
      - code: "store.ambiguousReference"
        location:
          root: "content"
          path: "units.gesd"
          line: 5
          column: 28
```

---

## Test: pins need content hashes and a known store

Pinning a handle of a type whose version is not a content hash, or of a type without a store, is `store.invalidPin`.

### Case description

```yaml
gesBlock: case
id: store-invalid-pins
schemaApi:
  sources: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 }\nhandle @Hull of :Hull with id :Text max 32 versioned by hash(#ges)\nschema :Composition as { name: :Text max 64, hull: @Hull~ optional, escort: @Unit optional, leader: @Unit~ optional, cost: :Number integer between 0 and 1000 default 10, tag: @Tag optional, rivals: :Map(@Unit → :Number integer between 0 and 9) max 4 optional }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Tag of :Contact with id :Text max 16\nhandle @Slot of :Contact attached to @Tag with part :Number integer between 0 and 15\n"]
  store:
    files:
      - root: "content"
        path: "units.gesd"
        text: ":Store(@Unit)\na: [name: 'A', tag: @Tag:x~]\nb: [name: 'B', hull: @Hull:light~]\n"
    stores:
      - type: "Unit"
        roots:
          - name: "content"
            path: "units.gesd"
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    errors:
      - code: "store.invalidPin"
        location:
          root: "content"
          path: "units.gesd"
          line: 2
          column: 21
      - code: "store.invalidPin"
        location:
          root: "content"
          path: "units.gesd"
          line: 3
          column: 22
```

---

## Test: pinned references that form a cycle

Pinned references from a to b and back are `store.dependencyCycle` at the reference that closes the cycle; a self-pin is a cycle as well.

### Case description

```yaml
gesBlock: case
id: store-dependency-cycle
schemaApi:
  sources: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 }\nhandle @Hull of :Hull with id :Text max 32 versioned by hash(#ges)\nschema :Composition as { name: :Text max 64, hull: @Hull~ optional, escort: @Unit optional, leader: @Unit~ optional, cost: :Number integer between 0 and 1000 default 10, tag: @Tag optional, rivals: :Map(@Unit → :Number integer between 0 and 9) max 4 optional }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Tag of :Contact with id :Text max 16\nhandle @Slot of :Contact attached to @Tag with part :Number integer between 0 and 15\n"]
  store:
    files:
      - root: "content"
        path: "units.gesd"
        text: ":Store(@Unit)\na: [name: 'A', leader: @b~]\nb: [name: 'B', leader: @a~]\nc: [name: 'C', leader: @c~]\n"
    stores:
      - type: "Unit"
        roots:
          - name: "content"
            path: "units.gesd"
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    errors:
      - code: "store.dependencyCycle"
        location:
          root: "content"
          path: "units.gesd"
          line: 3
          column: 24
      - code: "store.dependencyCycle"
        location:
          root: "content"
          path: "units.gesd"
          line: 4
          column: 24
```

---

## Test: entries are checked against the state schema

Each entry reports at most one schema error with its field path; an unpinned handle in a pinned field fails the check.

### Case description

```yaml
gesBlock: case
id: store-schema-errors
schemaApi:
  sources: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 }\nhandle @Hull of :Hull with id :Text max 32 versioned by hash(#ges)\nschema :Composition as { name: :Text max 64, hull: @Hull~ optional, escort: @Unit optional, leader: @Unit~ optional, cost: :Number integer between 0 and 1000 default 10, tag: @Tag optional, rivals: :Map(@Unit → :Number integer between 0 and 9) max 4 optional }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Tag of :Contact with id :Text max 16\nhandle @Slot of :Contact attached to @Tag with part :Number integer between 0 and 15\n"]
  store:
    files:
      - root: "content"
        path: "hulls.gesd"
        text: ":Store(@Hull)\nlight: [name: 'Light', armor: 20]\nheavy: [name: 'Heavy', armor: 80]\n"
      - root: "content"
        path: "units.gesd"
        text: ":Store(@Unit)\na: [name: 'A', cost: 5000]\nb: [name: 'B', hull: @Hull:light, armor: 1]\nc: [cost: 1]\n"
    stores:
      - type: "Hull"
        roots:
          - name: "content"
            path: "hulls.gesd"
      - type: "Unit"
        roots:
          - name: "content"
            path: "units.gesd"
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    errors:
      - code: "document.outOfRange"
        location:
          root: "content"
          path: "units.gesd"
          line: 2
          column: 22
        fieldPath: "cost"
      - code: "document.fieldType"
        location:
          root: "content"
          path: "units.gesd"
          line: 3
          column: 22
        fieldPath: "hull"
      - code: "document.missingField"
        location:
          root: "content"
          path: "units.gesd"
          line: 4
          column: 4
        fieldPath: "name"
```

---

## Test: an entry value that is no Map

An entry whose value is no Map literal is `document.unexpectedToken` at its start.

### Case description

```yaml
gesBlock: case
id: store-not-a-map
schemaApi:
  sources: ["schema :Hull as { name: :Text max 32, armor: :Number integer between 0 and 100 }\nhandle @Hull of :Hull with id :Text max 32 versioned by hash(#ges)\nschema :Composition as { name: :Text max 64, hull: @Hull~ optional, escort: @Unit optional, leader: @Unit~ optional, cost: :Number integer between 0 and 1000 default 10, tag: @Tag optional, rivals: :Map(@Unit → :Number integer between 0 and 9) max 4 optional }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\nschema :Contact as { label: :Text max 16 }\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Tag of :Contact with id :Text max 16\nhandle @Slot of :Contact attached to @Tag with part :Number integer between 0 and 15\n"]
  store:
    files:
      - root: "content"
        path: "units.gesd"
        text: ":Store(@Unit)\na: 5\n"
    stores:
      - type: "Unit"
        roots:
          - name: "content"
            path: "units.gesd"
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    errors:
      - code: "document.unexpectedToken"
        location:
          root: "content"
          path: "units.gesd"
          line: 2
          column: 4
```

## Test: a store read from sources

Each source is a root handle of a type without state with the path of its root file: a root without a version is pinned by the version of its first answer, a root with one is asked at it, imports ask the root at the root-relative path, origins name the root handle as given, and the sources are the root handles with their versions.

### Case description

```yaml
gesBlock: case
id: store-handle-roots
schemaApi:
  sources: ["schema :Composition as { name: :Text max 16, escort: @Unit optional }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\nhandle @Repository with id :Text max 16 versioned by revision max 1000\n"]
  store:
    latest: 3
    files:
      - root: "content"
        path: "units.gesd"
        text: ":Store(@Unit)\nimport 'more.gesd'\nscout: [name: 'Scout']\n"
      - root: "content"
        path: "more.gesd"
        text: ":Store(@Unit)\nmodule extra { one: [name: 'One', escort: @scout] }\n"
      - root: "mod"
        path: "mod.gesd"
        text: ":Store(@Unit)\nmodule mod { two: [name: 'Two'] }\n"
    stores:
      - type: "Unit"
        sources:
          - handle: "@Repository:content~7"
            path: "units.gesd"
          - handle: "@Repository:mod"
            path: "mod.gesd"
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    entries:
      - id: "extra.one"
        handle: "@Unit:extra.one~15f58b6f29c8314325163aa044b637271a45efc8d04e367764b82f7677d74698"
        origin:
          root: "@Repository:content~7"
          path: "more.gesd"
          line: 2
          column: 16
        value:
          type: ":Composition"
          entries:
            - key: "escort"
              value:
                type: ":Handle"
                value: "@Unit:scout"
            - key: "name"
              value:
                type: ":Text"
                value: "One"
      - id: "mod.two"
        handle: "@Unit:mod.two~40f5efd57837aed432a64836d7c09be3103ae42b5f5e3eb1a0412d2969460088"
        origin:
          root: "@Repository:mod"
          path: "mod.gesd"
          line: 2
          column: 14
        value:
          type: ":Composition"
          entries:
            - key: "name"
              value:
                type: ":Text"
                value: "Two"
      - id: "scout"
        handle: "@Unit:scout~dc20cbd6764a0396a056b91678b026841cc3629021959ba35a0d3ddd376873bc"
        origin:
          root: "@Repository:content~7"
          path: "units.gesd"
          line: 3
          column: 1
        value:
          type: ":Composition"
          entries:
            - key: "name"
              value:
                type: ":Text"
                value: "Scout"
    sources:
      - "@Repository:content~7"
      - "@Repository:mod~3"
```

## Test: a root answers with one version

A root without a version is pinned by its first answer; a later file of that root answered with another version fails the read with `runtime.handleVersionMismatch`.

### Case description

```yaml
gesBlock: case
id: store-handle-version-mismatch
schemaApi:
  sources: ["schema :Composition as { name: :Text max 16, escort: @Unit optional }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\nhandle @Repository with id :Text max 16 versioned by revision max 1000\n"]
  store:
    files:
      - root: "content"
        path: "units.gesd"
        text: ":Store(@Unit)\nimport 'more.gesd'\nscout: [name: 'Scout']\n"
        version: 4
      - root: "content"
        path: "more.gesd"
        text: ":Store(@Unit)\none: [name: 'One']\n"
        version: 5
    stores:
      - type: "Unit"
        sources:
          - handle: "@Repository:content"
            path: "units.gesd"
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    failure: "runtime.handleVersionMismatch"
```

## Test: a missing root file and a path beyond 256 bytes

A root file the source does not have is `store.importMissing` for its root file; an import whose path is longer than 256 UTF-8 bytes is `store.importMissing` at its path without a request.

### Case description

```yaml
gesBlock: case
id: store-handle-missing
schemaApi:
  sources: ["schema :Composition as { name: :Text max 16, escort: @Unit optional }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\nhandle @Repository with id :Text max 16 versioned by revision max 1000\n"]
  store:
    files:
      - root: "content"
        path: "units.gesd"
        text: ":Store(@Unit)\nimport 'd/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/all.gesd'\nscout: [name: 'Scout']\n"
      - root: "content"
        path: "d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/d/all.gesd"
        text: ":Store(@Unit)\none: [name: 'One']\n"
    stores:
      - type: "Unit"
        sources:
          - handle: "@Repository:content"
            path: "units.gesd"
          - handle: "@Repository:gone"
            path: "units.gesd"
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    errors:
      - code: "store.importMissing"
        location:
          root: "@Repository:content"
          path: "units.gesd"
          line: 2
          column: 8
      - code: "store.importMissing"
        location:
          root: "@Repository:gone"
          path: "units.gesd"
```

---

## Test: the handles of a store through its projections

The index of a projection holds every entry's handle in id order with the projection's version: for a `:ContentHash` type the content hash of the projected state, which differs from the entry's own hash and is equal for entries that differ only outside the projection; for a revision type the entry's version.

### Case description

```yaml
gesBlock: case
id: store-projection-index
schemaApi:
  sources: ["schema :Composition as { name: :Text max 32, icon: :Text max 16, mass: :Number integer between 0 and 1000 }\nprojection :UnitDisplay of :Composition as { name, icon }\nprojection :UnitLabel of :UnitDisplay as { name }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\n"]
  store:
    files:
      - root: "content"
        path: "units.gesd"
        text: ":Store(@Unit)\nmodule ruin {\n  radarTower: [name: 'Radar tower', icon: 'radar', mass: 400]\n  heavyTower: [name: 'Radar tower', icon: 'radar', mass: 900]\n}\nscout: [name: 'Scout', icon: 'eye', mass: 10]\n"
    stores:
      - type: "Unit"
        roots:
          - name: "content"
            path: "units.gesd"
    index: ["UnitDisplay", "UnitLabel"]
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    entries:
      - id: "ruin.heavyTower"
        handle: "@Unit:ruin.heavyTower~8149ccc400a67787dc97f9d73e2a2d2bd7884381384cf89aeb3dbc5abafb8966"
        origin:
          root: "content"
          path: "units.gesd"
          line: 4
          column: 3
        value:
          type: ":Composition"
          entries:
            - key: "icon"
              value:
                type: ":Text"
                value: "radar"
            - key: "mass"
              value:
                type: ":Number.int64"
                value: "900"
            - key: "name"
              value:
                type: ":Text"
                value: "Radar tower"
      - id: "ruin.radarTower"
        handle: "@Unit:ruin.radarTower~fcc4c210fc5a34e5fac5c7e7063e23c205fc62bd7d585dc946672f91e7ade22c"
        origin:
          root: "content"
          path: "units.gesd"
          line: 3
          column: 3
        value:
          type: ":Composition"
          entries:
            - key: "icon"
              value:
                type: ":Text"
                value: "radar"
            - key: "mass"
              value:
                type: ":Number.int64"
                value: "400"
            - key: "name"
              value:
                type: ":Text"
                value: "Radar tower"
      - id: "scout"
        handle: "@Unit:scout~f78e40e502db85826ec924bc948de7ff64499f64570ac1c08ceaed101eaa0566"
        origin:
          root: "content"
          path: "units.gesd"
          line: 6
          column: 1
        value:
          type: ":Composition"
          entries:
            - key: "icon"
              value:
                type: ":Text"
                value: "eye"
            - key: "mass"
              value:
                type: ":Number.int64"
                value: "10"
            - key: "name"
              value:
                type: ":Text"
                value: "Scout"
    indexes:
      UnitDisplay:
        - "@Unit:ruin.heavyTower~b4a10c1306cc17f26ac5ed5c8c6204d81349ab8c8b9fbf0f50d1966f299fc4b9"
        - "@Unit:ruin.radarTower~b4a10c1306cc17f26ac5ed5c8c6204d81349ab8c8b9fbf0f50d1966f299fc4b9"
        - "@Unit:scout~88fab1c785f11db20cd7f36b8e20598801ac315e0ff36cc3e7e38c0f81ba4575"
      UnitLabel:
        - "@Unit:ruin.heavyTower~6c9d6c51351fb59d0556187800d9c4275b78dbbd323f668a6ebf50e740026e0f"
        - "@Unit:ruin.radarTower~6c9d6c51351fb59d0556187800d9c4275b78dbbd323f668a6ebf50e740026e0f"
        - "@Unit:scout~c0d40774333ae807ae8f77b6526787ab7c06a92b301e82d7368b2142a1e0c811"
```

---

## Test: index versions over defaults, absent fields, nested paths, Lists and handles

The version of an entry in a projection's index is the content hash of the projected state normalized against the
projection, so every port computes the same version: a default the entry leaves out counts as written, an absent
optional field as absent, a nested path as the Record it builds, and a reference as its handle without a version.

### Case description

```yaml
gesBlock: case
id: store-index-versions
schemaApi:
  sources: ["schema :Look as { icon: :Text max 16, scale: :Number between 0 and 4 step 0.25 default 1 }\nschema :Composition as { name: :Text max 32, look: :Look, tags: :List(:Text max 8) max 4 default [], escort: @Unit optional, mass: :Number integer between 0 and 1000 }\nprojection :UnitCard of :Composition as { name, icon: look.icon, scale: look.scale, tags, escort }\nhandle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\n"]
  store:
    files:
      - root: "content"
        path: "units.gesd"
        text: ":Store(@Unit)\nscout: [name: 'Scout', look: [icon: 'eye'], mass: 10]\nscoutHeavy: [name: 'Scout', look: [icon: 'eye', scale: 1], tags: [], mass: 900]\nguard: [name: 'Guard', look: [icon: 'shield', scale: 2.5], tags: ['front', 'slow'], escort: @scout, mass: 300]\n"
    stores:
      - type: "Unit"
        roots:
          - name: "content"
            path: "units.gesd"
    index: ["UnitCard"]
```

### Expectation

```yaml
gesBlock: expect
schema:
  store:
    entries:
      - id: "guard"
        handle: "@Unit:guard~21cdc93df90066542ad6c7e7b7a23a3f64e4eaff459bd100b769ea29ed24bd09"
        origin:
          root: "content"
          path: "units.gesd"
          line: 4
          column: 1
        value:
          type: ":Composition"
          entries:
            - key: "escort"
              value:
                type: ":Handle"
                value: "@Unit:scout"
            - key: "look"
              value:
                type: ":Look"
                entries:
                  - key: "icon"
                    value:
                      type: ":Text"
                      value: "shield"
                  - key: "scale"
                    value:
                      type: ":Number.binary64"
                      value: "2.5"
            - key: "mass"
              value:
                type: ":Number.int64"
                value: "300"
            - key: "name"
              value:
                type: ":Text"
                value: "Guard"
            - key: "tags"
              value:
                type: ":List"
                items:
                  - type: ":Text"
                    value: "front"
                  - type: ":Text"
                    value: "slow"
      - id: "scout"
        handle: "@Unit:scout~9adb1ffc45e56118bb8c6c4020ba076eefce3fd87cc3418b45fc447e72cdd496"
        origin:
          root: "content"
          path: "units.gesd"
          line: 2
          column: 1
        value:
          type: ":Composition"
          entries:
            - key: "look"
              value:
                type: ":Look"
                entries:
                  - key: "icon"
                    value:
                      type: ":Text"
                      value: "eye"
                  - key: "scale"
                    value:
                      type: ":Number.int64"
                      value: "1"
            - key: "mass"
              value:
                type: ":Number.int64"
                value: "10"
            - key: "name"
              value:
                type: ":Text"
                value: "Scout"
            - key: "tags"
              value:
                type: ":List"
                items: []
      - id: "scoutHeavy"
        handle: "@Unit:scoutHeavy~726d0b1a0c36b703adadf3fab9020af4af16dc0d4420371083a325f354fbea51"
        origin:
          root: "content"
          path: "units.gesd"
          line: 3
          column: 1
        value:
          type: ":Composition"
          entries:
            - key: "look"
              value:
                type: ":Look"
                entries:
                  - key: "icon"
                    value:
                      type: ":Text"
                      value: "eye"
                  - key: "scale"
                    value:
                      type: ":Number.int64"
                      value: "1"
            - key: "mass"
              value:
                type: ":Number.int64"
                value: "900"
            - key: "name"
              value:
                type: ":Text"
                value: "Scout"
            - key: "tags"
              value:
                type: ":List"
                items: []
    indexes:
      UnitCard:
        - "@Unit:guard~7b5cc499b99051280d15a910d580d20b7f88d3d114968638fd56895115173d64"
        - "@Unit:scout~42f205ca1fe5cee769f8a6310e8818528228559d902f2d4ce8ba43226ec5dbff"
        - "@Unit:scoutHeavy~42f205ca1fe5cee769f8a6310e8818528228559d902f2d4ce8ba43226ec5dbff"
```
