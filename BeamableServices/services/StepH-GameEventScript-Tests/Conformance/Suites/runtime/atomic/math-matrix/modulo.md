---
formatVersion: 1
suiteId: "runtime.atomic.math-matrix.modulo"
title: "Math Matrix — Modulo"
categories: [conformance]
---

# Math Matrix — Modulo

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers modulo behavior across the portable value matrix.

---

## Test: modulo nothing

This runtime case exercises “modulo nothing” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0071
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "modulo nothing.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbq
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
  emit Done(nothing: a mod vNothing, integer: a mod vInteger, float: a mod vFloat, percentage: a mod vPercentage, meter: a mod vMeter, boolean: a mod vBoolean, textNumber: a mod vTextNumber, textInvalid: a mod vTextInvalid, tagPi: a mod vTagPi, tagNan: a mod vTagNan, tagInfinity: a mod vTagInfinity, vector: a mod vVector, point: a mod vPoint)
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

## Test: modulo integer

This runtime case exercises “modulo integer” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0072
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "modulo integer.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbr
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
  emit Done(nothing: a mod vNothing, integer: a mod vInteger, float: a mod vFloat, percentage: a mod vPercentage, meter: a mod vMeter, boolean: a mod vBoolean, textNumber: a mod vTextNumber, textInvalid: a mod vTextInvalid, tagPi: a mod vTagPi, tagNan: a mod vTagNan, tagInfinity: a mod vTagInfinity, vector: a mod vVector, point: a mod vPoint)
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
              value: "0"
          - name: "float"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "percentage"
            value:
              type: ":Number.binary64"
              value: "0.0999999999999995"
          - name: "meter"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "boolean"
            value:
              type: ":Number.int64"
              value: "0"
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
              value: "0.575222039230621"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Number.int64"
              value: "10"
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

## Test: modulo float

This runtime case exercises “modulo float” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0073
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "modulo float.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbs
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
  emit Done(nothing: a mod vNothing, integer: a mod vInteger, float: a mod vFloat, percentage: a mod vPercentage, meter: a mod vMeter, boolean: a mod vBoolean, textNumber: a mod vTextNumber, textInvalid: a mod vTextInvalid, tagPi: a mod vTagPi, tagNan: a mod vTagNan, tagInfinity: a mod vTagInfinity, vector: a mod vVector, point: a mod vPoint)
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
              value: "0.300000000000001"
          - name: "float"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "percentage"
            value:
              type: ":Number.binary64"
              value: "1.387778780781e-16"
          - name: "meter"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "boolean"
            value:
              type: ":Number.binary64"
              value: "0.300000000000001"
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
              value: "0.875222039230621"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "10.3"
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

## Test: modulo percentage

This runtime case exercises “modulo percentage” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0074
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "modulo percentage.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbt
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
  emit Done(nothing: a mod vNothing, integer: a mod vInteger, float: a mod vFloat, percentage: a mod vPercentage, meter: a mod vMeter, boolean: a mod vBoolean, textNumber: a mod vTextNumber, textInvalid: a mod vTextInvalid, tagPi: a mod vTagPi, tagNan: a mod vTagNan, tagInfinity: a mod vTagInfinity, vector: a mod vVector, point: a mod vPoint)
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
              value: "0.1"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "0.1"
          - name: "percentage"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "meter"
            value:
              type: ":Number.binary64"
              value: "NaN"
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
              value: "0.1"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "0.1"
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

## Test: modulo meter

This runtime case exercises “modulo meter” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0075
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "modulo meter.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbu
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
  emit Done(nothing: a mod vNothing, integer: a mod vInteger, float: a mod vFloat, percentage: a mod vPercentage, meter: a mod vMeter, boolean: a mod vBoolean, textNumber: a mod vTextNumber, textInvalid: a mod vTextInvalid, tagPi: a mod vTagPi, tagNan: a mod vTagNan, tagInfinity: a mod vTagInfinity, vector: a mod vVector, point: a mod vPoint)
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
              type: ":Quantity.int64"
              value: "0"
              unit: ":meter"
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

## Test: modulo boolean

This runtime case exercises “modulo boolean” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0076
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "modulo boolean.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbv
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
  emit Done(nothing: a mod vNothing, integer: a mod vInteger, float: a mod vFloat, percentage: a mod vPercentage, meter: a mod vMeter, boolean: a mod vBoolean, textNumber: a mod vTextNumber, textInvalid: a mod vTextInvalid, tagPi: a mod vTagPi, tagNan: a mod vTagNan, tagInfinity: a mod vTagInfinity, vector: a mod vVector, point: a mod vPoint)
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
              type: ":Number.int64"
              value: "1"
          - name: "percentage"
            value:
              type: ":Number.binary64"
              value: "0.1"
          - name: "meter"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "boolean"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "textNumber"
            value:
              type: ":Nothing"
          - name: "textInvalid"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Number.int64"
              value: "1"
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

## Test: modulo textNumber

This runtime case exercises “modulo textNumber” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0077
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "modulo textNumber.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbw
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
  emit Done(nothing: a mod vNothing, integer: a mod vInteger, float: a mod vFloat, percentage: a mod vPercentage, meter: a mod vMeter, boolean: a mod vBoolean, textNumber: a mod vTextNumber, textInvalid: a mod vTextInvalid, tagPi: a mod vTagPi, tagNan: a mod vTagNan, tagInfinity: a mod vTagInfinity, vector: a mod vVector, point: a mod vPoint)
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

## Test: modulo textInvalid

This runtime case exercises “modulo textInvalid” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0078
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "modulo textInvalid.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbx
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
  emit Done(nothing: a mod vNothing, integer: a mod vInteger, float: a mod vFloat, percentage: a mod vPercentage, meter: a mod vMeter, boolean: a mod vBoolean, textNumber: a mod vTextNumber, textInvalid: a mod vTextInvalid, tagPi: a mod vTagPi, tagNan: a mod vTagNan, tagInfinity: a mod vTagInfinity, vector: a mod vVector, point: a mod vPoint)
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

## Test: modulo tagPi

This runtime case exercises “modulo tagPi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0079
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "modulo tagPi.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixby
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
  emit Done(nothing: a mod vNothing, integer: a mod vInteger, float: a mod vFloat, percentage: a mod vPercentage, meter: a mod vMeter, boolean: a mod vBoolean, textNumber: a mod vTextNumber, textInvalid: a mod vTextInvalid, tagPi: a mod vTagPi, tagNan: a mod vTagNan, tagInfinity: a mod vTagInfinity, vector: a mod vVector, point: a mod vPoint)
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
              type: ":Number.binary64"
              value: "0.0415926535897929"
          - name: "meter"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "boolean"
            value:
              type: ":Number.binary64"
              value: "0.141592653589793"
          - name: "textNumber"
            value:
              type: ":Nothing"
          - name: "textInvalid"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagPi"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
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

## Test: modulo tagNan

This runtime case exercises “modulo tagNan” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0080
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "modulo tagNan.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbz
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
  emit Done(nothing: a mod vNothing, integer: a mod vInteger, float: a mod vFloat, percentage: a mod vPercentage, meter: a mod vMeter, boolean: a mod vBoolean, textNumber: a mod vTextNumber, textInvalid: a mod vTextInvalid, tagPi: a mod vTagPi, tagNan: a mod vTagNan, tagInfinity: a mod vTagInfinity, vector: a mod vVector, point: a mod vPoint)
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

## Test: modulo tagInfinity

This runtime case exercises “modulo tagInfinity” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0081
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "modulo tagInfinity.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixca
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
  emit Done(nothing: a mod vNothing, integer: a mod vInteger, float: a mod vFloat, percentage: a mod vPercentage, meter: a mod vMeter, boolean: a mod vBoolean, textNumber: a mod vTextNumber, textInvalid: a mod vTextInvalid, tagPi: a mod vTagPi, tagNan: a mod vTagNan, tagInfinity: a mod vTagInfinity, vector: a mod vVector, point: a mod vPoint)
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

## Test: modulo vector

This runtime case exercises “modulo vector” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0082
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "modulo vector.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixcb
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
  emit Done(nothing: a mod vNothing, integer: a mod vInteger, float: a mod vFloat, percentage: a mod vPercentage, meter: a mod vMeter, boolean: a mod vBoolean, textNumber: a mod vTextNumber, textInvalid: a mod vTextInvalid, tagPi: a mod vTagPi, tagNan: a mod vTagNan, tagInfinity: a mod vTagInfinity, vector: a mod vVector, point: a mod vPoint)
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

## Test: modulo point

This runtime case exercises “modulo point” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0083
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "modulo point.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixcc
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
  emit Done(nothing: a mod vNothing, integer: a mod vInteger, float: a mod vFloat, percentage: a mod vPercentage, meter: a mod vMeter, boolean: a mod vBoolean, textNumber: a mod vTextNumber, textInvalid: a mod vTextInvalid, tagPi: a mod vTagPi, tagNan: a mod vTagNan, tagInfinity: a mod vTagInfinity, vector: a mod vVector, point: a mod vPoint)
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
