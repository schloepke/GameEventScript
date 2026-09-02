---
formatVersion: 1
suiteId: "runtime.atomic.math-matrix.random-and-chance"
title: "Math Matrix — Random and Chance"
categories: [conformance]
---

# Math Matrix — Random and Chance

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers random bounds and chance evaluation across the portable value matrix.

---

## Test: random nothing

This runtime case exercises “random nothing” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0305
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4"]
sources:
  - name: "random nothing.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixKQ
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
  emit Done(nothing: random from a to vNothing, integer: random from a to vInteger, float: random from a to vFloat, percentage: random from a to vPercentage, meter: random from a to vMeter, boolean: random from a to vBoolean, textNumber: random from a to vTextNumber, textInvalid: random from a to vTextInvalid, tagPi: random from a to vTagPi, tagNan: random from a to vTagNan, tagInfinity: random from a to vTagInfinity, vector: random from a to vVector, point: random from a to vPoint)
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

## Test: random integer

This runtime case exercises “random integer” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0306
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4"]
sources:
  - name: "random integer.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixKR
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
  emit Done(nothing: random from a to vNothing, integer: random from a to vInteger, float: random from a to vFloat, percentage: random from a to vPercentage, meter: random from a to vMeter, boolean: random from a to vBoolean, textNumber: random from a to vTextNumber, textInvalid: random from a to vTextInvalid, tagPi: random from a to vTagPi, tagNan: random from a to vTagNan, tagInfinity: random from a to vTagInfinity, vector: random from a to vVector, point: random from a to vPoint)
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
              type: ":integer"
              value: "4"
          - name: "meter"
            value:
              type: ":float"
              value: "NaN"
          - name: "boolean"
            value:
              type: ":integer"
              value: "4"
          - name: "textNumber"
            value:
              type: ":nothing"
          - name: "textInvalid"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":integer"
              value: "4"
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

## Test: random float

This runtime case exercises “random float” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0307
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4"]
sources:
  - name: "random float.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixKS
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
  emit Done(nothing: random from a to vNothing, integer: random from a to vInteger, float: random from a to vFloat, percentage: random from a to vPercentage, meter: random from a to vMeter, boolean: random from a to vBoolean, textNumber: random from a to vTextNumber, textInvalid: random from a to vTextInvalid, tagPi: random from a to vTagPi, tagNan: random from a to vTagNan, tagInfinity: random from a to vTagInfinity, vector: random from a to vVector, point: random from a to vPoint)
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
              type: ":integer"
              value: "4"
          - name: "meter"
            value:
              type: ":float"
              value: "NaN"
          - name: "boolean"
            value:
              type: ":integer"
              value: "4"
          - name: "textNumber"
            value:
              type: ":nothing"
          - name: "textInvalid"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":integer"
              value: "4"
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

## Test: random percentage

This runtime case exercises “random percentage” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0308
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4"]
sources:
  - name: "random percentage.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixKT
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
  emit Done(nothing: random from a to vNothing, integer: random from a to vInteger, float: random from a to vFloat, percentage: random from a to vPercentage, meter: random from a to vMeter, boolean: random from a to vBoolean, textNumber: random from a to vTextNumber, textInvalid: random from a to vTextInvalid, tagPi: random from a to vTagPi, tagNan: random from a to vTagNan, tagInfinity: random from a to vTagInfinity, vector: random from a to vVector, point: random from a to vPoint)
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
              value: "4"
          - name: "float"
            value:
              type: ":integer"
              value: "4"
          - name: "percentage"
            value:
              type: ":float"
              value: "0.1"
          - name: "meter"
            value:
              type: ":float"
              value: "NaN"
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

## Test: random meter

This runtime case exercises “random meter” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0309
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4"]
sources:
  - name: "random meter.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixKU
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
  emit Done(nothing: random from a to vNothing, integer: random from a to vInteger, float: random from a to vFloat, percentage: random from a to vPercentage, meter: random from a to vMeter, boolean: random from a to vBoolean, textNumber: random from a to vTextNumber, textInvalid: random from a to vTextInvalid, tagPi: random from a to vTagPi, tagNan: random from a to vTagNan, tagInfinity: random from a to vTagInfinity, vector: random from a to vVector, point: random from a to vPoint)
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
              type: ":integer"
              value: "10"
              unit: ":meter"
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

## Test: random boolean

This runtime case exercises “random boolean” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0310
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4"]
sources:
  - name: "random boolean.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixKV
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
  emit Done(nothing: random from a to vNothing, integer: random from a to vInteger, float: random from a to vFloat, percentage: random from a to vPercentage, meter: random from a to vMeter, boolean: random from a to vBoolean, textNumber: random from a to vTextNumber, textInvalid: random from a to vTextInvalid, tagPi: random from a to vTagPi, tagNan: random from a to vTagNan, tagInfinity: random from a to vTagInfinity, vector: random from a to vVector, point: random from a to vPoint)
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
              value: "4"
          - name: "float"
            value:
              type: ":integer"
              value: "4"
          - name: "percentage"
            value:
              type: ":integer"
              value: "1"
          - name: "meter"
            value:
              type: ":float"
              value: "NaN"
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

## Test: random textNumber

This runtime case exercises “random textNumber” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0311
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4"]
sources:
  - name: "random textNumber.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixKW
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
  emit Done(nothing: random from a to vNothing, integer: random from a to vInteger, float: random from a to vFloat, percentage: random from a to vPercentage, meter: random from a to vMeter, boolean: random from a to vBoolean, textNumber: random from a to vTextNumber, textInvalid: random from a to vTextInvalid, tagPi: random from a to vTagPi, tagNan: random from a to vTagNan, tagInfinity: random from a to vTagInfinity, vector: random from a to vVector, point: random from a to vPoint)
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

## Test: random textInvalid

This runtime case exercises “random textInvalid” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0312
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4"]
sources:
  - name: "random textInvalid.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixKX
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
  emit Done(nothing: random from a to vNothing, integer: random from a to vInteger, float: random from a to vFloat, percentage: random from a to vPercentage, meter: random from a to vMeter, boolean: random from a to vBoolean, textNumber: random from a to vTextNumber, textInvalid: random from a to vTextInvalid, tagPi: random from a to vTagPi, tagNan: random from a to vTagNan, tagInfinity: random from a to vTagInfinity, vector: random from a to vVector, point: random from a to vPoint)
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

## Test: random tagPi

This runtime case exercises “random tagPi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0313
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4"]
sources:
  - name: "random tagPi.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixKY
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
  emit Done(nothing: random from a to vNothing, integer: random from a to vInteger, float: random from a to vFloat, percentage: random from a to vPercentage, meter: random from a to vMeter, boolean: random from a to vBoolean, textNumber: random from a to vTextNumber, textInvalid: random from a to vTextInvalid, tagPi: random from a to vTagPi, tagNan: random from a to vTagNan, tagInfinity: random from a to vTagInfinity, vector: random from a to vVector, point: random from a to vPoint)
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
              value: "4"
          - name: "float"
            value:
              type: ":integer"
              value: "4"
          - name: "percentage"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "meter"
            value:
              type: ":float"
              value: "NaN"
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
              value: "3.141592653589793"
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

## Test: random tagNan

This runtime case exercises “random tagNan” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0314
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4"]
sources:
  - name: "random tagNan.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixKZ
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
  emit Done(nothing: random from a to vNothing, integer: random from a to vInteger, float: random from a to vFloat, percentage: random from a to vPercentage, meter: random from a to vMeter, boolean: random from a to vBoolean, textNumber: random from a to vTextNumber, textInvalid: random from a to vTextInvalid, tagPi: random from a to vTagPi, tagNan: random from a to vTagNan, tagInfinity: random from a to vTagInfinity, vector: random from a to vVector, point: random from a to vPoint)
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

## Test: random tagInfinity

This runtime case exercises “random tagInfinity” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0315
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4"]
sources:
  - name: "random tagInfinity.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixLA
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
  emit Done(nothing: random from a to vNothing, integer: random from a to vInteger, float: random from a to vFloat, percentage: random from a to vPercentage, meter: random from a to vMeter, boolean: random from a to vBoolean, textNumber: random from a to vTextNumber, textInvalid: random from a to vTextInvalid, tagPi: random from a to vTagPi, tagNan: random from a to vTagNan, tagInfinity: random from a to vTagInfinity, vector: random from a to vVector, point: random from a to vPoint)
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

## Test: random vector

This runtime case exercises “random vector” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0316
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4"]
sources:
  - name: "random vector.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixLB
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
  emit Done(nothing: random from a to vNothing, integer: random from a to vInteger, float: random from a to vFloat, percentage: random from a to vPercentage, meter: random from a to vMeter, boolean: random from a to vBoolean, textNumber: random from a to vTextNumber, textInvalid: random from a to vTextInvalid, tagPi: random from a to vTagPi, tagNan: random from a to vTagNan, tagInfinity: random from a to vTagInfinity, vector: random from a to vVector, point: random from a to vPoint)
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

## Test: random point

This runtime case exercises “random point” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0317
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4", "4"]
sources:
  - name: "random point.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixLC
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
  emit Done(nothing: random from a to vNothing, integer: random from a to vInteger, float: random from a to vFloat, percentage: random from a to vPercentage, meter: random from a to vMeter, boolean: random from a to vBoolean, textNumber: random from a to vTextNumber, textInvalid: random from a to vTextInvalid, tagPi: random from a to vTagPi, tagNan: random from a to vTagNan, tagInfinity: random from a to vTagInfinity, vector: random from a to vVector, point: random from a to vPoint)
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

## Test: chance

This runtime case exercises “chance” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0318
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["0.2", "0.8", "0.2", "0.8", "0.2", "0.2", "0.8", "0.02", "0.04"]
sources:
  - name: "chance.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixLD
on Start {
  let vNothing be nothing
  let vIntegerZero be 0
  let vIntegerQuarter be 25
  let vIntegerHundred be 100
  let vFloatRatio be 0.25
  let vFloatPercent be 25.0
  let vPercentage be 25%
  let vNegative be -10%
  let vOver be 150%
  let vMeter be 10m
  let vTextInvalid be 'hello'
  let vTagInvalid be #hello
  let vTagPi be pi
  emit Done(nothing: chance vNothing, integerZero: chance vIntegerZero, integerHit: chance vIntegerQuarter, integerMiss: chance vIntegerQuarter, integerHundred: chance vIntegerHundred, floatRatioHit: chance vFloatRatio, floatRatioMiss: chance vFloatRatio, floatPercentHit: chance vFloatPercent, percentageHit: chance vPercentage, percentageMiss: chance vPercentage, negative: chance vNegative, over: chance vOver, meter: chance vMeter, textInvalid: chance vTextInvalid, tagInvalid: chance vTagInvalid, tagPiHit: chance vTagPi, tagPiMiss: chance vTagPi)
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
          - name: "integerZero"
            value:
              type: ":boolean"
              value: false
          - name: "integerHit"
            value:
              type: ":boolean"
              value: true
          - name: "integerMiss"
            value:
              type: ":boolean"
              value: false
          - name: "integerHundred"
            value:
              type: ":boolean"
              value: true
          - name: "floatRatioHit"
            value:
              type: ":boolean"
              value: true
          - name: "floatRatioMiss"
            value:
              type: ":boolean"
              value: false
          - name: "floatPercentHit"
            value:
              type: ":boolean"
              value: true
          - name: "percentageHit"
            value:
              type: ":boolean"
              value: true
          - name: "percentageMiss"
            value:
              type: ":boolean"
              value: false
          - name: "negative"
            value:
              type: ":boolean"
              value: false
          - name: "over"
            value:
              type: ":boolean"
              value: true
          - name: "meter"
            value:
              type: ":nothing"
          - name: "textInvalid"
            value:
              type: ":nothing"
          - name: "tagInvalid"
            value:
              type: ":nothing"
          - name: "tagPiHit"
            value:
              type: ":boolean"
              value: true
          - name: "tagPiMiss"
            value:
              type: ":boolean"
              value: false
```
