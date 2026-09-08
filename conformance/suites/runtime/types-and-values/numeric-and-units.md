---
formatVersion: 1
suiteId: "runtime.types-and-values.numeric-and-units"
title: "Types and Values — Numeric Values and Units"
categories: [conformance]
---

# Types and Values — Numeric Values and Units

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers numeric operations, units, constants, rounding, aliases, boundaries, and floating-point behavior.

---

## Test: integer arithmetic preserves integer results

This runtime case exercises “integer arithmetic preserves integer results” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
on Start {
  emit Done(add: 1 + 2, subtract: 5 - 3, multiply: 6 * 7, modulo: 7 mod 4, divide: 7 / 2, mixed: 7 * 0.5)
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
          - name: "add"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "subtract"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "multiply"
            value:
              type: ":Number.int64"
              value: "42"
          - name: "modulo"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "divide"
            value:
              type: ":Number.binary64"
              value: "3.5"
          - name: "mixed"
            value:
              type: ":Number.binary64"
              value: "3.5"
```

---

## Test: integer division modulo and remainder semantics

This runtime case exercises “integer division modulo and remainder semantics” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
on Start(a, b) {
  emit Done(divPositive: a div b, divNegative: (0 - a) div b, divNegativeDivisor: a div (0 - b), floatDiv: 7.5 div 2, sameUnitDiv: 7m div 2m, unitDivScalar: 7m div 2, modPositive: a mod b, modNegative: (0 - a) mod b, modNegativeDivisor: a mod (0 - b), remPositive: a rem b, remNegative: (0 - a) rem b, remNegativeDivisor: a rem (0 - b), divZero: a div 0, remZero: a rem 0)
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
      args:
        - name: "a"
          value:
            type: ":Number.int64"
            value: "7"
        - name: "b"
          value:
            type: ":Number.int64"
            value: "3"
    local:
      - name: "Done"
        args:
          - name: "divPositive"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "divNegative"
            value:
              type: ":Number.int64"
              value: "-3"
          - name: "divNegativeDivisor"
            value:
              type: ":Number.int64"
              value: "-3"
          - name: "floatDiv"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "sameUnitDiv"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "unitDivScalar"
            value:
              type: ":Quantity.int64"
              value: "3"
              unit: ":meter"
          - name: "modPositive"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "modNegative"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "modNegativeDivisor"
            value:
              type: ":Number.int64"
              value: "-2"
          - name: "remPositive"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "remNegative"
            value:
              type: ":Number.int64"
              value: "-1"
          - name: "remNegativeDivisor"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "divZero"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "remZero"
            value:
              type: ":Number.binary64"
              value: "NaN"
```

---

## Test: percentage arithmetic keeps ratios and applies relative bases

This runtime case exercises “percentage arithmetic keeps ratios and applies relative bases” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
on Start {
  emit Done(addToBase: 100 + 5%, subtractFromBase: 100 - 5%, multiplyBaseRight: 100 * 5%, divideBaseByPercent: 100 / 5%, reverseAdd: 5% + 100, reverseSubtract: 5% - 100, percentAdd: 15% + 15%, percentSubtract: 15% - 5%, percentScaleRight: 15% * 2, percentDivideScalar: 15% / 3, scalarScaleLeft: 2 * 15%, percentProduct: 15% * 15%, percentRatio: 15% / 15%, percentDivideZero: 15% / 0)
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
          - name: "addToBase"
            value:
              type: ":Number.int64"
              value: "105"
          - name: "subtractFromBase"
            value:
              type: ":Number.int64"
              value: "95"
          - name: "multiplyBaseRight"
            value:
              type: ":Number.int64"
              value: "5"
          - name: "divideBaseByPercent"
            value:
              type: ":Number.int64"
              value: "2000"
          - name: "reverseAdd"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "reverseSubtract"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "percentAdd"
            value:
              type: ":Percentage"
              value: "0.3"
          - name: "percentSubtract"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "percentScaleRight"
            value:
              type: ":Number.binary64"
              value: "0.3"
          - name: "percentDivideScalar"
            value:
              type: ":Percentage"
              value: "0.05"
          - name: "scalarScaleLeft"
            value:
              type: ":Number.binary64"
              value: "0.3"
          - name: "percentProduct"
            value:
              type: ":Percentage"
              value: "0.0225"
          - name: "percentRatio"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "percentDivideZero"
            value:
              type: ":Number.binary64"
              value: "Infinity"
```

---

## Test: percentage arithmetic preserves compatible numeric units

This runtime case exercises “percentage arithmetic preserves compatible numeric units” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
on Start {
  emit Done(meterAddPercent: 100m + 5%, meterSubtractPercent: 100m - 5%, meterMultiplyPercentRight: 100m * 5%, meterMultiplyPercentLeft: 5% * 100m, meterDividePercent: 100m / 5%, reverseMeterAdd: 5% + 100m, reverseMeterSubtract: 5% - 100m, percentDivideMeter: 5% / 100m)
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
          - name: "meterAddPercent"
            value:
              type: ":Quantity.int64"
              value: "105"
              unit: ":meter"
          - name: "meterSubtractPercent"
            value:
              type: ":Quantity.int64"
              value: "95"
              unit: ":meter"
          - name: "meterMultiplyPercentRight"
            value:
              type: ":Quantity.int64"
              value: "5"
              unit: ":meter"
          - name: "meterMultiplyPercentLeft"
            value:
              type: ":Quantity.int64"
              value: "5"
              unit: ":meter"
          - name: "meterDividePercent"
            value:
              type: ":Quantity.int64"
              value: "2000"
              unit: ":meter"
          - name: "reverseMeterAdd"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "reverseMeterSubtract"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "percentDivideMeter"
            value:
              type: ":Number.binary64"
              value: "NaN"
```

---

## Test: degree literals preserve raw angles and wrap explicitly

This runtime case exercises “degree literals preserve raw angles and wrap explicitly” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
on Start {
  let fromLiteral be 90°
  let overLiteral be 360°
  let floatLiteral be 43.9°
  let addOpen be 360° + 90°
  let addOver be 180° + 200°
  let subtractOpen be 10° - 40°
  let negOpen be -90°
  let castOpen be (450) as :Quantity(°)
  let wrapNegative be wrap degree -10°
  let wrapOver be wrap degree 370°
  let wrapLarge be wrap degree 1000°
  let wrapCast be wrap degree castOpen
  let textValue be (43.9°) as :Text
  let floatValue be (43.9°) as :Number
  emit Done(fromLiteral: fromLiteral, overLiteral: overLiteral, floatLiteral: floatLiteral, addOpen: addOpen, addOver: addOver, subtractOpen: subtractOpen, negOpen: negOpen, castOpen: castOpen, wrapNegative: wrapNegative, wrapOver: wrapOver, wrapLarge: wrapLarge, wrapCast: wrapCast, textValue: textValue, floatValue: floatValue, isDegree: fromLiteral is :Quantity(°))
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
          - name: "fromLiteral"
            value:
              type: ":Quantity.int64"
              value: "90"
              unit: ":degree"
          - name: "overLiteral"
            value:
              type: ":Quantity.int64"
              value: "360"
              unit: ":degree"
          - name: "floatLiteral"
            value:
              type: ":Quantity.binary64"
              value: "43.9"
              unit: ":degree"
          - name: "addOpen"
            value:
              type: ":Quantity.int64"
              value: "450"
              unit: ":degree"
          - name: "addOver"
            value:
              type: ":Quantity.int64"
              value: "380"
              unit: ":degree"
          - name: "subtractOpen"
            value:
              type: ":Quantity.int64"
              value: "-30"
              unit: ":degree"
          - name: "negOpen"
            value:
              type: ":Quantity.int64"
              value: "-90"
              unit: ":degree"
          - name: "castOpen"
            value:
              type: ":Quantity.int64"
              value: "450"
              unit: ":degree"
          - name: "wrapNegative"
            value:
              type: ":Quantity.int64"
              value: "350"
              unit: ":degree"
          - name: "wrapOver"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: ":degree"
          - name: "wrapLarge"
            value:
              type: ":Quantity.int64"
              value: "280"
              unit: ":degree"
          - name: "wrapCast"
            value:
              type: ":Quantity.int64"
              value: "90"
              unit: ":degree"
          - name: "textValue"
            value:
              type: ":Text"
              value: "43.9\u00B0"
          - name: "floatValue"
            value:
              type: ":Quantity.binary64"
              value: "43.9"
              unit: ":degree"
          - name: "isDegree"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: degree arithmetic percentages helpers and comparisons

This runtime case exercises “degree arithmetic percentages helpers and comparisons” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
on Start {
  emit Done(addNumber: 120° + 10, subtractNumber: 120° - 150, addPercentRight: 120° + 10%, addPercentLeft: 10% + 120°, subtractPercent: 120° - 10%, multiplyPercentRight: 120° * 10%, multiplyPercentLeft: 10% * 120°, multiplyFloat: 30° * 0.2, divideFloat: 30° / 0.2, dividePercent: 120° / 10%, wrappedDividePercent: wrap degree (120° / 10%), moduloDegree: 370° mod 90°, moduloPercent: 120° mod 10%, divideZero: 120° / 0, moduloZero: 120° mod 0, reverseSubtract: 10 - 120°, reverseDivide: 10 / 120°, reverseModulo: 10 mod 120°, floorDegree: floor 43.9°, ceilDegree: ceil 43.1°, roundEvenDegree: round half even 42.5°, minDegree: min of 350° and 10°, maxDegree: max of 350° and 10°, greaterDegree: 350° > 10°, lessMixed: 10° < 20, percentCompare: 10° < 10%)
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
          - name: "addNumber"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "subtractNumber"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "addPercentRight"
            value:
              type: ":Quantity.int64"
              value: "132"
              unit: ":degree"
          - name: "addPercentLeft"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "subtractPercent"
            value:
              type: ":Quantity.int64"
              value: "108"
              unit: ":degree"
          - name: "multiplyPercentRight"
            value:
              type: ":Quantity.int64"
              value: "12"
              unit: ":degree"
          - name: "multiplyPercentLeft"
            value:
              type: ":Quantity.int64"
              value: "12"
              unit: ":degree"
          - name: "multiplyFloat"
            value:
              type: ":Quantity.int64"
              value: "6"
              unit: ":degree"
          - name: "divideFloat"
            value:
              type: ":Quantity.int64"
              value: "150"
              unit: ":degree"
          - name: "dividePercent"
            value:
              type: ":Quantity.int64"
              value: "1200"
              unit: ":degree"
          - name: "wrappedDividePercent"
            value:
              type: ":Quantity.int64"
              value: "120"
              unit: ":degree"
          - name: "moduloDegree"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: ":degree"
          - name: "moduloPercent"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "divideZero"
            value:
              type: ":Quantity.binary64"
              value: "Infinity"
              unit: ":degree"
          - name: "moduloZero"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "reverseSubtract"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "reverseDivide"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "reverseModulo"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "floorDegree"
            value:
              type: ":Number.int64"
              value: "43"
          - name: "ceilDegree"
            value:
              type: ":Number.int64"
              value: "44"
          - name: "roundEvenDegree"
            value:
              type: ":Number.int64"
              value: "42"
          - name: "minDegree"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: ":degree"
          - name: "maxDegree"
            value:
              type: ":Quantity.int64"
              value: "350"
              unit: ":degree"
          - name: "greaterDegree"
            value:
              type: ":Boolean"
              value: true
          - name: "lessMixed"
            value:
              type: ":Boolean"
              value: false
          - name: "percentCompare"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: numeric unit literals casts and strict arithmetic

This runtime case exercises “numeric unit literals casts and strict arithmetic” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
on Start {
  let castMeter be (100) as :Quantity(m)
  let rawMeter be (castMeter) as :Number
  emit Done(distance: 100m, duration: 15s, castMeter: castMeter, rawMeter: rawMeter, isMeter: castMeter is :Quantity(m), isSecond: 15s is :Quantity(s), isFloat: castMeter is :Number, sameAdd: 100m + 50m, mixedAdd: 100m + 50, wrongAdd: 100m + 5s, scaleLeft: 100m * 2, scaleRight: 2 * 100m, divideScalar: 100m / 2, divideSame: 100m / 25m, unitModulo: 370m mod 90m, unitModuloScalar: 100m mod 3, wrapMeter: wrap degree 10m)
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
          - name: "castMeter"
            value:
              type: ":Quantity.int64"
              value: "100"
              unit: ":meter"
          - name: "rawMeter"
            value:
              type: ":Quantity.int64"
              value: "100"
              unit: ":meter"
          - name: "isMeter"
            value:
              type: ":Boolean"
              value: true
          - name: "isSecond"
            value:
              type: ":Boolean"
              value: true
          - name: "isFloat"
            value:
              type: ":Boolean"
              value: true
          - name: "sameAdd"
            value:
              type: ":Quantity.int64"
              value: "150"
              unit: ":meter"
          - name: "mixedAdd"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "wrongAdd"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "scaleLeft"
            value:
              type: ":Quantity.int64"
              value: "200"
              unit: ":meter"
          - name: "scaleRight"
            value:
              type: ":Quantity.int64"
              value: "200"
              unit: ":meter"
          - name: "divideScalar"
            value:
              type: ":Quantity.int64"
              value: "50"
              unit: ":meter"
          - name: "divideSame"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "unitModulo"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: ":meter"
          - name: "unitModuloScalar"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "wrapMeter"
            value:
              type: ":Number.binary64"
              value: "NaN"
```

---

## Test: required standard integer and degree extensions are intrinsic

This runtime case exercises “required standard integer and degree extensions are intrinsic” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
on Start(value, heading, unitlessHeading, meterValue) {
  let radiansValue be rad 180°
  emit Done(floorBare: floor value, floorCall: floor(value), floorNegative: floor -10.4, ceilNegative: ceil -10.4, truncateNegative: truncate -10.4, halfEven12: round half even 12.5, halfEven13: round half even 13.5, halfUpNegative: round half up -12.5, halfDownNegative: round half down -12.5, wrapNegative: wrap degree heading, wrapUnitless: wrap degree unitlessHeading, radians: radiansValue, degrees: deg radiansValue, wrapWrongUnit: wrap degree meterValue, radiansWrongUnit: rad meterValue, fromRadiansWrongUnit: deg meterValue)
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
      args:
        - name: "value"
          value:
            type: ":Number.binary64"
            value: "10.4"
        - name: "heading"
          value:
            type: ":Quantity.binary64"
            value: "-10"
            unit: ":degree"
        - name: "unitlessHeading"
          value:
            type: ":Number.binary64"
            value: "370"
        - name: "meterValue"
          value:
            type: ":Quantity.binary64"
            value: "10"
            unit: ":meter"
    local:
      - name: "Done"
        args:
          - name: "floorBare"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "floorCall"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "floorNegative"
            value:
              type: ":Number.int64"
              value: "-11"
          - name: "ceilNegative"
            value:
              type: ":Number.int64"
              value: "-10"
          - name: "truncateNegative"
            value:
              type: ":Number.int64"
              value: "-10"
          - name: "halfEven12"
            value:
              type: ":Number.int64"
              value: "12"
          - name: "halfEven13"
            value:
              type: ":Number.int64"
              value: "14"
          - name: "halfUpNegative"
            value:
              type: ":Number.int64"
              value: "-13"
          - name: "halfDownNegative"
            value:
              type: ":Number.int64"
              value: "-12"
          - name: "wrapNegative"
            value:
              type: ":Quantity.int64"
              value: "350"
              unit: ":degree"
          - name: "wrapUnitless"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: ":degree"
          - name: "radians"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "degrees"
            value:
              type: ":Quantity.int64"
              value: "180"
              unit: ":degree"
          - name: "wrapWrongUnit"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "radiansWrongUnit"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "fromRadiansWrongUnit"
            value:
              type: ":Number.binary64"
              value: "NaN"
```

---

## Test: float edge values stay observable

This runtime case exercises “float edge values stay observable” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
on Start {
  emit Done(divZero: 1 / 0, modZero: 1 mod 0, overflow: 9999999999999999999999999999 * 9999999999999999999999999999)
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
          - name: "divZero"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "modZero"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "overflow"
            value:
              type: ":Number.binary64"
              value: "1e56"
```

---

## Test: unary domain conditional and standard integer rounding forms

This runtime case exercises “unary domain conditional and standard integer rounding forms” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
on Start(hp, mana, hand, target, age) {
  let negInt be -12
  let negFloat be -12.34
  let negPercent be -25%
  let restored be -negFloat
  let score be (12 when age is :Boolean, or 15 when age is :Number, otherwise 5) as :Number
  emit Done(negInt: negInt, negFloat: negFloat, negPercent: negPercent, restored: restored, boolNegation: not false, hpCheck: hp is 0 or less, manaCheck: mana is at least 3, handCheck: hand is empty, targetCheck: target has value, oldHasValue: target has value, newHasValue: target has value, oldEmpty: empty hand, newEmpty: hand is empty, score: score, floor: floor 12.7, ceil: ceil 12.1, roundEven12: round half even 12.5, roundEven13: round half even 13.5, truncate: truncate -12.9, halfUp: round half up -12.5, halfDown: round half down -12.5)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |
| step-0002 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "hp"
          value:
            type: ":Number.binary64"
            value: "-1"
        - name: "mana"
          value:
            type: ":Number.int64"
            value: "3"
        - name: "hand"
          value:
            type: ":List"
            items: []
        - name: "target"
          value:
            type: ":Text"
            value: "orc"
        - name: "age"
          value:
            type: ":Boolean"
            value: true
    local:
      - name: "Done"
        args:
          - name: "negInt"
            value:
              type: ":Number.int64"
              value: "-12"
          - name: "negFloat"
            value:
              type: ":Number.binary64"
              value: "-12.34"
          - name: "negPercent"
            value:
              type: ":Percentage"
              value: "-0.25"
          - name: "restored"
            value:
              type: ":Number.binary64"
              value: "12.34"
          - name: "boolNegation"
            value:
              type: ":Boolean"
              value: true
          - name: "hpCheck"
            value:
              type: ":Boolean"
              value: true
          - name: "manaCheck"
            value:
              type: ":Boolean"
              value: true
          - name: "handCheck"
            value:
              type: ":Boolean"
              value: true
          - name: "targetCheck"
            value:
              type: ":Boolean"
              value: true
          - name: "oldHasValue"
            value:
              type: ":Boolean"
              value: true
          - name: "newHasValue"
            value:
              type: ":Boolean"
              value: true
          - name: "oldEmpty"
            value:
              type: ":Boolean"
              value: true
          - name: "newEmpty"
            value:
              type: ":Boolean"
              value: true
          - name: "score"
            value:
              type: ":Number.int64"
              value: "12"
          - name: "floor"
            value:
              type: ":Number.int64"
              value: "12"
          - name: "ceil"
            value:
              type: ":Number.int64"
              value: "13"
          - name: "roundEven12"
            value:
              type: ":Number.int64"
              value: "12"
          - name: "roundEven13"
            value:
              type: ":Number.int64"
              value: "14"
          - name: "truncate"
            value:
              type: ":Number.int64"
              value: "-12"
          - name: "halfUp"
            value:
              type: ":Number.int64"
              value: "-13"
          - name: "halfDown"
            value:
              type: ":Number.int64"
              value: "-12"
  step-0002:
    input:
      args:
        - name: "hp"
          value:
            type: ":Number.binary64"
            value: "-1"
        - name: "mana"
          value:
            type: ":Number.int64"
            value: "3"
        - name: "hand"
          value:
            type: ":List"
            items: []
        - name: "target"
          value:
            type: ":Text"
            value: "orc"
        - name: "age"
          value:
            type: ":Text"
            value: "x"
    local:
      - name: "Done"
        args:
          - name: "negInt"
            value:
              type: ":Number.int64"
              value: "-12"
          - name: "negFloat"
            value:
              type: ":Number.binary64"
              value: "-12.34"
          - name: "negPercent"
            value:
              type: ":Percentage"
              value: "-0.25"
          - name: "restored"
            value:
              type: ":Number.binary64"
              value: "12.34"
          - name: "boolNegation"
            value:
              type: ":Boolean"
              value: true
          - name: "hpCheck"
            value:
              type: ":Boolean"
              value: true
          - name: "manaCheck"
            value:
              type: ":Boolean"
              value: true
          - name: "handCheck"
            value:
              type: ":Boolean"
              value: true
          - name: "targetCheck"
            value:
              type: ":Boolean"
              value: true
          - name: "oldHasValue"
            value:
              type: ":Boolean"
              value: true
          - name: "newHasValue"
            value:
              type: ":Boolean"
              value: true
          - name: "oldEmpty"
            value:
              type: ":Boolean"
              value: true
          - name: "newEmpty"
            value:
              type: ":Boolean"
              value: true
          - name: "score"
            value:
              type: ":Number.int64"
              value: "5"
          - name: "floor"
            value:
              type: ":Number.int64"
              value: "12"
          - name: "ceil"
            value:
              type: ":Number.int64"
              value: "13"
          - name: "roundEven12"
            value:
              type: ":Number.int64"
              value: "12"
          - name: "roundEven13"
            value:
              type: ":Number.int64"
              value: "14"
          - name: "truncate"
            value:
              type: ":Number.int64"
              value: "-12"
          - name: "halfUp"
            value:
              type: ":Number.int64"
              value: "-13"
          - name: "halfDown"
            value:
              type: ":Number.int64"
              value: "-12"
```

---

## Test: xor word operator and power symbol operator

This runtime case exercises “xor word operator and power symbol operator” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
on Start {
  emit Done(wordTrue: true xor false, wordFalse: true xor true, square: 3 ^ 2, rightAssoc: 2 ^ 3 ^ 2, fractional: 9 ^ 0.5, negativeExponent: 2 ^ -2, negatedSquare: -3 ^ 2, negativeBaseSquare: (0 - 3) ^ 2, precedence: 2 + 3 ^ 2 * 4, unitPower: 2m ^ 2)
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
          - name: "wordTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "wordFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "square"
            value:
              type: ":Number.int64"
              value: "9"
          - name: "rightAssoc"
            value:
              type: ":Number.int64"
              value: "512"
          - name: "fractional"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "negativeExponent"
            value:
              type: ":Number.binary64"
              value: "0.25"
          - name: "negatedSquare"
            value:
              type: ":Number.int64"
              value: "-9"
          - name: "negativeBaseSquare"
            value:
              type: ":Number.int64"
              value: "9"
          - name: "precedence"
            value:
              type: ":Number.int64"
              value: "38"
          - name: "unitPower"
            value:
              type: ":Number.binary64"
              value: "NaN"
```

---

## Test: implicit multiplication superscripts and unicode operator aliases

This runtime case exercises “implicit multiplication superscripts and unicode operator aliases” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
on Start(x, y, flag) {
  emit Done(doubleX: 2x, floatY: 2.5y, meter: 2m, square: x², cube: y³, multiply: x × y, divide: y ÷ x, lessOrEqual: x ≤ y, greaterOrEqual: y ≥ x, notEqual: x ≠ y, notFlag: ¬flag)
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
      args:
        - name: "x"
          value:
            type: ":Number.int64"
            value: "3"
        - name: "y"
          value:
            type: ":Number.int64"
            value: "6"
        - name: "flag"
          value:
            type: ":Boolean"
            value: false
    local:
      - name: "Done"
        args:
          - name: "doubleX"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "floatY"
            value:
              type: ":Number.int64"
              value: "15"
          - name: "meter"
            value:
              type: ":Quantity.int64"
              value: "2"
              unit: ":meter"
          - name: "square"
            value:
              type: ":Number.int64"
              value: "9"
          - name: "cube"
            value:
              type: ":Number.int64"
              value: "216"
          - name: "multiply"
            value:
              type: ":Number.int64"
              value: "18"
          - name: "divide"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "lessOrEqual"
            value:
              type: ":Boolean"
              value: true
          - name: "greaterOrEqual"
            value:
              type: ":Boolean"
              value: true
          - name: "notEqual"
            value:
              type: ":Boolean"
              value: true
          - name: "notFlag"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: additional unicode mathematical aliases

This runtime case exercises “additional unicode mathematical aliases” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
on Start(x, y) {
  let tags be [#boss, #scout]
  let unit be [name: 'Ada', hp: 10]
  let projected be :List[:select item from 1 to 2↦item + 1]
  emit Done(dot: x · y, smallDot: x ⋅ y, unicodeMinus: y − x, andAlias: true ∧ false, orAlias: false ∨ true, xorAlias: true ⊕ false, tagMember: #boss ∈ tags, tagNotMember: #ghost ∉ tags, dictKey: 'name' ∈ unit, infinity: ∞ as :Number, infinityAdd: ∞ + 1, negativeInfinity: -∞ as :Number, unicodeNegativeInfinity: −∞ + 1, piTag: pi as :Number, piAlias: ∏ as :Number, piGreater: ∏ > 3, eTag: e as :Number, eAlias: ℇ as :Number, eLess: ℇ < 3, tauTag: tau as :Number, tauAlias: τ as :Number, tauGreater: τ > 6, phiTag: 1.6180339887498948 as :Number, phiAlias: 1.6180339887498948 as :Number, phiBetween: 1.6180339887498948 > 1 and 1.6180339887498948 < 2, sqrtTag: sqrt 81, sqrtAlias: √81, cbrtTag: cbrt 27, cbrtAlias: ∛27, projectionAlias: projected[2])
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
      args:
        - name: "x"
          value:
            type: ":Number.int64"
            value: "3"
        - name: "y"
          value:
            type: ":Number.int64"
            value: "6"
    local:
      - name: "Done"
        args:
          - name: "dot"
            value:
              type: ":Number.int64"
              value: "18"
          - name: "smallDot"
            value:
              type: ":Number.int64"
              value: "18"
          - name: "unicodeMinus"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "andAlias"
            value:
              type: ":Boolean"
              value: false
          - name: "orAlias"
            value:
              type: ":Boolean"
              value: true
          - name: "xorAlias"
            value:
              type: ":Boolean"
              value: true
          - name: "tagMember"
            value:
              type: ":Boolean"
              value: true
          - name: "tagNotMember"
            value:
              type: ":Boolean"
              value: true
          - name: "dictKey"
            value:
              type: ":Boolean"
              value: true
          - name: "infinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "infinityAdd"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "negativeInfinity"
            value:
              type: ":Number.binary64"
              value: "-Infinity"
          - name: "unicodeNegativeInfinity"
            value:
              type: ":Number.binary64"
              value: "-Infinity"
          - name: "piTag"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "piAlias"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "piGreater"
            value:
              type: ":Boolean"
              value: true
          - name: "eTag"
            value:
              type: ":Number.binary64"
              value: "2.71828182845905"
          - name: "eAlias"
            value:
              type: ":Number.binary64"
              value: "2.71828182845905"
          - name: "eLess"
            value:
              type: ":Boolean"
              value: true
          - name: "tauTag"
            value:
              type: ":Number.binary64"
              value: "6.28318530717959"
          - name: "tauAlias"
            value:
              type: ":Number.binary64"
              value: "6.28318530717959"
          - name: "tauGreater"
            value:
              type: ":Boolean"
              value: true
          - name: "phiTag"
            value:
              type: ":Number.binary64"
              value: "1.61803398874989"
          - name: "phiAlias"
            value:
              type: ":Number.binary64"
              value: "1.61803398874989"
          - name: "phiBetween"
            value:
              type: ":Boolean"
              value: true
          - name: "sqrtTag"
            value:
              type: ":Number.int64"
              value: "9"
          - name: "sqrtAlias"
            value:
              type: ":Number.int64"
              value: "9"
          - name: "cbrtTag"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "cbrtAlias"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "projectionAlias"
            value:
              type: ":Number.int64"
              value: "3"
```

---

## Test: floating point equality uses ulp tolerance

This runtime case exercises “floating point equality uses ulp tolerance” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

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
          - name: "exact"
            value:
              type: ":Boolean"
              value: true
          - name: "far"
            value:
              type: ":Boolean"
              value: false
          - name: "tagExact"
            value:
              type: ":Boolean"
              value: true
          - name: "unitNear"
            value:
              type: ":Boolean"
              value: true
          - name: "unitMismatch"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: numeric literals allow underscore digit separators

This runtime case exercises “numeric literals allow underscore digit separators” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
on Start(x) {
  emit Done(integer: 100_000, double: 100_000.25, percent: 1_5%, meter: 1_000m, degree: 90_0°, second: 3_600s, square: 1_2², scaled: 2_5x)
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
      args:
        - name: "x"
          value:
            type: ":Number.int64"
            value: "4"
    local:
      - name: "Done"
        args:
          - name: "integer"
            value:
              type: ":Number.int64"
              value: "100000"
          - name: "double"
            value:
              type: ":Number.binary64"
              value: "100000.25"
          - name: "percent"
            value:
              type: ":Percentage"
              value: "0.15"
          - name: "meter"
            value:
              type: ":Quantity.int64"
              value: "1000"
              unit: ":meter"
          - name: "degree"
            value:
              type: ":Quantity.int64"
              value: "900"
              unit: ":degree"
          - name: "second"
            value:
              type: ":Quantity.int64"
              value: "3600"
              unit: ":second"
          - name: "square"
            value:
              type: ":Number.int64"
              value: "144"
          - name: "scaled"
            value:
              type: ":Number.int64"
              value: "100"
```

---

## Test: explicit numeric identifier suffixes and compact math remain valid

This runtime case exercises “explicit numeric identifier suffixes and compact math remain valid” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
on Start(x, values) {
  let player0 be x
  let player1 be player0 + 1
  let player22 be player1 + 21
  emit Done(playerZero: player0, playerOne: player1, playerMany: player22, compactMultiply: 2x, square: x², lookup: values[2])
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
      args:
        - name: "x"
          value:
            type: ":Number.int64"
            value: "3"
        - name: "values"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "10"
              - type: ":Number.int64"
                value: "20"
    local:
      - name: "Done"
        args:
          - name: "playerZero"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "playerOne"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "playerMany"
            value:
              type: ":Number.int64"
              value: "25"
          - name: "compactMultiply"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "square"
            value:
              type: ":Number.int64"
              value: "9"
          - name: "lookup"
            value:
              type: ":Number.int64"
              value: "20"
```

---

## Test: quantity casts checks and constructors normalize numeric units

This runtime case exercises “quantity casts checks and constructors normalize numeric units” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
record :Quantity as {
  value: :Number
}

on Start {
  let distanceValue be 100 as :Quantity(m)
  let duration be :Quantity(s)(5)
  let heading be :Quantity(degree)(90)
  let strippedDistance be distanceValue as :Quantity(none)
  let custom be :Quantity(value: 3)
  emit Done(distance: distanceValue, duration: duration, heading: heading, strippedDistance: strippedDistance, strippedIsUnitless: strippedDistance is :Quantity(none), distanceIsMeter: distanceValue is :Quantity(m), distanceIsSecond: distanceValue is :Quantity(s), symbolIsDegree: heading is :Quantity(°), percentageIsMeter: 50% is :Quantity(m), customIsQuantity: custom is :Quantity, customValue: custom.value)
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
    local:
      - name: "Done"
        args:
          - name: "distance"
            value:
              type: ":Quantity.int64"
              value: "100"
              unit: ":meter"
          - name: "duration"
            value:
              type: ":Quantity.int64"
              value: "5"
              unit: ":second"
          - name: "heading"
            value:
              type: ":Quantity.int64"
              value: "90"
              unit: ":degree"
          - name: "strippedDistance"
            value:
              type: ":Number.int64"
              value: "100"
          - name: "strippedIsUnitless"
            value:
              type: ":Boolean"
              value: true
          - name: "distanceIsMeter"
            value:
              type: ":Boolean"
              value: true
          - name: "distanceIsSecond"
            value:
              type: ":Boolean"
              value: false
          - name: "symbolIsDegree"
            value:
              type: ":Boolean"
              value: true
          - name: "percentageIsMeter"
            value:
              type: ":Boolean"
              value: false
          - name: "customIsQuantity"
            value:
              type: ":Boolean"
              value: true
          - name: "customValue"
            value:
              type: ":Number.int64"
              value: "3"
```

---

## Test: numeric helper keyword casts and checks

This runtime case exercises “numeric helper keyword casts and checks” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
on Start {
  let integerValue be 12.0 as :Number
  let floatValue be 12.5 as :Number
  emit Done(integerValue: integerValue, floatValue: floatValue, integerIsNumeric: integerValue is numeric, canonicalIntegerCheck: integerValue is integer, textIsNumeric: '12' is numeric, badTextIsNumeric: 'abc' is numeric, textLeftConcat: '10' + 20, textRightConcat: 10 + '20')
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
    local:
      - name: "Done"
        args:
          - name: "integerValue"
            value:
              type: ":Number.int64"
              value: "12"
          - name: "floatValue"
            value:
              type: ":Number.binary64"
              value: "12.5"
          - name: "integerIsNumeric"
            value:
              type: ":Boolean"
              value: true
          - name: "canonicalIntegerCheck"
            value:
              type: ":Boolean"
              value: true
          - name: "textIsNumeric"
            value:
              type: ":Boolean"
              value: false
          - name: "badTextIsNumeric"
            value:
              type: ":Boolean"
              value: false
          - name: "textLeftConcat"
            value:
              type: ":Text"
              value: "1020"
          - name: "textRightConcat"
            value:
              type: ":Text"
              value: "1020"
```

---

## Test: portable integer boundaries division modulo and rounding

This runtime case exercises “portable integer boundaries division modulo and rounding” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
on Start(maximum, minimum, exactLarge, positiveInfinity, negativeInfinity) {
  emit Done(exactAdd: exactLarge + 1, exactMultiply: exactLarge * 1, addOverflow: maximum + 1, subtractOverflow: minimum - 1, multiplyOverflow: maximum * 2, divNegativeLeft: -7 div 3, divNegativeRight: 7 div -3, divBothNegative: -7 div -3, modNegativeLeft: -7 mod 3, modNegativeRight: 7 mod -3, remNegativeLeft: -7 rem 3, remNegativeRight: 7 rem -3, floorPositiveInfinity: floor positiveInfinity, ceilNegativeInfinity: ceil negativeInfinity, truncateNegative: truncate -12.9, halfEvenPositive: round half even 12.5, halfEvenOdd: round half even 13.5, halfUpNegative: round half up -12.5, halfDownNegative: round half down -12.5)
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
      args:
        - name: "maximum"
          value:
            type: ":Number.int64"
            value: "9223372036854775807"
        - name: "minimum"
          value:
            type: ":Number.int64"
            value: "-9223372036854775808"
        - name: "exactLarge"
          value:
            type: ":Number.int64"
            value: "9007199254740993"
        - name: "positiveInfinity"
          value:
            type: ":Number.binary64"
            value: "Infinity"
        - name: "negativeInfinity"
          value:
            type: ":Number.binary64"
            value: "-Infinity"
    local:
      - name: "Done"
        args:
          - name: "exactAdd"
            value:
              type: ":Number.int64"
              value: "9007199254740994"
          - name: "exactMultiply"
            value:
              type: ":Number.int64"
              value: "9007199254740993"
          - name: "addOverflow"
            value:
              type: ":Number.binary64"
              value: "9.223372036854776e18"
          - name: "subtractOverflow"
            value:
              type: ":Number.int64"
              value: "-9223372036854775808"
          - name: "multiplyOverflow"
            value:
              type: ":Number.binary64"
              value: "1.8446744073709552e19"
          - name: "divNegativeLeft"
            value:
              type: ":Number.int64"
              value: "-3"
          - name: "divNegativeRight"
            value:
              type: ":Number.int64"
              value: "-3"
          - name: "divBothNegative"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "modNegativeLeft"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "modNegativeRight"
            value:
              type: ":Number.int64"
              value: "-2"
          - name: "remNegativeLeft"
            value:
              type: ":Number.int64"
              value: "-1"
          - name: "remNegativeRight"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "floorPositiveInfinity"
            value:
              type: ":Number.int64"
              value: "9223372036854775807"
          - name: "ceilNegativeInfinity"
            value:
              type: ":Number.int64"
              value: "-9223372036854775808"
          - name: "truncateNegative"
            value:
              type: ":Number.int64"
              value: "-12"
          - name: "halfEvenPositive"
            value:
              type: ":Number.int64"
              value: "12"
          - name: "halfEvenOdd"
            value:
              type: ":Number.int64"
              value: "14"
          - name: "halfUpNegative"
            value:
              type: ":Number.int64"
              value: "-13"
          - name: "halfDownNegative"
            value:
              type: ":Number.int64"
              value: "-12"
```
