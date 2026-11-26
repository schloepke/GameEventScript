---
formatVersion: 1
suiteId: runtime.handles
title: Handles in scripts
kind: scriptApi
level: scenario
categories: [conformance]
tags: [handles, schemas, host]
---

# Handles in scripts

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

> **Since: Unreleased**

These cases verify [handles](../../../specs/Language.md#handles) in scripts: handle parameter types and unions, `is`
checks, the identity members, lazy state reads through the host's resolvers with their per-invocation cache, version
checks and the content hash, `as :State`, Map keys and handle-named timers
([Host runtime](../../../specs/HostRuntime.md#handles)). Each case declares its handle catalog and scripted resolver
answers in a `handles` block ([Markdown format](../../../specs/Conformance/MarkdownFormat.md#handle-catalogs)).

---

## Test: identity members need no state

`id`, `version`, `root`, `id.root`, `id.part` and `as :Text` read the handle itself, so no resolver is registered.

### Case description

```yaml
gesBlock: case
id: identity-members
sources:
  - name: identity-members.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @RepoFile of :Contact composite @Repository as { part: :Text max 256 }\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(target as @Target, drone as @Drone, file as @RepoFile) {
  emit Out(targetId: target.id, droneVersion: drone.version, part: file.id.part, rootId: file.id.root, fileVersion: file.version, root: file.root, text: target as :Text, droneText: drone as :Text)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| seen | Seen | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  seen:
    input:
      args:
        - name: target
          value: { type: ":Handle", value: "@Target:3" }
        - name: drone
          value: { type: ":Handle", value: "@Drone:7~120" }
        - name: file
          value: { type: ":Handle", value: "@RepoFile:content~3f2a000000000000000000000000000000000001/Units/radar.gesd" }
    local:
      - name: Out
        args:
          - name: targetId
            value: { type: ":Number.int64", value: "3" }
          - name: droneVersion
            value: { type: ":Number.int64", value: "120" }
          - name: part
            value: { type: ":Text", value: "Units/radar.gesd" }
          - name: rootId
            value: { type: ":Text", value: "content" }
          - name: fileVersion
            value: { type: ":Bytes", value: "3f2a000000000000000000000000000000000001" }
          - name: root
            value: { type: ":Handle", value: "@Repository:content~3f2a000000000000000000000000000000000001" }
          - name: text
            value: { type: ":Text", value: "@Target:3" }
          - name: droneText
            value: { type: ":Text", value: "@Drone:7~120" }
```

---

## Test: handle parameters check without converting

A union binds a handle of either type; `~` requires a version; a value of another kind binds `nothing`. `is` tests the same in any expression.

### Case description

```yaml
gesBlock: case
id: parameter-checks-and-unions
sources:
  - name: parameter-checks-and-unions.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @RepoFile of :Contact composite @Repository as { part: :Text max 256 }\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(subject as @Drone | @Target, pilot as @Drone~, other as @Target, raw) {
  emit Out(subject: subject, pilot: pilot, other: other, isTarget: subject is @Target, isDrone: subject is @Drone, rawVersioned: raw is @Drone~, rawDrone: raw is @Drone)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| seen | Seen | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  seen:
    input:
      args:
        - name: subject
          value: { type: ":Handle", value: "@Target:5" }
        - name: pilot
          value: { type: ":Handle", value: "@Drone:7" }
        - name: other
          value: { type: ":Text", value: "@Target:5" }
        - name: raw
          value: { type: ":Handle", value: "@Drone:7~2" }
    local:
      - name: Out
        args:
          - name: subject
            value: { type: ":Handle", value: "@Target:5" }
          - name: pilot
            value: { type: ":Nothing" }
          - name: other
            value: { type: ":Nothing" }
          - name: isTarget
            value: { type: ":Boolean", value: "true" }
          - name: isDrone
            value: { type: ":Boolean", value: "false" }
          - name: rawVersioned
            value: { type: ":Boolean", value: "true" }
          - name: rawDrone
            value: { type: ":Boolean", value: "true" }
```

---

## Test: equality ignores the version

Two handles of the same object are equal with and without a version; handles of other types or ids differ, and ordering comparisons give `nothing`.

### Case description

```yaml
gesBlock: case
id: equality-ignores-version
sources:
  - name: equality-ignores-version.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @RepoFile of :Contact composite @Repository as { part: :Text max 256 }\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(a as @Drone, b as @Drone, c as @Drone, t as @Target) {
  emit Out(same: a = b, other: a = c, kind: a = t, less: a < c)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| seen | Seen | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  seen:
    input:
      args:
        - name: a
          value: { type: ":Handle", value: "@Drone:7~2" }
        - name: b
          value: { type: ":Handle", value: "@Drone:7" }
        - name: c
          value: { type: ":Handle", value: "@Drone:8" }
        - name: t
          value: { type: ":Handle", value: "@Target:7" }
    local:
      - name: Out
        args:
          - name: same
            value: { type: ":Boolean", value: "true" }
          - name: other
            value: { type: ":Boolean", value: "false" }
          - name: kind
            value: { type: ":Boolean", value: "false" }
          - name: less
            value: { type: ":Nothing" }
```

---

## Test: a state read resolves a document answer

A member other than the identity members reads the state; the resolver answers with a document naming the state schema.

### Case description

```yaml
gesBlock: case
id: state-read-document
sources:
  - name: state-read-document.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @RepoFile of :Contact composite @Repository as { part: :Text max 256 }\n"]
  answers:
    - { handle: "@Target:3", document: ":Schema(:Contact)\nlabel: 'Alpha'\nstrength: 42\n" }
```

### Source code under test

```ges
module main
on Seen(target as @Target) {
  emit Out(label: target.label, strength: target.strength)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| seen | Seen | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  seen:
    input:
      args:
        - name: target
          value: { type: ":Handle", value: "@Target:3" }
    local:
      - name: Out
        args:
          - name: label
            value: { type: ":Text", value: "Alpha" }
          - name: strength
            value: { type: ":Number.int64", value: "42" }
```

---

## Test: a state read resolves a Record answer and `as :State`

The resolver answers with a Record of the state schema; `as :Contact` gives the whole state.

### Case description

```yaml
gesBlock: case
id: state-read-record
sources:
  - name: state-read-record.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @RepoFile of :Contact composite @Repository as { part: :Text max 256 }\n"]
  answers:
    - { handle: "@Target:4", record: ":Record(\"Contact\", [label: 'Beta', strength: 7])" }
```

### Source code under test

```ges
module main
on Seen(target as @Target) {
  let state be target as :Contact
  emit Out(label: state.label, isContact: state is :Contact, strength: target.strength)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| seen | Seen | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  seen:
    input:
      args:
        - name: target
          value: { type: ":Handle", value: "@Target:4" }
    local:
      - name: Out
        args:
          - name: label
            value: { type: ":Text", value: "Beta" }
          - name: isContact
            value: { type: ":Boolean", value: "true" }
          - name: strength
            value: { type: ":Number.int64", value: "7" }
```

---

## Test: a missing object reads as nothing

Every state read of a missing object and its `as :State` give `nothing`.

### Case description

```yaml
gesBlock: case
id: state-missing
sources:
  - name: state-missing.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @RepoFile of :Contact composite @Repository as { part: :Text max 256 }\n"]
  answers:
    - { handle: "@Target:9", missing: true }
```

### Source code under test

```ges
module main
on Seen(target as @Target) {
  emit Out(label: target.label, state: target as :Contact, id: target.id)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| seen | Seen | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  seen:
    input:
      args:
        - name: target
          value: { type: ":Handle", value: "@Target:9" }
    local:
      - name: Out
        args:
          - name: label
            value: { type: ":Nothing" }
          - name: state
            value: { type: ":Nothing" }
          - name: id
            value: { type: ":Number.int64", value: "9" }
```

---

## Test: one answer per object and handler invocation

Within one invocation every read of the same object uses the first answer; the next invocation reads again.

### Case description

```yaml
gesBlock: case
id: state-cache-per-invocation
sources:
  - name: state-cache-per-invocation.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @RepoFile of :Contact composite @Repository as { part: :Text max 256 }\n"]
  answers:
    - { handle: "@Target:3", document: ":Schema(:Contact)\nlabel: 'Alpha'\nstrength: 42\n" }
    - { handle: "@Target:3", document: ":Schema(:Contact)\nlabel: 'Beta'\nstrength: 7\n" }
```

### Source code under test

```ges
module main
on Seen(target as @Target) {
  emit Out(first: target.strength, second: target.strength, label: target.label)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| first | Seen | completion | |
| second | Seen | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  first:
    input:
      args:
        - name: target
          value: { type: ":Handle", value: "@Target:3" }
    local:
      - name: Out
        args:
          - name: first
            value: { type: ":Number.int64", value: "42" }
          - name: second
            value: { type: ":Number.int64", value: "42" }
          - name: label
            value: { type: ":Text", value: "Alpha" }
  second:
    input:
      args:
        - name: target
          value: { type: ":Handle", value: "@Target:3" }
    local:
      - name: Out
        args:
          - name: first
            value: { type: ":Number.int64", value: "7" }
          - name: second
            value: { type: ":Number.int64", value: "7" }
          - name: label
            value: { type: ":Text", value: "Beta" }
```

---

## Test: the state is checked against its schema

A document answer that does not fit the state schema fails with `runtime.invalidHandleState` and stops the handler.

### Case description

```yaml
gesBlock: case
id: state-defaults-applied
sources:
  - name: state-defaults-applied.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @RepoFile of :Contact composite @Repository as { part: :Text max 256 }\n"]
  answers:
    - { handle: "@Target:3", document: ":Schema(:Contact)\nlabel: 'Alpha'\nstrength: 420\n" }
```

### Source code under test

```ges
module main
on Seen(target as @Target) {
  emit Before()
  emit Out(strength: target.strength)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| seen | Seen | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  seen:
    input:
      args:
        - name: target
          value: { type: ":Handle", value: "@Target:3" }
    local:
      - name: Before
    diagnostics:
      - phase: runtime
        code: runtime.invalidHandleState
        symbol: "@Target:3"
        programName: "main"
        handlerName: "Seen(target)"
```

---

## Test: a document must name the state schema

A document whose header names another schema fails with `runtime.invalidHandleState`.

### Case description

```yaml
gesBlock: case
id: state-wrong-schema
sources:
  - name: state-wrong-schema.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @RepoFile of :Contact composite @Repository as { part: :Text max 256 }\n"]
  answers:
    - { handle: "@Target:3", document: ":Schema(:Source)\ntext: 'x'\n" }
```

### Source code under test

```ges
module main
on Seen(target as @Target) {
  emit Out(strength: target.strength)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| seen | Seen | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  seen:
    input:
      args:
        - name: target
          value: { type: ":Handle", value: "@Target:3" }
    diagnostics:
      - phase: runtime
        code: runtime.invalidHandleState
        symbol: "@Target:3"
        programName: "main"
        handlerName: "Seen(target)"
```

---

## Test: a failing resolver stops the handler

A resolver that fails ends the read with `runtime.handleResolveFailed`; the next message runs normally.

### Case description

```yaml
gesBlock: case
id: resolver-failure
sources:
  - name: resolver-failure.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @RepoFile of :Contact composite @Repository as { part: :Text max 256 }\n"]
  answers:
    - { handle: "@Target:3", fail: true }
```

### Source code under test

```ges
module main
on Seen(target as @Target) {
  emit Out(strength: target.strength)
}
on Ping { emit Pong() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| seen | Seen | completion | |
| ping | Ping | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  seen:
    input:
      args:
        - name: target
          value: { type: ":Handle", value: "@Target:3" }
    diagnostics:
      - phase: runtime
        code: runtime.handleResolveFailed
        symbol: "@Target:3"
        programName: "main"
        handlerName: "Seen(target)"
  ping:
    local:
      - name: Pong
```

---

## Test: a revision type takes the passed version

A handle without a version picks up the version the resolver passed for later reads, while `h.version` keeps the carried one. A passed version that differs from the carried one fails with `runtime.handleVersionMismatch`.

### Case description

```yaml
gesBlock: case
id: revision-passed
sources:
  - name: revision-passed.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @RepoFile of :Contact composite @Repository as { part: :Text max 256 }\n"]
  answers:
    - { handle: "@Drone:7", document: ":Schema(:DroneState)\naltitude: 120\ncallsign: 'Gnabblo'\n", version: { type: ":Number.int64", value: "3" } }
```

### Source code under test

```ges
module main
on Seen(drone as @Drone) {
  emit Out(altitude: drone.altitude, version: drone.version)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| plain | Seen | completion | |
| carried | Seen | completion | |
| other | Seen | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  plain:
    input:
      args:
        - name: drone
          value: { type: ":Handle", value: "@Drone:7" }
    local:
      - name: Out
        args:
          - name: altitude
            value: { type: ":Number.int64", value: "120" }
          - name: version
            value: { type: ":Nothing" }
  carried:
    input:
      args:
        - name: drone
          value: { type: ":Handle", value: "@Drone:7~3" }
    local:
      - name: Out
        args:
          - name: altitude
            value: { type: ":Number.int64", value: "120" }
          - name: version
            value: { type: ":Number.int64", value: "3" }
  other:
    input:
      args:
        - name: drone
          value: { type: ":Handle", value: "@Drone:7~4" }
    diagnostics:
      - phase: runtime
        code: runtime.handleVersionMismatch
        symbol: "@Drone:7~4"
        programName: "main"
        handlerName: "Seen(drone)"
```

---

## Test: an unversioned type passes no version

A resolver of a type without versions that passes a version fails with `runtime.invalidHandleState`.

### Case description

```yaml
gesBlock: case
id: unversioned-passes-version
sources:
  - name: unversioned-passes-version.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @RepoFile of :Contact composite @Repository as { part: :Text max 256 }\n"]
  answers:
    - { handle: "@Target:3", document: ":Schema(:Contact)\nlabel: 'Alpha'\nstrength: 42\n", version: { type: ":Number.int64", value: "1" } }
```

### Source code under test

```ges
module main
on Seen(target as @Target) {
  emit Out(strength: target.strength)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| seen | Seen | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  seen:
    input:
      args:
        - name: target
          value: { type: ":Handle", value: "@Target:3" }
    diagnostics:
      - phase: runtime
        code: runtime.invalidHandleState
        symbol: "@Target:3"
        programName: "main"
        handlerName: "Seen(target)"
```

---

## Test: GES computes the content hash

The content hash of the state `label: 'Alpha', strength: 42` is `a2951261887894b7bc7f72d4af023c7b7576677f76283d8a524f2496b19cdc50`. A handle carrying it reads the state; one carrying another hash, or a passed version that differs, fails with `runtime.handleVersionMismatch`.

### Case description

```yaml
gesBlock: case
id: content-hash-computed
sources:
  - name: content-hash-computed.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @RepoFile of :Contact composite @Repository as { part: :Text max 256 }\n"]
  answers:
    - { handle: "@Contacted:1", document: ":Schema(:Contact)\nlabel: 'Alpha'\nstrength: 42\n" }
    - { handle: "@Contacted:2", document: ":Schema(:Contact)\nlabel: 'Alpha'\nstrength: 42\n" }
    - { handle: "@Contacted:3", document: ":Schema(:Contact)\nlabel: 'Alpha'\nstrength: 42\n" }
    - { handle: "@Contacted:4", document: ":Schema(:Contact)\nlabel: 'Alpha'\nstrength: 42\n", version: { type: ":Bytes", value: "1111111111111111111111111111111111111111111111111111111111111111" } }
```

### Source code under test

```ges
module main
on Seen(contact as @Contacted) {
  emit Out(label: contact.label)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| plain | Seen | completion | |
| carried | Seen | completion | |
| stale | Seen | completion | |
| passed | Seen | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  plain:
    input:
      args:
        - name: contact
          value: { type: ":Handle", value: "@Contacted:1" }
    local:
      - name: Out
        args:
          - name: label
            value: { type: ":Text", value: "Alpha" }
  carried:
    input:
      args:
        - name: contact
          value: { type: ":Handle", value: "@Contacted:2~a2951261887894b7bc7f72d4af023c7b7576677f76283d8a524f2496b19cdc50" }
    local:
      - name: Out
        args:
          - name: label
            value: { type: ":Text", value: "Alpha" }
  stale:
    input:
      args:
        - name: contact
          value: { type: ":Handle", value: "@Contacted:3~0000000000000000000000000000000000000000000000000000000000000000" }
    diagnostics:
      - phase: runtime
        code: runtime.handleVersionMismatch
        symbol: "@Contacted:3~0000000000000000000000000000000000000000000000000000000000000000"
        programName: "main"
        handlerName: "Seen(contact)"
  passed:
    input:
      args:
        - name: contact
          value: { type: ":Handle", value: "@Contacted:4" }
    diagnostics:
      - phase: runtime
        code: runtime.handleVersionMismatch
        symbol: "@Contacted:4"
        programName: "main"
        handlerName: "Seen(contact)"
```

---

## Test: the content hash covers the normalized state

A Record answer with a binary64 strength hashes like the document with an integer one.

### Case description

```yaml
gesBlock: case
id: content-hash-normalized
sources:
  - name: content-hash-normalized.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @RepoFile of :Contact composite @Repository as { part: :Text max 256 }\n"]
  answers:
    - { handle: "@Contacted:2", record: ":Record(\"Contact\", [label: 'Alpha', strength: 42.0])" }
```

### Source code under test

```ges
module main
on Seen(contact as @Contacted) {
  emit Out(strength: contact.strength)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| seen | Seen | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  seen:
    input:
      args:
        - name: contact
          value: { type: ":Handle", value: "@Contacted:2~a2951261887894b7bc7f72d4af023c7b7576677f76283d8a524f2496b19cdc50" }
    local:
      - name: Out
        args:
          - name: strength
            value: { type: ":Number.int64", value: "42" }
```

---

## Test: handles are Map keys

`[:map x by key]` keeps a handle key; `m[h]` and `h in m` look it up, with or without a version.

### Case description

```yaml
gesBlock: case
id: map-keys
sources:
  - name: map-keys.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @RepoFile of :Contact composite @Repository as { part: :Text max 256 }\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(drones) {
  let byDrone be drones[:map d by d => d as :Text]
  let first be drones[:first]
  emit Out(count: byDrone[:count], found: byDrone[first], member: first in byDrone, keys: byDrone[:keys][:first] is @Drone)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| seen | Seen | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  seen:
    input:
      args:
        - name: drones
          value: { type: ":List", items: [{ type: ":Handle", value: "@Drone:7~1" }, { type: ":Handle", value: "@Drone:2" }] }
    local:
      - name: Out
        args:
          - name: count
            value: { type: ":Number.int64", value: "2" }
          - name: found
            value: { type: ":Text", value: "@Drone:7~1" }
          - name: member
            value: { type: ":Boolean", value: "true" }
          - name: keys
            value: { type: ":Boolean", value: "true" }
```

---

## Test: handles sort and group by the key order

Text comes first, then handles by type name, integer ids numerically and Text ids in scalar order; a composite orders by its root and then its part. Grouping keeps handle keys.

### Case description

```yaml
gesBlock: case
id: sort-and-group-order
sources:
  - name: sort-and-group-order.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @RepoFile of :Contact composite @Repository as { part: :Text max 256 }\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(items) {
  emit Out(sorted: items[:sort ascending], groups: items[:group by item => item][:keys])
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| seen | Seen | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  seen:
    input:
      args:
        - name: items
          value: { type: ":List", items: [{ type: ":Handle", value: "@Target:10" }, { type: ":Handle", value: "@RepoFile:b/z" }, { type: ":Handle", value: "@Drone:7~1" }, { type: ":Text", value: "x" }, { type: ":Handle", value: "@Target:9" }, { type: ":Handle", value: "@RepoFile:a/z" }, { type: ":Handle", value: "@RepoFile:b/a" }] }
    local:
      - name: Out
        args:
          - name: sorted
            value: { type: ":List", items: [{ type: ":Text", value: "x" }, { type: ":Handle", value: "@Drone:7~1" }, { type: ":Handle", value: "@RepoFile:a/z" }, { type: ":Handle", value: "@RepoFile:b/a" }, { type: ":Handle", value: "@RepoFile:b/z" }, { type: ":Handle", value: "@Target:9" }, { type: ":Handle", value: "@Target:10" }] }
          - name: groups
            value: { type: ":List", items: [{ type: ":Text", value: "x" }, { type: ":Handle", value: "@Drone:7~1" }, { type: ":Handle", value: "@RepoFile:a/z" }, { type: ":Handle", value: "@RepoFile:b/a" }, { type: ":Handle", value: "@RepoFile:b/z" }, { type: ":Handle", value: "@Target:9" }, { type: ":Handle", value: "@Target:10" }] }
```

---

## Test: a handle names a timer

A named timer per handle keeps one timer per object, so the second send replaces the first; a handle name differs from the Text name of its text form, which the pending-timer list also shows as `@Drone:7`.

### Case description

```yaml
gesBlock: case
id: timer-named-by-handle
sources:
  - name: timer-named-by-handle.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\nhandle @Contacted of :Contact as { id: :Number integer between 0 and 65535, version: :ContentHash }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @RepoFile of :Contact composite @Repository as { part: :Text max 256 }\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(drone as @Drone) {
  emit after 1s named drone Forget(drone: drone)
  emit after 1s named drone Forget(drone: drone)
  emit after 1s named (drone as :Text) Forget(drone: drone)
}
on Forget(drone) { emit Gone() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| seen | Seen | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  seen:
    input:
      args:
        - name: drone
          value: { type: ":Handle", value: "@Drone:7" }
    waiting: true
    pendingTimers:
      - { name: "@Drone:7", deadline: "1000000", period: "0", message: Forget, program: main, publish: false }
      - { name: "@Drone:7", deadline: "1000000", period: "0", message: Forget, program: main, publish: false }
    local:
      - { name: Forget, args: [{ name: drone, value: { type: ":Handle", value: "@Drone:7" } }] }
      - { name: Forget, args: [{ name: drone, value: { type: ":Handle", value: "@Drone:7" } }] }
      - { name: Forget, args: [{ name: drone, value: { type: ":Handle", value: "@Drone:7" } }] }
```

---

## Test: the text form uses the bare form whenever it applies

`as :Text` writes the [text form](../../../specs/Language.md#handle-text-form): a Text id or part is bare when it
consists of ASCII letters, digits, `.`, `_` and `-` and does not read as an integer, and is otherwise quoted with a
doubled quote inside. `parse` does not recognize handles, so the text stays Text and `parse strict` gives `nothing`.

### Case description

```yaml
gesBlock: case
id: text-form
sources:
  - name: text-form.ges
    program: main
handles:
  schemas: ["schema :Source as { text: :Text }\nschema :Composition as { name: :Text max 64 }\nhandle @Unit of :Composition as { id: :Text max 64, version: :Nothing }\nhandle @Repository of :Source as { id: :Text max 64, version: :Hash(#sha1) }\nhandle @RepoFile of :Source composite @Repository as { part: :Text max 256 }\nhandle @UnitSlot of :Composition composite @Unit as { part: :Number integer between 0 and 16 }\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(bare as @Unit, spaced as @Unit, digits as @Unit, quoted as @Unit, file as @RepoFile, slot as @UnitSlot) {
  let text be bare as :Text
  emit Out(bare: text, spaced: spaced as :Text, digits: digits as :Text, quoted: quoted as :Text, file: file as :Text, slot: slot as :Text, parsed: parse text, strict: parse strict text)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| seen | Seen | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  seen:
    input:
      args:
        - name: bare
          value: { type: ":Handle", value: "@Unit:'drone.gnabblo'" }
        - name: spaced
          value: { type: ":Handle", value: "@Unit:'One Two'" }
        - name: digits
          value: { type: ":Handle", value: "@Unit:'7'" }
        - name: quoted
          value: { type: ":Handle", value: "@Unit:'it''s'" }
        - name: file
          value: { type: ":Handle", value: "@RepoFile:content~3f2a000000000000000000000000000000000001/'Tier 1/radar.gesd'" }
        - name: slot
          value: { type: ":Handle", value: "@UnitSlot:drone.gnabblo/1" }
    local:
      - name: Out
        args:
          - name: bare
            value: { type: ":Text", value: "@Unit:drone.gnabblo" }
          - name: spaced
            value: { type: ":Text", value: "@Unit:'One Two'" }
          - name: digits
            value: { type: ":Text", value: "@Unit:'7'" }
          - name: quoted
            value: { type: ":Text", value: "@Unit:'it''s'" }
          - name: file
            value: { type: ":Text", value: "@RepoFile:content~3f2a000000000000000000000000000000000001/'Tier 1/radar.gesd'" }
          - name: slot
            value: { type: ":Text", value: "@UnitSlot:drone.gnabblo/1" }
          - name: parsed
            value: { type: ":Text", value: "@Unit:drone.gnabblo" }
          - name: strict
            value: { type: ":Nothing" }
```
