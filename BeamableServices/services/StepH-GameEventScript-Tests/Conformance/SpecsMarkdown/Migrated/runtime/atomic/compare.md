---
formatVersion: 1
suiteId: "runtime.atomic.compare"
title: "RuntimeAtomicCompare"
categories: [conformance]
tags: [migrated-json-v1]
---

# RuntimeAtomicCompare

Mechanically migrated from the former JSON conformance corpus.

## Test: less left nothing

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left nothing.ges"
    program: main
```

```ges
module AtomicCompareA
on Start {
  let left be nothing
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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

## Test: less left boolean

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left boolean.ges"
    program: main
```

```ges
module AtomicCompareB
on Start {
  let left be true
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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

## Test: less left integer

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left integer.ges"
    program: main
```

```ges
module AtomicCompareC
on Start {
  let left be 10
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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
              value: false
```

## Test: less left float

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left float.ges"
    program: main
```

```ges
module AtomicCompareD
on Start {
  let left be 0.6
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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

## Test: less left percentage

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left percentage.ges"
    program: main
```

```ges
module AtomicCompareE
on Start {
  let left be 60%
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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

## Test: less left quantity

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left quantity.ges"
    program: main
```

```ges
module AtomicCompareF
on Start {
  let left be 10m
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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
              value: false
```

## Test: less left text

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left text.ges"
    program: main
```

```ges
module AtomicCompareG
on Start {
  let left be '10'
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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
              value: false
```

## Test: less left numericTag

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left numericTag.ges"
    program: main
```

```ges
module AtomicCompareH
on Start {
  let left be pi
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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

## Test: less left plainTag

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left plainTag.ges"
    program: main
```

```ges
module AtomicCompareI
on Start {
  let left be #custom
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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
              value: false
```

## Test: less left vector

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left vector.ges"
    program: main
```

```ges
module AtomicCompareJ
on Start {
  let left be :vector(1, 2, 3)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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
              value: false
```

## Test: less left point

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left point.ges"
    program: main
```

```ges
module AtomicCompareK
on Start {
  let left be :point(1, 2, 3)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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
              value: false
```

## Test: less left list

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left list.ges"
    program: main
```

```ges
module AtomicCompareL
on Start {
  let left be [1, 'x']
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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
              value: false
```

## Test: less left map

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left map.ges"
    program: main
```

```ges
module AtomicCompareM
on Start {
  let left be [hp: 10, name: 'Ada']
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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
              value: false
```

## Test: less left dice

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left dice.ges"
    program: main
```

```ges
module AtomicCompareN
on Start {
  let left be roll dice 2d6
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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
              value: false
```

## Test: less left range

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left range.ges"
    program: main
```

```ges
module AtomicCompareO
on Start {
  let left be from 1 to 3
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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
              value: false
```

## Test: less left message

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left message.ges"
    program: main
```

```ges
module AtomicCompareP
on Start {
  let left be Ping(amount: 10)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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
              value: false
```

## Test: less left handler

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left handler.ges"
    program: main
```

```ges
module AtomicCompareQ
on Start {
  let left be Ping(amount)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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
              value: false
```

## Test: less left series

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left series.ges"
    program: main
```

```ges
module AtomicCompareR
on Start {
  let left be series fibonacci
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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
              value: false
```

## Test: greater left nothing

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greater left nothing.ges"
    program: main
```

```ges
module AtomicCompareS
on Start {
  let left be nothing
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left > rightNothing, boolean: left > rightBoolean, integer: left > rightInteger, float: left > rightFloat, percentage: left > rightPercentage, quantity: left > rightQuantity, text: left > rightText, numericTag: left > rightNumericTag, plainTag: left > rightPlainTag, vector: left > rightVector, point: left > rightPoint, list: left > rightList, map: left > rightMap, dice: left > rightDice, range: left > rightRange, message: left > rightMessage, handler: left > rightHandler, seriesValue: left > rightSeries)
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

## Test: greater left boolean

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greater left boolean.ges"
    program: main
```

```ges
module AtomicCompareT
on Start {
  let left be true
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left > rightNothing, boolean: left > rightBoolean, integer: left > rightInteger, float: left > rightFloat, percentage: left > rightPercentage, quantity: left > rightQuantity, text: left > rightText, numericTag: left > rightNumericTag, plainTag: left > rightPlainTag, vector: left > rightVector, point: left > rightPoint, list: left > rightList, map: left > rightMap, dice: left > rightDice, range: left > rightRange, message: left > rightMessage, handler: left > rightHandler, seriesValue: left > rightSeries)
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

## Test: greater left integer

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greater left integer.ges"
    program: main
```

```ges
module AtomicCompareU
on Start {
  let left be 10
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left > rightNothing, boolean: left > rightBoolean, integer: left > rightInteger, float: left > rightFloat, percentage: left > rightPercentage, quantity: left > rightQuantity, text: left > rightText, numericTag: left > rightNumericTag, plainTag: left > rightPlainTag, vector: left > rightVector, point: left > rightPoint, list: left > rightList, map: left > rightMap, dice: left > rightDice, range: left > rightRange, message: left > rightMessage, handler: left > rightHandler, seriesValue: left > rightSeries)
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

## Test: greater left float

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greater left float.ges"
    program: main
```

```ges
module AtomicCompareV
on Start {
  let left be 0.6
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left > rightNothing, boolean: left > rightBoolean, integer: left > rightInteger, float: left > rightFloat, percentage: left > rightPercentage, quantity: left > rightQuantity, text: left > rightText, numericTag: left > rightNumericTag, plainTag: left > rightPlainTag, vector: left > rightVector, point: left > rightPoint, list: left > rightList, map: left > rightMap, dice: left > rightDice, range: left > rightRange, message: left > rightMessage, handler: left > rightHandler, seriesValue: left > rightSeries)
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

## Test: greater left percentage

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greater left percentage.ges"
    program: main
```

```ges
module AtomicCompareW
on Start {
  let left be 60%
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left > rightNothing, boolean: left > rightBoolean, integer: left > rightInteger, float: left > rightFloat, percentage: left > rightPercentage, quantity: left > rightQuantity, text: left > rightText, numericTag: left > rightNumericTag, plainTag: left > rightPlainTag, vector: left > rightVector, point: left > rightPoint, list: left > rightList, map: left > rightMap, dice: left > rightDice, range: left > rightRange, message: left > rightMessage, handler: left > rightHandler, seriesValue: left > rightSeries)
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

## Test: greater left quantity

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greater left quantity.ges"
    program: main
```

```ges
module AtomicCompareX
on Start {
  let left be 10m
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left > rightNothing, boolean: left > rightBoolean, integer: left > rightInteger, float: left > rightFloat, percentage: left > rightPercentage, quantity: left > rightQuantity, text: left > rightText, numericTag: left > rightNumericTag, plainTag: left > rightPlainTag, vector: left > rightVector, point: left > rightPoint, list: left > rightList, map: left > rightMap, dice: left > rightDice, range: left > rightRange, message: left > rightMessage, handler: left > rightHandler, seriesValue: left > rightSeries)
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
              value: false
```

## Test: greater left text

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greater left text.ges"
    program: main
```

```ges
module AtomicCompareY
on Start {
  let left be '10'
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left > rightNothing, boolean: left > rightBoolean, integer: left > rightInteger, float: left > rightFloat, percentage: left > rightPercentage, quantity: left > rightQuantity, text: left > rightText, numericTag: left > rightNumericTag, plainTag: left > rightPlainTag, vector: left > rightVector, point: left > rightPoint, list: left > rightList, map: left > rightMap, dice: left > rightDice, range: left > rightRange, message: left > rightMessage, handler: left > rightHandler, seriesValue: left > rightSeries)
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
              value: false
```

## Test: greater left numericTag

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greater left numericTag.ges"
    program: main
```

```ges
module AtomicCompareZ
on Start {
  let left be pi
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left > rightNothing, boolean: left > rightBoolean, integer: left > rightInteger, float: left > rightFloat, percentage: left > rightPercentage, quantity: left > rightQuantity, text: left > rightText, numericTag: left > rightNumericTag, plainTag: left > rightPlainTag, vector: left > rightVector, point: left > rightPoint, list: left > rightList, map: left > rightMap, dice: left > rightDice, range: left > rightRange, message: left > rightMessage, handler: left > rightHandler, seriesValue: left > rightSeries)
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

## Test: greater left plainTag

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greater left plainTag.ges"
    program: main
```

```ges
module AtomicCompareAA
on Start {
  let left be #custom
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left > rightNothing, boolean: left > rightBoolean, integer: left > rightInteger, float: left > rightFloat, percentage: left > rightPercentage, quantity: left > rightQuantity, text: left > rightText, numericTag: left > rightNumericTag, plainTag: left > rightPlainTag, vector: left > rightVector, point: left > rightPoint, list: left > rightList, map: left > rightMap, dice: left > rightDice, range: left > rightRange, message: left > rightMessage, handler: left > rightHandler, seriesValue: left > rightSeries)
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
              value: false
```

## Test: greater left vector

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greater left vector.ges"
    program: main
```

```ges
module AtomicCompareAB
on Start {
  let left be :vector(1, 2, 3)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left > rightNothing, boolean: left > rightBoolean, integer: left > rightInteger, float: left > rightFloat, percentage: left > rightPercentage, quantity: left > rightQuantity, text: left > rightText, numericTag: left > rightNumericTag, plainTag: left > rightPlainTag, vector: left > rightVector, point: left > rightPoint, list: left > rightList, map: left > rightMap, dice: left > rightDice, range: left > rightRange, message: left > rightMessage, handler: left > rightHandler, seriesValue: left > rightSeries)
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
              value: false
```

## Test: greater left point

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greater left point.ges"
    program: main
```

```ges
module AtomicCompareAC
on Start {
  let left be :point(1, 2, 3)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left > rightNothing, boolean: left > rightBoolean, integer: left > rightInteger, float: left > rightFloat, percentage: left > rightPercentage, quantity: left > rightQuantity, text: left > rightText, numericTag: left > rightNumericTag, plainTag: left > rightPlainTag, vector: left > rightVector, point: left > rightPoint, list: left > rightList, map: left > rightMap, dice: left > rightDice, range: left > rightRange, message: left > rightMessage, handler: left > rightHandler, seriesValue: left > rightSeries)
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
              value: false
```

## Test: greater left list

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greater left list.ges"
    program: main
```

```ges
module AtomicCompareAD
on Start {
  let left be [1, 'x']
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left > rightNothing, boolean: left > rightBoolean, integer: left > rightInteger, float: left > rightFloat, percentage: left > rightPercentage, quantity: left > rightQuantity, text: left > rightText, numericTag: left > rightNumericTag, plainTag: left > rightPlainTag, vector: left > rightVector, point: left > rightPoint, list: left > rightList, map: left > rightMap, dice: left > rightDice, range: left > rightRange, message: left > rightMessage, handler: left > rightHandler, seriesValue: left > rightSeries)
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
              value: false
```

## Test: greater left map

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greater left map.ges"
    program: main
```

```ges
module AtomicCompareAE
on Start {
  let left be [hp: 10, name: 'Ada']
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left > rightNothing, boolean: left > rightBoolean, integer: left > rightInteger, float: left > rightFloat, percentage: left > rightPercentage, quantity: left > rightQuantity, text: left > rightText, numericTag: left > rightNumericTag, plainTag: left > rightPlainTag, vector: left > rightVector, point: left > rightPoint, list: left > rightList, map: left > rightMap, dice: left > rightDice, range: left > rightRange, message: left > rightMessage, handler: left > rightHandler, seriesValue: left > rightSeries)
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
              value: false
```

## Test: greater left dice

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greater left dice.ges"
    program: main
```

```ges
module AtomicCompareAF
on Start {
  let left be roll dice 2d6
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left > rightNothing, boolean: left > rightBoolean, integer: left > rightInteger, float: left > rightFloat, percentage: left > rightPercentage, quantity: left > rightQuantity, text: left > rightText, numericTag: left > rightNumericTag, plainTag: left > rightPlainTag, vector: left > rightVector, point: left > rightPoint, list: left > rightList, map: left > rightMap, dice: left > rightDice, range: left > rightRange, message: left > rightMessage, handler: left > rightHandler, seriesValue: left > rightSeries)
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

## Test: greater left range

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greater left range.ges"
    program: main
```

```ges
module AtomicCompareAG
on Start {
  let left be from 1 to 3
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left > rightNothing, boolean: left > rightBoolean, integer: left > rightInteger, float: left > rightFloat, percentage: left > rightPercentage, quantity: left > rightQuantity, text: left > rightText, numericTag: left > rightNumericTag, plainTag: left > rightPlainTag, vector: left > rightVector, point: left > rightPoint, list: left > rightList, map: left > rightMap, dice: left > rightDice, range: left > rightRange, message: left > rightMessage, handler: left > rightHandler, seriesValue: left > rightSeries)
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
              value: false
```

## Test: greater left message

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greater left message.ges"
    program: main
```

```ges
module AtomicCompareAH
on Start {
  let left be Ping(amount: 10)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left > rightNothing, boolean: left > rightBoolean, integer: left > rightInteger, float: left > rightFloat, percentage: left > rightPercentage, quantity: left > rightQuantity, text: left > rightText, numericTag: left > rightNumericTag, plainTag: left > rightPlainTag, vector: left > rightVector, point: left > rightPoint, list: left > rightList, map: left > rightMap, dice: left > rightDice, range: left > rightRange, message: left > rightMessage, handler: left > rightHandler, seriesValue: left > rightSeries)
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
              value: false
```

## Test: greater left handler

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greater left handler.ges"
    program: main
```

```ges
module AtomicCompareAI
on Start {
  let left be Ping(amount)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left > rightNothing, boolean: left > rightBoolean, integer: left > rightInteger, float: left > rightFloat, percentage: left > rightPercentage, quantity: left > rightQuantity, text: left > rightText, numericTag: left > rightNumericTag, plainTag: left > rightPlainTag, vector: left > rightVector, point: left > rightPoint, list: left > rightList, map: left > rightMap, dice: left > rightDice, range: left > rightRange, message: left > rightMessage, handler: left > rightHandler, seriesValue: left > rightSeries)
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
              value: false
```

## Test: greater left series

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greater left series.ges"
    program: main
```

```ges
module AtomicCompareAJ
on Start {
  let left be series fibonacci
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left > rightNothing, boolean: left > rightBoolean, integer: left > rightInteger, float: left > rightFloat, percentage: left > rightPercentage, quantity: left > rightQuantity, text: left > rightText, numericTag: left > rightNumericTag, plainTag: left > rightPlainTag, vector: left > rightVector, point: left > rightPoint, list: left > rightList, map: left > rightMap, dice: left > rightDice, range: left > rightRange, message: left > rightMessage, handler: left > rightHandler, seriesValue: left > rightSeries)
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
              value: false
```

## Test: lessOrEqual left nothing

```yaml
gesBlock: case
id: case-0037
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "lessOrEqual left nothing.ges"
    program: main
```

```ges
module AtomicCompareAK
on Start {
  let left be nothing
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left <= rightNothing, boolean: left <= rightBoolean, integer: left <= rightInteger, float: left <= rightFloat, percentage: left <= rightPercentage, quantity: left <= rightQuantity, text: left <= rightText, numericTag: left <= rightNumericTag, plainTag: left <= rightPlainTag, vector: left <= rightVector, point: left <= rightPoint, list: left <= rightList, map: left <= rightMap, dice: left <= rightDice, range: left <= rightRange, message: left <= rightMessage, handler: left <= rightHandler, seriesValue: left <= rightSeries)
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

## Test: lessOrEqual left boolean

```yaml
gesBlock: case
id: case-0038
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "lessOrEqual left boolean.ges"
    program: main
```

```ges
module AtomicCompareAL
on Start {
  let left be true
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left <= rightNothing, boolean: left <= rightBoolean, integer: left <= rightInteger, float: left <= rightFloat, percentage: left <= rightPercentage, quantity: left <= rightQuantity, text: left <= rightText, numericTag: left <= rightNumericTag, plainTag: left <= rightPlainTag, vector: left <= rightVector, point: left <= rightPoint, list: left <= rightList, map: left <= rightMap, dice: left <= rightDice, range: left <= rightRange, message: left <= rightMessage, handler: left <= rightHandler, seriesValue: left <= rightSeries)
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

## Test: lessOrEqual left integer

```yaml
gesBlock: case
id: case-0039
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "lessOrEqual left integer.ges"
    program: main
```

```ges
module AtomicCompareAM
on Start {
  let left be 10
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left <= rightNothing, boolean: left <= rightBoolean, integer: left <= rightInteger, float: left <= rightFloat, percentage: left <= rightPercentage, quantity: left <= rightQuantity, text: left <= rightText, numericTag: left <= rightNumericTag, plainTag: left <= rightPlainTag, vector: left <= rightVector, point: left <= rightPoint, list: left <= rightList, map: left <= rightMap, dice: left <= rightDice, range: left <= rightRange, message: left <= rightMessage, handler: left <= rightHandler, seriesValue: left <= rightSeries)
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

## Test: lessOrEqual left float

```yaml
gesBlock: case
id: case-0040
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "lessOrEqual left float.ges"
    program: main
```

```ges
module AtomicCompareAN
on Start {
  let left be 0.6
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left <= rightNothing, boolean: left <= rightBoolean, integer: left <= rightInteger, float: left <= rightFloat, percentage: left <= rightPercentage, quantity: left <= rightQuantity, text: left <= rightText, numericTag: left <= rightNumericTag, plainTag: left <= rightPlainTag, vector: left <= rightVector, point: left <= rightPoint, list: left <= rightList, map: left <= rightMap, dice: left <= rightDice, range: left <= rightRange, message: left <= rightMessage, handler: left <= rightHandler, seriesValue: left <= rightSeries)
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

## Test: lessOrEqual left percentage

```yaml
gesBlock: case
id: case-0041
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "lessOrEqual left percentage.ges"
    program: main
```

```ges
module AtomicCompareAO
on Start {
  let left be 60%
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left <= rightNothing, boolean: left <= rightBoolean, integer: left <= rightInteger, float: left <= rightFloat, percentage: left <= rightPercentage, quantity: left <= rightQuantity, text: left <= rightText, numericTag: left <= rightNumericTag, plainTag: left <= rightPlainTag, vector: left <= rightVector, point: left <= rightPoint, list: left <= rightList, map: left <= rightMap, dice: left <= rightDice, range: left <= rightRange, message: left <= rightMessage, handler: left <= rightHandler, seriesValue: left <= rightSeries)
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

## Test: lessOrEqual left quantity

```yaml
gesBlock: case
id: case-0042
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "lessOrEqual left quantity.ges"
    program: main
```

```ges
module AtomicCompareAP
on Start {
  let left be 10m
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left <= rightNothing, boolean: left <= rightBoolean, integer: left <= rightInteger, float: left <= rightFloat, percentage: left <= rightPercentage, quantity: left <= rightQuantity, text: left <= rightText, numericTag: left <= rightNumericTag, plainTag: left <= rightPlainTag, vector: left <= rightVector, point: left <= rightPoint, list: left <= rightList, map: left <= rightMap, dice: left <= rightDice, range: left <= rightRange, message: left <= rightMessage, handler: left <= rightHandler, seriesValue: left <= rightSeries)
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

## Test: lessOrEqual left text

```yaml
gesBlock: case
id: case-0043
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "lessOrEqual left text.ges"
    program: main
```

```ges
module AtomicCompareAQ
on Start {
  let left be '10'
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left <= rightNothing, boolean: left <= rightBoolean, integer: left <= rightInteger, float: left <= rightFloat, percentage: left <= rightPercentage, quantity: left <= rightQuantity, text: left <= rightText, numericTag: left <= rightNumericTag, plainTag: left <= rightPlainTag, vector: left <= rightVector, point: left <= rightPoint, list: left <= rightList, map: left <= rightMap, dice: left <= rightDice, range: left <= rightRange, message: left <= rightMessage, handler: left <= rightHandler, seriesValue: left <= rightSeries)
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
              value: false
```

## Test: lessOrEqual left numericTag

```yaml
gesBlock: case
id: case-0044
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "lessOrEqual left numericTag.ges"
    program: main
```

```ges
module AtomicCompareAR
on Start {
  let left be pi
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left <= rightNothing, boolean: left <= rightBoolean, integer: left <= rightInteger, float: left <= rightFloat, percentage: left <= rightPercentage, quantity: left <= rightQuantity, text: left <= rightText, numericTag: left <= rightNumericTag, plainTag: left <= rightPlainTag, vector: left <= rightVector, point: left <= rightPoint, list: left <= rightList, map: left <= rightMap, dice: left <= rightDice, range: left <= rightRange, message: left <= rightMessage, handler: left <= rightHandler, seriesValue: left <= rightSeries)
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

## Test: lessOrEqual left plainTag

```yaml
gesBlock: case
id: case-0045
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "lessOrEqual left plainTag.ges"
    program: main
```

```ges
module AtomicCompareAS
on Start {
  let left be #custom
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left <= rightNothing, boolean: left <= rightBoolean, integer: left <= rightInteger, float: left <= rightFloat, percentage: left <= rightPercentage, quantity: left <= rightQuantity, text: left <= rightText, numericTag: left <= rightNumericTag, plainTag: left <= rightPlainTag, vector: left <= rightVector, point: left <= rightPoint, list: left <= rightList, map: left <= rightMap, dice: left <= rightDice, range: left <= rightRange, message: left <= rightMessage, handler: left <= rightHandler, seriesValue: left <= rightSeries)
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
              value: false
```

## Test: lessOrEqual left vector

```yaml
gesBlock: case
id: case-0046
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "lessOrEqual left vector.ges"
    program: main
```

```ges
module AtomicCompareAT
on Start {
  let left be :vector(1, 2, 3)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left <= rightNothing, boolean: left <= rightBoolean, integer: left <= rightInteger, float: left <= rightFloat, percentage: left <= rightPercentage, quantity: left <= rightQuantity, text: left <= rightText, numericTag: left <= rightNumericTag, plainTag: left <= rightPlainTag, vector: left <= rightVector, point: left <= rightPoint, list: left <= rightList, map: left <= rightMap, dice: left <= rightDice, range: left <= rightRange, message: left <= rightMessage, handler: left <= rightHandler, seriesValue: left <= rightSeries)
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
              value: false
```

## Test: lessOrEqual left point

```yaml
gesBlock: case
id: case-0047
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "lessOrEqual left point.ges"
    program: main
```

```ges
module AtomicCompareAU
on Start {
  let left be :point(1, 2, 3)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left <= rightNothing, boolean: left <= rightBoolean, integer: left <= rightInteger, float: left <= rightFloat, percentage: left <= rightPercentage, quantity: left <= rightQuantity, text: left <= rightText, numericTag: left <= rightNumericTag, plainTag: left <= rightPlainTag, vector: left <= rightVector, point: left <= rightPoint, list: left <= rightList, map: left <= rightMap, dice: left <= rightDice, range: left <= rightRange, message: left <= rightMessage, handler: left <= rightHandler, seriesValue: left <= rightSeries)
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
              value: false
```

## Test: lessOrEqual left list

```yaml
gesBlock: case
id: case-0048
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "lessOrEqual left list.ges"
    program: main
```

```ges
module AtomicCompareAV
on Start {
  let left be [1, 'x']
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left <= rightNothing, boolean: left <= rightBoolean, integer: left <= rightInteger, float: left <= rightFloat, percentage: left <= rightPercentage, quantity: left <= rightQuantity, text: left <= rightText, numericTag: left <= rightNumericTag, plainTag: left <= rightPlainTag, vector: left <= rightVector, point: left <= rightPoint, list: left <= rightList, map: left <= rightMap, dice: left <= rightDice, range: left <= rightRange, message: left <= rightMessage, handler: left <= rightHandler, seriesValue: left <= rightSeries)
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
              value: false
```

## Test: lessOrEqual left map

```yaml
gesBlock: case
id: case-0049
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "lessOrEqual left map.ges"
    program: main
```

```ges
module AtomicCompareAW
on Start {
  let left be [hp: 10, name: 'Ada']
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left <= rightNothing, boolean: left <= rightBoolean, integer: left <= rightInteger, float: left <= rightFloat, percentage: left <= rightPercentage, quantity: left <= rightQuantity, text: left <= rightText, numericTag: left <= rightNumericTag, plainTag: left <= rightPlainTag, vector: left <= rightVector, point: left <= rightPoint, list: left <= rightList, map: left <= rightMap, dice: left <= rightDice, range: left <= rightRange, message: left <= rightMessage, handler: left <= rightHandler, seriesValue: left <= rightSeries)
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
              value: false
```

## Test: lessOrEqual left dice

```yaml
gesBlock: case
id: case-0050
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "lessOrEqual left dice.ges"
    program: main
```

```ges
module AtomicCompareAX
on Start {
  let left be roll dice 2d6
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left <= rightNothing, boolean: left <= rightBoolean, integer: left <= rightInteger, float: left <= rightFloat, percentage: left <= rightPercentage, quantity: left <= rightQuantity, text: left <= rightText, numericTag: left <= rightNumericTag, plainTag: left <= rightPlainTag, vector: left <= rightVector, point: left <= rightPoint, list: left <= rightList, map: left <= rightMap, dice: left <= rightDice, range: left <= rightRange, message: left <= rightMessage, handler: left <= rightHandler, seriesValue: left <= rightSeries)
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

## Test: lessOrEqual left range

```yaml
gesBlock: case
id: case-0051
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "lessOrEqual left range.ges"
    program: main
```

```ges
module AtomicCompareAY
on Start {
  let left be from 1 to 3
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left <= rightNothing, boolean: left <= rightBoolean, integer: left <= rightInteger, float: left <= rightFloat, percentage: left <= rightPercentage, quantity: left <= rightQuantity, text: left <= rightText, numericTag: left <= rightNumericTag, plainTag: left <= rightPlainTag, vector: left <= rightVector, point: left <= rightPoint, list: left <= rightList, map: left <= rightMap, dice: left <= rightDice, range: left <= rightRange, message: left <= rightMessage, handler: left <= rightHandler, seriesValue: left <= rightSeries)
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
              value: false
```

## Test: lessOrEqual left message

```yaml
gesBlock: case
id: case-0052
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "lessOrEqual left message.ges"
    program: main
```

```ges
module AtomicCompareAZ
on Start {
  let left be Ping(amount: 10)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left <= rightNothing, boolean: left <= rightBoolean, integer: left <= rightInteger, float: left <= rightFloat, percentage: left <= rightPercentage, quantity: left <= rightQuantity, text: left <= rightText, numericTag: left <= rightNumericTag, plainTag: left <= rightPlainTag, vector: left <= rightVector, point: left <= rightPoint, list: left <= rightList, map: left <= rightMap, dice: left <= rightDice, range: left <= rightRange, message: left <= rightMessage, handler: left <= rightHandler, seriesValue: left <= rightSeries)
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
              value: false
```

## Test: lessOrEqual left handler

```yaml
gesBlock: case
id: case-0053
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "lessOrEqual left handler.ges"
    program: main
```

```ges
module AtomicCompareBA
on Start {
  let left be Ping(amount)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left <= rightNothing, boolean: left <= rightBoolean, integer: left <= rightInteger, float: left <= rightFloat, percentage: left <= rightPercentage, quantity: left <= rightQuantity, text: left <= rightText, numericTag: left <= rightNumericTag, plainTag: left <= rightPlainTag, vector: left <= rightVector, point: left <= rightPoint, list: left <= rightList, map: left <= rightMap, dice: left <= rightDice, range: left <= rightRange, message: left <= rightMessage, handler: left <= rightHandler, seriesValue: left <= rightSeries)
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
              value: false
```

## Test: lessOrEqual left series

```yaml
gesBlock: case
id: case-0054
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "lessOrEqual left series.ges"
    program: main
```

```ges
module AtomicCompareBB
on Start {
  let left be series fibonacci
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left <= rightNothing, boolean: left <= rightBoolean, integer: left <= rightInteger, float: left <= rightFloat, percentage: left <= rightPercentage, quantity: left <= rightQuantity, text: left <= rightText, numericTag: left <= rightNumericTag, plainTag: left <= rightPlainTag, vector: left <= rightVector, point: left <= rightPoint, list: left <= rightList, map: left <= rightMap, dice: left <= rightDice, range: left <= rightRange, message: left <= rightMessage, handler: left <= rightHandler, seriesValue: left <= rightSeries)
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
              value: false
```

## Test: greaterOrEqual left nothing

```yaml
gesBlock: case
id: case-0055
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greaterOrEqual left nothing.ges"
    program: main
```

```ges
module AtomicCompareBC
on Start {
  let left be nothing
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left >= rightNothing, boolean: left >= rightBoolean, integer: left >= rightInteger, float: left >= rightFloat, percentage: left >= rightPercentage, quantity: left >= rightQuantity, text: left >= rightText, numericTag: left >= rightNumericTag, plainTag: left >= rightPlainTag, vector: left >= rightVector, point: left >= rightPoint, list: left >= rightList, map: left >= rightMap, dice: left >= rightDice, range: left >= rightRange, message: left >= rightMessage, handler: left >= rightHandler, seriesValue: left >= rightSeries)
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

## Test: greaterOrEqual left boolean

```yaml
gesBlock: case
id: case-0056
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greaterOrEqual left boolean.ges"
    program: main
```

```ges
module AtomicCompareBD
on Start {
  let left be true
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left >= rightNothing, boolean: left >= rightBoolean, integer: left >= rightInteger, float: left >= rightFloat, percentage: left >= rightPercentage, quantity: left >= rightQuantity, text: left >= rightText, numericTag: left >= rightNumericTag, plainTag: left >= rightPlainTag, vector: left >= rightVector, point: left >= rightPoint, list: left >= rightList, map: left >= rightMap, dice: left >= rightDice, range: left >= rightRange, message: left >= rightMessage, handler: left >= rightHandler, seriesValue: left >= rightSeries)
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

## Test: greaterOrEqual left integer

```yaml
gesBlock: case
id: case-0057
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greaterOrEqual left integer.ges"
    program: main
```

```ges
module AtomicCompareBE
on Start {
  let left be 10
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left >= rightNothing, boolean: left >= rightBoolean, integer: left >= rightInteger, float: left >= rightFloat, percentage: left >= rightPercentage, quantity: left >= rightQuantity, text: left >= rightText, numericTag: left >= rightNumericTag, plainTag: left >= rightPlainTag, vector: left >= rightVector, point: left >= rightPoint, list: left >= rightList, map: left >= rightMap, dice: left >= rightDice, range: left >= rightRange, message: left >= rightMessage, handler: left >= rightHandler, seriesValue: left >= rightSeries)
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

## Test: greaterOrEqual left float

```yaml
gesBlock: case
id: case-0058
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greaterOrEqual left float.ges"
    program: main
```

```ges
module AtomicCompareBF
on Start {
  let left be 0.6
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left >= rightNothing, boolean: left >= rightBoolean, integer: left >= rightInteger, float: left >= rightFloat, percentage: left >= rightPercentage, quantity: left >= rightQuantity, text: left >= rightText, numericTag: left >= rightNumericTag, plainTag: left >= rightPlainTag, vector: left >= rightVector, point: left >= rightPoint, list: left >= rightList, map: left >= rightMap, dice: left >= rightDice, range: left >= rightRange, message: left >= rightMessage, handler: left >= rightHandler, seriesValue: left >= rightSeries)
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

## Test: greaterOrEqual left percentage

```yaml
gesBlock: case
id: case-0059
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greaterOrEqual left percentage.ges"
    program: main
```

```ges
module AtomicCompareBG
on Start {
  let left be 60%
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left >= rightNothing, boolean: left >= rightBoolean, integer: left >= rightInteger, float: left >= rightFloat, percentage: left >= rightPercentage, quantity: left >= rightQuantity, text: left >= rightText, numericTag: left >= rightNumericTag, plainTag: left >= rightPlainTag, vector: left >= rightVector, point: left >= rightPoint, list: left >= rightList, map: left >= rightMap, dice: left >= rightDice, range: left >= rightRange, message: left >= rightMessage, handler: left >= rightHandler, seriesValue: left >= rightSeries)
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

## Test: greaterOrEqual left quantity

```yaml
gesBlock: case
id: case-0060
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greaterOrEqual left quantity.ges"
    program: main
```

```ges
module AtomicCompareBH
on Start {
  let left be 10m
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left >= rightNothing, boolean: left >= rightBoolean, integer: left >= rightInteger, float: left >= rightFloat, percentage: left >= rightPercentage, quantity: left >= rightQuantity, text: left >= rightText, numericTag: left >= rightNumericTag, plainTag: left >= rightPlainTag, vector: left >= rightVector, point: left >= rightPoint, list: left >= rightList, map: left >= rightMap, dice: left >= rightDice, range: left >= rightRange, message: left >= rightMessage, handler: left >= rightHandler, seriesValue: left >= rightSeries)
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

## Test: greaterOrEqual left text

```yaml
gesBlock: case
id: case-0061
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greaterOrEqual left text.ges"
    program: main
```

```ges
module AtomicCompareBI
on Start {
  let left be '10'
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left >= rightNothing, boolean: left >= rightBoolean, integer: left >= rightInteger, float: left >= rightFloat, percentage: left >= rightPercentage, quantity: left >= rightQuantity, text: left >= rightText, numericTag: left >= rightNumericTag, plainTag: left >= rightPlainTag, vector: left >= rightVector, point: left >= rightPoint, list: left >= rightList, map: left >= rightMap, dice: left >= rightDice, range: left >= rightRange, message: left >= rightMessage, handler: left >= rightHandler, seriesValue: left >= rightSeries)
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
              value: false
```

## Test: greaterOrEqual left numericTag

```yaml
gesBlock: case
id: case-0062
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greaterOrEqual left numericTag.ges"
    program: main
```

```ges
module AtomicCompareBJ
on Start {
  let left be pi
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left >= rightNothing, boolean: left >= rightBoolean, integer: left >= rightInteger, float: left >= rightFloat, percentage: left >= rightPercentage, quantity: left >= rightQuantity, text: left >= rightText, numericTag: left >= rightNumericTag, plainTag: left >= rightPlainTag, vector: left >= rightVector, point: left >= rightPoint, list: left >= rightList, map: left >= rightMap, dice: left >= rightDice, range: left >= rightRange, message: left >= rightMessage, handler: left >= rightHandler, seriesValue: left >= rightSeries)
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

## Test: greaterOrEqual left plainTag

```yaml
gesBlock: case
id: case-0063
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greaterOrEqual left plainTag.ges"
    program: main
```

```ges
module AtomicCompareBK
on Start {
  let left be #custom
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left >= rightNothing, boolean: left >= rightBoolean, integer: left >= rightInteger, float: left >= rightFloat, percentage: left >= rightPercentage, quantity: left >= rightQuantity, text: left >= rightText, numericTag: left >= rightNumericTag, plainTag: left >= rightPlainTag, vector: left >= rightVector, point: left >= rightPoint, list: left >= rightList, map: left >= rightMap, dice: left >= rightDice, range: left >= rightRange, message: left >= rightMessage, handler: left >= rightHandler, seriesValue: left >= rightSeries)
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
              value: false
```

## Test: greaterOrEqual left vector

```yaml
gesBlock: case
id: case-0064
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greaterOrEqual left vector.ges"
    program: main
```

```ges
module AtomicCompareBL
on Start {
  let left be :vector(1, 2, 3)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left >= rightNothing, boolean: left >= rightBoolean, integer: left >= rightInteger, float: left >= rightFloat, percentage: left >= rightPercentage, quantity: left >= rightQuantity, text: left >= rightText, numericTag: left >= rightNumericTag, plainTag: left >= rightPlainTag, vector: left >= rightVector, point: left >= rightPoint, list: left >= rightList, map: left >= rightMap, dice: left >= rightDice, range: left >= rightRange, message: left >= rightMessage, handler: left >= rightHandler, seriesValue: left >= rightSeries)
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
              value: false
```

## Test: greaterOrEqual left point

```yaml
gesBlock: case
id: case-0065
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greaterOrEqual left point.ges"
    program: main
```

```ges
module AtomicCompareBM
on Start {
  let left be :point(1, 2, 3)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left >= rightNothing, boolean: left >= rightBoolean, integer: left >= rightInteger, float: left >= rightFloat, percentage: left >= rightPercentage, quantity: left >= rightQuantity, text: left >= rightText, numericTag: left >= rightNumericTag, plainTag: left >= rightPlainTag, vector: left >= rightVector, point: left >= rightPoint, list: left >= rightList, map: left >= rightMap, dice: left >= rightDice, range: left >= rightRange, message: left >= rightMessage, handler: left >= rightHandler, seriesValue: left >= rightSeries)
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
              value: false
```

## Test: greaterOrEqual left list

```yaml
gesBlock: case
id: case-0066
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greaterOrEqual left list.ges"
    program: main
```

```ges
module AtomicCompareBN
on Start {
  let left be [1, 'x']
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left >= rightNothing, boolean: left >= rightBoolean, integer: left >= rightInteger, float: left >= rightFloat, percentage: left >= rightPercentage, quantity: left >= rightQuantity, text: left >= rightText, numericTag: left >= rightNumericTag, plainTag: left >= rightPlainTag, vector: left >= rightVector, point: left >= rightPoint, list: left >= rightList, map: left >= rightMap, dice: left >= rightDice, range: left >= rightRange, message: left >= rightMessage, handler: left >= rightHandler, seriesValue: left >= rightSeries)
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
              value: false
```

## Test: greaterOrEqual left map

```yaml
gesBlock: case
id: case-0067
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greaterOrEqual left map.ges"
    program: main
```

```ges
module AtomicCompareBO
on Start {
  let left be [hp: 10, name: 'Ada']
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left >= rightNothing, boolean: left >= rightBoolean, integer: left >= rightInteger, float: left >= rightFloat, percentage: left >= rightPercentage, quantity: left >= rightQuantity, text: left >= rightText, numericTag: left >= rightNumericTag, plainTag: left >= rightPlainTag, vector: left >= rightVector, point: left >= rightPoint, list: left >= rightList, map: left >= rightMap, dice: left >= rightDice, range: left >= rightRange, message: left >= rightMessage, handler: left >= rightHandler, seriesValue: left >= rightSeries)
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
              value: false
```

## Test: greaterOrEqual left dice

```yaml
gesBlock: case
id: case-0068
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greaterOrEqual left dice.ges"
    program: main
```

```ges
module AtomicCompareBP
on Start {
  let left be roll dice 2d6
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left >= rightNothing, boolean: left >= rightBoolean, integer: left >= rightInteger, float: left >= rightFloat, percentage: left >= rightPercentage, quantity: left >= rightQuantity, text: left >= rightText, numericTag: left >= rightNumericTag, plainTag: left >= rightPlainTag, vector: left >= rightVector, point: left >= rightPoint, list: left >= rightList, map: left >= rightMap, dice: left >= rightDice, range: left >= rightRange, message: left >= rightMessage, handler: left >= rightHandler, seriesValue: left >= rightSeries)
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

## Test: greaterOrEqual left range

```yaml
gesBlock: case
id: case-0069
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greaterOrEqual left range.ges"
    program: main
```

```ges
module AtomicCompareBQ
on Start {
  let left be from 1 to 3
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left >= rightNothing, boolean: left >= rightBoolean, integer: left >= rightInteger, float: left >= rightFloat, percentage: left >= rightPercentage, quantity: left >= rightQuantity, text: left >= rightText, numericTag: left >= rightNumericTag, plainTag: left >= rightPlainTag, vector: left >= rightVector, point: left >= rightPoint, list: left >= rightList, map: left >= rightMap, dice: left >= rightDice, range: left >= rightRange, message: left >= rightMessage, handler: left >= rightHandler, seriesValue: left >= rightSeries)
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
              value: false
```

## Test: greaterOrEqual left message

```yaml
gesBlock: case
id: case-0070
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greaterOrEqual left message.ges"
    program: main
```

```ges
module AtomicCompareBR
on Start {
  let left be Ping(amount: 10)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left >= rightNothing, boolean: left >= rightBoolean, integer: left >= rightInteger, float: left >= rightFloat, percentage: left >= rightPercentage, quantity: left >= rightQuantity, text: left >= rightText, numericTag: left >= rightNumericTag, plainTag: left >= rightPlainTag, vector: left >= rightVector, point: left >= rightPoint, list: left >= rightList, map: left >= rightMap, dice: left >= rightDice, range: left >= rightRange, message: left >= rightMessage, handler: left >= rightHandler, seriesValue: left >= rightSeries)
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
              value: false
```

## Test: greaterOrEqual left handler

```yaml
gesBlock: case
id: case-0071
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greaterOrEqual left handler.ges"
    program: main
```

```ges
module AtomicCompareBS
on Start {
  let left be Ping(amount)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left >= rightNothing, boolean: left >= rightBoolean, integer: left >= rightInteger, float: left >= rightFloat, percentage: left >= rightPercentage, quantity: left >= rightQuantity, text: left >= rightText, numericTag: left >= rightNumericTag, plainTag: left >= rightPlainTag, vector: left >= rightVector, point: left >= rightPoint, list: left >= rightList, map: left >= rightMap, dice: left >= rightDice, range: left >= rightRange, message: left >= rightMessage, handler: left >= rightHandler, seriesValue: left >= rightSeries)
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
              value: false
```

## Test: greaterOrEqual left series

```yaml
gesBlock: case
id: case-0072
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greaterOrEqual left series.ges"
    program: main
```

```ges
module AtomicCompareBT
on Start {
  let left be series fibonacci
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
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
  emit Done(nothing: left >= rightNothing, boolean: left >= rightBoolean, integer: left >= rightInteger, float: left >= rightFloat, percentage: left >= rightPercentage, quantity: left >= rightQuantity, text: left >= rightText, numericTag: left >= rightNumericTag, plainTag: left >= rightPlainTag, vector: left >= rightVector, point: left >= rightPoint, list: left >= rightList, map: left >= rightMap, dice: left >= rightDice, range: left >= rightRange, message: left >= rightMessage, handler: left >= rightHandler, seriesValue: left >= rightSeries)
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
              value: false
```
