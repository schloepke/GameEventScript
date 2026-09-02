---
formatVersion: 1
suiteId: "runtime.atomic.equality"
title: "RuntimeAtomicEquality"
categories: [conformance]
tags: [migrated-json-v1]
---

# RuntimeAtomicEquality

Mechanically migrated from the former JSON conformance corpus.

## Test: equal left nothing

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left nothing.ges"
    program: main
```

```ges
module AtomicEqualityEqualLeftNothing
on Start {
  let left be nothing
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":nothing"
          - name: "integer"
            value:
              type: ":nothing"
          - name: "float"
            value:
              type: ":nothing"
          - name: "percentage"
            value:
              type: ":nothing"
          - name: "quantity"
            value:
              type: ":nothing"
          - name: "text"
            value:
              type: ":nothing"
          - name: "numericTag"
            value:
              type: ":nothing"
          - name: "plainTag"
            value:
              type: ":nothing"
          - name: "vector"
            value:
              type: ":nothing"
          - name: "point"
            value:
              type: ":nothing"
          - name: "list"
            value:
              type: ":nothing"
          - name: "map"
            value:
              type: ":nothing"
          - name: "dice"
            value:
              type: ":nothing"
          - name: "range"
            value:
              type: ":nothing"
          - name: "message"
            value:
              type: ":nothing"
          - name: "handler"
            value:
              type: ":nothing"
          - name: "seriesValue"
            value:
              type: ":nothing"
```

## Test: equal left boolean

```yaml
gesBlock: case
id: case-0002
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left boolean.ges"
    program: main
```

```ges
module AtomicEqualityEqualLeftBoolean
on Start {
  let left be true
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "integer"
            value:
              type: ":boolean"
              value: false
          - name: "float"
            value:
              type: ":boolean"
              value: false
          - name: "percentage"
            value:
              type: ":boolean"
              value: false
          - name: "quantity"
            value:
              type: ":boolean"
              value: false
          - name: "text"
            value:
              type: ":boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
          - name: "range"
            value:
              type: ":boolean"
              value: false
          - name: "message"
            value:
              type: ":boolean"
              value: false
          - name: "handler"
            value:
              type: ":boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: false
```

## Test: equal left integer

```yaml
gesBlock: case
id: case-0003
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left integer.ges"
    program: main
```

```ges
module AtomicEqualityEqualLeftInteger
on Start {
  let left be 10
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: false
          - name: "integer"
            value:
              type: ":boolean"
              value: true
          - name: "float"
            value:
              type: ":boolean"
              value: false
          - name: "percentage"
            value:
              type: ":boolean"
              value: true
          - name: "quantity"
            value:
              type: ":boolean"
              value: false
          - name: "text"
            value:
              type: ":boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: true
          - name: "range"
            value:
              type: ":boolean"
              value: false
          - name: "message"
            value:
              type: ":boolean"
              value: false
          - name: "handler"
            value:
              type: ":boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: false
```

## Test: equal left float

```yaml
gesBlock: case
id: case-0004
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left float.ges"
    program: main
```

```ges
module AtomicEqualityEqualLeftFloat
on Start {
  let left be 10.5
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: false
          - name: "integer"
            value:
              type: ":boolean"
              value: false
          - name: "float"
            value:
              type: ":boolean"
              value: true
          - name: "percentage"
            value:
              type: ":boolean"
              value: false
          - name: "quantity"
            value:
              type: ":boolean"
              value: false
          - name: "text"
            value:
              type: ":boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
          - name: "range"
            value:
              type: ":boolean"
              value: false
          - name: "message"
            value:
              type: ":boolean"
              value: false
          - name: "handler"
            value:
              type: ":boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: false
```

## Test: equal left percentage

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left percentage.ges"
    program: main
```

```ges
module AtomicEqualityEqualLeftPercentage
on Start {
  let left be 1000%
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: false
          - name: "integer"
            value:
              type: ":boolean"
              value: true
          - name: "float"
            value:
              type: ":boolean"
              value: false
          - name: "percentage"
            value:
              type: ":boolean"
              value: true
          - name: "quantity"
            value:
              type: ":boolean"
              value: false
          - name: "text"
            value:
              type: ":boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: true
          - name: "range"
            value:
              type: ":boolean"
              value: false
          - name: "message"
            value:
              type: ":boolean"
              value: false
          - name: "handler"
            value:
              type: ":boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: false
```

## Test: equal left quantity

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left quantity.ges"
    program: main
```

```ges
module AtomicEqualityEqualLeftQuantity
on Start {
  let left be 10m
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: false
          - name: "integer"
            value:
              type: ":boolean"
              value: false
          - name: "float"
            value:
              type: ":boolean"
              value: false
          - name: "percentage"
            value:
              type: ":boolean"
              value: false
          - name: "quantity"
            value:
              type: ":boolean"
              value: true
          - name: "text"
            value:
              type: ":boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
          - name: "range"
            value:
              type: ":boolean"
              value: false
          - name: "message"
            value:
              type: ":boolean"
              value: false
          - name: "handler"
            value:
              type: ":boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: false
```

## Test: equal left text

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left text.ges"
    program: main
```

```ges
module AtomicEqualityEqualLeftText
on Start {
  let left be '10'
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: false
          - name: "integer"
            value:
              type: ":boolean"
              value: false
          - name: "float"
            value:
              type: ":boolean"
              value: false
          - name: "percentage"
            value:
              type: ":boolean"
              value: false
          - name: "quantity"
            value:
              type: ":boolean"
              value: false
          - name: "text"
            value:
              type: ":boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
          - name: "range"
            value:
              type: ":boolean"
              value: false
          - name: "message"
            value:
              type: ":boolean"
              value: false
          - name: "handler"
            value:
              type: ":boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: false
```

## Test: equal left numeric tag

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left numeric tag.ges"
    program: main
```

```ges
module AtomicEqualityEqualLeftNumericTag
on Start {
  let left be pi
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: false
          - name: "integer"
            value:
              type: ":boolean"
              value: false
          - name: "float"
            value:
              type: ":boolean"
              value: false
          - name: "percentage"
            value:
              type: ":boolean"
              value: false
          - name: "quantity"
            value:
              type: ":boolean"
              value: false
          - name: "text"
            value:
              type: ":boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
          - name: "range"
            value:
              type: ":boolean"
              value: false
          - name: "message"
            value:
              type: ":boolean"
              value: false
          - name: "handler"
            value:
              type: ":boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: false
```

## Test: equal left plain tag

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left plain tag.ges"
    program: main
```

```ges
module AtomicEqualityEqualLeftPlainTag
on Start {
  let left be #custom
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: false
          - name: "integer"
            value:
              type: ":boolean"
              value: false
          - name: "float"
            value:
              type: ":boolean"
              value: false
          - name: "percentage"
            value:
              type: ":boolean"
              value: false
          - name: "quantity"
            value:
              type: ":boolean"
              value: false
          - name: "text"
            value:
              type: ":boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
          - name: "range"
            value:
              type: ":boolean"
              value: false
          - name: "message"
            value:
              type: ":boolean"
              value: false
          - name: "handler"
            value:
              type: ":boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: false
```

## Test: equal left vector

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left vector.ges"
    program: main
```

```ges
module AtomicEqualityEqualLeftVector
on Start {
  let left be :vector(1, 2, 3)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: false
          - name: "integer"
            value:
              type: ":boolean"
              value: false
          - name: "float"
            value:
              type: ":boolean"
              value: false
          - name: "percentage"
            value:
              type: ":boolean"
              value: false
          - name: "quantity"
            value:
              type: ":boolean"
              value: false
          - name: "text"
            value:
              type: ":boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
          - name: "range"
            value:
              type: ":boolean"
              value: false
          - name: "message"
            value:
              type: ":boolean"
              value: false
          - name: "handler"
            value:
              type: ":boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: false
```

## Test: equal left point

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left point.ges"
    program: main
```

```ges
module AtomicEqualityEqualLeftPoint
on Start {
  let left be :point(1, 2, 3)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: false
          - name: "integer"
            value:
              type: ":boolean"
              value: false
          - name: "float"
            value:
              type: ":boolean"
              value: false
          - name: "percentage"
            value:
              type: ":boolean"
              value: false
          - name: "quantity"
            value:
              type: ":boolean"
              value: false
          - name: "text"
            value:
              type: ":boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
          - name: "range"
            value:
              type: ":boolean"
              value: false
          - name: "message"
            value:
              type: ":boolean"
              value: false
          - name: "handler"
            value:
              type: ":boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: false
```

## Test: equal left list

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left list.ges"
    program: main
```

```ges
module AtomicEqualityEqualLeftList
on Start {
  let left be [1, 'x']
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: false
          - name: "integer"
            value:
              type: ":boolean"
              value: false
          - name: "float"
            value:
              type: ":boolean"
              value: false
          - name: "percentage"
            value:
              type: ":boolean"
              value: false
          - name: "quantity"
            value:
              type: ":boolean"
              value: false
          - name: "text"
            value:
              type: ":boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
          - name: "range"
            value:
              type: ":boolean"
              value: false
          - name: "message"
            value:
              type: ":boolean"
              value: false
          - name: "handler"
            value:
              type: ":boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: false
```

## Test: equal left map

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left map.ges"
    program: main
```

```ges
module AtomicEqualityEqualLeftMap
on Start {
  let left be [hp: 10, name: 'Ada']
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: false
          - name: "integer"
            value:
              type: ":boolean"
              value: false
          - name: "float"
            value:
              type: ":boolean"
              value: false
          - name: "percentage"
            value:
              type: ":boolean"
              value: false
          - name: "quantity"
            value:
              type: ":boolean"
              value: false
          - name: "text"
            value:
              type: ":boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: false
          - name: "range"
            value:
              type: ":boolean"
              value: false
          - name: "message"
            value:
              type: ":boolean"
              value: false
          - name: "handler"
            value:
              type: ":boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: false
```

## Test: equal left dice

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left dice.ges"
    program: main
```

```ges
module AtomicEqualityEqualLeftDice
on Start {
  let left be roll dice 2d6
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: false
          - name: "integer"
            value:
              type: ":boolean"
              value: true
          - name: "float"
            value:
              type: ":boolean"
              value: false
          - name: "percentage"
            value:
              type: ":boolean"
              value: true
          - name: "quantity"
            value:
              type: ":boolean"
              value: false
          - name: "text"
            value:
              type: ":boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: true
          - name: "range"
            value:
              type: ":boolean"
              value: false
          - name: "message"
            value:
              type: ":boolean"
              value: false
          - name: "handler"
            value:
              type: ":boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: false
```

## Test: equal left range

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left range.ges"
    program: main
```

```ges
module AtomicEqualityEqualLeftRange
on Start {
  let left be from 1 to 3
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: false
          - name: "integer"
            value:
              type: ":boolean"
              value: false
          - name: "float"
            value:
              type: ":boolean"
              value: false
          - name: "percentage"
            value:
              type: ":boolean"
              value: false
          - name: "quantity"
            value:
              type: ":boolean"
              value: false
          - name: "text"
            value:
              type: ":boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
          - name: "range"
            value:
              type: ":boolean"
              value: true
          - name: "message"
            value:
              type: ":boolean"
              value: false
          - name: "handler"
            value:
              type: ":boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: false
```

## Test: equal left message

```yaml
gesBlock: case
id: case-0016
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left message.ges"
    program: main
```

```ges
module AtomicEqualityEqualLeftMessage
on Start {
  let left be Ping(amount: 10)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: false
          - name: "integer"
            value:
              type: ":boolean"
              value: false
          - name: "float"
            value:
              type: ":boolean"
              value: false
          - name: "percentage"
            value:
              type: ":boolean"
              value: false
          - name: "quantity"
            value:
              type: ":boolean"
              value: false
          - name: "text"
            value:
              type: ":boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
          - name: "range"
            value:
              type: ":boolean"
              value: false
          - name: "message"
            value:
              type: ":boolean"
              value: true
          - name: "handler"
            value:
              type: ":boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: false
```

## Test: equal left handler

```yaml
gesBlock: case
id: case-0017
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left handler.ges"
    program: main
```

```ges
module AtomicEqualityEqualLeftHandler
on Start {
  let left be Ping(amount)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: false
          - name: "integer"
            value:
              type: ":boolean"
              value: false
          - name: "float"
            value:
              type: ":boolean"
              value: false
          - name: "percentage"
            value:
              type: ":boolean"
              value: false
          - name: "quantity"
            value:
              type: ":boolean"
              value: false
          - name: "text"
            value:
              type: ":boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
          - name: "range"
            value:
              type: ":boolean"
              value: false
          - name: "message"
            value:
              type: ":boolean"
              value: false
          - name: "handler"
            value:
              type: ":boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: false
```

## Test: equal left series

```yaml
gesBlock: case
id: case-0018
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left series.ges"
    program: main
```

```ges
module AtomicEqualityEqualLeftSeries
on Start {
  let left be series fibonacci
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: false
          - name: "integer"
            value:
              type: ":boolean"
              value: false
          - name: "float"
            value:
              type: ":boolean"
              value: false
          - name: "percentage"
            value:
              type: ":boolean"
              value: false
          - name: "quantity"
            value:
              type: ":boolean"
              value: false
          - name: "text"
            value:
              type: ":boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
          - name: "range"
            value:
              type: ":boolean"
              value: false
          - name: "message"
            value:
              type: ":boolean"
              value: false
          - name: "handler"
            value:
              type: ":boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: true
```

## Test: not equal left nothing

```yaml
gesBlock: case
id: case-0019
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left nothing.ges"
    program: main
```

```ges
module AtomicEqualityNotEqualLeftNothing
on Start {
  let left be nothing
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":nothing"
          - name: "integer"
            value:
              type: ":nothing"
          - name: "float"
            value:
              type: ":nothing"
          - name: "percentage"
            value:
              type: ":nothing"
          - name: "quantity"
            value:
              type: ":nothing"
          - name: "text"
            value:
              type: ":nothing"
          - name: "numericTag"
            value:
              type: ":nothing"
          - name: "plainTag"
            value:
              type: ":nothing"
          - name: "vector"
            value:
              type: ":nothing"
          - name: "point"
            value:
              type: ":nothing"
          - name: "list"
            value:
              type: ":nothing"
          - name: "map"
            value:
              type: ":nothing"
          - name: "dice"
            value:
              type: ":nothing"
          - name: "range"
            value:
              type: ":nothing"
          - name: "message"
            value:
              type: ":nothing"
          - name: "handler"
            value:
              type: ":nothing"
          - name: "seriesValue"
            value:
              type: ":nothing"
```

## Test: not equal left boolean

```yaml
gesBlock: case
id: case-0020
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left boolean.ges"
    program: main
```

```ges
module AtomicEqualityNotEqualLeftBoolean
on Start {
  let left be true
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: false
          - name: "integer"
            value:
              type: ":boolean"
              value: true
          - name: "float"
            value:
              type: ":boolean"
              value: true
          - name: "percentage"
            value:
              type: ":boolean"
              value: true
          - name: "quantity"
            value:
              type: ":boolean"
              value: true
          - name: "text"
            value:
              type: ":boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
          - name: "range"
            value:
              type: ":boolean"
              value: true
          - name: "message"
            value:
              type: ":boolean"
              value: true
          - name: "handler"
            value:
              type: ":boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: true
```

## Test: not equal left integer

```yaml
gesBlock: case
id: case-0021
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left integer.ges"
    program: main
```

```ges
module AtomicEqualityNotEqualLeftInteger
on Start {
  let left be 10
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "integer"
            value:
              type: ":boolean"
              value: false
          - name: "float"
            value:
              type: ":boolean"
              value: true
          - name: "percentage"
            value:
              type: ":boolean"
              value: false
          - name: "quantity"
            value:
              type: ":boolean"
              value: true
          - name: "text"
            value:
              type: ":boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: false
          - name: "range"
            value:
              type: ":boolean"
              value: true
          - name: "message"
            value:
              type: ":boolean"
              value: true
          - name: "handler"
            value:
              type: ":boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: true
```

## Test: not equal left float

```yaml
gesBlock: case
id: case-0022
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left float.ges"
    program: main
```

```ges
module AtomicEqualityNotEqualLeftFloat
on Start {
  let left be 10.5
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "integer"
            value:
              type: ":boolean"
              value: true
          - name: "float"
            value:
              type: ":boolean"
              value: false
          - name: "percentage"
            value:
              type: ":boolean"
              value: true
          - name: "quantity"
            value:
              type: ":boolean"
              value: true
          - name: "text"
            value:
              type: ":boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
          - name: "range"
            value:
              type: ":boolean"
              value: true
          - name: "message"
            value:
              type: ":boolean"
              value: true
          - name: "handler"
            value:
              type: ":boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: true
```

## Test: not equal left percentage

```yaml
gesBlock: case
id: case-0023
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left percentage.ges"
    program: main
```

```ges
module AtomicEqualityNotEqualLeftPercentage
on Start {
  let left be 1000%
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "integer"
            value:
              type: ":boolean"
              value: false
          - name: "float"
            value:
              type: ":boolean"
              value: true
          - name: "percentage"
            value:
              type: ":boolean"
              value: false
          - name: "quantity"
            value:
              type: ":boolean"
              value: true
          - name: "text"
            value:
              type: ":boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: false
          - name: "range"
            value:
              type: ":boolean"
              value: true
          - name: "message"
            value:
              type: ":boolean"
              value: true
          - name: "handler"
            value:
              type: ":boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: true
```

## Test: not equal left quantity

```yaml
gesBlock: case
id: case-0024
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left quantity.ges"
    program: main
```

```ges
module AtomicEqualityNotEqualLeftQuantity
on Start {
  let left be 10m
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "integer"
            value:
              type: ":boolean"
              value: true
          - name: "float"
            value:
              type: ":boolean"
              value: true
          - name: "percentage"
            value:
              type: ":boolean"
              value: true
          - name: "quantity"
            value:
              type: ":boolean"
              value: false
          - name: "text"
            value:
              type: ":boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
          - name: "range"
            value:
              type: ":boolean"
              value: true
          - name: "message"
            value:
              type: ":boolean"
              value: true
          - name: "handler"
            value:
              type: ":boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: true
```

## Test: not equal left text

```yaml
gesBlock: case
id: case-0025
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left text.ges"
    program: main
```

```ges
module AtomicEqualityNotEqualLeftText
on Start {
  let left be '10'
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "integer"
            value:
              type: ":boolean"
              value: true
          - name: "float"
            value:
              type: ":boolean"
              value: true
          - name: "percentage"
            value:
              type: ":boolean"
              value: true
          - name: "quantity"
            value:
              type: ":boolean"
              value: true
          - name: "text"
            value:
              type: ":boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
          - name: "range"
            value:
              type: ":boolean"
              value: true
          - name: "message"
            value:
              type: ":boolean"
              value: true
          - name: "handler"
            value:
              type: ":boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: true
```

## Test: not equal left numeric tag

```yaml
gesBlock: case
id: case-0026
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left numeric tag.ges"
    program: main
```

```ges
module AtomicEqualityNotEqualLeftNumericTag
on Start {
  let left be pi
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "integer"
            value:
              type: ":boolean"
              value: true
          - name: "float"
            value:
              type: ":boolean"
              value: true
          - name: "percentage"
            value:
              type: ":boolean"
              value: true
          - name: "quantity"
            value:
              type: ":boolean"
              value: true
          - name: "text"
            value:
              type: ":boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
          - name: "range"
            value:
              type: ":boolean"
              value: true
          - name: "message"
            value:
              type: ":boolean"
              value: true
          - name: "handler"
            value:
              type: ":boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: true
```

## Test: not equal left plain tag

```yaml
gesBlock: case
id: case-0027
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left plain tag.ges"
    program: main
```

```ges
module AtomicEqualityNotEqualLeftPlainTag
on Start {
  let left be #custom
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "integer"
            value:
              type: ":boolean"
              value: true
          - name: "float"
            value:
              type: ":boolean"
              value: true
          - name: "percentage"
            value:
              type: ":boolean"
              value: true
          - name: "quantity"
            value:
              type: ":boolean"
              value: true
          - name: "text"
            value:
              type: ":boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
          - name: "range"
            value:
              type: ":boolean"
              value: true
          - name: "message"
            value:
              type: ":boolean"
              value: true
          - name: "handler"
            value:
              type: ":boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: true
```

## Test: not equal left vector

```yaml
gesBlock: case
id: case-0028
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left vector.ges"
    program: main
```

```ges
module AtomicEqualityNotEqualLeftVector
on Start {
  let left be :vector(1, 2, 3)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "integer"
            value:
              type: ":boolean"
              value: true
          - name: "float"
            value:
              type: ":boolean"
              value: true
          - name: "percentage"
            value:
              type: ":boolean"
              value: true
          - name: "quantity"
            value:
              type: ":boolean"
              value: true
          - name: "text"
            value:
              type: ":boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
          - name: "range"
            value:
              type: ":boolean"
              value: true
          - name: "message"
            value:
              type: ":boolean"
              value: true
          - name: "handler"
            value:
              type: ":boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: true
```

## Test: not equal left point

```yaml
gesBlock: case
id: case-0029
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left point.ges"
    program: main
```

```ges
module AtomicEqualityNotEqualLeftPoint
on Start {
  let left be :point(1, 2, 3)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "integer"
            value:
              type: ":boolean"
              value: true
          - name: "float"
            value:
              type: ":boolean"
              value: true
          - name: "percentage"
            value:
              type: ":boolean"
              value: true
          - name: "quantity"
            value:
              type: ":boolean"
              value: true
          - name: "text"
            value:
              type: ":boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
          - name: "range"
            value:
              type: ":boolean"
              value: true
          - name: "message"
            value:
              type: ":boolean"
              value: true
          - name: "handler"
            value:
              type: ":boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: true
```

## Test: not equal left list

```yaml
gesBlock: case
id: case-0030
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left list.ges"
    program: main
```

```ges
module AtomicEqualityNotEqualLeftList
on Start {
  let left be [1, 'x']
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "integer"
            value:
              type: ":boolean"
              value: true
          - name: "float"
            value:
              type: ":boolean"
              value: true
          - name: "percentage"
            value:
              type: ":boolean"
              value: true
          - name: "quantity"
            value:
              type: ":boolean"
              value: true
          - name: "text"
            value:
              type: ":boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
          - name: "range"
            value:
              type: ":boolean"
              value: true
          - name: "message"
            value:
              type: ":boolean"
              value: true
          - name: "handler"
            value:
              type: ":boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: true
```

## Test: not equal left map

```yaml
gesBlock: case
id: case-0031
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left map.ges"
    program: main
```

```ges
module AtomicEqualityNotEqualLeftMap
on Start {
  let left be [hp: 10, name: 'Ada']
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "integer"
            value:
              type: ":boolean"
              value: true
          - name: "float"
            value:
              type: ":boolean"
              value: true
          - name: "percentage"
            value:
              type: ":boolean"
              value: true
          - name: "quantity"
            value:
              type: ":boolean"
              value: true
          - name: "text"
            value:
              type: ":boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: true
          - name: "range"
            value:
              type: ":boolean"
              value: true
          - name: "message"
            value:
              type: ":boolean"
              value: true
          - name: "handler"
            value:
              type: ":boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: true
```

## Test: not equal left dice

```yaml
gesBlock: case
id: case-0032
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left dice.ges"
    program: main
```

```ges
module AtomicEqualityNotEqualLeftDice
on Start {
  let left be roll dice 2d6
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "integer"
            value:
              type: ":boolean"
              value: false
          - name: "float"
            value:
              type: ":boolean"
              value: true
          - name: "percentage"
            value:
              type: ":boolean"
              value: false
          - name: "quantity"
            value:
              type: ":boolean"
              value: true
          - name: "text"
            value:
              type: ":boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: false
          - name: "range"
            value:
              type: ":boolean"
              value: true
          - name: "message"
            value:
              type: ":boolean"
              value: true
          - name: "handler"
            value:
              type: ":boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: true
```

## Test: not equal left range

```yaml
gesBlock: case
id: case-0033
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left range.ges"
    program: main
```

```ges
module AtomicEqualityNotEqualLeftRange
on Start {
  let left be from 1 to 3
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "integer"
            value:
              type: ":boolean"
              value: true
          - name: "float"
            value:
              type: ":boolean"
              value: true
          - name: "percentage"
            value:
              type: ":boolean"
              value: true
          - name: "quantity"
            value:
              type: ":boolean"
              value: true
          - name: "text"
            value:
              type: ":boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
          - name: "range"
            value:
              type: ":boolean"
              value: false
          - name: "message"
            value:
              type: ":boolean"
              value: true
          - name: "handler"
            value:
              type: ":boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: true
```

## Test: not equal left message

```yaml
gesBlock: case
id: case-0034
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left message.ges"
    program: main
```

```ges
module AtomicEqualityNotEqualLeftMessage
on Start {
  let left be Ping(amount: 10)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "integer"
            value:
              type: ":boolean"
              value: true
          - name: "float"
            value:
              type: ":boolean"
              value: true
          - name: "percentage"
            value:
              type: ":boolean"
              value: true
          - name: "quantity"
            value:
              type: ":boolean"
              value: true
          - name: "text"
            value:
              type: ":boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
          - name: "range"
            value:
              type: ":boolean"
              value: true
          - name: "message"
            value:
              type: ":boolean"
              value: false
          - name: "handler"
            value:
              type: ":boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: true
```

## Test: not equal left handler

```yaml
gesBlock: case
id: case-0035
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left handler.ges"
    program: main
```

```ges
module AtomicEqualityNotEqualLeftHandler
on Start {
  let left be Ping(amount)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "integer"
            value:
              type: ":boolean"
              value: true
          - name: "float"
            value:
              type: ":boolean"
              value: true
          - name: "percentage"
            value:
              type: ":boolean"
              value: true
          - name: "quantity"
            value:
              type: ":boolean"
              value: true
          - name: "text"
            value:
              type: ":boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
          - name: "range"
            value:
              type: ":boolean"
              value: true
          - name: "message"
            value:
              type: ":boolean"
              value: true
          - name: "handler"
            value:
              type: ":boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: true
```

## Test: not equal left series

```yaml
gesBlock: case
id: case-0036
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left series.ges"
    program: main
```

```ges
module AtomicEqualityNotEqualLeftSeries
on Start {
  let left be series fibonacci
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :vector(1, 2, 3)
  let rightPoint be :point(1, 2, 3)
  let rightList be [1, 'x']
  let rightMap be [name: 'Ada', hp: 10]
  let rightDice be roll dice 2d6
  let rightRange be from 1 to 3
  let rightMessage be Ping(amount: 10)
  let rightHandler be Ping(amount)
  let rightSeries be series fibonacci
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "integer"
            value:
              type: ":boolean"
              value: true
          - name: "float"
            value:
              type: ":boolean"
              value: true
          - name: "percentage"
            value:
              type: ":boolean"
              value: true
          - name: "quantity"
            value:
              type: ":boolean"
              value: true
          - name: "text"
            value:
              type: ":boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
          - name: "range"
            value:
              type: ":boolean"
              value: true
          - name: "message"
            value:
              type: ":boolean"
              value: true
          - name: "handler"
            value:
              type: ":boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":boolean"
              value: false
```

## Test: cross kind and structural equality are deterministic

```yaml
gesBlock: case
id: case-0037
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "cross kind and structural equality are deterministic.ges"
    program: main
```

```ges
module AtomicDeterministicEquality
record :first as { value: :number }
record :second as { value: :number }

on Start {
  let leftDice as :dice be [6, 4]
  let rightDice as :dice be [5, 5]
  let first be :first(value: 10)
  let sameFirst be :first(value: 10)
  let second be :second(value: 10)
  let plain be [value: 10]
  emit Done(numericCrossKind: 10 = 1000%, diceBySum: leftDice = rightDice, nestedNumericExact: [10] = [1000%], nestedDiceExact: [leftDice] = [rightDice], textTagDifferent: 'same' = #same, sameRecord: first = sameFirst, differentRecordType: first = second, recordMapDifferent: first = plain, mapRecordDifferent: plain = first, mapsIgnoreSourceOrder: [b: 2, a: 1] = [a: 1, b: 2])
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
          - name: "numericCrossKind"
            value:
              type: ":boolean"
              value: true
          - name: "diceBySum"
            value:
              type: ":boolean"
              value: true
          - name: "nestedNumericExact"
            value:
              type: ":boolean"
              value: false
          - name: "nestedDiceExact"
            value:
              type: ":boolean"
              value: false
          - name: "textTagDifferent"
            value:
              type: ":boolean"
              value: false
          - name: "sameRecord"
            value:
              type: ":boolean"
              value: true
          - name: "differentRecordType"
            value:
              type: ":boolean"
              value: false
          - name: "recordMapDifferent"
            value:
              type: ":boolean"
              value: false
          - name: "mapRecordDifferent"
            value:
              type: ":boolean"
              value: false
          - name: "mapsIgnoreSourceOrder"
            value:
              type: ":boolean"
              value: true
```
