---
formatVersion: 1
suiteId: "runtime.atomic.math-matrix.addition"
title: "Math Matrix — Addition"
categories: [conformance]
tags: [migrated-json-v1]
---

# Math Matrix — Addition

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers addition across primitive, measured, spatial, and collection values.

---

## Test: add nothing

This runtime case exercises “add nothing” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0004
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "add nothing.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixD
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
  emit Done(nothing: a + vNothing, integer: a + vInteger, float: a + vFloat, percentage: a + vPercentage, meter: a + vMeter, boolean: a + vBoolean, textNumber: a + vTextNumber, textInvalid: a + vTextInvalid, tagPi: a + vTagPi, tagNan: a + vTagNan, tagInfinity: a + vTagInfinity, vector: a + vVector, point: a + vPoint)
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

## Test: add integer

This runtime case exercises “add integer” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0005
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "add integer.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixE
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
  emit Done(nothing: a + vNothing, integer: a + vInteger, float: a + vFloat, percentage: a + vPercentage, meter: a + vMeter, boolean: a + vBoolean, textNumber: a + vTextNumber, textInvalid: a + vTextInvalid, tagPi: a + vTagPi, tagNan: a + vTagNan, tagInfinity: a + vTagInfinity, vector: a + vVector, point: a + vPoint)
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
              value: "20"
          - name: "float"
            value:
              type: ":float"
              value: "20.3"
          - name: "percentage"
            value:
              type: ":integer"
              value: "11"
          - name: "meter"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":integer"
              value: "11"
          - name: "textNumber"
            value:
              type: ":text"
              value: "1010.3"
          - name: "textInvalid"
            value:
              type: ":text"
              value: "10hello"
          - name: "tagPi"
            value:
              type: ":float"
              value: "13.1415926535898"
          - name: "tagNan"
            value:
              type: ":nothing"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":nothing"
          - name: "point"
            value:
              type: ":nothing"
```

---

## Test: add float

This runtime case exercises “add float” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0006
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "add float.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixF
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
  emit Done(nothing: a + vNothing, integer: a + vInteger, float: a + vFloat, percentage: a + vPercentage, meter: a + vMeter, boolean: a + vBoolean, textNumber: a + vTextNumber, textInvalid: a + vTextInvalid, tagPi: a + vTagPi, tagNan: a + vTagNan, tagInfinity: a + vTagInfinity, vector: a + vVector, point: a + vPoint)
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
              value: "20.3"
          - name: "float"
            value:
              type: ":float"
              value: "20.6"
          - name: "percentage"
            value:
              type: ":float"
              value: "11.33"
          - name: "meter"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":float"
              value: "11.3"
          - name: "textNumber"
            value:
              type: ":text"
              value: "10.310.3"
          - name: "textInvalid"
            value:
              type: ":text"
              value: "10.3hello"
          - name: "tagPi"
            value:
              type: ":float"
              value: "13.4415926535898"
          - name: "tagNan"
            value:
              type: ":nothing"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":nothing"
          - name: "point"
            value:
              type: ":nothing"
```

---

## Test: add percentage

This runtime case exercises “add percentage” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0007
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "add percentage.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixG
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
  emit Done(nothing: a + vNothing, integer: a + vInteger, float: a + vFloat, percentage: a + vPercentage, meter: a + vMeter, boolean: a + vBoolean, textNumber: a + vTextNumber, textInvalid: a + vTextInvalid, tagPi: a + vTagPi, tagNan: a + vTagNan, tagInfinity: a + vTagInfinity, vector: a + vVector, point: a + vPoint)
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
              type: ":percentage"
              value: "0.2"
          - name: "meter"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":nothing"
          - name: "textNumber"
            value:
              type: ":text"
              value: "10%10.3"
          - name: "textInvalid"
            value:
              type: ":text"
              value: "10%hello"
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

## Test: add meter

This runtime case exercises “add meter” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0008
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "add meter.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixH
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
  emit Done(nothing: a + vNothing, integer: a + vInteger, float: a + vFloat, percentage: a + vPercentage, meter: a + vMeter, boolean: a + vBoolean, textNumber: a + vTextNumber, textInvalid: a + vTextInvalid, tagPi: a + vTagPi, tagNan: a + vTagNan, tagInfinity: a + vTagInfinity, vector: a + vVector, point: a + vPoint)
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
              type: ":integer"
              value: "11"
              unit: ":meter"
          - name: "meter"
            value:
              type: ":integer"
              value: "20"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":nothing"
          - name: "textNumber"
            value:
              type: ":text"
              value: "10m10.3"
          - name: "textInvalid"
            value:
              type: ":text"
              value: "10mhello"
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

## Test: add boolean

This runtime case exercises “add boolean” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0009
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "add boolean.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixI
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
  emit Done(nothing: a + vNothing, integer: a + vInteger, float: a + vFloat, percentage: a + vPercentage, meter: a + vMeter, boolean: a + vBoolean, textNumber: a + vTextNumber, textInvalid: a + vTextInvalid, tagPi: a + vTagPi, tagNan: a + vTagNan, tagInfinity: a + vTagInfinity, vector: a + vVector, point: a + vPoint)
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
              value: "11"
          - name: "float"
            value:
              type: ":float"
              value: "11.3"
          - name: "percentage"
            value:
              type: ":float"
              value: "1.1"
          - name: "meter"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":integer"
              value: "2"
          - name: "textNumber"
            value:
              type: ":text"
              value: "True10.3"
          - name: "textInvalid"
            value:
              type: ":text"
              value: "Truehello"
          - name: "tagPi"
            value:
              type: ":float"
              value: "4.14159265358979"
          - name: "tagNan"
            value:
              type: ":nothing"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":nothing"
          - name: "point"
            value:
              type: ":nothing"
```

---

## Test: add textNumber

This runtime case exercises “add textNumber” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0010
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "add textNumber.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixJ
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
  emit Done(nothing: a + vNothing, integer: a + vInteger, float: a + vFloat, percentage: a + vPercentage, meter: a + vMeter, boolean: a + vBoolean, textNumber: a + vTextNumber, textInvalid: a + vTextInvalid, tagPi: a + vTagPi, tagNan: a + vTagNan, tagInfinity: a + vTagInfinity, vector: a + vVector, point: a + vPoint)
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
              type: ":text"
              value: "10.310"
          - name: "float"
            value:
              type: ":text"
              value: "10.310.3"
          - name: "percentage"
            value:
              type: ":text"
              value: "10.310%"
          - name: "meter"
            value:
              type: ":text"
              value: "10.310m"
          - name: "boolean"
            value:
              type: ":text"
              value: "10.3True"
          - name: "textNumber"
            value:
              type: ":text"
              value: "10.310.3"
          - name: "textInvalid"
            value:
              type: ":text"
              value: "10.3hello"
          - name: "tagPi"
            value:
              type: ":text"
              value: "10.33.141592653589793"
          - name: "tagNan"
            value:
              type: ":text"
              value: "10.3:nan"
          - name: "tagInfinity"
            value:
              type: ":text"
              value: "10.3Infinity"
          - name: "vector"
            value:
              type: ":text"
              value: "10.3vector[x: 1, y: 2, z: 3]"
          - name: "point"
            value:
              type: ":text"
              value: "10.3point[x: 1, y: 2, z: 3]"
```

---

## Test: add textInvalid

This runtime case exercises “add textInvalid” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0011
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "add textInvalid.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixK
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
  emit Done(nothing: a + vNothing, integer: a + vInteger, float: a + vFloat, percentage: a + vPercentage, meter: a + vMeter, boolean: a + vBoolean, textNumber: a + vTextNumber, textInvalid: a + vTextInvalid, tagPi: a + vTagPi, tagNan: a + vTagNan, tagInfinity: a + vTagInfinity, vector: a + vVector, point: a + vPoint)
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
              type: ":text"
              value: "hello10"
          - name: "float"
            value:
              type: ":text"
              value: "hello10.3"
          - name: "percentage"
            value:
              type: ":text"
              value: "hello10%"
          - name: "meter"
            value:
              type: ":text"
              value: "hello10m"
          - name: "boolean"
            value:
              type: ":text"
              value: "helloTrue"
          - name: "textNumber"
            value:
              type: ":text"
              value: "hello10.3"
          - name: "textInvalid"
            value:
              type: ":text"
              value: "hellohello"
          - name: "tagPi"
            value:
              type: ":text"
              value: "hello3.141592653589793"
          - name: "tagNan"
            value:
              type: ":text"
              value: "hello:nan"
          - name: "tagInfinity"
            value:
              type: ":text"
              value: "helloInfinity"
          - name: "vector"
            value:
              type: ":text"
              value: "hellovector[x: 1, y: 2, z: 3]"
          - name: "point"
            value:
              type: ":text"
              value: "hellopoint[x: 1, y: 2, z: 3]"
```

---

## Test: add tagPi

This runtime case exercises “add tagPi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0012
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "add tagPi.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixL
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
  emit Done(nothing: a + vNothing, integer: a + vInteger, float: a + vFloat, percentage: a + vPercentage, meter: a + vMeter, boolean: a + vBoolean, textNumber: a + vTextNumber, textInvalid: a + vTextInvalid, tagPi: a + vTagPi, tagNan: a + vTagNan, tagInfinity: a + vTagInfinity, vector: a + vVector, point: a + vPoint)
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
              value: "13.1415926535898"
          - name: "float"
            value:
              type: ":float"
              value: "13.4415926535898"
          - name: "percentage"
            value:
              type: ":float"
              value: "3.45575191894877"
          - name: "meter"
            value:
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":float"
              value: "4.14159265358979"
          - name: "textNumber"
            value:
              type: ":text"
              value: "3.14159265358979310.3"
          - name: "textInvalid"
            value:
              type: ":text"
              value: "3.141592653589793hello"
          - name: "tagPi"
            value:
              type: ":float"
              value: "6.28318530717959"
          - name: "tagNan"
            value:
              type: ":nothing"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":nothing"
          - name: "point"
            value:
              type: ":nothing"
```

---

## Test: add tagNan

This runtime case exercises “add tagNan” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0013
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "add tagNan.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixM
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
  emit Done(nothing: a + vNothing, integer: a + vInteger, float: a + vFloat, percentage: a + vPercentage, meter: a + vMeter, boolean: a + vBoolean, textNumber: a + vTextNumber, textInvalid: a + vTextInvalid, tagPi: a + vTagPi, tagNan: a + vTagNan, tagInfinity: a + vTagInfinity, vector: a + vVector, point: a + vPoint)
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
              type: ":text"
              value: ":nan10.3"
          - name: "textInvalid"
            value:
              type: ":text"
              value: ":nanhello"
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

## Test: add tagInfinity

This runtime case exercises “add tagInfinity” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0014
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "add tagInfinity.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixN
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
  emit Done(nothing: a + vNothing, integer: a + vInteger, float: a + vFloat, percentage: a + vPercentage, meter: a + vMeter, boolean: a + vBoolean, textNumber: a + vTextNumber, textInvalid: a + vTextInvalid, tagPi: a + vTagPi, tagNan: a + vTagNan, tagInfinity: a + vTagInfinity, vector: a + vVector, point: a + vPoint)
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
              type: ":nothing"
          - name: "boolean"
            value:
              type: ":float"
              value: "Infinity"
          - name: "textNumber"
            value:
              type: ":text"
              value: "Infinity10.3"
          - name: "textInvalid"
            value:
              type: ":text"
              value: "Infinityhello"
          - name: "tagPi"
            value:
              type: ":float"
              value: "Infinity"
          - name: "tagNan"
            value:
              type: ":nothing"
          - name: "tagInfinity"
            value:
              type: ":float"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":nothing"
          - name: "point"
            value:
              type: ":nothing"
```

---

## Test: add vector

This runtime case exercises “add vector” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0015
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "add vector.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixO
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
  emit Done(nothing: a + vNothing, integer: a + vInteger, float: a + vFloat, percentage: a + vPercentage, meter: a + vMeter, boolean: a + vBoolean, textNumber: a + vTextNumber, textInvalid: a + vTextInvalid, tagPi: a + vTagPi, tagNan: a + vTagNan, tagInfinity: a + vTagInfinity, vector: a + vVector, point: a + vPoint)
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
              type: ":text"
              value: "vector[x: 1, y: 2, z: 3]10.3"
          - name: "textInvalid"
            value:
              type: ":text"
              value: "vector[x: 1, y: 2, z: 3]hello"
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
              type: ":vector"
              x: "2"
              y: "4"
              z: "6"
          - name: "point"
            value:
              type: ":nothing"
```

---

## Test: add point

This runtime case exercises “add point” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0016
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "add point.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixP
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
  emit Done(nothing: a + vNothing, integer: a + vInteger, float: a + vFloat, percentage: a + vPercentage, meter: a + vMeter, boolean: a + vBoolean, textNumber: a + vTextNumber, textInvalid: a + vTextInvalid, tagPi: a + vTagPi, tagNan: a + vTagNan, tagInfinity: a + vTagInfinity, vector: a + vVector, point: a + vPoint)
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
              type: ":text"
              value: "point[x: 1, y: 2, z: 3]10.3"
          - name: "textInvalid"
            value:
              type: ":text"
              value: "point[x: 1, y: 2, z: 3]hello"
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
              type: ":point"
              x: "2"
              y: "4"
              z: "6"
          - name: "point"
            value:
              type: ":nothing"
```

---

## Test: add collection values

This runtime case exercises “add collection values” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0017
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "add collection values.ges"
    program: main
```

### Source code under test

```ges
module AtomicMathMatrixAddCollections
on Start {
  let vList be [1, 2, 3]
  let vListOther be [4, 5]
  let vMap be [name: 'Ada', hp: 10]
  let vMapOther be [hp: 12, mp: 5]
  let vDice as :dice be [3, 1]
  emit Done(listAppendInteger: vList + 4, listPrependInteger: 0 + vList, listAppendList: vList + vListOther, listAppendText: vList + 'end', listAppendMap: vList + vMap, mapMerge: vMap + vMapOther, mapPlusList: vMap + vList, mapPlusInteger: vMap + 1, diceAddInteger: vDice + 2, integerAddDice: 4 + vDice, diceAddFloat: vDice + 2.5)
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
          - name: "listAppendInteger"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "4"
          - name: "listPrependInteger"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "0"
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "listAppendList"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
                - type: ":list"
                  items:
                    - type: ":integer"
                      value: "4"
                    - type: ":integer"
                      value: "5"
          - name: "listAppendText"
            value:
              type: ":text"
              value: "[1, 2, 3]end"
          - name: "listAppendMap"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
                - type: ":map"
                  entries:
                    - key: "hp"
                      value:
                        type: ":integer"
                        value: "10"
                    - key: "name"
                      value:
                        type: ":text"
                        value: "Ada"
          - name: "mapMerge"
            value:
              type: ":nothing"
          - name: "mapPlusList"
            value:
              type: ":list"
              items:
                - type: ":map"
                  entries:
                    - key: "hp"
                      value:
                        type: ":integer"
                        value: "10"
                    - key: "name"
                      value:
                        type: ":text"
                        value: "Ada"
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "mapPlusInteger"
            value:
              type: ":nothing"
          - name: "diceAddInteger"
            value:
              type: ":dice"
              rolls:
                - 3
                - 2
                - 1
          - name: "integerAddDice"
            value:
              type: ":dice"
              rolls:
                - 4
                - 3
                - 1
          - name: "diceAddFloat"
            value:
              type: ":nothing"
```
