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

---

## Test: nested projections preserve their outer binding

This case reads each outer item in both inner iterations, including after a later literal is evaluated.

### Case description

```yaml
gesBlock: case
id: nested-projection-capture
kind: scriptApi
level: scenario
compile:
  binaryRoundTrip: true
sources:
  - name: nested-projection-capture.ges
    program: main
```

### Source code under test

```ges
on Start {
  emit Done(value: [10, 20][:select x => [1, 2][:select y => x + y + 1]])
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
    input: { args: [] }
    local:
      - name: Done
        args:
          - name: value
            value: { type: ":List", items: [{ type: ":List", items: [{ type: ":Number.int64", value: "12" }, { type: ":Number.int64", value: "13" }] }, { type: ":List", items: [{ type: ":Number.int64", value: "22" }, { type: ":Number.int64", value: "23" }] }] }
```

---

## Test: three nested projections preserve both outer bindings

This case keeps each enclosing binding stable across every inner loop back edge.

### Case description

```yaml
gesBlock: case
id: triple-projection-capture
kind: scriptApi
level: scenario
compile:
  binaryRoundTrip: true
sources:
  - name: triple-projection-capture.ges
    program: main
```

### Source code under test

```ges
on Start {
  emit Done(value: [10, 20][:select x => [1, 2][:select y => [3, 4][:select z => x + y + z + 1]]])
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
    input: { args: [] }
    local:
      - name: Done
        args:
          - name: value
            value: { type: ":List", items: [{ type: ":List", items: [{ type: ":List", items: [{ type: ":Number.int64", value: "15" }, { type: ":Number.int64", value: "16" }] }, { type: ":List", items: [{ type: ":Number.int64", value: "16" }, { type: ":Number.int64", value: "17" }] }] }, { type: ":List", items: [{ type: ":List", items: [{ type: ":Number.int64", value: "25" }, { type: ":Number.int64", value: "26" }] }, { type: ":List", items: [{ type: ":Number.int64", value: "26" }, { type: ":Number.int64", value: "27" }] }] }] }
```

---

## Test: nested aggregation preserves its outer binding

This case sums the projections using the same outer value on each inner iteration.

### Case description

```yaml
gesBlock: case
id: nested-aggregation-capture
kind: scriptApi
level: scenario
compile:
  binaryRoundTrip: true
sources:
  - name: nested-aggregation-capture.ges
    program: main
```

### Source code under test

```ges
on Start {
  emit Done(value: [10, 20][:select x => [1, 2][:sum y => x + y + 1]])
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
    input: { args: [] }
    local:
      - name: Done
        args:
          - name: value
            value: { type: ":List", items: [{ type: ":Number.int64", value: "25" }, { type: ":Number.int64", value: "45" }] }
```

---

## Test: a fused projection remains stable inside a nested selector

This case captures a computed pipeline value rather than the original iterator item.

### Case description

```yaml
gesBlock: case
id: fused-projection-capture
kind: scriptApi
level: scenario
compile:
  binaryRoundTrip: true
sources:
  - name: fused-projection-capture.ges
    program: main
```

### Source code under test

```ges
on Start {
  emit Done(value: [10, 20][:select item => item + 100][:select x => [1, 2][:select y => x + y + 1]])
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
    input: { args: [] }
    local:
      - name: Done
        args:
          - name: value
            value: { type: ":List", items: [{ type: ":List", items: [{ type: ":Number.int64", value: "112" }, { type: ":Number.int64", value: "113" }] }, { type: ":List", items: [{ type: ":Number.int64", value: "122" }, { type: ":Number.int64", value: "123" }] }] }
```

---

## Test: choose accepts the largest signed immediate count

This case checks the supported count boundary for deterministic, random, filtered and weighted choice without depending on random order.

### Case description

```yaml
gesBlock: case
id: choose-maximum-count
kind: scriptApi
level: scenario
compile:
  binaryRoundTrip: true
sources:
  - name: choose-maximum-count.ges
    program: main
```

### Source code under test

```ges
on Start {
  emit Done(value: [[1, 2, 3][:choose 32767][:count], [1, 2, 3][:choose 32767 at random][:count], [1, 2, 3][:choose 32767 item where item > 1][:count], [1, 2, 3][:choose 32767 weighted by item => item][:count]])
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
    input: { args: [] }
    local:
      - name: Done
        args:
          - name: value
            value: { type: ":List", items: [{ type: ":Number.int64", value: "3" }, { type: ":Number.int64", value: "3" }, { type: ":Number.int64", value: "2" }, { type: ":Number.int64", value: "3" }] }
```

---

## Test: take drop boolean tag order

This runtime case checks Boolean/Tag selection in both input orders through direct lists and iterator pipelines.

### Case description

```yaml
gesBlock: case
id: take-drop-boolean-tag-order
kind: scriptApi
level: atomic
sources:
  - name: "take-drop-boolean-tag-order.ges"
    program: main
```

### Source code under test

```ges
on Start {
  for values in [[true, #tag], [#tag, true], [false, #tag], [#tag, false]] {
    emit Direct(values: [values[:take highest 1][1], values[:take lowest 1][1], values[:drop highest 1], values[:drop lowest 1]])
    emit Iterator(values: [values[:filter item where true][:take highest 1][1], values[:filter item where true][:take lowest 1][1], values[:filter item where true][:drop highest 1], values[:filter item where true][:drop lowest 1]])
  }
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: "expect"
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Direct"
        args:
          - name: "values"
            value:
              type: ":List"
              items:
                - type: ":Tag"
                  value: "tag"
                - type: ":Boolean"
                  value: true
                - type: ":List"
                  items:
                    - type: ":Boolean"
                      value: true
                - type: ":List"
                  items:
                    - type: ":Tag"
                      value: "tag"
      - name: "Iterator"
        args:
          - name: "values"
            value:
              type: ":List"
              items:
                - type: ":Tag"
                  value: "tag"
                - type: ":Boolean"
                  value: true
                - type: ":List"
                  items:
                    - type: ":Boolean"
                      value: true
                - type: ":List"
                  items:
                    - type: ":Tag"
                      value: "tag"
      - name: "Direct"
        args:
          - name: "values"
            value:
              type: ":List"
              items:
                - type: ":Tag"
                  value: "tag"
                - type: ":Boolean"
                  value: true
                - type: ":List"
                  items:
                    - type: ":Boolean"
                      value: true
                - type: ":List"
                  items:
                    - type: ":Tag"
                      value: "tag"
      - name: "Iterator"
        args:
          - name: "values"
            value:
              type: ":List"
              items:
                - type: ":Tag"
                  value: "tag"
                - type: ":Boolean"
                  value: true
                - type: ":List"
                  items:
                    - type: ":Boolean"
                      value: true
                - type: ":List"
                  items:
                    - type: ":Tag"
                      value: "tag"
      - name: "Direct"
        args:
          - name: "values"
            value:
              type: ":List"
              items:
                - type: ":Tag"
                  value: "tag"
                - type: ":Boolean"
                  value: false
                - type: ":List"
                  items:
                    - type: ":Boolean"
                      value: false
                - type: ":List"
                  items:
                    - type: ":Tag"
                      value: "tag"
      - name: "Iterator"
        args:
          - name: "values"
            value:
              type: ":List"
              items:
                - type: ":Tag"
                  value: "tag"
                - type: ":Boolean"
                  value: false
                - type: ":List"
                  items:
                    - type: ":Boolean"
                      value: false
                - type: ":List"
                  items:
                    - type: ":Tag"
                      value: "tag"
      - name: "Direct"
        args:
          - name: "values"
            value:
              type: ":List"
              items:
                - type: ":Tag"
                  value: "tag"
                - type: ":Boolean"
                  value: false
                - type: ":List"
                  items:
                    - type: ":Boolean"
                      value: false
                - type: ":List"
                  items:
                    - type: ":Tag"
                      value: "tag"
      - name: "Iterator"
        args:
          - name: "values"
            value:
              type: ":List"
              items:
                - type: ":Tag"
                  value: "tag"
                - type: ":Boolean"
                  value: false
                - type: ":List"
                  items:
                    - type: ":Boolean"
                      value: false
                - type: ":List"
                  items:
                    - type: ":Tag"
                      value: "tag"
```

---

## Test: take drop record map order

This runtime case checks Record/Map selection, stable Record ties, retained source order after dropping, and exact result types for direct lists and iterator pipelines.

### Case description

```yaml
gesBlock: case
id: take-drop-record-map-order
kind: scriptApi
level: atomic
sources:
  - name: "take-drop-record-map-order.ges"
    program: main
```

### Source code under test

```ges
record :Box as { value: :Number }
on Start {
  for values in [[:Box(value: 1), [value: 2], :Box(value: 3)], [[value: 2], :Box(value: 3), :Box(value: 1)]] {
    emit Direct(values: [values[:take highest 1][1], values[:take lowest 1][1], values[:drop highest 1], values[:drop lowest 1]])
    emit Iterator(values: [values[:filter item where true][:take highest 1][1], values[:filter item where true][:take lowest 1][1], values[:filter item where true][:drop highest 1], values[:filter item where true][:drop lowest 1]])
  }
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: "expect"
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Direct"
        args:
          - name: "values"
            value:
              type: ":List"
              items:
                - type: ":Box"
                  entries:
                    - key: "value"
                      value:
                        type: ":Number.int64"
                        value: "1"
                - type: ":Map"
                  entries:
                    - key: "value"
                      value:
                        type: ":Number.int64"
                        value: "2"
                - type: ":List"
                  items:
                    - type: ":Map"
                      entries:
                        - key: "value"
                          value:
                            type: ":Number.int64"
                            value: "2"
                    - type: ":Box"
                      entries:
                        - key: "value"
                          value:
                            type: ":Number.int64"
                            value: "3"
                - type: ":List"
                  items:
                    - type: ":Box"
                      entries:
                        - key: "value"
                          value:
                            type: ":Number.int64"
                            value: "1"
                    - type: ":Box"
                      entries:
                        - key: "value"
                          value:
                            type: ":Number.int64"
                            value: "3"
      - name: "Iterator"
        args:
          - name: "values"
            value:
              type: ":List"
              items:
                - type: ":Box"
                  entries:
                    - key: "value"
                      value:
                        type: ":Number.int64"
                        value: "1"
                - type: ":Map"
                  entries:
                    - key: "value"
                      value:
                        type: ":Number.int64"
                        value: "2"
                - type: ":List"
                  items:
                    - type: ":Map"
                      entries:
                        - key: "value"
                          value:
                            type: ":Number.int64"
                            value: "2"
                    - type: ":Box"
                      entries:
                        - key: "value"
                          value:
                            type: ":Number.int64"
                            value: "3"
                - type: ":List"
                  items:
                    - type: ":Box"
                      entries:
                        - key: "value"
                          value:
                            type: ":Number.int64"
                            value: "1"
                    - type: ":Box"
                      entries:
                        - key: "value"
                          value:
                            type: ":Number.int64"
                            value: "3"
      - name: "Direct"
        args:
          - name: "values"
            value:
              type: ":List"
              items:
                - type: ":Box"
                  entries:
                    - key: "value"
                      value:
                        type: ":Number.int64"
                        value: "3"
                - type: ":Map"
                  entries:
                    - key: "value"
                      value:
                        type: ":Number.int64"
                        value: "2"
                - type: ":List"
                  items:
                    - type: ":Map"
                      entries:
                        - key: "value"
                          value:
                            type: ":Number.int64"
                            value: "2"
                    - type: ":Box"
                      entries:
                        - key: "value"
                          value:
                            type: ":Number.int64"
                            value: "1"
                - type: ":List"
                  items:
                    - type: ":Box"
                      entries:
                        - key: "value"
                          value:
                            type: ":Number.int64"
                            value: "3"
                    - type: ":Box"
                      entries:
                        - key: "value"
                          value:
                            type: ":Number.int64"
                            value: "1"
      - name: "Iterator"
        args:
          - name: "values"
            value:
              type: ":List"
              items:
                - type: ":Box"
                  entries:
                    - key: "value"
                      value:
                        type: ":Number.int64"
                        value: "3"
                - type: ":Map"
                  entries:
                    - key: "value"
                      value:
                        type: ":Number.int64"
                        value: "2"
                - type: ":List"
                  items:
                    - type: ":Map"
                      entries:
                        - key: "value"
                          value:
                            type: ":Number.int64"
                            value: "2"
                    - type: ":Box"
                      entries:
                        - key: "value"
                          value:
                            type: ":Number.int64"
                            value: "1"
                - type: ":List"
                  items:
                    - type: ":Box"
                      entries:
                        - key: "value"
                          value:
                            type: ":Number.int64"
                            value: "3"
                    - type: ":Box"
                      entries:
                        - key: "value"
                          value:
                            type: ":Number.int64"
                            value: "1"
```

---

## Test: compact selector boundaries

This case preserves ordinary pipeline results and recognizes argumentless selectors and split boundaries inside a single bracket.

### Case description

```yaml
gesBlock: case
id: compact-selector-boundaries
kind: scriptApi
level: atomic
sources:
  - name: compact.ges
    program: main
```

### Source code under test

```ges
on Start {
    emit Done(value: [1, 2, 3][:filter x where x > 1 :select x => x * 2 :sum])
    emit Done(value: [1, 2, 3][:reverse :first])
    emit Done(value: 'a b c'[:split on whitespace :count])
    emit Done(value: [b: 2, a: 1][:entries :select x => x.value :sum])
    emit Done(value: [1, 2][:count :select x => x])
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Number.int64', value: '10' }
      - name: Done
        args:
          - name: value
            value: { type: ':Number.int64', value: '3' }
      - name: Done
        args:
          - name: value
            value: { type: ':Number.int64', value: '3' }
      - name: Done
        args:
          - name: value
            value: { type: ':Number.int64', value: '3' }
      - name: Done
        args:
          - name: value
            value: { type: ':Nothing' }
```

---

## Test: cartesian-projection

This case verifies cartesian projection through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: cartesian-projection
kind: scriptApi
level: atomic
sources:
  - name: "cartesian-projection.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [:cartesian [1, 2], [3, 4]][:select a, b => a + b] = [4, 5, 5, 6])
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: cartesian-materialized

This case verifies cartesian materialized through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: cartesian-materialized
kind: scriptApi
level: atomic
sources:
  - name: "cartesian-materialized.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [:cartesian [1, 2], [3, 4]] = [[1, 3], [1, 4], [2, 3], [2, 4]])
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: cartesian-filter-preserves-extra

This case verifies cartesian filter preserves extra through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: cartesian-filter-preserves-extra
kind: scriptApi
level: atomic
sources:
  - name: "cartesian-filter-preserves-extra.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [:cartesian [1, 2], [3], [4]][:filter a, b where a = 1][:select a, b, c => c] = [4])
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: cartesian-filter-materializes

This case verifies cartesian filter materializes through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: cartesian-filter-materializes
kind: scriptApi
level: atomic
sources:
  - name: "cartesian-filter-materializes.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [:cartesian [1, 2], [3], [4]][:filter a, b where a = 1] = [[1, 3, 4]])
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: cartesian-first

This case verifies cartesian first through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: cartesian-first
kind: scriptApi
level: atomic
sources:
  - name: "cartesian-first.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [:cartesian [1, 2], [3, 4]][:first] = [1, 3])
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: cartesian-count

This case verifies cartesian count through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: cartesian-count
kind: scriptApi
level: atomic
sources:
  - name: "cartesian-count.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [:cartesian [1, 2], [3, 4]][:count] = 4)
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: cartesian-sum

This case verifies cartesian sum through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: cartesian-sum
kind: scriptApi
level: atomic
sources:
  - name: "cartesian-sum.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [:cartesian [1, 2], [3, 4]][:sum a, b => a + b] = 20)
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: cartesian-infix

This case verifies cartesian infix through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: cartesian-infix
kind: scriptApi
level: atomic
sources:
  - name: "cartesian-infix.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [1, 2] * [3] * [4] = [[[1, 3], 4], [[2, 3], 4]])
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: lockstep-shortest

This case verifies lockstep shortest through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: lockstep-shortest
kind: scriptApi
level: atomic
sources:
  - name: "lockstep-shortest.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [:lockstep [1, 2, 3], [4, 5]][:select a, b => a + b] = [5, 7])
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: zip-selector-alias

This case verifies zip selector alias through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: zip-selector-alias
kind: scriptApi
level: atomic
sources:
  - name: "zip-selector-alias.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [1, 2][:zip [3]][:select a, b => a + b] = [4])
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: entries-components

This case verifies entries components through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: entries-components
kind: scriptApi
level: atomic
sources:
  - name: "entries-components.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [b: 2, a: 1][:entries :filter key, value where key = 'a' :select key, value => value] = [1])
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: entries-whole-item

This case verifies entries whole item through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: entries-whole-item
kind: scriptApi
level: atomic
sources:
  - name: "entries-whole-item.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [b: 2, a: 1][:entries :filter key, value where key = 'a'] = [[key: 'a', value: 1]])
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: bindings-missing-and-scalar

This case verifies bindings missing and scalar through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: bindings-missing-and-scalar
kind: scriptApi
level: atomic
sources:
  - name: "bindings-missing-and-scalar.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [[1, 2], [3], 4][:select a, b => [a, b]] = [[1, 2], [3, nothing], [4, nothing]])
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: bindings-map-sorted

This case verifies bindings map sorted through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: bindings-map-sorted
kind: scriptApi
level: atomic
sources:
  - name: "bindings-map-sorted.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [[z: 9, a: 2]][:select first, second => first - second] = [-7])
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: bindings-fold

This case verifies bindings fold through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: bindings-fold
kind: scriptApi
level: atomic
sources:
  - name: "bindings-fold.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [[1, 2], [3, 4]][:fold acc be 0, a, b => acc + a + b] = 10)
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: foreach-empty

This case verifies foreach empty through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: foreach-empty
kind: scriptApi
level: atomic
sources:
  - name: "foreach-empty.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [][:foreach item => item] is nothing)
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: foreach-components

This case verifies foreach components through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: foreach-components
kind: scriptApi
level: atomic
sources:
  - name: "foreach-components.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [:cartesian [1], [2]][:foreach a, b => a + b] is nothing)
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: union-multiset

This case verifies union multiset through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: union-multiset
kind: scriptApi
level: atomic
sources:
  - name: "union-multiset.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [:union [1, 2], [2, 3]][:select x => x] = [1, 2, 2, 3])
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: intersect-multiset

This case verifies intersect multiset through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: intersect-multiset
kind: scriptApi
level: atomic
sources:
  - name: "intersect-multiset.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [:intersect [1, 2, 2, 3], [2, 2, 4]][:select x => x] = [2, 2])
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: difference-scalar-nothing

This case verifies difference scalar nothing through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: difference-scalar-nothing
kind: scriptApi
level: atomic
sources:
  - name: "difference-scalar-nothing.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [:difference [1, nothing, nothing], nothing][:select x => x] = [1, nothing])
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: union-map-result

This case verifies union map result through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: union-map-result
kind: scriptApi
level: atomic
sources:
  - name: "union-map-result.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [:union [a: 1], [a: 2, b: 3]] = [a: 2, b: 3])
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: union-map-pipeline-values

This case verifies union map pipeline values through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: union-map-pipeline-values
kind: scriptApi
level: atomic
sources:
  - name: "union-map-pipeline-values.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    emit Done(value: [:union [a: 1], [a: 2, b: 3]][:select x => x] = [2, 3])
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: component-asm

This case verifies component asm through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: component-asm
kind: scriptApi
level: atomic
sources:
  - name: "component-asm.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    let a be [1, 2]
    let b be [3, 4]
    let total be asm {
        .register iterator
        .register left
        .register right
        LoadInteger total, 0
        IteratorCreateOrJump iterator, [a, b], invalid, #cartesian
        next:
        IteratorNext [left, right], iterator, done
        Add total, total, left
        Add total, total, right
        Jump next
        done:
        IteratorClose iterator
        Jump exit
        invalid:
        LoadNothing total
        exit:
    }
    emit Done(value: total = 20)
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: component-for

This case verifies component for through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: component-for
kind: scriptApi
level: atomic
sources:
  - name: "component-for.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start {
    for a, b in [:cartesian [1, 2], [3]] emit Done(value: a + b > 3)
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: cartesian-fold

This case verifies cartesian fold through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: cartesian-fold
kind: scriptApi
level: atomic
sources:
  - name: "cartesian-fold.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start { emit Done(value: [:cartesian [1, 2], [3, 4]][:fold acc be 0, a, b => acc + a + b] = 20) }
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: component-choose

This case verifies component choose through the source compiler and runtime.

### Case description

```yaml
gesBlock: case
id: component-choose
kind: scriptApi
level: atomic
sources:
  - name: "component-choose.ges"
    program: main
```

### Source code under test

```ges
module collectionbindings
on Start { emit Done(value: [[1, 2], [3, 4]][:choose 1 a, b where a = 3] = [3, 4]) }
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: invalid-cartesian-skips-source

This case checks invalid cartesian skips source without relying on implementation-specific instruction shapes.

### Case description

```yaml
gesBlock: case
id: invalid-cartesian-skips-source
kind: scriptApi
level: atomic
sources:
  - name: "invalid-cartesian-skips-source.ges"
    program: main
```

### Source code under test

```ges
module collectionedges
on Start {
    let value be [:cartesian nothing, [emit Unexpected()]]
    emit Done(value: value is nothing)
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: empty-cartesian-evaluates-sources

This case checks empty cartesian evaluates sources without relying on implementation-specific instruction shapes.

### Case description

```yaml
gesBlock: case
id: empty-cartesian-evaluates-sources
kind: scriptApi
level: atomic
sources:
  - name: "empty-cartesian-evaluates-sources.ges"
    program: main
```

### Source code under test

```ges
module collectionedges
on Start {
    let value be [:cartesian [], [emit First()], [emit Second()]]
    emit Done(value: value = [])
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
    input:
      args: []
    local:
      - name: First
        args: []
      - name: Second
        args: []
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: invalid-union-prefix-skips-source

This case checks invalid union prefix skips source without relying on implementation-specific instruction shapes.

### Case description

```yaml
gesBlock: case
id: invalid-union-prefix-skips-source
kind: scriptApi
level: atomic
sources:
  - name: "invalid-union-prefix-skips-source.ges"
    program: main
```

### Source code under test

```ges
module collectionedges
on Start {
    let value be [:union [1], [a: 2], [emit Unexpected()]][:count]
    emit Done(value: value is nothing)
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: cartesian-sources-evaluated-once

This case checks cartesian sources evaluated once without relying on implementation-specific instruction shapes.

### Case description

```yaml
gesBlock: case
id: cartesian-sources-evaluated-once
kind: scriptApi
level: atomic
sources:
  - name: "cartesian-sources-evaluated-once.ges"
    program: main
```

### Source code under test

```ges
module collectionedges
on Start {
    let value be [:cartesian [emit First()], [emit Second()]][:count]
    emit Done(value: value = 1)
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
    input:
      args: []
    local:
      - name: First
        args: []
      - name: Second
        args: []
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: foreach-emits-in-order

This case checks foreach emits in order without relying on implementation-specific instruction shapes.

### Case description

```yaml
gesBlock: case
id: foreach-emits-in-order
kind: scriptApi
level: atomic
sources:
  - name: "foreach-emits-in-order.ges"
    program: main
```

### Source code under test

```ges
module collectionedges
on Start {
    let value be [[1, 2], [3, 4]][:foreach a, b => emit Item(value: a + b)]
    emit Done(value: value is nothing)
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
    input:
      args: []
    local:
      - name: Item
        args:
          - name: value
            value: { type: ':Number.int64', value: '3' }
      - name: Item
        args:
          - name: value
            value: { type: ':Number.int64', value: '7' }
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: component-first-with-one-item-budget

This case checks component first with one item budget without relying on implementation-specific instruction shapes.

### Case description

```yaml
gesBlock: case
id: component-first-with-one-item-budget
kind: scriptApi
level: atomic
runtimeLimits: { maxGeneratedCollectionItems: 1 }
sources:
  - name: "component-first-with-one-item-budget.ges"
    program: main
```

### Source code under test

```ges
module collectionedges
on Start {
    let value be [:cartesian [1], [2]][:select a, b => a + b][:first]
    emit Done(value: value = 3)
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: entries-first-with-one-item-budget

This case checks entries first with one item budget without relying on implementation-specific instruction shapes.

### Case description

```yaml
gesBlock: case
id: entries-first-with-one-item-budget
kind: scriptApi
level: atomic
runtimeLimits: { maxGeneratedCollectionItems: 1 }
sources:
  - name: "entries-first-with-one-item-budget.ges"
    program: main
```

### Source code under test

```ges
module collectionedges
on Start {
    let value be [a: 1][:entries :filter key, value where key = 'a' :select key, value => value :first]
    emit Done(value: value = 1)
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: component-first-with-one-loop-budget

This case checks component first with one loop budget without relying on implementation-specific instruction shapes.

### Case description

```yaml
gesBlock: case
id: component-first-with-one-loop-budget
kind: scriptApi
level: atomic
runtimeLimits: { maxLoopIterations: 1 }
sources:
  - name: "component-first-with-one-loop-budget.ges"
    program: main
```

### Source code under test

```ges
module collectionedges
on Start {
    let value be [:cartesian [1, 2], [3, 4]][:select a, b => a + b][:first]
    emit Done(value: value = 4)
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
    input:
      args: []
    local:
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: fold-source-before-seed

This case checks fold source before seed without relying on implementation-specific instruction shapes.

### Case description

```yaml
gesBlock: case
id: fold-source-before-seed
kind: scriptApi
level: atomic
sources:
  - name: "fold-source-before-seed.ges"
    program: main
```

### Source code under test

```ges
module collectionedges
on Start {
    let value be [:cartesian [], [emit Source()]][:fold acc be (emit Seed()), a, b => acc]
    emit Done(value: value)
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
    input:
      args: []
    local:
      - name: Source
        args: []
      - name: Seed
        args: []
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```

---

## Test: fold-invalid-source-still-evaluates-seed

This case checks fold invalid source still evaluates seed without relying on implementation-specific instruction shapes.

### Case description

```yaml
gesBlock: case
id: fold-invalid-source-still-evaluates-seed
kind: scriptApi
level: atomic
sources:
  - name: "fold-invalid-source-still-evaluates-seed.ges"
    program: main
```

### Source code under test

```ges
module collectionedges
on Start {
    let value be [:cartesian nothing, [emit Unexpected()]][:fold acc be (emit Seed()), a, b => acc]
    emit Done(value: value is nothing)
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
    input:
      args: []
    local:
      - name: Seed
        args: []
      - name: Done
        args:
          - name: value
            value: { type: ':Boolean', value: true }
```
