---
formatVersion: 1
suiteId: compile.compiler-parity
title: "Compiler decisions both ports share"
categories: [conformance]
---

# Compiler decisions both ports share

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite pins compiler decisions where the C# and Swift compilers once differed: folding with `nothing`, decimal random bounds, static types of constants and bindings, parameter type hints, and diagnostics of lowering and inline assembly.

---

## Test: record-parameter-type

`:Record` names no conversion target, so a parameter hint with it is rejected.

### Case description

```yaml
gesBlock: case
id: record-parameter-type
kind: compileError
level: scenario
sources:
  - name: record-parameter-type.ges
    program: main
```

### Source code under test

```ges
on Start(x as :Record) {
  emit Ping()
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  line: 1
  column: 10
```

---

## Test: unknown-quantity-parameter-type

A quantity parameter hint needs a known unit.

### Case description

```yaml
gesBlock: case
id: unknown-quantity-parameter-type
kind: compileError
level: scenario
sources:
  - name: unknown-quantity-parameter-type.ges
    program: main
```

### Source code under test

```ges
function f(_ x as :Quantity(zz)) be x
on Start {
  emit Ping()
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  line: 1
  column: 12
```

---

## Test: constant-delay-without-unit

A constant is replaced by its literal, so a unitless constant is a statically wrong delay.

### Case description

```yaml
gesBlock: case
id: constant-delay-without-unit
kind: compileError
level: scenario
sources:
  - name: constant-delay-without-unit.ges
    program: main
```

### Source code under test

```ges
constant $delay be 5
on Start {
  emit after $delay Ping()
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  line: 3
  column: 14
```

---

## Test: binding-delay-of-text

A binding has the static type of its value, so a Text binding is a statically wrong delay.

### Case description

```yaml
gesBlock: case
id: binding-delay-of-text
kind: compileError
level: scenario
sources:
  - name: binding-delay-of-text.ges
    program: main
```

### Source code under test

```ges
on Start {
  let t be 'abc'
  emit after t Ping()
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  line: 3
  column: 14
```

---

## Test: labeled-call-to-unknown-name

A labeled call to an unknown name is unresolved when lowering, in both ports.

### Case description

```yaml
gesBlock: case
id: labeled-call-to-unknown-name
kind: compileError
level: scenario
sources:
  - name: labeled-call-to-unknown-name.ges
    program: main
```

### Source code under test

```ges
on Start(a) {
  let r be leaf(value: a)
  emit Done(value: r)
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "compile"
  code: "compile.unresolvedSymbol"
  line: 2
  column: 12
```

---

## Test: assembly-symbolic-constant

A non-scalar symbolic assembly argument fails at its instruction.

### Case description

```yaml
gesBlock: case
id: assembly-symbolic-constant
kind: compileError
level: scenario
sources:
  - name: assembly-symbolic-constant.ges
    program: main
```

### Source code under test

```ges
constant $c be 1
on Start {
  let result be asm { EmitInstant result, Done(value: $c) }
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "compile"
  code: "compile.invalidAssembly"
  line: 3
  column: 23
```

---

## Test: nothing-equality-and-dice-faces

Equality with `nothing` folds to `nothing`, also inside a dice pattern face, and a percentage divided by a Boolean stays a percentage, as the VM computes them.

### Case description

```yaml
gesBlock: case
id: nothing-equality-and-dice-faces
kind: scriptApi
level: scenario
sources:
  - name: nothing-equality-and-dice-faces.ges
    program: main
```

### Source code under test

```ges
on Start {
  let d be [nothing, nothing, true, true, 3]
  emit Done(pair: d[:take pair of (nothing = nothing)], equal: nothing = nothing, ratio: (50%) / (true), folded: d[:has pair of (1.5 + 1.5)])
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
          - name: pair
            value: { type: ":List", items: [{ type: ":Nothing" }, { type: ":Nothing" }] }
          - name: equal
            value: { type: ":Nothing" }
          - name: ratio
            value: { type: ":Percentage", value: "0.5" }
          - name: folded
            value: { type: ":Boolean", value: "false" }
```

---

## Test: decimal-random-bounds

A bound written as a decimal literal draws a binary64 value, also when negated, through a constant, or as a math constant.

### Case description

```yaml
gesBlock: case
id: decimal-random-bounds
kind: scriptApi
level: scenario
sources:
  - name: decimal-random-bounds.ges
    program: main
```

### Source code under test

```ges
constant $neg be -2.0
on Start {
  let a be random with 1 random from -20.0 to -10.0
  let b be random with 1 random from $neg to -1.0
  let p be random with 1 random from 1 to pi
  emit Done(a: a, b: b, p: p)
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
          - name: a
            value: { type: ":Number.binary64", value: "-12.970781668411496" }
          - name: b
            value: { type: ":Number.binary64", value: "-1.2970781668411495" }
          - name: p
            value: { type: ":Number.binary64", value: "2.5053722339408644" }
```

---

## Test: constant-types-in-callables

A constant has the type of its literal also in function and predicate bodies.

### Case description

```yaml
gesBlock: case
id: constant-types-in-callables
kind: scriptApi
level: scenario
sources:
  - name: constant-types-in-callables.ges
    program: main
```

### Source code under test

```ges
constant $flag be true
constant $seed be 5
predicate active() be $flag
function draw() be random with $seed (random from 1 to 6)
on Start {
  emit Done(active: active(), rolled: draw())
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
          - name: active
            value: { type: ":Boolean", value: "true" }
          - name: rolled
            value: { type: ":Number.int64", value: "6" }
```

---

## Test: emit-of-a-send-expression

`emit emit Ping()` sends Ping and emits the Boolean result, which is no message.

### Case description

```yaml
gesBlock: case
id: emit-of-a-send-expression
kind: scriptApi
level: scenario
sources:
  - name: emit-of-a-send-expression.ges
    program: main
```

### Source code under test

```ges
on Start {
  emit emit Done(value: 1)
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
          - name: value
            value: { type: ":Number.int64", value: "1" }
```

---

## Test: fold-seed-from-two-registers

A fold seed computed from two values reads before it moves into the accumulator; both compilers produce the same Program.

### Case description

```yaml
gesBlock: case
id: fold-seed-from-two-registers
kind: scriptApi
level: scenario
sources:
  - name: fold-seed-from-two-registers.ges
    program: main
```

### Source code under test

```ges
on Start {
  let x be [1, 2, 3]
  let y be 2
  emit Done(value: x[:fold acc be y * y, i => acc + i])
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
          - name: value
            value: { type: ":Number.int64", value: "10" }
```

---

## Test: unit-index-reads-a-property

A unit literal index reads like any other non-immediate index.

### Case description

```yaml
gesBlock: case
id: unit-index-reads-a-property
kind: scriptApi
level: scenario
sources:
  - name: unit-index-reads-a-property.ges
    program: main
```

### Source code under test

```ges
on Start {
  let values be [1, 2, 3, 4]
  emit Done(value: values[3m])
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
          - name: value
            value: { type: ":Number.int64", value: "3" }
```
