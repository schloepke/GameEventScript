---
formatVersion: 1
suiteId: "runtime.atomic.create-values"
title: "RuntimeAtomicCreateValues"
categories: [conformance]
---

# RuntimeAtomicCreateValues

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite isolates value constructors and verifies the exact portable values they create.

---

## Test: load primitive values

This runtime case exercises “load primitive values” and verifies the declared messages, values, and execution result.

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
  - name: "load primitive values.ges"
    program: main
```

### Source code under test

```ges
module atomicloadprimitivevalues
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
          - name: "trueValue"
            value:
              type: ":Boolean"
              value: true
          - name: "falseValue"
            value:
              type: ":Boolean"
              value: false
          - name: "integerValue"
            value:
              type: ":Number.int64"
              value: "42"
          - name: "floatValue"
            value:
              type: ":Number.binary64"
              value: "12.5"
          - name: "meterValue"
            value:
              type: ":Quantity.int64"
              unit: ":meter"
              value: "7"
          - name: "percentageValue"
            value:
              type: ":Percentage"
              value: "0.25"
          - name: "textValue"
            value:
              type: ":Text"
              value: "hello"
          - name: "tagValue"
            value:
              type: ":Tag"
              value: "boss"
```

---

## Test: create vector and point values

This runtime case exercises “create vector and point values” and verifies the declared messages, values, and execution result.

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
  - name: "create vector and point values.ges"
    program: main
```

### Source code under test

```ges
module atomiccreatevectorpointvalues
on Start {
  let vectorValue be :Vector(1, 2.5, 3)
  let pointValue be :Point(4, 5.5, 6)
  let liftedVector be :Vector(vectorValue)
  let liftedVectorWithZ be :Vector(vectorValue, 9)
  let vectorFromPoint be :Vector(pointValue)
  let liftedPoint be :Point(pointValue)
  let liftedPointWithZ be :Point(pointValue, 8)
  let pointFromVector be :Point(vectorValue)
  let unitVector be :Vector(3m, 4m)
  let unitLiftedVector be :Vector(unitVector)
  let unitLiftedVectorWithZ be :Vector(unitVector, 20m)
  let unitPoint be :Point(3m, 4m)
  let unitLiftedPointWithZ be :Point(unitPoint, 20m)
  emit Done(vectorValue: vectorValue, pointValue: pointValue, liftedVector: liftedVector, liftedVectorWithZ: liftedVectorWithZ, vectorFromPoint: vectorFromPoint, liftedPoint: liftedPoint, liftedPointWithZ: liftedPointWithZ, pointFromVector: pointFromVector, unitVector: unitVector, unitLiftedVector: unitLiftedVector, unitLiftedVectorWithZ: unitLiftedVectorWithZ, unitPoint: unitPoint, unitLiftedPointWithZ: unitLiftedPointWithZ)
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
          - name: "vectorValue"
            value:
              type: ":Vector"
              x: "1"
              y: "2.5"
              z: "3"
          - name: "pointValue"
            value:
              type: ":Point"
              x: "4"
              y: "5.5"
              z: "6"
          - name: "liftedVector"
            value:
              type: ":Vector"
              x: "1"
              y: "2.5"
              z: "3"
          - name: "liftedVectorWithZ"
            value:
              type: ":Vector"
              x: "1"
              y: "2.5"
              z: "9"
          - name: "vectorFromPoint"
            value:
              type: ":Vector"
              x: "4"
              y: "5.5"
              z: "6"
          - name: "liftedPoint"
            value:
              type: ":Point"
              x: "4"
              y: "5.5"
              z: "6"
          - name: "liftedPointWithZ"
            value:
              type: ":Point"
              x: "4"
              y: "5.5"
              z: "8"
          - name: "pointFromVector"
            value:
              type: ":Point"
              x: "1"
              y: "2.5"
              z: "3"
          - name: "unitVector"
            value:
              type: ":Vector"
              unit: ":meter"
              x: "3"
              y: "4"
              z: "0"
          - name: "unitLiftedVector"
            value:
              type: ":Vector"
              unit: ":meter"
              x: "3"
              y: "4"
              z: "0"
          - name: "unitLiftedVectorWithZ"
            value:
              type: ":Vector"
              unit: ":meter"
              x: "3"
              y: "4"
              z: "20"
          - name: "unitPoint"
            value:
              type: ":Point"
              unit: ":meter"
              x: "3"
              y: "4"
              z: "0"
          - name: "unitLiftedPointWithZ"
            value:
              type: ":Point"
              unit: ":meter"
              x: "3"
              y: "4"
              z: "20"
```

---

## Test: create range values

This runtime case exercises “create range values” and verifies the declared messages, values, and execution result.

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
  - name: "create range values.ges"
    program: main
```

### Source code under test

```ges
module atomiccreaterangevalues
on Start {
  let ascending be (from 1 to 3) as :Range
  let stepped be (from 1 to 5 step 2) as :Range
  let descending be (from 5 to 1 step (0 - 2)) as :Range
  emit Done(ascending: ascending, stepped: stepped, descending: descending)
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
          - name: "ascending"
            value:
              type: ":Range.int64"
              from: "1"
              to: "3"
              step: "1"
          - name: "stepped"
            value:
              type: ":Range.int64"
              from: "1"
              to: "5"
              step: "2"
          - name: "descending"
            value:
              type: ":Range.int64"
              from: "5"
              to: "1"
              step: "-2"
```

---

## Test: create float range values

This runtime case exercises “create float range values” and verifies the declared messages, values, and execution result.

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
  - name: "create float range values.ges"
    program: main
```

### Source code under test

```ges
module atomiccreatefloatrangevalues
on Start {
  let ascending be (from 1.5 to 3.5 step 0.5) as :Range
  let descending be (from 3.5 to 1.5 step (0 - 0.5)) as :Range
  emit Done(ascending: ascending, descending: descending)
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
          - name: "ascending"
            value:
              type: ":Range.binary64"
              from: "1.5"
              to: "3.5"
              step: "0.5"
          - name: "descending"
            value:
              type: ":Range.binary64"
              from: "3.5"
              to: "1.5"
              step: "-0.5"
```

---

## Test: create dice values

This runtime case exercises “create dice values” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["3", "2", "1", "3", "2", "1"]
sources:
  - name: "create dice values.ges"
    program: main
```

### Source code under test

```ges
module atomiccreatedicevalues
on Start {
  let diceValue be roll dice 6d3
  let constructedDice be :Dice([1, 2, 3])
  emit Done(diceValue: diceValue, length: diceValue[:count], constructedDice: constructedDice, constructedLength: constructedDice[:count])
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
          - name: "diceValue"
            value:
              type: ":Dice"
              rolls:
                - 3
                - 3
                - 2
                - 2
                - 1
                - 1
          - name: "length"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "constructedDice"
            value:
              type: ":Dice"
              rolls:
                - 3
                - 2
                - 1
          - name: "constructedLength"
            value:
              type: ":Number.int64"
              value: "3"
```

---

## Test: create list values

This runtime case exercises “create list values” and verifies the declared messages, values, and execution result.

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
  - name: "create list values.ges"
    program: main
```

### Source code under test

```ges
module atomiccreatelistvalues
on Start {
  let values be [1, 'two', pi, true]
  emit Done(values: values, length: values[:count])
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
          - name: "values"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Text"
                  value: "two"
                - type: ":Number.binary64"
                  value: "3.141592653589793"
                - type: ":Boolean"
                  value: true
          - name: "length"
            value:
              type: ":Number.int64"
              value: "4"
```

---

## Test: create map values

This runtime case exercises “create map values” and verifies the declared messages, values, and execution result.

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
  - name: "create map values.ges"
    program: main
```

### Source code under test

```ges
module atomiccreatemapvalues
on Start {
  let unit be [name: 'Ada', hp: 10, alive: true, role: #boss]
  emit Done(unit: unit, length: unit[:count])
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
          - name: "unit"
            value:
              type: ":Map"
              entries:
                - key: "alive"
                  value:
                    type: ":Boolean"
                    value: true
                - key: "hp"
                  value:
                    type: ":Number.int64"
                    value: "10"
                - key: "name"
                  value:
                    type: ":Text"
                    value: "Ada"
                - key: "role"
                  value:
                    type: ":Tag"
                    value: "boss"
          - name: "length"
            value:
              type: ":Number.int64"
              value: "4"
```

---

## Test: create nested collection values

This runtime case exercises “create nested collection values” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "create nested collection values.ges"
    program: main
```

### Source code under test

```ges
module atomiccreatenestedcollectionvalues
on Start {
  let squad be [leader: [name: 'Ada', hp: 10], inventory: ['potion', 'key'], tags: [#boss, #scout]]
  emit Done(squad: squad, length: squad[:count])
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
          - name: "squad"
            value:
              type: ":Map"
              entries:
                - key: "inventory"
                  value:
                    type: ":List"
                    items:
                      - type: ":Text"
                        value: "potion"
                      - type: ":Text"
                        value: "key"
                - key: "leader"
                  value:
                    type: ":Map"
                    entries:
                      - key: "hp"
                        value:
                          type: ":Number.int64"
                          value: "10"
                      - key: "name"
                        value:
                          type: ":Text"
                          value: "Ada"
                - key: "tags"
                  value:
                    type: ":List"
                    items:
                      - type: ":Tag"
                        value: "boss"
                      - type: ":Tag"
                        value: "scout"
          - name: "length"
            value:
              type: ":Number.int64"
              value: "3"
```

---

## Test: stage literal value kinds

This runtime case exercises “stage literal value kinds” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "stage literal value kinds.ges"
    program: main
```

### Source code under test

```ges
module atomicstageliteralvaluekinds
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
          - name: "stagedList"
            value:
              type: ":List"
              items:
                - type: ":Boolean"
                  value: false
                - type: ":Boolean"
                  value: true
                - type: ":Number.int64"
                  value: "12"
                - type: ":Number.binary64"
                  value: "12.5"
                - type: ":Quantity.int64"
                  unit: ":meter"
                  value: "7"
                - type: ":Quantity.binary64"
                  unit: ":meter"
                  value: "1.5"
                - type: ":Percentage"
                  value: "0.25"
                - type: ":Text"
                  value: "txt"
                - type: ":Tag"
                  value: "ready"
          - name: "stagedMap"
            value:
              type: ":Map"
              entries:
                - key: "falseValue"
                  value:
                    type: ":Boolean"
                    value: false
                - key: "floatValue"
                  value:
                    type: ":Number.binary64"
                    value: "12.5"
                - key: "integerValue"
                  value:
                    type: ":Number.int64"
                    value: "12"
                - key: "meterFloat"
                  value:
                    type: ":Quantity.binary64"
                    unit: ":meter"
                    value: "1.5"
                - key: "meterInteger"
                  value:
                    type: ":Quantity.int64"
                    unit: ":meter"
                    value: "7"
                - key: "percentageValue"
                  value:
                    type: ":Percentage"
                    value: "0.25"
                - key: "tagValue"
                  value:
                    type: ":Tag"
                    value: "ready"
                - key: "textValue"
                  value:
                    type: ":Text"
                    value: "txt"
                - key: "trueValue"
                  value:
                    type: ":Boolean"
                    value: true
```

---

## Test: generated list builder values

This runtime case exercises “generated list builder values” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "generated list builder values.ges"
    program: main
```

### Source code under test

```ges
module atomicgeneratedlistbuildervalues
on Start {
  let sourceList be [1, 'two', #boss, true]
  let emptyList be []
  let dice be ([6, 4, 2]) as :Dice
  let text be 'ab'
  let map be [a: 1, b: 2]
  let fromRange be :List[:select item from 1 to 4 => item * item]
  let filteredRange be :List[:select item from 1 to 6 where item mod 2 = 0 => item]
  let fromList be :List[:select item in sourceList => item]
  let fromEmptyList be :List[:select item in emptyList => item]
  let fromDice be :List[:select item in dice => item + 1]
  let fromText be :List[:select item in text => item]
  let fromMap be :List[:select item in map => item * 10]
  let invalidSource be :List[:select item in 10 => item]
  emit Done(fromRange: fromRange, filteredRange: filteredRange, fromList: fromList, fromEmptyList: fromEmptyList, fromDice: fromDice, fromText: fromText, fromMap: fromMap, invalidSource: invalidSource)
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
                  value: "1"
                - type: ":Number.int64"
                  value: "4"
                - type: ":Number.int64"
                  value: "9"
                - type: ":Number.int64"
                  value: "16"
          - name: "filteredRange"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "4"
                - type: ":Number.int64"
                  value: "6"
          - name: "fromList"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Text"
                  value: "two"
                - type: ":Tag"
                  value: "boss"
                - type: ":Boolean"
                  value: true
          - name: "fromEmptyList"
            value:
              type: ":List"
              items: []
          - name: "fromDice"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "7"
                - type: ":Number.int64"
                  value: "5"
                - type: ":Number.int64"
                  value: "3"
          - name: "fromText"
            value:
              type: ":List"
              items:
                - type: ":Text"
                  value: "a"
                - type: ":Text"
                  value: "b"
          - name: "fromMap"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "10"
                - type: ":Number.int64"
                  value: "20"
          - name: "invalidSource"
            value:
              type: ":List"
              items: []
```

---

## Test: generated range iterator variants

This runtime case exercises “generated range iterator variants” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "generated range iterator variants.ges"
    program: main
```

### Source code under test

```ges
module atomicgeneratedrangeiteratorvariants
on Start {
  let fromValue be 2
  let toValue be 5
  let stepValue be 2
  let shortRange be :List[:select item from 1 to 3 => item]
  let dynamicRange be :List[:select item from fromValue to toValue => item]
  let dynamicSteppedRange be :List[:select item from fromValue to toValue step stepValue => item]
  let descendingRange be :List[:select item from 5 to 1 step (0 - 2) => item]
  let floatRange be :List[:select item from 1.5 to 2.5 step 0.5 => item]
  let emptyRange be :List[:select item from 3 to 1 => item]
  emit Done(shortRange: shortRange, dynamicRange: dynamicRange, dynamicSteppedRange: dynamicSteppedRange, descendingRange: descendingRange, floatRange: floatRange, emptyRange: emptyRange)
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
          - name: "shortRange"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
          - name: "dynamicRange"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
                - type: ":Number.int64"
                  value: "4"
                - type: ":Number.int64"
                  value: "5"
          - name: "dynamicSteppedRange"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "4"
          - name: "descendingRange"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "5"
                - type: ":Number.int64"
                  value: "3"
                - type: ":Number.int64"
                  value: "1"
          - name: "floatRange"
            value:
              type: ":List"
              items:
                - type: ":Number.binary64"
                  value: "1.5"
                - type: ":Number.binary64"
                  value: "2"
                - type: ":Number.binary64"
                  value: "2.5"
          - name: "emptyRange"
            value:
              type: ":List"
              items: []
```

---

## Test: large staged list grows register storage

This runtime case exercises “large staged list grows register storage” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "large staged list grows register storage.ges"
    program: main
```

### Source code under test

```ges
module atomiclargestagedlist
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
          - name: "count"
            value:
              type: ":Number.int64"
              value: "40"
```
