---
formatVersion: 1
suiteId: "runtime.atomic.iterator-core"
title: "RuntimeAtomicIteratorCore"
categories: [conformance]
---

# RuntimeAtomicIteratorCore

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite isolates iterator creation and intermediate pipeline operations while preserving deterministic order.

---

## Test: iterator create next close over source kinds

This runtime case exercises “iterator create next close over source kinds” and verifies the declared messages, values, and execution result.

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
  - name: "iterator create next close over source kinds.ges"
    program: main
```

### Source code under test

```ges
module atomiciteratorcreatenextclose
on Start {
  let values be [1, 2, 3]
  let dice be ([6, 4, 2]) as :Dice
  let text be 'abc'
  let tags be #boss
  let map be [a: 1, b: 2]
  let range be (from 1 to 4) as :Range
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
          - name: "fromRange"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "4"
                - type: ":Number.int64"
                  value: "6"
                - type: ":Number.int64"
                  value: "8"
          - name: "fromList"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "11"
                - type: ":Number.int64"
                  value: "12"
                - type: ":Number.int64"
                  value: "13"
          - name: "fromDice"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "6"
                - type: ":Number.int64"
                  value: "4"
                - type: ":Number.int64"
                  value: "2"
          - name: "fromText"
            value:
              type: ":List"
              items:
                - type: ":Text"
                  value: "a"
                - type: ":Text"
                  value: "b"
                - type: ":Text"
                  value: "c"
          - name: "fromTag"
            value:
              type: ":List"
              items:
                - type: ":Text"
                  value: "b"
                - type: ":Text"
                  value: "o"
                - type: ":Text"
                  value: "s"
                - type: ":Text"
                  value: "s"
          - name: "fromMap"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
          - name: "fromEmptyList"
            value:
              type: ":List"
              items: []
```

---

## Test: has any and all direct and iterators

This runtime case exercises “has any and all direct and iterators” and verifies the declared messages, values, and execution result.

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
  - name: "has any and all direct and iterators.ges"
    program: main
```

### Source code under test

```ges
module atomichasanyalldirectanditerators
on Start {
  let values be [true, false, true]
  let emptyList be []
  let dice be ([6, 4, 2]) as :Dice
  let range be (from 0 to 2) as :Range
  let text be '10'
  let map be [a: false, b: true]
  emit Done(listAny: values[:any value where value], listAll: values[:all value where value], emptyAny: emptyList[:any value where value], emptyAll: emptyList[:all value where value], diceAny: dice[:any value where value], diceAll: dice[:all value where value], rangeAny: range[:any value where value], rangeAll: range[:all value where value], textAny: text[:any value where value], textAll: text[:all value where value], mapAny: map[:any value where value], mapAll: map[:all value where value], iteratorAnyHit: [1, 2, 3][:any value where value > 2], iteratorAnyMiss: [1, 2, 3][:any value where value > 9], iteratorAllHit: [1, 2, 3][:all value where value > 0], iteratorAllMiss: [1, 2, 3][:all value where value > 1])
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
          - name: "listAny"
            value:
              type: ":Boolean"
              value: true
          - name: "listAll"
            value:
              type: ":Boolean"
              value: false
          - name: "emptyAny"
            value:
              type: ":Boolean"
              value: false
          - name: "emptyAll"
            value:
              type: ":Boolean"
              value: true
          - name: "diceAny"
            value:
              type: ":Boolean"
              value: true
          - name: "diceAll"
            value:
              type: ":Boolean"
              value: true
          - name: "rangeAny"
            value:
              type: ":Boolean"
              value: true
          - name: "rangeAll"
            value:
              type: ":Boolean"
              value: false
          - name: "textAny"
            value:
              type: ":Boolean"
              value: true
          - name: "textAll"
            value:
              type: ":Boolean"
              value: false
          - name: "mapAny"
            value:
              type: ":Boolean"
              value: true
          - name: "mapAll"
            value:
              type: ":Boolean"
              value: false
          - name: "iteratorAnyHit"
            value:
              type: ":Boolean"
              value: true
          - name: "iteratorAnyMiss"
            value:
              type: ":Boolean"
              value: false
          - name: "iteratorAllHit"
            value:
              type: ":Boolean"
              value: true
          - name: "iteratorAllMiss"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: iterator map filter collect list chains

This runtime case exercises “iterator map filter collect list chains” and verifies the declared messages, values, and execution result.

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
  - name: "iterator map filter collect list chains.ges"
    program: main
```

### Source code under test

```ges
module atomiciteratorselectfiltercollectlist
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
          - name: "mapped"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "10"
                - type: ":Number.int64"
                  value: "20"
                - type: ":Number.int64"
                  value: "30"
                - type: ":Number.int64"
                  value: "40"
                - type: ":Number.int64"
                  value: "50"
          - name: "filtered"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "3"
                - type: ":Number.int64"
                  value: "5"
          - name: "chained"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "6"
                - type: ":Number.int64"
                  value: "8"
                - type: ":Number.int64"
                  value: "10"
          - name: "captured"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "101"
                - type: ":Number.int64"
                  value: "102"
                - type: ":Number.int64"
                  value: "103"
                - type: ":Number.int64"
                  value: "104"
                - type: ":Number.int64"
                  value: "105"
```

---

## Test: iterator contains terminals

This runtime case exercises “iterator contains terminals” and verifies the declared messages, values, and execution result.

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
  - name: "iterator contains terminals.ges"
    program: main
```

### Source code under test

```ges
module atomiciteratorcontainsterminals
on Start {
  let values be [1, 2, 3, 4, 5]
  let dice be ([6, 4, 2]) as :Dice
  let range be (from 1 to 5) as :Range
  let text be 'abcd'
  let map be [alpha: 10, beta: 20]
  emit Done(listContainsHit: values[:filter value where value > 2][:contains 4], listContainsMiss: values[:filter value where value > 2][:contains 2], listAnyHit: values[:filter value where value > 2][:contains any [1, 5]], listAnyMiss: values[:filter value where value > 2][:contains any [1, 2]], listAllHit: values[:filter value where value > 2][:contains all [3, 5]], listAllMiss: values[:filter value where value > 2][:contains all [3, 2]], diceContainsHit: dice[:select die => die][:contains 4], diceAnyHit: dice[:select die => die][:contains any [1, 6]], diceAllHit: dice[:select die => die][:contains all [6, 2]], rangeContainsHit: range[:select item => item][:contains 4], rangeAnyHit: range[:select item => item][:contains any [0, 5]], rangeAllHit: range[:select item => item][:contains all [1, 5]], textContainsHit: text[:select char => char][:contains 'b'], textAllHit: text[:select char => char][:contains all ['a', 'd']], mapContainsHit: map[:select value => value][:contains 10], mapAnyHit: map[:select value => value][:contains any [99, 20]], mapAllHit: map[:select value => value][:contains all [10, 20]])
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
          - name: "listContainsHit"
            value:
              type: ":Boolean"
              value: true
          - name: "listContainsMiss"
            value:
              type: ":Boolean"
              value: false
          - name: "listAnyHit"
            value:
              type: ":Boolean"
              value: true
          - name: "listAnyMiss"
            value:
              type: ":Boolean"
              value: false
          - name: "listAllHit"
            value:
              type: ":Boolean"
              value: true
          - name: "listAllMiss"
            value:
              type: ":Boolean"
              value: false
          - name: "diceContainsHit"
            value:
              type: ":Boolean"
              value: true
          - name: "diceAnyHit"
            value:
              type: ":Boolean"
              value: true
          - name: "diceAllHit"
            value:
              type: ":Boolean"
              value: true
          - name: "rangeContainsHit"
            value:
              type: ":Boolean"
              value: true
          - name: "rangeAnyHit"
            value:
              type: ":Boolean"
              value: true
          - name: "rangeAllHit"
            value:
              type: ":Boolean"
              value: true
          - name: "textContainsHit"
            value:
              type: ":Boolean"
              value: true
          - name: "textAllHit"
            value:
              type: ":Boolean"
              value: true
          - name: "mapContainsHit"
            value:
              type: ":Boolean"
              value: true
          - name: "mapAnyHit"
            value:
              type: ":Boolean"
              value: true
          - name: "mapAllHit"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: iterator take and drop direct slices

This runtime case exercises “iterator take and drop direct slices” and verifies the declared messages, values, and execution result.

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
  - name: "iterator take and drop direct slices.ges"
    program: main
```

### Source code under test

```ges
module atomiciteratortakedropdirectslices
on Start {
  let values be [1, 2, 3, 4, 5]
  emit Done(takeFirst: values[:filter value where value > 1][:take first 2], dropFirst: values[:filter value where value > 1][:drop first 2], takeLast: values[:filter value where value > 1][:take last 2], dropLast: values[:filter value where value > 1][:drop last 2], takeHighest: values[:filter value where value > 1][:take highest 2], takeLowest: values[:filter value where value > 1][:take lowest 2], dropHighest: values[:filter value where value > 1][:drop highest 2], dropLowest: values[:filter value where value > 1][:drop lowest 2], mappedTakeFirst: values[:select value => value * 10][:take first 2])
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
          - name: "takeFirst"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
          - name: "dropFirst"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "4"
                - type: ":Number.int64"
                  value: "5"
          - name: "takeLast"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "4"
                - type: ":Number.int64"
                  value: "5"
          - name: "dropLast"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
          - name: "takeHighest"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "5"
                - type: ":Number.int64"
                  value: "4"
          - name: "takeLowest"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
          - name: "dropHighest"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
          - name: "dropLowest"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "4"
                - type: ":Number.int64"
                  value: "5"
          - name: "mappedTakeFirst"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "10"
                - type: ":Number.int64"
                  value: "20"
```

---

## Test: take drop direct source boundaries

This runtime case exercises “take drop direct source boundaries” and verifies the declared messages, values, and execution result.

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
  - name: "take drop direct source boundaries.ges"
    program: main
```

### Source code under test

```ges
module atomictakedropdirectboundaries
on Start {
  let values be [1, 2, 3]
  let emptyList be []
  let dice be ([6, 4, 2]) as :Dice
  let emptyDice be ([]) as :Dice
  let range be (from 1 to 3) as :Range
  let floatRange be (from 1.5 to 2.5 step 0.5) as :Range
  emit Done(listTakeFirstTooMany: values[:take first 9], listDropFirstTooManyLen: values[:drop first 9][:count], listTakeLastTooMany: values[:take last 9], listDropLastTooManyLen: values[:drop last 9][:count], emptyListTakeLen: emptyList[:take first 2][:count], emptyListDropLen: emptyList[:drop first 2][:count], diceTakeFirstTooMany: dice[:take first 9], diceDropFirstTooManyLen: dice[:drop first 9][:count], emptyDiceTakeLen: emptyDice[:take first 2][:count], emptyDiceDropLen: emptyDice[:drop first 2][:count], rangeTakeFirstTooMany: range[:take first 9], rangeDropFirstTooManyLen: range[:drop first 9][:count], floatRangeTakeLastTooMany: floatRange[:take last 9], floatRangeDropLastTooManyLen: floatRange[:drop last 9][:count], invalidIntTakeFirst: 10[:take first 1], invalidIntDropFirst: 10[:drop first 1], invalidIntTakeLast: 10[:take last 1], invalidIntDropLast: 10[:drop last 1], invalidIntTakeHighest: 10[:take highest 1], invalidIntTakeLowest: 10[:take lowest 1], invalidIntDropHighest: 10[:drop highest 1], invalidIntDropLowest: 10[:drop lowest 1], invalidNothingTakeFirst: nothing[:take first 1], invalidTextTakeFirst: 'abc'[:take first 1], invalidTagTakeFirst: #abc[:take first 1], invalidVectorTakeFirst: :Vector(1, 2, 3)[:take first 1], invalidPointTakeFirst: :Point(1, 2, 3)[:take first 1], invalidMapTakeFirst: [a: 1][:take first 1])
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
          - name: "listTakeFirstTooMany"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
          - name: "listDropFirstTooManyLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "listTakeLastTooMany"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
          - name: "listDropLastTooManyLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "emptyListTakeLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "emptyListDropLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "diceTakeFirstTooMany"
            value:
              type: ":Dice"
              rolls:
                - 6
                - 4
                - 2
          - name: "diceDropFirstTooManyLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "emptyDiceTakeLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "emptyDiceDropLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "rangeTakeFirstTooMany"
            value:
              type: ":Range.int64"
              from: "1"
              to: "3"
              step: "1"
          - name: "rangeDropFirstTooManyLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "floatRangeTakeLastTooMany"
            value:
              type: ":Range.binary64"
              from: "1.5"
              to: "2.5"
              step: "0.5"
          - name: "floatRangeDropLastTooManyLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "invalidIntTakeFirst"
            value:
              type: ":Nothing"
          - name: "invalidIntDropFirst"
            value:
              type: ":Nothing"
          - name: "invalidIntTakeLast"
            value:
              type: ":Nothing"
          - name: "invalidIntDropLast"
            value:
              type: ":Nothing"
          - name: "invalidIntTakeHighest"
            value:
              type: ":Nothing"
          - name: "invalidIntTakeLowest"
            value:
              type: ":Nothing"
          - name: "invalidIntDropHighest"
            value:
              type: ":Nothing"
          - name: "invalidIntDropLowest"
            value:
              type: ":Nothing"
          - name: "invalidNothingTakeFirst"
            value:
              type: ":Nothing"
          - name: "invalidTextTakeFirst"
            value:
              type: ":Nothing"
          - name: "invalidTagTakeFirst"
            value:
              type: ":Nothing"
          - name: "invalidVectorTakeFirst"
            value:
              type: ":Nothing"
          - name: "invalidPointTakeFirst"
            value:
              type: ":Nothing"
          - name: "invalidMapTakeFirst"
            value:
              type: ":Nothing"
```

---

## Test: take drop iterator boundary counts

This runtime case exercises “take drop iterator boundary counts” and verifies the declared messages, values, and execution result.

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
  - name: "take drop iterator boundary counts.ges"
    program: main
```

### Source code under test

```ges
module atomictakedropiteratorboundaries
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
          - name: "takeFirstTooMany"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
          - name: "dropFirstTooManyLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "takeLastTooMany"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
          - name: "dropLastTooManyLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "takeHighestTooMany"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "3"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "1"
          - name: "dropHighestTooManyLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "takeLowestTooMany"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
          - name: "dropLowestTooManyLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "emptyTakeFirstLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "emptyDropLastLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "emptyTakeHighestLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "emptyDropLowestLen"
            value:
              type: ":Number.int64"
              value: "0"
```

---

## Test: weighted choose lowers to iterator weighted terminals

This runtime case exercises “weighted choose lowers to iterator weighted terminals” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["9", "0", "8.99999", "0"]
sources:
  - name: "weighted choose lowers to iterator weighted terminals.ges"
    program: main
```

### Source code under test

```ges
module atomicweightedchooselowering
on Start {
  let units be [[name: 'A', weight: 1], [name: 'B', weight: 3], [name: 'C', weight: 6], [name: 'Ignored', weight: 0]]
  let liveUnits be units[:filter unit where unit.weight > 0]
  let weightedOne be liveUnits[:choose 1 weighted by unit => unit.weight]
  let weightedTwo be liveUnits[:choose 2 weighted by unit => unit.weight]
  let weightedFilteredOne be units[:choose 1 unit where unit.name <> 'Ignored' weighted by unit => unit.weight]
  emit Done(weightedOne: weightedOne.name, weighted1: weightedTwo[1].name, weighted2: weightedTwo[2].name, weightedFilteredOne: weightedFilteredOne.name)
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
          - name: "weightedOne"
            value:
              type: ":Text"
              value: "C"
          - name: "weighted1"
            value:
              type: ":Text"
              value: "A"
          - name: "weighted2"
            value:
              type: ":Text"
              value: "C"
          - name: "weightedFilteredOne"
            value:
              type: ":Text"
              value: "A"
```

---

## Test: weighted choose ignores empty non positive and invalid weights

This runtime case exercises “weighted choose ignores empty non positive and invalid weights” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["0", "0", "0"]
sources:
  - name: "weighted choose ignores empty non positive and invalid weights.ges"
    program: main
```

### Source code under test

```ges
module atomicweightedchooseinvalidweights
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
  emit Done(emptyOneName: emptyOne.name, emptyManyLen: emptyMany[:count], invalidOneName: invalidOne.name, invalidManyLen: invalidMany[:count], mixedOneName: mixedOne.name, mixedManyLen: mixedMany[:count], mixed1: mixedMany[1].name, mixed2: mixedMany[2].name, mixed3: mixedMany[3].name)
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
          - name: "emptyOneName"
            value:
              type: ":Nothing"
          - name: "emptyManyLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "invalidOneName"
            value:
              type: ":Nothing"
          - name: "invalidManyLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "mixedOneName"
            value:
              type: ":Text"
              value: "A"
          - name: "mixedManyLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "mixed1"
            value:
              type: ":Text"
              value: "A"
          - name: "mixed2"
            value:
              type: ":Text"
              value: "B"
          - name: "mixed3"
            value:
              type: ":Nothing"
```

---

## Test: weighted choose duplicate weights preserve deterministic iterator order

This runtime case exercises “weighted choose duplicate weights preserve deterministic iterator order” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomicweightedchooseduplicateweights
on Start {
  let units be [[name: 'A', weight: 1], [name: 'B', weight: 1], [name: 'C', weight: 1], [name: 'D', weight: 1]]
  let weightedOne be units[:choose 1 weighted by unit => unit.weight]
  let weightedAll be units[:choose 4 weighted by unit => unit.weight]
  let weightedBoundary be units[:choose 2 weighted by unit => unit.weight]
  emit Done(one: weightedOne.name, all1: weightedAll[1].name, all2: weightedAll[2].name, all3: weightedAll[3].name, all4: weightedAll[4].name, boundary1: weightedBoundary[1].name, boundary2: weightedBoundary[2].name)
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
          - name: "one"
            value:
              type: ":Text"
              value: "A"
          - name: "all1"
            value:
              type: ":Text"
              value: "A"
          - name: "all2"
            value:
              type: ":Text"
              value: "B"
          - name: "all3"
            value:
              type: ":Text"
              value: "C"
          - name: "all4"
            value:
              type: ":Text"
              value: "D"
          - name: "boundary1"
            value:
              type: ":Text"
              value: "B"
          - name: "boundary2"
            value:
              type: ":Text"
              value: "C"
```

---

## Test: weighted choose supports captured weight expressions

This runtime case exercises “weighted choose supports captured weight expressions” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomicweightedchoosecapturedweights
on Start {
  let scale be 2
  let bonus be 1
  let units be [[name: 'Low', weight: 1], [name: 'High', weight: 3], [name: 'Zero', weight: 0]]
  let weightedOne be units[:choose 1 weighted by unit => unit.weight * scale]
  let weightedTwo be units[:choose 2 weighted by unit => unit.weight + bonus]
  emit Done(one: weightedOne.name, two1: weightedTwo[1].name, two2: weightedTwo[2].name, twoLen: weightedTwo[:count])
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
          - name: "one"
            value:
              type: ":Text"
              value: "High"
          - name: "two1"
            value:
              type: ":Text"
              value: "Low"
          - name: "two2"
            value:
              type: ":Text"
              value: "High"
          - name: "twoLen"
            value:
              type: ":Number.int64"
              value: "2"
```

---

## Test: choose lowers to first take first and random take

This runtime case exercises “choose lowers to first take first and random take” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomicchooselowering
on Start {
  let values be [1, 2, 3, 4, 5]
  let dice be ([6, 5, 4, 3]) as :Dice
  let range be (from 10 to 14) as :Range
  emit Done(chooseOne: values[:choose 1], chooseTwo: values[:choose 2], chooseFilteredOne: values[:choose 1 value where value > 2], chooseFilteredTwo: values[:choose 2 value where value > 2], randomOne: values[:choose 1 at random], randomTwo: values[:choose 2 at random], randomFilteredTwo: values[:choose 2 at random value where value > 2], diceRandomTwo: dice[:choose 2 at random], rangeRandomTwo: range[:choose 2 at random])
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
          - name: "chooseOne"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "chooseTwo"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
          - name: "chooseFilteredOne"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "chooseFilteredTwo"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "3"
                - type: ":Number.int64"
                  value: "4"
          - name: "randomOne"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "randomTwo"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "5"
                - type: ":Number.int64"
                  value: "4"
          - name: "randomFilteredTwo"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "4"
                - type: ":Number.int64"
                  value: "5"
          - name: "diceRandomTwo"
            value:
              type: ":Dice"
              rolls:
                - 6
                - 4
          - name: "rangeRandomTwo"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "12"
                - type: ":Number.int64"
                  value: "14"
```

---

## Test: random choose boundary source kinds

This runtime case exercises “random choose boundary source kinds” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomicrandomchooseboundaries
on Start {
  let values be [1, 2, 3]
  let emptyList be []
  let dice be ([6, 4, 2]) as :Dice
  let emptyDice be ([]) as :Dice
  let range be (from 10 to 12) as :Range
  let emptyIterator be values[:filter value where false]
  emit Done(listOne: values[:choose 1 at random], listTakeTooMany: values[:choose 9 at random], emptyListOne: emptyList[:choose 1 at random], emptyListTakeLen: emptyList[:choose 2 at random][:count], diceOne: dice[:choose 1 at random], diceTakeTooMany: dice[:choose 9 at random], emptyDiceOne: emptyDice[:choose 1 at random], emptyDiceTakeLen: emptyDice[:choose 2 at random][:count], rangeOne: range[:choose 1 at random], rangeTakeTooMany: range[:choose 9 at random], iteratorTakeTooMany: values[:filter value where true][:choose 9 at random], emptyIteratorOne: emptyIterator[:choose 1 at random], emptyIteratorTakeLen: emptyIterator[:choose 2 at random][:count], invalidIntOne: 10[:choose 1 at random], invalidIntTake: 10[:choose 2 at random], invalidMapTake: [a: 1][:choose 2 at random])
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
          - name: "listOne"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "listTakeTooMany"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
          - name: "emptyListOne"
            value:
              type: ":Nothing"
          - name: "emptyListTakeLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "diceOne"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "diceTakeTooMany"
            value:
              type: ":Dice"
              rolls:
                - 6
                - 4
                - 2
          - name: "emptyDiceOne"
            value:
              type: ":Nothing"
          - name: "emptyDiceTakeLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "rangeOne"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "rangeTakeTooMany"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "10"
                - type: ":Number.int64"
                  value: "11"
                - type: ":Number.int64"
                  value: "12"
          - name: "iteratorTakeTooMany"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
          - name: "emptyIteratorOne"
            value:
              type: ":Nothing"
          - name: "emptyIteratorTakeLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "invalidIntOne"
            value:
              type: ":Nothing"
          - name: "invalidIntTake"
            value:
              type: ":Nothing"
          - name: "invalidMapTake"
            value:
              type: ":Nothing"
```

---

## Test: take drop highest lowest direct fast paths

This runtime case exercises “take drop highest lowest direct fast paths” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomictakedrophighestlowestdirectfastpaths
on Start {
  let values be [1, 2, 3, 4, 5]
  let dice be ([6, 4, 2, 1]) as :Dice
  let range be (from 1 to 5) as :Range
  emit Done(listTakeHighest: values[:take highest 2], listTakeLowest: values[:take lowest 2], listDropHighest: values[:drop highest 2], listDropLowest: values[:drop lowest 2], diceTakeHighest: dice[:take highest 2], diceTakeLowest: dice[:take lowest 2], diceDropHighest: dice[:drop highest 2], diceDropLowest: dice[:drop lowest 2], rangeTakeHighest: range[:take highest 2], rangeTakeLowest: range[:take lowest 2], rangeDropHighest: range[:drop highest 2], rangeDropLowest: range[:drop lowest 2])
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
          - name: "listTakeHighest"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "5"
                - type: ":Number.int64"
                  value: "4"
          - name: "listTakeLowest"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
          - name: "listDropHighest"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
          - name: "listDropLowest"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "3"
                - type: ":Number.int64"
                  value: "4"
                - type: ":Number.int64"
                  value: "5"
          - name: "diceTakeHighest"
            value:
              type: ":Dice"
              rolls:
                - 6
                - 4
          - name: "diceTakeLowest"
            value:
              type: ":Dice"
              rolls:
                - 2
                - 1
          - name: "diceDropHighest"
            value:
              type: ":Dice"
              rolls:
                - 2
                - 1
          - name: "diceDropLowest"
            value:
              type: ":Dice"
              rolls:
                - 6
                - 4
          - name: "rangeTakeHighest"
            value:
              type: ":Range.int64"
              from: "5"
              to: "4"
              step: "-1"
          - name: "rangeTakeLowest"
            value:
              type: ":Range.int64"
              from: "1"
              to: "2"
              step: "1"
          - name: "rangeDropHighest"
            value:
              type: ":Range.int64"
              from: "1"
              to: "3"
              step: "1"
          - name: "rangeDropLowest"
            value:
              type: ":Range.int64"
              from: "3"
              to: "5"
              step: "1"
```

---

## Test: iterator collect map and map value

This runtime case exercises “iterator collect map and map value” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomicmapselectorloop
on Start {
  let units be [[id: #rook, hp: 10, team: #blue], [id: #mage, hp: 6, team: #red], [id: #rook, hp: 12, team: #green], [id: '', hp: 99, team: #hidden]]
  let emptyUnits be []
  let dice be ([6, 4, 2]) as :Dice
  let range be (from 1 to 3) as :Range
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
          - name: "byIdCount"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "rookHp"
            value:
              type: ":Number.int64"
              value: "12"
          - name: "rookTeam"
            value:
              type: ":Tag"
              value: "green"
          - name: "mageHp"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "hpByIdCount"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "hpRook"
            value:
              type: ":Number.int64"
              value: "12"
          - name: "hpMage"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "missingEmpty"
            value:
              type: ":Nothing"
          - name: "emptyByIdLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "diceMapLen"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "diceSix"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "diceFour"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "rangeMapLen"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "rangeTwo"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "textMapLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "textA"
            value:
              type: ":Text"
              value: "a"
          - name: "textB"
            value:
              type: ":Text"
              value: "b"
          - name: "mapValueLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "mapValueOne"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "mapValueTwo"
            value:
              type: ":Number.int64"
              value: "20"
          - name: "invalidIntegerMapLen"
            value:
              type: ":Number.int64"
              value: "0"
```

---

## Test: iterator collect map duplicate keys use last value

This runtime case exercises “iterator collect map duplicate keys use last value” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomicmapselectorduplicatekeys
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
          - name: "byIdLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "rookHp"
            value:
              type: ":Number.int64"
              value: "12"
          - name: "rookTeam"
            value:
              type: ":Tag"
              value: "green"
          - name: "mageHp"
            value:
              type: ":Number.int64"
              value: "8"
          - name: "mageTeam"
            value:
              type: ":Tag"
              value: "gold"
          - name: "hpByIdLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "hpRook"
            value:
              type: ":Number.int64"
              value: "12"
          - name: "hpMage"
            value:
              type: ":Number.int64"
              value: "8"
          - name: "iteratorRookHp"
            value:
              type: ":Number.int64"
              value: "12"
          - name: "iteratorMageTeam"
            value:
              type: ":Tag"
              value: "gold"
```

---

## Test: iterator collect map skips empty keys and keeps projected nothing values

This runtime case exercises “iterator collect map skips empty keys and keeps projected nothing values” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomicmapselectorinvalidkeysandvalues
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
          - name: "byIdLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "rookHp"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "mageHp"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "emptyKey"
            value:
              type: ":Nothing"
          - name: "missingKey"
            value:
              type: ":Nothing"
          - name: "missingByIdLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "missingRook"
            value:
              type: ":Nothing"
          - name: "missingMage"
            value:
              type: ":Nothing"
          - name: "textKeyLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "textA"
            value:
              type: ":Text"
              value: "a"
          - name: "textB"
            value:
              type: ":Text"
              value: "b"
          - name: "textEmpty"
            value:
              type: ":Nothing"
```

---

## Test: first last single direct and iterators

This runtime case exercises “first last single direct and iterators” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomicfirstlastsingle
on Start {
  let values be [1, 2, 3, 4]
  let emptyList be []
  let dice be ([6, 4, 2]) as :Dice
  let range be (from 1 to 3) as :Range
  let text be 'ab'
  let map be [a: 1, b: 2]
  emit Done(first: values[:first], firstFiltered: values[:first value where value > 2], firstEmpty: emptyList[:first], last: values[:last], lastFiltered: values[:last value where value < 4], lastEmpty: emptyList[:last], single: values[:single value where value = 3], singleNone: values[:single value where value > 9], singleMany: values[:single value where value > 1], diceFirst: dice[:first], diceLast: dice[:last], rangeFirst: range[:first], rangeLast: range[:last], textFirst: text[:first], textLast: text[:last], textSingle: 'x'[:single], mapFirst: map[:first], mapLast: map[:last], listSingle: [9][:single])
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
          - name: "first"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "firstFiltered"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "firstEmpty"
            value:
              type: ":Nothing"
          - name: "last"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "lastFiltered"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "lastEmpty"
            value:
              type: ":Nothing"
          - name: "single"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "singleNone"
            value:
              type: ":Nothing"
          - name: "singleMany"
            value:
              type: ":Nothing"
          - name: "diceFirst"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "diceLast"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "rangeFirst"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "rangeLast"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "textFirst"
            value:
              type: ":Text"
              value: "a"
          - name: "textLast"
            value:
              type: ":Text"
              value: "b"
          - name: "textSingle"
            value:
              type: ":Text"
              value: "x"
          - name: "mapFirst"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "mapLast"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "listSingle"
            value:
              type: ":Number.int64"
              value: "9"
```

---

## Test: range iterators stop at numeric boundaries

This runtime case exercises “range iterators stop at numeric boundaries” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomicrangeiteratorboundaries
on Start(maximum, minimum, huge) {
  let maximumRange be (from maximum to maximum step 1) as :Range
  let minimumRange be (from minimum to minimum step -1) as :Range
  let hugeRange be (from huge to huge step 1.0) as :Range
  let wrongAscending be (from 3 to 1 step 1) as :Range
  let wrongDescending be (from 1 to 3 step -1) as :Range
  let zeroStep be (from 1 to 1 step 0) as :Range
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

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "maximum"
          value:
            type: ":Number.int64"
            value: "9223372036854775807"
        - name: "minimum"
          value:
            type: ":Number.int64"
            value: "-9223372036854775808"
        - name: "huge"
          value:
            type: ":Number.binary64"
            value: "1e308"
    local:
      - name: "Done"
        args:
          - name: "maximumCount"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "maximumValue"
            value:
              type: ":Number.int64"
              value: "9223372036854775807"
          - name: "minimumCount"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "minimumValue"
            value:
              type: ":Number.int64"
              value: "-9223372036854775808"
          - name: "hugeCount"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "hugeValue"
            value:
              type: ":Number.binary64"
              value: "1e308"
          - name: "wrongAscendingCount"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "wrongDescendingCount"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "zeroStepCount"
            value:
              type: ":Number.int64"
              value: "0"
```
