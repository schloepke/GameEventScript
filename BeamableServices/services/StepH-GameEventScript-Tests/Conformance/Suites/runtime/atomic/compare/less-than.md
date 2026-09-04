---
formatVersion: 1
suiteId: "runtime.atomic.compare.less-than"
title: "Comparison Matrix — Less Than"
categories: [conformance]
---

# Comparison Matrix — Less Than

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers strict less-than comparison across every supported value family.

---

## Test: less left nothing

This runtime case exercises “less left nothing” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left nothing.ges"
    program: main
```

### Source code under test

```ges
module atomiccomparea
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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

## Test: less left boolean

This runtime case exercises “less left boolean” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left boolean.ges"
    program: main
```

### Source code under test

```ges
module atomiccompareb
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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
              value: true
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
              value: true
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

## Test: less left integer

This runtime case exercises “less left integer” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left integer.ges"
    program: main
```

### Source code under test

```ges
module atomiccomparec
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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

## Test: less left float

This runtime case exercises “less left float” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left float.ges"
    program: main
```

### Source code under test

```ges
module atomiccompared
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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
              value: true
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
              value: true
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

## Test: less left percentage

This runtime case exercises “less left percentage” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left percentage.ges"
    program: main
```

### Source code under test

```ges
module atomiccomparee
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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
              value: true
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
              value: true
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

## Test: less left quantity

This runtime case exercises “less left quantity” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left quantity.ges"
    program: main
```

### Source code under test

```ges
module atomiccomparef
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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

## Test: less left text

This runtime case exercises “less left text” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left text.ges"
    program: main
```

### Source code under test

```ges
module atomiccompareg
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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

## Test: less left numericTag

This runtime case exercises “less left numericTag” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left numericTag.ges"
    program: main
```

### Source code under test

```ges
module atomiccompareh
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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
              value: true
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
              value: true
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

## Test: less left plainTag

This runtime case exercises “less left plainTag” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left plainTag.ges"
    program: main
```

### Source code under test

```ges
module atomiccomparei
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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

## Test: less left vector

This runtime case exercises “less left vector” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left vector.ges"
    program: main
```

### Source code under test

```ges
module atomiccomparej
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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

## Test: less left point

This runtime case exercises “less left point” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left point.ges"
    program: main
```

### Source code under test

```ges
module atomiccomparek
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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

## Test: less left list

This runtime case exercises “less left list” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left list.ges"
    program: main
```

### Source code under test

```ges
module atomiccomparel
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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

## Test: less left map

This runtime case exercises “less left map” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left map.ges"
    program: main
```

### Source code under test

```ges
module atomiccomparem
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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

## Test: less left dice

This runtime case exercises “less left dice” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left dice.ges"
    program: main
```

### Source code under test

```ges
module atomiccomparen
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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

## Test: less left range

This runtime case exercises “less left range” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "6", "4"]
sources:
  - name: "less left range.ges"
    program: main
```

### Source code under test

```ges
module atomiccompareo
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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

## Test: less left message

This runtime case exercises “less left message” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccomparep
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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

## Test: less left handler

This runtime case exercises “less left handler” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccompareq
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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

## Test: less left series

This runtime case exercises “less left series” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomiccomparer
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
  emit Done(nothing: left < rightNothing, boolean: left < rightBoolean, integer: left < rightInteger, float: left < rightFloat, percentage: left < rightPercentage, quantity: left < rightQuantity, text: left < rightText, numericTag: left < rightNumericTag, plainTag: left < rightPlainTag, vector: left < rightVector, point: left < rightPoint, list: left < rightList, map: left < rightMap, dice: left < rightDice, range: left < rightRange, message: left < rightMessage, handler: left < rightHandler, seriesValue: left < rightSeries)
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
