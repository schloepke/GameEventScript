---
formatVersion: 1
suiteId: "runtime.atomic.math-matrix.multiplication"
title: "Math Matrix — Multiplication"
categories: [conformance]
---

# Math Matrix — Multiplication

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers multiplication across primitive, measured, and spatial values.

---

## Test: multiply nothing

This runtime case exercises “multiply nothing” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "multiply nothing.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixad
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
  emit Done(nothing: a * vNothing, integer: a * vInteger, float: a * vFloat, percentage: a * vPercentage, meter: a * vMeter, boolean: a * vBoolean, textNumber: a * vTextNumber, textInvalid: a * vTextInvalid, tagPi: a * vTagPi, tagNan: a * vTagNan, tagInfinity: a * vTagInfinity, vector: a * vVector, point: a * vPoint)
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

## Test: multiply integer

This runtime case exercises “multiply integer” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "multiply integer.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixae
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
  emit Done(nothing: a * vNothing, integer: a * vInteger, float: a * vFloat, percentage: a * vPercentage, meter: a * vMeter, boolean: a * vBoolean, textNumber: a * vTextNumber, textInvalid: a * vTextInvalid, tagPi: a * vTagPi, tagNan: a * vTagNan, tagInfinity: a * vTagInfinity, vector: a * vVector, point: a * vPoint)
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
              value: "100"
          - name: "float"
            value:
              type: ":Number.int64"
              value: "103"
          - name: "percentage"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "meter"
            value:
              type: ":Quantity.int64"
              value: "100"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "textNumber"
            value:
              type: ":Nothing"
          - name: "textInvalid"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "31.4159265358979"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Vector"
              x: "10"
              y: "20"
              z: "30"
          - name: "point"
            value:
              type: ":Number.binary64"
              value: "NaN"
```

---

## Test: multiply float

This runtime case exercises “multiply float” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "multiply float.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixaf
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
  emit Done(nothing: a * vNothing, integer: a * vInteger, float: a * vFloat, percentage: a * vPercentage, meter: a * vMeter, boolean: a * vBoolean, textNumber: a * vTextNumber, textInvalid: a * vTextInvalid, tagPi: a * vTagPi, tagNan: a * vTagNan, tagInfinity: a * vTagInfinity, vector: a * vVector, point: a * vPoint)
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
              value: "103"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "106.09"
          - name: "percentage"
            value:
              type: ":Number.binary64"
              value: "1.03"
          - name: "meter"
            value:
              type: ":Quantity.int64"
              value: "103"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":Number.binary64"
              value: "10.3"
          - name: "textNumber"
            value:
              type: ":Nothing"
          - name: "textInvalid"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "32.3584043319749"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Vector"
              x: "10.3"
              y: "20.6"
              z: "30.9"
          - name: "point"
            value:
              type: ":Number.binary64"
              value: "NaN"
```

---

## Test: multiply percentage

This runtime case exercises “multiply percentage” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "multiply percentage.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixag
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
  emit Done(nothing: a * vNothing, integer: a * vInteger, float: a * vFloat, percentage: a * vPercentage, meter: a * vMeter, boolean: a * vBoolean, textNumber: a * vTextNumber, textInvalid: a * vTextInvalid, tagPi: a * vTagPi, tagNan: a * vTagNan, tagInfinity: a * vTagInfinity, vector: a * vVector, point: a * vPoint)
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
              value: "1"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "1.03"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0.01"
          - name: "meter"
            value:
              type: ":Quantity.int64"
              value: "1"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":Number.binary64"
              value: "0.1"
          - name: "textNumber"
            value:
              type: ":Nothing"
          - name: "textInvalid"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "0.314159265358979"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Vector"
              x: "0.1"
              y: "0.2"
              z: "0.3"
          - name: "point"
            value:
              type: ":Number.binary64"
              value: "NaN"
```

---

## Test: multiply meter

This runtime case exercises “multiply meter” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "multiply meter.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixah
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
  emit Done(nothing: a * vNothing, integer: a * vInteger, float: a * vFloat, percentage: a * vPercentage, meter: a * vMeter, boolean: a * vBoolean, textNumber: a * vTextNumber, textInvalid: a * vTextInvalid, tagPi: a * vTagPi, tagNan: a * vTagNan, tagInfinity: a * vTagInfinity, vector: a * vVector, point: a * vPoint)
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
              type: ":Quantity.int64"
              value: "100"
              unit: ":meter"
          - name: "float"
            value:
              type: ":Quantity.int64"
              value: "103"
              unit: ":meter"
          - name: "percentage"
            value:
              type: ":Quantity.int64"
              value: "1"
              unit: ":meter"
          - name: "meter"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "boolean"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: ":meter"
          - name: "textNumber"
            value:
              type: ":Nothing"
          - name: "textInvalid"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":Quantity.binary64"
              value: "31.4159265358979"
              unit: ":meter"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Quantity.binary64"
              value: "Infinity"
              unit: ":meter"
          - name: "vector"
            value:
              type: ":Vector"
              x: "10"
              y: "20"
              z: "30"
              unit: ":meter"
          - name: "point"
            value:
              type: ":Number.binary64"
              value: "NaN"
```

---

## Test: multiply boolean

This runtime case exercises “multiply boolean” and verifies the declared messages, values, and execution result.

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
  - name: "multiply boolean.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixai
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
  emit Done(nothing: a * vNothing, integer: a * vInteger, float: a * vFloat, percentage: a * vPercentage, meter: a * vMeter, boolean: a * vBoolean, textNumber: a * vTextNumber, textInvalid: a * vTextInvalid, tagPi: a * vTagPi, tagNan: a * vTagNan, tagInfinity: a * vTagInfinity, vector: a * vVector, point: a * vPoint)
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
              type: ":Number.binary64"
              value: "0.1"
          - name: "meter"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "textNumber"
            value:
              type: ":Nothing"
          - name: "textInvalid"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
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
              type: ":Number.binary64"
              value: "NaN"
```

---

## Test: multiply textNumber

This runtime case exercises “multiply textNumber” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "multiply textNumber.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixaj
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
  emit Done(nothing: a * vNothing, integer: a * vInteger, float: a * vFloat, percentage: a * vPercentage, meter: a * vMeter, boolean: a * vBoolean, textNumber: a * vTextNumber, textInvalid: a * vTextInvalid, tagPi: a * vTagPi, tagNan: a * vTagNan, tagInfinity: a * vTagInfinity, vector: a * vVector, point: a * vPoint)
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

## Test: multiply textInvalid

This runtime case exercises “multiply textInvalid” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "multiply textInvalid.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixak
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
  emit Done(nothing: a * vNothing, integer: a * vInteger, float: a * vFloat, percentage: a * vPercentage, meter: a * vMeter, boolean: a * vBoolean, textNumber: a * vTextNumber, textInvalid: a * vTextInvalid, tagPi: a * vTagPi, tagNan: a * vTagNan, tagInfinity: a * vTagInfinity, vector: a * vVector, point: a * vPoint)
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
              value: "NaN"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "percentage"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "meter"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "boolean"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "textNumber"
            value:
              type: ":Nothing"
          - name: "textInvalid"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "vector"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "point"
            value:
              type: ":Number.binary64"
              value: "NaN"
```

---

## Test: multiply tagPi

This runtime case exercises “multiply tagPi” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "multiply tagPi.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixal
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
  emit Done(nothing: a * vNothing, integer: a * vInteger, float: a * vFloat, percentage: a * vPercentage, meter: a * vMeter, boolean: a * vBoolean, textNumber: a * vTextNumber, textInvalid: a * vTextInvalid, tagPi: a * vTagPi, tagNan: a * vTagNan, tagInfinity: a * vTagInfinity, vector: a * vVector, point: a * vPoint)
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
              value: "31.4159265358979"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "32.3584043319749"
          - name: "percentage"
            value:
              type: ":Number.binary64"
              value: "0.314159265358979"
          - name: "meter"
            value:
              type: ":Quantity.binary64"
              value: "31.4159265358979"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "textNumber"
            value:
              type: ":Nothing"
          - name: "textInvalid"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "9.86960440108936"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Vector"
              x: "3.141592653589793"
              y: "6.28318530717959"
              z: "9.42477796076938"
          - name: "point"
            value:
              type: ":Number.binary64"
              value: "NaN"
```

---

## Test: multiply tagNan

This runtime case exercises “multiply tagNan” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "multiply tagNan.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixam
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
  emit Done(nothing: a * vNothing, integer: a * vInteger, float: a * vFloat, percentage: a * vPercentage, meter: a * vMeter, boolean: a * vBoolean, textNumber: a * vTextNumber, textInvalid: a * vTextInvalid, tagPi: a * vTagPi, tagNan: a * vTagNan, tagInfinity: a * vTagInfinity, vector: a * vVector, point: a * vPoint)
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
              value: "NaN"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "percentage"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "meter"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "boolean"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "textNumber"
            value:
              type: ":Nothing"
          - name: "textInvalid"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "vector"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "point"
            value:
              type: ":Number.binary64"
              value: "NaN"
```

---

## Test: multiply tagInfinity

This runtime case exercises “multiply tagInfinity” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "multiply tagInfinity.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixan
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
  emit Done(nothing: a * vNothing, integer: a * vInteger, float: a * vFloat, percentage: a * vPercentage, meter: a * vMeter, boolean: a * vBoolean, textNumber: a * vTextNumber, textInvalid: a * vTextInvalid, tagPi: a * vTagPi, tagNan: a * vTagNan, tagInfinity: a * vTagInfinity, vector: a * vVector, point: a * vPoint)
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
              value: "Infinity"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "percentage"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "meter"
            value:
              type: ":Quantity.binary64"
              value: "Infinity"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "textNumber"
            value:
              type: ":Nothing"
          - name: "textInvalid"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "point"
            value:
              type: ":Number.binary64"
              value: "NaN"
```

---

## Test: multiply vector

This runtime case exercises “multiply vector” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "multiply vector.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixao
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
  emit Done(nothing: a * vNothing, integer: a * vInteger, float: a * vFloat, percentage: a * vPercentage, meter: a * vMeter, boolean: a * vBoolean, textNumber: a * vTextNumber, textInvalid: a * vTextInvalid, tagPi: a * vTagPi, tagNan: a * vTagNan, tagInfinity: a * vTagInfinity, vector: a * vVector, point: a * vPoint)
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
              type: ":Vector"
              x: "10"
              y: "20"
              z: "30"
          - name: "float"
            value:
              type: ":Vector"
              x: "10.3"
              y: "20.6"
              z: "30.9"
          - name: "percentage"
            value:
              type: ":Vector"
              x: "0.1"
              y: "0.2"
              z: "0.3"
          - name: "meter"
            value:
              type: ":Vector"
              x: "10"
              y: "20"
              z: "30"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "textNumber"
            value:
              type: ":Nothing"
          - name: "textInvalid"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":Vector"
              x: "3.141592653589793"
              y: "6.28318530717959"
              z: "9.42477796076938"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "vector"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "point"
            value:
              type: ":Number.binary64"
              value: "NaN"
```

---

## Test: multiply point

This runtime case exercises “multiply point” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "multiply point.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixap
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
  emit Done(nothing: a * vNothing, integer: a * vInteger, float: a * vFloat, percentage: a * vPercentage, meter: a * vMeter, boolean: a * vBoolean, textNumber: a * vTextNumber, textInvalid: a * vTextInvalid, tagPi: a * vTagPi, tagNan: a * vTagNan, tagInfinity: a * vTagInfinity, vector: a * vVector, point: a * vPoint)
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
              value: "NaN"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "percentage"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "meter"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "boolean"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "textNumber"
            value:
              type: ":Nothing"
          - name: "textInvalid"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "vector"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "point"
            value:
              type: ":Number.binary64"
              value: "NaN"
```
