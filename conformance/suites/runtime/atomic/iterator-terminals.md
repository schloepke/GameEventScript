---
formatVersion: 1
suiteId: "runtime.atomic.iterator-terminals"
title: "RuntimeAtomicIteratorTerminals"
categories: [conformance]
---

# RuntimeAtomicIteratorTerminals

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite isolates terminal iterator operations such as aggregation, selection, and materialization.

---

## Test: iterator count over source kinds

This runtime case exercises “iterator count over source kinds” and verifies the declared messages, values, and execution result.

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
  - name: "iterator count over source kinds.ges"
    program: main
```

### Source code under test

```ges
module atomiccountterminals
on Start {
  let values be [1, 2, 3, 4]
  let dice be ([6, 4, 2]) as :Dice
  let range be (from 1 to 4) as :Range
  let map be [b: 2, a: 1, c: 3]
  let text be 'abba'
  let emptyList be []
  emit Done(allValues: values[:count value where true], allValuesBare: values[:count], filteredValues: values[:count value where value > 2], diceHigh: dice[:count value where value >= 4], diceBare: dice[:count], rangeAll: range[:count value where true], rangeBare: range[:count], mapOverOne: map[:count value where value > 1], mapBare: map[:count], textB: text[:count value where value = 'b'], textBare: text[:count], emptyCount: emptyList[:count value where true], emptyBare: emptyList[:count])
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
          - name: "allValues"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "allValuesBare"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "filteredValues"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "diceHigh"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "diceBare"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "rangeAll"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "rangeBare"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "mapOverOne"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "mapBare"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "textB"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "textBare"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "emptyCount"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "emptyBare"
            value:
              type: ":Number.int64"
              value: "0"
```

---

## Test: iterator sum and average over source kinds

This runtime case exercises “iterator sum and average over source kinds” and verifies the declared messages, values, and execution result.

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
  - name: "iterator sum and average over source kinds.ges"
    program: main
```

### Source code under test

```ges
module atomicsumaverageterminals
on Start {
  let values be [1, 2, 3, 4]
  let dice be ([6, 4, 2]) as :Dice
  let range be (from 1 to 4) as :Range
  let map be [b: 2, a: 1, c: 3]
  let emptyList be []
  emit Done(sumValues: values[:sum value => value], sumValuesBare: values[:sum], sumFiltered: values[:filter value where value > 2][:sum value => value], sumDice: dice[:sum value => value], sumDiceBare: dice[:sum], sumRange: range[:sum value => value], sumRangeBare: range[:sum], sumMap: map[:sum value => value], sumMapBare: map[:sum], sumEmpty: emptyList[:sum value => value], sumEmptyBare: emptyList[:sum], averageValues: values[:average value => value], averageValuesBare: values[:average], averageFiltered: values[:filter value where value > 2][:average value => value], averageDice: dice[:average value => value], averageDiceBare: dice[:average], averageRange: range[:average value => value], averageRangeBare: range[:average], averageMap: map[:average value => value], averageMapBare: map[:average], averageEmpty: emptyList[:average value => value], averageEmptyBare: emptyList[:average])
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
          - name: "sumValues"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "sumValuesBare"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "sumFiltered"
            value:
              type: ":Number.int64"
              value: "7"
          - name: "sumDice"
            value:
              type: ":Number.int64"
              value: "12"
          - name: "sumDiceBare"
            value:
              type: ":Number.int64"
              value: "12"
          - name: "sumRange"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "sumRangeBare"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "sumMap"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "sumMapBare"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "sumEmpty"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "sumEmptyBare"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "averageValues"
            value:
              type: ":Number.binary64"
              value: "2.5"
          - name: "averageValuesBare"
            value:
              type: ":Number.binary64"
              value: "2.5"
          - name: "averageFiltered"
            value:
              type: ":Number.binary64"
              value: "3.5"
          - name: "averageDice"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "averageDiceBare"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "averageRange"
            value:
              type: ":Number.binary64"
              value: "2.5"
          - name: "averageRangeBare"
            value:
              type: ":Number.binary64"
              value: "2.5"
          - name: "averageMap"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "averageMapBare"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "averageEmpty"
            value:
              type: ":Nothing"
          - name: "averageEmptyBare"
            value:
              type: ":Nothing"
```

---

## Test: iterator min and max over numeric source kinds

This runtime case exercises “iterator min and max over numeric source kinds” and verifies the declared messages, values, and execution result.

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
  - name: "iterator min and max over numeric source kinds.ges"
    program: main
```

### Source code under test

```ges
module atomiciteratorminmaxterminals
on Start {
  let values be [3, 1, 4, 2]
  let dice be ([6, 4, 2]) as :Dice
  let range be (from 1 to 4) as :Range
  let percentages be [10%, 25%, 5%]
  let quantities be [3m, 1m, 2m]
  let booleans be [true, false, true]
  let emptyList be []
  emit Done(minValue: values[:min value => value], maxValue: values[:max value => value], minDice: dice[:min value => value], maxDice: dice[:max value => value], minRange: range[:min value => value], maxRange: range[:max value => value], minPercentage: percentages[:min value => value], maxPercentage: percentages[:max value => value], minQuantity: quantities[:min value => value], maxQuantity: quantities[:max value => value], minBoolean: booleans[:min value => value], maxBoolean: booleans[:max value => value], emptyMin: emptyList[:min value => value], emptyMax: emptyList[:max value => value])
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
          - name: "minValue"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "maxValue"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "minDice"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "maxDice"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "minRange"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "maxRange"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "minPercentage"
            value:
              type: ":Percentage"
              value: "0.05"
          - name: "maxPercentage"
            value:
              type: ":Percentage"
              value: "0.25"
          - name: "minQuantity"
            value:
              type: ":Quantity.int64"
              unit: ":meter"
              value: "1"
          - name: "maxQuantity"
            value:
              type: ":Quantity.int64"
              unit: ":meter"
              value: "3"
          - name: "minBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "maxBoolean"
            value:
              type: ":Boolean"
              value: true
          - name: "emptyMin"
            value:
              type: ":Nothing"
          - name: "emptyMax"
            value:
              type: ":Nothing"
```

---

## Test: iterator min and max return winning source item

This runtime case exercises “iterator min and max return winning source item” and verifies the declared messages, values, and execution result.

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
  - name: "iterator min and max return winning source item.ges"
    program: main
```

### Source code under test

```ges
module atomiciteratorminmaxreturnsitem
on Start {
  let units be [[id: #rook, hp: 10], [id: #mage, hp: 6], [id: #guard, hp: 8]]
  let weakest be units[:min unit => unit.hp]
  let strongest be units[:max unit => unit.hp]
  let transformedMax be [1, 2, 3, 4][:filter value where value < 4][:select value => value * 10][:max value => value]
  emit Done(weakestId: weakest.id, weakestHp: weakest.hp, strongestId: strongest.id, strongestHp: strongest.hp, transformedMax: transformedMax)
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
          - name: "weakestId"
            value:
              type: ":Tag"
              value: "mage"
          - name: "weakestHp"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "strongestId"
            value:
              type: ":Tag"
              value: "rook"
          - name: "strongestHp"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "transformedMax"
            value:
              type: ":Number.int64"
              value: "30"
```
