---
formatVersion: 1
suiteId: compile.handles
title: Handle types in compilation and linking
kind: compileError
level: atomic
categories: [conformance, compiler]
tags: [handles, schemas, linking, diagnostics]
---

# Handle types in compilation and linking

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

> **Since: Unreleased**

These cases verify where scripts may name [handle types](../../../specs/Language.md#handles), the diagnostics
`validate.unknownHandleType`, `validate.invalidHandleUse`, `validate.invalidProjection` and
`validate.handleWidening` ([projections](../../../specs/Language.md#projections)), the `HandleType` import
([Bytecode](../../../specs/Bytecode.md)) and its link checks `link.missingHandleResolver` and
`link.mismatchedHandleType` ([Host runtime](../../../specs/HostRuntime.md#handles)). Each case declares its handle
store in a `handles` block ([Markdown format](../../../specs/Conformance/MarkdownFormat.md#handle-setup)).

---

## Test: a parameter names an unknown handle type

A handle type outside the compilation's store is `validate.unknownHandleType`.

### Case description

```yaml
gesBlock: case
id: unknown-parameter-type
sources:
  - name: unknown-parameter-type.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(target as @Ghost) { emit Out(target: target) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.unknownHandleType
  symbol: "@Ghost"
  sourceName: unknown-parameter-type.ges
  line: 2
  column: 9
  endLine: 2
  endColumn: 25
```

---

## Test: without a store no handle type is known

A compilation without handle types knows no `@Name`.

### Case description

```yaml
gesBlock: case
id: unknown-without-store
sources:
  - name: unknown-without-store.ges
    program: main
```

### Source code under test

```ges
module main
on Seen(target as @Drone) { emit Out(target: target) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.unknownHandleType
  symbol: "@Drone"
  sourceName: unknown-without-store.ges
  line: 2
  column: 9
  endLine: 2
  endColumn: 25
```

---

## Test: every union alternative must be known

Each alternative of a union is checked on its own.

### Case description

```yaml
gesBlock: case
id: unknown-union-alternative
sources:
  - name: unknown-union-alternative.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(target as @Drone | @Ghost) { emit Out(target: target) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.unknownHandleType
  symbol: "@Ghost"
  sourceName: unknown-union-alternative.ges
  line: 2
  column: 9
  endLine: 2
  endColumn: 34
```

---

## Test: an `is` check names an unknown handle type

`is @Name` is checked against the store like a parameter type.

### Case description

```yaml
gesBlock: case
id: unknown-type-check
sources:
  - name: unknown-type-check.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(target) { emit Out(match: target is @Ghost) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.unknownHandleType
  symbol: "@Ghost"
  sourceName: unknown-type-check.ges
  line: 2
  column: 35
  endLine: 2
  endColumn: 41
```

---

## Test: a record field names an unknown handle type

Record fields may be handle types and are checked against the store.

### Case description

```yaml
gesBlock: case
id: unknown-field-type
sources:
  - name: unknown-field-type.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: []
```

### Source code under test

```ges
module main
record :Crew as { pilot: @Ghost }
on Seen(target) { emit Out(crew: :Crew(pilot: target)) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.unknownHandleType
  symbol: "@Ghost"
  sourceName: unknown-field-type.ges
  line: 2
  column: 19
  endLine: 2
  endColumn: 32
```

---

## Test: `as @Name` is no cast

Outside a parameter or field `as @Name` is `validate.invalidHandleUse`; `is @Name` tests a value.

### Case description

```yaml
gesBlock: case
id: cast-to-handle
sources:
  - name: cast-to-handle.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(target) {
  let drone be target as @Drone
  emit Out(drone: drone)
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.invalidHandleUse
  symbol: "@Drone"
  sourceName: cast-to-handle.ges
  line: 3
  column: 16
  endLine: 3
  endColumn: 22
```

---

## Test: a state member of a stateless type

A type without `of` has no state: a member other than `id`, `version` and `root` is `validate.invalidHandleUse`, whose message names the missing `of :State`. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: stateless-member
sources:
  - name: stateless-member.ges
    program: main
handles:
  schemas: ["schema :Source as { text: :Text }\nhandle @Repository with id :Text max 64 versioned by hash(#sha1)\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(repo as @Repository) { emit Out(text: repo.text) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.invalidHandleUse
  symbol: "text"
  sourceName: stateless-member.ges
  line: 2
  column: 47
  endLine: 2
  endColumn: 51
```

---

## Test: reading a stateless type as a schema

`as :State` reads state, so it is `validate.invalidHandleUse` on a stateless type. *Since: Unreleased.*

### Case description

```yaml
gesBlock: case
id: stateless-cast
sources:
  - name: stateless-cast.ges
    program: main
handles:
  schemas: ["schema :Source as { text: :Text }\nhandle @Repository with id :Text max 64 versioned by hash(#sha1)\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(repo as @Repository) { emit Out(text: repo as :Source) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.invalidHandleUse
  symbol: "Source"
  sourceName: stateless-cast.ges
  line: 2
  column: 47
  endLine: 2
  endColumn: 51
```

---

## Test: `@Name~` needs a versioned type

A type without versions cannot require one.

### Case description

```yaml
gesBlock: case
id: versioned-unversioned-type
sources:
  - name: versioned-unversioned-type.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(target as @Target~) { emit Out(target: target) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.invalidHandleUse
  symbol: "@Target~"
  sourceName: versioned-unversioned-type.ges
  line: 2
  column: 9
  endLine: 2
  endColumn: 27
```

---

## Test: `h.version` needs a versioned type

Reading the version of a handle whose type has no versions is `validate.invalidHandleUse`.

### Case description

```yaml
gesBlock: case
id: version-of-unversioned-type
sources:
  - name: version-of-unversioned-type.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(target as @Target) { emit Out(version: target.version) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.invalidHandleUse
  symbol: "@Target"
  sourceName: version-of-unversioned-type.ges
  line: 2
  column: 48
  endLine: 2
  endColumn: 54
```

---

## Test: a union holds only handle types

A union alternative of another type is `validate.invalidHandleUse`.

### Case description

```yaml
gesBlock: case
id: union-with-other-type
sources:
  - name: union-with-other-type.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(target as @Drone | :Number) { emit Out(target: target) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.invalidHandleUse
  symbol: ":Number"
  sourceName: union-with-other-type.ges
  line: 2
  column: 9
  endLine: 2
  endColumn: 35
```

---

## Test: `h.root` of a union needs one root type

The alternatives of a union must be composites of the same root type for `.root`.

### Case description

```yaml
gesBlock: case
id: root-without-common-root
sources:
  - name: root-without-common-root.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(item as @RepoFile | @Drone) { emit Out(root: item.root) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.invalidHandleUse
  symbol: "@RepoFile|@Drone"
  sourceName: root-without-common-root.ges
  line: 2
  column: 54
  endLine: 2
  endColumn: 58
```

---

## Test: a handle-typed name is a valid timer name

A statically known handle names a timer, while another static type stays `validate.invalidTypeConstructor`.

### Case description

```yaml
gesBlock: case
id: timer-name-handle-typed
sources:
  - name: timer-name-handle-typed.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(drone as @Drone) {
  emit after 1s named drone Forget(drone: drone)
  emit after 1s named 5 Forget(drone: drone)
}
on Forget(drone) { emit Gone() }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.invalidTypeConstructor
  symbol: "Text"
  sourceName: timer-name-handle-typed.ges
  line: 4
  column: 23
  endLine: 4
  endColumn: 24
```

---

## Test: a state read needs a resolver

A Program that reads the state of a handle type fails loading without a resolver for it, located at the first read.

### Case description

```yaml
gesBlock: case
id: missing-resolver
kind: loadError
sources:
  - name: missing-resolver.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(target as @Target) {
  emit Out(id: target.id)
  emit Out(label: target.label, strength: target.strength)
}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: link
    code: link.missingHandleResolver
    symbol: "Target"
    sourceName: missing-resolver.ges
    line: 4
    column: 19
    endLine: 4
    endColumn: 25
```

---

## Test: `as :State` is a state read

A cast to the state schema reads the state too.

### Case description

```yaml
gesBlock: case
id: missing-resolver-state-cast
kind: loadError
sources:
  - name: missing-resolver-state-cast.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(target as @Target) { emit Out(state: target as :Contact) }
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: link
    code: link.missingHandleResolver
    symbol: "Target"
    sourceName: missing-resolver-state-cast.ges
    line: 2
    column: 46
    endLine: 2
    endColumn: 52
```

---

## Test: identity members need no resolver

A Program that only reads `id`, `version` and `root` loads without resolvers; loading fails only on the other type that reads state.

### Case description

```yaml
gesBlock: case
id: identity-needs-no-resolver
kind: loadError
sources:
  - name: identity-needs-no-resolver.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: [RepoFile]
```

### Source code under test

```ges
module main
on Seen(file as @RepoFile, drone as @Drone) {
  emit Out(id: file.id, version: file.version, root: file.root)
  emit Out(altitude: drone.altitude)
}
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: link
    code: link.missingHandleResolver
    symbol: "Drone"
    sourceName: identity-needs-no-resolver.ges
    line: 4
    column: 22
    endLine: 4
    endColumn: 27
```

---

## Test: the host's handle type must match the compiled one

A host whose handle type of the same name has another handle hash, here another id range, fails loading with `link.mismatchedHandleType` even when the Program reads no state.

### Case description

```yaml
gesBlock: case
id: mismatched-handle-type
kind: loadError
sources:
  - name: mismatched-handle-type.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  hostSchemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 9999\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
```

### Source code under test

```ges
module main
on Seen(target as @Target) { emit Out(id: target.id) }
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: link
    code: link.mismatchedHandleType
    symbol: "Target"
    sourceName: null
    line: null
    column: null
    endLine: null
    endColumn: null
```

---

## Test: the host's handle type must have the compiled state schema

> **Since: Unreleased**

The link hash covers the state schema: a host whose `:Contact` has another field range fails loading `@Target` with `link.mismatchedHandleType`, although id and version are the same and the Program reads no state.

### Case description

```yaml
gesBlock: case
id: mismatched-handle-state
kind: loadError
sources:
  - name: mismatched-handle-state.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  hostSchemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 99 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
```

### Source code under test

```ges
module main
on Seen(target as @Target) { emit Out(id: target.id) }
```

### Expectation

```yaml
gesBlock: expect
errors:
  - phase: link
    code: link.mismatchedHandleType
    symbol: "Target"
    sourceName: null
    line: null
    column: null
    endLine: null
    endColumn: null
```

---

## Test: a projection must narrow the type's state

`@Name(:P)` with a `:P` that is no projection of the type's state schema is `validate.invalidProjection`.

### Case description

```yaml
gesBlock: case
id: foreign-projection
sources:
  - name: foreign-projection.ges
    program: main
handles:
  schemas: ["schema :Composition as { name: :Text max 32, icon: :Text max 16, mass: :Number integer between 0 and 1000, label: :Text max 8 }\nprojection :UnitDisplay of :Composition as { name, icon, label }\nprojection :UnitLabel of :UnitDisplay as { label }\nschema :Contact as { label: :Text max 16 }\nprojection :ContactLabel of :Contact as { label }\nhandle @Unit of :Composition with id :Number integer between 0 and 255\nhandle @Target of :Contact with id :Number integer between 0 and 255\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(unit as @Unit(:ContactLabel)) { emit Out(unit: unit) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.invalidProjection
  symbol: "@Unit(:ContactLabel)"
  sourceName: foreign-projection.ges
  line: 2
  column: 9
  endLine: 2
  endColumn: 37
```

---

## Test: a union holds one view per handle type

A union that names one handle type with two projections, or with and without one, is `validate.invalidProjection`.

### Case description

```yaml
gesBlock: case
id: union-two-views
sources:
  - name: union-two-views.ges
    program: main
handles:
  schemas: ["schema :Composition as { name: :Text max 32, icon: :Text max 16, mass: :Number integer between 0 and 1000, label: :Text max 8 }\nprojection :UnitDisplay of :Composition as { name, icon, label }\nprojection :UnitLabel of :UnitDisplay as { label }\nschema :Contact as { label: :Text max 16 }\nprojection :ContactLabel of :Contact as { label }\nhandle @Unit of :Composition with id :Number integer between 0 and 255\nhandle @Target of :Contact with id :Number integer between 0 and 255\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(unit as @Unit(:UnitDisplay)|@Unit(:UnitLabel)) { emit Out(unit: unit) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.invalidProjection
  symbol: "@Unit(:UnitLabel)"
  sourceName: union-two-views.ges
  line: 2
  column: 9
  endLine: 2
  endColumn: 54
```

---

## Test: a projected handle reads only its view's fields

A member of a projected handle that is no field of its projection is `validate.invalidProjection`; the identity members stay.

### Case description

```yaml
gesBlock: case
id: member-outside-view
sources:
  - name: member-outside-view.ges
    program: main
handles:
  schemas: ["schema :Composition as { name: :Text max 32, icon: :Text max 16, mass: :Number integer between 0 and 1000, label: :Text max 8 }\nprojection :UnitDisplay of :Composition as { name, icon, label }\nprojection :UnitLabel of :UnitDisplay as { label }\nschema :Contact as { label: :Text max 16 }\nprojection :ContactLabel of :Contact as { label }\nhandle @Unit of :Composition with id :Number integer between 0 and 255\nhandle @Target of :Contact with id :Number integer between 0 and 255\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(unit as @Unit(:UnitDisplay)) { emit Out(id: unit.id, mass: unit.mass) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.invalidProjection
  symbol: "mass"
  sourceName: member-outside-view.ges
  line: 2
  column: 68
  endLine: 2
  endColumn: 72
```

---

## Test: `as :S` reads no more than the view

`as` the state schema or a projection that does not narrow the handle's view is `validate.handleWidening`.

### Case description

```yaml
gesBlock: case
id: widening-cast
sources:
  - name: widening-cast.ges
    program: main
handles:
  schemas: ["schema :Composition as { name: :Text max 32, icon: :Text max 16, mass: :Number integer between 0 and 1000, label: :Text max 8 }\nprojection :UnitDisplay of :Composition as { name, icon, label }\nprojection :UnitLabel of :UnitDisplay as { label }\nschema :Contact as { label: :Text max 16 }\nprojection :ContactLabel of :Contact as { label }\nhandle @Unit of :Composition with id :Number integer between 0 and 255\nhandle @Target of :Contact with id :Number integer between 0 and 255\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Seen(unit as @Unit(:UnitLabel)) { emit Out(view: unit as :UnitDisplay) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.handleWidening
  symbol: "UnitDisplay"
  sourceName: widening-cast.ges
  line: 2
  column: 53
  endLine: 2
  endColumn: 57
```

---

## Test: a projected handle passes only to a narrower view

Passing a projected handle to a Record field of the full view is `validate.handleWidening`.

### Case description

```yaml
gesBlock: case
id: widening-field
sources:
  - name: widening-field.ges
    program: main
handles:
  schemas: ["schema :Composition as { name: :Text max 32, icon: :Text max 16, mass: :Number integer between 0 and 1000, label: :Text max 8 }\nprojection :UnitDisplay of :Composition as { name, icon, label }\nprojection :UnitLabel of :UnitDisplay as { label }\nschema :Contact as { label: :Text max 16 }\nprojection :ContactLabel of :Contact as { label }\nhandle @Unit of :Composition with id :Number integer between 0 and 255\nhandle @Target of :Contact with id :Number integer between 0 and 255\n"]
  resolvers: []
```

### Source code under test

```ges
module main
record :Report as { unit: @Unit }
on Seen(unit as @Unit(:UnitDisplay)) { emit Out(report: :Report(unit: unit)) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.handleWidening
  symbol: "@Unit"
  sourceName: widening-field.ges
  line: 3
  column: 71
  endLine: 3
  endColumn: 75
```

---

## Test: a handle type alone is no value

A store is read through a selector; `@Target` alone is `validate.invalidHandleUse`.

### Case description

```yaml
gesBlock: case
id: store-bare-type
sources:
  - name: store-bare-type.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Look { emit Out(store: @Target) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.invalidHandleUse
  symbol: "@Target"
  sourceName: store-bare-type.ges
  line: 2
  column: 27
  endLine: 2
  endColumn: 34
```

---

## Test: a store read names the plain type

A store read names its type without a reference or projection: `@Target~[:count]` is `validate.invalidHandleUse`.

### Case description

```yaml
gesBlock: case
id: store-read-projection
sources:
  - name: store-read-projection.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Look { emit Out(count: @Target~[:count]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.invalidHandleUse
  symbol: "@Target~"
  sourceName: store-read-projection.ges
  line: 2
  column: 27
  endLine: 2
  endColumn: 42
```

---

## Test: a store read of an unknown type

A store read of a type outside the compilation's store is `validate.unknownHandleType`.

### Case description

```yaml
gesBlock: case
id: store-read-unknown-type
sources:
  - name: store-read-unknown-type.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Look { emit Out(count: @Ghost[:count]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.unknownHandleType
  symbol: "@Ghost"
  sourceName: store-read-unknown-type.ges
  line: 2
  column: 27
  endLine: 2
  endColumn: 40
```

---

## Test: parts need a composite type

Only a composite handle type has parts: `@Target(root)` is `validate.invalidHandleUse`.

### Case description

```yaml
gesBlock: case
id: store-parts-without-part
sources:
  - name: store-parts-without-part.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: []
```

### Source code under test

```ges
module main
on Look(root) { emit Out(count: @Target(root)[:count]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.invalidHandleUse
  symbol: "@Target"
  sourceName: store-parts-without-part.ges
  line: 2
  column: 33
  endLine: 2
  endColumn: 53
```

---

## Test: a store read of a type that does not enumerate warns

When the host names the types whose resolvers enumerate, a store read of another type compiles with the warning `validate.handleTypeNotEnumerable`; the read yields `nothing` at runtime.

### Case description

```yaml
gesBlock: case
id: store-read-not-enumerable
kind: bytecode
sources:
  - name: store-read-not-enumerable.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: []
  enumerable: ["Drone"]
```

### Source code under test

```ges
module main
on Look { emit Out(count: @Target[:count]) }
```

### Expectation

```yaml
gesBlock: expect
opcodes:
  contains: ["CountStore"]
warnings:
  - phase: validate
    code: validate.handleTypeNotEnumerable
    symbol: "@Target"
    sourceName: store-read-not-enumerable.ges
    line: 2
    column: 27
    endLine: 2
    endColumn: 41
```

---

## Test: a store read of an enumerable type compiles without warning

Store reads lower to one opcode per selector; the parts of one root take the root register.

### Case description

```yaml
gesBlock: case
id: store-read-enumerable
kind: bytecode
sources:
  - name: store-read-enumerable.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: []
  enumerable: ["Target", "RepoFile"]
```

### Source code under test

```ges
module main
on Look(repository as @Repository) { emit Out(count: @Target[:count], keys: @Target[:keys], values: @Target[:values], entries: @Target[:entries], parts: @RepoFile(repository)[:count], files: @RepoFile(repository)[:keys], states: @RepoFile(repository)[:values], pairs: @RepoFile(repository)[:entries]) }
```

### Expectation

```yaml
gesBlock: expect
opcodes:
  contains: ["CountStore", "KeysOfStore", "ValuesOfStore", "EntriesOfStore", "CountParts", "KeysOfParts", "ValuesOfParts", "EntriesOfParts"]
warnings: []
```

---

## Test: a Program imports the handle types it names

Each named handle type is one `HandleType` import with its link hash; the import of a type whose state is read points at the first read. `root` reads `@Root` and checks are by name.

### Case description

```yaml
gesBlock: case
id: import-and-members
kind: bytecodeSnapshot
sources:
  - name: import-and-members.ges
    program: main
handles:
  schemas: ["schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }\nschema :DroneState as { altitude: :Number integer between 0 and 1000, callsign: :Text max 16 }\nschema :Source as { text: :Text }\nhandle @Drone of :DroneState with id :Number integer between 0 and 255 versioned by revision max 1000\nhandle @Target of :Contact with id :Number integer between 0 and 65535\nhandle @Contacted of :Contact with id :Number integer between 0 and 65535 versioned by hash(#ges)\nhandle @Repository of :Source with id :Text max 64 versioned by hash(#sha1)\nhandle @RepoFile of :Contact attached to @Repository with part :Text max 256\n"]
  resolvers: []
```

### Source code under test

```ges
module handles
on Seen(file as @RepoFile, other) {
  emit Out(root: file.root, label: file.label, drone: other is @Drone~)
}
```

### Expected Game Event Script Assembler

```gesa
// -------------------------------------------------------------------------------
//  Module: handles
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "handles"
.program-version 0

// -------------------------------------------------------------------------------
.region "Source: import-and-members.ges"

.segment source "import-and-members.ges"

module handles
on Seen(file as @RepoFile, other) {
  emit Out(root: file.root, label: file.label, drone: other is @Drone~)
}

.region-end "Source: import-and-members.ges"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Text"

.segment text

T_file:				.text "file"
T_other:			.text "other"
T_Seen:				.text "Seen"
T_root:				.text "root"
T_label:			.text "label"
T_drone:			.text "drone"
T_Out:				.text "Out"
T_Drone:			.text "Drone"
T_RepoFile:			.text "RepoFile"
T_Repository:		.text "Repository"
T__RepoFile:		.text "@RepoFile"
T__Repository:		.text "@Repository"
T__Drone_:			.text "@Drone~"
T_handles:			.text "handles"

.region-end "Text"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Lists"

.segment lists

U16_0:				.u16 [0, 1]
U16_1:				.u16 []
U16_2:				.u16 [3, 4, 5]
U16_3:				.u16 [39126, 13630, 54943, 15110]
U16_4:				.u16 [52008, 5956, 49759, 36174]
U16_5:				.u16 [7066, 48939, 6365, 61199]
Args_6:				.registers [r2, r3, r4]

.region-end "Lists"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Bindings"

.segment bind

Handler_Seen:		.bind MessageHandler id=0 name=T_Seen args=[T_file, T_other] entry=Seen // "Seen(file, other)"
Outbound_Out:		.bind OutboundMessage id=0 name=T_Out args=[T_root, T_label, T_drone] // "Out(root, label, drone)"
HandleType_Drone:	.bind HandleType id=none name=T_Drone hash=0x3b06d69f353e98d6 // "@Drone"
HandleType_RepoFile:	.bind HandleType id=none name=T_RepoFile hash=0x8d4ec25f1744cb28 entry=Seen_3 // "@RepoFile"
HandleType_Repository:	.bind HandleType id=none name=T_Repository hash=0xef0f18ddbf2b1b9a // "@Repository"

.region-end "Bindings"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Code"

.segment code

.source-line "import-and-members.ges" 2 | on Seen(file as @RepoFile, other) {
Seen:				 // handler Seen(file, other)
					RegisterLocals #3
					CastCustom r0(file), r0(file), T__RepoFile // "@RepoFile"

.source-line "import-and-members.ges" 3 |   emit Out(root: file.root, label: file.label, drone: other is @Drone~)
					MemberAccess r2, T__Repository, r0(file) // "@Repository"
Seen_3:				MemberAccess r3, T_label, r0(file) // "label"
					CheckCustomType r4, r1(other), T__Drone_ // "@Drone~"
					EmitMessage Outbound_Out, Args_6 // "Out(root, label, drone)"

.source-line "import-and-members.ges" 2 | on Seen(file as @RepoFile, other) {
					ReturnVoid

.region-end "Code"
// -------------------------------------------------------------------------------

```

