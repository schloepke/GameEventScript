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
  let castMeter as :quantity(m) be 100
  let rawMeter as :number be castMeter
  emit Done(distance: 100m, duration: 15s, castMeter: castMeter, rawMeter: rawMeter, isMeter: castMeter is :quantity(m), isSecond: 15s is :quantity(s), isFloat: castMeter is :number, sameAdd: 100m + 50m, mixedAdd: 100m + 50, wrongAdd: 100m + 5s, scaleLeft: 100m * 2, scaleRight: 2 * 100m, divideScalar: 100m / 2, divideSame: 100m / 25m, unitModulo: 370m mod 90m, unitModuloScalar: 100m mod 3, wrapMeter: wrap degree 10m)
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
  emit Done(floorBare: floor value, floorCall: floor(value), floorNegative: floor -10.4, ceilNegative: ceil -10.4, truncateNegative: truncate -10.4, halfEven_12: round half even 12.5, halfEven_13: round half even 13.5, halfUpNegative: round half up -12.5, halfDownNegative: round half down -12.5, wrapNegative: wrap degree heading, wrapUnitless: wrap degree unitlessHeading, radians: radiansValue, degrees: deg radiansValue, wrapWrongUnit: wrap degree meterValue, radiansWrongUnit: rad meterValue, fromRadiansWrongUnit: deg meterValue)
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
  let score as :number be 12 when age is :boolean, or 15 when age is :number, otherwise 5
  emit Done(negInt: negInt, negFloat: negFloat, negPercent: negPercent, restored: restored, boolNegation: not false, hpCheck: hp is 0 or less, manaCheck: mana is at least 3, handCheck: hand is empty, targetCheck: target has value, oldHasValue: target has value, newHasValue: target has value, oldEmpty: empty hand, newEmpty: hand is empty, score: score, floor: floor 12.7, ceil: ceil 12.1, roundEven_12: round half even 12.5, roundEven_13: round half even 13.5, truncate: truncate -12.9, halfUp: round half up -12.5, halfDown: round half down -12.5)
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
  let projected be :list[:select item from 1 to 2↦item + 1]
  emit Done(dot: x · y, smallDot: x ⋅ y, unicodeMinus: y − x, andAlias: true ∧ false, orAlias: false ∨ true, xorAlias: true ⊕ false, tagMember: #boss ∈ tags, tagNotMember: #ghost ∉ tags, dictKey: 'name' ∈ unit, infinity: ∞ as :number, infinityAdd: ∞ + 1, negativeInfinity: -∞ as :number, unicodeNegativeInfinity: −∞ + 1, piTag: pi as :number, piAlias: ∏ as :number, piGreater: ∏ > 3, eTag: e as :number, eAlias: ℇ as :number, eLess: ℇ < 3, tauTag: tau as :number, tauAlias: τ as :number, tauGreater: τ > 6, phiTag: 1.6180339887498948 as :number, phiAlias: 1.6180339887498948 as :number, phiBetween: 1.6180339887498948 > 1 and 1.6180339887498948 < 2, sqrtTag: sqrt 81, sqrtAlias: √81, cbrtTag: cbrt 27, cbrtAlias: ∛27, projectionAlias: projected[2])
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

### Expectation

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
  let integerValue be 12.0 as :number
  let floatValue be 12.5 as :number
  emit Done(integerValue: integerValue, floatValue: floatValue, integerIsNumeric: integerValue is numeric, textIsNumeric: '12' is numeric, badTextIsNumeric: 'abc' is numeric)
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
