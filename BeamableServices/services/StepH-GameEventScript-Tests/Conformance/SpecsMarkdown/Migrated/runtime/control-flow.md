---
formatVersion: 1
suiteId: "runtime.control-flow"
title: "RuntimeControlFlow"
categories: [conformance]
tags: [migrated-json-v1]
---

# RuntimeControlFlow

Mechanically migrated from the former JSON conformance corpus.

## Test: simple emit

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

```ges
on Start {
  emit Done
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

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

## Test: standalone and trailing comments are ignored during execution

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

```ges
module CommentRuntime
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
              value: "10"
```

## Test: semicolons and continued expressions preserve behavior

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
              type: ":integer"
              value: "10"
          - name: "values"
            value:
              type: ":integer"
              value: "20"
          - name: "result"
            value:
              type: ":integer"
              value: "3"
```

## Test: published messages are dispatched through the same host queue

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

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "value"
          value:
            type: ":float"
            value: "2"
    local:
      - name: "Next"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "3"
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "3"
```

## Test: processing limit stops event loops

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

## Test: queue limit drops newest published messages

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

## Test: if and for single statements run without blocks

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

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "first"
          value:
            type: ":boolean"
            value: false
        - name: "second"
          value:
            type: ":boolean"
            value: true
        - name: "items"
          value:
            type: ":list"
            items:
              - type: ":integer"
                value: "1"
              - type: ":integer"
                value: "2"
    local:
      - name: "Two"
        args: []
      - name: "Item"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "1"
      - name: "Item"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "2"
```

## Test: threshold check publishes matching branch

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

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "values"
          value:
            type: ":list"
            items:
              - type: ":integer"
                value: "1"
              - type: ":integer"
                value: "5"
              - type: ":integer"
                value: "9"
        - name: "threshold"
          value:
            type: ":integer"
            value: "7"
    local:
      - name: "Passed"
        args:
          - name: "count"
            value:
              type: ":integer"
              value: "3"
```

## Test: else runs when condition is not true

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

```ges
on Start {
  let unknown be nothing > 0
  if unknown {
    emit TrueBranch
  } else {
    emit FalseBranch
  }

  let explicitFalse be unknown as :boolean
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
              type: ":nothing"
```

## Test: logical operators use tri state truth tables

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

```ges
on Start {
  emit Done(notUnknown: not nothing, falseAndUnknown: false and nothing, unknownAndFalse: nothing and false, trueAndUnknown: true and nothing, unknownAndTrue: nothing and true, unknownAndUnknown: nothing and nothing, trueOrUnknown: true or nothing, unknownOrTrue: nothing or true, falseOrUnknown: false or nothing, unknownOrFalse: nothing or false, unknownOrUnknown: nothing or nothing, trueXorFalse: true xor false, trueXorTrue: true xor true, unknownXorTrue: nothing xor true, unknownXorUnknown: nothing xor nothing)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

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
              type: ":nothing"
          - name: "falseAndUnknown"
            value:
              type: ":boolean"
              value: false
          - name: "unknownAndFalse"
            value:
              type: ":boolean"
              value: false
          - name: "trueAndUnknown"
            value:
              type: ":nothing"
          - name: "unknownAndTrue"
            value:
              type: ":nothing"
          - name: "unknownAndUnknown"
            value:
              type: ":nothing"
          - name: "trueOrUnknown"
            value:
              type: ":boolean"
              value: true
          - name: "unknownOrTrue"
            value:
              type: ":boolean"
              value: true
          - name: "falseOrUnknown"
            value:
              type: ":nothing"
          - name: "unknownOrFalse"
            value:
              type: ":nothing"
          - name: "unknownOrUnknown"
            value:
              type: ":nothing"
          - name: "trueXorFalse"
            value:
              type: ":boolean"
              value: true
          - name: "trueXorTrue"
            value:
              type: ":boolean"
              value: false
          - name: "unknownXorTrue"
            value:
              type: ":nothing"
          - name: "unknownXorUnknown"
            value:
              type: ":nothing"
```

## Test: and and or short circuit right hand expressions

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
              type: ":boolean"
              value: false
          - name: "skippedOr"
            value:
              type: ":boolean"
              value: true
          - name: "evaluatedAnd"
            value:
              type: ":boolean"
              value: true
          - name: "evaluatedOr"
            value:
              type: ":boolean"
              value: true
```

## Test: implication is right associative tri state and short circuiting

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
              type: ":boolean"
              value: true
          - name: "unicodeSkipped"
            value:
              type: ":boolean"
              value: true
          - name: "unicodeEvaluated"
            value:
              type: ":boolean"
              value: true
          - name: "unicodeSingleSkipped"
            value:
              type: ":boolean"
              value: true
          - name: "unicodeSingleEvaluated"
            value:
              type: ":boolean"
              value: true
          - name: "rightAssoc"
            value:
              type: ":boolean"
              value: true
          - name: "absorbedChain"
            value:
              type: ":boolean"
              value: true
          - name: "notHit"
            value:
              type: ":boolean"
              value: true
          - name: "absorbed"
            value:
              type: ":boolean"
              value: true
          - name: "applied"
            value:
              type: ":boolean"
              value: true
          - name: "failed"
            value:
              type: ":boolean"
              value: false
          - name: "unknownResolved"
            value:
              type: ":boolean"
              value: true
          - name: "unknownBlocked"
            value:
              type: ":nothing"
```

## Test: nested block scopes may shadow outer variables

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
  - name: "nested block scopes may shadow outer variables.ges"
    program: main
```

```ges
module NestedShadowing
on Start {
  let x be 10
  if true {
    let x be 20
    emit Done(value: x)
  }
  emit Done(value: x)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

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
              value: "20"
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "10"
```

## Test: incompatible conditions and loops are lenient

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

## Test: sql style inequality changes branching

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

## Test: text iteration yields single character items

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

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "text"
          value:
            type: ":text"
            value: "ab"
    local:
      - name: "Found"
        args:
          - name: "value"
            value:
              type: ":text"
              value: "a"
```
