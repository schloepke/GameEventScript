---
formatVersion: 1
suiteId: "runtime.atomic.patterns"
title: "RuntimeAtomicPatterns"
categories: [conformance]
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
module atomichaspatterndirectkinds
on Start {
  let vNothing be nothing
  let vBoolean be true
  let vInteger be 10
  let vFloat be 10.5
  let vPercentage be 25%
  let vText be 'aabb'
  let vTag be #aabb
  let vVector be :Vector(1, 1, 2)
  let vPoint be :Point(1, 1, 2)
  let vList be [3, 1, 3, 2]
  let vListText be ['a', 'b', 'a']
  let vListTag be [#fire, #ice, #fire]
  let vMap be [a: 1, b: 1]
  let vDice be ([6, 6, 5, 4]) as :Dice
  let vDiceNoPair be ([6, 5, 4, 3]) as :Dice
  let vRange be (from 1 to 4) as :Range
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
              type: ":Boolean"
              value: false
          - name: "booleanValue"
            value:
              type: ":Boolean"
              value: false
          - name: "integerValue"
            value:
              type: ":Boolean"
              value: false
          - name: "floatValue"
            value:
              type: ":Boolean"
              value: false
          - name: "percentageValue"
            value:
              type: ":Boolean"
              value: false
          - name: "textValue"
            value:
              type: ":Boolean"
              value: false
          - name: "tagValue"
            value:
              type: ":Boolean"
              value: false
          - name: "vectorValue"
            value:
              type: ":Boolean"
              value: false
          - name: "pointValue"
            value:
              type: ":Boolean"
              value: false
          - name: "listPair"
            value:
              type: ":Boolean"
              value: true
          - name: "listPairOfThree"
            value:
              type: ":Boolean"
              value: true
          - name: "listPairOfFour"
            value:
              type: ":Boolean"
              value: false
          - name: "listTextPair"
            value:
              type: ":Boolean"
              value: true
          - name: "listTextMissing"
            value:
              type: ":Boolean"
              value: false
          - name: "listTagPair"
            value:
              type: ":Boolean"
              value: true
          - name: "mapValue"
            value:
              type: ":Boolean"
              value: false
          - name: "dicePair"
            value:
              type: ":Boolean"
              value: true
          - name: "dicePairOfSix"
            value:
              type: ":Boolean"
              value: true
          - name: "dicePairOfFive"
            value:
              type: ":Boolean"
              value: false
          - name: "diceNoPair"
            value:
              type: ":Boolean"
              value: false
          - name: "rangeValue"
            value:
              type: ":Boolean"
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
module atomictakepatterncountkinds
on Start {
  let vNothing be nothing
  let vInteger be 10
  let vText be 'aabb'
  let vMap be [a: 1, b: 1]
  let vRange be (from 1 to 4) as :Range
  let vList be [3, 1, 3, 2, 3]
  let vListText be ['a', 'b', 'a']
  let vListTag be [#fire, #ice, #fire]
  let vDice be ([6, 6, 5, 5, 5]) as :Dice
  let vDiceNoTriple be ([6, 6, 5, 4]) as :Dice
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
              type: ":Nothing"
          - name: "integerValue"
            value:
              type: ":Nothing"
          - name: "textValue"
            value:
              type: ":Nothing"
          - name: "mapValue"
            value:
              type: ":Nothing"
          - name: "rangeValue"
            value:
              type: ":Nothing"
          - name: "listPairLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "listPairFirst"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "listPairSecond"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "listThreeLen"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "listThreeFirst"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "listThreeThird"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "listPairOfThreeLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "listPairOfThreeFirst"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "listPairOfFour"
            value:
              type: ":Nothing"
          - name: "textPairLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "textPairFirst"
            value:
              type: ":Text"
              value: "a"
          - name: "textPairSecond"
            value:
              type: ":Text"
              value: "a"
          - name: "tagPairLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "tagPairFirst"
            value:
              type: ":Tag"
              value: "fire"
          - name: "tagPairSecond"
            value:
              type: ":Tag"
              value: "fire"
          - name: "dicePair"
            value:
              type: ":Dice"
              rolls:
                - 6
                - 6
          - name: "diceThree"
            value:
              type: ":Dice"
              rolls:
                - 5
                - 5
                - 5
          - name: "dicePairOfSix"
            value:
              type: ":Dice"
              rolls:
                - 6
                - 6
          - name: "diceNoTriple"
            value:
              type: ":Nothing"
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
module atomicfullhousestraightpatterns
on Start {
  let diceFull be ([6, 6, 6, 5, 5]) as :Dice
  let diceNotFull be ([6, 6, 5, 5, 4]) as :Dice
  let diceStraight be ([6, 5, 4, 3, 2]) as :Dice
  let diceNotStraight be ([6, 5, 4, 2, 1]) as :Dice
  let listFull be ['a', 'b', 'a', 'b', 'a']
  let listNotFull be ['a', 'b', 'a', 'b', 'c']
  let listStraight be [3, 1, 2]
  let listNotStraight be [3, 1, 5]
  let diceFullTaken be diceFull[:take full house]
  let diceStraightTaken be diceStraight[:take straight]
  let listFullTaken be listFull[:take full house]
  let listStraightTaken be listStraight[:take straight]
  emit Done(diceFullHas: diceFull[:has full house], diceNotFullHas: diceNotFull[:has full house], diceStraightHas: diceStraight[:has straight], diceNotStraightHas: diceNotStraight[:has straight], listFullHas: listFull[:has full house], listNotFullHas: listNotFull[:has full house], listStraightHas: listStraight[:has straight], listNotStraightHas: listNotStraight[:has straight], diceFullTaken: diceFullTaken, diceFullMissing: diceNotFull[:take full house], diceStraightTaken: diceStraightTaken, diceStraightMissing: diceNotStraight[:take straight], listFullLen: listFull[:count], listFull1: listFullTaken[1], listFull3: listFullTaken[3], listFull4: listFullTaken[4], listStraightLen: listStraight[:count], listStraight1: listStraightTaken[1], listStraight2: listStraightTaken[2], listStraight3: listStraightTaken[3], listStraightMissing: listNotStraight[:take straight])
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
              type: ":Boolean"
              value: true
          - name: "diceNotFullHas"
            value:
              type: ":Boolean"
              value: false
          - name: "diceStraightHas"
            value:
              type: ":Boolean"
              value: true
          - name: "diceNotStraightHas"
            value:
              type: ":Boolean"
              value: false
          - name: "listFullHas"
            value:
              type: ":Boolean"
              value: true
          - name: "listNotFullHas"
            value:
              type: ":Boolean"
              value: false
          - name: "listStraightHas"
            value:
              type: ":Boolean"
              value: true
          - name: "listNotStraightHas"
            value:
              type: ":Boolean"
              value: false
          - name: "diceFullTaken"
            value:
              type: ":Dice"
              rolls:
                - 6
                - 6
                - 6
                - 5
                - 5
          - name: "diceFullMissing"
            value:
              type: ":Nothing"
          - name: "diceStraightTaken"
            value:
              type: ":Dice"
              rolls:
                - 6
                - 5
                - 4
                - 3
                - 2
          - name: "diceStraightMissing"
            value:
              type: ":Nothing"
          - name: "listFullLen"
            value:
              type: ":Number.int64"
              value: "5"
          - name: "listFull1"
            value:
              type: ":Text"
              value: "a"
          - name: "listFull3"
            value:
              type: ":Text"
              value: "a"
          - name: "listFull4"
            value:
              type: ":Text"
              value: "b"
          - name: "listStraightLen"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "listStraight1"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "listStraight2"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "listStraight3"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "listStraightMissing"
            value:
              type: ":Nothing"
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
module atomicpatternstreams
on Start {
  let listStream be [1, 2, 1, 3][:filter value where value > 0]
  let listStreamTake be [1, 2, 1, 3][:filter value where value > 0][:take pair]
  let dice be ([6, 6, 5, 4]) as :Dice
  let diceStreamTake be dice[:filter value where value > 0][:take pair of 6]
  let textStream be 'aabb'[:filter value where true]
  let rangeStream be (from 1 to 4) as :Range
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
              type: ":Boolean"
              value: true
          - name: "listStreamTakeLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "listStreamTakeFirst"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "listStreamTakeSecond"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "diceStreamHasPair"
            value:
              type: ":Boolean"
              value: true
          - name: "diceStreamTakeLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "diceStreamTakeFirst"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "diceStreamTakeSecond"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "textStreamHasPair"
            value:
              type: ":Boolean"
              value: true
          - name: "textStreamTake"
            value:
              type: ":List"
              items:
                - type: ":Text"
                  value: "a"
                - type: ":Text"
                  value: "a"
          - name: "rangeStreamHasPair"
            value:
              type: ":Boolean"
              value: false
          - name: "rangeStreamTake"
            value:
              type: ":Nothing"
```
