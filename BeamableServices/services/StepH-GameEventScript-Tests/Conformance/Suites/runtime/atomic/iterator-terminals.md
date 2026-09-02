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
module AtomicCountTerminals
on Start {
  let values be [1, 2, 3, 4]
  let dice as :dice be [6, 4, 2]
  let range as :range be from 1 to 4
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
              type: ":integer"
              value: "4"
          - name: "allValuesBare"
            value:
              type: ":integer"
              value: "4"
          - name: "filteredValues"
            value:
              type: ":integer"
              value: "2"
          - name: "diceHigh"
            value:
              type: ":integer"
              value: "2"
          - name: "diceBare"
            value:
              type: ":integer"
              value: "3"
          - name: "rangeAll"
            value:
              type: ":integer"
              value: "4"
          - name: "rangeBare"
            value:
              type: ":integer"
              value: "4"
          - name: "mapOverOne"
            value:
              type: ":integer"
              value: "2"
          - name: "mapBare"
            value:
              type: ":integer"
              value: "3"
          - name: "textB"
            value:
              type: ":integer"
              value: "2"
          - name: "textBare"
            value:
              type: ":integer"
              value: "4"
          - name: "emptyCount"
            value:
              type: ":integer"
              value: "0"
          - name: "emptyBare"
            value:
              type: ":integer"
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
module AtomicSumAverageTerminals
on Start {
  let values be [1, 2, 3, 4]
  let dice as :dice be [6, 4, 2]
  let range as :range be from 1 to 4
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
              type: ":integer"
              value: "10"
          - name: "sumValuesBare"
            value:
              type: ":integer"
              value: "10"
          - name: "sumFiltered"
            value:
              type: ":integer"
              value: "7"
          - name: "sumDice"
            value:
              type: ":integer"
              value: "12"
          - name: "sumDiceBare"
            value:
              type: ":integer"
              value: "12"
          - name: "sumRange"
            value:
              type: ":integer"
              value: "10"
          - name: "sumRangeBare"
            value:
              type: ":integer"
              value: "10"
          - name: "sumMap"
            value:
              type: ":integer"
              value: "6"
          - name: "sumMapBare"
            value:
              type: ":integer"
              value: "6"
          - name: "sumEmpty"
            value:
              type: ":integer"
              value: "0"
          - name: "sumEmptyBare"
            value:
              type: ":integer"
              value: "0"
          - name: "averageValues"
            value:
              type: ":float"
              value: "2.5"
          - name: "averageValuesBare"
            value:
              type: ":float"
              value: "2.5"
          - name: "averageFiltered"
            value:
              type: ":float"
              value: "3.5"
          - name: "averageDice"
            value:
              type: ":integer"
              value: "4"
          - name: "averageDiceBare"
            value:
              type: ":integer"
              value: "4"
          - name: "averageRange"
            value:
              type: ":float"
              value: "2.5"
          - name: "averageRangeBare"
            value:
              type: ":float"
              value: "2.5"
          - name: "averageMap"
            value:
              type: ":integer"
              value: "2"
          - name: "averageMapBare"
            value:
              type: ":integer"
              value: "2"
          - name: "averageEmpty"
            value:
              type: ":nothing"
          - name: "averageEmptyBare"
            value:
              type: ":nothing"
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
module AtomicIteratorMinMaxTerminals
on Start {
  let values be [3, 1, 4, 2]
  let dice as :dice be [6, 4, 2]
  let range as :range be from 1 to 4
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
              type: ":integer"
              value: "1"
          - name: "maxValue"
            value:
              type: ":integer"
              value: "4"
          - name: "minDice"
            value:
              type: ":integer"
              value: "2"
          - name: "maxDice"
            value:
              type: ":integer"
              value: "6"
          - name: "minRange"
            value:
              type: ":integer"
              value: "1"
          - name: "maxRange"
            value:
              type: ":integer"
              value: "4"
          - name: "minPercentage"
            value:
              type: ":percentage"
              value: "0.05"
          - name: "maxPercentage"
            value:
              type: ":percentage"
              value: "0.25"
          - name: "minQuantity"
            value:
              type: ":integer"
              unit: ":meter"
              value: "1"
          - name: "maxQuantity"
            value:
              type: ":integer"
              unit: ":meter"
              value: "3"
          - name: "minBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "maxBoolean"
            value:
              type: ":boolean"
              value: true
          - name: "emptyMin"
            value:
              type: ":nothing"
          - name: "emptyMax"
            value:
              type: ":nothing"
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
module AtomicIteratorMinMaxReturnsItem
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
              type: ":tag"
              value: "mage"
          - name: "weakestHp"
            value:
              type: ":integer"
              value: "6"
          - name: "strongestId"
            value:
              type: ":tag"
              value: "rook"
          - name: "strongestHp"
            value:
              type: ":integer"
              value: "10"
          - name: "transformedMax"
            value:
              type: ":integer"
              value: "30"
```
