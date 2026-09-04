---
formatVersion: 1
suiteId: "runtime.atomic.equality.equal"
title: "Equality Matrix — Equal"
categories: [conformance]
---

# Equality Matrix — Equal

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers equality across every supported value family and the cross-kind structural invariants.

---

## Test: equal left nothing

This runtime case exercises “equal left nothing” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left nothing.ges"
    program: main
```

### Source code under test

```ges
module atomicequalityequalleftnothing
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
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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

## Test: equal left boolean

This runtime case exercises “equal left boolean” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left boolean.ges"
    program: main
```

### Source code under test

```ges
module atomicequalityequalleftboolean
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
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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

## Test: equal left integer

This runtime case exercises “equal left integer” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left integer.ges"
    program: main
```

### Source code under test

```ges
module atomicequalityequalleftinteger
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
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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

## Test: equal left float

This runtime case exercises “equal left float” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left float.ges"
    program: main
```

### Source code under test

```ges
module atomicequalityequalleftfloat
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
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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

## Test: equal left percentage

This runtime case exercises “equal left percentage” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left percentage.ges"
    program: main
```

### Source code under test

```ges
module atomicequalityequalleftpercentage
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
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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

## Test: equal left quantity

This runtime case exercises “equal left quantity” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left quantity.ges"
    program: main
```

### Source code under test

```ges
module atomicequalityequalleftquantity
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
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
              value: true
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

## Test: equal left text

This runtime case exercises “equal left text” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left text.ges"
    program: main
```

### Source code under test

```ges
module atomicequalityequallefttext
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
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
              value: true
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

## Test: equal left numeric tag

This runtime case exercises “equal left numeric tag” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left numeric tag.ges"
    program: main
```

### Source code under test

```ges
module atomicequalityequalleftnumerictag
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
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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

## Test: equal left plain tag

This runtime case exercises “equal left plain tag” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left plain tag.ges"
    program: main
```

### Source code under test

```ges
module atomicequalityequalleftplaintag
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
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
              value: true
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

## Test: equal left vector

This runtime case exercises “equal left vector” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left vector.ges"
    program: main
```

### Source code under test

```ges
module atomicequalityequalleftvector
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
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
              value: true
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

## Test: equal left point

This runtime case exercises “equal left point” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left point.ges"
    program: main
```

### Source code under test

```ges
module atomicequalityequalleftpoint
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
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
              value: true
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

## Test: equal left list

This runtime case exercises “equal left list” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left list.ges"
    program: main
```

### Source code under test

```ges
module atomicequalityequalleftlist
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
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
              value: true
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

## Test: equal left map

This runtime case exercises “equal left map” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left map.ges"
    program: main
```

### Source code under test

```ges
module atomicequalityequalleftmap
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
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
              value: true
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

## Test: equal left dice

This runtime case exercises “equal left dice” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left dice.ges"
    program: main
```

### Source code under test

```ges
module atomicequalityequalleftdice
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
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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

## Test: equal left range

This runtime case exercises “equal left range” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left range.ges"
    program: main
```

### Source code under test

```ges
module atomicequalityequalleftrange
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
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
              value: true
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

## Test: equal left message

This runtime case exercises “equal left message” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left message.ges"
    program: main
```

### Source code under test

```ges
module atomicequalityequalleftmessage
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
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
              value: true
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

## Test: equal left handler

This runtime case exercises “equal left handler” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left handler.ges"
    program: main
```

### Source code under test

```ges
module atomicequalityequallefthandler
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
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
              value: true
          - name: "seriesValue"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: equal left series

This runtime case exercises “equal left series” and verifies the declared messages, values, and execution result.

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
  sequence: ["6", "4", "5", "5"]
sources:
  - name: "equal left series.ges"
    program: main
```

### Source code under test

```ges
module atomicequalityequalleftseries
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
  emit Done(nothing: left = rightNothing, boolean: left = rightBoolean, integer: left = rightInteger, float: left = rightFloat, percentage: left = rightPercentage, quantity: left = rightQuantity, text: left = rightText, numericTag: left = rightNumericTag, plainTag: left = rightPlainTag, vector: left = rightVector, point: left = rightPoint, list: left = rightList, map: left = rightMap, dice: left = rightDice, range: left = rightRange, message: left = rightMessage, handler: left = rightHandler, seriesValue: left = rightSeries)
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
              value: true
```

---

## Test: cross kind and structural equality are deterministic

This runtime case exercises “cross kind and structural equality are deterministic” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "cross kind and structural equality are deterministic.ges"
    program: main
```

### Source code under test

```ges
module atomicdeterministicequality
record :First as { value: :Number }
record :Second as { value: :Number }

on Start {
  let leftDice be ([6, 4]) as :Dice
  let rightDice be ([5, 5]) as :Dice
  let first be :First(value: 10)
  let sameFirst be :First(value: 10)
  let second be :Second(value: 10)
  let plain be [value: 10]
  emit Done(numericCrossKind: 10 = 1000%, diceBySum: leftDice = rightDice, nestedNumericExact: [10] = [1000%], nestedDiceExact: [leftDice] = [rightDice], textTagDifferent: 'same' = #same, sameRecord: first = sameFirst, differentRecordType: first = second, recordMapDifferent: first = plain, mapRecordDifferent: plain = first, mapsIgnoreSourceOrder: [b: 2, a: 1] = [a: 1, b: 2])
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
          - name: "numericCrossKind"
            value:
              type: ":Boolean"
              value: true
          - name: "diceBySum"
            value:
              type: ":Boolean"
              value: true
          - name: "nestedNumericExact"
            value:
              type: ":Boolean"
              value: false
          - name: "nestedDiceExact"
            value:
              type: ":Boolean"
              value: false
          - name: "textTagDifferent"
            value:
              type: ":Boolean"
              value: false
          - name: "sameRecord"
            value:
              type: ":Boolean"
              value: true
          - name: "differentRecordType"
            value:
              type: ":Boolean"
              value: false
          - name: "recordMapDifferent"
            value:
              type: ":Boolean"
              value: false
          - name: "mapRecordDifferent"
            value:
              type: ":Boolean"
              value: false
          - name: "mapsIgnoreSourceOrder"
            value:
              type: ":Boolean"
              value: true
```
