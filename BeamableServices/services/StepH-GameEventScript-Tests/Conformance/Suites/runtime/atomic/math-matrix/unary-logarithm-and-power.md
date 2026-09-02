---
formatVersion: 1
suiteId: "runtime.atomic.math-matrix.unary-logarithm-and-power"
title: "Math Matrix — Unary, Logarithm, and Power"
categories: [conformance]
---

# Math Matrix — Unary, Logarithm, and Power

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers unary numeric operations, natural logarithms, and exponentiation across the portable value matrix.

---

## Test: negate

This runtime case exercises “negate” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "negate.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixA
on Start {
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
  emit Done(nothing: -vNothing, integer: -vInteger, float: -vFloat, percentage: -vPercentage, meter: -vMeter, boolean: -vBoolean, textNumber: -vTextNumber, textInvalid: -vTextInvalid, tagPi: -vTagPi, tagNan: -vTagNan, tagInfinity: -vTagInfinity, vector: -vVector, point: -vPoint)
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
              value: "-10"
          - name: "float"
            value:
              type: ":float"
              value: "-10.3"
          - name: "percentage"
            value:
              type: ":percentage"
              value: "-0.1"
          - name: "meter"
            value:
              type: ":integer"
              value: "-10"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":integer"
              value: "-1"
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
              value: "-3.141592653589793"
          - name: "tagNan"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "-Infinity"
          - name: "vector"
            value:
              type: ":vector"
              x: "-1"
              y: "-2"
              z: "-3"
          - name: "point"
            value:
              type: ":float"
              value: "NaN"
```

---

## Test: abs

This runtime case exercises “abs” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "abs.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixB
on Start {
  let vNothing be nothing
  let vInteger be -10
  let vFloat be -10.3
  let vPercentage be -10%
  let vMeter be -10m
  let vBoolean be true
  let vTextNumber be '-10.3'
  let vTextInvalid be 'hello'
  let vTagPi be pi
  let vTagNan be #nan
  let vTagInfinity be infinity
  let vVector be :vector(-1, -2, -3)
  let vPoint be :point(1, 2, 3)
  emit Done(nothing: abs vNothing, integer: abs vInteger, float: abs vFloat, percentage: abs vPercentage, meter: abs vMeter, boolean: abs vBoolean, textNumber: abs vTextNumber, textInvalid: abs vTextInvalid, tagPi: abs vTagPi, tagNan: abs vTagNan, tagInfinity: abs vTagInfinity, vector: abs vVector, point: abs vPoint)
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
              type: ":float"
              value: "3.74165738677394"
          - name: "point"
            value:
              type: ":float"
              value: "NaN"
```

---

## Test: natural_log

This runtime case exercises “natural_log” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "natural_log.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixC
on Start {
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
  emit Done(nothing: ln vNothing, integer: ln vInteger, float: ln vFloat, percentage: ln vPercentage, meter: ln vMeter, boolean: ln vBoolean, textNumber: ln vTextNumber, textInvalid: ln vTextInvalid, tagPi: ln vTagPi, tagNan: ln vTagNan, tagInfinity: ln vTagInfinity, vector: ln vVector, point: ln vPoint)
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
              value: "2.30258509299405"
          - name: "float"
            value:
              type: ":float"
              value: "2.33214389523559"
          - name: "percentage"
            value:
              type: ":float"
              value: "-2.30258509299405"
          - name: "meter"
            value:
              type: ":float"
              value: "NaN"
          - name: "boolean"
            value:
              type: ":integer"
              value: "0"
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
              value: "1.1447298858494"
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

## Test: power nothing

This runtime case exercises “power nothing” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0097
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "power nothing.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixCQ
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
  emit Done(nothing: a ^ vNothing, integer: a ^ vInteger, float: a ^ vFloat, percentage: a ^ vPercentage, meter: a ^ vMeter, boolean: a ^ vBoolean, textNumber: a ^ vTextNumber, textInvalid: a ^ vTextInvalid, tagPi: a ^ vTagPi, tagNan: a ^ vTagNan, tagInfinity: a ^ vTagInfinity, vector: a ^ vVector, point: a ^ vPoint)
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

## Test: power integer

This runtime case exercises “power integer” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0098
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "power integer.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixCR
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
  emit Done(nothing: a ^ vNothing, integer: a ^ vInteger, float: a ^ vFloat, percentage: a ^ vPercentage, meter: a ^ vMeter, boolean: a ^ vBoolean, textNumber: a ^ vTextNumber, textInvalid: a ^ vTextInvalid, tagPi: a ^ vTagPi, tagNan: a ^ vTagNan, tagInfinity: a ^ vTagInfinity, vector: a ^ vVector, point: a ^ vPoint)
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
              value: "10000000000"
          - name: "float"
            value:
              type: ":float"
              value: "19952623149.6888"
          - name: "percentage"
            value:
              type: ":float"
              value: "1.25892541179417"
          - name: "meter"
            value:
              type: ":float"
              value: "NaN"
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
              value: "1385.45573136701"
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

## Test: power float

This runtime case exercises “power float” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0099
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "power float.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixCS
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
  emit Done(nothing: a ^ vNothing, integer: a ^ vInteger, float: a ^ vFloat, percentage: a ^ vPercentage, meter: a ^ vMeter, boolean: a ^ vBoolean, textNumber: a ^ vTextNumber, textInvalid: a ^ vTextInvalid, tagPi: a ^ vTagPi, tagNan: a ^ vTagNan, tagInfinity: a ^ vTagInfinity, vector: a ^ vVector, point: a ^ vPoint)
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
              value: "13439163793.4412"
          - name: "float"
            value:
              type: ":float"
              value: "27053497214.5545"
          - name: "percentage"
            value:
              type: ":float"
              value: "1.26265214970457"
          - name: "meter"
            value:
              type: ":float"
              value: "NaN"
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
              value: "1520.27440687653"
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

## Test: power percentage

This runtime case exercises “power percentage” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0100
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "power percentage.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixCT
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
  emit Done(nothing: a ^ vNothing, integer: a ^ vInteger, float: a ^ vFloat, percentage: a ^ vPercentage, meter: a ^ vMeter, boolean: a ^ vBoolean, textNumber: a ^ vTextNumber, textInvalid: a ^ vTextInvalid, tagPi: a ^ vTagPi, tagNan: a ^ vTagNan, tagInfinity: a ^ vTagInfinity, vector: a ^ vVector, point: a ^ vPoint)
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
              value: "1e-10"
          - name: "float"
            value:
              type: ":float"
              value: "5.01187233627272e-11"
          - name: "percentage"
            value:
              type: ":float"
              value: "0.794328234724281"
          - name: "meter"
            value:
              type: ":float"
              value: "NaN"
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
              value: "0.000721784159074728"
          - name: "tagNan"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":integer"
              value: "0"
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

## Test: power meter

This runtime case exercises “power meter” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0101
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "power meter.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixCU
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
  emit Done(nothing: a ^ vNothing, integer: a ^ vInteger, float: a ^ vFloat, percentage: a ^ vPercentage, meter: a ^ vMeter, boolean: a ^ vBoolean, textNumber: a ^ vTextNumber, textInvalid: a ^ vTextInvalid, tagPi: a ^ vTagPi, tagNan: a ^ vTagNan, tagInfinity: a ^ vTagInfinity, vector: a ^ vVector, point: a ^ vPoint)
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

## Test: power boolean

This runtime case exercises “power boolean” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0102
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "power boolean.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixCV
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
  emit Done(nothing: a ^ vNothing, integer: a ^ vInteger, float: a ^ vFloat, percentage: a ^ vPercentage, meter: a ^ vMeter, boolean: a ^ vBoolean, textNumber: a ^ vTextNumber, textInvalid: a ^ vTextInvalid, tagPi: a ^ vTagPi, tagNan: a ^ vTagNan, tagInfinity: a ^ vTagInfinity, vector: a ^ vVector, point: a ^ vPoint)
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
              type: ":integer"
              value: "1"
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
              type: ":integer"
              value: "1"
          - name: "tagNan"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":integer"
              value: "1"
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

## Test: power textNumber

This runtime case exercises “power textNumber” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0103
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "power textNumber.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixCW
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
  emit Done(nothing: a ^ vNothing, integer: a ^ vInteger, float: a ^ vFloat, percentage: a ^ vPercentage, meter: a ^ vMeter, boolean: a ^ vBoolean, textNumber: a ^ vTextNumber, textInvalid: a ^ vTextInvalid, tagPi: a ^ vTagPi, tagNan: a ^ vTagNan, tagInfinity: a ^ vTagInfinity, vector: a ^ vVector, point: a ^ vPoint)
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

## Test: power textInvalid

This runtime case exercises “power textInvalid” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0104
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "power textInvalid.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixCX
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
  emit Done(nothing: a ^ vNothing, integer: a ^ vInteger, float: a ^ vFloat, percentage: a ^ vPercentage, meter: a ^ vMeter, boolean: a ^ vBoolean, textNumber: a ^ vTextNumber, textInvalid: a ^ vTextInvalid, tagPi: a ^ vTagPi, tagNan: a ^ vTagNan, tagInfinity: a ^ vTagInfinity, vector: a ^ vVector, point: a ^ vPoint)
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

## Test: power tagPi

This runtime case exercises “power tagPi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0105
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "power tagPi.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixCY
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
  emit Done(nothing: a ^ vNothing, integer: a ^ vInteger, float: a ^ vFloat, percentage: a ^ vPercentage, meter: a ^ vMeter, boolean: a ^ vBoolean, textNumber: a ^ vTextNumber, textInvalid: a ^ vTextInvalid, tagPi: a ^ vTagPi, tagNan: a ^ vTagNan, tagInfinity: a ^ vTagInfinity, vector: a ^ vVector, point: a ^ vPoint)
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
              value: "93648.047476083"
          - name: "float"
            value:
              type: ":float"
              value: "132021.203896669"
          - name: "percentage"
            value:
              type: ":float"
              value: "1.12128235323186"
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
              value: "36.4621596072079"
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

## Test: power tagNan

This runtime case exercises “power tagNan” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0106
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "power tagNan.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixCZ
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
  emit Done(nothing: a ^ vNothing, integer: a ^ vInteger, float: a ^ vFloat, percentage: a ^ vPercentage, meter: a ^ vMeter, boolean: a ^ vBoolean, textNumber: a ^ vTextNumber, textInvalid: a ^ vTextInvalid, tagPi: a ^ vTagPi, tagNan: a ^ vTagNan, tagInfinity: a ^ vTagInfinity, vector: a ^ vVector, point: a ^ vPoint)
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

## Test: power tagInfinity

This runtime case exercises “power tagInfinity” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0107
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "power tagInfinity.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixDA
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
  emit Done(nothing: a ^ vNothing, integer: a ^ vInteger, float: a ^ vFloat, percentage: a ^ vPercentage, meter: a ^ vMeter, boolean: a ^ vBoolean, textNumber: a ^ vTextNumber, textInvalid: a ^ vTextInvalid, tagPi: a ^ vTagPi, tagNan: a ^ vTagNan, tagInfinity: a ^ vTagInfinity, vector: a ^ vVector, point: a ^ vPoint)
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
              value: "NaN"
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

## Test: power vector

This runtime case exercises “power vector” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0108
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "power vector.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixDB
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
  emit Done(nothing: a ^ vNothing, integer: a ^ vInteger, float: a ^ vFloat, percentage: a ^ vPercentage, meter: a ^ vMeter, boolean: a ^ vBoolean, textNumber: a ^ vTextNumber, textInvalid: a ^ vTextInvalid, tagPi: a ^ vTagPi, tagNan: a ^ vTagNan, tagInfinity: a ^ vTagInfinity, vector: a ^ vVector, point: a ^ vPoint)
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

## Test: power point

This runtime case exercises “power point” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0109
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "power point.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixDC
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
  emit Done(nothing: a ^ vNothing, integer: a ^ vInteger, float: a ^ vFloat, percentage: a ^ vPercentage, meter: a ^ vMeter, boolean: a ^ vBoolean, textNumber: a ^ vTextNumber, textInvalid: a ^ vTextInvalid, tagPi: a ^ vTagPi, tagNan: a ^ vTagNan, tagInfinity: a ^ vTagInfinity, vector: a ^ vVector, point: a ^ vPoint)
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
