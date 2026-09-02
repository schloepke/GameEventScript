---
formatVersion: 1
suiteId: "runtime.types-and-values.custom-and-spatial"
title: "Types and Values — Custom and Spatial Values"
categories: [conformance]
tags: [migrated-json-v1]
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
            type: ":text"
            value: "unit-1"
        - name: "tags"
          value:
            type: ":list"
            items:
              - type: ":tag"
                value: "Ready"
              - type: ":text"
                value: "frontline"
        - name: "stats"
          value:
            type: ":Unit"
            entries:
              - key: "hp"
                value:
                  type: ":integer"
                  value: "7"
              - key: "name"
                value:
                  type: ":text"
                  value: "Knight"
        - name: "maybe"
          value:
            type: ":nothing"
        - name: "flag"
          value:
            type: ":boolean"
            value: true
        - name: "percent"
          value:
            type: ":percentage"
            value: "0.25"
        - name: "heading"
          value:
            type: ":float"
            value: "90"
            unit: ":degree"
        - name: "position"
          value:
            type: ":vector"
            x: "10.5"
            y: "-2"
            z: "0"
        - name: "point"
          value:
            type: ":vector"
            x: "1"
            y: "2"
            z: "3"
        - name: "category"
          value:
            type: ":tag"
            value: "Infantry"
        - name: "points"
          value:
            type: ":list"
            items:
              - type: ":integer"
                value: "2"
              - type: ":integer"
                value: "1"
        - name: "span"
          value:
            type: ":range"
            from: "1"
            to: "3"
            step: "1"
    local:
      - name: "Echoed"
        args:
          - name: "name"
            value:
              type: ":text"
              value: "unit-1"
          - name: "tags"
            value:
              type: ":list"
              items:
                - type: ":tag"
                  value: "Ready"
                - type: ":text"
                  value: "frontline"
          - name: "stats"
            value:
              type: ":Unit"
              entries:
                - key: "hp"
                  value:
                    type: ":integer"
                    value: "7"
                - key: "name"
                  value:
                    type: ":text"
                    value: "Knight"
          - name: "maybe"
            value:
              type: ":nothing"
          - name: "flag"
            value:
              type: ":boolean"
              value: true
          - name: "percent"
            value:
              type: ":percentage"
              value: "0.25"
          - name: "heading"
            value:
              type: ":float"
              value: "90"
              unit: ":degree"
          - name: "position"
            value:
              type: ":vector"
              x: "10.5"
              y: "-2"
              z: "0"
          - name: "point"
            value:
              type: ":vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "category"
            value:
              type: ":tag"
              value: "Infantry"
          - name: "points"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "1"
          - name: "span"
            value:
              type: ":range"
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
record :gauge as {
  current: :number clamped between 0 and maximum,
  maximum: :number clamped between 0 and infinity,
  percentage: :percentage computed by
    0% when maximum <= 0,
    otherwise (current / maximum) as :percentage
}

on Start {
  let hp as :gauge be [current: 125, maximum: 100]
  emit Done(meter: hp, isMeter: hp is :gauge)
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
              type: ":gauge"
              entries:
                - key: "current"
                  value:
                    type: ":integer"
                    value: "100"
                - key: "maximum"
                  value:
                    type: ":integer"
                    value: "100"
                - key: "percentage"
                  value:
                    type: ":percentage"
                    value: "0.01"
          - name: "isMeter"
            value:
              type: ":boolean"
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
  let heading be :quantity(°)(180)
  let distanceValue be :quantity(m)(100)
  let duration be :quantity(s)(15)
  let product be :number(90°) * :number(100m)
  let ratio be :percentage(25)
  let textValue be :text(heading)
  emit Done(heading: heading, distance: distanceValue, duration: duration, product: product, ratio: ratio, textValue: textValue, headingIsDegree: heading is :quantity(°))
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
              type: ":integer"
              value: "180"
              unit: ":degree"
          - name: "distance"
            value:
              type: ":integer"
              value: "100"
              unit: ":meter"
          - name: "duration"
            value:
              type: ":integer"
              value: "15"
              unit: ":second"
          - name: "product"
            value:
              type: ":float"
              value: "NaN"
          - name: "ratio"
            value:
              type: ":percentage"
              value: "0.25"
          - name: "textValue"
            value:
              type: ":text"
              value: "180\u00B0"
          - name: "headingIsDegree"
            value:
              type: ":boolean"
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
  let position be :vector(10.5, -2)
  let labeledPosition be :vector(x: 3, y: 4)
  let yOnly be :vector(y: 10m)
  let zero_2 be :vector()
  let point be :vector(1, 2, 3)
  let labeledPoint be :vector(x: 4, y: 5, z: 6)
  let zOnly be :vector(z: 7m)
  let yzOnly be :vector(y: 2m, z: 3m)
  let zero_3 be :vector()
  let lifted be :vector(position)
  let liftedWithZ be :vector(position, 7)
  let flattened be :vector(labeledPoint)
  emit Done(position: position, labeledPosition: labeledPosition, yOnly: yOnly, zero_2: zero_2, point: point, labeledPoint: labeledPoint, zOnly: zOnly, yzOnly: yzOnly, zero_3: zero_3, lifted: lifted, liftedWithZ: liftedWithZ, flattened: flattened, x: position.x, y: labeledPosition.y, z: point.z, labeledX: labeledPoint.x)
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
              type: ":vector"
              x: "10.5"
              y: "-2"
              z: "0"
          - name: "labeledPosition"
            value:
              type: ":vector"
              x: "3"
              y: "4"
              z: "0"
          - name: "yOnly"
            value:
              type: ":vector"
              x: "0"
              y: "10"
              z: "0"
              unit: ":meter"
          - name: "zero_2"
            value:
              type: ":vector"
              x: "0"
              y: "0"
              z: "0"
          - name: "point"
            value:
              type: ":vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "labeledPoint"
            value:
              type: ":vector"
              x: "4"
              y: "5"
              z: "6"
          - name: "zOnly"
            value:
              type: ":vector"
              x: "0"
              y: "0"
              z: "7"
              unit: ":meter"
          - name: "yzOnly"
            value:
              type: ":vector"
              x: "0"
              y: "2"
              z: "3"
              unit: ":meter"
          - name: "zero_3"
            value:
              type: ":vector"
              x: "0"
              y: "0"
              z: "0"
          - name: "lifted"
            value:
              type: ":vector"
              x: "10.5"
              y: "-2"
              z: "0"
          - name: "liftedWithZ"
            value:
              type: ":vector"
              x: "10.5"
              y: "-2"
              z: "7"
          - name: "flattened"
            value:
              type: ":vector"
              x: "4"
              y: "5"
              z: "6"
          - name: "x"
            value:
              type: ":float"
              value: "10.5"
          - name: "y"
            value:
              type: ":float"
              value: "4"
          - name: "z"
            value:
              type: ":float"
              value: "3"
          - name: "labeledX"
            value:
              type: ":float"
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
record :gauge as {
  current: :number clamped between 0 and maximum,
  maximum: :number clamped between 0 and infinity,
  percentage: :percentage computed by
    0% when maximum <= 0,
    otherwise (current / maximum) as :percentage
}

on Start {
  let hp be :gauge(current: 125, maximum: 100)
  emit Done(meter: hp, current: hp.current, percentage: hp.percentage, isMeter: hp is :gauge)
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
              type: ":gauge"
              entries:
                - key: "current"
                  value:
                    type: ":integer"
                    value: "100"
                - key: "maximum"
                  value:
                    type: ":integer"
                    value: "100"
                - key: "percentage"
                  value:
                    type: ":percentage"
                    value: "0.01"
          - name: "current"
            value:
              type: ":integer"
              value: "100"
          - name: "percentage"
            value:
              type: ":percentage"
              value: "0.01"
          - name: "isMeter"
            value:
              type: ":boolean"
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
  let origin be :point(10m, 20m)
  let delta be :vector(3m, -5m)
  let moved be origin + delta
  let back be moved - delta
  let between be moved - origin
  let labeled be :point(x: 3, y: 4)
  let yOnly be :point(y: 10m)
  let zero_2 be :point()
  let p_3 be :point(origin, 7m)
  let flat be :point(p_3)
  let badAdd be origin + moved
  let badScale be origin * 2
  let badAbs be abs origin
  emit Done(origin: origin, moved: moved, back: back, between: between, labeled: labeled, yOnly: yOnly, zero_2: zero_2, p_3: p_3, flat: flat, x: moved.x, z: p_3.z, isPoint: moved is :point, badAdd: badAdd, badScale: badScale, badAbs: badAbs)
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
              type: ":point"
              x: "10"
              y: "20"
              z: "0"
              unit: ":meter"
          - name: "moved"
            value:
              type: ":point"
              x: "13"
              y: "15"
              z: "0"
              unit: ":meter"
          - name: "back"
            value:
              type: ":point"
              x: "10"
              y: "20"
              z: "0"
              unit: ":meter"
          - name: "between"
            value:
              type: ":vector"
              x: "3"
              y: "-5"
              z: "0"
              unit: ":meter"
          - name: "labeled"
            value:
              type: ":point"
              x: "3"
              y: "4"
              z: "0"
          - name: "yOnly"
            value:
              type: ":point"
              x: "0"
              y: "10"
              z: "0"
              unit: ":meter"
          - name: "zero_2"
            value:
              type: ":point"
              x: "0"
              y: "0"
              z: "0"
          - name: "p_3"
            value:
              type: ":point"
              x: "10"
              y: "20"
              z: "7"
              unit: ":meter"
          - name: "flat"
            value:
              type: ":point"
              x: "10"
              y: "20"
              z: "7"
              unit: ":meter"
          - name: "x"
            value:
              type: ":float"
              value: "13"
              unit: ":meter"
          - name: "z"
            value:
              type: ":float"
              value: "7"
              unit: ":meter"
          - name: "isPoint"
            value:
              type: ":boolean"
              value: true
          - name: "badAdd"
            value:
              type: ":float"
              value: "NaN"
          - name: "badScale"
            value:
              type: ":float"
              value: "NaN"
          - name: "badAbs"
            value:
              type: ":float"
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
  let position as :vector be [x: 10.5, y: -2]
  let point as :vector be [1, 2, 3]
  let lifted as :vector be position
  let flattened as :vector be point
  let vectorList as :list be position
  let vectorDictionary as :map be point
  emit Done(position: position, point: point, lifted: lifted, flattened: flattened, x: position.x, y: position[#y], z: point.z, listFirst: vectorList[1], dictZ: vectorDictionary.z, isVectorPosition: position is :vector, isVectorPoint: point is :vector, vectorIsMap: position is :map)
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
              type: ":vector"
              x: "10.5"
              y: "-2"
              z: "0"
          - name: "point"
            value:
              type: ":vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "lifted"
            value:
              type: ":vector"
              x: "10.5"
              y: "-2"
              z: "0"
          - name: "flattened"
            value:
              type: ":vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "x"
            value:
              type: ":float"
              value: "10.5"
          - name: "y"
            value:
              type: ":float"
              value: "-2"
          - name: "z"
            value:
              type: ":float"
              value: "3"
          - name: "listFirst"
            value:
              type: ":float"
              value: "10.5"
          - name: "dictZ"
            value:
              type: ":float"
              value: "3"
          - name: "isVectorPosition"
            value:
              type: ":boolean"
              value: true
          - name: "isVectorPoint"
            value:
              type: ":boolean"
              value: true
          - name: "vectorIsMap"
            value:
              type: ":boolean"
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
  let position be :vector(3m, 4m)
  let rawPosition be :number(position)
  let rawPositionCast as :number be position
  let remetered be :quantity(m)(rawPosition)
  let sameMeter be :quantity(m)(position)
  let wrongUnitVector be :quantity(s)(position)
  let wrongScalarUnit be :quantity(m)(15s)
  let lifted as :vector be position
  let liftedConstructed be :vector(position)
  let liftedConstructedWithZ be :vector(position, 20m)
  let flattened as :vector be :vector(1m, 2m, 3m)
  let flattenedConstructed be :vector(:vector(1m, 2m, 3m))
  let vectorList as :list be position
  let vectorDictionary as :map be position
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
              type: ":vector"
              x: "3"
              y: "4"
              z: "0"
              unit: ":meter"
          - name: "x"
            value:
              type: ":float"
              value: "3"
              unit: ":meter"
          - name: "y"
            value:
              type: ":float"
              value: "4"
              unit: ":meter"
          - name: "listY"
            value:
              type: ":float"
              value: "4"
              unit: ":meter"
          - name: "dictX"
            value:
              type: ":float"
              value: "3"
              unit: ":meter"
          - name: "rawPosition"
            value:
              type: ":nothing"
          - name: "rawPositionCast"
            value:
              type: ":nothing"
          - name: "remetered"
            value:
              type: ":float"
              value: "NaN"
          - name: "sameMeter"
            value:
              type: ":vector"
              x: "3"
              y: "4"
              z: "0"
              unit: ":meter"
          - name: "wrongUnitVector"
            value:
              type: ":float"
              value: "NaN"
          - name: "wrongScalarUnit"
            value:
              type: ":float"
              value: "NaN"
          - name: "lifted"
            value:
              type: ":vector"
              x: "3"
              y: "4"
              z: "0"
              unit: ":meter"
          - name: "liftedConstructed"
            value:
              type: ":vector"
              x: "3"
              y: "4"
              z: "0"
              unit: ":meter"
          - name: "liftedConstructedWithZ"
            value:
              type: ":vector"
              x: "3"
              y: "4"
              z: "20"
              unit: ":meter"
          - name: "flattened"
            value:
              type: ":vector"
              x: "1"
              y: "2"
              z: "3"
              unit: ":meter"
          - name: "flattenedConstructed"
            value:
              type: ":vector"
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
  let a be :vector(1m, 2m)
  let b be :vector(3m, 4m)
  let c be :vector(1, 2, 2)
  emit Done(sum: a + b, difference: a - b, negated: -a, scaleRight: a * 2, scaleLeft: 2 * a, percentScale: a * 50%, divided: b / 2, unitlessScaled: :vector(1, 2) * 5m, unitDivided: a / 1m, length_2: abs :vector(3m, 4m), length_3: abs c)
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
              type: ":vector"
              x: "4"
              y: "6"
              z: "0"
              unit: ":meter"
          - name: "difference"
            value:
              type: ":vector"
              x: "-2"
              y: "-2"
              z: "0"
              unit: ":meter"
          - name: "negated"
            value:
              type: ":vector"
              x: "-1"
              y: "-2"
              z: "0"
              unit: ":meter"
          - name: "scaleRight"
            value:
              type: ":vector"
              x: "2"
              y: "4"
              z: "0"
              unit: ":meter"
          - name: "scaleLeft"
            value:
              type: ":vector"
              x: "2"
              y: "4"
              z: "0"
              unit: ":meter"
          - name: "percentScale"
            value:
              type: ":vector"
              x: "0.5"
              y: "1"
              z: "0"
              unit: ":meter"
          - name: "divided"
            value:
              type: ":vector"
              x: "1.5"
              y: "2"
              z: "0"
              unit: ":meter"
          - name: "unitlessScaled"
            value:
              type: ":vector"
              x: "5"
              y: "10"
              z: "0"
              unit: ":meter"
          - name: "unitDivided"
            value:
              type: ":vector"
              x: "1"
              y: "2"
              z: "0"
          - name: "length_2"
            value:
              type: ":float"
              value: "5"
              unit: ":meter"
          - name: "length_3"
            value:
              type: ":float"
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
  emit Done(mixedUnits: :vector(10m, 20s), mixedUnitless: :vector(10, 20m), partialMixedUnit: :vector(y: 20m, z: 30), liftedMixedUnit: :vector(:vector(10m, 20m), 20), vectorProduct: :vector(1, 2) * :vector(3, 4), scalarDivide: 10 / :vector(1, 2), dimensionAdd: :vector(1, 2) + :vector(1, 2, 3), incompatibleScalar: :vector(1m, 2m) * 5s, divideZero: :vector(1, 2) / 0, vectorModulo: :vector(1, 2) mod 2)
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
              type: ":float"
              value: "NaN"
          - name: "mixedUnitless"
            value:
              type: ":float"
              value: "NaN"
          - name: "partialMixedUnit"
            value:
              type: ":float"
              value: "NaN"
          - name: "liftedMixedUnit"
            value:
              type: ":float"
              value: "NaN"
          - name: "vectorProduct"
            value:
              type: ":float"
              value: "NaN"
          - name: "scalarDivide"
            value:
              type: ":float"
              value: "NaN"
          - name: "dimensionAdd"
            value:
              type: ":vector"
              x: "2"
              y: "4"
              z: "3"
          - name: "incompatibleScalar"
            value:
              type: ":float"
              value: "NaN"
          - name: "divideZero"
            value:
              type: ":float"
              value: "NaN"
          - name: "vectorModulo"
            value:
              type: ":float"
              value: "NaN"
```
