---
formatVersion: 1
suiteId: "runtime.atomic.compare.less-than-or-equal"
title: "Comparison Matrix — Less Than or Equal"
categories: [conformance]
---

# Comparison Matrix — Less Than or Equal

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers inclusive less-than comparison across every supported value family.

---

## Test: lessOrEqual left nothing

This runtime case exercises “lessOrEqual left nothing” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

## Test: lessOrEqual left boolean

This runtime case exercises “lessOrEqual left boolean” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: lessOrEqual left integer

This runtime case exercises “lessOrEqual left integer” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: lessOrEqual left float

This runtime case exercises “lessOrEqual left float” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: lessOrEqual left percentage

This runtime case exercises “lessOrEqual left percentage” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: lessOrEqual left quantity

This runtime case exercises “lessOrEqual left quantity” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

## Test: lessOrEqual left text

This runtime case exercises “lessOrEqual left text” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

## Test: lessOrEqual left numericTag

This runtime case exercises “lessOrEqual left numericTag” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: lessOrEqual left plainTag

This runtime case exercises “lessOrEqual left plainTag” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

## Test: lessOrEqual left vector

This runtime case exercises “lessOrEqual left vector” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

## Test: lessOrEqual left point

This runtime case exercises “lessOrEqual left point” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

## Test: lessOrEqual left list

This runtime case exercises “lessOrEqual left list” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

## Test: lessOrEqual left map

This runtime case exercises “lessOrEqual left map” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

## Test: lessOrEqual left dice

This runtime case exercises “lessOrEqual left dice” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

---

## Test: lessOrEqual left range

This runtime case exercises “lessOrEqual left range” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

## Test: lessOrEqual left message

This runtime case exercises “lessOrEqual left message” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

## Test: lessOrEqual left handler

This runtime case exercises “lessOrEqual left handler” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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

## Test: lessOrEqual left series

This runtime case exercises “lessOrEqual left series” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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
