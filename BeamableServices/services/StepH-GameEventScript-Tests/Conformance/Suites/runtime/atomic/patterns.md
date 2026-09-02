---
formatVersion: 1
suiteId: "runtime.atomic.patterns"
title: "RuntimeAtomicPatterns"
categories: [conformance]
tags: [migrated-json-v1]
---

# RuntimeAtomicPatterns

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite isolates pattern matching, guarded alternatives, and binding behavior.

---

## Test: has pattern direct source kinds

This runtime case exercises “has pattern direct source kinds” and verifies the declared messages, values, and execution result.

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
  - name: "has pattern direct source kinds.ges"
    program: main
```

### Source code under test

```ges
module AtomicHasPatternDirectKinds
on Start {
  let vNothing be nothing
  let vBoolean be true
  let vInteger be 10
  let vFloat be 10.5
  let vPercentage be 25%
  let vText be 'aabb'
  let vTag be #aabb
  let vVector be :vector(1, 1, 2)
  let vPoint be :point(1, 1, 2)
  let vList be [3, 1, 3, 2]
  let vListText be ['a', 'b', 'a']
  let vListTag be [#fire, #ice, #fire]
  let vMap be [a: 1, b: 1]
  let vDice as :dice be [6, 6, 5, 4]
  let vDiceNoPair as :dice be [6, 5, 4, 3]
  let vRange as :range be from 1 to 4
  emit Done(nothingValue: vNothing[:has pair], booleanValue: vBoolean[:has pair], integerValue: vInteger[:has pair], floatValue: vFloat[:has pair], percentageValue: vPercentage[:has pair], textValue: vText[:has pair], tagValue: vTag[:has pair], vectorValue: vVector[:has pair], pointValue: vPoint[:has pair], listPair: vList[:has pair], listPairOfThree: vList[:has pair of 3], listPairOfFour: vList[:has pair of 4], listTextPair: vListText[:has pair of 'a'], listTextMissing: vListText[:has pair of 'c'], listTagPair: vListTag[:has pair of #fire], mapValue: vMap[:has pair], dicePair: vDice[:has pair], dicePairOfSix: vDice[:has pair of 6], dicePairOfFive: vDice[:has pair of 5], diceNoPair: vDiceNoPair[:has pair], rangeValue: vRange[:has pair])
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
              type: ":boolean"
              value: false
          - name: "booleanValue"
            value:
              type: ":boolean"
              value: false
          - name: "integerValue"
            value:
              type: ":boolean"
              value: false
          - name: "floatValue"
            value:
              type: ":boolean"
              value: false
          - name: "percentageValue"
            value:
              type: ":boolean"
              value: false
          - name: "textValue"
            value:
              type: ":boolean"
              value: false
          - name: "tagValue"
            value:
              type: ":boolean"
              value: false
          - name: "vectorValue"
            value:
              type: ":boolean"
              value: false
          - name: "pointValue"
            value:
              type: ":boolean"
              value: false
          - name: "listPair"
            value:
              type: ":boolean"
              value: true
          - name: "listPairOfThree"
            value:
              type: ":boolean"
              value: true
          - name: "listPairOfFour"
            value:
              type: ":boolean"
              value: false
          - name: "listTextPair"
            value:
              type: ":boolean"
              value: true
          - name: "listTextMissing"
            value:
              type: ":boolean"
              value: false
          - name: "listTagPair"
            value:
              type: ":boolean"
              value: true
          - name: "mapValue"
            value:
              type: ":boolean"
              value: false
          - name: "dicePair"
            value:
              type: ":boolean"
              value: true
          - name: "dicePairOfSix"
            value:
              type: ":boolean"
              value: true
          - name: "dicePairOfFive"
            value:
              type: ":boolean"
              value: false
          - name: "diceNoPair"
            value:
              type: ":boolean"
              value: false
          - name: "rangeValue"
            value:
              type: ":boolean"
              value: false
```

---

## Test: take pattern count direct source kinds

This runtime case exercises “take pattern count direct source kinds” and verifies the declared messages, values, and execution result.

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
  - name: "take pattern count direct source kinds.ges"
    program: main
```

### Source code under test

```ges
module AtomicTakePatternCountKinds
on Start {
  let vNothing be nothing
  let vInteger be 10
  let vText be 'aabb'
  let vMap be [a: 1, b: 1]
  let vRange as :range be from 1 to 4
  let vList be [3, 1, 3, 2, 3]
  let vListText be ['a', 'b', 'a']
  let vListTag be [#fire, #ice, #fire]
  let vDice as :dice be [6, 6, 5, 5, 5]
  let vDiceNoTriple as :dice be [6, 6, 5, 4]
  let listPair be vList[:take pair]
  let listThree be vList[:take three of a kind]
  let listPairOfThree be vList[:take pair of 3]
  let textPair be vListText[:take pair of 'a']
  let tagPair be vListTag[:take pair of #fire]
  let dicePair be vDice[:take pair]
  let diceThree be vDice[:take three of a kind]
  let dicePairOfSix be vDice[:take pair of 6]
  emit Done(nothingValue: vNothing[:take pair], integerValue: vInteger[:take pair], textValue: vText[:take pair], mapValue: vMap[:take pair], rangeValue: vRange[:take pair], listPairLen: listPair[:count], listPairFirst: listPair[1], listPairSecond: listPair[2], listThreeLen: listThree[:count], listThreeFirst: listThree[1], listThreeThird: listThree[3], listPairOfThreeLen: listPairOfThree[:count], listPairOfThreeFirst: listPairOfThree[1], listPairOfFour: vList[:take pair of 4], textPairLen: textPair[:count], textPairFirst: textPair[1], textPairSecond: textPair[2], tagPairLen: tagPair[:count], tagPairFirst: tagPair[1], tagPairSecond: tagPair[2], dicePair: dicePair, diceThree: diceThree, dicePairOfSix: dicePairOfSix, diceNoTriple: vDiceNoTriple[:take three of a kind])
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
          - name: "integerValue"
            value:
              type: ":nothing"
          - name: "textValue"
            value:
              type: ":nothing"
          - name: "mapValue"
            value:
              type: ":nothing"
          - name: "rangeValue"
            value:
              type: ":nothing"
          - name: "listPairLen"
            value:
              type: ":integer"
              value: "2"
          - name: "listPairFirst"
            value:
              type: ":integer"
              value: "3"
          - name: "listPairSecond"
            value:
              type: ":integer"
              value: "3"
          - name: "listThreeLen"
            value:
              type: ":integer"
              value: "3"
          - name: "listThreeFirst"
            value:
              type: ":integer"
              value: "3"
          - name: "listThreeThird"
            value:
              type: ":integer"
              value: "3"
          - name: "listPairOfThreeLen"
            value:
              type: ":integer"
              value: "2"
          - name: "listPairOfThreeFirst"
            value:
              type: ":integer"
              value: "3"
          - name: "listPairOfFour"
            value:
              type: ":nothing"
          - name: "textPairLen"
            value:
              type: ":integer"
              value: "2"
          - name: "textPairFirst"
            value:
              type: ":text"
              value: "a"
          - name: "textPairSecond"
            value:
              type: ":text"
              value: "a"
          - name: "tagPairLen"
            value:
              type: ":integer"
              value: "2"
          - name: "tagPairFirst"
            value:
              type: ":tag"
              value: "fire"
          - name: "tagPairSecond"
            value:
              type: ":tag"
              value: "fire"
          - name: "dicePair"
            value:
              type: ":dice"
              rolls:
                - 6
                - 6
          - name: "diceThree"
            value:
              type: ":dice"
              rolls:
                - 5
                - 5
                - 5
          - name: "dicePairOfSix"
            value:
              type: ":dice"
              rolls:
                - 6
                - 6
          - name: "diceNoTriple"
            value:
              type: ":nothing"
```

---

## Test: full house and straight patterns

This runtime case exercises “full house and straight patterns” and verifies the declared messages, values, and execution result.

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
  - name: "full house and straight patterns.ges"
    program: main
```

### Source code under test

```ges
module AtomicFullHouseStraightPatterns
on Start {
  let diceFull as :dice be [6, 6, 6, 5, 5]
  let diceNotFull as :dice be [6, 6, 5, 5, 4]
  let diceStraight as :dice be [6, 5, 4, 3, 2]
  let diceNotStraight as :dice be [6, 5, 4, 2, 1]
  let listFull be ['a', 'b', 'a', 'b', 'a']
  let listNotFull be ['a', 'b', 'a', 'b', 'c']
  let listStraight be [3, 1, 2]
  let listNotStraight be [3, 1, 5]
  let diceFullTaken be diceFull[:take full house]
  let diceStraightTaken be diceStraight[:take straight]
  let listFullTaken be listFull[:take full house]
  let listStraightTaken be listStraight[:take straight]
  emit Done(diceFullHas: diceFull[:has full house], diceNotFullHas: diceNotFull[:has full house], diceStraightHas: diceStraight[:has straight], diceNotStraightHas: diceNotStraight[:has straight], listFullHas: listFull[:has full house], listNotFullHas: listNotFull[:has full house], listStraightHas: listStraight[:has straight], listNotStraightHas: listNotStraight[:has straight], diceFullTaken: diceFullTaken, diceFullMissing: diceNotFull[:take full house], diceStraightTaken: diceStraightTaken, diceStraightMissing: diceNotStraight[:take straight], listFullLen: listFull[:count], listFull_1: listFullTaken[1], listFull_3: listFullTaken[3], listFull_4: listFullTaken[4], listStraightLen: listStraight[:count], listStraight_1: listStraightTaken[1], listStraight_2: listStraightTaken[2], listStraight_3: listStraightTaken[3], listStraightMissing: listNotStraight[:take straight])
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
          - name: "diceFullHas"
            value:
              type: ":boolean"
              value: true
          - name: "diceNotFullHas"
            value:
              type: ":boolean"
              value: false
          - name: "diceStraightHas"
            value:
              type: ":boolean"
              value: true
          - name: "diceNotStraightHas"
            value:
              type: ":boolean"
              value: false
          - name: "listFullHas"
            value:
              type: ":boolean"
              value: true
          - name: "listNotFullHas"
            value:
              type: ":boolean"
              value: false
          - name: "listStraightHas"
            value:
              type: ":boolean"
              value: true
          - name: "listNotStraightHas"
            value:
              type: ":boolean"
              value: false
          - name: "diceFullTaken"
            value:
              type: ":dice"
              rolls:
                - 6
                - 6
                - 6
                - 5
                - 5
          - name: "diceFullMissing"
            value:
              type: ":nothing"
          - name: "diceStraightTaken"
            value:
              type: ":dice"
              rolls:
                - 6
                - 5
                - 4
                - 3
                - 2
          - name: "diceStraightMissing"
            value:
              type: ":nothing"
          - name: "listFullLen"
            value:
              type: ":integer"
              value: "5"
          - name: "listFull_1"
            value:
              type: ":text"
              value: "a"
          - name: "listFull_3"
            value:
              type: ":text"
              value: "a"
          - name: "listFull_4"
            value:
              type: ":text"
              value: "b"
          - name: "listStraightLen"
            value:
              type: ":integer"
              value: "3"
          - name: "listStraight_1"
            value:
              type: ":integer"
              value: "3"
          - name: "listStraight_2"
            value:
              type: ":integer"
              value: "1"
          - name: "listStraight_3"
            value:
              type: ":integer"
              value: "2"
          - name: "listStraightMissing"
            value:
              type: ":nothing"
```

---

## Test: pattern selectors over streams

This runtime case exercises “pattern selectors over streams” and verifies the declared messages, values, and execution result.

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
  - name: "pattern selectors over streams.ges"
    program: main
```

### Source code under test

```ges
module AtomicPatternStreams
on Start {
  let listStream be [1, 2, 1, 3][:filter value where value > 0]
  let listStreamTake be [1, 2, 1, 3][:filter value where value > 0][:take pair]
  let dice as :dice be [6, 6, 5, 4]
  let diceStreamTake be dice[:filter value where value > 0][:take pair of 6]
  let textStream be 'aabb'[:filter value where true]
  let rangeStream as :range be from 1 to 4
  emit Done(listStreamHasPair: listStream[:has pair], listStreamTakeLen: listStreamTake[:count], listStreamTakeFirst: listStreamTake[1], listStreamTakeSecond: listStreamTake[2], diceStreamHasPair: dice[:filter value where value > 0][:has pair of 6], diceStreamTakeLen: diceStreamTake[:count], diceStreamTakeFirst: diceStreamTake[1], diceStreamTakeSecond: diceStreamTake[2], textStreamHasPair: textStream[:has pair], textStreamTake: textStream[:take pair], rangeStreamHasPair: rangeStream[:filter value where true][:has pair], rangeStreamTake: rangeStream[:filter value where true][:take pair])
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
          - name: "listStreamHasPair"
            value:
              type: ":boolean"
              value: true
          - name: "listStreamTakeLen"
            value:
              type: ":integer"
              value: "2"
          - name: "listStreamTakeFirst"
            value:
              type: ":integer"
              value: "1"
          - name: "listStreamTakeSecond"
            value:
              type: ":integer"
              value: "1"
          - name: "diceStreamHasPair"
            value:
              type: ":boolean"
              value: true
          - name: "diceStreamTakeLen"
            value:
              type: ":integer"
              value: "2"
          - name: "diceStreamTakeFirst"
            value:
              type: ":integer"
              value: "6"
          - name: "diceStreamTakeSecond"
            value:
              type: ":integer"
              value: "6"
          - name: "textStreamHasPair"
            value:
              type: ":boolean"
              value: true
          - name: "textStreamTake"
            value:
              type: ":list"
              items:
                - type: ":text"
                  value: "a"
                - type: ":text"
                  value: "a"
          - name: "rangeStreamHasPair"
            value:
              type: ":boolean"
              value: false
          - name: "rangeStreamTake"
            value:
              type: ":nothing"
```
