---
formatVersion: 1
suiteId: "runtime.atomic.compare.greater-than"
title: "Comparison Matrix — Greater Than"
categories: [conformance]
---

# Comparison Matrix — Greater Than

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers strict greater-than comparison across every supported value family.

---

## Test: greater left nothing

This runtime case exercises “greater left nothing” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "greater left nothing.ges"
    program: main
```

### Source code under test

```ges
module atomiccompares
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Nothing"
          - name: "integer"
            value:
              type: ":Nothing"
          - name: "float"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "quantity"
            value:
              type: ":Nothing"
          - name: "text"
            value:
              type: ":Nothing"
          - name: "numericTag"
            value:
              type: ":Nothing"
          - name: "plainTag"
            value:
              type: ":Nothing"
          - name: "vector"
            value:
              type: ":Nothing"
          - name: "point"
            value:
              type: ":Nothing"
          - name: "list"
            value:
              type: ":Nothing"
          - name: "map"
            value:
              type: ":Nothing"
          - name: "dice"
            value:
              type: ":Nothing"
          - name: "range"
            value:
              type: ":Nothing"
          - name: "message"
            value:
              type: ":Nothing"
          - name: "handler"
            value:
              type: ":Nothing"
          - name: "seriesValue"
            value:
              type: ":Nothing"
```

---

## Test: greater left boolean

This runtime case exercises “greater left boolean” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccomparet
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: false
          - name: "integer"
            value:
              type: ":Boolean"
              value: false
          - name: "float"
            value:
              type: ":Boolean"
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "quantity"
            value:
              type: ":Boolean"
              value: false
          - name: "text"
            value:
              type: ":Boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: false
          - name: "message"
            value:
              type: ":Boolean"
              value: false
          - name: "handler"
            value:
              type: ":Boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: greater left integer

This runtime case exercises “greater left integer” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccompareu
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: true
          - name: "integer"
            value:
              type: ":Boolean"
              value: false
          - name: "float"
            value:
              type: ":Boolean"
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "quantity"
            value:
              type: ":Boolean"
              value: false
          - name: "text"
            value:
              type: ":Boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: false
          - name: "message"
            value:
              type: ":Boolean"
              value: false
          - name: "handler"
            value:
              type: ":Boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: greater left float

This runtime case exercises “greater left float” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccomparev
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: false
          - name: "integer"
            value:
              type: ":Boolean"
              value: false
          - name: "float"
            value:
              type: ":Boolean"
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "quantity"
            value:
              type: ":Boolean"
              value: false
          - name: "text"
            value:
              type: ":Boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: false
          - name: "message"
            value:
              type: ":Boolean"
              value: false
          - name: "handler"
            value:
              type: ":Boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: greater left percentage

This runtime case exercises “greater left percentage” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccomparew
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: false
          - name: "integer"
            value:
              type: ":Boolean"
              value: false
          - name: "float"
            value:
              type: ":Boolean"
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "quantity"
            value:
              type: ":Boolean"
              value: false
          - name: "text"
            value:
              type: ":Boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: false
          - name: "message"
            value:
              type: ":Boolean"
              value: false
          - name: "handler"
            value:
              type: ":Boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: greater left quantity

This runtime case exercises “greater left quantity” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccomparex
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: false
          - name: "integer"
            value:
              type: ":Boolean"
              value: false
          - name: "float"
            value:
              type: ":Boolean"
              value: false
          - name: "percentage"
            value:
              type: ":Boolean"
              value: false
          - name: "quantity"
            value:
              type: ":Boolean"
              value: false
          - name: "text"
            value:
              type: ":Boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: false
          - name: "message"
            value:
              type: ":Boolean"
              value: false
          - name: "handler"
            value:
              type: ":Boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: greater left text

This runtime case exercises “greater left text” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccomparey
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: false
          - name: "integer"
            value:
              type: ":Boolean"
              value: false
          - name: "float"
            value:
              type: ":Boolean"
              value: false
          - name: "percentage"
            value:
              type: ":Boolean"
              value: false
          - name: "quantity"
            value:
              type: ":Boolean"
              value: false
          - name: "text"
            value:
              type: ":Boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: false
          - name: "message"
            value:
              type: ":Boolean"
              value: false
          - name: "handler"
            value:
              type: ":Boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: greater left numericTag

This runtime case exercises “greater left numericTag” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccomparez
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: true
          - name: "integer"
            value:
              type: ":Boolean"
              value: false
          - name: "float"
            value:
              type: ":Boolean"
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "quantity"
            value:
              type: ":Boolean"
              value: false
          - name: "text"
            value:
              type: ":Boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: false
          - name: "message"
            value:
              type: ":Boolean"
              value: false
          - name: "handler"
            value:
              type: ":Boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: greater left plainTag

This runtime case exercises “greater left plainTag” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccompareaa
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: false
          - name: "integer"
            value:
              type: ":Boolean"
              value: false
          - name: "float"
            value:
              type: ":Boolean"
              value: false
          - name: "percentage"
            value:
              type: ":Boolean"
              value: false
          - name: "quantity"
            value:
              type: ":Boolean"
              value: false
          - name: "text"
            value:
              type: ":Boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: false
          - name: "message"
            value:
              type: ":Boolean"
              value: false
          - name: "handler"
            value:
              type: ":Boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: greater left vector

This runtime case exercises “greater left vector” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccompareab
on Start {
  let left be :Vector(1, 2, 3)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: false
          - name: "integer"
            value:
              type: ":Boolean"
              value: false
          - name: "float"
            value:
              type: ":Boolean"
              value: false
          - name: "percentage"
            value:
              type: ":Boolean"
              value: false
          - name: "quantity"
            value:
              type: ":Boolean"
              value: false
          - name: "text"
            value:
              type: ":Boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: false
          - name: "message"
            value:
              type: ":Boolean"
              value: false
          - name: "handler"
            value:
              type: ":Boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: greater left point

This runtime case exercises “greater left point” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccompareac
on Start {
  let left be :Point(1, 2, 3)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 0.5
  let rightPercentage be 50%
  let rightQuantity be 10m
  let rightText be '10'
  let rightNumericTag be pi
  let rightPlainTag be #custom
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: false
          - name: "integer"
            value:
              type: ":Boolean"
              value: false
          - name: "float"
            value:
              type: ":Boolean"
              value: false
          - name: "percentage"
            value:
              type: ":Boolean"
              value: false
          - name: "quantity"
            value:
              type: ":Boolean"
              value: false
          - name: "text"
            value:
              type: ":Boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: false
          - name: "message"
            value:
              type: ":Boolean"
              value: false
          - name: "handler"
            value:
              type: ":Boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: greater left list

This runtime case exercises “greater left list” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccomparead
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: false
          - name: "integer"
            value:
              type: ":Boolean"
              value: false
          - name: "float"
            value:
              type: ":Boolean"
              value: false
          - name: "percentage"
            value:
              type: ":Boolean"
              value: false
          - name: "quantity"
            value:
              type: ":Boolean"
              value: false
          - name: "text"
            value:
              type: ":Boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: false
          - name: "message"
            value:
              type: ":Boolean"
              value: false
          - name: "handler"
            value:
              type: ":Boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: greater left map

This runtime case exercises “greater left map” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccompareae
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: false
          - name: "integer"
            value:
              type: ":Boolean"
              value: false
          - name: "float"
            value:
              type: ":Boolean"
              value: false
          - name: "percentage"
            value:
              type: ":Boolean"
              value: false
          - name: "quantity"
            value:
              type: ":Boolean"
              value: false
          - name: "text"
            value:
              type: ":Boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: false
          - name: "message"
            value:
              type: ":Boolean"
              value: false
          - name: "handler"
            value:
              type: ":Boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: greater left dice

This runtime case exercises “greater left dice” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccompareaf
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: true
          - name: "integer"
            value:
              type: ":Boolean"
              value: false
          - name: "float"
            value:
              type: ":Boolean"
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "quantity"
            value:
              type: ":Boolean"
              value: false
          - name: "text"
            value:
              type: ":Boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: false
          - name: "message"
            value:
              type: ":Boolean"
              value: false
          - name: "handler"
            value:
              type: ":Boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: greater left range

This runtime case exercises “greater left range” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccompareag
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: false
          - name: "integer"
            value:
              type: ":Boolean"
              value: false
          - name: "float"
            value:
              type: ":Boolean"
              value: false
          - name: "percentage"
            value:
              type: ":Boolean"
              value: false
          - name: "quantity"
            value:
              type: ":Boolean"
              value: false
          - name: "text"
            value:
              type: ":Boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: false
          - name: "message"
            value:
              type: ":Boolean"
              value: false
          - name: "handler"
            value:
              type: ":Boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: greater left message

This runtime case exercises “greater left message” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccompareah
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: false
          - name: "integer"
            value:
              type: ":Boolean"
              value: false
          - name: "float"
            value:
              type: ":Boolean"
              value: false
          - name: "percentage"
            value:
              type: ":Boolean"
              value: false
          - name: "quantity"
            value:
              type: ":Boolean"
              value: false
          - name: "text"
            value:
              type: ":Boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: false
          - name: "message"
            value:
              type: ":Boolean"
              value: false
          - name: "handler"
            value:
              type: ":Boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: greater left handler

This runtime case exercises “greater left handler” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccompareai
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: false
          - name: "integer"
            value:
              type: ":Boolean"
              value: false
          - name: "float"
            value:
              type: ":Boolean"
              value: false
          - name: "percentage"
            value:
              type: ":Boolean"
              value: false
          - name: "quantity"
            value:
              type: ":Boolean"
              value: false
          - name: "text"
            value:
              type: ":Boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: false
          - name: "message"
            value:
              type: ":Boolean"
              value: false
          - name: "handler"
            value:
              type: ":Boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: greater left series

This runtime case exercises “greater left series” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccompareaj
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: false
          - name: "integer"
            value:
              type: ":Boolean"
              value: false
          - name: "float"
            value:
              type: ":Boolean"
              value: false
          - name: "percentage"
            value:
              type: ":Boolean"
              value: false
          - name: "quantity"
            value:
              type: ":Boolean"
              value: false
          - name: "text"
            value:
              type: ":Boolean"
              value: false
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: false
          - name: "message"
            value:
              type: ":Boolean"
              value: false
          - name: "handler"
            value:
              type: ":Boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: false
```
