---
formatVersion: 1
suiteId: "runtime.atomic.iterator-core"
title: "RuntimeAtomicIteratorCore"
categories: [conformance]
tags: [migrated-json-v1]
---

# RuntimeAtomicIteratorCore

Mechanically migrated from the former JSON conformance corpus.

## Test: iterator create next close over source kinds

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
  - name: "iterator create next close over source kinds.ges"
    program: main
```

```ges
module AtomicIteratorCreateNextClose
on Start {
  let values be [1, 2, 3]
  let dice as :dice be [6, 4, 2]
  let text be 'abc'
  let tags be #boss
  let map be [a: 1, b: 2]
  let range as :range be from 1 to 4
  let fromRange be range[:select item => item * 2]
  let fromList be values[:select item => item + 10]
  let fromDice be dice[:select item => item]
  let fromText be text[:select item => item]
  let fromTag be tags[:select item => item]
  let fromMap be map[:select item => item]
  let fromEmptyList be [][:select item => item]
  emit Done(fromRange: fromRange, fromList: fromList, fromDice: fromDice, fromText: fromText, fromTag: fromTag, fromMap: fromMap, fromEmptyList: fromEmptyList)
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
          - name: "fromRange"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "4"
                - type: ":integer"
                  value: "6"
                - type: ":integer"
                  value: "8"
          - name: "fromList"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "11"
                - type: ":integer"
                  value: "12"
                - type: ":integer"
                  value: "13"
          - name: "fromDice"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "6"
                - type: ":integer"
                  value: "4"
                - type: ":integer"
                  value: "2"
          - name: "fromText"
            value:
              type: ":list"
              items:
                - type: ":text"
                  value: "a"
                - type: ":text"
                  value: "b"
                - type: ":text"
                  value: "c"
          - name: "fromTag"
            value:
              type: ":list"
              items:
                - type: ":text"
                  value: "b"
                - type: ":text"
                  value: "o"
                - type: ":text"
                  value: "s"
                - type: ":text"
                  value: "s"
          - name: "fromMap"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
          - name: "fromEmptyList"
            value:
              type: ":list"
              items: []
```

## Test: has any and all direct and iterators

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
  - name: "has any and all direct and iterators.ges"
    program: main
```

```ges
module AtomicHasAnyAllDirectAndIterators
on Start {
  let values be [true, false, true]
  let emptyList be []
  let dice as :dice be [6, 4, 2]
  let range as :range be from 0 to 2
  let text be '10'
  let map be [a: false, b: true]
  emit Done(listAny: values[:any value where value], listAll: values[:all value where value], emptyAny: emptyList[:any value where value], emptyAll: emptyList[:all value where value], diceAny: dice[:any value where value], diceAll: dice[:all value where value], rangeAny: range[:any value where value], rangeAll: range[:all value where value], textAny: text[:any value where value], textAll: text[:all value where value], mapAny: map[:any value where value], mapAll: map[:all value where value], iteratorAnyHit: [1, 2, 3][:any value where value > 2], iteratorAnyMiss: [1, 2, 3][:any value where value > 9], iteratorAllHit: [1, 2, 3][:all value where value > 0], iteratorAllMiss: [1, 2, 3][:all value where value > 1])
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
          - name: "listAny"
            value:
              type: ":boolean"
              value: true
          - name: "listAll"
            value:
              type: ":boolean"
              value: false
          - name: "emptyAny"
            value:
              type: ":boolean"
              value: false
          - name: "emptyAll"
            value:
              type: ":boolean"
              value: true
          - name: "diceAny"
            value:
              type: ":boolean"
              value: true
          - name: "diceAll"
            value:
              type: ":boolean"
              value: true
          - name: "rangeAny"
            value:
              type: ":boolean"
              value: true
          - name: "rangeAll"
            value:
              type: ":boolean"
              value: false
          - name: "textAny"
            value:
              type: ":boolean"
              value: true
          - name: "textAll"
            value:
              type: ":boolean"
              value: false
          - name: "mapAny"
            value:
              type: ":boolean"
              value: true
          - name: "mapAll"
            value:
              type: ":boolean"
              value: false
          - name: "iteratorAnyHit"
            value:
              type: ":boolean"
              value: true
          - name: "iteratorAnyMiss"
            value:
              type: ":boolean"
              value: false
          - name: "iteratorAllHit"
            value:
              type: ":boolean"
              value: true
          - name: "iteratorAllMiss"
            value:
              type: ":boolean"
              value: false
```

## Test: iterator map filter collect list chains

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
  - name: "iterator map filter collect list chains.ges"
    program: main
```

```ges
module AtomicIteratorSelectFilterCollectList
on Start {
  let values be [1, 2, 3, 4, 5]
  let mapped be values[:select value => value * 10]
  let filtered be values[:filter value where value mod 2 = 1]
  let chained be values[:filter value where value >= 2][:select value => value * 2][:filter value where value > 5]
  let capturedBase be 100
  let captured be values[:select value => value + capturedBase]
  emit Done(mapped: mapped, filtered: filtered, chained: chained, captured: captured)
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
          - name: "mapped"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "10"
                - type: ":integer"
                  value: "20"
                - type: ":integer"
                  value: "30"
                - type: ":integer"
                  value: "40"
                - type: ":integer"
                  value: "50"
          - name: "filtered"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "5"
          - name: "chained"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "6"
                - type: ":integer"
                  value: "8"
                - type: ":integer"
                  value: "10"
          - name: "captured"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "101"
                - type: ":integer"
                  value: "102"
                - type: ":integer"
                  value: "103"
                - type: ":integer"
                  value: "104"
                - type: ":integer"
                  value: "105"
```

## Test: iterator contains terminals

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
  - name: "iterator contains terminals.ges"
    program: main
```

```ges
module AtomicIteratorContainsTerminals
on Start {
  let values be [1, 2, 3, 4, 5]
  let dice as :dice be [6, 4, 2]
  let range as :range be from 1 to 5
  let text be 'abcd'
  let map be [alpha: 10, beta: 20]
  emit Done(listContainsHit: values[:filter value where value > 2][:contains 4], listContainsMiss: values[:filter value where value > 2][:contains 2], listAnyHit: values[:filter value where value > 2][:contains any [1, 5]], listAnyMiss: values[:filter value where value > 2][:contains any [1, 2]], listAllHit: values[:filter value where value > 2][:contains all [3, 5]], listAllMiss: values[:filter value where value > 2][:contains all [3, 2]], diceContainsHit: dice[:select die => die][:contains 4], diceAnyHit: dice[:select die => die][:contains any [1, 6]], diceAllHit: dice[:select die => die][:contains all [6, 2]], rangeContainsHit: range[:select item => item][:contains 4], rangeAnyHit: range[:select item => item][:contains any [0, 5]], rangeAllHit: range[:select item => item][:contains all [1, 5]], textContainsHit: text[:select char => char][:contains 'b'], textAllHit: text[:select char => char][:contains all ['a', 'd']], mapContainsHit: map[:select value => value][:contains 10], mapAnyHit: map[:select value => value][:contains any [99, 20]], mapAllHit: map[:select value => value][:contains all [10, 20]])
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
          - name: "listContainsHit"
            value:
              type: ":boolean"
              value: true
          - name: "listContainsMiss"
            value:
              type: ":boolean"
              value: false
          - name: "listAnyHit"
            value:
              type: ":boolean"
              value: true
          - name: "listAnyMiss"
            value:
              type: ":boolean"
              value: false
          - name: "listAllHit"
            value:
              type: ":boolean"
              value: true
          - name: "listAllMiss"
            value:
              type: ":boolean"
              value: false
          - name: "diceContainsHit"
            value:
              type: ":boolean"
              value: true
          - name: "diceAnyHit"
            value:
              type: ":boolean"
              value: true
          - name: "diceAllHit"
            value:
              type: ":boolean"
              value: true
          - name: "rangeContainsHit"
            value:
              type: ":boolean"
              value: true
          - name: "rangeAnyHit"
            value:
              type: ":boolean"
              value: true
          - name: "rangeAllHit"
            value:
              type: ":boolean"
              value: true
          - name: "textContainsHit"
            value:
              type: ":boolean"
              value: true
          - name: "textAllHit"
            value:
              type: ":boolean"
              value: true
          - name: "mapContainsHit"
            value:
              type: ":boolean"
              value: true
          - name: "mapAnyHit"
            value:
              type: ":boolean"
              value: true
          - name: "mapAllHit"
            value:
              type: ":boolean"
              value: true
```

## Test: iterator take and drop direct slices

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
  - name: "iterator take and drop direct slices.ges"
    program: main
```

```ges
module AtomicIteratorTakeDropDirectSlices
on Start {
  let values be [1, 2, 3, 4, 5]
  emit Done(takeFirst: values[:filter value where value > 1][:take first 2], dropFirst: values[:filter value where value > 1][:drop first 2], takeLast: values[:filter value where value > 1][:take last 2], dropLast: values[:filter value where value > 1][:drop last 2], takeHighest: values[:filter value where value > 1][:take highest 2], takeLowest: values[:filter value where value > 1][:take lowest 2], dropHighest: values[:filter value where value > 1][:drop highest 2], dropLowest: values[:filter value where value > 1][:drop lowest 2], mappedTakeFirst: values[:select value => value * 10][:take first 2])
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
          - name: "takeFirst"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "dropFirst"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "4"
                - type: ":integer"
                  value: "5"
          - name: "takeLast"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "4"
                - type: ":integer"
                  value: "5"
          - name: "dropLast"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "takeHighest"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "5"
                - type: ":integer"
                  value: "4"
          - name: "takeLowest"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "dropHighest"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "dropLowest"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "4"
                - type: ":integer"
                  value: "5"
          - name: "mappedTakeFirst"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "10"
                - type: ":integer"
                  value: "20"
```

## Test: take drop direct source boundaries

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
  - name: "take drop direct source boundaries.ges"
    program: main
```

```ges
module AtomicTakeDropDirectBoundaries
on Start {
  let values be [1, 2, 3]
  let emptyList be []
  let dice as :dice be [6, 4, 2]
  let emptyDice as :dice be []
  let range as :range be from 1 to 3
  let floatRange as :range be from 1.5 to 2.5 step 0.5
  emit Done(listTakeFirstTooMany: values[:take first 9], listDropFirstTooManyLen: values[:drop first 9][:count], listTakeLastTooMany: values[:take last 9], listDropLastTooManyLen: values[:drop last 9][:count], emptyListTakeLen: emptyList[:take first 2][:count], emptyListDropLen: emptyList[:drop first 2][:count], diceTakeFirstTooMany: dice[:take first 9], diceDropFirstTooManyLen: dice[:drop first 9][:count], emptyDiceTakeLen: emptyDice[:take first 2][:count], emptyDiceDropLen: emptyDice[:drop first 2][:count], rangeTakeFirstTooMany: range[:take first 9], rangeDropFirstTooManyLen: range[:drop first 9][:count], floatRangeTakeLastTooMany: floatRange[:take last 9], floatRangeDropLastTooManyLen: floatRange[:drop last 9][:count], invalidIntTakeFirst: 10[:take first 1], invalidIntDropFirst: 10[:drop first 1], invalidIntTakeLast: 10[:take last 1], invalidIntDropLast: 10[:drop last 1], invalidIntTakeHighest: 10[:take highest 1], invalidIntTakeLowest: 10[:take lowest 1], invalidIntDropHighest: 10[:drop highest 1], invalidIntDropLowest: 10[:drop lowest 1], invalidNothingTakeFirst: nothing[:take first 1], invalidTextTakeFirst: 'abc'[:take first 1], invalidTagTakeFirst: #abc[:take first 1], invalidVectorTakeFirst: :vector(1, 2, 3)[:take first 1], invalidPointTakeFirst: :point(1, 2, 3)[:take first 1], invalidMapTakeFirst: [a: 1][:take first 1])
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
          - name: "listTakeFirstTooMany"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "listDropFirstTooManyLen"
            value:
              type: ":integer"
              value: "0"
          - name: "listTakeLastTooMany"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "listDropLastTooManyLen"
            value:
              type: ":integer"
              value: "0"
          - name: "emptyListTakeLen"
            value:
              type: ":integer"
              value: "0"
          - name: "emptyListDropLen"
            value:
              type: ":integer"
              value: "0"
          - name: "diceTakeFirstTooMany"
            value:
              type: ":dice"
              rolls:
                - 6
                - 4
                - 2
          - name: "diceDropFirstTooManyLen"
            value:
              type: ":integer"
              value: "0"
          - name: "emptyDiceTakeLen"
            value:
              type: ":integer"
              value: "0"
          - name: "emptyDiceDropLen"
            value:
              type: ":integer"
              value: "0"
          - name: "rangeTakeFirstTooMany"
            value:
              type: ":range"
              from: "1"
              to: "3"
              step: "1"
          - name: "rangeDropFirstTooManyLen"
            value:
              type: ":integer"
              value: "0"
          - name: "floatRangeTakeLastTooMany"
            value:
              type: ":range"
              from: "1.5"
              to: "2.5"
              step: "0.5"
          - name: "floatRangeDropLastTooManyLen"
            value:
              type: ":integer"
              value: "0"
          - name: "invalidIntTakeFirst"
            value:
              type: ":nothing"
          - name: "invalidIntDropFirst"
            value:
              type: ":nothing"
          - name: "invalidIntTakeLast"
            value:
              type: ":nothing"
          - name: "invalidIntDropLast"
            value:
              type: ":nothing"
          - name: "invalidIntTakeHighest"
            value:
              type: ":nothing"
          - name: "invalidIntTakeLowest"
            value:
              type: ":nothing"
          - name: "invalidIntDropHighest"
            value:
              type: ":nothing"
          - name: "invalidIntDropLowest"
            value:
              type: ":nothing"
          - name: "invalidNothingTakeFirst"
            value:
              type: ":nothing"
          - name: "invalidTextTakeFirst"
            value:
              type: ":nothing"
          - name: "invalidTagTakeFirst"
            value:
              type: ":nothing"
          - name: "invalidVectorTakeFirst"
            value:
              type: ":nothing"
          - name: "invalidPointTakeFirst"
            value:
              type: ":nothing"
          - name: "invalidMapTakeFirst"
            value:
              type: ":nothing"
```

## Test: take drop iterator boundary counts

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
  - name: "take drop iterator boundary counts.ges"
    program: main
```

```ges
module AtomicTakeDropIteratorBoundaries
on Start {
  let values be [1, 2, 3]
  let emptyIterator be values[:filter value where false]
  emit Done(takeFirstTooMany: values[:filter value where true][:take first 9], dropFirstTooManyLen: values[:filter value where true][:drop first 9][:count], takeLastTooMany: values[:filter value where true][:take last 9], dropLastTooManyLen: values[:filter value where true][:drop last 9][:count], takeHighestTooMany: values[:filter value where true][:take highest 9], dropHighestTooManyLen: values[:filter value where true][:drop highest 9][:count], takeLowestTooMany: values[:filter value where true][:take lowest 9], dropLowestTooManyLen: values[:filter value where true][:drop lowest 9][:count], emptyTakeFirstLen: emptyIterator[:take first 2][:count], emptyDropLastLen: emptyIterator[:drop last 2][:count], emptyTakeHighestLen: emptyIterator[:take highest 2][:count], emptyDropLowestLen: emptyIterator[:drop lowest 2][:count])
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
          - name: "takeFirstTooMany"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "dropFirstTooManyLen"
            value:
              type: ":integer"
              value: "0"
          - name: "takeLastTooMany"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "dropLastTooManyLen"
            value:
              type: ":integer"
              value: "0"
          - name: "takeHighestTooMany"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "1"
          - name: "dropHighestTooManyLen"
            value:
              type: ":integer"
              value: "0"
          - name: "takeLowestTooMany"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "dropLowestTooManyLen"
            value:
              type: ":integer"
              value: "0"
          - name: "emptyTakeFirstLen"
            value:
              type: ":integer"
              value: "0"
          - name: "emptyDropLastLen"
            value:
              type: ":integer"
              value: "0"
          - name: "emptyTakeHighestLen"
            value:
              type: ":integer"
              value: "0"
          - name: "emptyDropLowestLen"
            value:
              type: ":integer"
              value: "0"
```

## Test: weighted choose lowers to iterator weighted terminals

```yaml
gesBlock: case
id: case-0008
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["9", "0", "8.99999", "0"]
sources:
  - name: "weighted choose lowers to iterator weighted terminals.ges"
    program: main
```

```ges
module AtomicWeightedChooseLowering
on Start {
  let units be [[name: 'A', weight: 1], [name: 'B', weight: 3], [name: 'C', weight: 6], [name: 'Ignored', weight: 0]]
  let liveUnits be units[:filter unit where unit.weight > 0]
  let weightedOne be liveUnits[:choose 1 weighted by unit => unit.weight]
  let weightedTwo be liveUnits[:choose 2 weighted by unit => unit.weight]
  let weightedFilteredOne be units[:choose 1 unit where unit.name <> 'Ignored' weighted by unit => unit.weight]
  emit Done(weightedOne: weightedOne.name, weighted_1: weightedTwo[1].name, weighted_2: weightedTwo[2].name, weightedFilteredOne: weightedFilteredOne.name)
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
          - name: "weightedOne"
            value:
              type: ":text"
              value: "C"
          - name: "weighted_1"
            value:
              type: ":text"
              value: "A"
          - name: "weighted_2"
            value:
              type: ":text"
              value: "C"
          - name: "weightedFilteredOne"
            value:
              type: ":text"
              value: "A"
```

## Test: weighted choose ignores empty non positive and invalid weights

```yaml
gesBlock: case
id: case-0009
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["0", "0", "0"]
sources:
  - name: "weighted choose ignores empty non positive and invalid weights.ges"
    program: main
```

```ges
module AtomicWeightedChooseInvalidWeights
on Start {
  let emptyUnits be []
  let invalidUnits be [[name: 'Zero', weight: 0], [name: 'Negative', weight: 0 - 1], [name: 'Missing', weight: nothing], [name: 'Text', weight: 'bad'], [name: 'Infinite', weight: infinity]]
  let mixedUnits be [[name: 'Zero', weight: 0], [name: 'A', weight: 1], [name: 'Negative', weight: 0 - 2], [name: 'B', weight: 2], [name: 'Missing', weight: nothing]]
  let emptyOne be emptyUnits[:choose 1 weighted by unit => unit.weight]
  let emptyMany be emptyUnits[:choose 3 weighted by unit => unit.weight]
  let invalidOne be invalidUnits[:choose 1 weighted by unit => unit.weight]
  let invalidMany be invalidUnits[:choose 3 weighted by unit => unit.weight]
  let mixedOne be mixedUnits[:choose 1 weighted by unit => unit.weight]
  let mixedMany be mixedUnits[:choose 5 weighted by unit => unit.weight]
  emit Done(emptyOneName: emptyOne.name, emptyManyLen: emptyMany[:count], invalidOneName: invalidOne.name, invalidManyLen: invalidMany[:count], mixedOneName: mixedOne.name, mixedManyLen: mixedMany[:count], mixed_1: mixedMany[1].name, mixed_2: mixedMany[2].name, mixed_3: mixedMany[3].name)
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
          - name: "emptyOneName"
            value:
              type: ":nothing"
          - name: "emptyManyLen"
            value:
              type: ":integer"
              value: "0"
          - name: "invalidOneName"
            value:
              type: ":nothing"
          - name: "invalidManyLen"
            value:
              type: ":integer"
              value: "0"
          - name: "mixedOneName"
            value:
              type: ":text"
              value: "A"
          - name: "mixedManyLen"
            value:
              type: ":integer"
              value: "2"
          - name: "mixed_1"
            value:
              type: ":text"
              value: "A"
          - name: "mixed_2"
            value:
              type: ":text"
              value: "B"
          - name: "mixed_3"
            value:
              type: ":nothing"
```

## Test: weighted choose duplicate weights preserve deterministic iterator order

```yaml
gesBlock: case
id: case-0010
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["0", "0", "0", "0", "0", "1", "1"]
sources:
  - name: "weighted choose duplicate weights preserve deterministic iterator order.ges"
    program: main
```

```ges
module AtomicWeightedChooseDuplicateWeights
on Start {
  let units be [[name: 'A', weight: 1], [name: 'B', weight: 1], [name: 'C', weight: 1], [name: 'D', weight: 1]]
  let weightedOne be units[:choose 1 weighted by unit => unit.weight]
  let weightedAll be units[:choose 4 weighted by unit => unit.weight]
  let weightedBoundary be units[:choose 2 weighted by unit => unit.weight]
  emit Done(one: weightedOne.name, all_1: weightedAll[1].name, all_2: weightedAll[2].name, all_3: weightedAll[3].name, all_4: weightedAll[4].name, boundary_1: weightedBoundary[1].name, boundary_2: weightedBoundary[2].name)
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
          - name: "one"
            value:
              type: ":text"
              value: "A"
          - name: "all_1"
            value:
              type: ":text"
              value: "A"
          - name: "all_2"
            value:
              type: ":text"
              value: "B"
          - name: "all_3"
            value:
              type: ":text"
              value: "C"
          - name: "all_4"
            value:
              type: ":text"
              value: "D"
          - name: "boundary_1"
            value:
              type: ":text"
              value: "B"
          - name: "boundary_2"
            value:
              type: ":text"
              value: "C"
```

## Test: weighted choose supports captured weight expressions

```yaml
gesBlock: case
id: case-0011
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["2", "0", "0"]
sources:
  - name: "weighted choose supports captured weight expressions.ges"
    program: main
```

```ges
module AtomicWeightedChooseCapturedWeights
on Start {
  let scale be 2
  let bonus be 1
  let units be [[name: 'Low', weight: 1], [name: 'High', weight: 3], [name: 'Zero', weight: 0]]
  let weightedOne be units[:choose 1 weighted by unit => unit.weight * scale]
  let weightedTwo be units[:choose 2 weighted by unit => unit.weight + bonus]
  emit Done(one: weightedOne.name, two_1: weightedTwo[1].name, two_2: weightedTwo[2].name, twoLen: weightedTwo[:count])
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
          - name: "one"
            value:
              type: ":text"
              value: "High"
          - name: "two_1"
            value:
              type: ":text"
              value: "Low"
          - name: "two_2"
            value:
              type: ":text"
              value: "High"
          - name: "twoLen"
            value:
              type: ":integer"
              value: "2"
```

## Test: choose lowers to first take first and random take

```yaml
gesBlock: case
id: case-0012
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["2", "4", "3", "1", "2", "0", "1", "2", "4"]
sources:
  - name: "choose lowers to first take first and random take.ges"
    program: main
```

```ges
module AtomicChooseLowering
on Start {
  let values be [1, 2, 3, 4, 5]
  let dice as :dice be [6, 5, 4, 3]
  let range as :range be from 10 to 14
  emit Done(chooseOne: values[:choose 1], chooseTwo: values[:choose 2], chooseFilteredOne: values[:choose 1 value where value > 2], chooseFilteredTwo: values[:choose 2 value where value > 2], randomOne: values[:choose 1 at random], randomTwo: values[:choose 2 at random], randomFilteredTwo: values[:choose 2 at random value where value > 2], diceRandomTwo: dice[:choose 2 at random], rangeRandomTwo: range[:choose 2 at random])
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
          - name: "chooseOne"
            value:
              type: ":integer"
              value: "1"
          - name: "chooseTwo"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
          - name: "chooseFilteredOne"
            value:
              type: ":integer"
              value: "3"
          - name: "chooseFilteredTwo"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "4"
          - name: "randomOne"
            value:
              type: ":integer"
              value: "3"
          - name: "randomTwo"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "5"
                - type: ":integer"
                  value: "4"
          - name: "randomFilteredTwo"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "4"
                - type: ":integer"
                  value: "5"
          - name: "diceRandomTwo"
            value:
              type: ":dice"
              rolls:
                - 6
                - 4
          - name: "rangeRandomTwo"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "12"
                - type: ":integer"
                  value: "14"
```

## Test: random choose boundary source kinds

```yaml
gesBlock: case
id: case-0013
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0"]
sources:
  - name: "random choose boundary source kinds.ges"
    program: main
```

```ges
module AtomicRandomChooseBoundaries
on Start {
  let values be [1, 2, 3]
  let emptyList be []
  let dice as :dice be [6, 4, 2]
  let emptyDice as :dice be []
  let range as :range be from 10 to 12
  let emptyIterator be values[:filter value where false]
  emit Done(listOne: values[:choose 1 at random], listTakeTooMany: values[:choose 9 at random], emptyListOne: emptyList[:choose 1 at random], emptyListTakeLen: emptyList[:choose 2 at random][:count], diceOne: dice[:choose 1 at random], diceTakeTooMany: dice[:choose 9 at random], emptyDiceOne: emptyDice[:choose 1 at random], emptyDiceTakeLen: emptyDice[:choose 2 at random][:count], rangeOne: range[:choose 1 at random], rangeTakeTooMany: range[:choose 9 at random], iteratorTakeTooMany: values[:filter value where true][:choose 9 at random], emptyIteratorOne: emptyIterator[:choose 1 at random], emptyIteratorTakeLen: emptyIterator[:choose 2 at random][:count], invalidIntOne: 10[:choose 1 at random], invalidIntTake: 10[:choose 2 at random], invalidMapTake: [a: 1][:choose 2 at random])
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
          - name: "listOne"
            value:
              type: ":integer"
              value: "1"
          - name: "listTakeTooMany"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "emptyListOne"
            value:
              type: ":nothing"
          - name: "emptyListTakeLen"
            value:
              type: ":integer"
              value: "0"
          - name: "diceOne"
            value:
              type: ":integer"
              value: "6"
          - name: "diceTakeTooMany"
            value:
              type: ":dice"
              rolls:
                - 6
                - 4
                - 2
          - name: "emptyDiceOne"
            value:
              type: ":nothing"
          - name: "emptyDiceTakeLen"
            value:
              type: ":integer"
              value: "0"
          - name: "rangeOne"
            value:
              type: ":integer"
              value: "10"
          - name: "rangeTakeTooMany"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "10"
                - type: ":integer"
                  value: "11"
                - type: ":integer"
                  value: "12"
          - name: "iteratorTakeTooMany"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "emptyIteratorOne"
            value:
              type: ":nothing"
          - name: "emptyIteratorTakeLen"
            value:
              type: ":integer"
              value: "0"
          - name: "invalidIntOne"
            value:
              type: ":nothing"
          - name: "invalidIntTake"
            value:
              type: ":nothing"
          - name: "invalidMapTake"
            value:
              type: ":nothing"
```

## Test: take drop highest lowest direct fast paths

```yaml
gesBlock: case
id: case-0014
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "take drop highest lowest direct fast paths.ges"
    program: main
```

```ges
module AtomicTakeDropHighestLowestDirectFastPaths
on Start {
  let values be [1, 2, 3, 4, 5]
  let dice as :dice be [6, 4, 2, 1]
  let range as :range be from 1 to 5
  emit Done(listTakeHighest: values[:take highest 2], listTakeLowest: values[:take lowest 2], listDropHighest: values[:drop highest 2], listDropLowest: values[:drop lowest 2], diceTakeHighest: dice[:take highest 2], diceTakeLowest: dice[:take lowest 2], diceDropHighest: dice[:drop highest 2], diceDropLowest: dice[:drop lowest 2], rangeTakeHighest: range[:take highest 2], rangeTakeLowest: range[:take lowest 2], rangeDropHighest: range[:drop highest 2], rangeDropLowest: range[:drop lowest 2])
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
          - name: "listTakeHighest"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "5"
                - type: ":integer"
                  value: "4"
          - name: "listTakeLowest"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
          - name: "listDropHighest"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "listDropLowest"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "4"
                - type: ":integer"
                  value: "5"
          - name: "diceTakeHighest"
            value:
              type: ":dice"
              rolls:
                - 6
                - 4
          - name: "diceTakeLowest"
            value:
              type: ":dice"
              rolls:
                - 2
                - 1
          - name: "diceDropHighest"
            value:
              type: ":dice"
              rolls:
                - 2
                - 1
          - name: "diceDropLowest"
            value:
              type: ":dice"
              rolls:
                - 6
                - 4
          - name: "rangeTakeHighest"
            value:
              type: ":range"
              from: "5"
              to: "4"
              step: "-1"
          - name: "rangeTakeLowest"
            value:
              type: ":range"
              from: "1"
              to: "2"
              step: "1"
          - name: "rangeDropHighest"
            value:
              type: ":range"
              from: "1"
              to: "3"
              step: "1"
          - name: "rangeDropLowest"
            value:
              type: ":range"
              from: "3"
              to: "5"
              step: "1"
```

## Test: iterator collect map and map value

```yaml
gesBlock: case
id: case-0015
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "iterator collect map and map value.ges"
    program: main
```

```ges
module AtomicMapSelectorLoop
on Start {
  let units be [[id: #rook, hp: 10, team: #blue], [id: #mage, hp: 6, team: #red], [id: #rook, hp: 12, team: #green], [id: '', hp: 99, team: #hidden]]
  let emptyUnits be []
  let dice as :dice be [6, 4, 2]
  let range as :range be from 1 to 3
  let text be 'aba'
  let sourceMap be [b: 2, a: 1]
  let byId be units[:filter unit where true][:map unit by unit.id]
  let hpById be units[:map unit by unit.id => unit.hp]
  let emptyById be emptyUnits[:map unit by unit.id]
  let diceMap be dice[:map die by die]
  let rangeMap be range[:map value by value]
  let textMap be text[:map char by char]
  let mapValueMap be sourceMap[:map value by value => value * 10]
  let invalidIntegerMap be 10[:map value by value]
  emit Done(byIdCount: byId[:count], rookHp: byId[#rook].hp, rookTeam: byId[#rook].team, mageHp: byId[#mage].hp, hpByIdCount: hpById[:count], hpRook: hpById[#rook], hpMage: hpById[#mage], missingEmpty: byId[''], emptyByIdLen: emptyById[:count], diceMapLen: diceMap[:count], diceSix: diceMap['6'], diceFour: diceMap['4'], rangeMapLen: rangeMap[:count], rangeTwo: rangeMap['2'], textMapLen: textMap[:count], textA: textMap['a'], textB: textMap['b'], mapValueLen: mapValueMap[:count], mapValueOne: mapValueMap['1'], mapValueTwo: mapValueMap['2'], invalidIntegerMapLen: invalidIntegerMap[:count])
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
          - name: "byIdCount"
            value:
              type: ":integer"
              value: "2"
          - name: "rookHp"
            value:
              type: ":integer"
              value: "12"
          - name: "rookTeam"
            value:
              type: ":tag"
              value: "green"
          - name: "mageHp"
            value:
              type: ":integer"
              value: "6"
          - name: "hpByIdCount"
            value:
              type: ":integer"
              value: "2"
          - name: "hpRook"
            value:
              type: ":integer"
              value: "12"
          - name: "hpMage"
            value:
              type: ":integer"
              value: "6"
          - name: "missingEmpty"
            value:
              type: ":nothing"
          - name: "emptyByIdLen"
            value:
              type: ":integer"
              value: "0"
          - name: "diceMapLen"
            value:
              type: ":integer"
              value: "3"
          - name: "diceSix"
            value:
              type: ":integer"
              value: "6"
          - name: "diceFour"
            value:
              type: ":integer"
              value: "4"
          - name: "rangeMapLen"
            value:
              type: ":integer"
              value: "3"
          - name: "rangeTwo"
            value:
              type: ":integer"
              value: "2"
          - name: "textMapLen"
            value:
              type: ":integer"
              value: "2"
          - name: "textA"
            value:
              type: ":text"
              value: "a"
          - name: "textB"
            value:
              type: ":text"
              value: "b"
          - name: "mapValueLen"
            value:
              type: ":integer"
              value: "2"
          - name: "mapValueOne"
            value:
              type: ":integer"
              value: "10"
          - name: "mapValueTwo"
            value:
              type: ":integer"
              value: "20"
          - name: "invalidIntegerMapLen"
            value:
              type: ":integer"
              value: "0"
```

## Test: iterator collect map duplicate keys use last value

```yaml
gesBlock: case
id: case-0016
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "iterator collect map duplicate keys use last value.ges"
    program: main
```

```ges
module AtomicMapSelectorDuplicateKeys
on Start {
  let units be [[id: #rook, hp: 10, team: #blue], [id: #mage, hp: 6, team: #red], [id: #rook, hp: 12, team: #green], [id: #mage, hp: 8, team: #gold]]
  let byId be units[:filter unit where true][:map unit by unit.id]
  let hpById be units[:map unit by unit.id => unit.hp]
  let iteratorById be units[:filter unit where true][:map unit by unit.id]
  emit Done(byIdLen: byId[:count], rookHp: byId[#rook].hp, rookTeam: byId[#rook].team, mageHp: byId[#mage].hp, mageTeam: byId[#mage].team, hpByIdLen: hpById[:count], hpRook: hpById[#rook], hpMage: hpById[#mage], iteratorRookHp: iteratorById[#rook].hp, iteratorMageTeam: iteratorById[#mage].team)
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
          - name: "byIdLen"
            value:
              type: ":integer"
              value: "2"
          - name: "rookHp"
            value:
              type: ":integer"
              value: "12"
          - name: "rookTeam"
            value:
              type: ":tag"
              value: "green"
          - name: "mageHp"
            value:
              type: ":integer"
              value: "8"
          - name: "mageTeam"
            value:
              type: ":tag"
              value: "gold"
          - name: "hpByIdLen"
            value:
              type: ":integer"
              value: "2"
          - name: "hpRook"
            value:
              type: ":integer"
              value: "12"
          - name: "hpMage"
            value:
              type: ":integer"
              value: "8"
          - name: "iteratorRookHp"
            value:
              type: ":integer"
              value: "12"
          - name: "iteratorMageTeam"
            value:
              type: ":tag"
              value: "gold"
```

## Test: iterator collect map skips empty keys and keeps projected nothing values

```yaml
gesBlock: case
id: case-0017
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "iterator collect map skips empty keys and keeps projected nothing values.ges"
    program: main
```

```ges
module AtomicMapSelectorInvalidKeysAndValues
on Start {
  let units be [[id: #rook, hp: 10], [id: '', hp: 99], [id: nothing, hp: 42], [id: #mage, hp: 6]]
  let byId be units[:filter unit where true][:map unit by unit.id]
  let missingById be units[:map unit by unit.id => unit.missing]
  let textKeyMap be ['a', '', 'b'][:map value by value => value]
  emit Done(byIdLen: byId[:count], rookHp: byId[#rook].hp, mageHp: byId[#mage].hp, emptyKey: byId[''], missingKey: byId['nothing'], missingByIdLen: missingById[:count], missingRook: missingById[#rook], missingMage: missingById[#mage], textKeyLen: textKeyMap[:count], textA: textKeyMap['a'], textB: textKeyMap['b'], textEmpty: textKeyMap[''])
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
          - name: "byIdLen"
            value:
              type: ":integer"
              value: "2"
          - name: "rookHp"
            value:
              type: ":integer"
              value: "10"
          - name: "mageHp"
            value:
              type: ":integer"
              value: "6"
          - name: "emptyKey"
            value:
              type: ":nothing"
          - name: "missingKey"
            value:
              type: ":nothing"
          - name: "missingByIdLen"
            value:
              type: ":integer"
              value: "2"
          - name: "missingRook"
            value:
              type: ":nothing"
          - name: "missingMage"
            value:
              type: ":nothing"
          - name: "textKeyLen"
            value:
              type: ":integer"
              value: "2"
          - name: "textA"
            value:
              type: ":text"
              value: "a"
          - name: "textB"
            value:
              type: ":text"
              value: "b"
          - name: "textEmpty"
            value:
              type: ":nothing"
```

## Test: first last single direct and iterators

```yaml
gesBlock: case
id: case-0018
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "first last single direct and iterators.ges"
    program: main
```

```ges
module AtomicFirstLastSingle
on Start {
  let values be [1, 2, 3, 4]
  let emptyList be []
  let dice as :dice be [6, 4, 2]
  let range as :range be from 1 to 3
  let text be 'ab'
  let map be [a: 1, b: 2]
  emit Done(first: values[:first], firstFiltered: values[:first value where value > 2], firstEmpty: emptyList[:first], last: values[:last], lastFiltered: values[:last value where value < 4], lastEmpty: emptyList[:last], single: values[:single value where value = 3], singleNone: values[:single value where value > 9], singleMany: values[:single value where value > 1], diceFirst: dice[:first], diceLast: dice[:last], rangeFirst: range[:first], rangeLast: range[:last], textFirst: text[:first], textLast: text[:last], textSingle: 'x'[:single], mapFirst: map[:first], mapLast: map[:last], listSingle: [9][:single])
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
          - name: "first"
            value:
              type: ":integer"
              value: "1"
          - name: "firstFiltered"
            value:
              type: ":integer"
              value: "3"
          - name: "firstEmpty"
            value:
              type: ":nothing"
          - name: "last"
            value:
              type: ":integer"
              value: "4"
          - name: "lastFiltered"
            value:
              type: ":integer"
              value: "3"
          - name: "lastEmpty"
            value:
              type: ":nothing"
          - name: "single"
            value:
              type: ":integer"
              value: "3"
          - name: "singleNone"
            value:
              type: ":nothing"
          - name: "singleMany"
            value:
              type: ":nothing"
          - name: "diceFirst"
            value:
              type: ":integer"
              value: "6"
          - name: "diceLast"
            value:
              type: ":integer"
              value: "2"
          - name: "rangeFirst"
            value:
              type: ":integer"
              value: "1"
          - name: "rangeLast"
            value:
              type: ":integer"
              value: "3"
          - name: "textFirst"
            value:
              type: ":text"
              value: "a"
          - name: "textLast"
            value:
              type: ":text"
              value: "b"
          - name: "textSingle"
            value:
              type: ":text"
              value: "x"
          - name: "mapFirst"
            value:
              type: ":integer"
              value: "1"
          - name: "mapLast"
            value:
              type: ":integer"
              value: "2"
          - name: "listSingle"
            value:
              type: ":integer"
              value: "9"
```

## Test: range iterators stop at numeric boundaries

```yaml
gesBlock: case
id: case-0019
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "range iterators stop at numeric boundaries.ges"
    program: main
```

```ges
module AtomicRangeIteratorBoundaries
on Start(maximum, minimum, huge) {
  let maximumRange as :range be from maximum to maximum step 1
  let minimumRange as :range be from minimum to minimum step -1
  let hugeRange as :range be from huge to huge step 1.0
  let wrongAscending as :range be from 3 to 1 step 1
  let wrongDescending as :range be from 1 to 3 step -1
  let zeroStep as :range be from 1 to 1 step 0
  let maximumValues be maximumRange[:select value => value]
  let minimumValues be minimumRange[:select value => value]
  let hugeValues be hugeRange[:select value => value]
  emit Done(maximumCount: maximumValues[:count], maximumValue: maximumValues[1], minimumCount: minimumValues[:count], minimumValue: minimumValues[1], hugeCount: hugeValues[:count], hugeValue: hugeValues[1], wrongAscendingCount: wrongAscending[:count], wrongDescendingCount: wrongDescending[:count], zeroStepCount: zeroStep[:count])
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
        - name: "maximum"
          value:
            type: ":integer"
            value: "9223372036854775807"
        - name: "minimum"
          value:
            type: ":integer"
            value: "-9223372036854775808"
        - name: "huge"
          value:
            type: ":float"
            value: "1e308"
    local:
      - name: "Done"
        args:
          - name: "maximumCount"
            value:
              type: ":integer"
              value: "1"
          - name: "maximumValue"
            value:
              type: ":integer"
              value: "9223372036854775807"
          - name: "minimumCount"
            value:
              type: ":integer"
              value: "1"
          - name: "minimumValue"
            value:
              type: ":integer"
              value: "-9223372036854775808"
          - name: "hugeCount"
            value:
              type: ":integer"
              value: "1"
          - name: "hugeValue"
            value:
              type: ":float"
              value: "1e308"
          - name: "wrongAscendingCount"
            value:
              type: ":integer"
              value: "0"
          - name: "wrongDescendingCount"
            value:
              type: ":integer"
              value: "0"
          - name: "zeroStepCount"
            value:
              type: ":integer"
              value: "0"
```
