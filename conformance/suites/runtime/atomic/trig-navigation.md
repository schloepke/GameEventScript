---
formatVersion: 1
suiteId: "runtime.atomic.trig-navigation"
title: "RuntimeAtomicTrigNavigation"
categories: [conformance]
---

# RuntimeAtomicTrigNavigation

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite isolates trigonometric and navigation operations, including angle units and wrapping.

---

## Test: trigonometric intrinsics

This runtime case exercises “trigonometric intrinsics” and verifies the declared messages, values, and execution result.

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
  - name: "trigonometric intrinsics.ges"
    program: main
```

### Source code under test

```ges
module atomictrigintrinsics
on Start {
  let missing be nothing
  emit Done(sinZero: sin 0, sinParen: sin(0), sinDegree: sin 90°, cosPi: cos pi, tanZero: tan 0, asinOne: asin 1, acosOne: acos 1, atanOne: atan 1, atanTwoQuarter: atan2(1, 1), atanTwoZero: atan2(0, 0), invalidUnit: sin 10m, invalidText: sin 'hello', invalidTag: sin #angle, invalidDomain: asin 2, missing: sin missing)
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
          - name: "sinZero"
            value:
              type: ":Number.binary64"
              value: "0"
          - name: "sinParen"
            value:
              type: ":Number.binary64"
              value: "0"
          - name: "sinDegree"
            value:
              type: ":Number.binary64"
              value: "1"
          - name: "cosPi"
            value:
              type: ":Number.binary64"
              value: "-1"
          - name: "tanZero"
            value:
              type: ":Number.binary64"
              value: "0"
          - name: "asinOne"
            value:
              type: ":Number.binary64"
              value: "1.5707963267949"
          - name: "acosOne"
            value:
              type: ":Number.binary64"
              value: "0"
          - name: "atanOne"
            value:
              type: ":Number.binary64"
              value: "0.785398163397448"
          - name: "atanTwoQuarter"
            value:
              type: ":Number.binary64"
              value: "0.785398163397448"
          - name: "atanTwoZero"
            value:
              type: ":Number.binary64"
              value: "0"
          - name: "invalidUnit"
            value:
              type: ":Nothing"
          - name: "invalidText"
            value:
              type: ":Nothing"
          - name: "invalidTag"
            value:
              type: ":Nothing"
          - name: "invalidDomain"
            value:
              type: ":Nothing"
          - name: "missing"
            value:
              type: ":Nothing"
```

---

## Test: hypot distance and squared distance intrinsics

This runtime case exercises “hypot distance and squared distance intrinsics” and verifies the declared messages, values, and execution result.

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
  - name: "hypot distance and squared distance intrinsics.ges"
    program: main
```

### Source code under test

```ges
module atomicdistanceintrinsics
on Start {
  let pointA be :Point(1m, 2m, 2m)
  let pointB be :Point(4m, 6m, 2m)
  emit Done(hypotTwo: hypot(3, 4), hypotThree: hypot(3, 4, 12), hypotUnit: hypot(3m, 4m), hypotMismatch: hypot(3m, 4s), scalarDistance: distance(10m, 4m), coordinateDistanceTwo: distance(0, 0, 3, 4), coordinateDistanceThree: distance(0, 0, 0, 2, 3, 6), pointDistance: distance(pointA, pointB), scalarDistanceSquared: distance squared(10m, 4m), coordinateDistanceSquaredTwo: distance squared(0, 0, 3, 4), coordinateDistanceSquaredThree: distance squared(0, 0, 0, 2, 3, 6), pointDistanceSquared: distance squared(pointA, pointB), invalidTextDistance: distance('a', 'b'))
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
          - name: "hypotTwo"
            value:
              type: ":Number.binary64"
              value: "5"
          - name: "hypotThree"
            value:
              type: ":Number.binary64"
              value: "13"
          - name: "hypotUnit"
            value:
              type: ":Quantity.binary64"
              unit: ":meter"
              value: "5"
          - name: "hypotMismatch"
            value:
              type: ":Nothing"
          - name: "scalarDistance"
            value:
              type: ":Quantity.binary64"
              unit: ":meter"
              value: "6"
          - name: "coordinateDistanceTwo"
            value:
              type: ":Number.binary64"
              value: "5"
          - name: "coordinateDistanceThree"
            value:
              type: ":Number.binary64"
              value: "7"
          - name: "pointDistance"
            value:
              type: ":Quantity.binary64"
              unit: ":meter"
              value: "5"
          - name: "scalarDistanceSquared"
            value:
              type: ":Number.binary64"
              value: "36"
          - name: "coordinateDistanceSquaredTwo"
            value:
              type: ":Number.binary64"
              value: "25"
          - name: "coordinateDistanceSquaredThree"
            value:
              type: ":Number.binary64"
              value: "49"
          - name: "pointDistanceSquared"
            value:
              type: ":Number.binary64"
              value: "25"
          - name: "invalidTextDistance"
            value:
              type: ":Nothing"
```

---

## Test: vector navigation intrinsics

This runtime case exercises “vector navigation intrinsics” and verifies the declared messages, values, and execution result.

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
  - name: "vector navigation intrinsics.ges"
    program: main
```

### Source code under test

```ges
module atomicvectornavigationintrinsics
on Start {
  let a be :Vector(1, 2, 3)
  let b be :Vector(4, 5, 6)
  let right be :Vector(1, 0, 0)
  let up be :Vector(0, 1, 0)
  emit Done(lengthObject: length squared(a), lengthTwo: length squared(3, 4), lengthThree: length squared(1, 2, 2), normalizeObject: normalize(:Vector(3, 0, 4)), normalizeTwo: normalize(3, 4), normalizeThree: normalize(0, 3, 4), normalizeZero: normalize(0, 0), dotObject: dot(a, b), dotTwo: dot(1, 2, 3, 4), dotThree: dot(1, 2, 3, 4, 5, 6), crossObject: cross(right, up), crossTwo: cross(1, 0, 0, 1), crossThree: cross(1, 0, 0, 0, 1, 0), angleObject: angle between(right, up), angleTwo: angle between(1, 0, 0, 1), angleThree: angle between(1, 0, 0, 0, 1, 0), angleZero: angle between(0, 0, 1, 0))
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
          - name: "lengthObject"
            value:
              type: ":Number.binary64"
              value: "14"
          - name: "lengthTwo"
            value:
              type: ":Number.binary64"
              value: "25"
          - name: "lengthThree"
            value:
              type: ":Number.binary64"
              value: "9"
          - name: "normalizeObject"
            value:
              type: ":Vector"
              x: "0.6"
              y: "0"
              z: "0.8"
          - name: "normalizeTwo"
            value:
              type: ":Vector"
              x: "0.6"
              y: "0.8"
              z: "0"
          - name: "normalizeThree"
            value:
              type: ":Vector"
              x: "0"
              y: "0.6"
              z: "0.8"
          - name: "normalizeZero"
            value:
              type: ":Nothing"
          - name: "dotObject"
            value:
              type: ":Number.binary64"
              value: "32"
          - name: "dotTwo"
            value:
              type: ":Number.binary64"
              value: "11"
          - name: "dotThree"
            value:
              type: ":Number.binary64"
              value: "32"
          - name: "crossObject"
            value:
              type: ":Vector"
              x: "0"
              y: "0"
              z: "1"
          - name: "crossTwo"
            value:
              type: ":Number.binary64"
              value: "1"
          - name: "crossThree"
            value:
              type: ":Vector"
              x: "0"
              y: "0"
              z: "1"
          - name: "angleObject"
            value:
              type: ":Number.binary64"
              value: "1.5707963267949"
          - name: "angleTwo"
            value:
              type: ":Number.binary64"
              value: "1.5707963267949"
          - name: "angleThree"
            value:
              type: ":Number.binary64"
              value: "1.5707963267949"
          - name: "angleZero"
            value:
              type: ":Nothing"
```
