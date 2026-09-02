---
formatVersion: 1
suiteId: "runtime.types-and-values"
title: "RuntimeTypesAndValues"
categories: [conformance]
tags: [migrated-json-v1]
---

# RuntimeTypesAndValues

Mechanically migrated from the former JSON conformance corpus.

## Test: typed message values roundtrip

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

```ges
on Echo(name, tags, stats, maybe, flag, percent, heading, position, point, category, points, span) {
  emit Echoed(name: name, tags: tags, stats: stats, maybe: maybe, flag: flag, percent: percent, heading: heading, position: position, point: point, category: category, points: points, span: span)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Echo | completion |  |

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

## Test: computed custom type fields are materialized

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

## Test: type constructors convert inline and construct units

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

## Test: vector type constructors expose components

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

## Test: custom type constructors apply clamp and computed fields

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

## Test: point constructors and affine arithmetic

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

## Test: integer arithmetic preserves integer results

```yaml
gesBlock: case
id: case-0007
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "integer arithmetic preserves integer results.ges"
    program: main
```

```ges
on Start {
  emit Done(add: 1 + 2, subtract: 5 - 3, multiply: 6 * 7, modulo: 7 mod 4, divide: 7 / 2, mixed: 7 * 0.5)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "add"
            value:
              type: ":integer"
              value: "3"
          - name: "subtract"
            value:
              type: ":integer"
              value: "2"
          - name: "multiply"
            value:
              type: ":integer"
              value: "42"
          - name: "modulo"
            value:
              type: ":integer"
              value: "3"
          - name: "divide"
            value:
              type: ":float"
              value: "3.5"
          - name: "mixed"
            value:
              type: ":float"
              value: "3.5"
```

## Test: integer division modulo and remainder semantics

```yaml
gesBlock: case
id: case-0008
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "integer division modulo and remainder semantics.ges"
    program: main
```

```ges
on Start(a, b) {
  emit Done(divPositive: a div b, divNegative: (0 - a) div b, divNegativeDivisor: a div (0 - b), floatDiv: 7.5 div 2, sameUnitDiv: 7m div 2m, unitDivScalar: 7m div 2, modPositive: a mod b, modNegative: (0 - a) mod b, modNegativeDivisor: a mod (0 - b), remPositive: a rem b, remNegative: (0 - a) rem b, remNegativeDivisor: a rem (0 - b), divZero: a div 0, remZero: a rem 0)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "a"
          value:
            type: ":integer"
            value: "7"
        - name: "b"
          value:
            type: ":integer"
            value: "3"
    local:
      - name: "Done"
        args:
          - name: "divPositive"
            value:
              type: ":integer"
              value: "2"
          - name: "divNegative"
            value:
              type: ":integer"
              value: "-3"
          - name: "divNegativeDivisor"
            value:
              type: ":integer"
              value: "-3"
          - name: "floatDiv"
            value:
              type: ":integer"
              value: "3"
          - name: "sameUnitDiv"
            value:
              type: ":integer"
              value: "3"
          - name: "unitDivScalar"
            value:
              type: ":integer"
              value: "3"
              unit: ":meter"
          - name: "modPositive"
            value:
              type: ":integer"
              value: "1"
          - name: "modNegative"
            value:
              type: ":integer"
              value: "2"
          - name: "modNegativeDivisor"
            value:
              type: ":integer"
              value: "-2"
          - name: "remPositive"
            value:
              type: ":integer"
              value: "1"
          - name: "remNegative"
            value:
              type: ":integer"
              value: "-1"
          - name: "remNegativeDivisor"
            value:
              type: ":integer"
              value: "1"
          - name: "divZero"
            value:
              type: ":float"
              value: "Infinity"
          - name: "remZero"
            value:
              type: ":float"
              value: "NaN"
```

## Test: percentage arithmetic keeps ratios and applies relative bases

```yaml
gesBlock: case
id: case-0009
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "percentage arithmetic keeps ratios and applies relative bases.ges"
    program: main
```

```ges
on Start {
  emit Done(addToBase: 100 + 5%, subtractFromBase: 100 - 5%, multiplyBaseRight: 100 * 5%, divideBaseByPercent: 100 / 5%, reverseAdd: 5% + 100, reverseSubtract: 5% - 100, percentAdd: 15% + 15%, percentSubtract: 15% - 5%, percentScaleRight: 15% * 2, percentDivideScalar: 15% / 3, scalarScaleLeft: 2 * 15%, percentProduct: 15% * 15%, percentRatio: 15% / 15%, percentDivideZero: 15% / 0)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "addToBase"
            value:
              type: ":integer"
              value: "105"
          - name: "subtractFromBase"
            value:
              type: ":integer"
              value: "95"
          - name: "multiplyBaseRight"
            value:
              type: ":integer"
              value: "5"
          - name: "divideBaseByPercent"
            value:
              type: ":integer"
              value: "2000"
          - name: "reverseAdd"
            value:
              type: ":float"
              value: "NaN"
          - name: "reverseSubtract"
            value:
              type: ":float"
              value: "NaN"
          - name: "percentAdd"
            value:
              type: ":percentage"
              value: "0.3"
          - name: "percentSubtract"
            value:
              type: ":percentage"
              value: "0.1"
          - name: "percentScaleRight"
            value:
              type: ":float"
              value: "0.3"
          - name: "percentDivideScalar"
            value:
              type: ":percentage"
              value: "0.05"
          - name: "scalarScaleLeft"
            value:
              type: ":float"
              value: "0.3"
          - name: "percentProduct"
            value:
              type: ":percentage"
              value: "0.0225"
          - name: "percentRatio"
            value:
              type: ":integer"
              value: "1"
          - name: "percentDivideZero"
            value:
              type: ":float"
              value: "Infinity"
```

## Test: percentage arithmetic preserves compatible numeric units

```yaml
gesBlock: case
id: case-0010
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "percentage arithmetic preserves compatible numeric units.ges"
    program: main
```

```ges
on Start {
  emit Done(meterAddPercent: 100m + 5%, meterSubtractPercent: 100m - 5%, meterMultiplyPercentRight: 100m * 5%, meterMultiplyPercentLeft: 5% * 100m, meterDividePercent: 100m / 5%, reverseMeterAdd: 5% + 100m, reverseMeterSubtract: 5% - 100m, percentDivideMeter: 5% / 100m)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "meterAddPercent"
            value:
              type: ":integer"
              value: "105"
              unit: ":meter"
          - name: "meterSubtractPercent"
            value:
              type: ":integer"
              value: "95"
              unit: ":meter"
          - name: "meterMultiplyPercentRight"
            value:
              type: ":integer"
              value: "5"
              unit: ":meter"
          - name: "meterMultiplyPercentLeft"
            value:
              type: ":float"
              value: "5"
              unit: ":meter"
          - name: "meterDividePercent"
            value:
              type: ":integer"
              value: "2000"
              unit: ":meter"
          - name: "reverseMeterAdd"
            value:
              type: ":float"
              value: "NaN"
          - name: "reverseMeterSubtract"
            value:
              type: ":float"
              value: "NaN"
          - name: "percentDivideMeter"
            value:
              type: ":float"
              value: "NaN"
```

## Test: degree literals preserve raw angles and wrap explicitly

```yaml
gesBlock: case
id: case-0011
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "degree literals preserve raw angles and wrap explicitly.ges"
    program: main
```

```ges
on Start {
  let fromLiteral be 90°
  let overLiteral be 360°
  let floatLiteral be 43.9°
  let addOpen be 360° + 90°
  let addOver be 180° + 200°
  let subtractOpen be 10° - 40°
  let negOpen be -90°
  let castOpen as :quantity(°) be 450
  let wrapNegative be wrap degree -10°
  let wrapOver be wrap degree 370°
  let wrapLarge be wrap degree 1000°
  let wrapCast be wrap degree castOpen
  let textValue as :text be 43.9°
  let floatValue as :number be 43.9°
  emit Done(fromLiteral: fromLiteral, overLiteral: overLiteral, floatLiteral: floatLiteral, addOpen: addOpen, addOver: addOver, subtractOpen: subtractOpen, negOpen: negOpen, castOpen: castOpen, wrapNegative: wrapNegative, wrapOver: wrapOver, wrapLarge: wrapLarge, wrapCast: wrapCast, textValue: textValue, floatValue: floatValue, isDegree: fromLiteral is :quantity(°))
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "fromLiteral"
            value:
              type: ":integer"
              value: "90"
              unit: ":degree"
          - name: "overLiteral"
            value:
              type: ":integer"
              value: "360"
              unit: ":degree"
          - name: "floatLiteral"
            value:
              type: ":float"
              value: "43.9"
              unit: ":degree"
          - name: "addOpen"
            value:
              type: ":integer"
              value: "450"
              unit: ":degree"
          - name: "addOver"
            value:
              type: ":integer"
              value: "380"
              unit: ":degree"
          - name: "subtractOpen"
            value:
              type: ":integer"
              value: "-30"
              unit: ":degree"
          - name: "negOpen"
            value:
              type: ":integer"
              value: "-90"
              unit: ":degree"
          - name: "castOpen"
            value:
              type: ":integer"
              value: "450"
              unit: ":degree"
          - name: "wrapNegative"
            value:
              type: ":float"
              value: "350"
              unit: ":degree"
          - name: "wrapOver"
            value:
              type: ":float"
              value: "10"
              unit: ":degree"
          - name: "wrapLarge"
            value:
              type: ":float"
              value: "280"
              unit: ":degree"
          - name: "wrapCast"
            value:
              type: ":float"
              value: "90"
              unit: ":degree"
          - name: "textValue"
            value:
              type: ":text"
              value: "43.9\u00B0"
          - name: "floatValue"
            value:
              type: ":float"
              value: "43.9"
              unit: ":degree"
          - name: "isDegree"
            value:
              type: ":boolean"
              value: true
```

## Test: degree arithmetic percentages helpers and comparisons

```yaml
gesBlock: case
id: case-0012
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "degree arithmetic percentages helpers and comparisons.ges"
    program: main
```

```ges
on Start {
  emit Done(addNumber: 120° + 10, subtractNumber: 120° - 150, addPercentRight: 120° + 10%, addPercentLeft: 10% + 120°, subtractPercent: 120° - 10%, multiplyPercentRight: 120° * 10%, multiplyPercentLeft: 10% * 120°, multiplyFloat: 30° * 0.2, divideFloat: 30° / 0.2, dividePercent: 120° / 10%, wrappedDividePercent: wrap degree (120° / 10%), moduloDegree: 370° mod 90°, moduloPercent: 120° mod 10%, divideZero: 120° / 0, moduloZero: 120° mod 0, reverseSubtract: 10 - 120°, reverseDivide: 10 / 120°, reverseModulo: 10 mod 120°, floorDegree: floor 43.9°, ceilDegree: ceil 43.1°, roundEvenDegree: round half even 42.5°, minDegree: min of 350° and 10°, maxDegree: max of 350° and 10°, greaterDegree: 350° > 10°, lessMixed: 10° < 20, percentCompare: 10° < 10%)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "addNumber"
            value:
              type: ":float"
              value: "NaN"
          - name: "subtractNumber"
            value:
              type: ":float"
              value: "NaN"
          - name: "addPercentRight"
            value:
              type: ":integer"
              value: "132"
              unit: ":degree"
          - name: "addPercentLeft"
            value:
              type: ":float"
              value: "NaN"
          - name: "subtractPercent"
            value:
              type: ":integer"
              value: "108"
              unit: ":degree"
          - name: "multiplyPercentRight"
            value:
              type: ":integer"
              value: "12"
              unit: ":degree"
          - name: "multiplyPercentLeft"
            value:
              type: ":float"
              value: "12"
              unit: ":degree"
          - name: "multiplyFloat"
            value:
              type: ":integer"
              value: "6"
              unit: ":degree"
          - name: "divideFloat"
            value:
              type: ":integer"
              value: "150"
              unit: ":degree"
          - name: "dividePercent"
            value:
              type: ":integer"
              value: "1200"
              unit: ":degree"
          - name: "wrappedDividePercent"
            value:
              type: ":float"
              value: "120"
              unit: ":degree"
          - name: "moduloDegree"
            value:
              type: ":integer"
              value: "10"
              unit: ":degree"
          - name: "moduloPercent"
            value:
              type: ":float"
              value: "NaN"
          - name: "divideZero"
            value:
              type: ":float"
              value: "Infinity"
          - name: "moduloZero"
            value:
              type: ":float"
              value: "NaN"
          - name: "reverseSubtract"
            value:
              type: ":float"
              value: "NaN"
          - name: "reverseDivide"
            value:
              type: ":float"
              value: "NaN"
          - name: "reverseModulo"
            value:
              type: ":float"
              value: "NaN"
          - name: "floorDegree"
            value:
              type: ":integer"
              value: "43"
          - name: "ceilDegree"
            value:
              type: ":integer"
              value: "44"
          - name: "roundEvenDegree"
            value:
              type: ":integer"
              value: "42"
          - name: "minDegree"
            value:
              type: ":integer"
              value: "10"
              unit: ":degree"
          - name: "maxDegree"
            value:
              type: ":integer"
              value: "350"
              unit: ":degree"
          - name: "greaterDegree"
            value:
              type: ":boolean"
              value: true
          - name: "lessMixed"
            value:
              type: ":boolean"
              value: false
          - name: "percentCompare"
            value:
              type: ":boolean"
              value: false
```

## Test: numeric unit literals casts and strict arithmetic

```yaml
gesBlock: case
id: case-0013
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "numeric unit literals casts and strict arithmetic.ges"
    program: main
```

```ges
on Start {
  let castMeter as :quantity(m) be 100
  let rawMeter as :number be castMeter
  emit Done(distance: 100m, duration: 15s, castMeter: castMeter, rawMeter: rawMeter, isMeter: castMeter is :quantity(m), isSecond: 15s is :quantity(s), isFloat: castMeter is :number, sameAdd: 100m + 50m, mixedAdd: 100m + 50, wrongAdd: 100m + 5s, scaleLeft: 100m * 2, scaleRight: 2 * 100m, divideScalar: 100m / 2, divideSame: 100m / 25m, unitModulo: 370m mod 90m, unitModuloScalar: 100m mod 3, wrapMeter: wrap degree 10m)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
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
          - name: "castMeter"
            value:
              type: ":integer"
              value: "100"
              unit: ":meter"
          - name: "rawMeter"
            value:
              type: ":integer"
              value: "100"
              unit: ":meter"
          - name: "isMeter"
            value:
              type: ":boolean"
              value: true
          - name: "isSecond"
            value:
              type: ":boolean"
              value: true
          - name: "isFloat"
            value:
              type: ":boolean"
              value: true
          - name: "sameAdd"
            value:
              type: ":integer"
              value: "150"
              unit: ":meter"
          - name: "mixedAdd"
            value:
              type: ":float"
              value: "NaN"
          - name: "wrongAdd"
            value:
              type: ":float"
              value: "NaN"
          - name: "scaleLeft"
            value:
              type: ":integer"
              value: "200"
              unit: ":meter"
          - name: "scaleRight"
            value:
              type: ":integer"
              value: "200"
              unit: ":meter"
          - name: "divideScalar"
            value:
              type: ":integer"
              value: "50"
              unit: ":meter"
          - name: "divideSame"
            value:
              type: ":integer"
              value: "4"
          - name: "unitModulo"
            value:
              type: ":integer"
              value: "10"
              unit: ":meter"
          - name: "unitModuloScalar"
            value:
              type: ":float"
              value: "NaN"
          - name: "wrapMeter"
            value:
              type: ":float"
              value: "NaN"
```

## Test: required standard integer and degree extensions are intrinsic

```yaml
gesBlock: case
id: case-0014
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "required standard integer and degree extensions are intrinsic.ges"
    program: main
```

```ges
on Start(value, heading, unitlessHeading, meterValue) {
  let radiansValue be rad 180°
  emit Done(floorBare: floor value, floorCall: floor(value), floorNegative: floor -10.4, ceilNegative: ceil -10.4, truncateNegative: truncate -10.4, halfEven_12: round half even 12.5, halfEven_13: round half even 13.5, halfUpNegative: round half up -12.5, halfDownNegative: round half down -12.5, wrapNegative: wrap degree heading, wrapUnitless: wrap degree unitlessHeading, radians: radiansValue, degrees: deg radiansValue, wrapWrongUnit: wrap degree meterValue, radiansWrongUnit: rad meterValue, fromRadiansWrongUnit: deg meterValue)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "value"
          value:
            type: ":float"
            value: "10.4"
        - name: "heading"
          value:
            type: ":float"
            value: "-10"
            unit: ":degree"
        - name: "unitlessHeading"
          value:
            type: ":float"
            value: "370"
        - name: "meterValue"
          value:
            type: ":float"
            value: "10"
            unit: ":meter"
    local:
      - name: "Done"
        args:
          - name: "floorBare"
            value:
              type: ":integer"
              value: "10"
          - name: "floorCall"
            value:
              type: ":integer"
              value: "10"
          - name: "floorNegative"
            value:
              type: ":integer"
              value: "-11"
          - name: "ceilNegative"
            value:
              type: ":integer"
              value: "-10"
          - name: "truncateNegative"
            value:
              type: ":integer"
              value: "-10"
          - name: "halfEven_12"
            value:
              type: ":integer"
              value: "12"
          - name: "halfEven_13"
            value:
              type: ":integer"
              value: "14"
          - name: "halfUpNegative"
            value:
              type: ":integer"
              value: "-13"
          - name: "halfDownNegative"
            value:
              type: ":integer"
              value: "-12"
          - name: "wrapNegative"
            value:
              type: ":float"
              value: "350"
              unit: ":degree"
          - name: "wrapUnitless"
            value:
              type: ":float"
              value: "10"
              unit: ":degree"
          - name: "radians"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "degrees"
            value:
              type: ":float"
              value: "180"
              unit: ":degree"
          - name: "wrapWrongUnit"
            value:
              type: ":float"
              value: "NaN"
          - name: "radiansWrongUnit"
            value:
              type: ":float"
              value: "NaN"
          - name: "fromRadiansWrongUnit"
            value:
              type: ":float"
              value: "NaN"
```

## Test: vector values cast expose components

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

## Test: vector units are preserved in components and conversions

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

## Test: vector arithmetic supports addition scaling division and length

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

## Test: invalid vector arithmetic returns nan

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

```ges
on Start {
  emit Done(mixedUnits: :vector(10m, 20s), mixedUnitless: :vector(10, 20m), partialMixedUnit: :vector(y: 20m, z: 30), liftedMixedUnit: :vector(:vector(10m, 20m), 20), vectorProduct: :vector(1, 2) * :vector(3, 4), scalarDivide: 10 / :vector(1, 2), dimensionAdd: :vector(1, 2) + :vector(1, 2, 3), incompatibleScalar: :vector(1m, 2m) * 5s, divideZero: :vector(1, 2) / 0, vectorModulo: :vector(1, 2) mod 2)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

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

## Test: inline literals escaped quotes and numeric helpers

```yaml
gesBlock: case
id: case-0019
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "inline literals escaped quotes and numeric helpers.ges"
    program: main
```

```ges
on Start(opt, missingValue, value, meterValue) {
  let text be 'Hello ''World'', I''m here'
  let doubleText be "didn't say ""stop"""
  let myList be [1, 2, 3]
  let myValues be [name: 'Hello', position: 1]
  let emptyValues be [:]
  let distanceValue be abs (0 - 12.5)
  let naturalOne be ln e
  let naturalZero be ln 0
  let naturalNegative be ln (0 - 1)
  let naturalInfinity be ln infinity
  let naturalInvalidTag be ln #nan
  let naturalNothing be ln missingValue
  let boundedHigh be clamp 120 between 0 and 99
  let boundedLow be clamp (0 - 3) between 0 and 99
  let boundedNothingValue be clamp missingValue between 0 and 99
  let boundedNothingMin be clamp 10 between missingValue and 99
  let boundedInvalid be clamp 'hello' between 0 and 99
  let boundedInfinity be clamp infinity between 0 and 99
  let highest be max of 4 and 9 and 2;
  let lowest be min of 4 and 9 and 2;
  emit Done(text: text, doubleText: doubleText, listLen: myList[:count], name: myValues.name, position: myValues.position, emptyLen: emptyValues[:count], distance: distanceValue, naturalOne: naturalOne, naturalZero: naturalZero, naturalNegative: naturalNegative, naturalInfinity: naturalInfinity, naturalInvalidTag: naturalInvalidTag, naturalNothing: naturalNothing, naturalRuntime: ln value > 1, naturalUnit: ln meterValue, boundedHigh: boundedHigh, boundedLow: boundedLow, boundedNothingValue: boundedNothingValue, boundedNothingMin: boundedNothingMin, boundedInvalid: boundedInvalid, boundedInfinity: boundedInfinity, highest: highest, lowest: lowest, presenceFallback: opt default 10, nothingFallback: missingValue default 20)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "opt"
          value:
            type: ":nothing"
        - name: "missingValue"
          value:
            type: ":nothing"
        - name: "value"
          value:
            type: ":integer"
            value: "3"
        - name: "meterValue"
          value:
            type: ":float"
            value: "5"
            unit: ":meter"
    local:
      - name: "Done"
        args:
          - name: "text"
            value:
              type: ":text"
              value: "Hello \u0027World\u0027, I\u0027m here"
          - name: "doubleText"
            value:
              type: ":text"
              value: "didn\u0027t say \u0022stop\u0022"
          - name: "listLen"
            value:
              type: ":integer"
              value: "3"
          - name: "name"
            value:
              type: ":text"
              value: "Hello"
          - name: "position"
            value:
              type: ":integer"
              value: "1"
          - name: "emptyLen"
            value:
              type: ":integer"
              value: "0"
          - name: "distance"
            value:
              type: ":float"
              value: "12.5"
          - name: "naturalOne"
            value:
              type: ":float"
              value: "1"
          - name: "naturalZero"
            value:
              type: ":float"
              value: "-Infinity"
          - name: "naturalNegative"
            value:
              type: ":float"
              value: "NaN"
          - name: "naturalInfinity"
            value:
              type: ":float"
              value: "Infinity"
          - name: "naturalInvalidTag"
            value:
              type: ":nothing"
          - name: "naturalNothing"
            value:
              type: ":nothing"
          - name: "naturalRuntime"
            value:
              type: ":boolean"
              value: true
          - name: "naturalUnit"
            value:
              type: ":float"
              value: "NaN"
          - name: "boundedHigh"
            value:
              type: ":float"
              value: "99"
          - name: "boundedLow"
            value:
              type: ":float"
              value: "0"
          - name: "boundedNothingValue"
            value:
              type: ":nothing"
          - name: "boundedNothingMin"
            value:
              type: ":nothing"
          - name: "boundedInvalid"
            value:
              type: ":float"
              value: "NaN"
          - name: "boundedInfinity"
            value:
              type: ":float"
              value: "99"
          - name: "highest"
            value:
              type: ":integer"
              value: "9"
          - name: "lowest"
            value:
              type: ":integer"
              value: "2"
          - name: "presenceFallback"
            value:
              type: ":integer"
              value: "10"
          - name: "nothingFallback"
            value:
              type: ":integer"
              value: "20"
```

## Test: lookup and empty checks are runtime-visible

```yaml
gesBlock: case
id: case-0020
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "lookup and empty checks are runtime-visible.ges"
    program: main
```

```ges
on Start {
  let items be [10, 20, 30]
  let unit be [hp: 7, name: 'Knight']
  let emptyList be []
  let invalidNumber as :number be 'abc'
  emit Done(second: items[2], nothing: items[4], hp: unit[#hp], emptyList: emptyList is empty, invalidHasValue: invalidNumber has value, invalidIsEmpty: invalidNumber is empty)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "second"
            value:
              type: ":integer"
              value: "20"
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "hp"
            value:
              type: ":integer"
              value: "7"
          - name: "emptyList"
            value:
              type: ":boolean"
              value: true
          - name: "invalidHasValue"
            value:
              type: ":boolean"
              value: false
          - name: "invalidIsEmpty"
            value:
              type: ":boolean"
              value: true
```

## Test: dice like names remain ordinary identifiers

```yaml
gesBlock: case
id: case-0021
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "dice like names remain ordinary identifiers.ges"
    program: main
```

```ges
on Start(d_6) {
  let kept be d_6
  emit Done(value: kept)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "d_6"
          value:
            type: ":integer"
            value: "6"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "6"
```

## Test: containment boundary sort and distinct semantics

```yaml
gesBlock: case
id: case-0022
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "containment boundary sort and distinct semantics.ges"
    program: main
```

```ges
on Start {
  let values be [3, 1, 2, 2]
  emit Done(textContains: 'club' in 'battle club', startsList: [1, 2, 3] starts with [1, 2], endsList: [1, 2, 3] ends with [1, 2], hasAll: [1, 2, 3][:contains all [1, 3]], hasAny: [1, 2, 3][:contains any [0, 3]], sorted: values[:sort ascending], distinct: values[:distinct])
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "textContains"
            value:
              type: ":boolean"
              value: true
          - name: "startsList"
            value:
              type: ":boolean"
              value: true
          - name: "endsList"
            value:
              type: ":boolean"
              value: false
          - name: "hasAll"
            value:
              type: ":boolean"
              value: true
          - name: "hasAny"
            value:
              type: ":boolean"
              value: true
          - name: "sorted"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "distinct"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
```

## Test: float edge values stay observable

```yaml
gesBlock: case
id: case-0023
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "float edge values stay observable.ges"
    program: main
```

```ges
on Start {
  emit Done(divZero: 1 / 0, modZero: 1 mod 0, overflow: 9999999999999999999999999999 * 9999999999999999999999999999)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "divZero"
            value:
              type: ":float"
              value: "Infinity"
          - name: "modZero"
            value:
              type: ":float"
              value: "NaN"
          - name: "overflow"
            value:
              type: ":float"
              value: "1e56"
```

## Test: one based lookup and dictionary property access

```yaml
gesBlock: case
id: case-0024
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "one based lookup and dictionary property access.ges"
    program: main
```

```ges
on Start(entry, keyName) {
  let values be [10, 20, 30]
  let tags be ['alpha', 'beta']
  let lookupProperty be #name
  emit Done(first: values[1], third: values[3], zero: values[0], missingIndex: values[4], property: entry.name, textKey: entry['name'], tagKey: entry[#name], dynamicTag: entry[lookupProperty], dynamicText: entry[keyName], missingProperty: entry['missing'], firstTag: tags[1], missingTag: tags[3])
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "entry"
          value:
            type: ":map"
            entries:
              - key: "name"
                value:
                  type: ":text"
                  value: "Ada"
        - name: "keyName"
          value:
            type: ":text"
            value: "name"
    local:
      - name: "Done"
        args:
          - name: "first"
            value:
              type: ":integer"
              value: "10"
          - name: "third"
            value:
              type: ":integer"
              value: "30"
          - name: "zero"
            value:
              type: ":nothing"
          - name: "missingIndex"
            value:
              type: ":nothing"
          - name: "property"
            value:
              type: ":text"
              value: "Ada"
          - name: "textKey"
            value:
              type: ":text"
              value: "Ada"
          - name: "tagKey"
            value:
              type: ":text"
              value: "Ada"
          - name: "dynamicTag"
            value:
              type: ":text"
              value: "Ada"
          - name: "dynamicText"
            value:
              type: ":text"
              value: "Ada"
          - name: "missingProperty"
            value:
              type: ":nothing"
          - name: "firstTag"
            value:
              type: ":text"
              value: "alpha"
          - name: "missingTag"
            value:
              type: ":nothing"
```

## Test: value checks defaults and presence

```yaml
gesBlock: case
id: case-0025
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "value checks defaults and presence.ges"
    program: main
```

```ges
on Start(opt, emptyText, emptyList, emptyDict, zeroValue, falseValue, diceValue, nanValue, infinityValue) {
  emit Done(hasOpt: opt has value, hasEmptyText: emptyText has value, hasEmptyList: emptyList has value, hasEmptyDict: emptyDict has value, hasZero: zeroValue has value, hasFalse: falseValue has value, hasNaN: nanValue has value, hasInfinity: infinityValue has value, isEmptyOpt: empty opt, isEmptyText: empty emptyText, isEmptyList: empty emptyList, isEmptyDict: empty emptyDict, isEmptyDice: empty diceValue, isEmptyNaN: empty nanValue, isEmptyInfinity: empty infinityValue, nanIsNothing: nanValue is nothing, notEmptyList: not empty [1], notFalse: not falseValue, optValue: opt default 10, listValue: (emptyList default [1, 2])[1], textValue: emptyText default 'fallback', dictValue: (emptyDict default [name: 'default']).name, missingTextValue: emptyDict['name'] default 'fallback')
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "opt"
          value:
            type: ":integer"
            value: "7"
        - name: "emptyText"
          value:
            type: ":text"
            value: ""
        - name: "emptyList"
          value:
            type: ":list"
            items: []
        - name: "emptyDict"
          value:
            type: ":map"
            entries: []
        - name: "zeroValue"
          value:
            type: ":integer"
            value: "0"
        - name: "falseValue"
          value:
            type: ":boolean"
            value: false
        - name: "diceValue"
          value:
            type: ":dice"
            rolls: []
        - name: "nanValue"
          value:
            type: ":float"
            value: "NaN"
        - name: "infinityValue"
          value:
            type: ":float"
            value: "Infinity"
    local:
      - name: "Done"
        args:
          - name: "hasOpt"
            value:
              type: ":boolean"
              value: true
          - name: "hasEmptyText"
            value:
              type: ":boolean"
              value: false
          - name: "hasEmptyList"
            value:
              type: ":boolean"
              value: false
          - name: "hasEmptyDict"
            value:
              type: ":boolean"
              value: false
          - name: "hasZero"
            value:
              type: ":boolean"
              value: true
          - name: "hasFalse"
            value:
              type: ":boolean"
              value: true
          - name: "hasNaN"
            value:
              type: ":boolean"
              value: false
          - name: "hasInfinity"
            value:
              type: ":boolean"
              value: true
          - name: "isEmptyOpt"
            value:
              type: ":boolean"
              value: false
          - name: "isEmptyText"
            value:
              type: ":boolean"
              value: true
          - name: "isEmptyList"
            value:
              type: ":boolean"
              value: true
          - name: "isEmptyDict"
            value:
              type: ":boolean"
              value: true
          - name: "isEmptyDice"
            value:
              type: ":boolean"
              value: true
          - name: "isEmptyNaN"
            value:
              type: ":boolean"
              value: true
          - name: "isEmptyInfinity"
            value:
              type: ":boolean"
              value: false
          - name: "nanIsNothing"
            value:
              type: ":boolean"
              value: true
          - name: "notEmptyList"
            value:
              type: ":boolean"
              value: true
          - name: "notFalse"
            value:
              type: ":boolean"
              value: true
          - name: "optValue"
            value:
              type: ":integer"
              value: "7"
          - name: "listValue"
            value:
              type: ":integer"
              value: "1"
          - name: "textValue"
            value:
              type: ":text"
              value: "fallback"
          - name: "dictValue"
            value:
              type: ":text"
              value: "default"
          - name: "missingTextValue"
            value:
              type: ":text"
              value: "fallback"
```

## Test: type conversions length checks and type tags

```yaml
gesBlock: case
id: case-0026
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["2", "6", "3", "5"]
sources:
  - name: "type conversions length checks and type tags.ges"
    program: main
```

```ges
on Start(valueForLen, a, b, c, d, listArg, f, g) {
  let tagOk as :tag be #name
  let numberOk as :number be '12.2'
  let numberFail as :number be 'abc'
  let integerOk as :number be '12.7'
  let booleanOk as :boolean be 'true'
  let listOk as :list be 'ab'
  let diceOk as :dice be [6, 2, 4]
  emit Done(tagOk: tagOk, numberOk: numberOk, numberFail: numberFail, integerOk: integerOk, booleanOk: booleanOk, listLen: listOk[:count], diceFirst: diceOk[1], diceLen: diceOk[:count], textLen: valueForLen[:count], dictLen: [first: 1, second: 2][:count], valueForLenLen: valueForLen[:count], floatLen: 12.5[:count], booleanLen: true[:count], aIsTag: a is :tag, bIsFloat: b is :number, cIsInteger: c is :number, dIsText: d is :text, eIsList: listArg is :list, fIsMap: f is :map, gIsNothing: g is nothing)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "valueForLen"
          value:
            type: ":text"
            value: "x"
        - name: "a"
          value:
            type: ":tag"
            value: "name"
        - name: "b"
          value:
            type: ":float"
            value: "1.5"
        - name: "c"
          value:
            type: ":integer"
            value: "2"
        - name: "d"
          value:
            type: ":text"
            value: "x"
        - name: "listArg"
          value:
            type: ":list"
            items:
              - type: ":integer"
                value: "1"
        - name: "f"
          value:
            type: ":map"
            entries:
              - key: "v"
                value:
                  type: ":integer"
                  value: "1"
        - name: "g"
          value:
            type: ":nothing"
    local:
      - name: "Done"
        args:
          - name: "tagOk"
            value:
              type: ":tag"
              value: "name"
          - name: "numberOk"
            value:
              type: ":float"
              value: "12.2"
          - name: "numberFail"
            value:
              type: ":nothing"
          - name: "integerOk"
            value:
              type: ":float"
              value: "12.7"
          - name: "booleanOk"
            value:
              type: ":boolean"
              value: true
          - name: "listLen"
            value:
              type: ":integer"
              value: "2"
          - name: "diceFirst"
            value:
              type: ":integer"
              value: "6"
          - name: "diceLen"
            value:
              type: ":integer"
              value: "3"
          - name: "textLen"
            value:
              type: ":integer"
              value: "1"
          - name: "dictLen"
            value:
              type: ":integer"
              value: "2"
          - name: "valueForLenLen"
            value:
              type: ":integer"
              value: "1"
          - name: "floatLen"
            value:
              type: ":nothing"
          - name: "booleanLen"
            value:
              type: ":nothing"
          - name: "aIsTag"
            value:
              type: ":boolean"
              value: true
          - name: "bIsFloat"
            value:
              type: ":boolean"
              value: true
          - name: "cIsInteger"
            value:
              type: ":boolean"
              value: true
          - name: "dIsText"
            value:
              type: ":boolean"
              value: true
          - name: "eIsList"
            value:
              type: ":boolean"
              value: true
          - name: "fIsMap"
            value:
              type: ":boolean"
              value: true
          - name: "gIsNothing"
            value:
              type: ":boolean"
              value: true
```

## Test: unary domain conditional and standard integer rounding forms

```yaml
gesBlock: case
id: case-0027
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "unary domain conditional and standard integer rounding forms.ges"
    program: main
```

```ges
on Start(hp, mana, hand, target, age) {
  let negInt be -12
  let negFloat be -12.34
  let negPercent be -25%
  let restored be -negFloat
  let score as :number be 12 when age is :boolean, or 15 when age is :number, otherwise 5
  emit Done(negInt: negInt, negFloat: negFloat, negPercent: negPercent, restored: restored, boolNegation: not false, hpCheck: hp is 0 or less, manaCheck: mana is at least 3, handCheck: hand is empty, targetCheck: target has value, oldHasValue: target has value, newHasValue: target has value, oldEmpty: empty hand, newEmpty: hand is empty, score: score, floor: floor 12.7, ceil: ceil 12.1, roundEven_12: round half even 12.5, roundEven_13: round half even 13.5, truncate: truncate -12.9, halfUp: round half up -12.5, halfDown: round half down -12.5)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |
| step-0002 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "hp"
          value:
            type: ":float"
            value: "-1"
        - name: "mana"
          value:
            type: ":integer"
            value: "3"
        - name: "hand"
          value:
            type: ":list"
            items: []
        - name: "target"
          value:
            type: ":text"
            value: "orc"
        - name: "age"
          value:
            type: ":boolean"
            value: true
    local:
      - name: "Done"
        args:
          - name: "negInt"
            value:
              type: ":integer"
              value: "-12"
          - name: "negFloat"
            value:
              type: ":float"
              value: "-12.34"
          - name: "negPercent"
            value:
              type: ":percentage"
              value: "-0.25"
          - name: "restored"
            value:
              type: ":float"
              value: "12.34"
          - name: "boolNegation"
            value:
              type: ":boolean"
              value: true
          - name: "hpCheck"
            value:
              type: ":boolean"
              value: true
          - name: "manaCheck"
            value:
              type: ":boolean"
              value: true
          - name: "handCheck"
            value:
              type: ":boolean"
              value: true
          - name: "targetCheck"
            value:
              type: ":boolean"
              value: true
          - name: "oldHasValue"
            value:
              type: ":boolean"
              value: true
          - name: "newHasValue"
            value:
              type: ":boolean"
              value: true
          - name: "oldEmpty"
            value:
              type: ":boolean"
              value: true
          - name: "newEmpty"
            value:
              type: ":boolean"
              value: true
          - name: "score"
            value:
              type: ":integer"
              value: "12"
          - name: "floor"
            value:
              type: ":integer"
              value: "12"
          - name: "ceil"
            value:
              type: ":integer"
              value: "13"
          - name: "roundEven_12"
            value:
              type: ":integer"
              value: "12"
          - name: "roundEven_13"
            value:
              type: ":integer"
              value: "14"
          - name: "truncate"
            value:
              type: ":integer"
              value: "-12"
          - name: "halfUp"
            value:
              type: ":integer"
              value: "-13"
          - name: "halfDown"
            value:
              type: ":integer"
              value: "-12"
  step-0002:
    input:
      args:
        - name: "hp"
          value:
            type: ":float"
            value: "-1"
        - name: "mana"
          value:
            type: ":integer"
            value: "3"
        - name: "hand"
          value:
            type: ":list"
            items: []
        - name: "target"
          value:
            type: ":text"
            value: "orc"
        - name: "age"
          value:
            type: ":text"
            value: "x"
    local:
      - name: "Done"
        args:
          - name: "negInt"
            value:
              type: ":integer"
              value: "-12"
          - name: "negFloat"
            value:
              type: ":float"
              value: "-12.34"
          - name: "negPercent"
            value:
              type: ":percentage"
              value: "-0.25"
          - name: "restored"
            value:
              type: ":float"
              value: "12.34"
          - name: "boolNegation"
            value:
              type: ":boolean"
              value: true
          - name: "hpCheck"
            value:
              type: ":boolean"
              value: true
          - name: "manaCheck"
            value:
              type: ":boolean"
              value: true
          - name: "handCheck"
            value:
              type: ":boolean"
              value: true
          - name: "targetCheck"
            value:
              type: ":boolean"
              value: true
          - name: "oldHasValue"
            value:
              type: ":boolean"
              value: true
          - name: "newHasValue"
            value:
              type: ":boolean"
              value: true
          - name: "oldEmpty"
            value:
              type: ":boolean"
              value: true
          - name: "newEmpty"
            value:
              type: ":boolean"
              value: true
          - name: "score"
            value:
              type: ":integer"
              value: "5"
          - name: "floor"
            value:
              type: ":integer"
              value: "12"
          - name: "ceil"
            value:
              type: ":integer"
              value: "13"
          - name: "roundEven_12"
            value:
              type: ":integer"
              value: "12"
          - name: "roundEven_13"
            value:
              type: ":integer"
              value: "14"
          - name: "truncate"
            value:
              type: ":integer"
              value: "-12"
          - name: "halfUp"
            value:
              type: ":integer"
              value: "-13"
          - name: "halfDown"
            value:
              type: ":integer"
              value: "-12"
```

## Test: xor word operator and power symbol operator

```yaml
gesBlock: case
id: case-0028
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor word operator and power symbol operator.ges"
    program: main
```

```ges
on Start {
  emit Done(wordTrue: true xor false, wordFalse: true xor true, square: 3 ^ 2, rightAssoc: 2 ^ 3 ^ 2, fractional: 9 ^ 0.5, negativeExponent: 2 ^ -2, negatedSquare: -3 ^ 2, negativeBaseSquare: (0 - 3) ^ 2, precedence: 2 + 3 ^ 2 * 4, unitPower: 2m ^ 2)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "wordTrue"
            value:
              type: ":boolean"
              value: true
          - name: "wordFalse"
            value:
              type: ":boolean"
              value: false
          - name: "square"
            value:
              type: ":integer"
              value: "9"
          - name: "rightAssoc"
            value:
              type: ":integer"
              value: "512"
          - name: "fractional"
            value:
              type: ":integer"
              value: "3"
          - name: "negativeExponent"
            value:
              type: ":float"
              value: "0.25"
          - name: "negatedSquare"
            value:
              type: ":integer"
              value: "-9"
          - name: "negativeBaseSquare"
            value:
              type: ":integer"
              value: "9"
          - name: "precedence"
            value:
              type: ":integer"
              value: "38"
          - name: "unitPower"
            value:
              type: ":float"
              value: "NaN"
```

## Test: implicit multiplication superscripts and unicode operator aliases

```yaml
gesBlock: case
id: case-0029
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implicit multiplication superscripts and unicode operator aliases.ges"
    program: main
```

```ges
on Start(x, y, flag) {
  emit Done(doubleX: 2x, floatY: 2.5y, meter: 2m, square: x², cube: y³, multiply: x × y, divide: y ÷ x, lessOrEqual: x ≤ y, greaterOrEqual: y ≥ x, notEqual: x ≠ y, notFlag: ¬flag)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "x"
          value:
            type: ":integer"
            value: "3"
        - name: "y"
          value:
            type: ":integer"
            value: "6"
        - name: "flag"
          value:
            type: ":boolean"
            value: false
    local:
      - name: "Done"
        args:
          - name: "doubleX"
            value:
              type: ":integer"
              value: "6"
          - name: "floatY"
            value:
              type: ":integer"
              value: "15"
          - name: "meter"
            value:
              type: ":integer"
              value: "2"
              unit: ":meter"
          - name: "square"
            value:
              type: ":integer"
              value: "9"
          - name: "cube"
            value:
              type: ":integer"
              value: "216"
          - name: "multiply"
            value:
              type: ":integer"
              value: "18"
          - name: "divide"
            value:
              type: ":integer"
              value: "2"
          - name: "lessOrEqual"
            value:
              type: ":boolean"
              value: true
          - name: "greaterOrEqual"
            value:
              type: ":boolean"
              value: true
          - name: "notEqual"
            value:
              type: ":boolean"
              value: true
          - name: "notFlag"
            value:
              type: ":boolean"
              value: true
```

## Test: additional unicode mathematical aliases

```yaml
gesBlock: case
id: case-0030
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "additional unicode mathematical aliases.ges"
    program: main
```

```ges
on Start(x, y) {
  let tags be [#boss, #scout]
  let unit be [name: 'Ada', hp: 10]
  let projected be :list[:select item from 1 to 2↦item + 1]
  emit Done(dot: x · y, smallDot: x ⋅ y, unicodeMinus: y − x, andAlias: true ∧ false, orAlias: false ∨ true, xorAlias: true ⊕ false, tagMember: #boss ∈ tags, tagNotMember: #ghost ∉ tags, dictKey: 'name' ∈ unit, infinity: ∞ as :number, infinityAdd: ∞ + 1, negativeInfinity: -∞ as :number, unicodeNegativeInfinity: −∞ + 1, piTag: pi as :number, piAlias: ∏ as :number, piGreater: ∏ > 3, eTag: e as :number, eAlias: ℇ as :number, eLess: ℇ < 3, tauTag: tau as :number, tauAlias: τ as :number, tauGreater: τ > 6, phiTag: 1.6180339887498948 as :number, phiAlias: 1.6180339887498948 as :number, phiBetween: 1.6180339887498948 > 1 and 1.6180339887498948 < 2, sqrtTag: sqrt 81, sqrtAlias: √81, cbrtTag: cbrt 27, cbrtAlias: ∛27, projectionAlias: projected[2])
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "x"
          value:
            type: ":integer"
            value: "3"
        - name: "y"
          value:
            type: ":integer"
            value: "6"
    local:
      - name: "Done"
        args:
          - name: "dot"
            value:
              type: ":integer"
              value: "18"
          - name: "smallDot"
            value:
              type: ":integer"
              value: "18"
          - name: "unicodeMinus"
            value:
              type: ":integer"
              value: "3"
          - name: "andAlias"
            value:
              type: ":boolean"
              value: false
          - name: "orAlias"
            value:
              type: ":boolean"
              value: true
          - name: "xorAlias"
            value:
              type: ":boolean"
              value: true
          - name: "tagMember"
            value:
              type: ":boolean"
              value: true
          - name: "tagNotMember"
            value:
              type: ":boolean"
              value: true
          - name: "dictKey"
            value:
              type: ":boolean"
              value: true
          - name: "infinity"
            value:
              type: ":float"
              value: "Infinity"
          - name: "infinityAdd"
            value:
              type: ":float"
              value: "Infinity"
          - name: "negativeInfinity"
            value:
              type: ":float"
              value: "-Infinity"
          - name: "unicodeNegativeInfinity"
            value:
              type: ":float"
              value: "-Infinity"
          - name: "piTag"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "piAlias"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "piGreater"
            value:
              type: ":boolean"
              value: true
          - name: "eTag"
            value:
              type: ":float"
              value: "2.71828182845905"
          - name: "eAlias"
            value:
              type: ":float"
              value: "2.71828182845905"
          - name: "eLess"
            value:
              type: ":boolean"
              value: true
          - name: "tauTag"
            value:
              type: ":float"
              value: "6.28318530717959"
          - name: "tauAlias"
            value:
              type: ":float"
              value: "6.28318530717959"
          - name: "tauGreater"
            value:
              type: ":boolean"
              value: true
          - name: "phiTag"
            value:
              type: ":float"
              value: "1.61803398874989"
          - name: "phiAlias"
            value:
              type: ":float"
              value: "1.61803398874989"
          - name: "phiBetween"
            value:
              type: ":boolean"
              value: true
          - name: "sqrtTag"
            value:
              type: ":integer"
              value: "9"
          - name: "sqrtAlias"
            value:
              type: ":integer"
              value: "9"
          - name: "cbrtTag"
            value:
              type: ":integer"
              value: "3"
          - name: "cbrtAlias"
            value:
              type: ":integer"
              value: "3"
          - name: "projectionAlias"
            value:
              type: ":integer"
              value: "3"
```

## Test: floating point equality uses ulp tolerance

```yaml
gesBlock: case
id: case-0031
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "floating point equality uses ulp tolerance.ges"
    program: main
```

```ges
on Start {
  let near be 0.1 + 0.2
  let target be 0.3
  emit Done(exact: near = target, far: 0.1 = 0.2, tagExact: #nan = #nan, unitNear: 0.1m + 0.2m = 0.3m, unitMismatch: 1m = 1s)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "exact"
            value:
              type: ":boolean"
              value: true
          - name: "far"
            value:
              type: ":boolean"
              value: false
          - name: "tagExact"
            value:
              type: ":boolean"
              value: true
          - name: "unitNear"
            value:
              type: ":boolean"
              value: true
          - name: "unitMismatch"
            value:
              type: ":boolean"
              value: false
```

## Test: numeric literals allow underscore digit separators

```yaml
gesBlock: case
id: case-0032
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "numeric literals allow underscore digit separators.ges"
    program: main
```

```ges
on Start(x) {
  emit Done(integer: 100_000, double: 100_000.25, percent: 1_5%, meter: 1_000m, degree: 90_0°, second: 3_600s, square: 1_2², scaled: 2_5x)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "x"
          value:
            type: ":integer"
            value: "4"
    local:
      - name: "Done"
        args:
          - name: "integer"
            value:
              type: ":integer"
              value: "100000"
          - name: "double"
            value:
              type: ":float"
              value: "100000.25"
          - name: "percent"
            value:
              type: ":percentage"
              value: "0.15"
          - name: "meter"
            value:
              type: ":integer"
              value: "1000"
              unit: ":meter"
          - name: "degree"
            value:
              type: ":integer"
              value: "900"
              unit: ":degree"
          - name: "second"
            value:
              type: ":integer"
              value: "3600"
              unit: ":second"
          - name: "square"
            value:
              type: ":integer"
              value: "144"
          - name: "scaled"
            value:
              type: ":integer"
              value: "100"
```

## Test: missing invalid and large values remain lenient

```yaml
gesBlock: case
id: case-0033
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "missing invalid and large values remain lenient.ges"
    program: main
```

```ges
on Start(item) {
  let nanValue as :number be 'abc'
  let nanPropagated be nanValue + 5
  let nothingValue be nothing
  let nothingPropagated be nothingValue + 5
  let pos be 79228162514264337593543950335 + 1
  let neg be 0 - 79228162514264337593543950335 - 1
  emit Done(nanValue: nanValue, nanPropagated: nanPropagated, nothing: nothingValue, nothingPropagated: nothingPropagated, propertyFromNothing: item.name, pos: pos, neg: neg)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "item"
          value:
            type: ":nothing"
    local:
      - name: "Done"
        args:
          - name: "nanValue"
            value:
              type: ":nothing"
          - name: "nanPropagated"
            value:
              type: ":nothing"
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "nothingPropagated"
            value:
              type: ":nothing"
          - name: "propertyFromNothing"
            value:
              type: ":nothing"
          - name: "pos"
            value:
              type: ":float"
              value: "7.92281625142643e28"
          - name: "neg"
            value:
              type: ":float"
              value: "-7.92281625142643e28"
```

## Test: explicit numeric identifier suffixes and compact math remain valid

```yaml
gesBlock: case
id: case-0034
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "explicit numeric identifier suffixes and compact math remain valid.ges"
    program: main
```

```ges
on Start(x, values) {
  let player_0 be x
  let player_1 be player_0 + 1
  let player_22 be player_1 + 21
  emit Done(playerZero: player_0, playerOne: player_1, playerMany: player_22, compactMultiply: 2x, square: x², lookup: values[2])
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "x"
          value:
            type: ":integer"
            value: "3"
        - name: "values"
          value:
            type: ":list"
            items:
              - type: ":integer"
                value: "10"
              - type: ":integer"
                value: "20"
    local:
      - name: "Done"
        args:
          - name: "playerZero"
            value:
              type: ":integer"
              value: "3"
          - name: "playerOne"
            value:
              type: ":integer"
              value: "4"
          - name: "playerMany"
            value:
              type: ":integer"
              value: "25"
          - name: "compactMultiply"
            value:
              type: ":integer"
              value: "6"
          - name: "square"
            value:
              type: ":integer"
              value: "9"
          - name: "lookup"
            value:
              type: ":integer"
              value: "20"
```

## Test: type checks identify primitive custom and first class values

```yaml
gesBlock: case
id: case-0035
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "type checks identify primitive custom and first class values.ges"
    program: main
```

```ges
on Start(custom) {
  let integerValue be 12
  let percentValue be 5%
  let degreeValue be 90°
  let meterValue be 100m
  let secondValue be 15s
  let listValue be [1]
  let dictValue be [name: 'Ada']
  let msg be Ping(value: 1)
  let handler be Ping(value)
  emit Done(intIsInteger: integerValue is :number, intIsFloat: integerValue is :number, percentIsFloat: percentValue is :number, degreeIsFloat: degreeValue is :number, degreeIsDegree: degreeValue is :quantity(°), meterIsMeter: meterValue is :quantity(m), secondIsSecond: secondValue is :quantity(s), textIsText: 'x' is :text, tagIsTag: #ready is :tag, boolIsBoolean: true is :boolean, listIsList: listValue is :list, dictIsMap: dictValue is :map, customIsGauge: custom is :gauge, customIsMap: custom is :map, msgIsMessage: msg is :message, msgIsMap: msg is :map, handlerIsHandler: handler is :handler, missingIsNothing: nothing is nothing)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "custom"
          value:
            type: ":gauge"
            entries:
              - key: "current"
                value:
                  type: ":integer"
                  value: "5"
    local:
      - name: "Done"
        args:
          - name: "intIsInteger"
            value:
              type: ":boolean"
              value: true
          - name: "intIsFloat"
            value:
              type: ":boolean"
              value: true
          - name: "percentIsFloat"
            value:
              type: ":boolean"
              value: true
          - name: "degreeIsFloat"
            value:
              type: ":boolean"
              value: true
          - name: "degreeIsDegree"
            value:
              type: ":boolean"
              value: true
          - name: "meterIsMeter"
            value:
              type: ":boolean"
              value: true
          - name: "secondIsSecond"
            value:
              type: ":boolean"
              value: true
          - name: "textIsText"
            value:
              type: ":boolean"
              value: true
          - name: "tagIsTag"
            value:
              type: ":boolean"
              value: true
          - name: "boolIsBoolean"
            value:
              type: ":boolean"
              value: true
          - name: "listIsList"
            value:
              type: ":boolean"
              value: true
          - name: "dictIsMap"
            value:
              type: ":boolean"
              value: true
          - name: "customIsGauge"
            value:
              type: ":boolean"
              value: true
          - name: "customIsMap"
            value:
              type: ":boolean"
              value: true
          - name: "msgIsMessage"
            value:
              type: ":boolean"
              value: true
          - name: "msgIsMap"
            value:
              type: ":boolean"
              value: false
          - name: "handlerIsHandler"
            value:
              type: ":boolean"
              value: true
          - name: "missingIsNothing"
            value:
              type: ":boolean"
              value: true
```

## Test: quantity casts checks and constructors normalize numeric units

```yaml
gesBlock: case
id: case-0036
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "quantity casts checks and constructors normalize numeric units.ges"
    program: main
```

```ges
record :quantity as {
  value: :number
}

on Start {
  let distanceValue be 100 as :quantity(m)
  let duration be :quantity(s)(5)
  let heading be :quantity(degree)(90)
  let custom be :quantity(value: 3)
  emit Done(distance: distanceValue, duration: duration, heading: heading, distanceIsMeter: distanceValue is :quantity(m), distanceIsSecond: distanceValue is :quantity(s), symbolIsDegree: heading is :quantity(°), percentageIsMeter: 50% is :quantity(m), customIsQuantity: custom is :quantity, customValue: custom.value)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    local:
      - name: "Done"
        args:
          - name: "distance"
            value:
              type: ":integer"
              value: "100"
              unit: ":meter"
          - name: "duration"
            value:
              type: ":integer"
              value: "5"
              unit: ":second"
          - name: "heading"
            value:
              type: ":integer"
              value: "90"
              unit: ":degree"
          - name: "distanceIsMeter"
            value:
              type: ":boolean"
              value: true
          - name: "distanceIsSecond"
            value:
              type: ":boolean"
              value: false
          - name: "symbolIsDegree"
            value:
              type: ":boolean"
              value: true
          - name: "percentageIsMeter"
            value:
              type: ":boolean"
              value: false
          - name: "customIsQuantity"
            value:
              type: ":boolean"
              value: true
          - name: "customValue"
            value:
              type: ":integer"
              value: "3"
```

## Test: old unit names remain ordinary tags

```yaml
gesBlock: case
id: case-0037
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "old unit names remain ordinary tags.ges"
    program: main
```

```ges
on Start {
  emit Done(meter: #meter, degree: #degree, seconds: #seconds)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    local:
      - name: "Done"
        args:
          - name: "meter"
            value:
              type: ":tag"
              value: "meter"
          - name: "degree"
            value:
              type: ":tag"
              value: "degree"
          - name: "seconds"
            value:
              type: ":tag"
              value: "seconds"
```

## Test: numeric helper keyword casts and checks

```yaml
gesBlock: case
id: case-0038
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "numeric helper keyword casts and checks.ges"
    program: main
```

```ges
on Start {
  let integerValue be 12.0 as :number
  let floatValue be 12.5 as :number
  emit Done(integerValue: integerValue, floatValue: floatValue, integerIsNumeric: integerValue is numeric, textIsNumeric: '12' is numeric, badTextIsNumeric: 'abc' is numeric)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    local:
      - name: "Done"
        args:
          - name: "integerValue"
            value:
              type: ":integer"
              value: "12"
          - name: "floatValue"
            value:
              type: ":float"
              value: "12.5"
          - name: "integerIsNumeric"
            value:
              type: ":boolean"
              value: true
          - name: "textIsNumeric"
            value:
              type: ":boolean"
              value: false
          - name: "badTextIsNumeric"
            value:
              type: ":boolean"
              value: false
```

## Test: unicode text uses scalar length indexing and iteration

```yaml
gesBlock: case
id: case-0039
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "unicode text uses scalar length indexing and iteration.ges"
    program: main
```

```ges
﻿on Start {
  let text be 'A😀é'
  let scalars as :list be text
  emit Done(count: text[:count], first: text[:first], second: text[2], third: text[3], fourth: text[4], last: text[:last], single: '😀'[:single], listCount: scalars[:count], listSecond: scalars[2], hugeIndex: text[9223372036854775807])
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    local:
      - name: "Done"
        args:
          - name: "count"
            value:
              type: ":integer"
              value: "4"
          - name: "first"
            value:
              type: ":text"
              value: "A"
          - name: "second"
            value:
              type: ":text"
              value: "\uD83D\uDE00"
          - name: "third"
            value:
              type: ":text"
              value: "e"
          - name: "fourth"
            value:
              type: ":text"
              value: "\u0301"
          - name: "last"
            value:
              type: ":text"
              value: "\u0301"
          - name: "single"
            value:
              type: ":text"
              value: "\uD83D\uDE00"
          - name: "listCount"
            value:
              type: ":integer"
              value: "4"
          - name: "listSecond"
            value:
              type: ":text"
              value: "\uD83D\uDE00"
          - name: "hugeIndex"
            value:
              type: ":nothing"
```

## Test: portable integer boundaries division modulo and rounding

```yaml
gesBlock: case
id: case-0040
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: exact
sources:
  - name: "portable integer boundaries division modulo and rounding.ges"
    program: main
```

```ges
on Start(maximum, minimum, exactLarge, positiveInfinity, negativeInfinity) {
  emit Done(exactAdd: exactLarge + 1, exactMultiply: exactLarge * 1, addOverflow: maximum + 1, subtractOverflow: minimum - 1, multiplyOverflow: maximum * 2, divNegativeLeft: -7 div 3, divNegativeRight: 7 div -3, divBothNegative: -7 div -3, modNegativeLeft: -7 mod 3, modNegativeRight: 7 mod -3, remNegativeLeft: -7 rem 3, remNegativeRight: 7 rem -3, floorPositiveInfinity: floor positiveInfinity, ceilNegativeInfinity: ceil negativeInfinity, truncateNegative: truncate -12.9, halfEvenPositive: round half even 12.5, halfEvenOdd: round half even 13.5, halfUpNegative: round half up -12.5, halfDownNegative: round half down -12.5)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "maximum"
          value:
            type: ":integer"
            value: "9223372036854775807"
        - name: "minimum"
          value:
            type: ":integer"
            value: "-9223372036854775808"
        - name: "exactLarge"
          value:
            type: ":integer"
            value: "9007199254740993"
        - name: "positiveInfinity"
          value:
            type: ":float"
            value: "Infinity"
        - name: "negativeInfinity"
          value:
            type: ":float"
            value: "-Infinity"
    local:
      - name: "Done"
        args:
          - name: "exactAdd"
            value:
              type: ":integer"
              value: "9007199254740994"
          - name: "exactMultiply"
            value:
              type: ":integer"
              value: "9007199254740993"
          - name: "addOverflow"
            value:
              type: ":float"
              value: "9.223372036854776e18"
          - name: "subtractOverflow"
            value:
              type: ":integer"
              value: "-9223372036854775808"
          - name: "multiplyOverflow"
            value:
              type: ":float"
              value: "1.8446744073709552e19"
          - name: "divNegativeLeft"
            value:
              type: ":integer"
              value: "-3"
          - name: "divNegativeRight"
            value:
              type: ":integer"
              value: "-3"
          - name: "divBothNegative"
            value:
              type: ":integer"
              value: "2"
          - name: "modNegativeLeft"
            value:
              type: ":integer"
              value: "2"
          - name: "modNegativeRight"
            value:
              type: ":integer"
              value: "-2"
          - name: "remNegativeLeft"
            value:
              type: ":integer"
              value: "-1"
          - name: "remNegativeRight"
            value:
              type: ":integer"
              value: "1"
          - name: "floorPositiveInfinity"
            value:
              type: ":integer"
              value: "9223372036854775807"
          - name: "ceilNegativeInfinity"
            value:
              type: ":integer"
              value: "-9223372036854775808"
          - name: "truncateNegative"
            value:
              type: ":integer"
              value: "-12"
          - name: "halfEvenPositive"
            value:
              type: ":integer"
              value: "12"
          - name: "halfEvenOdd"
            value:
              type: ":integer"
              value: "14"
          - name: "halfUpNegative"
            value:
              type: ":integer"
              value: "-13"
          - name: "halfDownNegative"
            value:
              type: ":integer"
              value: "-12"
```
