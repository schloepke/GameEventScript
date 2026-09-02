---
formatVersion: 1
suiteId: "runtime.atomic.math-matrix.clamp-measured"
title: "Math Matrix — Clamp Measured Values"
categories: [conformance]
---

# Math Matrix — Clamp Measured Values

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers clamp behavior for percentages and measured quantities.

---

## Test: clamp percentage nothing

This runtime case exercises “clamp percentage nothing” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0175
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp percentage nothing.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixFQ
on Start {
  let a be 10%
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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

## Test: clamp percentage integer

This runtime case exercises “clamp percentage integer” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0176
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp percentage integer.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixFR
on Start {
  let a be 10%
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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
              type: ":integer"
              value: "10"
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

## Test: clamp percentage float

This runtime case exercises “clamp percentage float” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0177
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp percentage float.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixFS
on Start {
  let a be 10%
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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
              value: "10.3"
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

## Test: clamp percentage percentage

This runtime case exercises “clamp percentage percentage” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0178
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp percentage percentage.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixFT
on Start {
  let a be 10%
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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
              type: ":nothing"
          - name: "integer"
            value:
              type: ":float"
              value: "0.1"
          - name: "float"
            value:
              type: ":float"
              value: "0.1"
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
              value: "0.1"
          - name: "tagNan"
            value:
              type: ":float"
              value: "NaN"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "0.1"
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

## Test: clamp percentage meter

This runtime case exercises “clamp percentage meter” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0179
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp percentage meter.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixFU
on Start {
  let a be 10%
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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

## Test: clamp percentage boolean

This runtime case exercises “clamp percentage boolean” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0180
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp percentage boolean.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixFV
on Start {
  let a be 10%
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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

## Test: clamp percentage textNumber

This runtime case exercises “clamp percentage textNumber” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0181
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp percentage textNumber.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixFW
on Start {
  let a be 10%
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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

## Test: clamp percentage textInvalid

This runtime case exercises “clamp percentage textInvalid” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0182
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp percentage textInvalid.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixFX
on Start {
  let a be 10%
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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

## Test: clamp percentage tagPi

This runtime case exercises “clamp percentage tagPi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0183
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp percentage tagPi.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixFY
on Start {
  let a be 10%
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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
              value: "3.141592653589793"
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

## Test: clamp percentage tagNan

This runtime case exercises “clamp percentage tagNan” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0184
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp percentage tagNan.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixFZ
on Start {
  let a be 10%
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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

## Test: clamp percentage tagInfinity

This runtime case exercises “clamp percentage tagInfinity” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0185
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp percentage tagInfinity.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixGA
on Start {
  let a be 10%
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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

## Test: clamp percentage vector

This runtime case exercises “clamp percentage vector” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0186
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp percentage vector.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixGB
on Start {
  let a be 10%
  let b be :vector(1, 2, 3)
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

## Test: clamp percentage point

This runtime case exercises “clamp percentage point” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0187
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp percentage point.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixGC
on Start {
  let a be 10%
  let b be :point(1, 2, 3)
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

## Test: clamp meter nothing

This runtime case exercises “clamp meter nothing” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0188
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp meter nothing.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixGD
on Start {
  let a be 10m
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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

## Test: clamp meter integer

This runtime case exercises “clamp meter integer” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0189
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp meter integer.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixGE
on Start {
  let a be 10m
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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

## Test: clamp meter float

This runtime case exercises “clamp meter float” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0190
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp meter float.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixGF
on Start {
  let a be 10m
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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

## Test: clamp meter percentage

This runtime case exercises “clamp meter percentage” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0191
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp meter percentage.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixGG
on Start {
  let a be 10m
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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

## Test: clamp meter meter

This runtime case exercises “clamp meter meter” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0192
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp meter meter.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixGH
on Start {
  let a be 10m
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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

## Test: clamp meter boolean

This runtime case exercises “clamp meter boolean” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0193
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp meter boolean.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixGI
on Start {
  let a be 10m
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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

## Test: clamp meter textNumber

This runtime case exercises “clamp meter textNumber” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0194
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp meter textNumber.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixGJ
on Start {
  let a be 10m
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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

## Test: clamp meter textInvalid

This runtime case exercises “clamp meter textInvalid” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0195
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp meter textInvalid.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixGK
on Start {
  let a be 10m
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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

## Test: clamp meter tagPi

This runtime case exercises “clamp meter tagPi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0196
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp meter tagPi.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixGL
on Start {
  let a be 10m
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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

## Test: clamp meter tagNan

This runtime case exercises “clamp meter tagNan” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0197
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp meter tagNan.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixGM
on Start {
  let a be 10m
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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

## Test: clamp meter tagInfinity

This runtime case exercises “clamp meter tagInfinity” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0198
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp meter tagInfinity.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixGN
on Start {
  let a be 10m
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
  let vVector be :vector(1, 2, 3)
  let vPoint be :point(1, 2, 3)
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

## Test: clamp meter vector

This runtime case exercises “clamp meter vector” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0199
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp meter vector.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixGO
on Start {
  let a be 10m
  let b be :vector(1, 2, 3)
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

## Test: clamp meter point

This runtime case exercises “clamp meter point” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0200
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp meter point.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixGP
on Start {
  let a be 10m
  let b be :point(1, 2, 3)
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
