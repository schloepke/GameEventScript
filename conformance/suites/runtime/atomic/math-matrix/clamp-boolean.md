---
formatVersion: 1
suiteId: "runtime.atomic.math-matrix.clamp-boolean"
title: "Math Matrix — Clamp Boolean Values"
categories: [conformance]
---

# Math Matrix — Clamp Boolean Values

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers clamp behavior when the value being clamped is boolean.

---

## Test: clamp boolean nothing

This runtime case exercises “clamp boolean nothing” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0201
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp boolean nothing.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixgq
on Start {
  let a be true
  let b be nothing
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
  emit Done(nothing: clamp a between b and vNothing, integer: clamp a between b and vInteger, float: clamp a between b and vFloat, percentage: clamp a between b and vPercentage, meter: clamp a between b and vMeter, boolean: clamp a between b and vBoolean, textNumber: clamp a between b and vTextNumber, textInvalid: clamp a between b and vTextInvalid, tagPi: clamp a between b and vTagPi, tagNan: clamp a between b and vTagNan, tagInfinity: clamp a between b and vTagInfinity, vector: clamp a between b and vVector, point: clamp a between b and vPoint)
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

## Test: clamp boolean integer

This runtime case exercises “clamp boolean integer” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0202
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp boolean integer.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixgr
on Start {
  let a be true
  let b be 10
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
  emit Done(nothing: clamp a between b and vNothing, integer: clamp a between b and vInteger, float: clamp a between b and vFloat, percentage: clamp a between b and vPercentage, meter: clamp a between b and vMeter, boolean: clamp a between b and vBoolean, textNumber: clamp a between b and vTextNumber, textInvalid: clamp a between b and vTextInvalid, tagPi: clamp a between b and vTagPi, tagNan: clamp a between b and vTagNan, tagInfinity: clamp a between b and vTagInfinity, vector: clamp a between b and vVector, point: clamp a between b and vPoint)
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
              type: ":Number.int64"
              value: "1"
          - name: "meter"
            value:
              type: ":Number.binary64"
              value: "NaN"
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

## Test: clamp boolean float

This runtime case exercises “clamp boolean float” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0203
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp boolean float.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixgs
on Start {
  let a be true
  let b be 10.3
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
  emit Done(nothing: clamp a between b and vNothing, integer: clamp a between b and vInteger, float: clamp a between b and vFloat, percentage: clamp a between b and vPercentage, meter: clamp a between b and vMeter, boolean: clamp a between b and vBoolean, textNumber: clamp a between b and vTextNumber, textInvalid: clamp a between b and vTextInvalid, tagPi: clamp a between b and vTagPi, tagNan: clamp a between b and vTagNan, tagInfinity: clamp a between b and vTagInfinity, vector: clamp a between b and vVector, point: clamp a between b and vPoint)
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
              type: ":Number.int64"
              value: "1"
          - name: "meter"
            value:
              type: ":Number.binary64"
              value: "NaN"
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

## Test: clamp boolean percentage

This runtime case exercises “clamp boolean percentage” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0204
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp boolean percentage.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixgt
on Start {
  let a be true
  let b be 10%
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
  emit Done(nothing: clamp a between b and vNothing, integer: clamp a between b and vInteger, float: clamp a between b and vFloat, percentage: clamp a between b and vPercentage, meter: clamp a between b and vMeter, boolean: clamp a between b and vBoolean, textNumber: clamp a between b and vTextNumber, textInvalid: clamp a between b and vTextInvalid, tagPi: clamp a between b and vTagPi, tagNan: clamp a between b and vTagNan, tagInfinity: clamp a between b and vTagInfinity, vector: clamp a between b and vVector, point: clamp a between b and vPoint)
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

## Test: clamp boolean meter

This runtime case exercises “clamp boolean meter” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0205
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp boolean meter.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixgu
on Start {
  let a be true
  let b be 10m
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
  emit Done(nothing: clamp a between b and vNothing, integer: clamp a between b and vInteger, float: clamp a between b and vFloat, percentage: clamp a between b and vPercentage, meter: clamp a between b and vMeter, boolean: clamp a between b and vBoolean, textNumber: clamp a between b and vTextNumber, textInvalid: clamp a between b and vTextInvalid, tagPi: clamp a between b and vTagPi, tagNan: clamp a between b and vTagNan, tagInfinity: clamp a between b and vTagInfinity, vector: clamp a between b and vVector, point: clamp a between b and vPoint)
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

## Test: clamp boolean boolean

This runtime case exercises “clamp boolean boolean” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0206
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp boolean boolean.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixgv
on Start {
  let a be true
  let b be true
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
  emit Done(nothing: clamp a between b and vNothing, integer: clamp a between b and vInteger, float: clamp a between b and vFloat, percentage: clamp a between b and vPercentage, meter: clamp a between b and vMeter, boolean: clamp a between b and vBoolean, textNumber: clamp a between b and vTextNumber, textInvalid: clamp a between b and vTextInvalid, tagPi: clamp a between b and vTagPi, tagNan: clamp a between b and vTagNan, tagInfinity: clamp a between b and vTagInfinity, vector: clamp a between b and vVector, point: clamp a between b and vPoint)
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
              type: ":Number.int64"
              value: "1"
          - name: "meter"
            value:
              type: ":Number.binary64"
              value: "NaN"
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

## Test: clamp boolean textNumber

This runtime case exercises “clamp boolean textNumber” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0207
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp boolean textNumber.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixgw
on Start {
  let a be true
  let b be '10.3'
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
  emit Done(nothing: clamp a between b and vNothing, integer: clamp a between b and vInteger, float: clamp a between b and vFloat, percentage: clamp a between b and vPercentage, meter: clamp a between b and vMeter, boolean: clamp a between b and vBoolean, textNumber: clamp a between b and vTextNumber, textInvalid: clamp a between b and vTextInvalid, tagPi: clamp a between b and vTagPi, tagNan: clamp a between b and vTagNan, tagInfinity: clamp a between b and vTagInfinity, vector: clamp a between b and vVector, point: clamp a between b and vPoint)
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

## Test: clamp boolean textInvalid

This runtime case exercises “clamp boolean textInvalid” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0208
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp boolean textInvalid.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixgx
on Start {
  let a be true
  let b be 'hello'
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
  emit Done(nothing: clamp a between b and vNothing, integer: clamp a between b and vInteger, float: clamp a between b and vFloat, percentage: clamp a between b and vPercentage, meter: clamp a between b and vMeter, boolean: clamp a between b and vBoolean, textNumber: clamp a between b and vTextNumber, textInvalid: clamp a between b and vTextInvalid, tagPi: clamp a between b and vTagPi, tagNan: clamp a between b and vTagNan, tagInfinity: clamp a between b and vTagInfinity, vector: clamp a between b and vVector, point: clamp a between b and vPoint)
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

## Test: clamp boolean tagPi

This runtime case exercises “clamp boolean tagPi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0209
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp boolean tagPi.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixgy
on Start {
  let a be true
  let b be pi
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
  emit Done(nothing: clamp a between b and vNothing, integer: clamp a between b and vInteger, float: clamp a between b and vFloat, percentage: clamp a between b and vPercentage, meter: clamp a between b and vMeter, boolean: clamp a between b and vBoolean, textNumber: clamp a between b and vTextNumber, textInvalid: clamp a between b and vTextInvalid, tagPi: clamp a between b and vTagPi, tagNan: clamp a between b and vTagNan, tagInfinity: clamp a between b and vTagInfinity, vector: clamp a between b and vVector, point: clamp a between b and vPoint)
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
              type: ":Number.int64"
              value: "1"
          - name: "meter"
            value:
              type: ":Number.binary64"
              value: "NaN"
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

## Test: clamp boolean tagNan

This runtime case exercises “clamp boolean tagNan” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0210
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp boolean tagNan.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixgz
on Start {
  let a be true
  let b be #nan
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
  emit Done(nothing: clamp a between b and vNothing, integer: clamp a between b and vInteger, float: clamp a between b and vFloat, percentage: clamp a between b and vPercentage, meter: clamp a between b and vMeter, boolean: clamp a between b and vBoolean, textNumber: clamp a between b and vTextNumber, textInvalid: clamp a between b and vTextInvalid, tagPi: clamp a between b and vTagPi, tagNan: clamp a between b and vTagNan, tagInfinity: clamp a between b and vTagInfinity, vector: clamp a between b and vVector, point: clamp a between b and vPoint)
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

## Test: clamp boolean tagInfinity

This runtime case exercises “clamp boolean tagInfinity” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0211
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp boolean tagInfinity.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixha
on Start {
  let a be true
  let b be infinity
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
  emit Done(nothing: clamp a between b and vNothing, integer: clamp a between b and vInteger, float: clamp a between b and vFloat, percentage: clamp a between b and vPercentage, meter: clamp a between b and vMeter, boolean: clamp a between b and vBoolean, textNumber: clamp a between b and vTextNumber, textInvalid: clamp a between b and vTextInvalid, tagPi: clamp a between b and vTagPi, tagNan: clamp a between b and vTagNan, tagInfinity: clamp a between b and vTagInfinity, vector: clamp a between b and vVector, point: clamp a between b and vPoint)
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
              type: ":Number.int64"
              value: "1"
          - name: "meter"
            value:
              type: ":Number.binary64"
              value: "NaN"
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
              type: ":Number.binary64"
              value: "NaN"
          - name: "point"
            value:
              type: ":Number.binary64"
              value: "NaN"
```

---

## Test: clamp boolean vector

This runtime case exercises “clamp boolean vector” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0212
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp boolean vector.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixhb
on Start {
  let a be true
  let b be :Vector(1, 2, 3)
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
  emit Done(nothing: clamp a between b and vNothing, integer: clamp a between b and vInteger, float: clamp a between b and vFloat, percentage: clamp a between b and vPercentage, meter: clamp a between b and vMeter, boolean: clamp a between b and vBoolean, textNumber: clamp a between b and vTextNumber, textInvalid: clamp a between b and vTextInvalid, tagPi: clamp a between b and vTagPi, tagNan: clamp a between b and vTagNan, tagInfinity: clamp a between b and vTagInfinity, vector: clamp a between b and vVector, point: clamp a between b and vPoint)
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

## Test: clamp boolean point

This runtime case exercises “clamp boolean point” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0213
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp boolean point.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixhc
on Start {
  let a be true
  let b be :Point(1, 2, 3)
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
  emit Done(nothing: clamp a between b and vNothing, integer: clamp a between b and vInteger, float: clamp a between b and vFloat, percentage: clamp a between b and vPercentage, meter: clamp a between b and vMeter, boolean: clamp a between b and vBoolean, textNumber: clamp a between b and vTextNumber, textInvalid: clamp a between b and vTextInvalid, tagPi: clamp a between b and vTagPi, tagNan: clamp a between b and vTagNan, tagInfinity: clamp a between b and vTagInfinity, vector: clamp a between b and vVector, point: clamp a between b and vPoint)
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
