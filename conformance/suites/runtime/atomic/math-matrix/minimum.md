---
formatVersion: 1
suiteId: "runtime.atomic.math-matrix.minimum"
title: "Math Matrix — Minimum"
categories: [conformance]
---

# Math Matrix — Minimum

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers minimum selection across primitive, measured, and structured values.

---

## Test: min nothing

This runtime case exercises “min nothing” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0110
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "min nothing.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdd
on Start {
  let a be nothing
  let vNothing be nothing
  let vInteger be 10
  let vFloat be 10.3
  let vPercentage be 10%
  let vMeter be 10m
  let vBoolean be true
  let vTextNumber be '10.3'
  let vTextInvalid be 'hello'
  let vTagPi be pi
  let vTagNan be #nan
  let vTagInfinity be infinity
  let vVector be :Vector(1, 2, 3)
  let vPoint be :Point(1, 2, 3)
  emit Done(nothing: min of a and vNothing, integer: min of a and vInteger, float: min of a and vFloat, percentage: min of a and vPercentage, meter: min of a and vMeter, boolean: min of a and vBoolean, textNumber: min of a and vTextNumber, textInvalid: min of a and vTextInvalid, tagPi: min of a and vTagPi, tagNan: min of a and vTagNan, tagInfinity: min of a and vTagInfinity, vector: min of a and vVector, point: min of a and vPoint)
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
          - name: "integer"
            value:
              type: ":Nothing"
          - name: "float"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "meter"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Nothing"
          - name: "textNumber"
            value:
              type: ":Nothing"
          - name: "textInvalid"
            value:
              type: ":Nothing"
          - name: "tagPi"
            value:
              type: ":Nothing"
          - name: "tagNan"
            value:
              type: ":Nothing"
          - name: "tagInfinity"
            value:
              type: ":Nothing"
          - name: "vector"
            value:
              type: ":Nothing"
          - name: "point"
            value:
              type: ":Nothing"
```

---

## Test: min integer

This runtime case exercises “min integer” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0111
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "min integer.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixde
on Start {
  let a be 10
  let vNothing be nothing
  let vInteger be 10
  let vFloat be 10.3
  let vPercentage be 10%
  let vMeter be 10m
  let vBoolean be true
  let vTextNumber be '10.3'
  let vTextInvalid be 'hello'
  let vTagPi be pi
  let vTagNan be #nan
  let vTagInfinity be infinity
  let vVector be :Vector(1, 2, 3)
  let vPoint be :Point(1, 2, 3)
  emit Done(nothing: min of a and vNothing, integer: min of a and vInteger, float: min of a and vFloat, percentage: min of a and vPercentage, meter: min of a and vMeter, boolean: min of a and vBoolean, textNumber: min of a and vTextNumber, textInvalid: min of a and vTextInvalid, tagPi: min of a and vTagPi, tagNan: min of a and vTagNan, tagInfinity: min of a and vTagInfinity, vector: min of a and vVector, point: min of a and vPoint)
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
          - name: "integer"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "float"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: true
          - name: "textNumber"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "textInvalid"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "tagInfinity"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "vector"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "point"
            value:
              type: ":Number.int64"
              value: "10"
```

---

## Test: min float

This runtime case exercises “min float” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0112
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "min float.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdf
on Start {
  let a be 10.3
  let vNothing be nothing
  let vInteger be 10
  let vFloat be 10.3
  let vPercentage be 10%
  let vMeter be 10m
  let vBoolean be true
  let vTextNumber be '10.3'
  let vTextInvalid be 'hello'
  let vTagPi be pi
  let vTagNan be #nan
  let vTagInfinity be infinity
  let vVector be :Vector(1, 2, 3)
  let vPoint be :Point(1, 2, 3)
  emit Done(nothing: min of a and vNothing, integer: min of a and vInteger, float: min of a and vFloat, percentage: min of a and vPercentage, meter: min of a and vMeter, boolean: min of a and vBoolean, textNumber: min of a and vTextNumber, textInvalid: min of a and vTextInvalid, tagPi: min of a and vTagPi, tagNan: min of a and vTagNan, tagInfinity: min of a and vTagInfinity, vector: min of a and vVector, point: min of a and vPoint)
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
          - name: "integer"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "10.3"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: true
          - name: "textNumber"
            value:
              type: ":Number.binary64"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":Number.binary64"
              value: "10.3"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "10.3"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "10.3"
          - name: "vector"
            value:
              type: ":Number.binary64"
              value: "10.3"
          - name: "point"
            value:
              type: ":Number.binary64"
              value: "10.3"
```

---

## Test: min percentage

This runtime case exercises “min percentage” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0113
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "min percentage.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdg
on Start {
  let a be 10%
  let vNothing be nothing
  let vInteger be 10
  let vFloat be 10.3
  let vPercentage be 10%
  let vMeter be 10m
  let vBoolean be true
  let vTextNumber be '10.3'
  let vTextInvalid be 'hello'
  let vTagPi be pi
  let vTagNan be #nan
  let vTagInfinity be infinity
  let vVector be :Vector(1, 2, 3)
  let vPoint be :Point(1, 2, 3)
  emit Done(nothing: min of a and vNothing, integer: min of a and vInteger, float: min of a and vFloat, percentage: min of a and vPercentage, meter: min of a and vMeter, boolean: min of a and vBoolean, textNumber: min of a and vTextNumber, textInvalid: min of a and vTextInvalid, tagPi: min of a and vTagPi, tagNan: min of a and vTagNan, tagInfinity: min of a and vTagInfinity, vector: min of a and vVector, point: min of a and vPoint)
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
          - name: "integer"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "float"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "textNumber"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "textInvalid"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "tagPi"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "tagNan"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "tagInfinity"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "vector"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "point"
            value:
              type: ":Percentage"
              value: "0.1"
```

---

## Test: min meter

This runtime case exercises “min meter” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0114
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "min meter.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdh
on Start {
  let a be 10m
  let vNothing be nothing
  let vInteger be 10
  let vFloat be 10.3
  let vPercentage be 10%
  let vMeter be 10m
  let vBoolean be true
  let vTextNumber be '10.3'
  let vTextInvalid be 'hello'
  let vTagPi be pi
  let vTagNan be #nan
  let vTagInfinity be infinity
  let vVector be :Vector(1, 2, 3)
  let vPoint be :Point(1, 2, 3)
  emit Done(nothing: min of a and vNothing, integer: min of a and vInteger, float: min of a and vFloat, percentage: min of a and vPercentage, meter: min of a and vMeter, boolean: min of a and vBoolean, textNumber: min of a and vTextNumber, textInvalid: min of a and vTextInvalid, tagPi: min of a and vTagPi, tagNan: min of a and vTagNan, tagInfinity: min of a and vTagInfinity, vector: min of a and vVector, point: min of a and vPoint)
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
          - name: "integer"
            value:
              type: ":Nothing"
          - name: "float"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "meter"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":Nothing"
          - name: "textNumber"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: ":meter"
          - name: "textInvalid"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: ":meter"
          - name: "tagPi"
            value:
              type: ":Nothing"
          - name: "tagNan"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: ":meter"
          - name: "tagInfinity"
            value:
              type: ":Nothing"
          - name: "vector"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: ":meter"
          - name: "point"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: ":meter"
```

---

## Test: min boolean

This runtime case exercises “min boolean” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0115
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "min boolean.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdi
on Start {
  let a be true
  let vNothing be nothing
  let vInteger be 10
  let vFloat be 10.3
  let vPercentage be 10%
  let vMeter be 10m
  let vBoolean be true
  let vTextNumber be '10.3'
  let vTextInvalid be 'hello'
  let vTagPi be pi
  let vTagNan be #nan
  let vTagInfinity be infinity
  let vVector be :Vector(1, 2, 3)
  let vPoint be :Point(1, 2, 3)
  emit Done(nothing: min of a and vNothing, integer: min of a and vInteger, float: min of a and vFloat, percentage: min of a and vPercentage, meter: min of a and vMeter, boolean: min of a and vBoolean, textNumber: min of a and vTextNumber, textInvalid: min of a and vTextInvalid, tagPi: min of a and vTagPi, tagNan: min of a and vTagNan, tagInfinity: min of a and vTagInfinity, vector: min of a and vVector, point: min of a and vPoint)
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
              type: ":Percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: true
          - name: "textNumber"
            value:
              type: ":Text"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "hello"
          - name: "tagPi"
            value:
              type: ":Boolean"
              value: true
          - name: "tagNan"
            value:
              type: ":Tag"
              value: "nan"
          - name: "tagInfinity"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "point"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
```

---

## Test: min textNumber

This runtime case exercises “min textNumber” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0116
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "min textNumber.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdj
on Start {
  let a be '10.3'
  let vNothing be nothing
  let vInteger be 10
  let vFloat be 10.3
  let vPercentage be 10%
  let vMeter be 10m
  let vBoolean be true
  let vTextNumber be '10.3'
  let vTextInvalid be 'hello'
  let vTagPi be pi
  let vTagNan be #nan
  let vTagInfinity be infinity
  let vVector be :Vector(1, 2, 3)
  let vPoint be :Point(1, 2, 3)
  emit Done(nothing: min of a and vNothing, integer: min of a and vInteger, float: min of a and vFloat, percentage: min of a and vPercentage, meter: min of a and vMeter, boolean: min of a and vBoolean, textNumber: min of a and vTextNumber, textInvalid: min of a and vTextInvalid, tagPi: min of a and vTagPi, tagNan: min of a and vTagNan, tagInfinity: min of a and vTagInfinity, vector: min of a and vVector, point: min of a and vPoint)
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
          - name: "integer"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "10.3"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":Text"
              value: "10.3"
          - name: "textNumber"
            value:
              type: ":Text"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "10.3"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":Text"
              value: "10.3"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Text"
              value: "10.3"
          - name: "point"
            value:
              type: ":Text"
              value: "10.3"
```

---

## Test: min textInvalid

This runtime case exercises “min textInvalid” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0117
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "min textInvalid.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdk
on Start {
  let a be 'hello'
  let vNothing be nothing
  let vInteger be 10
  let vFloat be 10.3
  let vPercentage be 10%
  let vMeter be 10m
  let vBoolean be true
  let vTextNumber be '10.3'
  let vTextInvalid be 'hello'
  let vTagPi be pi
  let vTagNan be #nan
  let vTagInfinity be infinity
  let vVector be :Vector(1, 2, 3)
  let vPoint be :Point(1, 2, 3)
  emit Done(nothing: min of a and vNothing, integer: min of a and vInteger, float: min of a and vFloat, percentage: min of a and vPercentage, meter: min of a and vMeter, boolean: min of a and vBoolean, textNumber: min of a and vTextNumber, textInvalid: min of a and vTextInvalid, tagPi: min of a and vTagPi, tagNan: min of a and vTagNan, tagInfinity: min of a and vTagInfinity, vector: min of a and vVector, point: min of a and vPoint)
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
          - name: "integer"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "10.3"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":Text"
              value: "hello"
          - name: "textNumber"
            value:
              type: ":Text"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "hello"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":Text"
              value: "hello"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Text"
              value: "hello"
          - name: "point"
            value:
              type: ":Text"
              value: "hello"
```

---

## Test: min tagPi

This runtime case exercises “min tagPi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0118
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "min tagPi.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdl
on Start {
  let a be pi
  let vNothing be nothing
  let vInteger be 10
  let vFloat be 10.3
  let vPercentage be 10%
  let vMeter be 10m
  let vBoolean be true
  let vTextNumber be '10.3'
  let vTextInvalid be 'hello'
  let vTagPi be pi
  let vTagNan be #nan
  let vTagInfinity be infinity
  let vVector be :Vector(1, 2, 3)
  let vPoint be :Point(1, 2, 3)
  emit Done(nothing: min of a and vNothing, integer: min of a and vInteger, float: min of a and vFloat, percentage: min of a and vPercentage, meter: min of a and vMeter, boolean: min of a and vBoolean, textNumber: min of a and vTextNumber, textInvalid: min of a and vTextInvalid, tagPi: min of a and vTagPi, tagNan: min of a and vTagNan, tagInfinity: min of a and vTagInfinity, vector: min of a and vVector, point: min of a and vPoint)
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
          - name: "integer"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: true
          - name: "textNumber"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "textInvalid"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "vector"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "point"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
```

---

## Test: min tagNan

This runtime case exercises “min tagNan” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0119
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "min tagNan.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdm
on Start {
  let a be #nan
  let vNothing be nothing
  let vInteger be 10
  let vFloat be 10.3
  let vPercentage be 10%
  let vMeter be 10m
  let vBoolean be true
  let vTextNumber be '10.3'
  let vTextInvalid be 'hello'
  let vTagPi be pi
  let vTagNan be #nan
  let vTagInfinity be infinity
  let vVector be :Vector(1, 2, 3)
  let vPoint be :Point(1, 2, 3)
  emit Done(nothing: min of a and vNothing, integer: min of a and vInteger, float: min of a and vFloat, percentage: min of a and vPercentage, meter: min of a and vMeter, boolean: min of a and vBoolean, textNumber: min of a and vTextNumber, textInvalid: min of a and vTextInvalid, tagPi: min of a and vTagPi, tagNan: min of a and vTagNan, tagInfinity: min of a and vTagInfinity, vector: min of a and vVector, point: min of a and vPoint)
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
          - name: "integer"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "10.3"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":Tag"
              value: "nan"
          - name: "textNumber"
            value:
              type: ":Text"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "hello"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":Tag"
              value: "nan"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Tag"
              value: "nan"
          - name: "point"
            value:
              type: ":Tag"
              value: "nan"
```

---

## Test: min tagInfinity

This runtime case exercises “min tagInfinity” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0120
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "min tagInfinity.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdn
on Start {
  let a be infinity
  let vNothing be nothing
  let vInteger be 10
  let vFloat be 10.3
  let vPercentage be 10%
  let vMeter be 10m
  let vBoolean be true
  let vTextNumber be '10.3'
  let vTextInvalid be 'hello'
  let vTagPi be pi
  let vTagNan be #nan
  let vTagInfinity be infinity
  let vVector be :Vector(1, 2, 3)
  let vPoint be :Point(1, 2, 3)
  emit Done(nothing: min of a and vNothing, integer: min of a and vInteger, float: min of a and vFloat, percentage: min of a and vPercentage, meter: min of a and vMeter, boolean: min of a and vBoolean, textNumber: min of a and vTextNumber, textInvalid: min of a and vTextInvalid, tagPi: min of a and vTagPi, tagNan: min of a and vTagNan, tagInfinity: min of a and vTagInfinity, vector: min of a and vVector, point: min of a and vPoint)
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
          - name: "integer"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "10.3"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: true
          - name: "textNumber"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "textInvalid"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "point"
            value:
              type: ":Number.binary64"
              value: "Infinity"
```

---

## Test: min vector

This runtime case exercises “min vector” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0121
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "min vector.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdo
on Start {
  let a be :Vector(1, 2, 3)
  let vNothing be nothing
  let vInteger be 10
  let vFloat be 10.3
  let vPercentage be 10%
  let vMeter be 10m
  let vBoolean be true
  let vTextNumber be '10.3'
  let vTextInvalid be 'hello'
  let vTagPi be pi
  let vTagNan be #nan
  let vTagInfinity be infinity
  let vVector be :Vector(1, 2, 3)
  let vPoint be :Point(1, 2, 3)
  emit Done(nothing: min of a and vNothing, integer: min of a and vInteger, float: min of a and vFloat, percentage: min of a and vPercentage, meter: min of a and vMeter, boolean: min of a and vBoolean, textNumber: min of a and vTextNumber, textInvalid: min of a and vTextInvalid, tagPi: min of a and vTagPi, tagNan: min of a and vTagNan, tagInfinity: min of a and vTagInfinity, vector: min of a and vVector, point: min of a and vPoint)
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
          - name: "integer"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "10.3"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "textNumber"
            value:
              type: ":Text"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "hello"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":Tag"
              value: "nan"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "point"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
```

---

## Test: min point

This runtime case exercises “min point” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0122
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "min point.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdp
on Start {
  let a be :Point(1, 2, 3)
  let vNothing be nothing
  let vInteger be 10
  let vFloat be 10.3
  let vPercentage be 10%
  let vMeter be 10m
  let vBoolean be true
  let vTextNumber be '10.3'
  let vTextInvalid be 'hello'
  let vTagPi be pi
  let vTagNan be #nan
  let vTagInfinity be infinity
  let vVector be :Vector(1, 2, 3)
  let vPoint be :Point(1, 2, 3)
  emit Done(nothing: min of a and vNothing, integer: min of a and vInteger, float: min of a and vFloat, percentage: min of a and vPercentage, meter: min of a and vMeter, boolean: min of a and vBoolean, textNumber: min of a and vTextNumber, textInvalid: min of a and vTextInvalid, tagPi: min of a and vTagPi, tagNan: min of a and vTagNan, tagInfinity: min of a and vTagInfinity, vector: min of a and vVector, point: min of a and vPoint)
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
          - name: "integer"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "10.3"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
          - name: "textNumber"
            value:
              type: ":Text"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "hello"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":Tag"
              value: "nan"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "point"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
```
