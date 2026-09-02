---
formatVersion: 1
suiteId: "runtime.atomic.compare.greater-than-or-equal"
title: "Comparison Matrix — Greater Than or Equal"
categories: [conformance]
---

# Comparison Matrix — Greater Than or Equal

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers inclusive greater-than comparison across every supported value family.

---

## Test: greaterOrEqual left nothing

This runtime case exercises “greaterOrEqual left nothing” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: greaterOrEqual left boolean

This runtime case exercises “greaterOrEqual left boolean” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: greaterOrEqual left integer

This runtime case exercises “greaterOrEqual left integer” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: greaterOrEqual left float

This runtime case exercises “greaterOrEqual left float” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: greaterOrEqual left percentage

This runtime case exercises “greaterOrEqual left percentage” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: greaterOrEqual left quantity

This runtime case exercises “greaterOrEqual left quantity” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: greaterOrEqual left text

This runtime case exercises “greaterOrEqual left text” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: greaterOrEqual left numericTag

This runtime case exercises “greaterOrEqual left numericTag” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: greaterOrEqual left plainTag

This runtime case exercises “greaterOrEqual left plainTag” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: greaterOrEqual left vector

This runtime case exercises “greaterOrEqual left vector” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: greaterOrEqual left point

This runtime case exercises “greaterOrEqual left point” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: greaterOrEqual left list

This runtime case exercises “greaterOrEqual left list” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: greaterOrEqual left map

This runtime case exercises “greaterOrEqual left map” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: greaterOrEqual left dice

This runtime case exercises “greaterOrEqual left dice” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: greaterOrEqual left range

This runtime case exercises “greaterOrEqual left range” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: greaterOrEqual left message

This runtime case exercises “greaterOrEqual left message” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: greaterOrEqual left handler

This runtime case exercises “greaterOrEqual left handler” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: greaterOrEqual left series

This runtime case exercises “greaterOrEqual left series” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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
