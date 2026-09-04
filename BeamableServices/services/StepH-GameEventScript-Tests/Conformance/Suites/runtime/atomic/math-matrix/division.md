---
formatVersion: 1
suiteId: "runtime.atomic.math-matrix.division"
title: "Math Matrix — Division"
categories: [conformance]
---

# Math Matrix — Division

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers ordinary division across primitive, measured, and spatial values.

---

## Test: divide nothing

This runtime case exercises “divide nothing” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0045
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "divide nothing.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixaq
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
  emit Done(nothing: a / vNothing, integer: a / vInteger, float: a / vFloat, percentage: a / vPercentage, meter: a / vMeter, boolean: a / vBoolean, textNumber: a / vTextNumber, textInvalid: a / vTextInvalid, tagPi: a / vTagPi, tagNan: a / vTagNan, tagInfinity: a / vTagInfinity, vector: a / vVector, point: a / vPoint)
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

## Test: divide integer

This runtime case exercises “divide integer” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0046
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "divide integer.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixar
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
  emit Done(nothing: a / vNothing, integer: a / vInteger, float: a / vFloat, percentage: a / vPercentage, meter: a / vMeter, boolean: a / vBoolean, textNumber: a / vTextNumber, textInvalid: a / vTextInvalid, tagPi: a / vTagPi, tagNan: a / vTagNan, tagInfinity: a / vTagInfinity, vector: a / vVector, point: a / vPoint)
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
              value: "0.970873786407767"
          - name: "percentage"
            value:
              type: ":Number.int64"
              value: "100"
          - name: "meter"
            value:
              type: ":Number.binary64"
              value: "NaN"
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
              value: "3.18309886183791"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Number.int64"
              value: "0"
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

## Test: divide float

This runtime case exercises “divide float” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0047
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "divide float.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixas
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
  emit Done(nothing: a / vNothing, integer: a / vInteger, float: a / vFloat, percentage: a / vPercentage, meter: a / vMeter, boolean: a / vBoolean, textNumber: a / vTextNumber, textInvalid: a / vTextInvalid, tagPi: a / vTagPi, tagNan: a / vTagNan, tagInfinity: a / vTagInfinity, vector: a / vVector, point: a / vPoint)
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
              value: "1.03"
          - name: "float"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "percentage"
            value:
              type: ":Number.int64"
              value: "103"
          - name: "meter"
            value:
              type: ":Number.binary64"
              value: "NaN"
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
              value: "3.27859182769304"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Number.int64"
              value: "0"
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

## Test: divide percentage

This runtime case exercises “divide percentage” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0048
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "divide percentage.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixat
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
  emit Done(nothing: a / vNothing, integer: a / vInteger, float: a / vFloat, percentage: a / vPercentage, meter: a / vMeter, boolean: a / vBoolean, textNumber: a / vTextNumber, textInvalid: a / vTextInvalid, tagPi: a / vTagPi, tagNan: a / vTagNan, tagInfinity: a / vTagInfinity, vector: a / vVector, point: a / vPoint)
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
              value: "0.01"
          - name: "float"
            value:
              type: ":Percentage"
              value: "0.00970873786407767"
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
              type: ":Percentage"
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
              type: ":Percentage"
              value: "0.0318309886183791"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Percentage"
              value: "0"
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

## Test: divide meter

This runtime case exercises “divide meter” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0049
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "divide meter.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixau
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
  emit Done(nothing: a / vNothing, integer: a / vInteger, float: a / vFloat, percentage: a / vPercentage, meter: a / vMeter, boolean: a / vBoolean, textNumber: a / vTextNumber, textInvalid: a / vTextInvalid, tagPi: a / vTagPi, tagNan: a / vTagNan, tagInfinity: a / vTagInfinity, vector: a / vVector, point: a / vPoint)
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
              value: "1"
              unit: ":meter"
          - name: "float"
            value:
              type: ":Quantity.binary64"
              value: "0.970873786407767"
              unit: ":meter"
          - name: "percentage"
            value:
              type: ":Quantity.int64"
              value: "100"
              unit: ":meter"
          - name: "meter"
            value:
              type: ":Number.int64"
              value: "1"
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
              value: "3.18309886183791"
              unit: ":meter"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Quantity.int64"
              value: "0"
              unit: ":meter"
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

## Test: divide boolean

This runtime case exercises “divide boolean” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0050
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "divide boolean.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixav
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
  emit Done(nothing: a / vNothing, integer: a / vInteger, float: a / vFloat, percentage: a / vPercentage, meter: a / vMeter, boolean: a / vBoolean, textNumber: a / vTextNumber, textInvalid: a / vTextInvalid, tagPi: a / vTagPi, tagNan: a / vTagNan, tagInfinity: a / vTagInfinity, vector: a / vVector, point: a / vPoint)
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
              value: "0.0970873786407767"
          - name: "percentage"
            value:
              type: ":Number.int64"
              value: "10"
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
              value: "0.318309886183791"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Number.int64"
              value: "0"
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

## Test: divide textNumber

This runtime case exercises “divide textNumber” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0051
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "divide textNumber.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixaw
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
  emit Done(nothing: a / vNothing, integer: a / vInteger, float: a / vFloat, percentage: a / vPercentage, meter: a / vMeter, boolean: a / vBoolean, textNumber: a / vTextNumber, textInvalid: a / vTextInvalid, tagPi: a / vTagPi, tagNan: a / vTagNan, tagInfinity: a / vTagInfinity, vector: a / vVector, point: a / vPoint)
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

## Test: divide textInvalid

This runtime case exercises “divide textInvalid” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0052
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "divide textInvalid.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixax
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
  emit Done(nothing: a / vNothing, integer: a / vInteger, float: a / vFloat, percentage: a / vPercentage, meter: a / vMeter, boolean: a / vBoolean, textNumber: a / vTextNumber, textInvalid: a / vTextInvalid, tagPi: a / vTagPi, tagNan: a / vTagNan, tagInfinity: a / vTagInfinity, vector: a / vVector, point: a / vPoint)
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

## Test: divide tagPi

This runtime case exercises “divide tagPi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0053
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "divide tagPi.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixay
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
  emit Done(nothing: a / vNothing, integer: a / vInteger, float: a / vFloat, percentage: a / vPercentage, meter: a / vMeter, boolean: a / vBoolean, textNumber: a / vTextNumber, textInvalid: a / vTextInvalid, tagPi: a / vTagPi, tagNan: a / vTagNan, tagInfinity: a / vTagInfinity, vector: a / vVector, point: a / vPoint)
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
              value: "0.314159265358979"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "0.305008995494155"
          - name: "percentage"
            value:
              type: ":Number.binary64"
              value: "31.4159265358979"
          - name: "meter"
            value:
              type: ":Number.binary64"
              value: "NaN"
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
              type: ":Number.int64"
              value: "1"
          - name: "tagNan"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":Number.int64"
              value: "0"
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

## Test: divide tagNan

This runtime case exercises “divide tagNan” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0054
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "divide tagNan.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixaz
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
  emit Done(nothing: a / vNothing, integer: a / vInteger, float: a / vFloat, percentage: a / vPercentage, meter: a / vMeter, boolean: a / vBoolean, textNumber: a / vTextNumber, textInvalid: a / vTextInvalid, tagPi: a / vTagPi, tagNan: a / vTagNan, tagInfinity: a / vTagInfinity, vector: a / vVector, point: a / vPoint)
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

## Test: divide tagInfinity

This runtime case exercises “divide tagInfinity” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0055
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "divide tagInfinity.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixba
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
  emit Done(nothing: a / vNothing, integer: a / vInteger, float: a / vFloat, percentage: a / vPercentage, meter: a / vMeter, boolean: a / vBoolean, textNumber: a / vTextNumber, textInvalid: a / vTextInvalid, tagPi: a / vTagPi, tagNan: a / vTagNan, tagInfinity: a / vTagInfinity, vector: a / vVector, point: a / vPoint)
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
              type: ":Number.binary64"
              value: "NaN"
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

## Test: divide vector

This runtime case exercises “divide vector” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0056
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "divide vector.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbb
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
  emit Done(nothing: a / vNothing, integer: a / vInteger, float: a / vFloat, percentage: a / vPercentage, meter: a / vMeter, boolean: a / vBoolean, textNumber: a / vTextNumber, textInvalid: a / vTextInvalid, tagPi: a / vTagPi, tagNan: a / vTagNan, tagInfinity: a / vTagInfinity, vector: a / vVector, point: a / vPoint)
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
              x: "0.1"
              y: "0.2"
              z: "0.3"
          - name: "float"
            value:
              type: ":Vector"
              x: "0.0970873786407767"
              y: "0.194174757281553"
              z: "0.29126213592233"
          - name: "percentage"
            value:
              type: ":Vector"
              x: "10"
              y: "20"
              z: "30"
          - name: "meter"
            value:
              type: ":Number.binary64"
              value: "NaN"
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
              x: "0.318309886183791"
              y: "0.636619772367581"
              z: "0.954929658551372"
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

## Test: divide point

This runtime case exercises “divide point” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0057
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "divide point.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbc
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
  emit Done(nothing: a / vNothing, integer: a / vInteger, float: a / vFloat, percentage: a / vPercentage, meter: a / vMeter, boolean: a / vBoolean, textNumber: a / vTextNumber, textInvalid: a / vTextInvalid, tagPi: a / vTagPi, tagNan: a / vTagNan, tagInfinity: a / vTagInfinity, vector: a / vVector, point: a / vPoint)
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
