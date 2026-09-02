---
formatVersion: 1
suiteId: "runtime.atomic.create-values"
title: "RuntimeAtomicCreateValues"
categories: [conformance]
tags: [migrated-json-v1]
---

# RuntimeAtomicCreateValues

Mechanically migrated from the former JSON conformance corpus.

## Test: load primitive values

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
  - name: "load primitive values.ges"
    program: main
```

```ges
module AtomicLoadPrimitiveValues
on Start {
  let nothingValue be nothing
  let trueValue be true
  let falseValue be false
  let integerValue be 42
  let floatValue be 12.5
  let meterValue be 7m
  let percentageValue be 25%
  let textValue be 'hello'
  let tagValue be #boss
  emit Done(nothingValue: nothingValue, trueValue: trueValue, falseValue: falseValue, integerValue: integerValue, floatValue: floatValue, meterValue: meterValue, percentageValue: percentageValue, textValue: textValue, tagValue: tagValue)
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
          - name: "nothingValue"
            value:
              type: ":nothing"
          - name: "trueValue"
            value:
              type: ":boolean"
              value: true
          - name: "falseValue"
            value:
              type: ":boolean"
              value: false
          - name: "integerValue"
            value:
              type: ":integer"
              value: "42"
          - name: "floatValue"
            value:
              type: ":float"
              value: "12.5"
          - name: "meterValue"
            value:
              type: ":integer"
              unit: ":meter"
              value: "7"
          - name: "percentageValue"
            value:
              type: ":percentage"
              value: "0.25"
          - name: "textValue"
            value:
              type: ":text"
              value: "hello"
          - name: "tagValue"
            value:
              type: ":tag"
              value: "boss"
```

## Test: create vector and point values

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
  - name: "create vector and point values.ges"
    program: main
```

```ges
module AtomicCreateVectorPointValues
on Start {
  let vectorValue be :vector(1, 2.5, 3)
  let pointValue be :point(4, 5.5, 6)
  let liftedVector be :vector(vectorValue)
  let liftedVectorWithZ be :vector(vectorValue, 9)
  let vectorFromPoint be :vector(pointValue)
  let liftedPoint be :point(pointValue)
  let liftedPointWithZ be :point(pointValue, 8)
  let pointFromVector be :point(vectorValue)
  let unitVector be :vector(3m, 4m)
  let unitLiftedVector be :vector(unitVector)
  let unitLiftedVectorWithZ be :vector(unitVector, 20m)
  let unitPoint be :point(3m, 4m)
  let unitLiftedPointWithZ be :point(unitPoint, 20m)
  emit Done(vectorValue: vectorValue, pointValue: pointValue, liftedVector: liftedVector, liftedVectorWithZ: liftedVectorWithZ, vectorFromPoint: vectorFromPoint, liftedPoint: liftedPoint, liftedPointWithZ: liftedPointWithZ, pointFromVector: pointFromVector, unitVector: unitVector, unitLiftedVector: unitLiftedVector, unitLiftedVectorWithZ: unitLiftedVectorWithZ, unitPoint: unitPoint, unitLiftedPointWithZ: unitLiftedPointWithZ)
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
          - name: "vectorValue"
            value:
              type: ":vector"
              x: "1"
              y: "2.5"
              z: "3"
          - name: "pointValue"
            value:
              type: ":point"
              x: "4"
              y: "5.5"
              z: "6"
          - name: "liftedVector"
            value:
              type: ":vector"
              x: "1"
              y: "2.5"
              z: "3"
          - name: "liftedVectorWithZ"
            value:
              type: ":vector"
              x: "1"
              y: "2.5"
              z: "9"
          - name: "vectorFromPoint"
            value:
              type: ":vector"
              x: "4"
              y: "5.5"
              z: "6"
          - name: "liftedPoint"
            value:
              type: ":point"
              x: "4"
              y: "5.5"
              z: "6"
          - name: "liftedPointWithZ"
            value:
              type: ":point"
              x: "4"
              y: "5.5"
              z: "8"
          - name: "pointFromVector"
            value:
              type: ":point"
              x: "1"
              y: "2.5"
              z: "3"
          - name: "unitVector"
            value:
              type: ":vector"
              unit: ":meter"
              x: "3"
              y: "4"
              z: "0"
          - name: "unitLiftedVector"
            value:
              type: ":vector"
              unit: ":meter"
              x: "3"
              y: "4"
              z: "0"
          - name: "unitLiftedVectorWithZ"
            value:
              type: ":vector"
              unit: ":meter"
              x: "3"
              y: "4"
              z: "20"
          - name: "unitPoint"
            value:
              type: ":point"
              unit: ":meter"
              x: "3"
              y: "4"
              z: "0"
          - name: "unitLiftedPointWithZ"
            value:
              type: ":point"
              unit: ":meter"
              x: "3"
              y: "4"
              z: "20"
```

## Test: create range values

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
  - name: "create range values.ges"
    program: main
```

```ges
module AtomicCreateRangeValues
on Start {
  let ascending as :range be from 1 to 3
  let stepped as :range be from 1 to 5 step 2
  let descending as :range be from 5 to 1 step (0 - 2)
  emit Done(ascending: ascending, stepped: stepped, descending: descending)
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
          - name: "ascending"
            value:
              type: ":range"
              from: "1"
              to: "3"
              step: "1"
          - name: "stepped"
            value:
              type: ":range"
              from: "1"
              to: "5"
              step: "2"
          - name: "descending"
            value:
              type: ":range"
              from: "5"
              to: "1"
              step: "-2"
```

## Test: create float range values

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
  - name: "create float range values.ges"
    program: main
```

```ges
module AtomicCreateFloatRangeValues
on Start {
  let ascending as :range be from 1.5 to 3.5 step 0.5
  let descending as :range be from 3.5 to 1.5 step (0 - 0.5)
  emit Done(ascending: ascending, descending: descending)
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
          - name: "ascending"
            value:
              type: ":range"
              from: "1.5"
              to: "3.5"
              step: "0.5"
          - name: "descending"
            value:
              type: ":range"
              from: "3.5"
              to: "1.5"
              step: "-0.5"
```

## Test: create dice values

```yaml
gesBlock: case
id: case-0005
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["3", "2", "1", "3", "2", "1"]
sources:
  - name: "create dice values.ges"
    program: main
```

```ges
module AtomicCreateDiceValues
on Start {
  let diceValue be roll dice 6d3
  let constructedDice be :dice([1, 2, 3])
  emit Done(diceValue: diceValue, length: diceValue[:count], constructedDice: constructedDice, constructedLength: constructedDice[:count])
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
          - name: "diceValue"
            value:
              type: ":dice"
              rolls:
                - 3
                - 3
                - 2
                - 2
                - 1
                - 1
          - name: "length"
            value:
              type: ":integer"
              value: "6"
          - name: "constructedDice"
            value:
              type: ":dice"
              rolls:
                - 3
                - 2
                - 1
          - name: "constructedLength"
            value:
              type: ":integer"
              value: "3"
```

## Test: create list values

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
  - name: "create list values.ges"
    program: main
```

```ges
module AtomicCreateListValues
on Start {
  let values be [1, 'two', pi, true]
  emit Done(values: values, length: values[:count])
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
          - name: "values"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":text"
                  value: "two"
                - type: ":float"
                  value: "3.141592653589793"
                - type: ":boolean"
                  value: true
          - name: "length"
            value:
              type: ":integer"
              value: "4"
```

## Test: create map values

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
  - name: "create map values.ges"
    program: main
```

```ges
module AtomicCreateMapValues
on Start {
  let unit be [name: 'Ada', hp: 10, alive: true, role: #boss]
  emit Done(unit: unit, length: unit[:count])
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
          - name: "unit"
            value:
              type: ":map"
              entries:
                - key: "alive"
                  value:
                    type: ":boolean"
                    value: true
                - key: "hp"
                  value:
                    type: ":integer"
                    value: "10"
                - key: "name"
                  value:
                    type: ":text"
                    value: "Ada"
                - key: "role"
                  value:
                    type: ":tag"
                    value: "boss"
          - name: "length"
            value:
              type: ":integer"
              value: "4"
```

## Test: create nested collection values

```yaml
gesBlock: case
id: case-0008
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "create nested collection values.ges"
    program: main
```

```ges
module AtomicCreateNestedCollectionValues
on Start {
  let squad be [leader: [name: 'Ada', hp: 10], inventory: ['potion', 'key'], tags: [#boss, #scout]]
  emit Done(squad: squad, length: squad[:count])
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
          - name: "squad"
            value:
              type: ":map"
              entries:
                - key: "inventory"
                  value:
                    type: ":list"
                    items:
                      - type: ":text"
                        value: "potion"
                      - type: ":text"
                        value: "key"
                - key: "leader"
                  value:
                    type: ":map"
                    entries:
                      - key: "hp"
                        value:
                          type: ":integer"
                          value: "10"
                      - key: "name"
                        value:
                          type: ":text"
                          value: "Ada"
                - key: "tags"
                  value:
                    type: ":list"
                    items:
                      - type: ":tag"
                        value: "boss"
                      - type: ":tag"
                        value: "scout"
          - name: "length"
            value:
              type: ":integer"
              value: "3"
```

## Test: stage literal value kinds

```yaml
gesBlock: case
id: case-0009
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "stage literal value kinds.ges"
    program: main
```

```ges
module AtomicStageLiteralValueKinds
on Start {
  let stagedList be [false, true, 12, 12.5, 7m, 1.5m, 25%, 'txt', #ready]
  let stagedMap be [falseValue: false, trueValue: true, integerValue: 12, floatValue: 12.5, meterInteger: 7m, meterFloat: 1.5m, percentageValue: 25%, textValue: 'txt', tagValue: #ready]
  emit Done(stagedList: stagedList, stagedMap: stagedMap)
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
          - name: "stagedList"
            value:
              type: ":list"
              items:
                - type: ":boolean"
                  value: false
                - type: ":boolean"
                  value: true
                - type: ":integer"
                  value: "12"
                - type: ":float"
                  value: "12.5"
                - type: ":integer"
                  unit: ":meter"
                  value: "7"
                - type: ":float"
                  unit: ":meter"
                  value: "1.5"
                - type: ":percentage"
                  value: "0.25"
                - type: ":text"
                  value: "txt"
                - type: ":tag"
                  value: "ready"
          - name: "stagedMap"
            value:
              type: ":map"
              entries:
                - key: "falseValue"
                  value:
                    type: ":boolean"
                    value: false
                - key: "floatValue"
                  value:
                    type: ":float"
                    value: "12.5"
                - key: "integerValue"
                  value:
                    type: ":integer"
                    value: "12"
                - key: "meterFloat"
                  value:
                    type: ":float"
                    unit: ":meter"
                    value: "1.5"
                - key: "meterInteger"
                  value:
                    type: ":integer"
                    unit: ":meter"
                    value: "7"
                - key: "percentageValue"
                  value:
                    type: ":percentage"
                    value: "0.25"
                - key: "tagValue"
                  value:
                    type: ":tag"
                    value: "ready"
                - key: "textValue"
                  value:
                    type: ":text"
                    value: "txt"
                - key: "trueValue"
                  value:
                    type: ":boolean"
                    value: true
```

## Test: generated list builder values

```yaml
gesBlock: case
id: case-0010
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "generated list builder values.ges"
    program: main
```

```ges
module AtomicGeneratedListBuilderValues
on Start {
  let sourceList be [1, 'two', #boss, true]
  let emptyList be []
  let dice as :dice be [6, 4, 2]
  let text be 'ab'
  let map be [a: 1, b: 2]
  let fromRange be :list[:select item from 1 to 4 => item * item]
  let filteredRange be :list[:select item from 1 to 6 where item mod 2 = 0 => item]
  let fromList be :list[:select item in sourceList => item]
  let fromEmptyList be :list[:select item in emptyList => item]
  let fromDice be :list[:select item in dice => item + 1]
  let fromText be :list[:select item in text => item]
  let fromMap be :list[:select item in map => item * 10]
  let invalidSource be :list[:select item in 10 => item]
  emit Done(fromRange: fromRange, filteredRange: filteredRange, fromList: fromList, fromEmptyList: fromEmptyList, fromDice: fromDice, fromText: fromText, fromMap: fromMap, invalidSource: invalidSource)
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
                  value: "1"
                - type: ":integer"
                  value: "4"
                - type: ":integer"
                  value: "9"
                - type: ":integer"
                  value: "16"
          - name: "filteredRange"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "4"
                - type: ":integer"
                  value: "6"
          - name: "fromList"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":text"
                  value: "two"
                - type: ":tag"
                  value: "boss"
                - type: ":boolean"
                  value: true
          - name: "fromEmptyList"
            value:
              type: ":list"
              items: []
          - name: "fromDice"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "7"
                - type: ":integer"
                  value: "5"
                - type: ":integer"
                  value: "3"
          - name: "fromText"
            value:
              type: ":list"
              items:
                - type: ":text"
                  value: "a"
                - type: ":text"
                  value: "b"
          - name: "fromMap"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "10"
                - type: ":integer"
                  value: "20"
          - name: "invalidSource"
            value:
              type: ":list"
              items: []
```

## Test: generated range iterator variants

```yaml
gesBlock: case
id: case-0011
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "generated range iterator variants.ges"
    program: main
```

```ges
module AtomicGeneratedRangeIteratorVariants
on Start {
  let fromValue be 2
  let toValue be 5
  let stepValue be 2
  let shortRange be :list[:select item from 1 to 3 => item]
  let dynamicRange be :list[:select item from fromValue to toValue => item]
  let dynamicSteppedRange be :list[:select item from fromValue to toValue step stepValue => item]
  let descendingRange be :list[:select item from 5 to 1 step (0 - 2) => item]
  let floatRange be :list[:select item from 1.5 to 2.5 step 0.5 => item]
  let emptyRange be :list[:select item from 3 to 1 => item]
  emit Done(shortRange: shortRange, dynamicRange: dynamicRange, dynamicSteppedRange: dynamicSteppedRange, descendingRange: descendingRange, floatRange: floatRange, emptyRange: emptyRange)
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
          - name: "shortRange"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "dynamicRange"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "4"
                - type: ":integer"
                  value: "5"
          - name: "dynamicSteppedRange"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "4"
          - name: "descendingRange"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "5"
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "1"
          - name: "floatRange"
            value:
              type: ":list"
              items:
                - type: ":float"
                  value: "1.5"
                - type: ":float"
                  value: "2"
                - type: ":float"
                  value: "2.5"
          - name: "emptyRange"
            value:
              type: ":list"
              items: []
```

## Test: large staged list grows register storage

```yaml
gesBlock: case
id: case-0012
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "large staged list grows register storage.ges"
    program: main
```

```ges
module AtomicLargeStagedList
on Start {
  let values be [
    1, 2, 3, 4, 5, 6, 7, 8,
    9, 10, 11, 12, 13, 14, 15, 16,
    17, 18, 19, 20, 21, 22, 23, 24,
    25, 26, 27, 28, 29, 30, 31, 32,
    33, 34, 35, 36, 37, 38, 39, 40
  ]
  emit Done(count: values[:count])
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
          - name: "count"
            value:
              type: ":integer"
              value: "40"
```
