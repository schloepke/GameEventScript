---
formatVersion: 1
suiteId: "runtime.atomic.control-flow"
title: "RuntimeAtomicControlFlow"
categories: [conformance]
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
module atomiccontrolifbranches
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
              type: ":Text"
              value: "true-then"
      - name: "Branch"
        args:
          - name: "name"
            value:
              type: ":Text"
              value: "false-else"
      - name: "Branch"
        args:
          - name: "name"
            value:
              type: ":Text"
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
module atomiccontrolorshortcircuit
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
              type: ":Boolean"
              value: true
          - name: "falseEvaluates"
            value:
              type: ":Boolean"
              value: true
          - name: "missingResolves"
            value:
              type: ":Boolean"
              value: true
          - name: "missingStaysUnknown"
            value:
              type: ":Nothing"
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
module atomiccontrolandshortcircuit
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
              type: ":Boolean"
              value: false
          - name: "trueEvaluates"
            value:
              type: ":Boolean"
              value: true
          - name: "missingResolvesFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "missingStaysUnknown"
            value:
              type: ":Nothing"
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
module atomiccontrolimplication
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
              type: ":Boolean"
              value: true
          - name: "trueEvaluates"
            value:
              type: ":Boolean"
              value: true
          - name: "trueFails"
            value:
              type: ":Boolean"
              value: false
          - name: "missingResolvesTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "missingStaysUnknown"
            value:
              type: ":Nothing"
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
module atomiccontrolguardedchoice
on Start {
  let firstTrue be (10 when false, or 20 when true, or 30 when true, otherwise 40) as :Number
  let otherwiseValue be (10 when false, or 20 when nothing, otherwise 40) as :Number
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
              type: ":Number.int64"
              value: "20"
          - name: "otherwiseValue"
            value:
              type: ":Number.int64"
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
module atomiccontrolforiteration
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
              type: ":Text"
              value: "list"
          - name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      - name: "Item"
        args:
          - name: "kind"
            value:
              type: ":Text"
              value: "list"
          - name: "value"
            value:
              type: ":Number.int64"
              value: "2"
      - name: "Item"
        args:
          - name: "kind"
            value:
              type: ":Text"
              value: "range"
          - name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      - name: "Item"
        args:
          - name: "kind"
            value:
              type: ":Text"
              value: "range"
          - name: "value"
            value:
              type: ":Number.int64"
              value: "3"
      - name: "Item"
        args:
          - name: "kind"
            value:
              type: ":Text"
              value: "text"
          - name: "value"
            value:
              type: ":Text"
              value: "a"
      - name: "Item"
        args:
          - name: "kind"
            value:
              type: ":Text"
              value: "text"
          - name: "value"
            value:
              type: ":Text"
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
module atomiccontrolforemptysources
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
module atomiccontrolreturns
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
              type: ":Number.int64"
              value: "3"
          - name: "matched"
            value:
              type: ":Boolean"
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
module atomiccontrolseededrandom
on Start(seed as :Number) {
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
            type: ":Number.int64"
            value: "7"
    local:
      - name: "Roll"
        args:
          - name: "scope"
            value:
              type: ":Text"
              value: "inner"
          - name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      - name: "Done"
        args:
          - name: "after"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: if-let-chain

This case verifies if-let-chain.

### Case description

```yaml
gesBlock: case
id: if-let-chain
kind: scriptApi
level: atomic
sources:
  - name: "if-let-chain.ges"
    program: main
```

### Source code under test

```ges
on Start {
  if let x be 0; let y be false; x = 0; let z be 12; { emit Done(value: z) } else { emit Done(value: -1) }
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
              type: ":Number.int64"
              value: "12"
```

---

## Test: if-let-empty

This case verifies if-let-empty.

### Case description

```yaml
gesBlock: case
id: if-let-empty
kind: scriptApi
level: atomic
sources:
  - name: "if-let-empty.ges"
    program: main
```

### Source code under test

```ges
on Start {
  if let x be []; let y be 10 { emit Done(value: y) } else { emit Done(value: 7) }
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
              type: ":Number.int64"
              value: "7"
```

---

## Test: if-let-guarded-choice

This case verifies if-let-guarded-choice.

### Case description

```yaml
gesBlock: case
id: if-let-guarded-choice
kind: scriptApi
level: atomic
sources:
  - name: "if-let-guarded-choice.ges"
    program: main
```

### Source code under test

```ges
on Start {
  if let x be 1 when false, 2 when true otherwise nothing; x = 2 { emit Done(value: x) }
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
              type: ":Number.int64"
              value: "2"
```

---

## Test: if-let-short-circuit

This case verifies if-let-short-circuit.

### Case description

```yaml
gesBlock: case
id: if-let-short-circuit
kind: scriptApi
level: atomic
sources:
  - name: "if-let-short-circuit.ges"
    program: main
```

### Source code under test

```ges
on Start {
  if false; let x be 20 { emit Done(value: x) } else { emit Done(value: 3) }
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
              type: ":Number.int64"
              value: "3"
```

---

## Test: fold-sum

This case verifies fold-sum.

### Case description

```yaml
gesBlock: case
id: fold-sum
kind: scriptApi
level: atomic
sources:
  - name: "fold-sum.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Done(value: [1, 2, 3][:fold acc be 100, value => acc + value])
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
              type: ":Number.int64"
              value: "106"
```

---

## Test: fold-empty

This case verifies fold-empty.

### Case description

```yaml
gesBlock: case
id: fold-empty
kind: scriptApi
level: atomic
sources:
  - name: "fold-empty.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Done(value: [][:fold acc be 100, value => acc + value])
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
              type: ":Number.int64"
              value: "100"
```

---

## Test: reduce-sum

This case verifies reduce-sum.

### Case description

```yaml
gesBlock: case
id: reduce-sum
kind: scriptApi
level: atomic
sources:
  - name: "reduce-sum.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Done(value: [1, 2, 3][:reduce acc, value => acc + value])
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
              type: ":Number.int64"
              value: "6"
```

---

## Test: reduce-empty

This case verifies reduce-empty.

### Case description

```yaml
gesBlock: case
id: reduce-empty
kind: scriptApi
level: atomic
sources:
  - name: "reduce-empty.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Done(value: [][:reduce acc, value => acc + value])
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
              type: ":Nothing"
```

---

## Test: reduce-single

This case verifies reduce-single.

### Case description

```yaml
gesBlock: case
id: reduce-single
kind: scriptApi
level: atomic
sources:
  - name: "reduce-single.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Done(value: [7][:reduce acc, value => acc + 100])
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
              type: ":Number.int64"
              value: "7"
```

---

## Test: fold-order

This case verifies fold-order.

### Case description

```yaml
gesBlock: case
id: fold-order
kind: scriptApi
level: atomic
sources:
  - name: "fold-order.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Done(value: [1, 2, 3][:fold acc be 100, value => acc - value])
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
              type: ":Number.int64"
              value: "94"
```

---

## Test: fold-guarded

This case verifies fold-guarded.

### Case description

```yaml
gesBlock: case
id: fold-guarded
kind: scriptApi
level: atomic
sources:
  - name: "fold-guarded.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Done(value: [1, nothing, 3][:fold acc be 10, value => acc + value when value has value otherwise acc])
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
              type: ":Number.int64"
              value: "14"
```

---

## Test: fold-nested

This case verifies fold-nested.

### Case description

```yaml
gesBlock: case
id: fold-nested
kind: scriptApi
level: atomic
sources:
  - name: "fold-nested.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Done(value: [[1, 2], [3]][:fold acc be 0, item => acc + item[:reduce sum, value => sum + value]])
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
              type: ":Number.int64"
              value: "6"
```

---

## Test: emit-after-binding

This case verifies emit-after-binding.

### Case description

```yaml
gesBlock: case
id: emit-after-binding
kind: scriptApi
level: atomic
sources:
  - name: "emit-after-binding.ges"
    program: main
stepActions:
  step-0002:
    - advanceMicroseconds: "1"
  step-0003:
    - advanceMicroseconds: "1"
```

### Source code under test

```ges
on Start {
  let accepted be emit after 0.0000011s Ping(value: 7)
  emit Accepted(value: accepted)
}
on Tick {}
on Ping(value) { emit Done(value: value) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |
| step-0002 | Tick | completion |  |
| step-0003 | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input: { args: [] }
    waiting: true
    local: [{ name: Ping, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }] }, { name: Accepted, args: [{ name: value, value: { type: ":Boolean", value: true } }] }]
  step-0002:
    input: { args: [] }
    waiting: true
    local: []
  step-0003:
    input: { args: [] }
    waiting: false
    local: [{ name: Done, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }] }]
```

---

## Test: emit-after-binding-tags

This case verifies emit-after-binding-tags.

### Case description

```yaml
gesBlock: case
id: emit-after-binding-tags
kind: scriptApi
level: atomic
sources:
  - name: "emit-after-binding-tags.ges"
    program: main
stepActions:
  step-0002:
    - advanceMicroseconds: "1"
  step-0003:
    - advanceMicroseconds: "1"
```

### Source code under test

```ges
on Start {
  let accepted be emit after 0.0000011s Ping(value: 7) with ["ready", [#ready, ""]]
  emit Accepted(value: accepted)
}
on Tick {}
on Ping(value) { emit Done(value: value) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |
| step-0002 | Tick | completion |  |
| step-0003 | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input: { args: [] }
    waiting: true
    local: [{ name: Ping, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }], tags: [ready] }, { name: Accepted, args: [{ name: value, value: { type: ":Boolean", value: true } }] }]
  step-0002:
    input: { args: [] }
    waiting: true
    local: []
  step-0003:
    input: { args: [] }
    waiting: false
    local: [{ name: Done, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }] }]
```

---

## Test: emit-after-indirect

This case verifies emit-after-indirect.

### Case description

```yaml
gesBlock: case
id: emit-after-indirect
kind: scriptApi
level: atomic
sources:
  - name: "emit-after-indirect.ges"
    program: main
stepActions:
  step-0002:
    - advanceMicroseconds: "1"
  step-0003:
    - advanceMicroseconds: "1"
```

### Source code under test

```ges
on Start {
  let message be Ping(value: 7)
  let accepted be emit after 0.0000011s message
  emit Accepted(value: accepted)
}
on Tick {}
on Ping(value) { emit Done(value: value) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |
| step-0002 | Tick | completion |  |
| step-0003 | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input: { args: [] }
    waiting: true
    local: [{ name: Ping, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }] }, { name: Accepted, args: [{ name: value, value: { type: ":Boolean", value: true } }] }]
  step-0002:
    input: { args: [] }
    waiting: true
    local: []
  step-0003:
    input: { args: [] }
    waiting: false
    local: [{ name: Done, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }] }]
```

---

## Test: emit-after-indirect-tags

This case verifies emit-after-indirect-tags.

### Case description

```yaml
gesBlock: case
id: emit-after-indirect-tags
kind: scriptApi
level: atomic
sources:
  - name: "emit-after-indirect-tags.ges"
    program: main
stepActions:
  step-0002:
    - advanceMicroseconds: "1"
  step-0003:
    - advanceMicroseconds: "1"
```

### Source code under test

```ges
on Start {
  let message be Ping(value: 7)
  let accepted be emit after 0.0000011s message with ["ready", [#ready, ""]]
  emit Accepted(value: accepted)
}
on Tick {}
on Ping(value) { emit Done(value: value) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |
| step-0002 | Tick | completion |  |
| step-0003 | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input: { args: [] }
    waiting: true
    local: [{ name: Ping, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }], tags: [ready] }, { name: Accepted, args: [{ name: value, value: { type: ":Boolean", value: true } }] }]
  step-0002:
    input: { args: [] }
    waiting: true
    local: []
  step-0003:
    input: { args: [] }
    waiting: false
    local: [{ name: Done, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }] }]
```

---

## Test: publish-after-binding

This case verifies publish-after-binding.

### Case description

```yaml
gesBlock: case
id: publish-after-binding
kind: scriptApi
level: atomic
sources:
  - name: "publish-after-binding.ges"
    program: main
stepActions:
  step-0002:
    - advanceMicroseconds: "1"
  step-0003:
    - advanceMicroseconds: "1"
```

### Source code under test

```ges
on Start {
  let accepted be publish after 0.0000011s Ping(value: 7)
  emit Accepted(value: accepted)
}
on Tick {}
on Ping(value) { emit Done(value: value) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |
| step-0002 | Tick | completion |  |
| step-0003 | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input: { args: [] }
    waiting: true
    local: [{ name: Accepted, args: [{ name: value, value: { type: ":Boolean", value: true } }] }]
  step-0002:
    input: { args: [] }
    waiting: true
    local: []
  step-0003:
    input: { args: [] }
    waiting: false
    local: [{ name: Ping, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }] }, { name: Done, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }] }]
    outbound: [{ name: Ping, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }] }]
```

---

## Test: publish-after-binding-tags

This case verifies publish-after-binding-tags.

### Case description

```yaml
gesBlock: case
id: publish-after-binding-tags
kind: scriptApi
level: atomic
sources:
  - name: "publish-after-binding-tags.ges"
    program: main
stepActions:
  step-0002:
    - advanceMicroseconds: "1"
  step-0003:
    - advanceMicroseconds: "1"
```

### Source code under test

```ges
on Start {
  let accepted be publish after 0.0000011s Ping(value: 7) with ["ready", [#ready, ""]]
  emit Accepted(value: accepted)
}
on Tick {}
on Ping(value) { emit Done(value: value) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |
| step-0002 | Tick | completion |  |
| step-0003 | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input: { args: [] }
    waiting: true
    local: [{ name: Accepted, args: [{ name: value, value: { type: ":Boolean", value: true } }] }]
  step-0002:
    input: { args: [] }
    waiting: true
    local: []
  step-0003:
    input: { args: [] }
    waiting: false
    local: [{ name: Ping, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }], tags: [ready] }, { name: Done, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }] }]
    outbound: [{ name: Ping, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }], tags: [ready] }]
```

---

## Test: publish-after-indirect

This case verifies publish-after-indirect.

### Case description

```yaml
gesBlock: case
id: publish-after-indirect
kind: scriptApi
level: atomic
sources:
  - name: "publish-after-indirect.ges"
    program: main
stepActions:
  step-0002:
    - advanceMicroseconds: "1"
  step-0003:
    - advanceMicroseconds: "1"
```

### Source code under test

```ges
on Start {
  let message be Ping(value: 7)
  let accepted be publish after 0.0000011s message
  emit Accepted(value: accepted)
}
on Tick {}
on Ping(value) { emit Done(value: value) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |
| step-0002 | Tick | completion |  |
| step-0003 | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input: { args: [] }
    waiting: true
    local: [{ name: Accepted, args: [{ name: value, value: { type: ":Boolean", value: true } }] }]
  step-0002:
    input: { args: [] }
    waiting: true
    local: []
  step-0003:
    input: { args: [] }
    waiting: false
    local: [{ name: Ping, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }] }, { name: Done, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }] }]
    outbound: [{ name: Ping, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }] }]
```

---

## Test: publish-after-indirect-tags

This case verifies publish-after-indirect-tags.

### Case description

```yaml
gesBlock: case
id: publish-after-indirect-tags
kind: scriptApi
level: atomic
sources:
  - name: "publish-after-indirect-tags.ges"
    program: main
stepActions:
  step-0002:
    - advanceMicroseconds: "1"
  step-0003:
    - advanceMicroseconds: "1"
```

### Source code under test

```ges
on Start {
  let message be Ping(value: 7)
  let accepted be publish after 0.0000011s message with ["ready", [#ready, ""]]
  emit Accepted(value: accepted)
}
on Tick {}
on Ping(value) { emit Done(value: value) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |
| step-0002 | Tick | completion |  |
| step-0003 | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input: { args: [] }
    waiting: true
    local: [{ name: Accepted, args: [{ name: value, value: { type: ":Boolean", value: true } }] }]
  step-0002:
    input: { args: [] }
    waiting: true
    local: []
  step-0003:
    input: { args: [] }
    waiting: false
    local: [{ name: Ping, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }], tags: [ready] }, { name: Done, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }] }]
    outbound: [{ name: Ping, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }], tags: [ready] }]
```

---

## Test: emit-instant-result

This case verifies emit-instant-result.

### Case description

```yaml
gesBlock: case
id: emit-instant-result
kind: scriptApi
level: atomic
sources:
  - name: "emit-instant-result.ges"
    program: main
publishSink: absent
```

### Source code under test

```ges
on Start { let accepted be emit Missing(value: 9)
 emit Done(value: accepted) }
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
    input: { args: [] }
    waiting: false
    local: [{ name: Missing, args: [{ name: value, value: { type: ":Number.int64", value: "9" } }] }, { name: Done, args: [{ name: value, value: { type: ":Boolean", value: true } }] }]
```

---

## Test: publish-instant-result

This case verifies publish-instant-result.

### Case description

```yaml
gesBlock: case
id: publish-instant-result
kind: scriptApi
level: atomic
sources:
  - name: "publish-instant-result.ges"
    program: main
publishSink: absent
```

### Source code under test

```ges
on Start { let accepted be publish Missing(value: 9)
 emit Done(value: accepted) }
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
    input: { args: [] }
    waiting: false
    local: [{ name: Missing, args: [{ name: value, value: { type: ":Number.int64", value: "9" } }] }, { name: Done, args: [{ name: value, value: { type: ":Boolean", value: true } }] }]
```

---

## Test: delay-unitless

This case verifies delay-unitless.

### Case description

```yaml
gesBlock: case
id: delay-unitless
kind: compileError
level: atomic
sources:
  - name: "delay-unitless.ges"
    program: main
```

### Source code under test

```ges
on Start { emit after 10 Ping() }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
```

---

## Test: delay-distance

This case verifies delay-distance.

### Case description

```yaml
gesBlock: case
id: delay-distance
kind: compileError
level: atomic
sources:
  - name: "delay-distance.ges"
    program: main
```

### Source code under test

```ges
on Start { emit after 10m Ping() }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
```

---

## Test: if-binding-else

This case verifies if-binding-else.

### Case description

```yaml
gesBlock: case
id: if-binding-else
kind: compileError
level: atomic
sources:
  - name: "if-binding-else.ges"
    program: main
```

### Source code under test

```ges
on Start { if let x be 0 { } else { emit Done(value: x) } }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "compile"
  code: "compile.unresolvedSymbol"
```

---

## Test: delay-nothing

This case verifies delay-nothing.

### Case description

```yaml
gesBlock: case
id: delay-nothing
kind: scriptApi
level: atomic
sources:
  - name: "delay-nothing.ges"
    program: main
```

### Source code under test

```ges
on Start { let accepted be emit after nothing Ping()
 emit Done(value: accepted) }
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
    input: { args: [] }
    waiting: false
    local: [{ name: Done, args: [{ name: value, value: { type: ":Boolean", value: false } }] }]
```

---

## Test: delay-negative

This case verifies delay-negative.

### Case description

```yaml
gesBlock: case
id: delay-negative
kind: scriptApi
level: atomic
sources:
  - name: "delay-negative.ges"
    program: main
```

### Source code under test

```ges
on Start { let accepted be emit after -0.0000001s Ping()
 emit Done(value: accepted) }
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
    input: { args: [] }
    waiting: false
    local: [{ name: Done, args: [{ name: value, value: { type: ":Boolean", value: false } }] }]
```

---

## Test: delay-infinity

This case verifies delay-infinity.

### Case description

```yaml
gesBlock: case
id: delay-infinity
kind: scriptApi
level: atomic
sources:
  - name: "delay-infinity.ges"
    program: main
```

### Source code under test

```ges
on Start { let accepted be emit after (1s / 0) Ping()
 emit Done(value: accepted) }
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
    input: { args: [] }
    waiting: false
    local: [{ name: Done, args: [{ name: value, value: { type: ":Boolean", value: false } }] }]
```

---

## Test: delay-overflow

This case verifies delay-overflow.

### Case description

```yaml
gesBlock: case
id: delay-overflow
kind: scriptApi
level: atomic
sources:
  - name: "delay-overflow.ges"
    program: main
```

### Source code under test

```ges
on Start { let accepted be emit after 100000000000000000000s Ping()
 emit Done(value: accepted) }
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
    input: { args: [] }
    waiting: false
    local: [{ name: Done, args: [{ name: value, value: { type: ":Boolean", value: false } }] }]
```

---

## Test: fold-source-invalid

This case verifies fold-source-invalid.

### Case description

```yaml
gesBlock: case
id: fold-source-invalid
kind: scriptApi
level: atomic
sources:
  - name: "fold-source-invalid.ges"
    program: main
```

### Source code under test

```ges
on Start { emit Done(value: 12[:fold acc be 10, item => acc + item]) }
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
    input: { args: [] }
    local: [{ name: Done, args: [{ name: value, value: { type: ":Nothing" } }] }]
```

---

## Test: fold-nothing-continues

This case verifies fold-nothing-continues.

### Case description

```yaml
gesBlock: case
id: fold-nothing-continues
kind: scriptApi
level: atomic
sources:
  - name: "fold-nothing-continues.ges"
    program: main
```

### Source code under test

```ges
on Start { emit Done(value: [1, 2, 3][:fold acc be 0, item => nothing when item = 2 otherwise item]) }
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
    input: { args: [] }
    local: [{ name: Done, args: [{ name: value, value: { type: ":Number.int64", value: "3" } }] }]
```

---

## Test: reduce-nothing-first

This case verifies reduce-nothing-first.

### Case description

```yaml
gesBlock: case
id: reduce-nothing-first
kind: scriptApi
level: atomic
sources:
  - name: "reduce-nothing-first.ges"
    program: main
```

### Source code under test

```ges
on Start { emit Done(value: [nothing, 2][:reduce acc, item => item]) }
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
    input: { args: [] }
    local: [{ name: Done, args: [{ name: value, value: { type: ":Number.int64", value: "2" } }] }]
```

---

## Test: if-let-text-empty-fails

This case verifies if-let-text-empty-fails.

### Case description

```yaml
gesBlock: case
id: if-let-text-empty-fails
kind: scriptApi
level: atomic
sources:
  - name: "if-let-text-empty-fails.ges"
    program: main
```

### Source code under test

```ges
on Start { if let value be "" { emit Done(value: 1) } else { emit Done(value: 2) } }
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
    input: { args: [] }
    local: [{ name: Done, args: [{ name: value, value: { type: ":Number.int64", value: "2" } }] }]
```

---

## Test: if-let-list-empty-fails

This case verifies if-let-list-empty-fails.

### Case description

```yaml
gesBlock: case
id: if-let-list-empty-fails
kind: scriptApi
level: atomic
sources:
  - name: "if-let-list-empty-fails.ges"
    program: main
```

### Source code under test

```ges
on Start { if let value be [] { emit Done(value: 1) } else { emit Done(value: 2) } }
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
    input: { args: [] }
    local: [{ name: Done, args: [{ name: value, value: { type: ":Number.int64", value: "2" } }] }]
```

---

## Test: if-let-evaluates-once

This case verifies if-let-evaluates-once.

### Case description

```yaml
gesBlock: case
id: if-let-evaluates-once
kind: scriptApi
level: atomic
sources:
  - name: "if-let-evaluates-once.ges"
    program: main
```

### Source code under test

```ges
on Start { if let value be emit Marker() { emit Done(value: value) } }
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
    input: { args: [] }
    local: [{ name: Marker }, { name: Done, args: [{ name: value, value: { type: ":Boolean", value: true } }] }]
```

---

## Test: publish-instant-rejecting-sink

This case verifies publish-instant-rejecting-sink.

### Case description

```yaml
gesBlock: case
id: publish-instant-rejecting-sink
kind: scriptApi
level: atomic
sources:
  - name: "publish-instant-rejecting-sink.ges"
    program: main
publishSink: reject
```

### Source code under test

```ges
on Start { let accepted be publish Ping()
 emit Done(value: accepted) }
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
    input: { args: [] }
    local: [{ name: Ping }, { name: Done, args: [{ name: value, value: { type: ":Boolean", value: true } }] }]
    outbound: [{ name: Ping }]
```

---

## Test: after-zero-immediate

This case verifies after-zero-immediate.

### Case description

```yaml
gesBlock: case
id: after-zero-immediate
kind: scriptApi
level: atomic
sources:
  - name: "after-zero-immediate.ges"
    program: main
```

### Source code under test

```ges
on Start { let accepted be emit after 0s Ping(value: 7)
 emit Done(value: accepted) }
on Ping(value) { emit Done(value: value) }
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
    input: { args: [] }
    waiting: false
    local: [{ name: Ping, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }] }, { name: Done, args: [{ name: value, value: { type: ":Boolean", value: true } }] }, { name: Done, args: [{ name: value, value: { type: ":Number.int64", value: "7" } }] }]
```

---

## Test: delayed-deadline-and-fifo

This case verifies delayed-deadline-and-fifo.

### Case description

```yaml
gesBlock: case
id: delayed-deadline-and-fifo
kind: scriptApi
level: atomic
sources:
  - name: "delayed-deadline-and-fifo.ges"
    program: main
stepActions:
  step-0002:
    - advanceMicroseconds: "2"
```

### Source code under test

```ges
on Start { emit after 0.000002s Ping(value: 3); emit after 0.000001s Ping(value: 1); emit after 0.000001s Ping(value: 2) }
on Tick { emit Done(value: 0) }
on Ping(value) { emit Done(value: value) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |
| step-0002 | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input: { args: [] }
    waiting: true
    local: [{ name: Ping, args: [{ name: value, value: { type: ":Number.int64", value: "3" } }] }, { name: Ping, args: [{ name: value, value: { type: ":Number.int64", value: "1" } }] }, { name: Ping, args: [{ name: value, value: { type: ":Number.int64", value: "2" } }] }]
  step-0002:
    input: { args: [] }
    waiting: false
    local: [{ name: Done, args: [{ name: value, value: { type: ":Number.int64", value: "0" } }] }, { name: Done, args: [{ name: value, value: { type: ":Number.int64", value: "1" } }] }, { name: Done, args: [{ name: value, value: { type: ":Number.int64", value: "2" } }] }, { name: Done, args: [{ name: value, value: { type: ":Number.int64", value: "3" } }] }]
```

---

## Test: emit-delayed-queue-limit

This case verifies emit-delayed-queue-limit.

### Case description

```yaml
gesBlock: case
id: emit-delayed-queue-limit
kind: scriptApi
level: atomic
sources:
  - name: "emit-delayed-queue-limit.ges"
    program: main
runtimeLimits:
  maxQueuedMessagesPerRun: 1
```

### Source code under test

```ges
on Start { emit after 1s Ping(); emit after 1s Ping(); emit Bad() }
on Ping {}
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
    input: { args: [] }
    waiting: false
    local: [{ name: Ping }]
    outbound: []
    runtimeLimits:
      include:
        - name: MaxQueuedMessagesPerRun
          limit: 1
```

---

## Test: publish-delayed-queue-limit

This case verifies publish-delayed-queue-limit.

### Case description

```yaml
gesBlock: case
id: publish-delayed-queue-limit
kind: scriptApi
level: atomic
sources:
  - name: "publish-delayed-queue-limit.ges"
    program: main
runtimeLimits:
  maxQueuedMessagesPerRun: 1
```

### Source code under test

```ges
on Start { publish after 1s Ping(); publish after 1s Ping(); emit Bad() }
on Ping {}
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
    input: { args: [] }
    waiting: false
    local: []
    outbound: []
    runtimeLimits:
      include:
        - name: MaxQueuedMessagesPerRun
          limit: 1
```

---

## Test: instant-result-respects-queue-limit

This case verifies instant-result-respects-queue-limit.

### Case description

```yaml
gesBlock: case
id: instant-result-respects-queue-limit
kind: scriptApi
level: atomic
sources:
  - name: "instant-result-respects-queue-limit.ges"
    program: main
runtimeLimits:
  maxQueuedMessagesPerRun: 1
```

### Source code under test

```ges
on Start { emit after 1s Ping(); let accepted be emit Ping(); emit Bad() }
on Ping {}
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
    input: { args: [] }
    waiting: false
    local: [{ name: Ping }]
    runtimeLimits:
      include:
        - name: MaxQueuedMessagesPerRun
          limit: 1
```

---

## Test: delayed-does-not-preempt-paused-handler

This case verifies delayed-does-not-preempt-paused-handler.

### Case description

```yaml
gesBlock: case
id: delayed-does-not-preempt-paused-handler
kind: scriptApi
level: atomic
sources:
  - name: "delayed-does-not-preempt-paused-handler.ges"
    program: main
stepActions:
  step-0003:
    - advanceMicroseconds: "1000000"
```

### Source code under test

```ges
on Start { emit after 1s Ping() }
on Work { emit First(); emit Second() }
on Ping { emit Due() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |
| step-0002 | Work | frame | 1 |
| step-0003 | Unknown | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    waiting: true
    local: [{ name: Ping }]
  step-0002:
    paused: true
    waiting: false
    local: []
  step-0003:
    accepted: false
    waiting: false
    local: [{ name: First }, { name: Second }, { name: Due }]
```

---

## Test: fold-seed-evaluated-on-empty

This case verifies fold-seed-evaluated-on-empty.

### Case description

```yaml
gesBlock: case
id: fold-seed-evaluated-on-empty
kind: scriptApi
level: atomic
sources:
  - name: "fold-seed-evaluated-on-empty.ges"
    program: main
```

### Source code under test

```ges
on Start { let result be [][:fold acc be emit Seed(), item => acc]
 emit Done(value: result) }
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
    local: [{ name: Seed }, { name: Done, args: [{ name: value, value: { type: ":Boolean", value: true } }] }]
```

---

## Test: due-promotion-respects-message-limit

This case verifies due-promotion-respects-message-limit.

### Case description

```yaml
gesBlock: case
id: due-promotion-respects-message-limit
kind: scriptApi
level: atomic
sources:
  - name: "due-promotion-respects-message-limit.ges"
    program: main
runtimeLimits:
  maxProcessedEventsPerRun: 2
nativeHandlers:
  - id: clock
    message: Advance
    actions:
      - advanceMicroseconds: "1000000"
```

### Source code under test

```ges
on Start { emit after 1s Ping(); emit Advance() }
on Ping { emit Done() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |
| step-0002 | Unknown | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    waiting: false
    local: [{ name: Ping }, { name: Advance }]
    runtimeLimits:
      include:
        - name: MaxProcessedEventsPerRun
          limit: 2
  step-0002:
    accepted: false
    waiting: false
    local: [{ name: Done }]
```

---

## Test: result-send-invalid-tags-false

This case verifies result-send-invalid-tags-false.

### Case description

```yaml
gesBlock: case
id: result-send-invalid-tags-false
kind: scriptApi
level: atomic
sources:
  - name: "result-send-invalid-tags-false.ges"
    program: main
```

### Source code under test

```ges
on Start { let accepted be emit Ping(value: 1) with "Invalid"
 emit Done(value: accepted) }
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
    local: [{ name: Done, args: [{ name: value, value: { type: ":Boolean", value: false } }] }]
```

---

## Test: result-send-invalid-tags-true

This case verifies result-send-invalid-tags-true.

### Case description

```yaml
gesBlock: case
id: result-send-invalid-tags-true
kind: scriptApi
level: atomic
sources:
  - name: "result-send-invalid-tags-true.ges"
    program: main
```

### Source code under test

```ges
on Start { let message be Ping(value: 1); let accepted be emit message with "Invalid"
 emit Done(value: accepted) }
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
    local: [{ name: Done, args: [{ name: value, value: { type: ":Boolean", value: false } }] }]
```

---

## Test: delayed-computed-variable-and-function

This case verifies delayed-computed-variable-and-function.

### Case description

```yaml
gesBlock: case
id: delayed-computed-variable-and-function
kind: scriptApi
level: atomic
sources:
  - name: "delayed-computed-variable-and-function.ges"
    program: main
stepActions:
  step-0002:
    - advanceMicroseconds: "2000"
```

### Source code under test

```ges
function duration(_ x) be x + 0.001s
on Start {
  let delay be 0.001s + 0.001s
  emit after delay Ping(value: 1)
  emit after duration(0.001s) Ping(value: 2)
}
on Tick {}
on Ping(value) { emit Done(value: value) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |
| step-0002 | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
steps: {"step-0001": {"waiting": true, "local": [{"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "1"}}]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "2"}}]}]}, "step-0002": {"waiting": false, "local": [{"name": "Done", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "1"}}]}, {"name": "Done", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "2"}}]}]}}
```

---

## Test: result-emit-operator-boundaries

This case verifies result-emit-operator-boundaries.

### Case description

```yaml
gesBlock: case
id: result-emit-operator-boundaries
kind: scriptApi
level: atomic
sources:
  - name: "result-emit-operator-boundaries.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let message be Ping(value: 2)
  let direct be emit Ping(value: 1) and false
  let indirect be emit message and false
  let disjunction be emit message or (emit Unexpected(value: 1))
  if emit Ping(value: 3) with #ready and false { emit Unexpected(value: 2) }
  let skipped be false and (emit Unexpected(value: 3))
  let compound be emit (nothing default message) with ([#ready] | [#extra]) and true
  emit Result(value: direct)
  emit Result(value: indirect)
  emit Result(value: disjunction)
  emit Result(value: skipped)
  emit Result(value: compound)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps: {"step-0001": {"local": [{"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "1"}}]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "2"}}]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "2"}}]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "3"}}], "tags": ["ready"]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "2"}}], "tags": ["ready", "extra"]}, {"name": "Result", "args": [{"name": "value", "value": {"type": ":Boolean", "value": false}}]}, {"name": "Result", "args": [{"name": "value", "value": {"type": ":Boolean", "value": false}}]}, {"name": "Result", "args": [{"name": "value", "value": {"type": ":Boolean", "value": true}}]}, {"name": "Result", "args": [{"name": "value", "value": {"type": ":Boolean", "value": false}}]}, {"name": "Result", "args": [{"name": "value", "value": {"type": ":Boolean", "value": true}}]}]}}
```

---

## Test: result-publish-operator-boundaries

This case verifies result-publish-operator-boundaries.

### Case description

```yaml
gesBlock: case
id: result-publish-operator-boundaries
kind: scriptApi
level: atomic
sources:
  - name: "result-publish-operator-boundaries.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let message be Ping(value: 2)
  let direct be publish Ping(value: 1) and false
  let indirect be publish message and false
  let disjunction be publish message or (publish Unexpected(value: 1))
  if publish Ping(value: 3) with #ready and false { emit Unexpected(value: 2) }
  let skipped be false and (publish Unexpected(value: 3))
  let compound be publish (nothing default message) with ([#ready] | [#extra]) and true
  emit Result(value: direct)
  emit Result(value: indirect)
  emit Result(value: disjunction)
  emit Result(value: skipped)
  emit Result(value: compound)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps: {"step-0001": {"local": [{"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "1"}}]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "2"}}]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "2"}}]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "3"}}], "tags": ["ready"]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "2"}}], "tags": ["ready", "extra"]}, {"name": "Result", "args": [{"name": "value", "value": {"type": ":Boolean", "value": false}}]}, {"name": "Result", "args": [{"name": "value", "value": {"type": ":Boolean", "value": false}}]}, {"name": "Result", "args": [{"name": "value", "value": {"type": ":Boolean", "value": true}}]}, {"name": "Result", "args": [{"name": "value", "value": {"type": ":Boolean", "value": false}}]}, {"name": "Result", "args": [{"name": "value", "value": {"type": ":Boolean", "value": true}}]}], "outbound": [{"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "1"}}]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "2"}}]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "2"}}]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "3"}}], "tags": ["ready"]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "2"}}], "tags": ["ready", "extra"]}]}}
```

---

## Test: delayed-send-operator-boundaries

This case verifies delayed-send-operator-boundaries.

### Case description

```yaml
gesBlock: case
id: delayed-send-operator-boundaries
kind: scriptApi
level: atomic
sources:
  - name: "delayed-send-operator-boundaries.ges"
    program: main
stepActions:
  step-0002:
    - advanceMicroseconds: "2000"
```

### Source code under test

```ges
on Start {
  let message be Ping(value: 1)
  let emitted be emit after 0.001s message and false
  if publish after 0.001s Ping(value: 2) with #ready and false { emit Unexpected(value: 1) }
  emit Result(value: emitted)
}
on Tick {}
on Ping(value) { emit Done(value: value) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |
| step-0002 | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
steps: {"step-0001": {"waiting": true, "local": [{"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "1"}}]}, {"name": "Result", "args": [{"name": "value", "value": {"type": ":Boolean", "value": false}}]}]}, "step-0002": {"waiting": false, "local": [{"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "2"}}], "tags": ["ready"]}, {"name": "Done", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "1"}}]}, {"name": "Done", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "2"}}]}], "outbound": [{"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "2"}}], "tags": ["ready"]}]}}
```

---

## Test: if-branches-own-scopes-without-braces

This case verifies if-branches-own-scopes-without-braces.

### Case description

```yaml
gesBlock: case
id: if-branches-own-scopes-without-braces
kind: scriptApi
level: atomic
sources:
  - name: "if-branches-own-scopes-without-braces.ges"
    program: main
```

### Source code under test

```ges
on Start {
  if true let x be 1
  let x be 3
  emit Done(value: x)
  if false let y be 1 else let y be 2
  let y be 4
  emit Done(value: y)
  if let a be 2; a > 0 { let b be a * 20; emit Done(value: b) } else { let a be 9; emit Done(value: a) }
  let a be 5
  emit Done(value: a)
  if let bound be 2 let result be bound * 20
  let result be 6
  emit Done(value: result)
  let conditional be 7 when false otherwise nothing
  if conditional has value { emit Done(value: -1) } else { emit Done(value: 8) }
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps: {"step-0001": {"local": [{"name": "Done", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "3"}}]}, {"name": "Done", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "4"}}]}, {"name": "Done", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "40"}}]}, {"name": "Done", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "5"}}]}, {"name": "Done", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "6"}}]}, {"name": "Done", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "8"}}]}]}}
```

---

## Test: then-binding-does-not-escape

This case verifies then-binding-does-not-escape.

### Case description

```yaml
gesBlock: case
id: then-binding-does-not-escape
kind: compileError
level: atomic
sources:
  - name: "then-binding-does-not-escape.ges"
    program: main
```

### Source code under test

```ges
on Start { if true let x be 1
 emit Done(value: x) }
```

### Expectation

```yaml
gesBlock: expect
error: {"phase": "compile", "code": "compile.unresolvedSymbol"}
```

---

## Test: else-binding-does-not-escape

This case verifies else-binding-does-not-escape.

### Case description

```yaml
gesBlock: case
id: else-binding-does-not-escape
kind: compileError
level: atomic
sources:
  - name: "else-binding-does-not-escape.ges"
    program: main
```

### Source code under test

```ges
on Start { if false emit Other() else let x be 1
 emit Done(value: x) }
```

### Expectation

```yaml
gesBlock: expect
error: {"phase": "compile", "code": "compile.unresolvedSymbol"}
```

---

## Test: header-body-binding-does-not-escape

This case verifies header-body-binding-does-not-escape.

### Case description

```yaml
gesBlock: case
id: header-body-binding-does-not-escape
kind: compileError
level: atomic
sources:
  - name: "header-body-binding-does-not-escape.ges"
    program: main
```

### Source code under test

```ges
on Start { if let x be 2 let y be x * 20
 emit Done(value: y) }
```

### Expectation

```yaml
gesBlock: expect
error: {"phase": "compile", "code": "compile.unresolvedSymbol"}
```

---

## Test: then-binding-not-in-else

This case verifies then-binding-not-in-else.

### Case description

```yaml
gesBlock: case
id: then-binding-not-in-else
kind: compileError
level: atomic
sources:
  - name: "then-binding-not-in-else.ges"
    program: main
```

### Source code under test

```ges
on Start { if false let x be 2 else emit Done(value: x) }
```

### Expectation

```yaml
gesBlock: expect
error: {"phase": "compile", "code": "compile.unresolvedSymbol"}
```

---

## Test: header-then-duplicate-false

This case verifies header-then-duplicate-false.

### Case description

```yaml
gesBlock: case
id: header-then-duplicate-false
kind: compileError
level: atomic
sources:
  - name: "header-then-duplicate-false.ges"
    program: main
```

### Source code under test

```ges
on Start { if let x be 1 let x be 2 }
```

### Expectation

```yaml
gesBlock: expect
error: {"phase": "validate", "code": "validate.duplicateVariable"}
```

---

## Test: if-ancestor-shadow-false

This case verifies if-ancestor-shadow-false.

### Case description

```yaml
gesBlock: case
id: if-ancestor-shadow-false
kind: compileError
level: atomic
sources:
  - name: "if-ancestor-shadow-false.ges"
    program: main
```

### Source code under test

```ges
on Start { let x be 1; if true let x be 2 }
```

### Expectation

```yaml
gesBlock: expect
error: {"phase": "validate", "code": "validate.shadowedVariable"}
```

---

## Test: header-then-duplicate-true

This case verifies header-then-duplicate-true.

### Case description

```yaml
gesBlock: case
id: header-then-duplicate-true
kind: compileError
level: atomic
sources:
  - name: "header-then-duplicate-true.ges"
    program: main
```

### Source code under test

```ges
on Start { if let x be 1 { let x be 2 } }
```

### Expectation

```yaml
gesBlock: expect
error: {"phase": "validate", "code": "validate.duplicateVariable"}
```

---

## Test: if-ancestor-shadow-true

This case verifies if-ancestor-shadow-true.

### Case description

```yaml
gesBlock: case
id: if-ancestor-shadow-true
kind: compileError
level: atomic
sources:
  - name: "if-ancestor-shadow-true.ges"
    program: main
```

### Source code under test

```ges
on Start { let x be 1; if true { let x be 2 } }
```

### Expectation

```yaml
gesBlock: expect
error: {"phase": "validate", "code": "validate.shadowedVariable"}
```

---

## Test: delays-preserve-computed-quantity-units

This case verifies delays-preserve-computed-quantity-units.

### Case description

```yaml
gesBlock: case
id: delays-preserve-computed-quantity-units
kind: scriptApi
level: atomic
sources:
  - name: "delays-preserve-computed-quantity-units.ges"
    program: main
stepActions:
  step-0002:
    - advanceMicroseconds: "1000000"
```

### Source code under test

```ges
function positive(_ value) be abs value
on Start {
  let delay be abs (-1s)
  emit after (abs (-1s)) Ping(value: 1)
  emit after (positive(-1s)) Ping(value: 2)
  emit after (delay) Ping(value: 3)
  emit after (hypot(0s, 1s)) Ping(value: 4)
  emit after (distance(0s, 1s)) Ping(value: 5)
  emit after (clamp 1s between 0s and 2s) Ping(value: 6)
  emit after (min of 1s and 2s) Ping(value: 7)
  emit after (max of 0s and 1s) Ping(value: 8)
}
on Tick {}
on Ping(value) { emit Done(value: value) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |
| step-0002 | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
steps: {"step-0001": {"local": [{"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "1"}}]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "2"}}]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "3"}}]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "4"}}]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "5"}}]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "6"}}]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "7"}}]}, {"name": "Ping", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "8"}}]}], "waiting": true}, "step-0002": {"waiting": false, "local": [{"name": "Done", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "1"}}]}, {"name": "Done", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "2"}}]}, {"name": "Done", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "3"}}]}, {"name": "Done", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "4"}}]}, {"name": "Done", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "5"}}]}, {"name": "Done", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "6"}}]}, {"name": "Done", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "7"}}]}, {"name": "Done", "args": [{"name": "value", "value": {"type": ":Number.int64", "value": "8"}}]}]}}
```

---

## Test: delay-rejects-abs-unitless

This case verifies delay-rejects-abs-unitless.

### Case description

```yaml
gesBlock: case
id: delay-rejects-abs-unitless
kind: compileError
level: atomic
sources:
  - name: "delay-rejects-abs-unitless.ges"
    program: main
```

### Source code under test

```ges
on Start { emit after (abs (-1)) Ping() }
```

### Expectation

```yaml
gesBlock: expect
error: {"phase": "validate", "code": "validate.invalidTypeConstructor"}
```

---

## Test: delay-rejects-abs-distance

This case verifies delay-rejects-abs-distance.

### Case description

```yaml
gesBlock: case
id: delay-rejects-abs-distance
kind: compileError
level: atomic
sources:
  - name: "delay-rejects-abs-distance.ges"
    program: main
```

### Source code under test

```ges
on Start { emit after (abs (-1m)) Ping() }
```

### Expectation

```yaml
gesBlock: expect
error: {"phase": "validate", "code": "validate.invalidTypeConstructor"}
```

---

## Test: delay-rejects-floor-unitless

This case verifies delay-rejects-floor-unitless.

### Case description

```yaml
gesBlock: case
id: delay-rejects-floor-unitless
kind: compileError
level: atomic
sources:
  - name: "delay-rejects-floor-unitless.ges"
    program: main
```

### Source code under test

```ges
on Start { emit after (floor 1s) Ping() }
```

### Expectation

```yaml
gesBlock: expect
error: {"phase": "validate", "code": "validate.invalidTypeConstructor"}
```
