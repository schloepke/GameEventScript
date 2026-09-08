---
formatVersion: 1
suiteId: "runtime.atomic.shuffle-reverse"
title: "RuntimeAtomicShuffleReverse"
categories: [conformance]
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
module atomicreversedirectkinds
on Start {
  let vNothing be nothing
  let vBoolean be true
  let vInteger be 10
  let vFloat be 10.5
  let vPercentage be 25%
  let vText be 'abc'
  let vTag be #abc
  let vVector be :Vector(1, 2, 3)
  let vPoint be :Point(4, 5, 6)
  let vList be [1, 2, 3]
  let vEmptyList be []
  let vMap be [a: 1, b: 2]
  let vDice be ([6, 4, 2]) as :Dice
  let vEmptyDice be ([]) as :Dice
  let vRange be (from 1 to 3) as :Range
  let vFloatRange be (from 1.5 to 2.5 step 0.5) as :Range
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
              type: ":Nothing"
          - name: "booleanValue"
            value:
              type: ":Nothing"
          - name: "integerValue"
            value:
              type: ":Nothing"
          - name: "floatValue"
            value:
              type: ":Nothing"
          - name: "percentageValue"
            value:
              type: ":Nothing"
          - name: "textValue"
            value:
              type: ":Nothing"
          - name: "tagValue"
            value:
              type: ":Nothing"
          - name: "vectorValue"
            value:
              type: ":Nothing"
          - name: "pointValue"
            value:
              type: ":Nothing"
          - name: "listValue"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "3"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "1"
          - name: "emptyListValue"
            value:
              type: ":List"
              items: []
          - name: "mapValue"
            value:
              type: ":Nothing"
          - name: "diceValue"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "4"
                - type: ":Number.int64"
                  value: "6"
          - name: "emptyDiceValue"
            value:
              type: ":List"
              items: []
          - name: "rangeValue"
            value:
              type: ":Range.int64"
              from: "3"
              to: "1"
              step: "-1"
          - name: "floatRangeValue"
            value:
              type: ":Range.binary64"
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
module atomicshuffledirectkinds
on Start {
  let vNothing be nothing
  let vBoolean be true
  let vInteger be 10
  let vFloat be 10.5
  let vPercentage be 25%
  let vText be 'abc'
  let vTag be #abc
  let vVector be :Vector(1, 2, 3)
  let vPoint be :Point(4, 5, 6)
  let vList be [1, 2, 3]
  let vEmptyList be []
  let vMap be [a: 1, b: 2]
  let vDice be ([6, 4, 2]) as :Dice
  let vEmptyDice be ([]) as :Dice
  let vRange be (from 1 to 3) as :Range
  let vFloatRange be (from 1.5 to 2.5 step 0.5) as :Range
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
              type: ":Nothing"
          - name: "booleanValue"
            value:
              type: ":Nothing"
          - name: "integerValue"
            value:
              type: ":Nothing"
          - name: "floatValue"
            value:
              type: ":Nothing"
          - name: "percentageValue"
            value:
              type: ":Nothing"
          - name: "textValue"
            value:
              type: ":Nothing"
          - name: "tagValue"
            value:
              type: ":Nothing"
          - name: "vectorValue"
            value:
              type: ":Nothing"
          - name: "pointValue"
            value:
              type: ":Nothing"
          - name: "listValue"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
                - type: ":Number.int64"
                  value: "1"
          - name: "emptyListValue"
            value:
              type: ":List"
              items: []
          - name: "mapValue"
            value:
              type: ":Nothing"
          - name: "diceValue"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "4"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "6"
          - name: "emptyDiceValue"
            value:
              type: ":List"
              items: []
          - name: "rangeValue"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
                - type: ":Number.int64"
                  value: "1"
          - name: "floatRangeValue"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.binary64"
                  value: "2.5"
                - type: ":Number.binary64"
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
module atomicshufflereversestreams
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
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "30"
                - type: ":Number.int64"
                  value: "20"
                - type: ":Number.int64"
                  value: "10"
          - name: "shuffledStream"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "20"
                - type: ":Number.int64"
                  value: "30"
                - type: ":Number.int64"
                  value: "10"
```
