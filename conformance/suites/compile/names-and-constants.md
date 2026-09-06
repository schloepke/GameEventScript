---
formatVersion: 1
suiteId: compile.names-and-constants
title: "Names, modules, and compile-time constants"
kind: scriptApi
level: atomic
categories: [conformance, compiler, language]
---

# Names, modules, and compile-time constants

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite defines the portable spelling rules for modules and symbols and verifies that scalar constants are resolved entirely during compilation.

---

## Test: scalar constants and numeric names are resolved at compile time

This case verifies that constants may contain digits after their first letter, module paths may contain dotted lowercase components with digits, and variable bindings may use the canonical `_number` suffix.

### Case description

```yaml
gesBlock: case
id: constants-runtime
```

### Source code under test

```ges
module gameplay2.rules3

constant $base2 be 10
constant $label2 be 'ready'
constant $active2 be true
constant $tag2 be #team2
constant $distance2 be 3m
constant $negative2 be -7
constant $none2 be nothing

record :Box2 as {
  value2: :Number
}

function add2(value2, increment2) be value2 + increment2

on Start2 {
  let value_0 be $base2
  let value_1 be add2(value2: value_0, increment2: 5)
  let box2 be :Box2(value2: value_1)
  let seeded2 be random with $base2 (random from 1 to 1)
  emit Done2(value: value_1, label: $label2, active: $active2, tag: $tag2, distance: $distance2, negative: $negative2, none: $none2, seeded: seeded2, box: box2)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| execute | Start2 | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  execute:
    input:
      args: []
    local:
      - name: Done2
        args:
          - name: value
            value: { type: ":Number.int64", value: "15" }
          - name: label
            value: { type: ":Text", value: "ready" }
          - name: active
            value: { type: ":Boolean", value: true }
          - name: tag
            value: { type: ":Tag", value: "team2" }
          - name: distance
            value: { type: ":Quantity.int64", value: "3", unit: ":meter" }
          - name: negative
            value: { type: ":Number.int64", value: "-7" }
          - name: none
            value: { type: ":Nothing" }
          - name: seeded
            value: { type: ":Number.int64", value: "1" }
          - name: box
            value:
              type: ":Box2"
              entries:
                - key: value2
                  value: { type: ":Number.int64", value: "15" }
```

---

## Test: constants are shared by sources in one program

This case verifies that all sources assigned to one program share the same constant namespace when their explicit module identifiers agree.

### Case description

```yaml
gesBlock: case
id: constants-multiple-sources
sources:
  - name: definitions.ges
    program: main
  - name: handlers.ges
    program: main
```

### Source code under test

```ges
module gameplay.shared2
constant $scoreLimit2 be 21
```

```ges
module gameplay.shared2
on Start { emit Done(value: $scoreLimit2) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| execute | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  execute:
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ":Number.int64", value: "21" }
```

---

## Test: duplicate constants are rejected across sources

This case verifies that the program-wide constant namespace rejects a second definition with the same name even when it occurs in another source file.

### Case description

```yaml
gesBlock: case
id: duplicate-constant
kind: compileError
sources:
  - name: first.ges
    program: main
  - name: second.ges
    program: main
```

### Source code under test

```ges
module constants
constant $limit be 10
```

```ges
module constants
constant $limit be 20
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.duplicateConstant
  symbol: limit
```

---

## Test: constant initializers must be scalar literals

This case verifies that even a foldable expression is not a constant initializer in V1; derived compile-time behavior belongs in a function until a broader constant-expression contract is specified.

### Case description

```yaml
gesBlock: case
id: nonliteral-constant
kind: compileError
```

### Source code under test

```ges
constant $derived be 10 + 5
on Start { }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: parse
  code: parse.syntax
```

---

## Test: unresolved constants fail compilation

This case verifies that a constant reference is distinct from a local identifier and must resolve against the program-wide constant namespace.

### Case description

```yaml
gesBlock: case
id: unresolved-constant
kind: compileError
```

### Source code under test

```ges
on Start { emit Done(value: $missing) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: compile
  code: compile.unresolvedSymbol
  symbol: missing
```

---

## Test: mismatched explicit modules cannot form one program

This case verifies that every nonempty module declaration in a source group names the same lowercase dotted module.

### Case description

```yaml
gesBlock: case
id: mismatched-modules
kind: compileError
sources:
  - name: first.ges
    program: main
  - name: second.ges
    program: main
```

### Source code under test

```ges
module gameplay.first
constant $value be 1
```

```ges
module gameplay.second
on Start { emit Done(value: $value) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.invalidIdentifierCase
  symbol: gameplay.second
```

---

## Test: constant names reject variable subscript suffixes

This case verifies that `_number` is reserved for variable bindings and cannot be used in a program-wide constant name.

### Case description

```yaml
gesBlock: case
id: constant-rejects-variable-subscript
kind: compileError
```

### Source code under test

```ges
constant $value_1 be 10
on Start { emit Done }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: parse
  code: parse.syntax
```

---

## Test: module names reject underscores

This case verifies that a module component contains only lowercase ASCII letters and digits after its first letter.

### Case description

```yaml
gesBlock: case
id: module-rejects-underscore
kind: compileError
```

### Source code under test

```ges
module gameplay_rules
on Start { emit Done }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: parse
  code: parse.syntax
```

---

## Test: module names reject hyphens

This case verifies that hyphens are never module-component separators; only dots separate lowercase components.

### Case description

```yaml
gesBlock: case
id: module-rejects-hyphen
kind: compileError
```

### Source code under test

```ges
module gameplay-rules
on Start { emit Done }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: parse
  code: parse.syntax
```
