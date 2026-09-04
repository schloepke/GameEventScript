---
formatVersion: 1
suiteId: "runtime.atomic.equality.not-equal"
title: "Equality Matrix — Not Equal"
categories: [conformance]
---

# Equality Matrix — Not Equal

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers inequality across every supported value family.

---

## Test: not equal left nothing

This runtime case exercises “not equal left nothing” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left nothing.ges"
    program: main
```

### Source code under test

```ges
module atomicequalitynotequalleftnothing
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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

## Test: not equal left boolean

This runtime case exercises “not equal left boolean” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left boolean.ges"
    program: main
```

### Source code under test

```ges
module atomicequalitynotequalleftboolean
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "quantity"
            value:
              type: ":Boolean"
              value: true
          - name: "text"
            value:
              type: ":Boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: true
          - name: "range"
            value:
              type: ":Boolean"
              value: true
          - name: "message"
            value:
              type: ":Boolean"
              value: true
          - name: "handler"
            value:
              type: ":Boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: not equal left integer

This runtime case exercises “not equal left integer” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left integer.ges"
    program: main
```

### Source code under test

```ges
module atomicequalitynotequalleftinteger
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
              value: false
          - name: "quantity"
            value:
              type: ":Boolean"
              value: true
          - name: "text"
            value:
              type: ":Boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: true
          - name: "message"
            value:
              type: ":Boolean"
              value: true
          - name: "handler"
            value:
              type: ":Boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: not equal left float

This runtime case exercises “not equal left float” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left float.ges"
    program: main
```

### Source code under test

```ges
module atomicequalitynotequalleftfloat
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
              value: true
          - name: "quantity"
            value:
              type: ":Boolean"
              value: true
          - name: "text"
            value:
              type: ":Boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: true
          - name: "range"
            value:
              type: ":Boolean"
              value: true
          - name: "message"
            value:
              type: ":Boolean"
              value: true
          - name: "handler"
            value:
              type: ":Boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: not equal left percentage

This runtime case exercises “not equal left percentage” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left percentage.ges"
    program: main
```

### Source code under test

```ges
module atomicequalitynotequalleftpercentage
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
              value: false
          - name: "quantity"
            value:
              type: ":Boolean"
              value: true
          - name: "text"
            value:
              type: ":Boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: true
          - name: "message"
            value:
              type: ":Boolean"
              value: true
          - name: "handler"
            value:
              type: ":Boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: not equal left quantity

This runtime case exercises “not equal left quantity” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left quantity.ges"
    program: main
```

### Source code under test

```ges
module atomicequalitynotequalleftquantity
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
              value: true
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: true
          - name: "range"
            value:
              type: ":Boolean"
              value: true
          - name: "message"
            value:
              type: ":Boolean"
              value: true
          - name: "handler"
            value:
              type: ":Boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: not equal left text

This runtime case exercises “not equal left text” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left text.ges"
    program: main
```

### Source code under test

```ges
module atomicequalitynotequallefttext
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "quantity"
            value:
              type: ":Boolean"
              value: true
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
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: true
          - name: "range"
            value:
              type: ":Boolean"
              value: true
          - name: "message"
            value:
              type: ":Boolean"
              value: true
          - name: "handler"
            value:
              type: ":Boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: not equal left numeric tag

This runtime case exercises “not equal left numeric tag” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left numeric tag.ges"
    program: main
```

### Source code under test

```ges
module atomicequalitynotequalleftnumerictag
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "quantity"
            value:
              type: ":Boolean"
              value: true
          - name: "text"
            value:
              type: ":Boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: false
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: true
          - name: "range"
            value:
              type: ":Boolean"
              value: true
          - name: "message"
            value:
              type: ":Boolean"
              value: true
          - name: "handler"
            value:
              type: ":Boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: not equal left plain tag

This runtime case exercises “not equal left plain tag” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left plain tag.ges"
    program: main
```

### Source code under test

```ges
module atomicequalitynotequalleftplaintag
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "quantity"
            value:
              type: ":Boolean"
              value: true
          - name: "text"
            value:
              type: ":Boolean"
              value: true
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
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: true
          - name: "range"
            value:
              type: ":Boolean"
              value: true
          - name: "message"
            value:
              type: ":Boolean"
              value: true
          - name: "handler"
            value:
              type: ":Boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: not equal left vector

This runtime case exercises “not equal left vector” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left vector.ges"
    program: main
```

### Source code under test

```ges
module atomicequalitynotequalleftvector
on Start {
  let left be :Vector(1, 2, 3)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
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
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "quantity"
            value:
              type: ":Boolean"
              value: true
          - name: "text"
            value:
              type: ":Boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: true
          - name: "range"
            value:
              type: ":Boolean"
              value: true
          - name: "message"
            value:
              type: ":Boolean"
              value: true
          - name: "handler"
            value:
              type: ":Boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: not equal left point

This runtime case exercises “not equal left point” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left point.ges"
    program: main
```

### Source code under test

```ges
module atomicequalitynotequalleftpoint
on Start {
  let left be :Point(1, 2, 3)
  let rightNothing be nothing
  let rightBoolean be true
  let rightInteger be 10
  let rightFloat be 10.5
  let rightPercentage be 1000%
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
  emit Done(nothing: left <> rightNothing, boolean: left <> rightBoolean, integer: left <> rightInteger, float: left <> rightFloat, percentage: left <> rightPercentage, quantity: left <> rightQuantity, text: left <> rightText, numericTag: left <> rightNumericTag, plainTag: left <> rightPlainTag, vector: left <> rightVector, point: left <> rightPoint, list: left <> rightList, map: left <> rightMap, dice: left <> rightDice, range: left <> rightRange, message: left <> rightMessage, handler: left <> rightHandler, seriesValue: left <> rightSeries)
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
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "quantity"
            value:
              type: ":Boolean"
              value: true
          - name: "text"
            value:
              type: ":Boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: true
          - name: "range"
            value:
              type: ":Boolean"
              value: true
          - name: "message"
            value:
              type: ":Boolean"
              value: true
          - name: "handler"
            value:
              type: ":Boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: not equal left list

This runtime case exercises “not equal left list” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left list.ges"
    program: main
```

### Source code under test

```ges
module atomicequalitynotequalleftlist
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "quantity"
            value:
              type: ":Boolean"
              value: true
          - name: "text"
            value:
              type: ":Boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: true
          - name: "range"
            value:
              type: ":Boolean"
              value: true
          - name: "message"
            value:
              type: ":Boolean"
              value: true
          - name: "handler"
            value:
              type: ":Boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: not equal left map

This runtime case exercises “not equal left map” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left map.ges"
    program: main
```

### Source code under test

```ges
module atomicequalitynotequalleftmap
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "quantity"
            value:
              type: ":Boolean"
              value: true
          - name: "text"
            value:
              type: ":Boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
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
              value: true
          - name: "message"
            value:
              type: ":Boolean"
              value: true
          - name: "handler"
            value:
              type: ":Boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: not equal left dice

This runtime case exercises “not equal left dice” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left dice.ges"
    program: main
```

### Source code under test

```ges
module atomicequalitynotequalleftdice
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
              value: false
          - name: "quantity"
            value:
              type: ":Boolean"
              value: true
          - name: "text"
            value:
              type: ":Boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
          - name: "range"
            value:
              type: ":Boolean"
              value: true
          - name: "message"
            value:
              type: ":Boolean"
              value: true
          - name: "handler"
            value:
              type: ":Boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: not equal left range

This runtime case exercises “not equal left range” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left range.ges"
    program: main
```

### Source code under test

```ges
module atomicequalitynotequalleftrange
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "quantity"
            value:
              type: ":Boolean"
              value: true
          - name: "text"
            value:
              type: ":Boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
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
              value: true
          - name: "handler"
            value:
              type: ":Boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: not equal left message

This runtime case exercises “not equal left message” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left message.ges"
    program: main
```

### Source code under test

```ges
module atomicequalitynotequalleftmessage
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "quantity"
            value:
              type: ":Boolean"
              value: true
          - name: "text"
            value:
              type: ":Boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: true
          - name: "range"
            value:
              type: ":Boolean"
              value: true
          - name: "message"
            value:
              type: ":Boolean"
              value: false
          - name: "handler"
            value:
              type: ":Boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: not equal left handler

This runtime case exercises “not equal left handler” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left handler.ges"
    program: main
```

### Source code under test

```ges
module atomicequalitynotequallefthandler
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "quantity"
            value:
              type: ":Boolean"
              value: true
          - name: "text"
            value:
              type: ":Boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: true
          - name: "range"
            value:
              type: ":Boolean"
              value: true
          - name: "message"
            value:
              type: ":Boolean"
              value: true
          - name: "handler"
            value:
              type: ":Boolean"
              value: false
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: not equal left series

This runtime case exercises “not equal left series” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "not equal left series.ges"
    program: main
```

### Source code under test

```ges
module atomicequalitynotequalleftseries
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
  let rightVector be :Vector(1, 2, 3)
  let rightPoint be :Point(1, 2, 3)
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
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "quantity"
            value:
              type: ":Boolean"
              value: true
          - name: "text"
            value:
              type: ":Boolean"
              value: true
          - name: "numericTag"
            value:
              type: ":Boolean"
              value: true
          - name: "plainTag"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: true
          - name: "range"
            value:
              type: ":Boolean"
              value: true
          - name: "message"
            value:
              type: ":Boolean"
              value: true
          - name: "handler"
            value:
              type: ":Boolean"
              value: true
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: false
```
