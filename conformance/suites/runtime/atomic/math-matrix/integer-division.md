---
formatVersion: 1
suiteId: "runtime.atomic.math-matrix.integer-division"
title: "Math Matrix — Integer Division"
categories: [conformance]
---

# Math Matrix — Integer Division

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers integer division and its coercion behavior across the portable value matrix.

---

## Test: integerDivide nothing

This runtime case exercises “integerDivide nothing” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0058
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "integerDivide nothing.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbd
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
  emit Done(nothing: a div vNothing, integer: a div vInteger, float: a div vFloat, percentage: a div vPercentage, meter: a div vMeter, boolean: a div vBoolean, textNumber: a div vTextNumber, textInvalid: a div vTextInvalid, tagPi: a div vTagPi, tagNan: a div vTagNan, tagInfinity: a div vTagInfinity, vector: a div vVector, point: a div vPoint)
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

## Test: integerDivide integer

This runtime case exercises “integerDivide integer” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0059
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "integerDivide integer.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbe
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
  emit Done(nothing: a div vNothing, integer: a div vInteger, float: a div vFloat, percentage: a div vPercentage, meter: a div vMeter, boolean: a div vBoolean, textNumber: a div vTextNumber, textInvalid: a div vTextInvalid, tagPi: a div vTagPi, tagNan: a div vTagNan, tagInfinity: a div vTagInfinity, vector: a div vVector, point: a div vPoint)
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
              value: "0"
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
              type: ":Number.int64"
              value: "3"
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

## Test: integerDivide float

This runtime case exercises “integerDivide float” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0060
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "integerDivide float.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbf
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
  emit Done(nothing: a div vNothing, integer: a div vInteger, float: a div vFloat, percentage: a div vPercentage, meter: a div vMeter, boolean: a div vBoolean, textNumber: a div vTextNumber, textInvalid: a div vTextInvalid, tagPi: a div vTagPi, tagNan: a div vTagNan, tagInfinity: a div vTagInfinity, vector: a div vVector, point: a div vPoint)
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
              value: "103"
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
              type: ":Number.int64"
              value: "3"
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

## Test: integerDivide percentage

This runtime case exercises “integerDivide percentage” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0061
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "integerDivide percentage.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbg
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
  emit Done(nothing: a div vNothing, integer: a div vInteger, float: a div vFloat, percentage: a div vPercentage, meter: a div vMeter, boolean: a div vBoolean, textNumber: a div vTextNumber, textInvalid: a div vTextInvalid, tagPi: a div vTagPi, tagNan: a div vTagNan, tagInfinity: a div vTagInfinity, vector: a div vVector, point: a div vPoint)
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
              value: "0"
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
              value: "0"
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

## Test: integerDivide meter

This runtime case exercises “integerDivide meter” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0062
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "integerDivide meter.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbh
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
  emit Done(nothing: a div vNothing, integer: a div vInteger, float: a div vFloat, percentage: a div vPercentage, meter: a div vMeter, boolean: a div vBoolean, textNumber: a div vTextNumber, textInvalid: a div vTextInvalid, tagPi: a div vTagPi, tagNan: a div vTagNan, tagInfinity: a div vTagInfinity, vector: a div vVector, point: a div vPoint)
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
              type: ":Quantity.int64"
              value: "0"
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
              type: ":Quantity.int64"
              value: "3"
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

## Test: integerDivide boolean

This runtime case exercises “integerDivide boolean” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0063
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "integerDivide boolean.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbi
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
  emit Done(nothing: a div vNothing, integer: a div vInteger, float: a div vFloat, percentage: a div vPercentage, meter: a div vMeter, boolean: a div vBoolean, textNumber: a div vTextNumber, textInvalid: a div vTextInvalid, tagPi: a div vTagPi, tagNan: a div vTagNan, tagInfinity: a div vTagInfinity, vector: a div vVector, point: a div vPoint)
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
              value: "0"
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
              type: ":Number.int64"
              value: "0"
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

## Test: integerDivide textNumber

This runtime case exercises “integerDivide textNumber” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0064
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "integerDivide textNumber.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbj
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
  emit Done(nothing: a div vNothing, integer: a div vInteger, float: a div vFloat, percentage: a div vPercentage, meter: a div vMeter, boolean: a div vBoolean, textNumber: a div vTextNumber, textInvalid: a div vTextInvalid, tagPi: a div vTagPi, tagNan: a div vTagNan, tagInfinity: a div vTagInfinity, vector: a div vVector, point: a div vPoint)
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

## Test: integerDivide textInvalid

This runtime case exercises “integerDivide textInvalid” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0065
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "integerDivide textInvalid.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbk
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
  emit Done(nothing: a div vNothing, integer: a div vInteger, float: a div vFloat, percentage: a div vPercentage, meter: a div vMeter, boolean: a div vBoolean, textNumber: a div vTextNumber, textInvalid: a div vTextInvalid, tagPi: a div vTagPi, tagNan: a div vTagNan, tagInfinity: a div vTagInfinity, vector: a div vVector, point: a div vPoint)
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

## Test: integerDivide tagPi

This runtime case exercises “integerDivide tagPi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0066
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "integerDivide tagPi.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbl
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
  emit Done(nothing: a div vNothing, integer: a div vInteger, float: a div vFloat, percentage: a div vPercentage, meter: a div vMeter, boolean: a div vBoolean, textNumber: a div vTextNumber, textInvalid: a div vTextInvalid, tagPi: a div vTagPi, tagNan: a div vTagNan, tagInfinity: a div vTagInfinity, vector: a div vVector, point: a div vPoint)
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
              value: "0"
          - name: "percentage"
            value:
              type: ":Number.int64"
              value: "31"
          - name: "meter"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "boolean"
            value:
              type: ":Number.int64"
              value: "3"
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

## Test: integerDivide tagNan

This runtime case exercises “integerDivide tagNan” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0067
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "integerDivide tagNan.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbm
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
  emit Done(nothing: a div vNothing, integer: a div vInteger, float: a div vFloat, percentage: a div vPercentage, meter: a div vMeter, boolean: a div vBoolean, textNumber: a div vTextNumber, textInvalid: a div vTextInvalid, tagPi: a div vTagPi, tagNan: a div vTagNan, tagInfinity: a div vTagInfinity, vector: a div vVector, point: a div vPoint)
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

## Test: integerDivide tagInfinity

This runtime case exercises “integerDivide tagInfinity” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0068
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "integerDivide tagInfinity.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbn
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
  emit Done(nothing: a div vNothing, integer: a div vInteger, float: a div vFloat, percentage: a div vPercentage, meter: a div vMeter, boolean: a div vBoolean, textNumber: a div vTextNumber, textInvalid: a div vTextInvalid, tagPi: a div vTagPi, tagNan: a div vTagNan, tagInfinity: a div vTagInfinity, vector: a div vVector, point: a div vPoint)
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

## Test: integerDivide vector

This runtime case exercises “integerDivide vector” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0069
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "integerDivide vector.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbo
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
  emit Done(nothing: a div vNothing, integer: a div vInteger, float: a div vFloat, percentage: a div vPercentage, meter: a div vMeter, boolean: a div vBoolean, textNumber: a div vTextNumber, textInvalid: a div vTextInvalid, tagPi: a div vTagPi, tagNan: a div vTagNan, tagInfinity: a div vTagInfinity, vector: a div vVector, point: a div vPoint)
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

## Test: integerDivide point

This runtime case exercises “integerDivide point” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0070
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "integerDivide point.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixbp
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
  emit Done(nothing: a div vNothing, integer: a div vInteger, float: a div vFloat, percentage: a div vPercentage, meter: a div vMeter, boolean: a div vBoolean, textNumber: a div vTextNumber, textInvalid: a div vTextInvalid, tagPi: a div vTagPi, tagNan: a div vTagNan, tagInfinity: a div vTagInfinity, vector: a div vVector, point: a div vPoint)
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
