---
formatVersion: 1
suiteId: "runtime.types-and-values.custom-and-spatial"
title: "Types and Values — Custom and Spatial Values"
categories: [conformance]
---

# Types and Values — Custom and Spatial Values

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers messages, records, constructors, vectors, points, and spatial arithmetic.

---

## Test: typed message values roundtrip

This runtime case exercises “typed message values roundtrip” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0001
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "typed message values roundtrip.ges"
    program: main
```

### Source code under test

```ges
on Echo(name, tags, stats, maybe, flag, percent, heading, position, point, category, points, span) {
  emit Echoed(name: name, tags: tags, stats: stats, maybe: maybe, flag: flag, percent: percent, heading: heading, position: position, point: point, category: category, points: points, span: span)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Echo | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "name"
          value:
            type: ":Text"
            value: "unit-1"
        - name: "tags"
          value:
            type: ":List"
            items:
              - type: ":Tag"
                value: "ready"
              - type: ":Text"
                value: "frontline"
        - name: "stats"
          value:
            type: ":Unit"
            entries:
              - key: "hp"
                value:
                  type: ":Number.int64"
                  value: "7"
              - key: "name"
                value:
                  type: ":Text"
                  value: "Knight"
        - name: "maybe"
          value:
            type: ":Nothing"
        - name: "flag"
          value:
            type: ":Boolean"
            value: true
        - name: "percent"
          value:
            type: ":Percentage"
            value: "0.25"
        - name: "heading"
          value:
            type: ":Quantity.binary64"
            value: "90"
            unit: ":degree"
        - name: "position"
          value:
            type: ":Vector"
            x: "10.5"
            y: "-2"
            z: "0"
        - name: "point"
          value:
            type: ":Vector"
            x: "1"
            y: "2"
            z: "3"
        - name: "category"
          value:
            type: ":Tag"
            value: "infantry"
        - name: "points"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "2"
              - type: ":Number.int64"
                value: "1"
        - name: "span"
          value:
            type: ":Range.int64"
            from: "1"
            to: "3"
            step: "1"
    local:
      - name: "Echoed"
        args:
          - name: "name"
            value:
              type: ":Text"
              value: "unit-1"
          - name: "tags"
            value:
              type: ":List"
              items:
                - type: ":Tag"
                  value: "ready"
                - type: ":Text"
                  value: "frontline"
          - name: "stats"
            value:
              type: ":Unit"
              entries:
                - key: "hp"
                  value:
                    type: ":Number.int64"
                    value: "7"
                - key: "name"
                  value:
                    type: ":Text"
                    value: "Knight"
          - name: "maybe"
            value:
              type: ":Nothing"
          - name: "flag"
            value:
              type: ":Boolean"
              value: true
          - name: "percent"
            value:
              type: ":Percentage"
              value: "0.25"
          - name: "heading"
            value:
              type: ":Quantity.binary64"
              value: "90"
              unit: ":degree"
          - name: "position"
            value:
              type: ":Vector"
              x: "10.5"
              y: "-2"
              z: "0"
          - name: "point"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "category"
            value:
              type: ":Tag"
              value: "infantry"
          - name: "points"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "1"
          - name: "span"
            value:
              type: ":Range.int64"
              from: "1"
              to: "3"
              step: "1"
```

---

## Test: computed custom type fields are materialized

This runtime case exercises “computed custom type fields are materialized” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0002
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "computed custom type fields are materialized.ges"
    program: main
```

### Source code under test

```ges
record :Gauge as {
  current: :Number clamped between 0 and maximum,
  maximum: :Number clamped between 0 and infinity,
  percentage: :Percentage computed by
    0% when maximum <= 0,
    otherwise (current / maximum) as :Percentage
}

on Start {
  let hp be ([current: 125, maximum: 100]) as :Gauge
  emit Done(meter: hp, isMeter: hp is :Gauge)
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
          - name: "meter"
            value:
              type: ":Gauge"
              entries:
                - key: "current"
                  value:
                    type: ":Number.int64"
                    value: "100"
                - key: "maximum"
                  value:
                    type: ":Number.int64"
                    value: "100"
                - key: "percentage"
                  value:
                    type: ":Percentage"
                    value: "0.01"
          - name: "isMeter"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: type constructors convert inline and construct units

This runtime case exercises “type constructors convert inline and construct units” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0003
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "type constructors convert inline and construct units.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let heading be :Quantity(°)(180)
  let distanceValue be :Quantity(m)(100)
  let duration be :Quantity(s)(15)
  let product be :Number(90°) * :Number(100m)
  let ratio be :Percentage(25)
  let textValue be :Text(heading)
  emit Done(heading: heading, distance: distanceValue, duration: duration, product: product, ratio: ratio, textValue: textValue, headingIsDegree: heading is :Quantity(°))
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
          - name: "heading"
            value:
              type: ":Quantity.int64"
              value: "180"
              unit: ":degree"
          - name: "distance"
            value:
              type: ":Quantity.int64"
              value: "100"
              unit: ":meter"
          - name: "duration"
            value:
              type: ":Quantity.int64"
              value: "15"
              unit: ":second"
          - name: "product"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "ratio"
            value:
              type: ":Percentage"
              value: "0.25"
          - name: "textValue"
            value:
              type: ":Text"
              value: "180\u00B0"
          - name: "headingIsDegree"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: vector type constructors expose components

This runtime case exercises “vector type constructors expose components” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0004
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "vector type constructors expose components.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let position be :Vector(10.5, -2)
  let labeledPosition be :Vector(x: 3, y: 4)
  let yOnly be :Vector(y: 10m)
  let zero2 be :Vector()
  let point be :Vector(1, 2, 3)
  let labeledPoint be :Vector(x: 4, y: 5, z: 6)
  let zOnly be :Vector(z: 7m)
  let yzOnly be :Vector(y: 2m, z: 3m)
  let zero3 be :Vector()
  let lifted be :Vector(position)
  let liftedWithZ be :Vector(position, 7)
  let flattened be :Vector(labeledPoint)
  emit Done(position: position, labeledPosition: labeledPosition, yOnly: yOnly, zero2: zero2, point: point, labeledPoint: labeledPoint, zOnly: zOnly, yzOnly: yzOnly, zero3: zero3, lifted: lifted, liftedWithZ: liftedWithZ, flattened: flattened, x: position.x, y: labeledPosition.y, z: point.z, labeledX: labeledPoint.x)
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
          - name: "position"
            value:
              type: ":Vector"
              x: "10.5"
              y: "-2"
              z: "0"
          - name: "labeledPosition"
            value:
              type: ":Vector"
              x: "3"
              y: "4"
              z: "0"
          - name: "yOnly"
            value:
              type: ":Vector"
              x: "0"
              y: "10"
              z: "0"
              unit: ":meter"
          - name: "zero2"
            value:
              type: ":Vector"
              x: "0"
              y: "0"
              z: "0"
          - name: "point"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "labeledPoint"
            value:
              type: ":Vector"
              x: "4"
              y: "5"
              z: "6"
          - name: "zOnly"
            value:
              type: ":Vector"
              x: "0"
              y: "0"
              z: "7"
              unit: ":meter"
          - name: "yzOnly"
            value:
              type: ":Vector"
              x: "0"
              y: "2"
              z: "3"
              unit: ":meter"
          - name: "zero3"
            value:
              type: ":Vector"
              x: "0"
              y: "0"
              z: "0"
          - name: "lifted"
            value:
              type: ":Vector"
              x: "10.5"
              y: "-2"
              z: "0"
          - name: "liftedWithZ"
            value:
              type: ":Vector"
              x: "10.5"
              y: "-2"
              z: "7"
          - name: "flattened"
            value:
              type: ":Vector"
              x: "4"
              y: "5"
              z: "6"
          - name: "x"
            value:
              type: ":Number.binary64"
              value: "10.5"
          - name: "y"
            value:
              type: ":Number.binary64"
              value: "4"
          - name: "z"
            value:
              type: ":Number.binary64"
              value: "3"
          - name: "labeledX"
            value:
              type: ":Number.binary64"
              value: "4"
```

---

## Test: custom type constructors apply clamp and computed fields

This runtime case exercises “custom type constructors apply clamp and computed fields” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0005
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "custom type constructors apply clamp and computed fields.ges"
    program: main
```

### Source code under test

```ges
record :Gauge as {
  current: :Number clamped between 0 and maximum,
  maximum: :Number clamped between 0 and infinity,
  percentage: :Percentage computed by
    0% when maximum <= 0,
    otherwise (current / maximum) as :Percentage
}

on Start {
  let hp be :Gauge(current: 125, maximum: 100)
  emit Done(meter: hp, current: hp.current, percentage: hp.percentage, isMeter: hp is :Gauge)
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
          - name: "meter"
            value:
              type: ":Gauge"
              entries:
                - key: "current"
                  value:
                    type: ":Number.int64"
                    value: "100"
                - key: "maximum"
                  value:
                    type: ":Number.int64"
                    value: "100"
                - key: "percentage"
                  value:
                    type: ":Percentage"
                    value: "0.01"
          - name: "current"
            value:
              type: ":Number.int64"
              value: "100"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0.01"
          - name: "isMeter"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: point constructors and affine arithmetic

This runtime case exercises “point constructors and affine arithmetic” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0006
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "point constructors and affine arithmetic.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let origin be :Point(10m, 20m)
  let delta be :Vector(3m, -5m)
  let moved be origin + delta
  let back be moved - delta
  let between be moved - origin
  let labeled be :Point(x: 3, y: 4)
  let yOnly be :Point(y: 10m)
  let zero2 be :Point()
  let p3 be :Point(origin, 7m)
  let flat be :Point(p3)
  let badAdd be origin + moved
  let badScale be origin * 2
  let badAbs be abs origin
  emit Done(origin: origin, moved: moved, back: back, between: between, labeled: labeled, yOnly: yOnly, zero2: zero2, p3: p3, flat: flat, x: moved.x, z: p3.z, isPoint: moved is :Point, badAdd: badAdd, badScale: badScale, badAbs: badAbs)
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
          - name: "origin"
            value:
              type: ":Point"
              x: "10"
              y: "20"
              z: "0"
              unit: ":meter"
          - name: "moved"
            value:
              type: ":Point"
              x: "13"
              y: "15"
              z: "0"
              unit: ":meter"
          - name: "back"
            value:
              type: ":Point"
              x: "10"
              y: "20"
              z: "0"
              unit: ":meter"
          - name: "between"
            value:
              type: ":Vector"
              x: "3"
              y: "-5"
              z: "0"
              unit: ":meter"
          - name: "labeled"
            value:
              type: ":Point"
              x: "3"
              y: "4"
              z: "0"
          - name: "yOnly"
            value:
              type: ":Point"
              x: "0"
              y: "10"
              z: "0"
              unit: ":meter"
          - name: "zero2"
            value:
              type: ":Point"
              x: "0"
              y: "0"
              z: "0"
          - name: "p3"
            value:
              type: ":Point"
              x: "10"
              y: "20"
              z: "7"
              unit: ":meter"
          - name: "flat"
            value:
              type: ":Point"
              x: "10"
              y: "20"
              z: "7"
              unit: ":meter"
          - name: "x"
            value:
              type: ":Quantity.binary64"
              value: "13"
              unit: ":meter"
          - name: "z"
            value:
              type: ":Quantity.binary64"
              value: "7"
              unit: ":meter"
          - name: "isPoint"
            value:
              type: ":Boolean"
              value: true
          - name: "badAdd"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "badScale"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "badAbs"
            value:
              type: ":Number.binary64"
              value: "NaN"
```

---

## Test: vector values cast expose components

This runtime case exercises “vector values cast expose components” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0015
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "vector values cast expose components.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let position be ([x: 10.5, y: -2]) as :Vector
  let point be ([1, 2, 3]) as :Vector
  let lifted be (position) as :Vector
  let flattened be (point) as :Vector
  let vectorList be (position) as :List
  let vectorDictionary be (point) as :Map
  emit Done(position: position, point: point, lifted: lifted, flattened: flattened, x: position.x, y: position[#y], z: point.z, listFirst: vectorList[1], dictZ: vectorDictionary.z, isVectorPosition: position is :Vector, isVectorPoint: point is :Vector, vectorIsMap: position is :Map)
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
          - name: "position"
            value:
              type: ":Vector"
              x: "10.5"
              y: "-2"
              z: "0"
          - name: "point"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "lifted"
            value:
              type: ":Vector"
              x: "10.5"
              y: "-2"
              z: "0"
          - name: "flattened"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "x"
            value:
              type: ":Number.binary64"
              value: "10.5"
          - name: "y"
            value:
              type: ":Number.binary64"
              value: "-2"
          - name: "z"
            value:
              type: ":Number.binary64"
              value: "3"
          - name: "listFirst"
            value:
              type: ":Number.binary64"
              value: "10.5"
          - name: "dictZ"
            value:
              type: ":Number.binary64"
              value: "3"
          - name: "isVectorPosition"
            value:
              type: ":Boolean"
              value: true
          - name: "isVectorPoint"
            value:
              type: ":Boolean"
              value: true
          - name: "vectorIsMap"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: vector units are preserved in components and conversions

This runtime case exercises “vector units are preserved in components and conversions” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0016
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "vector units are preserved in components and conversions.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let position be :Vector(3m, 4m)
  let rawPosition be :Number(position)
  let rawPositionCast be (position) as :Number
  let remetered be :Quantity(m)(rawPosition)
  let sameMeter be :Quantity(m)(position)
  let wrongUnitVector be :Quantity(s)(position)
  let wrongScalarUnit be :Quantity(m)(15s)
  let lifted be (position) as :Vector
  let liftedConstructed be :Vector(position)
  let liftedConstructedWithZ be :Vector(position, 20m)
  let flattened be (:Vector(1m, 2m, 3m)) as :Vector
  let flattenedConstructed be :Vector(:Vector(1m, 2m, 3m))
  let vectorList be (position) as :List
  let vectorDictionary be (position) as :Map
  emit Done(position: position, x: position.x, y: position[#y], listY: vectorList[2], dictX: vectorDictionary.x, rawPosition: rawPosition, rawPositionCast: rawPositionCast, remetered: remetered, sameMeter: sameMeter, wrongUnitVector: wrongUnitVector, wrongScalarUnit: wrongScalarUnit, lifted: lifted, liftedConstructed: liftedConstructed, liftedConstructedWithZ: liftedConstructedWithZ, flattened: flattened, flattenedConstructed: flattenedConstructed)
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
          - name: "position"
            value:
              type: ":Vector"
              x: "3"
              y: "4"
              z: "0"
              unit: ":meter"
          - name: "x"
            value:
              type: ":Quantity.binary64"
              value: "3"
              unit: ":meter"
          - name: "y"
            value:
              type: ":Quantity.binary64"
              value: "4"
              unit: ":meter"
          - name: "listY"
            value:
              type: ":Quantity.binary64"
              value: "4"
              unit: ":meter"
          - name: "dictX"
            value:
              type: ":Quantity.binary64"
              value: "3"
              unit: ":meter"
          - name: "rawPosition"
            value:
              type: ":Nothing"
          - name: "rawPositionCast"
            value:
              type: ":Nothing"
          - name: "remetered"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "sameMeter"
            value:
              type: ":Vector"
              x: "3"
              y: "4"
              z: "0"
              unit: ":meter"
          - name: "wrongUnitVector"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "wrongScalarUnit"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "lifted"
            value:
              type: ":Vector"
              x: "3"
              y: "4"
              z: "0"
              unit: ":meter"
          - name: "liftedConstructed"
            value:
              type: ":Vector"
              x: "3"
              y: "4"
              z: "0"
              unit: ":meter"
          - name: "liftedConstructedWithZ"
            value:
              type: ":Vector"
              x: "3"
              y: "4"
              z: "20"
              unit: ":meter"
          - name: "flattened"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
              unit: ":meter"
          - name: "flattenedConstructed"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
              unit: ":meter"
```

---

## Test: vector arithmetic supports addition scaling division and length

This runtime case exercises “vector arithmetic supports addition scaling division and length” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0017
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "vector arithmetic supports addition scaling division and length.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let a be :Vector(1m, 2m)
  let b be :Vector(3m, 4m)
  let c be :Vector(1, 2, 2)
  emit Done(sum: a + b, difference: a - b, negated: -a, scaleRight: a * 2, scaleLeft: 2 * a, percentScale: a * 50%, divided: b / 2, unitlessScaled: :Vector(1, 2) * 5m, unitDivided: a / 1m, length2: abs :Vector(3m, 4m), length3: abs c)
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
          - name: "sum"
            value:
              type: ":Vector"
              x: "4"
              y: "6"
              z: "0"
              unit: ":meter"
          - name: "difference"
            value:
              type: ":Vector"
              x: "-2"
              y: "-2"
              z: "0"
              unit: ":meter"
          - name: "negated"
            value:
              type: ":Vector"
              x: "-1"
              y: "-2"
              z: "0"
              unit: ":meter"
          - name: "scaleRight"
            value:
              type: ":Vector"
              x: "2"
              y: "4"
              z: "0"
              unit: ":meter"
          - name: "scaleLeft"
            value:
              type: ":Vector"
              x: "2"
              y: "4"
              z: "0"
              unit: ":meter"
          - name: "percentScale"
            value:
              type: ":Vector"
              x: "0.5"
              y: "1"
              z: "0"
              unit: ":meter"
          - name: "divided"
            value:
              type: ":Vector"
              x: "1.5"
              y: "2"
              z: "0"
              unit: ":meter"
          - name: "unitlessScaled"
            value:
              type: ":Vector"
              x: "5"
              y: "10"
              z: "0"
              unit: ":meter"
          - name: "unitDivided"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "0"
          - name: "length2"
            value:
              type: ":Quantity.binary64"
              value: "5"
              unit: ":meter"
          - name: "length3"
            value:
              type: ":Number.binary64"
              value: "3"
```

---

## Test: invalid vector arithmetic returns nan

This runtime case exercises “invalid vector arithmetic returns nan” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0018
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "invalid vector arithmetic returns nan.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Done(mixedUnits: :Vector(10m, 20s), mixedUnitless: :Vector(10, 20m), partialMixedUnit: :Vector(y: 20m, z: 30), liftedMixedUnit: :Vector(:Vector(10m, 20m), 20), vectorProduct: :Vector(1, 2) * :Vector(3, 4), scalarDivide: 10 / :Vector(1, 2), dimensionAdd: :Vector(1, 2) + :Vector(1, 2, 3), incompatibleScalar: :Vector(1m, 2m) * 5s, divideZero: :Vector(1, 2) / 0, vectorModulo: :Vector(1, 2) mod 2)
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
          - name: "mixedUnits"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "mixedUnitless"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "partialMixedUnit"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "liftedMixedUnit"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "vectorProduct"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "scalarDivide"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "dimensionAdd"
            value:
              type: ":Vector"
              x: "2"
              y: "4"
              z: "3"
          - name: "incompatibleScalar"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "divideZero"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "vectorModulo"
            value:
              type: ":Number.binary64"
              value: "NaN"
```
