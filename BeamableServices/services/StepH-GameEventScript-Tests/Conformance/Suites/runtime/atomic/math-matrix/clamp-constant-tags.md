---
formatVersion: 1
suiteId: "runtime.atomic.math-matrix.clamp-constant-tags"
title: "Math Matrix — Clamp Constant Tags"
categories: [conformance]
---

# Math Matrix — Clamp Constant Tags

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers clamp behavior for finite and infinite numeric constant tags.

---

## Test: clamp tagPi nothing

This runtime case exercises “clamp tagPi nothing” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0240
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagPi nothing.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixID
on Start {
  let a be pi
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

## Test: clamp tagPi integer

This runtime case exercises “clamp tagPi integer” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0241
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagPi integer.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixIE
on Start {
  let a be pi
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

## Test: clamp tagPi float

This runtime case exercises “clamp tagPi float” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0242
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagPi float.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixIF
on Start {
  let a be pi
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

## Test: clamp tagPi percentage

This runtime case exercises “clamp tagPi percentage” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0243
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagPi percentage.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixIG
on Start {
  let a be pi
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

## Test: clamp tagPi meter

This runtime case exercises “clamp tagPi meter” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0244
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagPi meter.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixIH
on Start {
  let a be pi
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

## Test: clamp tagPi boolean

This runtime case exercises “clamp tagPi boolean” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0245
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagPi boolean.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixII
on Start {
  let a be pi
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
              value: "3.141592653589793"
          - name: "float"
            value:
              type: ":float"
              value: "3.141592653589793"
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

## Test: clamp tagPi textNumber

This runtime case exercises “clamp tagPi textNumber” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0246
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagPi textNumber.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixIJ
on Start {
  let a be pi
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

## Test: clamp tagPi textInvalid

This runtime case exercises “clamp tagPi textInvalid” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0247
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagPi textInvalid.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixIK
on Start {
  let a be pi
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

## Test: clamp tagPi tagPi

This runtime case exercises “clamp tagPi tagPi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0248
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagPi tagPi.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixIL
on Start {
  let a be pi
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

## Test: clamp tagPi tagNan

This runtime case exercises “clamp tagPi tagNan” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0249
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagPi tagNan.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixIM
on Start {
  let a be pi
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

## Test: clamp tagPi tagInfinity

This runtime case exercises “clamp tagPi tagInfinity” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0250
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagPi tagInfinity.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixIN
on Start {
  let a be pi
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

## Test: clamp tagPi vector

This runtime case exercises “clamp tagPi vector” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0251
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagPi vector.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixIO
on Start {
  let a be pi
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

## Test: clamp tagPi point

This runtime case exercises “clamp tagPi point” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0252
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagPi point.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixIP
on Start {
  let a be pi
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

## Test: clamp tagInfinity nothing

This runtime case exercises “clamp tagInfinity nothing” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0266
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagInfinity nothing.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixJD
on Start {
  let a be infinity
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

## Test: clamp tagInfinity integer

This runtime case exercises “clamp tagInfinity integer” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0267
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagInfinity integer.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixJE
on Start {
  let a be infinity
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
              type: ":float"
              value: "10.3"
          - name: "percentage"
            value:
              type: ":integer"
              value: "10"
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
              type: ":integer"
              value: "10"
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

## Test: clamp tagInfinity float

This runtime case exercises “clamp tagInfinity float” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0268
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagInfinity float.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixJF
on Start {
  let a be infinity
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
              value: "10.3"
          - name: "float"
            value:
              type: ":float"
              value: "10.3"
          - name: "percentage"
            value:
              type: ":float"
              value: "10.3"
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
              value: "10.3"
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

## Test: clamp tagInfinity percentage

This runtime case exercises “clamp tagInfinity percentage” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0269
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagInfinity percentage.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixJG
on Start {
  let a be infinity
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

## Test: clamp tagInfinity meter

This runtime case exercises “clamp tagInfinity meter” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0270
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagInfinity meter.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixJH
on Start {
  let a be infinity
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

## Test: clamp tagInfinity boolean

This runtime case exercises “clamp tagInfinity boolean” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0271
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagInfinity boolean.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixJI
on Start {
  let a be infinity
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
              value: "10"
          - name: "float"
            value:
              type: ":float"
              value: "10.3"
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

## Test: clamp tagInfinity textNumber

This runtime case exercises “clamp tagInfinity textNumber” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0272
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagInfinity textNumber.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixJJ
on Start {
  let a be infinity
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

## Test: clamp tagInfinity textInvalid

This runtime case exercises “clamp tagInfinity textInvalid” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0273
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagInfinity textInvalid.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixJK
on Start {
  let a be infinity
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

## Test: clamp tagInfinity tagPi

This runtime case exercises “clamp tagInfinity tagPi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0274
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagInfinity tagPi.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixJL
on Start {
  let a be infinity
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
              type: ":integer"
              value: "10"
          - name: "float"
            value:
              type: ":float"
              value: "10.3"
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

## Test: clamp tagInfinity tagNan

This runtime case exercises “clamp tagInfinity tagNan” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0275
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagInfinity tagNan.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixJM
on Start {
  let a be infinity
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

## Test: clamp tagInfinity tagInfinity

This runtime case exercises “clamp tagInfinity tagInfinity” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0276
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagInfinity tagInfinity.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixJN
on Start {
  let a be infinity
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

## Test: clamp tagInfinity vector

This runtime case exercises “clamp tagInfinity vector” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0277
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagInfinity vector.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixJO
on Start {
  let a be infinity
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

## Test: clamp tagInfinity point

This runtime case exercises “clamp tagInfinity point” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0278
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "clamp tagInfinity point.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixJP
on Start {
  let a be infinity
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
