---
formatVersion: 1
suiteId: "runtime.control-flow"
title: "RuntimeControlFlow"
categories: [conformance]
---

# RuntimeControlFlow

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies branches, loops, early exits, and runtime limits in complete execution scenarios.

---

## Test: simple emit

This runtime case exercises “simple emit” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0001
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "simple emit.ges"
    program: main
```

### Source code under test

```ges
on Start {
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

## Test: standalone and trailing comments are ignored during execution

This runtime case exercises “standalone and trailing comments are ignored during execution” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0002
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "standalone and trailing comments are ignored during execution.ges"
    program: main
```

### Source code under test

```ges
module commentruntime
// initialize flow
on Start { // handler begins
  let hp be 10 // base value
  emit Done(value: hp) // finished
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
              value: "10"
```

---

## Test: semicolons and continued expressions preserve behavior

This runtime case exercises “semicolons and continued expressions preserve behavior” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0003
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "semicolons and continued expressions preserve behavior.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let lines be 10; let values be 20
  let result be 1 +
    2
  emit Done(lines: lines, values: values, result: result)
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
          - name: "lines"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "values"
            value:
              type: ":Number.int64"
              value: "20"
          - name: "result"
            value:
              type: ":Number.int64"
              value: "3"
```

---

## Test: published messages are dispatched through the same host queue

This runtime case exercises “published messages are dispatched through the same host queue” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0004
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "published messages are dispatched through the same host queue.ges"
    program: main
```

### Source code under test

```ges
on Start(value) {
  emit Next(value: value + 1)
}

on Next(value) {
  emit Done(value: value)
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
        - name: "value"
          value:
            type: ":Number.binary64"
            value: "2"
    local:
      - name: "Next"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "3"
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "3"
```

---

## Test: processing limit stops event loops

This runtime case exercises “processing limit stops event loops” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0005
kind: scriptApi
level: scenario
runtimeLimits:
  maxProcessedEventsPerRun: 4
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "processing limit stops event loops.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Loop
}

on Loop {
  emit Start
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
      - name: "Loop"
        args: []
      - name: "Start"
        args: []
      - name: "Loop"
        args: []
      - name: "Start"
        args: []
```

---

## Test: queue limit drops newest published messages

This runtime case exercises “queue limit drops newest published messages” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0006
kind: scriptApi
level: scenario
runtimeLimits:
  maxQueuedMessagesPerRun: 2
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "queue limit drops newest published messages.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit A
  emit B
  emit C
}

on A {}
on B {}
on C {}
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
      - name: "A"
        args: []
      - name: "B"
        args: []
      - name: "C"
        args: []
    runtimeLimits:
      include:
        - name: "MaxQueuedMessagesPerRun"
          detailContains: "Dropped \u0027C\u0027"
```

---

## Test: if and for single statements run without blocks

This runtime case exercises “if and for single statements run without blocks” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0007
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "if and for single statements run without blocks.ges"
    program: main
```

### Source code under test

```ges
on Start(first, second, items) {
  if first emit One
  else if second emit Two
  else emit Three

  for item in items emit Item(value: item)
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
        - name: "first"
          value:
            type: ":Boolean"
            value: false
        - name: "second"
          value:
            type: ":Boolean"
            value: true
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
              - type: ":Number.int64"
                value: "2"
    local:
      - name: "Two"
        args: []
      - name: "Item"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      - name: "Item"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "2"
```

---

## Test: threshold check publishes matching branch

This runtime case exercises “threshold check publishes matching branch” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0008
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "threshold check publishes matching branch.ges"
    program: main
```

### Source code under test

```ges
on Start(values, threshold) {
  let passed be values[:any value where value > threshold]
  if passed {
    emit Passed(count: values[:count])
  } else {
    emit Failed
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
      args:
        - name: "values"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
              - type: ":Number.int64"
                value: "5"
              - type: ":Number.int64"
                value: "9"
        - name: "threshold"
          value:
            type: ":Number.int64"
            value: "7"
    local:
      - name: "Passed"
        args:
          - name: "count"
            value:
              type: ":Number.int64"
              value: "3"
```

---

## Test: else runs when condition is not true

This runtime case exercises “else runs when condition is not true” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0009
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "else runs when condition is not true.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let unknown be nothing > 0
  if unknown {
    emit TrueBranch
  } else {
    emit FalseBranch
  }

  let explicitFalse be unknown as :Boolean
  if explicitFalse {
    emit ExplicitTrue
  } else {
    emit ExplicitFalse
  }

  emit Done(unknown: unknown)
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
      - name: "FalseBranch"
        args: []
      - name: "ExplicitFalse"
        args: []
      - name: "Done"
        args:
          - name: "unknown"
            value:
              type: ":Nothing"
```

---

## Test: logical operators use tri state truth tables

This runtime case exercises “logical operators use tri state truth tables” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0010
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "logical operators use tri state truth tables.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Done(notUnknown: not nothing, falseAndUnknown: false and nothing, unknownAndFalse: nothing and false, trueAndUnknown: true and nothing, unknownAndTrue: nothing and true, unknownAndUnknown: nothing and nothing, trueOrUnknown: true or nothing, unknownOrTrue: nothing or true, falseOrUnknown: false or nothing, unknownOrFalse: nothing or false, unknownOrUnknown: nothing or nothing, trueXorFalse: true xor false, trueXorTrue: true xor true, unknownXorTrue: nothing xor true, unknownXorUnknown: nothing xor nothing)
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
          - name: "notUnknown"
            value:
              type: ":Nothing"
          - name: "falseAndUnknown"
            value:
              type: ":Boolean"
              value: false
          - name: "unknownAndFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "trueAndUnknown"
            value:
              type: ":Nothing"
          - name: "unknownAndTrue"
            value:
              type: ":Nothing"
          - name: "unknownAndUnknown"
            value:
              type: ":Nothing"
          - name: "trueOrUnknown"
            value:
              type: ":Boolean"
              value: true
          - name: "unknownOrTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "falseOrUnknown"
            value:
              type: ":Nothing"
          - name: "unknownOrFalse"
            value:
              type: ":Nothing"
          - name: "unknownOrUnknown"
            value:
              type: ":Nothing"
          - name: "trueXorFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "trueXorTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "unknownXorTrue"
            value:
              type: ":Nothing"
          - name: "unknownXorUnknown"
            value:
              type: ":Nothing"
```

---

## Test: and and or short circuit right hand expressions

This runtime case exercises “and and or short circuit right hand expressions” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0011
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and and or short circuit right hand expressions.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let skippedAnd be false and :test.fail()
  let skippedOr be true or :test.fail()
  let evaluatedAnd be true and :test.truth()
  let evaluatedOr be false or :test.truth()
  emit Done(skippedAnd: skippedAnd, skippedOr: skippedOr, evaluatedAnd: evaluatedAnd, evaluatedOr: evaluatedOr)
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
          - name: "skippedAnd"
            value:
              type: ":Boolean"
              value: false
          - name: "skippedOr"
            value:
              type: ":Boolean"
              value: true
          - name: "evaluatedAnd"
            value:
              type: ":Boolean"
              value: true
          - name: "evaluatedOr"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: implication is right associative tri state and short circuiting

This runtime case exercises “implication is right associative tri state and short circuiting” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0012
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implication is right associative tri state and short circuiting.ges"
    program: main
```

### Source code under test

```ges
function handleDamage(_ wasHit, _ absorbed, _ applied) be wasHit -> !absorbed -> applied

on Start {
  let skipped be false -> :test.fail()
  let unicodeSkipped be false⇒:test.fail()
  let unicodeEvaluated be true⇒true
  let unicodeSingleSkipped be false→:test.fail()
  let unicodeSingleEvaluated be true→true
  let rightAssoc be false -> false -> :test.fail()
  let absorbedChain be true -> !true -> :test.fail()
  let notHit be handleDamage(false, false, false)
  let absorbed be handleDamage(true, true, false)
  let applied be handleDamage(true, false, true)
  let failed be handleDamage(true, false, false)
  let unknownResolved be nothing -> true
  let unknownBlocked be nothing -> false
  emit Done(skipped: skipped, unicodeSkipped: unicodeSkipped, unicodeEvaluated: unicodeEvaluated, unicodeSingleSkipped: unicodeSingleSkipped, unicodeSingleEvaluated: unicodeSingleEvaluated, rightAssoc: rightAssoc, absorbedChain: absorbedChain, notHit: notHit, absorbed: absorbed, applied: applied, failed: failed, unknownResolved: unknownResolved, unknownBlocked: unknownBlocked)
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
          - name: "skipped"
            value:
              type: ":Boolean"
              value: true
          - name: "unicodeSkipped"
            value:
              type: ":Boolean"
              value: true
          - name: "unicodeEvaluated"
            value:
              type: ":Boolean"
              value: true
          - name: "unicodeSingleSkipped"
            value:
              type: ":Boolean"
              value: true
          - name: "unicodeSingleEvaluated"
            value:
              type: ":Boolean"
              value: true
          - name: "rightAssoc"
            value:
              type: ":Boolean"
              value: true
          - name: "absorbedChain"
            value:
              type: ":Boolean"
              value: true
          - name: "notHit"
            value:
              type: ":Boolean"
              value: true
          - name: "absorbed"
            value:
              type: ":Boolean"
              value: true
          - name: "applied"
            value:
              type: ":Boolean"
              value: true
          - name: "failed"
            value:
              type: ":Boolean"
              value: false
          - name: "unknownResolved"
            value:
              type: ":Boolean"
              value: true
          - name: "unknownBlocked"
            value:
              type: ":Nothing"
```

---

## Test: sibling branch scopes may reuse local names

This runtime case verifies that the mutually exclusive child scopes of an `if` and its `else` branch may independently declare the same local name without either declaration shadowing the other.

### Case description

```yaml
gesBlock: case
id: case-0013
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "sibling branch scopes may reuse local names.ges"
    program: main
```

### Source code under test

```ges
module siblingbranchscopes
on Start(flag) {
  if flag {
    let result be 20
    emit Done(value: result)
  } else {
    let result be 10
    emit Done(value: result)
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
      args:
        - name: "flag"
          value:
            type: ":Boolean"
            value: true
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "20"
```

---

## Test: incompatible conditions and loops are lenient

This runtime case exercises “incompatible conditions and loops are lenient” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0014
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "incompatible conditions and loops are lenient.ges"
    program: main
```

### Source code under test

```ges
on Start {
  if 'abc' {
    emit IfTrue
  } else {
    emit IfFalse
  }

  for item in 123 {
    emit Loop(item: item)
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
      - name: "IfFalse"
        args: []
```

---

## Test: sql style inequality changes branching

This runtime case exercises “sql style inequality changes branching” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0015
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "sql style inequality changes branching.ges"
    program: main
```

### Source code under test

```ges
on Start {
  if 1 <> 2 {
    emit Different
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
      - name: "Different"
        args: []
```

---

## Test: text iteration yields single character items

This runtime case exercises “text iteration yields single character items” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0016
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "text iteration yields single character items.ges"
    program: main
```

### Source code under test

```ges
on Start(text) {
  for item in text {
    if item = 'a' {
      emit Found(value: item)
    }
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
      args:
        - name: "text"
          value:
            type: ":Text"
            value: "ab"
    local:
      - name: "Found"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "a"
```

---

## Test: between-inclusive-boundaries

This case verifies inclusive between semantics, parsing, or observable evaluation order.

### Case description

```yaml
gesBlock: case
id: between-inclusive-boundaries
kind: scriptApi
level: scenario
```

### Source code under test

```ges
on Start {
  emit Done(low: 10 is between 10 and 20, middle: 15 is between 10 and 20,
    high: 20 is between 10 and 20, below: 9 is between 10 and 20,
    above: 21 is between 10 and 20, equal: 10 is between 10 and 10,
    reversed: 15 is between 20 and 10, negated: 21 is not between 10 and 20,
    negatedInside: 15 is not between 10 and 20)
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
    local:
      - name: Done
        args:
          - name: low
            value:
              type: ":Boolean"
              value: true
          - name: middle
            value:
              type: ":Boolean"
              value: true
          - name: high
            value:
              type: ":Boolean"
              value: true
          - name: below
            value:
              type: ":Boolean"
              value: false
          - name: above
            value:
              type: ":Boolean"
              value: false
          - name: equal
            value:
              type: ":Boolean"
              value: true
          - name: reversed
            value:
              type: ":Boolean"
              value: false
          - name: negated
            value:
              type: ":Boolean"
              value: true
          - name: negatedInside
            value:
              type: ":Boolean"
              value: false
```

---

## Test: between-precedence-and-context

This case verifies inclusive between semantics, parsing, or observable evaluation order.

### Case description

```yaml
gesBlock: case
id: between-precedence-and-context
kind: scriptApi
level: scenario
```

### Source code under test

```ges
predicate bounded(value) be value is between 1 + 2 and 3 * 4
function between(value) be value
record :Sample as { value: :Boolean computed by 5 is between 1 and 10 }
on Start {
  let low be 10
  let high be 20
  let selected be [5, 10, 15, 20, 25][:filter item where item is between low and high][:count]
  let multi be 15 is between
    low and
    high
  emit Done(andTail: 15 is between low and high and false,
    orTail: 25 is between low and high or true,
    arithmetic: bounded(value: 12), filtered: selected, multiline: multi,
    contextual: between(value: 7), computed: (:Sample()).value)
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
    local:
      - name: Done
        args:
          - name: andTail
            value:
              type: ":Boolean"
              value: false
          - name: orTail
            value:
              type: ":Boolean"
              value: true
          - name: arithmetic
            value:
              type: ":Boolean"
              value: true
          - name: filtered
            value:
              type: ":Number.int64"
              value: "3"
          - name: multiline
            value:
              type: ":Boolean"
              value: true
          - name: contextual
            value:
              type: ":Number.int64"
              value: "7"
          - name: computed
            value:
              type: ":Boolean"
              value: true
```

---

## Test: between-comparison-semantics

This case verifies inclusive between semantics, parsing, or observable evaluation order.

### Case description

```yaml
gesBlock: case
id: between-comparison-semantics
kind: scriptApi
level: scenario
```

### Source code under test

```ges
on Start {
  emit Done(unit: 15m is between 10m and 20m,
    fraction: 1.5 is between 1 and 2,
    absent: nothing is between 1 and 2,
    absentNegated: nothing is not between 1 and 2,
    absentLower: 15 is between nothing and 20,
    absentUpper: 15 is between 10 and nothing,
    falseLower: 5 is between 10 and nothing)
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
    local:
      - name: Done
        args:
          - name: unit
            value:
              type: ":Boolean"
              value: true
          - name: fraction
            value:
              type: ":Boolean"
              value: true
          - name: absent
            value:
              type: ":Nothing"
          - name: absentNegated
            value:
              type: ":Nothing"
          - name: absentLower
            value:
              type: ":Nothing"
          - name: absentUpper
            value:
              type: ":Nothing"
          - name: falseLower
            value:
              type: ":Boolean"
              value: false
```

---

## Test: between-evaluates-value-once-and-bounds-in-order

This case verifies inclusive between semantics, parsing, or observable evaluation order.

### Case description

```yaml
gesBlock: case
id: between-evaluates-value-once-and-bounds-in-order
kind: scriptApi
level: scenario
random:
  sequence: ["50", "10", "90", "77"]
```

### Source code under test

```ges
function draw() be random from 1 to 100
on Start {
  let result be draw() is between draw() and draw()
  emit Done(result: result, next: draw())
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
    local:
      - name: Done
        args:
          - name: result
            value:
              type: ":Boolean"
              value: true
          - name: next
            value:
              type: ":Number.int64"
              value: "77"
```

---

## Test: between-short-circuits-upper-bound

This case verifies inclusive between semantics, parsing, or observable evaluation order.

### Case description

```yaml
gesBlock: case
id: between-short-circuits-upper-bound
kind: scriptApi
level: scenario
random:
  sequence: ["5", "10", "77"]
```

### Source code under test

```ges
function draw() be random from 1 to 100
on Start {
  let result be draw() is not between draw() and :test.failWithRandomScope()
  emit Done(result: result, next: draw())
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
    local:
      - name: Done
        args:
          - name: result
            value:
              type: ":Boolean"
              value: true
          - name: next
            value:
              type: ":Number.int64"
              value: "77"
```

---

## Test: between-nothing-does-not-skip-upper-bound

This case verifies inclusive between semantics, parsing, or observable evaluation order.

### Case description

```yaml
gesBlock: case
id: between-nothing-does-not-skip-upper-bound
kind: scriptApi
level: scenario
random:
  sequence: ["10", "90", "77"]
```

### Source code under test

```ges
function draw() be random from 1 to 100
on Start {
  let result be nothing is between draw() and draw()
  emit Done(result: result, next: draw())
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
    local:
      - name: Done
        args:
          - name: result
            value:
              type: ":Nothing"
          - name: next
            value:
              type: ":Number.int64"
              value: "77"
```
