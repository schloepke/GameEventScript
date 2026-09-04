---
formatVersion: 1
suiteId: "runtime.atomic.math-matrix.maximum"
title: "Math Matrix — Maximum"
categories: [conformance]
---

# Math Matrix — Maximum

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers maximum selection across primitive, measured, and structured values.

---

## Test: max nothing

This runtime case exercises “max nothing” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0123
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "max nothing.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdq
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
  emit Done(nothing: max of a and vNothing, integer: max of a and vInteger, float: max of a and vFloat, percentage: max of a and vPercentage, meter: max of a and vMeter, boolean: max of a and vBoolean, textNumber: max of a and vTextNumber, textInvalid: max of a and vTextInvalid, tagPi: max of a and vTagPi, tagNan: max of a and vTagNan, tagInfinity: max of a and vTagInfinity, vector: max of a and vVector, point: max of a and vPoint)
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

## Test: max integer

This runtime case exercises “max integer” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0124
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "max integer.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdr
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
  emit Done(nothing: max of a and vNothing, integer: max of a and vInteger, float: max of a and vFloat, percentage: max of a and vPercentage, meter: max of a and vMeter, boolean: max of a and vBoolean, textNumber: max of a and vTextNumber, textInvalid: max of a and vTextInvalid, tagPi: max of a and vTagPi, tagNan: max of a and vTagNan, tagInfinity: max of a and vTagInfinity, vector: max of a and vVector, point: max of a and vPoint)
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
              value: "10"
          - name: "meter"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "textNumber"
            value:
              type: ":Text"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "hello"
          - name: "tagPi"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "tagNan"
            value:
              type: ":Tag"
              value: "nan"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "point"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
```

---

## Test: max float

This runtime case exercises “max float” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0125
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "max float.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixds
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
  emit Done(nothing: max of a and vNothing, integer: max of a and vInteger, float: max of a and vFloat, percentage: max of a and vPercentage, meter: max of a and vMeter, boolean: max of a and vBoolean, textNumber: max of a and vTextNumber, textInvalid: max of a and vTextInvalid, tagPi: max of a and vTagPi, tagNan: max of a and vTagNan, tagInfinity: max of a and vTagInfinity, vector: max of a and vVector, point: max of a and vPoint)
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
              value: "10.3"
          - name: "float"
            value:
              type: ":Number.binary64"
              value: "10.3"
          - name: "percentage"
            value:
              type: ":Number.binary64"
              value: "10.3"
          - name: "meter"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Number.binary64"
              value: "10.3"
          - name: "textNumber"
            value:
              type: ":Text"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "hello"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "10.3"
          - name: "tagNan"
            value:
              type: ":Tag"
              value: "nan"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "point"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
```

---

## Test: max percentage

This runtime case exercises “max percentage” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0126
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "max percentage.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdt
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
  emit Done(nothing: max of a and vNothing, integer: max of a and vInteger, float: max of a and vFloat, percentage: max of a and vPercentage, meter: max of a and vMeter, boolean: max of a and vBoolean, textNumber: max of a and vTextNumber, textInvalid: max of a and vTextInvalid, tagPi: max of a and vTagPi, tagNan: max of a and vTagNan, tagInfinity: max of a and vTagInfinity, vector: max of a and vVector, point: max of a and vPoint)
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
              type: ":Percentage"
              value: "0.1"
          - name: "meter"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: true
          - name: "textNumber"
            value:
              type: ":Text"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "hello"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":Tag"
              value: "nan"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "point"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
```

---

## Test: max meter

This runtime case exercises “max meter” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0127
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "max meter.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdu
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
  emit Done(nothing: max of a and vNothing, integer: max of a and vInteger, float: max of a and vFloat, percentage: max of a and vPercentage, meter: max of a and vMeter, boolean: max of a and vBoolean, textNumber: max of a and vTextNumber, textInvalid: max of a and vTextInvalid, tagPi: max of a and vTagPi, tagNan: max of a and vTagNan, tagInfinity: max of a and vTagInfinity, vector: max of a and vVector, point: max of a and vPoint)
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
              type: ":Quantity.int64"
              value: "10"
              unit: ":meter"
          - name: "boolean"
            value:
              type: ":Nothing"
          - name: "textNumber"
            value:
              type: ":Text"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "hello"
          - name: "tagPi"
            value:
              type: ":Nothing"
          - name: "tagNan"
            value:
              type: ":Tag"
              value: "nan"
          - name: "tagInfinity"
            value:
              type: ":Nothing"
          - name: "vector"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "point"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
```

---

## Test: max boolean

This runtime case exercises “max boolean” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0128
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "max boolean.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdv
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
  emit Done(nothing: max of a and vNothing, integer: max of a and vInteger, float: max of a and vFloat, percentage: max of a and vPercentage, meter: max of a and vMeter, boolean: max of a and vBoolean, textNumber: max of a and vTextNumber, textInvalid: max of a and vTextInvalid, tagPi: max of a and vTagPi, tagNan: max of a and vTagNan, tagInfinity: max of a and vTagInfinity, vector: max of a and vVector, point: max of a and vPoint)
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
              type: ":Boolean"
              value: true
          - name: "meter"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: true
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
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":Boolean"
              value: true
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: max textNumber

This runtime case exercises “max textNumber” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0129
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "max textNumber.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdw
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
  emit Done(nothing: max of a and vNothing, integer: max of a and vInteger, float: max of a and vFloat, percentage: max of a and vPercentage, meter: max of a and vMeter, boolean: max of a and vBoolean, textNumber: max of a and vTextNumber, textInvalid: max of a and vTextInvalid, tagPi: max of a and vTagPi, tagNan: max of a and vTagNan, tagInfinity: max of a and vTagInfinity, vector: max of a and vVector, point: max of a and vPoint)
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
              type: ":Text"
              value: "10.3"
          - name: "float"
            value:
              type: ":Text"
              value: "10.3"
          - name: "percentage"
            value:
              type: ":Text"
              value: "10.3"
          - name: "meter"
            value:
              type: ":Text"
              value: "10.3"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: true
          - name: "textNumber"
            value:
              type: ":Text"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "hello"
          - name: "tagPi"
            value:
              type: ":Text"
              value: "10.3"
          - name: "tagNan"
            value:
              type: ":Tag"
              value: "nan"
          - name: "tagInfinity"
            value:
              type: ":Text"
              value: "10.3"
          - name: "vector"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "point"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
```

---

## Test: max textInvalid

This runtime case exercises “max textInvalid” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0130
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "max textInvalid.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdx
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
  emit Done(nothing: max of a and vNothing, integer: max of a and vInteger, float: max of a and vFloat, percentage: max of a and vPercentage, meter: max of a and vMeter, boolean: max of a and vBoolean, textNumber: max of a and vTextNumber, textInvalid: max of a and vTextInvalid, tagPi: max of a and vTagPi, tagNan: max of a and vTagNan, tagInfinity: max of a and vTagInfinity, vector: max of a and vVector, point: max of a and vPoint)
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
              type: ":Text"
              value: "hello"
          - name: "float"
            value:
              type: ":Text"
              value: "hello"
          - name: "percentage"
            value:
              type: ":Text"
              value: "hello"
          - name: "meter"
            value:
              type: ":Text"
              value: "hello"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: true
          - name: "textNumber"
            value:
              type: ":Text"
              value: "hello"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "hello"
          - name: "tagPi"
            value:
              type: ":Text"
              value: "hello"
          - name: "tagNan"
            value:
              type: ":Tag"
              value: "nan"
          - name: "tagInfinity"
            value:
              type: ":Text"
              value: "hello"
          - name: "vector"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "point"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
```

---

## Test: max tagPi

This runtime case exercises “max tagPi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0131
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "max tagPi.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdy
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
  emit Done(nothing: max of a and vNothing, integer: max of a and vInteger, float: max of a and vFloat, percentage: max of a and vPercentage, meter: max of a and vMeter, boolean: max of a and vBoolean, textNumber: max of a and vTextNumber, textInvalid: max of a and vTextInvalid, tagPi: max of a and vTagPi, tagNan: max of a and vTagNan, tagInfinity: max of a and vTagInfinity, vector: max of a and vVector, point: max of a and vPoint)
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
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "meter"
            value:
              type: ":Nothing"
          - name: "boolean"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "textNumber"
            value:
              type: ":Text"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "hello"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "tagNan"
            value:
              type: ":Tag"
              value: "nan"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "point"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
```

---

## Test: max tagNan

This runtime case exercises “max tagNan” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0132
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "max tagNan.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixdz
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
  emit Done(nothing: max of a and vNothing, integer: max of a and vInteger, float: max of a and vFloat, percentage: max of a and vPercentage, meter: max of a and vMeter, boolean: max of a and vBoolean, textNumber: max of a and vTextNumber, textInvalid: max of a and vTextInvalid, tagPi: max of a and vTagPi, tagNan: max of a and vTagNan, tagInfinity: max of a and vTagInfinity, vector: max of a and vVector, point: max of a and vPoint)
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
              type: ":Tag"
              value: "nan"
          - name: "float"
            value:
              type: ":Tag"
              value: "nan"
          - name: "percentage"
            value:
              type: ":Tag"
              value: "nan"
          - name: "meter"
            value:
              type: ":Tag"
              value: "nan"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: true
          - name: "textNumber"
            value:
              type: ":Tag"
              value: "nan"
          - name: "textInvalid"
            value:
              type: ":Tag"
              value: "nan"
          - name: "tagPi"
            value:
              type: ":Tag"
              value: "nan"
          - name: "tagNan"
            value:
              type: ":Tag"
              value: "nan"
          - name: "tagInfinity"
            value:
              type: ":Tag"
              value: "nan"
          - name: "vector"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "point"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
```

---

## Test: max tagInfinity

This runtime case exercises “max tagInfinity” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0133
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "max tagInfinity.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixea
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
  emit Done(nothing: max of a and vNothing, integer: max of a and vInteger, float: max of a and vFloat, percentage: max of a and vPercentage, meter: max of a and vMeter, boolean: max of a and vBoolean, textNumber: max of a and vTextNumber, textInvalid: max of a and vTextInvalid, tagPi: max of a and vTagPi, tagNan: max of a and vTagNan, tagInfinity: max of a and vTagInfinity, vector: max of a and vVector, point: max of a and vPoint)
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
              type: ":Text"
              value: "10.3"
          - name: "textInvalid"
            value:
              type: ":Text"
              value: "hello"
          - name: "tagPi"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "tagNan"
            value:
              type: ":Tag"
              value: "nan"
          - name: "tagInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "vector"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "point"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
```

---

## Test: max vector

This runtime case exercises “max vector” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0134
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "max vector.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixeb
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
  emit Done(nothing: max of a and vNothing, integer: max of a and vInteger, float: max of a and vFloat, percentage: max of a and vPercentage, meter: max of a and vMeter, boolean: max of a and vBoolean, textNumber: max of a and vTextNumber, textInvalid: max of a and vTextInvalid, tagPi: max of a and vTagPi, tagNan: max of a and vTagNan, tagInfinity: max of a and vTagInfinity, vector: max of a and vVector, point: max of a and vPoint)
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
              x: "1"
              y: "2"
              z: "3"
          - name: "float"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "percentage"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "meter"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: true
          - name: "textNumber"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "textInvalid"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "tagPi"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "tagNan"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "tagInfinity"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "vector"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "point"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
```

---

## Test: max point

This runtime case exercises “max point” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0135
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "max point.ges"
    program: main
```

### Source code under test

```ges
module atomicmathmatrixec
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
  emit Done(nothing: max of a and vNothing, integer: max of a and vInteger, float: max of a and vFloat, percentage: max of a and vPercentage, meter: max of a and vMeter, boolean: max of a and vBoolean, textNumber: max of a and vTextNumber, textInvalid: max of a and vTextInvalid, tagPi: max of a and vTagPi, tagNan: max of a and vTagNan, tagInfinity: max of a and vTagInfinity, vector: max of a and vVector, point: max of a and vPoint)
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
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
          - name: "float"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
          - name: "percentage"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
          - name: "meter"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
          - name: "boolean"
            value:
              type: ":Boolean"
              value: true
          - name: "textNumber"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
          - name: "textInvalid"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
          - name: "tagPi"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
          - name: "tagNan"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
          - name: "tagInfinity"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
          - name: "vector"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
          - name: "point"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
```
