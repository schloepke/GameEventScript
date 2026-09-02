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
module AtomicMathMatrixAD
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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
          - name: "meter"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":nothing"
          - name: "textNumber"
            value:
              type: ":nothing"
          - name: "textInvalid"
            value:
              type: ":nothing"
          - name: "tagPi"
            value:
              type: ":nothing"
          - name: "tagNan"
            value:
              type: ":nothing"
          - name: "tagInfinity"
            value:
              type: ":nothing"
          - name: "vector"
            value:
              type: ":nothing"
          - name: "point"
            value:
              type: ":nothing"
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
module AtomicMathMatrixAE
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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
              type: ":nothing"
          - name: "integer"
            value:
              type: ":integer"
              value: "100"
          - name: "float"
            value:
              type: ":integer"
              value: "103"
          - name: "percentage"
            value:
              type: ":integer"
              value: "1"
          - name: "meter"
            value:
              type: ":integer"
              value: "100"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":integer"
              value: "10"
          - name: "textNumber"
            value:
              type: ":nothing"
          - name: "textInvalid"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":float"
              value: "31.4159265358979"
          - name: "tagNan"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":vector"
              x: "10"
              y: "20"
              z: "30"
          - name: "point"
            value:
              type: ":float"
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
module AtomicMathMatrixAF
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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
              type: ":nothing"
          - name: "integer"
            value:
              type: ":integer"
              value: "103"
          - name: "float"
            value:
              type: ":float"
              value: "106.09"
          - name: "percentage"
            value:
              type: ":float"
              value: "1.03"
          - name: "meter"
            value:
              type: ":integer"
              value: "103"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":float"
              value: "10.3"
          - name: "textNumber"
            value:
              type: ":nothing"
          - name: "textInvalid"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":float"
              value: "32.3584043319749"
          - name: "tagNan"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":vector"
              x: "10.3"
              y: "20.6"
              z: "30.9"
          - name: "point"
            value:
              type: ":float"
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
module AtomicMathMatrixAG
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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
              type: ":nothing"
          - name: "integer"
            value:
              type: ":integer"
              value: "1"
          - name: "float"
            value:
              type: ":float"
              value: "1.03"
          - name: "percentage"
            value:
              type: ":percentage"
              value: "0.01"
          - name: "meter"
            value:
              type: ":integer"
              value: "1"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":float"
              value: "0.1"
          - name: "textNumber"
            value:
              type: ":nothing"
          - name: "textInvalid"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":float"
              value: "0.314159265358979"
          - name: "tagNan"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":vector"
              x: "0.1"
              y: "0.2"
              z: "0.3"
          - name: "point"
            value:
              type: ":float"
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
module AtomicMathMatrixAH
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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
              type: ":nothing"
          - name: "integer"
            value:
              type: ":integer"
              value: "100"
              unit: ":meter"
          - name: "float"
            value:
              type: ":integer"
              value: "103"
              unit: ":meter"
          - name: "percentage"
            value:
              type: ":integer"
              value: "1"
              unit: ":meter"
          - name: "meter"
            value:
              type: ":float"
              value: "NaN"
          - name: "boolean"
            value:
              type: ":integer"
              value: "10"
              unit: ":meter"
          - name: "textNumber"
            value:
              type: ":nothing"
          - name: "textInvalid"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":float"
              value: "31.4159265358979"
              unit: ":meter"
          - name: "tagNan"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":vector"
              x: "10"
              y: "20"
              z: "30"
              unit: ":meter"
          - name: "point"
            value:
              type: ":float"
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
module AtomicMathMatrixAI
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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
              type: ":nothing"
          - name: "integer"
            value:
              type: ":integer"
              value: "10"
          - name: "float"
            value:
              type: ":float"
              value: "10.3"
          - name: "percentage"
            value:
              type: ":float"
              value: "0.1"
          - name: "meter"
            value:
              type: ":integer"
              value: "10"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":integer"
              value: "1"
          - name: "textNumber"
            value:
              type: ":nothing"
          - name: "textInvalid"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "point"
            value:
              type: ":float"
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
module AtomicMathMatrixAJ
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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
          - name: "meter"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":nothing"
          - name: "textNumber"
            value:
              type: ":nothing"
          - name: "textInvalid"
            value:
              type: ":nothing"
          - name: "tagPi"
            value:
              type: ":nothing"
          - name: "tagNan"
            value:
              type: ":nothing"
          - name: "tagInfinity"
            value:
              type: ":nothing"
          - name: "vector"
            value:
              type: ":nothing"
          - name: "point"
            value:
              type: ":nothing"
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
module AtomicMathMatrixAK
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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
              type: ":nothing"
          - name: "integer"
            value:
              type: ":float"
              value: "NaN"
          - name: "float"
            value:
              type: ":float"
              value: "NaN"
          - name: "percentage"
            value:
              type: ":float"
              value: "NaN"
          - name: "meter"
            value:
              type: ":float"
              value: "NaN"
          - name: "boolean"
            value:
              type: ":float"
              value: "NaN"
          - name: "textNumber"
            value:
              type: ":nothing"
          - name: "textInvalid"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagNan"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "NaN"
          - name: "vector"
            value:
              type: ":float"
              value: "NaN"
          - name: "point"
            value:
              type: ":float"
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
module AtomicMathMatrixAL
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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
              type: ":nothing"
          - name: "integer"
            value:
              type: ":float"
              value: "31.4159265358979"
          - name: "float"
            value:
              type: ":float"
              value: "32.3584043319749"
          - name: "percentage"
            value:
              type: ":float"
              value: "0.314159265358979"
          - name: "meter"
            value:
              type: ":float"
              value: "31.4159265358979"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "textNumber"
            value:
              type: ":nothing"
          - name: "textInvalid"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":float"
              value: "9.86960440108936"
          - name: "tagNan"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":vector"
              x: "3.141592653589793"
              y: "6.28318530717959"
              z: "9.42477796076938"
          - name: "point"
            value:
              type: ":float"
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
module AtomicMathMatrixAM
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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
              type: ":nothing"
          - name: "integer"
            value:
              type: ":float"
              value: "NaN"
          - name: "float"
            value:
              type: ":float"
              value: "NaN"
          - name: "percentage"
            value:
              type: ":float"
              value: "NaN"
          - name: "meter"
            value:
              type: ":float"
              value: "NaN"
          - name: "boolean"
            value:
              type: ":float"
              value: "NaN"
          - name: "textNumber"
            value:
              type: ":nothing"
          - name: "textInvalid"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagNan"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "NaN"
          - name: "vector"
            value:
              type: ":float"
              value: "NaN"
          - name: "point"
            value:
              type: ":float"
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
module AtomicMathMatrixAN
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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
              type: ":nothing"
          - name: "integer"
            value:
              type: ":float"
              value: "Infinity"
          - name: "float"
            value:
              type: ":float"
              value: "Infinity"
          - name: "percentage"
            value:
              type: ":float"
              value: "Infinity"
          - name: "meter"
            value:
              type: ":float"
              value: "Infinity"
          - name: "boolean"
            value:
              type: ":float"
              value: "Infinity"
          - name: "textNumber"
            value:
              type: ":nothing"
          - name: "textInvalid"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":float"
              value: "Infinity"
          - name: "tagNan"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":float"
              value: "NaN"
          - name: "point"
            value:
              type: ":float"
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
module AtomicMathMatrixAO
on Start {
  let a be :vector(1, 2, 3)
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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
              type: ":nothing"
          - name: "integer"
            value:
              type: ":vector"
              x: "10"
              y: "20"
              z: "30"
          - name: "float"
            value:
              type: ":vector"
              x: "10.3"
              y: "20.6"
              z: "30.9"
          - name: "percentage"
            value:
              type: ":vector"
              x: "0.1"
              y: "0.2"
              z: "0.3"
          - name: "meter"
            value:
              type: ":vector"
              x: "10"
              y: "20"
              z: "30"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "textNumber"
            value:
              type: ":nothing"
          - name: "textInvalid"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":vector"
              x: "3.141592653589793"
              y: "6.28318530717959"
              z: "9.42477796076938"
          - name: "tagNan"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "NaN"
          - name: "vector"
            value:
              type: ":float"
              value: "NaN"
          - name: "point"
            value:
              type: ":float"
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
module AtomicMathMatrixAP
on Start {
  let a be :point(1, 2, 3)
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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
              type: ":nothing"
          - name: "integer"
            value:
              type: ":float"
              value: "NaN"
          - name: "float"
            value:
              type: ":float"
              value: "NaN"
          - name: "percentage"
            value:
              type: ":float"
              value: "NaN"
          - name: "meter"
            value:
              type: ":float"
              value: "NaN"
          - name: "boolean"
            value:
              type: ":float"
              value: "NaN"
          - name: "textNumber"
            value:
              type: ":nothing"
          - name: "textInvalid"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagNan"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "NaN"
          - name: "vector"
            value:
              type: ":float"
              value: "NaN"
          - name: "point"
            value:
              type: ":float"
              value: "NaN"
```
