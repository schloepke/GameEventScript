---
formatVersion: 1
suiteId: "runtime.atomic.shuffle-reverse"
title: "RuntimeAtomicShuffleReverse"
categories: [conformance]
tags: [migrated-json-v1]
---

# RuntimeAtomicShuffleReverse

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite isolates deterministic shuffle and reverse transformations for supported collections.

---

## Test: reverse direct source kinds

This runtime case exercises “reverse direct source kinds” and verifies the declared messages, values, and execution result.

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
  - name: "reverse direct source kinds.ges"
    program: main
```

### Source code under test

```ges
module AtomicReverseDirectKinds
on Start {
  let vNothing be nothing
  let vBoolean be true
  let vInteger be 10
  let vFloat be 10.5
  let vPercentage be 25%
  let vText be 'abc'
  let vTag be #abc
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(4, 5, 6)
  let vList be [1, 2, 3]
  let vEmptyList be []
  let vMap be [a: 1, b: 2]
  let vDice as :dice be [6, 4, 2]
  let vEmptyDice as :dice be []
  let vRange as :range be from 1 to 3
  let vFloatRange as :range be from 1.5 to 2.5 step 0.5
  emit Done(nothingValue: vNothing[:reverse], booleanValue: vBoolean[:reverse], integerValue: vInteger[:reverse], floatValue: vFloat[:reverse], percentageValue: vPercentage[:reverse], textValue: vText[:reverse], tagValue: vTag[:reverse], vectorValue: vVector[:reverse], pointValue: vPoint[:reverse], listValue: vList[:reverse], emptyListValue: vEmptyList[:reverse], mapValue: vMap[:reverse], diceValue: vDice[:reverse], emptyDiceValue: vEmptyDice[:reverse], rangeValue: vRange[:reverse], floatRangeValue: vFloatRange[:reverse])
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
          - name: "nothingValue"
            value:
              type: ":nothing"
          - name: "booleanValue"
            value:
              type: ":nothing"
          - name: "integerValue"
            value:
              type: ":nothing"
          - name: "floatValue"
            value:
              type: ":nothing"
          - name: "percentageValue"
            value:
              type: ":nothing"
          - name: "textValue"
            value:
              type: ":nothing"
          - name: "tagValue"
            value:
              type: ":nothing"
          - name: "vectorValue"
            value:
              type: ":nothing"
          - name: "pointValue"
            value:
              type: ":nothing"
          - name: "listValue"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "1"
          - name: "emptyListValue"
            value:
              type: ":list"
              items: []
          - name: "mapValue"
            value:
              type: ":nothing"
          - name: "diceValue"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "4"
                - type: ":integer"
                  value: "6"
          - name: "emptyDiceValue"
            value:
              type: ":list"
              items: []
          - name: "rangeValue"
            value:
              type: ":range"
              from: "3"
              to: "1"
              step: "-1"
          - name: "floatRangeValue"
            value:
              type: ":range"
              from: "2.5"
              to: "1.5"
              step: "-0.5"
```

---

## Test: shuffle direct source kinds

This runtime case exercises “shuffle direct source kinds” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["0", "0", "0", "0", "0", "0", "0", "0"]
sources:
  - name: "shuffle direct source kinds.ges"
    program: main
```

### Source code under test

```ges
module AtomicShuffleDirectKinds
on Start {
  let vNothing be nothing
  let vBoolean be true
  let vInteger be 10
  let vFloat be 10.5
  let vPercentage be 25%
  let vText be 'abc'
  let vTag be #abc
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(4, 5, 6)
  let vList be [1, 2, 3]
  let vEmptyList be []
  let vMap be [a: 1, b: 2]
  let vDice as :dice be [6, 4, 2]
  let vEmptyDice as :dice be []
  let vRange as :range be from 1 to 3
  let vFloatRange as :range be from 1.5 to 2.5 step 0.5
  emit Done(nothingValue: vNothing[:shuffle], booleanValue: vBoolean[:shuffle], integerValue: vInteger[:shuffle], floatValue: vFloat[:shuffle], percentageValue: vPercentage[:shuffle], textValue: vText[:shuffle], tagValue: vTag[:shuffle], vectorValue: vVector[:shuffle], pointValue: vPoint[:shuffle], listValue: vList[:shuffle], emptyListValue: vEmptyList[:shuffle], mapValue: vMap[:shuffle], diceValue: vDice[:shuffle], emptyDiceValue: vEmptyDice[:shuffle], rangeValue: vRange[:shuffle], floatRangeValue: vFloatRange[:shuffle])
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
          - name: "nothingValue"
            value:
              type: ":nothing"
          - name: "booleanValue"
            value:
              type: ":nothing"
          - name: "integerValue"
            value:
              type: ":nothing"
          - name: "floatValue"
            value:
              type: ":nothing"
          - name: "percentageValue"
            value:
              type: ":nothing"
          - name: "textValue"
            value:
              type: ":nothing"
          - name: "tagValue"
            value:
              type: ":nothing"
          - name: "vectorValue"
            value:
              type: ":nothing"
          - name: "pointValue"
            value:
              type: ":nothing"
          - name: "listValue"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "1"
          - name: "emptyListValue"
            value:
              type: ":list"
              items: []
          - name: "mapValue"
            value:
              type: ":nothing"
          - name: "diceValue"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "4"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "6"
          - name: "emptyDiceValue"
            value:
              type: ":list"
              items: []
          - name: "rangeValue"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "1"
          - name: "floatRangeValue"
            value:
              type: ":list"
              items:
                - type: ":float"
                  value: "2"
                - type: ":float"
                  value: "2.5"
                - type: ":float"
                  value: "1.5"
```

---

## Test: reverse and shuffle transformed streams

This runtime case exercises “reverse and shuffle transformed streams” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["0", "0"]
sources:
  - name: "reverse and shuffle transformed streams.ges"
    program: main
```

### Source code under test

```ges
module AtomicShuffleReverseStreams
on Start {
  let values be [1, 2, 3]
  let reversedStream be values[:select value => value * 10][:reverse]
  let shuffledStream be values[:select value => value * 10][:shuffle]
  emit Done(reversedStream: reversedStream, shuffledStream: shuffledStream)
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
          - name: "reversedStream"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "30"
                - type: ":integer"
                  value: "20"
                - type: ":integer"
                  value: "10"
          - name: "shuffledStream"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "20"
                - type: ":integer"
                  value: "30"
                - type: ":integer"
                  value: "10"
```
