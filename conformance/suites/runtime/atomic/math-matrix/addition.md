---
formatVersion: 1
suiteId: "runtime.atomic.math-matrix.addition"
title: "Math Matrix — Addition"
categories: [conformance]
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
module atomicmathmatrixd
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
module atomicmathmatrixe
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
  emit Done(nothing: a + vNothing, integer: a + vInteger, float: a + vFloat, percentage: a + vPercentage, meter: a + vMeter, boolean: a + vBoolean, textNumber: ((a + vTextNumber) is :Text and (a + vTextNumber) = ((a as :Text) + (vTextNumber as :Text))), textInvalid: ((a + vTextInvalid) is :Text and (a + vTextInvalid) = ((a as :Text) + (vTextInvalid as :Text))), tagPi: a + vTagPi, tagNan: a + vTagNan, tagInfinity: a + vTagInfinity, vector: a + vVector, point: a + vPoint)
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
              value: "20"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "20.3"
          - name: "percentage"
            value:
              type: ":Number.int64"
              value: "11"
          - name: "meter"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Number.int64"
              value: "11"
          - name: "textNumber"
            value:
              type: ":Boolean"
              value: true
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: true
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "13.1415926535898"
          - name: "tagNan"
            value:
              type: ":Nothing"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Nothing"
          - name: "point"
            value:
              type: ":Nothing"
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
module atomicmathmatrixf
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
  emit Done(nothing: a + vNothing, integer: a + vInteger, float: a + vFloat, percentage: a + vPercentage, meter: a + vMeter, boolean: a + vBoolean, textNumber: ((a + vTextNumber) is :Text and (a + vTextNumber) = ((a as :Text) + (vTextNumber as :Text))), textInvalid: ((a + vTextInvalid) is :Text and (a + vTextInvalid) = ((a as :Text) + (vTextInvalid as :Text))), tagPi: a + vTagPi, tagNan: a + vTagNan, tagInfinity: a + vTagInfinity, vector: a + vVector, point: a + vPoint)
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
              value: "20.3"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "20.6"
          - name: "percentage"
            value:
              type: ":Number.binary64"
              value: "11.33"
          - name: "meter"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Number.binary64"
              value: "11.3"
          - name: "textNumber"
            value:
              type: ":Boolean"
              value: true
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: true
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "13.4415926535898"
          - name: "tagNan"
            value:
              type: ":Nothing"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Nothing"
          - name: "point"
            value:
              type: ":Nothing"
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
module atomicmathmatrixg
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
  emit Done(nothing: a + vNothing, integer: a + vInteger, float: a + vFloat, percentage: a + vPercentage, meter: a + vMeter, boolean: a + vBoolean, textNumber: ((a + vTextNumber) is :Text and (a + vTextNumber) = ((a as :Text) + (vTextNumber as :Text))), textInvalid: ((a + vTextInvalid) is :Text and (a + vTextInvalid) = ((a as :Text) + (vTextInvalid as :Text))), tagPi: a + vTagPi, tagNan: a + vTagNan, tagInfinity: a + vTagInfinity, vector: a + vVector, point: a + vPoint)
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
              type: ":Percentage"
              value: "0.2"
          - name: "meter"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Nothing"
          - name: "textNumber"
            value:
              type: ":Boolean"
              value: true
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: true
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
module atomicmathmatrixh
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
  emit Done(nothing: a + vNothing, integer: a + vInteger, float: a + vFloat, percentage: a + vPercentage, meter: a + vMeter, boolean: a + vBoolean, textNumber: ((a + vTextNumber) is :Text and (a + vTextNumber) = ((a as :Text) + (vTextNumber as :Text))), textInvalid: ((a + vTextInvalid) is :Text and (a + vTextInvalid) = ((a as :Text) + (vTextInvalid as :Text))), tagPi: a + vTagPi, tagNan: a + vTagNan, tagInfinity: a + vTagInfinity, vector: a + vVector, point: a + vPoint)
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
              type: ":Quantity.int64"
              value: "11"
              unit: ":meter"
          - name: "meter"
            value:
              type: ":Quantity.int64"
              value: "20"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":Nothing"
          - name: "textNumber"
            value:
              type: ":Boolean"
              value: true
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: true
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
module atomicmathmatrixi
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
              type: ":Nothing"
          - name: "integer"
            value:
              type: ":Number.int64"
              value: "11"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "11.3"
          - name: "percentage"
            value:
              type: ":Number.binary64"
              value: "1.1"
          - name: "meter"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "textNumber"
            value:
              type: ":Text"
              value: "true10.3"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "truehello"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "4.14159265358979"
          - name: "tagNan"
            value:
              type: ":Nothing"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Nothing"
          - name: "point"
            value:
              type: ":Nothing"
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
module atomicmathmatrixj
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
  emit Done(nothing: a + vNothing, integer: ((a + vInteger) is :Text and (a + vInteger) = ((a as :Text) + (vInteger as :Text))), float: ((a + vFloat) is :Text and (a + vFloat) = ((a as :Text) + (vFloat as :Text))), percentage: ((a + vPercentage) is :Text and (a + vPercentage) = ((a as :Text) + (vPercentage as :Text))), meter: ((a + vMeter) is :Text and (a + vMeter) = ((a as :Text) + (vMeter as :Text))), boolean: a + vBoolean, textNumber: a + vTextNumber, textInvalid: a + vTextInvalid, tagPi: ((a + vTagPi) is :Text and (a + vTagPi) = ((a as :Text) + (vTagPi as :Text))), tagNan: a + vTagNan, tagInfinity: ((a + vTagInfinity) is :Text and (a + vTagInfinity) = ((a as :Text) + (vTagInfinity as :Text))), vector: a + vVector, point: a + vPoint)
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
              type: ":Boolean"
              value: true
          - name: "float"
            value:
              type: ":Boolean"
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "meter"
            value:
              type: ":Boolean"
              value: true
          - name: "boolean"
            value:
              type: ":Text"
              value: "10.3true"
          - name: "textNumber"
            value:
              type: ":Text"
              value: "10.310.3"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "10.3hello"
          - name: "tagPi"
            value:
              type: ":Boolean"
              value: true
          - name: "tagNan"
            value:
              type: ":Text"
              value: "10.3#nan"
          - name: "tagInfinity"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Text"
              value: "10.3vector[x: 1, y: 2, z: 3]"
          - name: "point"
            value:
              type: ":Text"
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
module atomicmathmatrixk
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
  emit Done(nothing: a + vNothing, integer: ((a + vInteger) is :Text and (a + vInteger) = ((a as :Text) + (vInteger as :Text))), float: ((a + vFloat) is :Text and (a + vFloat) = ((a as :Text) + (vFloat as :Text))), percentage: ((a + vPercentage) is :Text and (a + vPercentage) = ((a as :Text) + (vPercentage as :Text))), meter: ((a + vMeter) is :Text and (a + vMeter) = ((a as :Text) + (vMeter as :Text))), boolean: a + vBoolean, textNumber: a + vTextNumber, textInvalid: a + vTextInvalid, tagPi: ((a + vTagPi) is :Text and (a + vTagPi) = ((a as :Text) + (vTagPi as :Text))), tagNan: a + vTagNan, tagInfinity: ((a + vTagInfinity) is :Text and (a + vTagInfinity) = ((a as :Text) + (vTagInfinity as :Text))), vector: a + vVector, point: a + vPoint)
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
              type: ":Boolean"
              value: true
          - name: "float"
            value:
              type: ":Boolean"
              value: true
          - name: "percentage"
            value:
              type: ":Boolean"
              value: true
          - name: "meter"
            value:
              type: ":Boolean"
              value: true
          - name: "boolean"
            value:
              type: ":Text"
              value: "hellotrue"
          - name: "textNumber"
            value:
              type: ":Text"
              value: "hello10.3"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "hellohello"
          - name: "tagPi"
            value:
              type: ":Boolean"
              value: true
          - name: "tagNan"
            value:
              type: ":Text"
              value: "hello#nan"
          - name: "tagInfinity"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Text"
              value: "hellovector[x: 1, y: 2, z: 3]"
          - name: "point"
            value:
              type: ":Text"
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
module atomicmathmatrixl
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
  emit Done(nothing: a + vNothing, integer: a + vInteger, float: a + vFloat, percentage: a + vPercentage, meter: a + vMeter, boolean: a + vBoolean, textNumber: ((a + vTextNumber) is :Text and (a + vTextNumber) = ((a as :Text) + (vTextNumber as :Text))), textInvalid: ((a + vTextInvalid) is :Text and (a + vTextInvalid) = ((a as :Text) + (vTextInvalid as :Text))), tagPi: a + vTagPi, tagNan: a + vTagNan, tagInfinity: a + vTagInfinity, vector: a + vVector, point: a + vPoint)
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
              value: "13.1415926535898"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "13.4415926535898"
          - name: "percentage"
            value:
              type: ":Number.binary64"
              value: "3.45575191894877"
          - name: "meter"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Number.binary64"
              value: "4.14159265358979"
          - name: "textNumber"
            value:
              type: ":Boolean"
              value: true
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: true
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "6.28318530717959"
          - name: "tagNan"
            value:
              type: ":Nothing"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Nothing"
          - name: "point"
            value:
              type: ":Nothing"
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
module atomicmathmatrixm
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
              type: ":Text"
              value: "#nan10.3"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "#nanhello"
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
module atomicmathmatrixn
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
  emit Done(nothing: a + vNothing, integer: a + vInteger, float: a + vFloat, percentage: a + vPercentage, meter: a + vMeter, boolean: a + vBoolean, textNumber: ((a + vTextNumber) is :Text and (a + vTextNumber) = ((a as :Text) + (vTextNumber as :Text))), textInvalid: ((a + vTextInvalid) is :Text and (a + vTextInvalid) = ((a as :Text) + (vTextInvalid as :Text))), tagPi: a + vTagPi, tagNan: a + vTagNan, tagInfinity: a + vTagInfinity, vector: a + vVector, point: a + vPoint)
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
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "textNumber"
            value:
              type: ":Boolean"
              value: true
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: true
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "tagNan"
            value:
              type: ":Nothing"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Nothing"
          - name: "point"
            value:
              type: ":Nothing"
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
module atomicmathmatrixo
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
              type: ":Text"
              value: "vector[x: 1, y: 2, z: 3]10.3"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "vector[x: 1, y: 2, z: 3]hello"
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
              type: ":Vector"
              x: "2"
              y: "4"
              z: "6"
          - name: "point"
            value:
              type: ":Nothing"
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
module atomicmathmatrixp
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
              type: ":Text"
              value: "point[x: 1, y: 2, z: 3]10.3"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "point[x: 1, y: 2, z: 3]hello"
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
              type: ":Point"
              x: "2"
              y: "4"
              z: "6"
          - name: "point"
            value:
              type: ":Nothing"
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
module atomicmathmatrixaddcollections
on Start {
  let vList be [1, 2, 3]
  let vListOther be [4, 5]
  let vMap be [name: 'Ada', hp: 10]
  let vMapOther be [hp: 12, mp: 5]
  let vDice be ([3, 1]) as :Dice
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
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
                - type: ":Number.int64"
                  value: "4"
          - name: "listPrependInteger"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "0"
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
          - name: "listAppendList"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
                - type: ":List"
                  items:
                    - type: ":Number.int64"
                      value: "4"
                    - type: ":Number.int64"
                      value: "5"
          - name: "listAppendText"
            value:
              type: ":Text"
              value: "[1, 2, 3]end"
          - name: "listAppendMap"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
                - type: ":Map"
                  entries:
                    - key: "hp"
                      value:
                        type: ":Number.int64"
                        value: "10"
                    - key: "name"
                      value:
                        type: ":Text"
                        value: "Ada"
          - name: "mapMerge"
            value:
              type: ":Nothing"
          - name: "mapPlusList"
            value:
              type: ":List"
              items:
                - type: ":Map"
                  entries:
                    - key: "hp"
                      value:
                        type: ":Number.int64"
                        value: "10"
                    - key: "name"
                      value:
                        type: ":Text"
                        value: "Ada"
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
          - name: "mapPlusInteger"
            value:
              type: ":Nothing"
          - name: "diceAddInteger"
            value:
              type: ":Dice"
              rolls:
                - 3
                - 2
                - 1
          - name: "integerAddDice"
            value:
              type: ":Dice"
              rolls:
                - 4
                - 3
                - 1
          - name: "diceAddFloat"
            value:
              type: ":Nothing"
```
