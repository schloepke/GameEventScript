---
formatVersion: 1
suiteId: "runtime.atomic.control-flow"
title: "RuntimeAtomicControlFlow"
categories: [conformance]
tags: [migrated-json-v1]
---

# RuntimeAtomicControlFlow

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite isolates branches and loops and verifies their deterministic execution behavior.

---

## Test: if branches use true only conditions

This runtime case exercises “if branches use true only conditions” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0001
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "if branches use true only conditions.ges"
    program: main
```

### Source code under test

```ges
module AtomicControlIfBranches
on Start {
  if true {
    emit Branch(name: 'true-then')
  } else {
    emit Branch(name: 'true-else')
  }

  if false {
    emit Branch(name: 'false-then')
  } else {
    emit Branch(name: 'false-else')
  }

  if nothing {
    emit Branch(name: 'missing-then')
  } else {
    emit Branch(name: 'missing-else')
  }

  emit Done
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Branch"
        args:
          - name: "name"
            value:
              type: ":text"
              value: "true-then"
      - name: "Branch"
        args:
          - name: "name"
            value:
              type: ":text"
              value: "false-else"
      - name: "Branch"
        args:
          - name: "name"
            value:
              type: ":text"
              value: "missing-else"
      - name: "Done"
        args: []
```

---

## Test: or short circuits only on true

This runtime case exercises “or short circuits only on true” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0002
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or short circuits only on true.ges"
    program: main
```

### Source code under test

```ges
module AtomicControlOrShortCircuit
on Start {
  let trueSkips be true or :test.fail()
  let falseEvaluates be false or :test.truth()
  let missingResolves be nothing or true
  let missingStaysUnknown be nothing or false
  emit Done(trueSkips: trueSkips, falseEvaluates: falseEvaluates, missingResolves: missingResolves, missingStaysUnknown: missingStaysUnknown)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "trueSkips"
            value:
              type: ":boolean"
              value: true
          - name: "falseEvaluates"
            value:
              type: ":boolean"
              value: true
          - name: "missingResolves"
            value:
              type: ":boolean"
              value: true
          - name: "missingStaysUnknown"
            value:
              type: ":nothing"
```

---

## Test: and short circuits only on false

This runtime case exercises “and short circuits only on false” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0003
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and short circuits only on false.ges"
    program: main
```

### Source code under test

```ges
module AtomicControlAndShortCircuit
on Start {
  let falseSkips be false and :test.fail()
  let trueEvaluates be true and :test.truth()
  let missingResolvesFalse be nothing and false
  let missingStaysUnknown be nothing and true
  emit Done(falseSkips: falseSkips, trueEvaluates: trueEvaluates, missingResolvesFalse: missingResolvesFalse, missingStaysUnknown: missingStaysUnknown)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "falseSkips"
            value:
              type: ":boolean"
              value: false
          - name: "trueEvaluates"
            value:
              type: ":boolean"
              value: true
          - name: "missingResolvesFalse"
            value:
              type: ":boolean"
              value: false
          - name: "missingStaysUnknown"
            value:
              type: ":nothing"
```

---

## Test: implication short circuits false antecedent

This runtime case exercises “implication short circuits false antecedent” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0004
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implication short circuits false antecedent.ges"
    program: main
```

### Source code under test

```ges
module AtomicControlImplication
on Start {
  let falseSkips be false -> :test.fail()
  let trueEvaluates be true -> true
  let trueFails be true -> false
  let missingResolvesTrue be nothing -> true
  let missingStaysUnknown be nothing -> false
  emit Done(falseSkips: falseSkips, trueEvaluates: trueEvaluates, trueFails: trueFails, missingResolvesTrue: missingResolvesTrue, missingStaysUnknown: missingStaysUnknown)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "falseSkips"
            value:
              type: ":boolean"
              value: true
          - name: "trueEvaluates"
            value:
              type: ":boolean"
              value: true
          - name: "trueFails"
            value:
              type: ":boolean"
              value: false
          - name: "missingResolvesTrue"
            value:
              type: ":boolean"
              value: true
          - name: "missingStaysUnknown"
            value:
              type: ":nothing"
```

---

## Test: guarded choice selects first true branch

This runtime case exercises “guarded choice selects first true branch” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0005
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "guarded choice selects first true branch.ges"
    program: main
```

### Source code under test

```ges
module AtomicControlGuardedChoice
on Start {
  let firstTrue as :number be 10 when false, or 20 when true, or 30 when true, otherwise 40
  let otherwiseValue as :number be 10 when false, or 20 when nothing, otherwise 40
  emit Done(firstTrue: firstTrue, otherwiseValue: otherwiseValue)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "firstTrue"
            value:
              type: ":integer"
              value: "20"
          - name: "otherwiseValue"
            value:
              type: ":integer"
              value: "40"
```

---

## Test: for loops iterate lists ranges and text

This runtime case exercises “for loops iterate lists ranges and text” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0006
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "for loops iterate lists ranges and text.ges"
    program: main
```

### Source code under test

```ges
module AtomicControlForIteration
on Start {
  for item in [1, 2] {
    emit Item(kind: 'list', value: item)
  }

  for item from 1 to 3 step 2 {
    emit Item(kind: 'range', value: item)
  }

  for item in 'ab' {
    emit Item(kind: 'text', value: item)
  }

  emit Done
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Item"
        args:
          - name: "kind"
            value:
              type: ":text"
              value: "list"
          - name: "value"
            value:
              type: ":integer"
              value: "1"
      - name: "Item"
        args:
          - name: "kind"
            value:
              type: ":text"
              value: "list"
          - name: "value"
            value:
              type: ":integer"
              value: "2"
      - name: "Item"
        args:
          - name: "kind"
            value:
              type: ":text"
              value: "range"
          - name: "value"
            value:
              type: ":integer"
              value: "1"
      - name: "Item"
        args:
          - name: "kind"
            value:
              type: ":text"
              value: "range"
          - name: "value"
            value:
              type: ":integer"
              value: "3"
      - name: "Item"
        args:
          - name: "kind"
            value:
              type: ":text"
              value: "text"
          - name: "value"
            value:
              type: ":text"
              value: "a"
      - name: "Item"
        args:
          - name: "kind"
            value:
              type: ":text"
              value: "text"
          - name: "value"
            value:
              type: ":text"
              value: "b"
      - name: "Done"
        args: []
```

---

## Test: for loops skip empty and non iterable sources

This runtime case exercises “for loops skip empty and non iterable sources” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0007
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "for loops skip empty and non iterable sources.ges"
    program: main
```

### Source code under test

```ges
module AtomicControlForEmptySources
on Start {
  for item in [] {
    emit Item(kind: 'empty-list', value: item)
  }

  for item in '' {
    emit Item(kind: 'empty-text', value: item)
  }

  for item in nothing {
    emit Item(kind: 'missing', value: item)
  }

  for item in 123 {
    emit Item(kind: 'number', value: item)
  }

  emit Done
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args: []
```

---

## Test: function and predicate calls return values

This runtime case exercises “function and predicate calls return values” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0008
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "function and predicate calls return values.ges"
    program: main
```

### Source code under test

```ges
module AtomicControlReturns
function addOne(_ value) be value + 1
predicate isLarge(_ value) be value > 2

on Start {
  let a be addOne(2)
  if isLarge(a) {
    emit Done(value: a, matched: true)
  } else {
    emit Done(value: a, matched: false)
  }
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "3"
          - name: "matched"
            value:
              type: ":boolean"
              value: true
```

---

## Test: seeded random statement restores outer flow

This runtime case exercises “seeded random statement restores outer flow” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0009
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "seeded random statement restores outer flow.ges"
    program: main
```

### Source code under test

```ges
module AtomicControlSeededRandom
on Start(seed as :number) {
  random with seed {
    let inner be random from 1 to 6
    emit Roll(scope: 'inner', value: inner)
  }

  emit Done(after: true)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "seed"
          value:
            type: ":integer"
            value: "7"
    local:
      - name: "Roll"
        args:
          - name: "scope"
            value:
              type: ":text"
              value: "inner"
          - name: "value"
            value:
              type: ":integer"
              value: "1"
      - name: "Done"
        args:
          - name: "after"
            value:
              type: ":boolean"
              value: true
```
