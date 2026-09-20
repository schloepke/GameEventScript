---
formatVersion: 1
suiteId: compile.lexical-boundaries
title: "Lexical boundaries and selector scopes"
categories: [conformance]
---

# Lexical boundaries and selector scopes

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite checks quoted syntax tokens, numeric constant signs, and binding visibility across expression scopes.

---

## Test: quoted be

This case verifies that literal text cannot substitute for grammar tokens or introduce an invalid binding.

### Case description

```yaml
gesBlock: case
id: quoted-be
kind: compileError
level: scenario
sources:
  - name: quoted-be.ges
    program: main
```

### Source code under test

```ges
on Start { let x "be" 5; emit Done(value: x) }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: parse, code: parse.syntax }
```

---

## Test: quoted parameter open

This case verifies that literal text cannot substitute for grammar tokens or introduce an invalid binding.

### Case description

```yaml
gesBlock: case
id: quoted-parameter-open
kind: compileError
level: scenario
sources:
  - name: quoted-parameter-open.ges
    program: main
```

### Source code under test

```ges
function f "(" x) be x
on Start { emit Done(value: f(x: 1)) }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: parse, code: parse.syntax }
```

---

## Test: quoted infix

This case verifies that literal text cannot substitute for grammar tokens or introduce an invalid binding.

### Case description

```yaml
gesBlock: case
id: quoted-infix
kind: compileError
level: scenario
sources:
  - name: quoted-infix.ges
    program: main
```

### Source code under test

```ges
on Start { emit Done(value: 1 "+" 2) }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: parse, code: parse.syntax }
```

---

## Test: quoted emit

This case verifies that literal text cannot substitute for grammar tokens or introduce an invalid binding.

### Case description

```yaml
gesBlock: case
id: quoted-emit
kind: compileError
level: scenario
sources:
  - name: quoted-emit.ges
    program: main
```

### Source code under test

```ges
on Start { "emit" Done(value: 1) }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: parse, code: parse.syntax }
```

---

## Test: negative text constant

This case verifies that literal text cannot substitute for grammar tokens or introduce an invalid binding.

### Case description

```yaml
gesBlock: case
id: negative-text-constant
kind: compileError
level: scenario
sources:
  - name: negative-text-constant.ges
    program: main
```

### Source code under test

```ges
constant $value be -"abc"
```

### Expectation

```yaml
gesBlock: expect
error: { phase: parse, code: parse.syntax }
```

---

## Test: negative tag constant

This case verifies that literal text cannot substitute for grammar tokens or introduce an invalid binding.

### Case description

```yaml
gesBlock: case
id: negative-tag-constant
kind: compileError
level: scenario
sources:
  - name: negative-tag-constant.ges
    program: main
```

### Source code under test

```ges
constant $value be -#ready
```

### Expectation

```yaml
gesBlock: expect
error: { phase: parse, code: parse.syntax }
```

---

## Test: negative boolean constant

This case verifies that literal text cannot substitute for grammar tokens or introduce an invalid binding.

### Case description

```yaml
gesBlock: case
id: negative-boolean-constant
kind: compileError
level: scenario
sources:
  - name: negative-boolean-constant.ges
    program: main
```

### Source code under test

```ges
constant $value be -true
```

### Expectation

```yaml
gesBlock: expect
error: { phase: parse, code: parse.syntax }
```

---

## Test: negative nothing constant

This case verifies that literal text cannot substitute for grammar tokens or introduce an invalid binding.

### Case description

```yaml
gesBlock: case
id: negative-nothing-constant
kind: compileError
level: scenario
sources:
  - name: negative-nothing-constant.ges
    program: main
```

### Source code under test

```ges
constant $value be -nothing
```

### Expectation

```yaml
gesBlock: expect
error: { phase: parse, code: parse.syntax }
```

---

## Test: function parameter shadow

This case rejects a binding that shadows a visible ancestor in an expression scope.

### Case description

```yaml
gesBlock: case
id: function-parameter-shadow
kind: compileError
level: scenario
sources:
  - name: function-parameter-shadow.ges
    program: main
```

### Source code under test

```ges
function f(_ x) be [1, 2][:select x => x]
```

### Expectation

```yaml
gesBlock: expect
error: { phase: validate, code: validate.shadowedVariable, symbol: x }
```

---

## Test: generated local shadow

This case rejects a binding that shadows a visible ancestor in an expression scope.

### Case description

```yaml
gesBlock: case
id: generated-local-shadow
kind: compileError
level: scenario
sources:
  - name: generated-local-shadow.ges
    program: main
```

### Source code under test

```ges
on Start { let x be 9; emit Done(value: :List[:select x in [1, 2] => x]) }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: validate, code: validate.shadowedVariable, symbol: x }
```

---

## Test: nested selector shadow

This case rejects a binding that shadows a visible ancestor in an expression scope.

### Case description

```yaml
gesBlock: case
id: nested-selector-shadow
kind: compileError
level: scenario
sources:
  - name: nested-selector-shadow.ges
    program: main
```

### Source code under test

```ges
on Start { emit Done(value: [1, 2][:select x => [3, 4][:select x => x]]) }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: validate, code: validate.shadowedVariable, symbol: x }
```

---

## Test: nested generator shadow

This case rejects a binding that shadows a visible ancestor in an expression scope.

### Case description

```yaml
gesBlock: case
id: nested-generator-shadow
kind: compileError
level: scenario
sources:
  - name: nested-generator-shadow.ges
    program: main
```

### Source code under test

```ges
on Start { emit Done(value: :List[:select x in [1, 2] => :List[:select x in [3, 4] => x]]) }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: validate, code: validate.shadowedVariable, symbol: x }
```

---

## Test: record field shadow

This case rejects a binding that shadows a visible ancestor in an expression scope.

### Case description

```yaml
gesBlock: case
id: record-field-shadow
kind: compileError
level: scenario
sources:
  - name: record-field-shadow.ges
    program: main
```

### Source code under test

```ges
record :Box as { x: :Number, values: :List computed by [1, 2][:select x => x] }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: validate, code: validate.shadowedVariable, symbol: x }
```

---

## Test: message tag shadow

This case rejects a binding that shadows a visible ancestor in an expression scope.

### Case description

```yaml
gesBlock: case
id: message-tag-shadow
kind: compileError
level: scenario
sources:
  - name: message-tag-shadow.ges
    program: main
```

### Source code under test

```ges
on Start { let x be #ready; emit Done with [1, 2][:select x => #ready] }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: validate, code: validate.shadowedVariable, symbol: x }
```

---

## Test: random seed shadow

This case rejects a binding that shadows a visible ancestor in an expression scope.

### Case description

```yaml
gesBlock: case
id: random-seed-shadow
kind: compileError
level: scenario
sources:
  - name: random-seed-shadow.ges
    program: main
```

### Source code under test

```ges
on Start { let x be 9; random with ([1, 2][:sum x => x] as :Number) { emit Done } }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: validate, code: validate.shadowedVariable, symbol: x }
```

---

## Test: nested weight shadow

This case rejects a binding that shadows a visible ancestor in an expression scope.

### Case description

```yaml
gesBlock: case
id: nested-weight-shadow
kind: compileError
level: scenario
sources:
  - name: nested-weight-shadow.ges
    program: main
```

### Source code under test

```ges
on Start { emit Done(value: [1, 2][:choose 1 weighted by x => [3, 4][:sum x => x]]) }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: validate, code: validate.shadowedVariable, symbol: x }
```

---

## Test: sibling selector scopes may reuse names

This case keeps independent sibling selector bindings legal.

### Case description

```yaml
gesBlock: case
id: sibling-selector-scopes
kind: scriptApi
level: scenario
sources:
  - name: sibling-selector-scopes.ges
    program: main
```

### Source code under test

```ges
on Start {
  emit Done(first: [1, 2][:select x => x + 1], second: :List[:select x in [3, 4] => x + 1])
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input: { args: [] }
    local:
      - name: Done
        args:
          - name: first
            value: { type: ":List", items: [{ type: ":Number.int64", value: "2" }, { type: ":Number.int64", value: "3" }] }
          - name: second
            value: { type: ":List", items: [{ type: ":Number.int64", value: "4" }, { type: ":Number.int64", value: "5" }] }
```

---

## Test: negative numeric constants remain valid

This case permits signs on Number, Percentage, and Quantity literals.

### Case description

```yaml
gesBlock: case
id: negative-numeric-constants
kind: scriptApi
level: scenario
sources:
  - name: negative-numeric-constants.ges
    program: main
```

### Source code under test

```ges
constant $integer be -7
constant $fraction be -1.25
constant $percentage be -25%
constant $quantity be -3m
on Start { emit Done(integer: $integer, fraction: $fraction, percentage: $percentage, quantity: $quantity) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input: { args: [] }
    local:
      - name: Done
        args:
          - name: integer
            value: { type: ":Number.int64", value: "-7" }
          - name: fraction
            value: { type: ":Number.binary64", value: "-1.25" }
          - name: percentage
            value: { type: ":Percentage", value: "-0.25" }
          - name: quantity
            value: { type: ":Quantity.int64", value: "-3", unit: ":meter" }
```

---

## Test: keywords and punctuation remain ordinary text values

This case preserves quoted keywords and punctuation as values.

### Case description

```yaml
gesBlock: case
id: keyword-text-values
kind: scriptApi
level: scenario
sources:
  - name: keyword-text-values.ges
    program: main
```

### Source code under test

```ges
on Start { emit Done(value: ["parse", "abs", "+", ";", "}", "be"]) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input: { args: [] }
    local:
      - name: Done
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Text", value: "parse" }
                - { type: ":Text", value: "abs" }
                - { type: ":Text", value: "+" }
                - { type: ":Text", value: ";" }
                - { type: ":Text", value: "}" }
                - { type: ":Text", value: "be" }
```
