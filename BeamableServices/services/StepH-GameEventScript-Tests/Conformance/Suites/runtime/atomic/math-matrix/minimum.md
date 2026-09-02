---
formatVersion: 1
suiteId: "runtime.atomic.math-matrix.minimum"
title: "Math Matrix — Minimum"
categories: [conformance]
tags: [migrated-json-v1]
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
module AtomicMathMatrixDD
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
module AtomicMathMatrixDE
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
              type: ":nothing"
          - name: "integer"
            value:
              type: ":integer"
              value: "10"
          - name: "float"
            value:
              type: ":integer"
              value: "10"
          - name: "percentage"
            value:
              type: ":percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "textNumber"
            value:
              type: ":integer"
              value: "10"
          - name: "textInvalid"
            value:
              type: ":integer"
              value: "10"
          - name: "tagPi"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":integer"
              value: "10"
          - name: "tagInfinity"
            value:
              type: ":integer"
              value: "10"
          - name: "vector"
            value:
              type: ":integer"
              value: "10"
          - name: "point"
            value:
              type: ":integer"
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
module AtomicMathMatrixDF
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
              type: ":percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "textNumber"
            value:
              type: ":float"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":float"
              value: "10.3"
          - name: "tagPi"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":float"
              value: "10.3"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "10.3"
          - name: "vector"
            value:
              type: ":float"
              value: "10.3"
          - name: "point"
            value:
              type: ":float"
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
module AtomicMathMatrixDG
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
              type: ":nothing"
          - name: "integer"
            value:
              type: ":percentage"
              value: "0.1"
          - name: "float"
            value:
              type: ":percentage"
              value: "0.1"
          - name: "percentage"
            value:
              type: ":percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":percentage"
              value: "0.1"
          - name: "textNumber"
            value:
              type: ":percentage"
              value: "0.1"
          - name: "textInvalid"
            value:
              type: ":percentage"
              value: "0.1"
          - name: "tagPi"
            value:
              type: ":percentage"
              value: "0.1"
          - name: "tagNan"
            value:
              type: ":percentage"
              value: "0.1"
          - name: "tagInfinity"
            value:
              type: ":percentage"
              value: "0.1"
          - name: "vector"
            value:
              type: ":percentage"
              value: "0.1"
          - name: "point"
            value:
              type: ":percentage"
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
module AtomicMathMatrixDH
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
              type: ":integer"
              value: "10"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":nothing"
          - name: "textNumber"
            value:
              type: ":integer"
              value: "10"
              unit: ":meter"
          - name: "textInvalid"
            value:
              type: ":integer"
              value: "10"
              unit: ":meter"
          - name: "tagPi"
            value:
              type: ":nothing"
          - name: "tagNan"
            value:
              type: ":integer"
              value: "10"
              unit: ":meter"
          - name: "tagInfinity"
            value:
              type: ":nothing"
          - name: "vector"
            value:
              type: ":integer"
              value: "10"
              unit: ":meter"
          - name: "point"
            value:
              type: ":integer"
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
module AtomicMathMatrixDI
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
              type: ":nothing"
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
              type: ":percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "textNumber"
            value:
              type: ":text"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":text"
              value: "hello"
          - name: "tagPi"
            value:
              type: ":boolean"
              value: true
          - name: "tagNan"
            value:
              type: ":tag"
              value: "nan"
          - name: "tagInfinity"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "point"
            value:
              type: ":point"
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
module AtomicMathMatrixDJ
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
              type: ":percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":integer"
              value: "10"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":text"
              value: "10.3"
          - name: "textNumber"
            value:
              type: ":text"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":text"
              value: "10.3"
          - name: "tagPi"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":text"
              value: "10.3"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":text"
              value: "10.3"
          - name: "point"
            value:
              type: ":text"
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
module AtomicMathMatrixDK
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
              type: ":percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":integer"
              value: "10"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":text"
              value: "hello"
          - name: "textNumber"
            value:
              type: ":text"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":text"
              value: "hello"
          - name: "tagPi"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":text"
              value: "hello"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":text"
              value: "hello"
          - name: "point"
            value:
              type: ":text"
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
module AtomicMathMatrixDL
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
              type: ":nothing"
          - name: "integer"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "float"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "percentage"
            value:
              type: ":percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "textNumber"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "textInvalid"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "tagPi"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "vector"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "point"
            value:
              type: ":float"
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
module AtomicMathMatrixDM
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
              type: ":percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":integer"
              value: "10"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":tag"
              value: "nan"
          - name: "textNumber"
            value:
              type: ":text"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":text"
              value: "hello"
          - name: "tagPi"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":tag"
              value: "nan"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":tag"
              value: "nan"
          - name: "point"
            value:
              type: ":tag"
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
module AtomicMathMatrixDN
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
              type: ":percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":boolean"
              value: true
          - name: "textNumber"
            value:
              type: ":float"
              value: "Infinity"
          - name: "textInvalid"
            value:
              type: ":float"
              value: "Infinity"
          - name: "tagPi"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":float"
              value: "Infinity"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":float"
              value: "Infinity"
          - name: "point"
            value:
              type: ":float"
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
module AtomicMathMatrixDO
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
              type: ":percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":integer"
              value: "10"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "textNumber"
            value:
              type: ":text"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":text"
              value: "hello"
          - name: "tagPi"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":tag"
              value: "nan"
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
              type: ":vector"
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
module AtomicMathMatrixDP
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
              type: ":percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":integer"
              value: "10"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":point"
              x: "1"
              y: "2"
              z: "3"
          - name: "textNumber"
            value:
              type: ":text"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":text"
              value: "hello"
          - name: "tagPi"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":tag"
              value: "nan"
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
              type: ":point"
              x: "1"
              y: "2"
              z: "3"
```
