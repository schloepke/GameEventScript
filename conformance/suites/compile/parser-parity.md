---
formatVersion: 1
suiteId: compile.parser-parity
title: "Parser decisions both ports share"
categories: [conformance]
---

# Parser decisions both ports share

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite pins grammar decisions where the C# and Swift parsers once differed: operator levels after type operations, power suffixes, number suffix boundaries, line breaks inside brackets, mixed call labels and the nesting depth of arguments and items.

---

## Test: cast-then-additive

`as` binds looser than additive operators, so an operator after a completed cast is an error.

### Case description

```yaml
gesBlock: case
id: cast-then-additive
kind: compileError
level: scenario
sources:
  - name: cast-then-additive.ges
    program: main
```

### Source code under test

```ges
on Start(a) {
  let x be a as :Number + 1
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
  line: 2
  column: 25
```

---

## Test: type-test-then-relational

After `is`, only type operations and looser operators follow.

### Case description

```yaml
gesBlock: case
id: type-test-then-relational
kind: compileError
level: scenario
sources:
  - name: type-test-then-relational.ges
    program: main
```

### Source code under test

```ges
on Start(a, c) {
  let x be a is numeric < c
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
  line: 2
  column: 25
```

---

## Test: has-value-then-additive

After `has value`, only membership and looser operators follow.

### Case description

```yaml
gesBlock: case
id: has-value-then-additive
kind: compileError
level: scenario
sources:
  - name: has-value-then-additive.ges
    program: main
```

### Source code under test

```ges
on Start(a) {
  let x be a has value + 1
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
  line: 2
  column: 24
```

---

## Test: repeated-superscript

A power takes one superscript suffix.

### Case description

```yaml
gesBlock: case
id: repeated-superscript
kind: compileError
level: scenario
sources:
  - name: repeated-superscript.ges
    program: main
```

### Source code under test

```ges
on Start(a) {
  let x be a²²
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
  line: 2
  column: 14
```

---

## Test: superscript-then-power

A superscript power takes no further `^`.

### Case description

```yaml
gesBlock: case
id: superscript-then-power
kind: compileError
level: scenario
sources:
  - name: superscript-then-power.ges
    program: main
```

### Source code under test

```ges
on Start(a, c) {
  let x be a² ^ c
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
  line: 2
  column: 15
```

---

## Test: numeric-type-name

`:Numeric` is no type; `numeric` is the keyword of the type test.

### Case description

```yaml
gesBlock: case
id: numeric-type-name
kind: compileError
level: scenario
sources:
  - name: numeric-type-name.ges
    program: main
```

### Source code under test

```ges
on Start(a) {
  let x be a as :Numeric
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
  line: 2
  column: 17
```

---

## Test: percentage-followed-by-word

A word directly after `%` makes the whole word illegal.

### Case description

```yaml
gesBlock: case
id: percentage-followed-by-word
kind: compileError
level: scenario
sources:
  - name: percentage-followed-by-word.ges
    program: main
```

### Source code under test

```ges
on Start {
  let abc be 2
  let x be 5%abc
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
  line: 3
  column: 12
```

---

## Test: degree-followed-by-word

A word directly after `°` makes the whole word illegal.

### Case description

```yaml
gesBlock: case
id: degree-followed-by-word
kind: compileError
level: scenario
sources:
  - name: degree-followed-by-word.ges
    program: main
```

### Source code under test

```ges
on Start {
  let x be 90°x
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
  line: 2
  column: 12
```

---

## Test: roll-without-dice

`roll` without `dice` is itself the unexpected token.

### Case description

```yaml
gesBlock: case
id: roll-without-dice
kind: compileError
level: scenario
sources:
  - name: roll-without-dice.ges
    program: main
```

### Source code under test

```ges
on Start {
  let x be roll
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
  line: 2
  column: 12
```

---

## Test: dice-count-on-next-line

The dice count follows `roll dice` on the same line outside brackets.

### Case description

```yaml
gesBlock: case
id: dice-count-on-next-line
kind: compileError
level: scenario
sources:
  - name: dice-count-on-next-line.ges
    program: main
```

### Source code under test

```ges
on Start {
  let x be roll dice
3d6
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
  line: 2
  column: 21
```

---

## Test: call-argument-nesting

A call is one level deeper than its deepest argument, so 32 chained terms in an argument exceed the limit.

### Case description

```yaml
gesBlock: case
id: call-argument-nesting
kind: compileError
level: scenario
sources:
  - name: call-argument-nesting.ges
    program: main
```

### Source code under test

```ges
on Start(a) {
  let x be f(a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a)
}
function f(_ v) be v
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.sourceNestingExceeded"
  line: 2
  column: 12
```

---

## Test: list-item-nesting

A list is one level deeper than its deepest item, so 32 chained terms in an item exceed the limit.

### Case description

```yaml
gesBlock: case
id: list-item-nesting
kind: compileError
level: scenario
sources:
  - name: list-item-nesting.ges
    program: main
```

### Source code under test

```ges
on Start(a) {
  let x be [a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a]
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.sourceNestingExceeded"
  line: 2
  column: 12
```

---

## Test: positional-then-labeled-arguments

A call may pass its unlabeled first argument positionally and label the following ones, as `scale(_ value, factor)` declares.

### Case description

```yaml
gesBlock: case
id: positional-then-labeled-arguments
kind: scriptApi
level: scenario
sources:
  - name: positional-then-labeled-arguments.ges
    program: main
```

### Source code under test

```ges
function scale(_ value, factor) be value * factor
on Start {
  emit Done(value: scale(2, factor: 3))
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
            value: { type: ":Number.int64", value: "6" }
```

---

## Test: line-breaks-inside-brackets

Inside parentheses and brackets, line breaks separate nothing: parameter lists, call arguments and selectors may span lines.

### Case description

```yaml
gesBlock: case
id: line-breaks-inside-brackets
kind: scriptApi
level: scenario
sources:
  - name: line-breaks-inside-brackets.ges
    program: main
```

### Source code under test

```ges
function scale(
  _ value,
  factor
) be value * factor
on Start {
  let values be [3, 1, 2]
  emit Done(highest: values[:take highest
    1][1], value: scale(2,
    factor: 3))
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
          - name: highest
            value: { type: ":Number.int64", value: "3" }
          - name: value
            value: { type: ":Number.int64", value: "6" }
```

---

## Test: type-operation-then-looser-operators

A completed cast or type test may be followed by further type operations and by looser operators.

### Case description

```yaml
gesBlock: case
id: type-operation-then-looser-operators
kind: scriptApi
level: scenario
sources:
  - name: type-operation-then-looser-operators.ges
    program: main
```

### Source code under test

```ges
on Start {
  let a be 3
  emit Done(value: (a as :Number is numeric and true) as :Number)
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

## Test: nesting-at-the-limit

A call with 31 chained terms in its argument has depth 32 and is allowed.

### Case description

```yaml
gesBlock: case
id: nesting-at-the-limit
kind: scriptApi
level: scenario
sources:
  - name: nesting-at-the-limit.ges
    program: main
```

### Source code under test

```ges
function f(_ v) be v
on Start {
  let a be 1
  let x be f(a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a)
  emit Done(value: x)
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
            value: { type: ":Number.int64", value: "31" }
```

---

## Test: unlabeled-record-field-before-line-break

A line break may follow the `_` of an unlabeled Record field; the field keeps its positional constructor parameter.

### Case description

```yaml
gesBlock: case
id: unlabeled-record-field-before-line-break
kind: scriptApi
level: scenario
sources:
  - name: unlabeled-record-field-before-line-break.ges
    program: main
```

### Source code under test

```ges
record :Hit as { _
  amount: :Number }
on Start {
  let hit be :Hit(7)
  emit Done(value: hit.amount)
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
            value: { type: ":Number.int64", value: "7" }
```

---

## Test: random-before-line-break

A line break may follow `random`; the expression and its source range start at `random`, which the cross-port compiler comparison checks in the SourceMap.

### Case description

```yaml
gesBlock: case
id: random-before-line-break
kind: scriptApi
level: scenario
sources:
  - name: random-before-line-break.ges
    program: main
```

### Source code under test

```ges
on Start {
  let value be random
    from 3 to 3
  emit Done(value: value)
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
