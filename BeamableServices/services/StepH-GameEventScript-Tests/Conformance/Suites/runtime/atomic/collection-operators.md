---
formatVersion: 1
suiteId: "runtime.atomic.collection-operators"
title: "RuntimeAtomicCollectionOperators"
categories: [conformance]
---

# RuntimeAtomicCollectionOperators

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite isolates collection transformations and verifies their ordering, cardinality, and value semantics.

---

## Test: count operator

This runtime case exercises “count operator” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["3", "2", "1"]
sources:
  - name: "count operator.ges"
    program: main
```

### Source code under test

```ges
module AtomicCollectionCountOperator
on Start {
  let vNothing be nothing
  let vBoolean be true
  let vInteger be 10
  let vFloat be 10.5
  let vPercentage be 25%
  let vText be 'ab'
  let vTag be #ab
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(4, 5, 6)
  let vList be [1, 2, 3]
  let vMap be [name: 'Ada', hp: 10]
  let vDice be roll dice 3d3
  let vRange as :range be from 1 to 3
  emit Done(nothingValue: vNothing[:count], booleanValue: vBoolean[:count], integerValue: vInteger[:count], floatValue: vFloat[:count], percentageValue: vPercentage[:count], textValue: vText[:count], tagValue: vTag[:count], vectorValue: vVector[:count], pointValue: vPoint[:count], listValue: vList[:count], mapValue: vMap[:count], diceValue: vDice[:count], rangeValue: vRange[:count])
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
              type: ":integer"
              value: "0"
          - name: "booleanValue"
            value:
              type: ":nothing"
          - name: "integerValue"
            value:
              type: ":nothing"
          - name: "floatValue"
            value:
              type: ":nothing"
          - name: "percentageValue"
            value:
              type: ":nothing"
          - name: "textValue"
            value:
              type: ":integer"
              value: "2"
          - name: "tagValue"
            value:
              type: ":integer"
              value: "2"
          - name: "vectorValue"
            value:
              type: ":integer"
              value: "3"
          - name: "pointValue"
            value:
              type: ":integer"
              value: "3"
          - name: "listValue"
            value:
              type: ":integer"
              value: "3"
          - name: "mapValue"
            value:
              type: ":integer"
              value: "2"
          - name: "diceValue"
            value:
              type: ":integer"
              value: "3"
          - name: "rangeValue"
            value:
              type: ":integer"
              value: "3"
```

---

## Test: has value operator

This runtime case exercises “has value operator” and verifies the declared messages, values, and execution result.

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
  - name: "has value operator.ges"
    program: main
```

### Source code under test

```ges
module AtomicCollectionHasValueOperator
on Start {
  let vNothing be nothing
  let vBooleanTrue be true
  let vBooleanFalse be false
  let vIntegerZero be 0
  let vFloatPositive be 10.5
  let vPercentageZero be 0%
  let vText be 'ab'
  let vTextEmpty be ''
  let vTag be #ab
  let vTagNan be #nan
  let vInfinity be infinity
  let vNegativeInfinity be 0 - infinity
  let vVectorZero be :vector(0, 0, 0)
  let vPointZero be :point(0, 0, 0)
  let vList be [1, 2]
  let vListEmpty be []
  let vMap be [name: 'Ada']
  let vMapEmpty be [:]
  let vDice as :dice be [6, 2]
  let vDiceEmpty as :dice be []
  let vRange as :range be from 1 to 3
  emit Done(nothingValue: vNothing has value, booleanTrue: vBooleanTrue has value, booleanFalse: vBooleanFalse has value, integerZero: vIntegerZero has value, floatPositive: vFloatPositive has value, percentageZero: vPercentageZero has value, textValue: vText has value, textEmpty: vTextEmpty has value, tagValue: vTag has value, tagNanValue: vTagNan has value, infinityValue: vInfinity has value, negativeInfinityValue: vNegativeInfinity has value, vectorZero: vVectorZero has value, pointZero: vPointZero has value, listValue: vList has value, listEmpty: vListEmpty has value, mapValue: vMap has value, mapEmpty: vMapEmpty has value, diceValue: vDice has value, diceEmpty: vDiceEmpty has value, rangeValue: vRange has value)
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
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":boolean"
              value: true
          - name: "floatPositive"
            value:
              type: ":boolean"
              value: true
          - name: "percentageZero"
            value:
              type: ":boolean"
              value: true
          - name: "textValue"
            value:
              type: ":boolean"
              value: true
          - name: "textEmpty"
            value:
              type: ":boolean"
              value: false
          - name: "tagValue"
            value:
              type: ":boolean"
              value: true
          - name: "tagNanValue"
            value:
              type: ":boolean"
              value: true
          - name: "infinityValue"
            value:
              type: ":boolean"
              value: true
          - name: "negativeInfinityValue"
            value:
              type: ":boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":boolean"
              value: true
          - name: "listValue"
            value:
              type: ":boolean"
              value: true
          - name: "listEmpty"
            value:
              type: ":boolean"
              value: false
          - name: "mapValue"
            value:
              type: ":boolean"
              value: true
          - name: "mapEmpty"
            value:
              type: ":boolean"
              value: false
          - name: "diceValue"
            value:
              type: ":boolean"
              value: true
          - name: "diceEmpty"
            value:
              type: ":boolean"
              value: false
          - name: "rangeValue"
            value:
              type: ":boolean"
              value: true
```

---

## Test: is empty operator

This runtime case exercises “is empty operator” and verifies the declared messages, values, and execution result.

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
  - name: "is empty operator.ges"
    program: main
```

### Source code under test

```ges
module AtomicCollectionIsEmptyOperator
on Start {
  let vNothing be nothing
  let vBooleanTrue be true
  let vBooleanFalse be false
  let vIntegerZero be 0
  let vFloatPositive be 10.5
  let vPercentageZero be 0%
  let vText be 'ab'
  let vTextEmpty be ''
  let vTag be #ab
  let vTagNan be #nan
  let vInfinity be infinity
  let vNegativeInfinity be 0 - infinity
  let vVectorZero be :vector(0, 0, 0)
  let vPointZero be :point(0, 0, 0)
  let vList be [1, 2]
  let vListEmpty be []
  let vMap be [name: 'Ada']
  let vMapEmpty be [:]
  let vDice as :dice be [6, 2]
  let vDiceEmpty as :dice be []
  let vRange as :range be from 1 to 3
  emit Done(nothingValue: vNothing is empty, booleanTrue: vBooleanTrue is empty, booleanFalse: vBooleanFalse is empty, integerZero: vIntegerZero is empty, floatPositive: vFloatPositive is empty, percentageZero: vPercentageZero is empty, textValue: vText is empty, textEmpty: vTextEmpty is empty, tagValue: vTag is empty, tagNanValue: vTagNan is empty, infinityValue: vInfinity is empty, negativeInfinityValue: vNegativeInfinity is empty, vectorZero: vVectorZero is empty, pointZero: vPointZero is empty, listValue: vList is empty, listEmpty: vListEmpty is empty, mapValue: vMap is empty, mapEmpty: vMapEmpty is empty, diceValue: vDice is empty, diceEmpty: vDiceEmpty is empty, rangeValue: vRange is empty)
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
              value: true
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":boolean"
              value: false
          - name: "percentageZero"
            value:
              type: ":boolean"
              value: false
          - name: "textValue"
            value:
              type: ":boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":boolean"
              value: true
          - name: "tagValue"
            value:
              type: ":boolean"
              value: false
          - name: "tagNanValue"
            value:
              type: ":boolean"
              value: false
          - name: "infinityValue"
            value:
              type: ":boolean"
              value: false
          - name: "negativeInfinityValue"
            value:
              type: ":boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":boolean"
              value: false
          - name: "listValue"
            value:
              type: ":boolean"
              value: false
          - name: "listEmpty"
            value:
              type: ":boolean"
              value: true
          - name: "mapValue"
            value:
              type: ":boolean"
              value: false
          - name: "mapEmpty"
            value:
              type: ":boolean"
              value: true
          - name: "diceValue"
            value:
              type: ":boolean"
              value: false
          - name: "diceEmpty"
            value:
              type: ":boolean"
              value: true
          - name: "rangeValue"
            value:
              type: ":boolean"
              value: false
```

---

## Test: default operator

This runtime case exercises “default operator” and verifies the declared messages, values, and execution result.

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
  - name: "default operator.ges"
    program: main
```

### Source code under test

```ges
module AtomicCollectionDefaultOperator
on Start {
  let vNothing be nothing
  let vBooleanTrue be true
  let vBooleanFalse be false
  let vIntegerZero be 0
  let vFloatPositive be 10.5
  let vPercentageZero be 0%
  let vText be 'ab'
  let vTextEmpty be ''
  let vTag be #ab
  let vTagNan be #nan
  let vInfinity be infinity
  let vNegativeInfinity be 0 - infinity
  let vVectorZero be :vector(0, 0, 0)
  let vPointZero be :point(0, 0, 0)
  let vList be [1, 2]
  let vListEmpty be []
  let vMap be [name: 'Ada']
  let vMapEmpty be [:]
  let vDice as :dice be [6, 2]
  let vDiceEmpty as :dice be []
  let vRange as :range be from 1 to 3
  let vRangeEmpty as :range be from 1 to 0
  emit Done(nothingValue: vNothing default 'fallback', booleanTrue: vBooleanTrue default 'fallback', booleanFalse: vBooleanFalse default 'fallback', integerZero: vIntegerZero default 99, floatPositive: vFloatPositive default 99, percentageZero: vPercentageZero default 99%, textValue: vText default 'fallback', textEmpty: vTextEmpty default 'fallback', tagValue: vTag default #fallback, tagNanValue: vTagNan default #fallback, infinityValue: vInfinity default 99, negativeInfinityValue: vNegativeInfinity default 99, vectorZero: vVectorZero default :vector(9, 9, 9), pointZero: vPointZero default :point(9, 9, 9), listValue: vList default [9], listEmpty: vListEmpty default [9], mapValue: vMap default [fallback: 1], mapEmpty: vMapEmpty default [fallback: 1], diceValue: vDice default [9], diceEmpty: vDiceEmpty default [9], rangeValue: vRange default [9], rangeEmpty: vRangeEmpty default [9])
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
              type: ":text"
              value: "fallback"
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":integer"
              value: "0"
          - name: "floatPositive"
            value:
              type: ":float"
              value: "10.5"
          - name: "percentageZero"
            value:
              type: ":percentage"
              value: "0"
          - name: "textValue"
            value:
              type: ":text"
              value: "ab"
          - name: "textEmpty"
            value:
              type: ":text"
              value: "fallback"
          - name: "tagValue"
            value:
              type: ":tag"
              value: "ab"
          - name: "tagNanValue"
            value:
              type: ":tag"
              value: "nan"
          - name: "infinityValue"
            value:
              type: ":float"
              value: "Infinity"
          - name: "negativeInfinityValue"
            value:
              type: ":float"
              value: "-Infinity"
          - name: "vectorZero"
            value:
              type: ":vector"
              x: "0"
              y: "0"
              z: "0"
          - name: "pointZero"
            value:
              type: ":point"
              x: "0"
              y: "0"
              z: "0"
          - name: "listValue"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
          - name: "listEmpty"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "9"
          - name: "mapValue"
            value:
              type: ":map"
              entries:
                - key: "name"
                  value:
                    type: ":text"
                    value: "Ada"
          - name: "mapEmpty"
            value:
              type: ":map"
              entries:
                - key: "fallback"
                  value:
                    type: ":integer"
                    value: "1"
          - name: "diceValue"
            value:
              type: ":dice"
              rolls:
                - 6
                - 2
          - name: "diceEmpty"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "9"
          - name: "rangeValue"
            value:
              type: ":range"
              from: "1"
              to: "3"
              step: "1"
          - name: "rangeEmpty"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "9"
```

---

## Test: keys operator

This runtime case exercises “keys operator” and verifies the declared messages, values, and execution result.

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
  sequence: ["3", "2", "1"]
sources:
  - name: "keys operator.ges"
    program: main
```

### Source code under test

```ges
module AtomicCollectionKeysOperator
on Start {
  let vNothing be nothing
  let vBoolean be true
  let vInteger be 10
  let vFloat be 10.5
  let vPercentage be 25%
  let vText be 'ab'
  let vTag be #ab
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(4, 5, 6)
  let vList be [1, 2, 3]
  let vMap be [name: 'Ada', hp: 10, alpha: 'first']
  let vDice be roll dice 3d3
  let vRange as :range be from 1 to 3
  emit Done(nothingValue: vNothing[:keys], booleanValue: vBoolean[:keys], integerValue: vInteger[:keys], floatValue: vFloat[:keys], percentageValue: vPercentage[:keys], textValue: vText[:keys], tagValue: vTag[:keys], vectorValue: vVector[:keys], pointValue: vPoint[:keys], listValue: vList[:keys], mapValue: vMap[:keys], diceValue: vDice[:keys], rangeValue: vRange[:keys])
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
          - name: "booleanValue"
            value:
              type: ":nothing"
          - name: "integerValue"
            value:
              type: ":nothing"
          - name: "floatValue"
            value:
              type: ":nothing"
          - name: "percentageValue"
            value:
              type: ":nothing"
          - name: "textValue"
            value:
              type: ":nothing"
          - name: "tagValue"
            value:
              type: ":nothing"
          - name: "vectorValue"
            value:
              type: ":nothing"
          - name: "pointValue"
            value:
              type: ":nothing"
          - name: "listValue"
            value:
              type: ":nothing"
          - name: "mapValue"
            value:
              type: ":list"
              items:
                - type: ":tag"
                  value: "alpha"
                - type: ":tag"
                  value: "hp"
                - type: ":tag"
                  value: "name"
          - name: "diceValue"
            value:
              type: ":nothing"
          - name: "rangeValue"
            value:
              type: ":nothing"
```

---

## Test: values operator

This runtime case exercises “values operator” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["3", "2", "1"]
sources:
  - name: "values operator.ges"
    program: main
```

### Source code under test

```ges
module AtomicCollectionValuesOperator
on Start {
  let vNothing be nothing
  let vBoolean be true
  let vInteger be 10
  let vFloat be 10.5
  let vPercentage be 25%
  let vText be 'ab'
  let vTag be #ab
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(4, 5, 6)
  let vList be [1, 2, 3]
  let vMap be [name: 'Ada', hp: 10, alpha: 'first']
  let vDice be roll dice 3d3
  let vRange as :range be from 1 to 3
  emit Done(nothingValue: vNothing[:values], booleanValue: vBoolean[:values], integerValue: vInteger[:values], floatValue: vFloat[:values], percentageValue: vPercentage[:values], textValue: vText[:values], tagValue: vTag[:values], vectorValue: vVector[:values], pointValue: vPoint[:values], listValue: vList[:values], mapValue: vMap[:values], diceValue: vDice[:values], rangeValue: vRange[:values])
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
          - name: "booleanValue"
            value:
              type: ":nothing"
          - name: "integerValue"
            value:
              type: ":nothing"
          - name: "floatValue"
            value:
              type: ":nothing"
          - name: "percentageValue"
            value:
              type: ":nothing"
          - name: "textValue"
            value:
              type: ":nothing"
          - name: "tagValue"
            value:
              type: ":nothing"
          - name: "vectorValue"
            value:
              type: ":nothing"
          - name: "pointValue"
            value:
              type: ":nothing"
          - name: "listValue"
            value:
              type: ":nothing"
          - name: "mapValue"
            value:
              type: ":list"
              items:
                - type: ":text"
                  value: "first"
                - type: ":integer"
                  value: "10"
                - type: ":text"
                  value: "Ada"
          - name: "diceValue"
            value:
              type: ":nothing"
          - name: "rangeValue"
            value:
              type: ":nothing"
```

---

## Test: entries operator

This runtime case exercises “entries operator” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["3", "2", "1"]
sources:
  - name: "entries operator.ges"
    program: main
```

### Source code under test

```ges
module AtomicCollectionEntriesOperator
on Start {
  let vNothing be nothing
  let vBoolean be true
  let vInteger be 10
  let vFloat be 10.5
  let vPercentage be 25%
  let vText be 'ab'
  let vTag be #ab
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(4, 5, 6)
  let vList be [1, 2, 3]
  let vMap be [name: 'Ada', hp: 10, alpha: 'first']
  let vDice be roll dice 3d3
  let vRange as :range be from 1 to 3
  emit Done(nothingValue: vNothing[:entries], booleanValue: vBoolean[:entries], integerValue: vInteger[:entries], floatValue: vFloat[:entries], percentageValue: vPercentage[:entries], textValue: vText[:entries], tagValue: vTag[:entries], vectorValue: vVector[:entries], pointValue: vPoint[:entries], listValue: vList[:entries], mapValue: vMap[:entries], diceValue: vDice[:entries], rangeValue: vRange[:entries])
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
          - name: "booleanValue"
            value:
              type: ":nothing"
          - name: "integerValue"
            value:
              type: ":nothing"
          - name: "floatValue"
            value:
              type: ":nothing"
          - name: "percentageValue"
            value:
              type: ":nothing"
          - name: "textValue"
            value:
              type: ":nothing"
          - name: "tagValue"
            value:
              type: ":nothing"
          - name: "vectorValue"
            value:
              type: ":nothing"
          - name: "pointValue"
            value:
              type: ":nothing"
          - name: "listValue"
            value:
              type: ":nothing"
          - name: "mapValue"
            value:
              type: ":list"
              items:
                - type: ":map"
                  entries:
                    - key: "key"
                      value:
                        type: ":tag"
                        value: "alpha"
                    - key: "value"
                      value:
                        type: ":text"
                        value: "first"
                - type: ":map"
                  entries:
                    - key: "key"
                      value:
                        type: ":tag"
                        value: "hp"
                    - key: "value"
                      value:
                        type: ":integer"
                        value: "10"
                - type: ":map"
                  entries:
                    - key: "key"
                      value:
                        type: ":tag"
                        value: "name"
                    - key: "value"
                      value:
                        type: ":text"
                        value: "Ada"
          - name: "diceValue"
            value:
              type: ":nothing"
          - name: "rangeValue"
            value:
              type: ":nothing"
```

---

## Test: starts with operator

This runtime case exercises “starts with operator” and verifies the declared messages, values, and execution result.

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
  sequence: ["3", "2", "1"]
sources:
  - name: "starts with operator.ges"
    program: main
```

### Source code under test

```ges
module AtomicCollectionStartsWithOperator
on Start {
  let vNothing be nothing
  let vBoolean be true
  let vInteger be 10
  let vFloat be 10.5
  let vPercentage be 25%
  let vText be 'abcd'
  let vTag be #abcd
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(4, 5, 6)
  let vList be [1, 2, 3]
  let vMap be [name: 'Ada', hp: 10]
  let vDice be roll dice 3d3
  let vRange as :range be from 1 to 3
  emit Done(nothingValue: vNothing starts with [1], booleanValue: vBoolean starts with [1], integerValue: vInteger starts with [1], floatValue: vFloat starts with [1], percentageValue: vPercentage starts with [1], textValue: vText starts with 'ab', tagValue: vTag starts with #ab, vectorValue: vVector starts with [1, 2], pointValue: vPoint starts with [4, 5], listValue: vList starts with [1, 2], mapValue: vMap starts with [10], diceValue: vDice starts with [3, 2], rangeValue: vRange starts with [1, 2])
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
              value: true
          - name: "tagValue"
            value:
              type: ":boolean"
              value: true
          - name: "vectorValue"
            value:
              type: ":boolean"
              value: false
          - name: "pointValue"
            value:
              type: ":boolean"
              value: false
          - name: "listValue"
            value:
              type: ":boolean"
              value: true
          - name: "mapValue"
            value:
              type: ":boolean"
              value: false
          - name: "diceValue"
            value:
              type: ":boolean"
              value: true
          - name: "rangeValue"
            value:
              type: ":boolean"
              value: true
```

---

## Test: ends with operator

This runtime case exercises “ends with operator” and verifies the declared messages, values, and execution result.

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
  sequence: ["3", "2", "1"]
sources:
  - name: "ends with operator.ges"
    program: main
```

### Source code under test

```ges
module AtomicCollectionEndsWithOperator
on Start {
  let vNothing be nothing
  let vBoolean be true
  let vInteger be 10
  let vFloat be 10.5
  let vPercentage be 25%
  let vText be 'abcd'
  let vTag be #abcd
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(4, 5, 6)
  let vList be [1, 2, 3]
  let vMap be [name: 'Ada', hp: 10]
  let vDice be roll dice 3d3
  let vRange as :range be from 1 to 3
  emit Done(nothingValue: vNothing ends with [1], booleanValue: vBoolean ends with [1], integerValue: vInteger ends with [1], floatValue: vFloat ends with [1], percentageValue: vPercentage ends with [1], textValue: vText ends with 'cd', tagValue: vTag ends with #cd, vectorValue: vVector ends with [2, 3], pointValue: vPoint ends with [5, 6], listValue: vList ends with [2, 3], mapValue: vMap ends with [10], diceValue: vDice ends with [2, 1], rangeValue: vRange ends with [2, 3])
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
              value: true
          - name: "tagValue"
            value:
              type: ":boolean"
              value: true
          - name: "vectorValue"
            value:
              type: ":boolean"
              value: false
          - name: "pointValue"
            value:
              type: ":boolean"
              value: false
          - name: "listValue"
            value:
              type: ":boolean"
              value: true
          - name: "mapValue"
            value:
              type: ":boolean"
              value: false
          - name: "diceValue"
            value:
              type: ":boolean"
              value: true
          - name: "rangeValue"
            value:
              type: ":boolean"
              value: true
```

---

## Test: contains operator

This runtime case exercises “contains operator” and verifies the declared messages, values, and execution result.

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
  sequence: ["3", "2", "1"]
sources:
  - name: "contains operator.ges"
    program: main
```

### Source code under test

```ges
module AtomicCollectionContainsOperator
on Start {
  let vNothing be nothing
  let vBoolean be true
  let vInteger be 10
  let vFloat be 10.5
  let vPercentage be 25%
  let vText be 'abcd'
  let vTag be #abcd
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(4, 5, 6)
  let vList be [1, 2, 3]
  let vMap be [name: 'Ada', hp: 10]
  let vDice be roll dice 3d3
  let vRange as :range be from 1 to 3
  emit Done(nothingValue: 1 in vNothing, booleanValue: 1 in vBoolean, integerValue: 1 in vInteger, floatValue: 1 in vFloat, percentageValue: 1 in vPercentage, textValue: 'bc' in vText, tagValue: #bc in vTag, vectorValue: 2 in vVector, pointValue: 5 in vPoint, listValue: 2 in vList, mapValue: #hp in vMap, diceValue: 2 in vDice, rangeValue: 2 in vRange)
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
              value: true
          - name: "tagValue"
            value:
              type: ":boolean"
              value: true
          - name: "vectorValue"
            value:
              type: ":boolean"
              value: true
          - name: "pointValue"
            value:
              type: ":boolean"
              value: true
          - name: "listValue"
            value:
              type: ":boolean"
              value: true
          - name: "mapValue"
            value:
              type: ":boolean"
              value: true
          - name: "diceValue"
            value:
              type: ":boolean"
              value: true
          - name: "rangeValue"
            value:
              type: ":boolean"
              value: true
```

---

## Test: contains any and all operators

This runtime case exercises “contains any and all operators” and verifies the declared messages, values, and execution result.

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
  - name: "contains any and all operators.ges"
    program: main
```

### Source code under test

```ges
module AtomicCollectionContainsAnyAllOperators
on Start {
  let vNothing be nothing
  let vBoolean be true
  let vText be 'abcd'
  let vTag be #abcd
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(4, 5, 6)
  let vList be [1, 2, 3]
  let vMap be [name: 'Ada', hp: 10]
  let vDice as :dice be [3, 2, 1]
  let vRange as :range be from 1 to 3
  emit Done(nothingAny: vNothing[:contains any [1]], nothingAll: vNothing[:contains all [1]], booleanAny: vBoolean[:contains any [1]], booleanAll: vBoolean[:contains all [1]], textAny: vText[:contains any ['xy', 'bc']], textAll: vText[:contains all ['ab', 'cd']], tagAny: vTag[:contains any [#xy, #bc]], tagAll: vTag[:contains all [#ab, #cd]], vectorAny: vVector[:contains any [9, 2]], vectorAll: vVector[:contains all [1, 3]], pointAny: vPoint[:contains any [9, 5]], pointAll: vPoint[:contains all [4, 6]], listAny: vList[:contains any [0, 2]], listAll: vList[:contains all [1, 3]], listAllMissing: vList[:contains all [1, 9]], mapAny: vMap[:contains any [#missing, #hp]], mapAll: vMap[:contains all [#name, #hp]], diceAny: vDice[:contains any [9, 2]], diceAll: vDice[:contains all [3, 2]], rangeAny: vRange[:contains any [9, 2]], rangeAll: vRange[:contains all [1, 3]], emptyAny: vList[:contains any []], emptyAll: vList[:contains all []])
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
          - name: "nothingAny"
            value:
              type: ":nothing"
          - name: "nothingAll"
            value:
              type: ":nothing"
          - name: "booleanAny"
            value:
              type: ":boolean"
              value: false
          - name: "booleanAll"
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
              value: true
          - name: "tagAny"
            value:
              type: ":boolean"
              value: true
          - name: "tagAll"
            value:
              type: ":boolean"
              value: true
          - name: "vectorAny"
            value:
              type: ":boolean"
              value: true
          - name: "vectorAll"
            value:
              type: ":boolean"
              value: true
          - name: "pointAny"
            value:
              type: ":boolean"
              value: true
          - name: "pointAll"
            value:
              type: ":boolean"
              value: true
          - name: "listAny"
            value:
              type: ":boolean"
              value: true
          - name: "listAll"
            value:
              type: ":boolean"
              value: true
          - name: "listAllMissing"
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
              value: true
          - name: "emptyAny"
            value:
              type: ":boolean"
              value: false
          - name: "emptyAll"
            value:
              type: ":boolean"
              value: true
```

---

## Test: contains value operator

This runtime case exercises “contains value operator” and verifies the declared messages, values, and execution result.

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
  sequence: ["3", "2", "1"]
sources:
  - name: "contains value operator.ges"
    program: main
```

### Source code under test

```ges
module AtomicCollectionContainsValueOperator
on Start {
  let vNothing be nothing
  let vBoolean be true
  let vInteger be 10
  let vFloat be 10.5
  let vPercentage be 25%
  let vText be 'abcd'
  let vTag be #abcd
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(4, 5, 6)
  let vList be [1, 2, 3]
  let vMap be [name: 'Ada', hp: 10]
  let vDice be roll dice 3d3
  let vRange as :range be from 1 to 3
  emit Done(nothingValue: 1 in values of vNothing, booleanValue: 1 in values of vBoolean, integerValue: 1 in values of vInteger, floatValue: 1 in values of vFloat, percentageValue: 1 in values of vPercentage, textValue: 'bc' in values of vText, tagValue: #bc in values of vTag, vectorValue: 2 in values of vVector, pointValue: 5 in values of vPoint, listValue: 2 in values of vList, mapValue: 10 in values of vMap, diceValue: 2 in values of vDice, rangeValue: 2 in values of vRange)
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
              value: true
          - name: "pointValue"
            value:
              type: ":boolean"
              value: true
          - name: "listValue"
            value:
              type: ":boolean"
              value: false
          - name: "mapValue"
            value:
              type: ":boolean"
              value: true
          - name: "diceValue"
            value:
              type: ":boolean"
              value: false
          - name: "rangeValue"
            value:
              type: ":boolean"
              value: false
```

---

## Test: union operator

This runtime case exercises “union operator” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "union operator.ges"
    program: main
```

### Source code under test

```ges
module AtomicCollectionUnionOperator
on Start {
  let vNothing be nothing
  let vInteger be 10
  let vList be [1, 2]
  let vMap be [name: 'Ada', hp: 10]
  let vDice as :dice be [3, 1]
  let vDiceOther as :dice be [2]
  emit Done(nothingValue: vNothing | [2], integerValue: vInteger | [2], listValue: vList | [3, 4], listDiceValue: vList | vDice, diceListValue: vDice | [2, 4], diceValue: vDice | vDiceOther, mapValue: vMap | [hp: 12, mp: 5], mapKeyListValue: vMap | [#mp, 'alive'], mapInvalidKeyList: vMap | [#mp, 1], diceIntegerValue: vDice | 2)
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
          - name: "listValue"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "4"
          - name: "listDiceValue"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "1"
          - name: "diceListValue"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "4"
          - name: "diceValue"
            value:
              type: ":dice"
              rolls:
                - 3
                - 2
                - 1
          - name: "mapValue"
            value:
              type: ":map"
              entries:
                - key: "hp"
                  value:
                    type: ":integer"
                    value: "12"
                - key: "mp"
                  value:
                    type: ":integer"
                    value: "5"
                - key: "name"
                  value:
                    type: ":text"
                    value: "Ada"
          - name: "mapKeyListValue"
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
                - key: "mp"
                  value:
                    type: ":boolean"
                    value: true
                - key: "name"
                  value:
                    type: ":text"
                    value: "Ada"
          - name: "mapInvalidKeyList"
            value:
              type: ":nothing"
          - name: "diceIntegerValue"
            value:
              type: ":nothing"
```

---

## Test: intersect operator

This runtime case exercises “intersect operator” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["3", "2", "1"]
sources:
  - name: "intersect operator.ges"
    program: main
```

### Source code under test

```ges
module AtomicCollectionIntersectOperator
on Start {
  let vNothing be nothing
  let vBoolean be true
  let vInteger be 10
  let vFloat be 10.5
  let vPercentage be 25%
  let vText be 'abcd'
  let vTag be #abcd
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(4, 5, 6)
  let vList be [1, 2, 2, 3]
  let vMap be [name: 'Ada', hp: 10]
  let vDice be roll dice 3d3
  let vRange as :range be from 1 to 3
  emit Done(nothingValue: vNothing & [2], booleanValue: vBoolean & [2], integerValue: vInteger & [2], floatValue: vFloat & [2], percentageValue: vPercentage & [2], textValue: vText & [2], tagValue: vTag & [2], vectorValue: vVector & [2], pointValue: vPoint & [2], listValue: vList & [2, 2, 4], mapValue: vMap & [hp: 99, extra: 1], diceValue: vDice & [2, 4], rangeValue: vRange & [2])
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
          - name: "booleanValue"
            value:
              type: ":nothing"
          - name: "integerValue"
            value:
              type: ":nothing"
          - name: "floatValue"
            value:
              type: ":nothing"
          - name: "percentageValue"
            value:
              type: ":nothing"
          - name: "textValue"
            value:
              type: ":nothing"
          - name: "tagValue"
            value:
              type: ":nothing"
          - name: "vectorValue"
            value:
              type: ":nothing"
          - name: "pointValue"
            value:
              type: ":nothing"
          - name: "listValue"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "2"
          - name: "mapValue"
            value:
              type: ":map"
              entries:
                - key: "hp"
                  value:
                    type: ":integer"
                    value: "10"
          - name: "diceValue"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "2"
          - name: "rangeValue"
            value:
              type: ":nothing"
```

---

## Test: zip operator

This runtime case exercises “zip operator” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["3", "2", "1"]
sources:
  - name: "zip operator.ges"
    program: main
```

### Source code under test

```ges
module AtomicCollectionZipOperator
on Start {
  let vNothing be nothing
  let vBoolean be true
  let vInteger be 10
  let vFloat be 10.5
  let vPercentage be 25%
  let vText be 'abcd'
  let vTag be #abcd
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(4, 5, 6)
  let vList be [1, 2, 3]
  let vMap be [name: 'Ada', hp: 10]
  let vDice be roll dice 3d3
  let vRange as :range be from 1 to 3
  emit Done(nothingValue: vNothing zip ['a'], booleanValue: vBoolean zip ['a'], integerValue: vInteger zip ['a'], floatValue: vFloat zip ['a'], percentageValue: vPercentage zip ['a'], textValue: vText zip ['a'], tagValue: vTag zip ['a'], vectorValue: vVector zip ['a'], pointValue: vPoint zip ['a'], listValue: vList zip ['a', 'b'], mapValue: vMap zip ['a'], diceValue: vDice zip ['a', 'b'], rangeValue: vRange zip ['a'])
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
          - name: "booleanValue"
            value:
              type: ":nothing"
          - name: "integerValue"
            value:
              type: ":nothing"
          - name: "floatValue"
            value:
              type: ":nothing"
          - name: "percentageValue"
            value:
              type: ":nothing"
          - name: "textValue"
            value:
              type: ":nothing"
          - name: "tagValue"
            value:
              type: ":nothing"
          - name: "vectorValue"
            value:
              type: ":nothing"
          - name: "pointValue"
            value:
              type: ":nothing"
          - name: "listValue"
            value:
              type: ":list"
              items:
                - type: ":map"
                  entries:
                    - key: "left"
                      value:
                        type: ":integer"
                        value: "1"
                    - key: "right"
                      value:
                        type: ":text"
                        value: "a"
                - type: ":map"
                  entries:
                    - key: "left"
                      value:
                        type: ":integer"
                        value: "2"
                    - key: "right"
                      value:
                        type: ":text"
                        value: "b"
          - name: "mapValue"
            value:
              type: ":nothing"
          - name: "diceValue"
            value:
              type: ":nothing"
          - name: "rangeValue"
            value:
              type: ":nothing"
```
