---
formatVersion: 1
suiteId: "compile.source-nesting"
title: "Source nesting limits"
categories: [conformance]
---

# Source nesting limits

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite checks portable source nesting before optimization: expression depth 32 and nested statement-body depth 32 are accepted, while the next level yields a structured parse diagnostic. Each source spells out its complete input so every implementation can run these cases without a generator or a new runner operation.

---

## Test: parentheses reaches expression depth 32

This runtime case verifies parentheses reaches expression depth 32.

### Case description

```yaml
gesBlock: case
id: parentheses-boundary
kind: scriptApi
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be (((((((((((((((((((((((((((((((1)))))))))))))))))))))))))))))))
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: input
          value: { type: ":Nothing" }
    local:
      - name: Done
    runtimeLimits:
      exclude: [{ any: true }]
```

---

## Test: parentheses exceeds expression depth 32

This negative compiler case verifies parentheses exceeds expression depth 32.

### Case description

```yaml
gesBlock: case
id: parentheses-above
kind: compileError
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be ((((((((((((((((((((((((((((((((1))))))))))))))))))))))))))))))))
  emit Done()
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: parse
  code: parse.sourceNestingExceeded
  sourceName: nesting.ges
```

---

## Test: unary reaches expression depth 32

This runtime case verifies unary reaches expression depth 32.

### Case description

```yaml
gesBlock: case
id: unary-boundary
kind: scriptApi
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be not not not not not not not not not not not not not not not not not not not not not not not not not not not not not not not true
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: input
          value: { type: ":Nothing" }
    local:
      - name: Done
    runtimeLimits:
      exclude: [{ any: true }]
```

---

## Test: unary exceeds expression depth 32

This negative compiler case verifies unary exceeds expression depth 32.

### Case description

```yaml
gesBlock: case
id: unary-above
kind: compileError
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be not not not not not not not not not not not not not not not not not not not not not not not not not not not not not not not not true
  emit Done()
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: parse
  code: parse.sourceNestingExceeded
  sourceName: nesting.ges
```

---

## Test: power reaches expression depth 32

This runtime case verifies power reaches expression depth 32.

### Case description

```yaml
gesBlock: case
id: power-boundary
kind: scriptApi
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: input
          value: { type: ":Nothing" }
    local:
      - name: Done
    runtimeLimits:
      exclude: [{ any: true }]
```

---

## Test: power exceeds expression depth 32

This negative compiler case verifies power exceeds expression depth 32.

### Case description

```yaml
gesBlock: case
id: power-above
kind: compileError
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1 ^ 1
  emit Done()
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: parse
  code: parse.sourceNestingExceeded
  sourceName: nesting.ges
```

---

## Test: implication reaches expression depth 32

This runtime case verifies implication reaches expression depth 32.

### Case description

```yaml
gesBlock: case
id: implication-boundary
kind: scriptApi
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: input
          value: { type: ":Nothing" }
    local:
      - name: Done
    runtimeLimits:
      exclude: [{ any: true }]
```

---

## Test: implication exceeds expression depth 32

This negative compiler case verifies implication exceeds expression depth 32.

### Case description

```yaml
gesBlock: case
id: implication-above
kind: compileError
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true -> true
  emit Done()
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: parse
  code: parse.sourceNestingExceeded
  sourceName: nesting.ges
```

---

## Test: addition reaches expression depth 32

This runtime case verifies addition reaches expression depth 32.

### Case description

```yaml
gesBlock: case
id: addition-boundary
kind: scriptApi
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: input
          value: { type: ":Nothing" }
    local:
      - name: Done
    runtimeLimits:
      exclude: [{ any: true }]
```

---

## Test: addition exceeds expression depth 32

This negative compiler case verifies addition exceeds expression depth 32.

### Case description

```yaml
gesBlock: case
id: addition-above
kind: compileError
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1
  emit Done()
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: parse
  code: parse.sourceNestingExceeded
  sourceName: nesting.ges
```

---

## Test: member access reaches expression depth 32

This runtime case verifies member access reaches expression depth 32.

### Case description

```yaml
gesBlock: case
id: member-access-boundary
kind: scriptApi
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be input.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: input
          value: { type: ":Nothing" }
    local:
      - name: Done
    runtimeLimits:
      exclude: [{ any: true }]
```

---

## Test: member access exceeds expression depth 32

This negative compiler case verifies member access exceeds expression depth 32.

### Case description

```yaml
gesBlock: case
id: member-access-above
kind: compileError
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be input.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value.value
  emit Done()
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: parse
  code: parse.sourceNestingExceeded
  sourceName: nesting.ges
```

---

## Test: list reaches expression depth 32

This runtime case verifies list reaches expression depth 32.

### Case description

```yaml
gesBlock: case
id: list-boundary
kind: scriptApi
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be [[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[1]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: input
          value: { type: ":Nothing" }
    local:
      - name: Done
    runtimeLimits:
      exclude: [{ any: true }]
```

---

## Test: list exceeds expression depth 32

This negative compiler case verifies list exceeds expression depth 32.

### Case description

```yaml
gesBlock: case
id: list-above
kind: compileError
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be [[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[1]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]
  emit Done()
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: parse
  code: parse.sourceNestingExceeded
  sourceName: nesting.ges
```

---

## Test: map reaches expression depth 32

This runtime case verifies map reaches expression depth 32.

### Case description

```yaml
gesBlock: case
id: map-boundary
kind: scriptApi
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: 1]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: input
          value: { type: ":Nothing" }
    local:
      - name: Done
    runtimeLimits:
      exclude: [{ any: true }]
```

---

## Test: map exceeds expression depth 32

This negative compiler case verifies map exceeds expression depth 32.

### Case description

```yaml
gesBlock: case
id: map-above
kind: compileError
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: 1]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]
  emit Done()
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: parse
  code: parse.sourceNestingExceeded
  sourceName: nesting.ges
```

---

## Test: type cast reaches expression depth 32

This runtime case verifies type cast reaches expression depth 32.

### Case description

```yaml
gesBlock: case
id: type-cast-boundary
kind: scriptApi
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be input as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: input
          value: { type: ":Nothing" }
    local:
      - name: Done
    runtimeLimits:
      exclude: [{ any: true }]
```

---

## Test: type cast exceeds expression depth 32

This negative compiler case verifies type cast exceeds expression depth 32.

### Case description

```yaml
gesBlock: case
id: type-cast-above
kind: compileError
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be input as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number as :Number
  emit Done()
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: parse
  code: parse.sourceNestingExceeded
  sourceName: nesting.ges
```

---

## Test: call reaches expression depth 32

This runtime case verifies call reaches expression depth 32.

### Case description

```yaml
gesBlock: case
id: call-boundary
kind: scriptApi
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
function ident(_ item) be item

on Start(input) {
  let value be ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(1)))))))))))))))))))))))))))))))
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: input
          value: { type: ":Nothing" }
    local:
      - name: Done
    runtimeLimits:
      exclude: [{ any: true }]
```

---

## Test: call exceeds expression depth 32

This negative compiler case verifies call exceeds expression depth 32.

### Case description

```yaml
gesBlock: case
id: call-above
kind: compileError
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
function ident(_ item) be item

on Start(input) {
  let value be ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(ident(1))))))))))))))))))))))))))))))))
  emit Done()
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: parse
  code: parse.sourceNestingExceeded
  sourceName: nesting.ges
```

---

## Test: object pattern reaches expression depth 32

This runtime case verifies object pattern reaches expression depth 32.

### Case description

```yaml
gesBlock: case
id: object-pattern-boundary
kind: scriptApi
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be input[:has [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: 1]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: input
          value: { type: ":Nothing" }
    local:
      - name: Done
    runtimeLimits:
      exclude: [{ any: true }]
```

---

## Test: object pattern exceeds expression depth 32

This negative compiler case verifies object pattern exceeds expression depth 32.

### Case description

```yaml
gesBlock: case
id: object-pattern-above
kind: compileError
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be input[:has [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: [value: 1]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]
  emit Done()
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: parse
  code: parse.sourceNestingExceeded
  sourceName: nesting.ges
```

---

## Test: block bodies reach statement depth 32

This runtime case verifies block bodies reach statement depth 32.

### Case description

```yaml
gesBlock: case
id: block-boundary
kind: scriptApi
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { emit Done()}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: input
          value: { type: ":Nothing" }
    local:
      - name: Done
    runtimeLimits:
      exclude: [{ any: true }]
```

---

## Test: block bodies exceed statement depth 32

This negative compiler case verifies block bodies exceed statement depth 32.

### Case description

```yaml
gesBlock: case
id: block-above
kind: compileError
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { emit Done()}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: parse
  code: parse.sourceNestingExceeded
  sourceName: nesting.ges
```

---

## Test: single-statement bodies reach statement depth 32

This runtime case verifies single-statement bodies reach statement depth 32.

### Case description

```yaml
gesBlock: case
id: single-statement-boundary
kind: scriptApi
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: input
          value: { type: ":Nothing" }
    local:
      - name: Done
    runtimeLimits:
      exclude: [{ any: true }]
```

---

## Test: single-statement bodies exceed statement depth 32

This negative compiler case verifies single-statement bodies exceed statement depth 32.

### Case description

```yaml
gesBlock: case
id: single-statement-above
kind: compileError
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true if true emit Done()
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: parse
  code: parse.sourceNestingExceeded
  sourceName: nesting.ges
```

---

## Test: Both independent nesting boundaries may be reached together

This runtime case verifies both independent nesting boundaries may be reached together.

### Case description

```yaml
gesBlock: case
id: combined-boundaries
kind: scriptApi
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { let value be (((((((((((((((((((((((((((((((1)))))))))))))))))))))))))))))))
  emit Done()}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: input
          value: { type: ":Nothing" }
    local:
      - name: Done
    runtimeLimits:
      exclude: [{ any: true }]
```

---

## Test: Grouping and operator depth reach the shared expression boundary

This runtime case verifies grouping and operator depth reach the shared expression boundary.

### Case description

```yaml
gesBlock: case
id: mixed-expression-boundary
kind: scriptApi
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be ((((((((((((((((1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1))))))))))))))))
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: input
          value: { type: ":Nothing" }
    local:
      - name: Done
    runtimeLimits:
      exclude: [{ any: true }]
```

---

## Test: Grouping and operator depth exceed the shared expression boundary

This negative compiler case verifies grouping and operator depth exceed the shared expression boundary.

### Case description

```yaml
gesBlock: case
id: mixed-expression-above
kind: compileError
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be ((((((((((((((((1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1))))))))))))))))
  emit Done()
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: parse
  code: parse.sourceNestingExceeded
  sourceName: nesting.ges
```

---

## Test: Sibling expressions and bodies reset their nesting depth

This runtime case verifies sibling expressions and bodies reset their nesting depth.

### Case description

```yaml
gesBlock: case
id: sibling-reset
kind: scriptApi
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { let item0 be (((((((((((((((((((((((((((((((1)))))))))))))))))))))))))))))))
  emit Done()}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}
  if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { if true { let item1 be (((((((((((((((((((((((((((((((1)))))))))))))))))))))))))))))))
  emit Done()}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: input
          value: { type: ":Nothing" }
    local:
      - name: Done
      - name: Done
    runtimeLimits:
      exclude: [{ any: true }]
```

---

## Test: Wide collections do not consume nesting per sibling

This runtime case verifies wide collections do not consume nesting per sibling.

### Case description

```yaml
gesBlock: case
id: wide-collection
kind: scriptApi
level: scenario
runtimeLimits:
  maxRegisterValues: 2048
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: input
          value: { type: ":Nothing" }
    local:
      - name: Done
    runtimeLimits:
      exclude: [{ any: true }]
```

---

## Test: Text contents and comments do not add source nesting

This runtime case verifies text contents and comments do not add source nesting.

### Case description

```yaml
gesBlock: case
id: text-and-comments
kind: scriptApi
level: scenario
sources:
  - name: nesting.ges
    program: main
```

### Source code under test

```ges
on Start(input) {
  let value be '(((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((())))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))'
  // ((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: input
          value: { type: ":Nothing" }
    local:
      - name: Done
    runtimeLimits:
      exclude: [{ any: true }]
```
