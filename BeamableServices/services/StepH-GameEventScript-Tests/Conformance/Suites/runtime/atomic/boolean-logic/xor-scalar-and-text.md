---
formatVersion: 1
suiteId: "runtime.atomic.boolean-logic.xor-scalar-and-text"
title: "Boolean Logic — Xor Scalars and Text"
categories: [conformance]
tags: [migrated-json-v1]
---

# Boolean Logic — Xor Scalars and Text

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers xor for nothing, boolean, numeric, percentage, and text operands.

---

## Test: xor nothing

This runtime case exercises “xor nothing” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0062
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor nothing.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBJ
on Start {
  let vNothing be nothing
  let vBooleanTrue be true
  let vBooleanFalse be false
  let vIntegerOne be 1
  let vIntegerZero be 0
  let vFloatPositive be 0.5
  let vPercentagePositive be 10%
  let vPercentageZero be 0%
  let vTextTrue be 'true'
  let vTextTrueUpper be 'TRUE'
  let vTextFalse be 'false'
  let vTextOne be '1'
  let vTextZero be '0'
  let vTextInvalid be 'hello'
  let vTextEmpty be ''
  let vTagTrue be #true
  let vTagFalse be #false
  let vTagTrueUpper as :tag be 'True'
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :vector(1, 2, 3)
  let vVectorZero be :vector(0, 0, 0)
  let vPoint be :point(1, 2, 3)
  let vPointZero be :point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice as :dice be [6, 2, 4]
  emit Done(nothing: (vNothing xor vNothing), booleanTrue: (vNothing xor vBooleanTrue), booleanFalse: (vNothing xor vBooleanFalse), integerOne: (vNothing xor vIntegerOne), integerZero: (vNothing xor vIntegerZero), floatPositive: (vNothing xor vFloatPositive), percentagePositive: (vNothing xor vPercentagePositive), percentageZero: (vNothing xor vPercentageZero), textTrue: (vNothing xor vTextTrue), textTrueUpper: (vNothing xor vTextTrueUpper), textFalse: (vNothing xor vTextFalse), textOne: (vNothing xor vTextOne), textZero: (vNothing xor vTextZero), textInvalid: (vNothing xor vTextInvalid), textEmpty: (vNothing xor vTextEmpty), tagTrue: (vNothing xor vTagTrue), tagFalse: (vNothing xor vTagFalse), tagTrueUpper: (vNothing xor vTagTrueUpper), tagPi: (vNothing xor vTagPi), tagPhi: (vNothing xor vTagPhi), tagInfinity: (vNothing xor vTagInfinity), tagNan: (vNothing xor vTagNan), tagCustom: (vNothing xor vTagCustom), vector: (vNothing xor vVector), vectorZero: (vNothing xor vVectorZero), point: (vNothing xor vPoint), pointZero: (vNothing xor vPointZero), list: (vNothing xor vList), map: (vNothing xor vMap), dice: (vNothing xor vDice))
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
          - name: "booleanTrue"
            value:
              type: ":nothing"
          - name: "booleanFalse"
            value:
              type: ":nothing"
          - name: "integerOne"
            value:
              type: ":nothing"
          - name: "integerZero"
            value:
              type: ":nothing"
          - name: "floatPositive"
            value:
              type: ":nothing"
          - name: "percentagePositive"
            value:
              type: ":nothing"
          - name: "percentageZero"
            value:
              type: ":nothing"
          - name: "textTrue"
            value:
              type: ":nothing"
          - name: "textTrueUpper"
            value:
              type: ":nothing"
          - name: "textFalse"
            value:
              type: ":nothing"
          - name: "textOne"
            value:
              type: ":nothing"
          - name: "textZero"
            value:
              type: ":nothing"
          - name: "textInvalid"
            value:
              type: ":nothing"
          - name: "textEmpty"
            value:
              type: ":nothing"
          - name: "tagTrue"
            value:
              type: ":nothing"
          - name: "tagFalse"
            value:
              type: ":nothing"
          - name: "tagTrueUpper"
            value:
              type: ":nothing"
          - name: "tagPi"
            value:
              type: ":nothing"
          - name: "tagPhi"
            value:
              type: ":nothing"
          - name: "tagInfinity"
            value:
              type: ":nothing"
          - name: "tagNan"
            value:
              type: ":nothing"
          - name: "tagCustom"
            value:
              type: ":nothing"
          - name: "vector"
            value:
              type: ":nothing"
          - name: "vectorZero"
            value:
              type: ":nothing"
          - name: "point"
            value:
              type: ":nothing"
          - name: "pointZero"
            value:
              type: ":nothing"
          - name: "list"
            value:
              type: ":nothing"
          - name: "map"
            value:
              type: ":nothing"
          - name: "dice"
            value:
              type: ":nothing"
```

---

## Test: xor booleanTrue

This runtime case exercises “xor booleanTrue” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0063
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor booleanTrue.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBK
on Start {
  let vNothing be nothing
  let vBooleanTrue be true
  let vBooleanFalse be false
  let vIntegerOne be 1
  let vIntegerZero be 0
  let vFloatPositive be 0.5
  let vPercentagePositive be 10%
  let vPercentageZero be 0%
  let vTextTrue be 'true'
  let vTextTrueUpper be 'TRUE'
  let vTextFalse be 'false'
  let vTextOne be '1'
  let vTextZero be '0'
  let vTextInvalid be 'hello'
  let vTextEmpty be ''
  let vTagTrue be #true
  let vTagFalse be #false
  let vTagTrueUpper as :tag be 'True'
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :vector(1, 2, 3)
  let vVectorZero be :vector(0, 0, 0)
  let vPoint be :point(1, 2, 3)
  let vPointZero be :point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice as :dice be [6, 2, 4]
  emit Done(nothing: (vBooleanTrue xor vNothing), booleanTrue: (vBooleanTrue xor vBooleanTrue), booleanFalse: (vBooleanTrue xor vBooleanFalse), integerOne: (vBooleanTrue xor vIntegerOne), integerZero: (vBooleanTrue xor vIntegerZero), floatPositive: (vBooleanTrue xor vFloatPositive), percentagePositive: (vBooleanTrue xor vPercentagePositive), percentageZero: (vBooleanTrue xor vPercentageZero), textTrue: (vBooleanTrue xor vTextTrue), textTrueUpper: (vBooleanTrue xor vTextTrueUpper), textFalse: (vBooleanTrue xor vTextFalse), textOne: (vBooleanTrue xor vTextOne), textZero: (vBooleanTrue xor vTextZero), textInvalid: (vBooleanTrue xor vTextInvalid), textEmpty: (vBooleanTrue xor vTextEmpty), tagTrue: (vBooleanTrue xor vTagTrue), tagFalse: (vBooleanTrue xor vTagFalse), tagTrueUpper: (vBooleanTrue xor vTagTrueUpper), tagPi: (vBooleanTrue xor vTagPi), tagPhi: (vBooleanTrue xor vTagPhi), tagInfinity: (vBooleanTrue xor vTagInfinity), tagNan: (vBooleanTrue xor vTagNan), tagCustom: (vBooleanTrue xor vTagCustom), vector: (vBooleanTrue xor vVector), vectorZero: (vBooleanTrue xor vVectorZero), point: (vBooleanTrue xor vPoint), pointZero: (vBooleanTrue xor vPointZero), list: (vBooleanTrue xor vList), map: (vBooleanTrue xor vMap), dice: (vBooleanTrue xor vDice))
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
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":boolean"
              value: true
          - name: "floatPositive"
            value:
              type: ":boolean"
              value: false
          - name: "percentagePositive"
            value:
              type: ":boolean"
              value: false
          - name: "percentageZero"
            value:
              type: ":boolean"
              value: true
          - name: "textTrue"
            value:
              type: ":boolean"
              value: false
          - name: "textTrueUpper"
            value:
              type: ":boolean"
              value: false
          - name: "textFalse"
            value:
              type: ":boolean"
              value: true
          - name: "textOne"
            value:
              type: ":boolean"
              value: false
          - name: "textZero"
            value:
              type: ":boolean"
              value: true
          - name: "textInvalid"
            value:
              type: ":boolean"
              value: true
          - name: "textEmpty"
            value:
              type: ":boolean"
              value: true
          - name: "tagTrue"
            value:
              type: ":boolean"
              value: true
          - name: "tagFalse"
            value:
              type: ":boolean"
              value: true
          - name: "tagTrueUpper"
            value:
              type: ":boolean"
              value: true
          - name: "tagPi"
            value:
              type: ":boolean"
              value: false
          - name: "tagPhi"
            value:
              type: ":boolean"
              value: false
          - name: "tagInfinity"
            value:
              type: ":boolean"
              value: false
          - name: "tagNan"
            value:
              type: ":boolean"
              value: true
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":nothing"
          - name: "map"
            value:
              type: ":nothing"
          - name: "dice"
            value:
              type: ":nothing"
```

---

## Test: xor booleanFalse

This runtime case exercises “xor booleanFalse” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0064
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor booleanFalse.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBL
on Start {
  let vNothing be nothing
  let vBooleanTrue be true
  let vBooleanFalse be false
  let vIntegerOne be 1
  let vIntegerZero be 0
  let vFloatPositive be 0.5
  let vPercentagePositive be 10%
  let vPercentageZero be 0%
  let vTextTrue be 'true'
  let vTextTrueUpper be 'TRUE'
  let vTextFalse be 'false'
  let vTextOne be '1'
  let vTextZero be '0'
  let vTextInvalid be 'hello'
  let vTextEmpty be ''
  let vTagTrue be #true
  let vTagFalse be #false
  let vTagTrueUpper as :tag be 'True'
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :vector(1, 2, 3)
  let vVectorZero be :vector(0, 0, 0)
  let vPoint be :point(1, 2, 3)
  let vPointZero be :point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice as :dice be [6, 2, 4]
  emit Done(nothing: (vBooleanFalse xor vNothing), booleanTrue: (vBooleanFalse xor vBooleanTrue), booleanFalse: (vBooleanFalse xor vBooleanFalse), integerOne: (vBooleanFalse xor vIntegerOne), integerZero: (vBooleanFalse xor vIntegerZero), floatPositive: (vBooleanFalse xor vFloatPositive), percentagePositive: (vBooleanFalse xor vPercentagePositive), percentageZero: (vBooleanFalse xor vPercentageZero), textTrue: (vBooleanFalse xor vTextTrue), textTrueUpper: (vBooleanFalse xor vTextTrueUpper), textFalse: (vBooleanFalse xor vTextFalse), textOne: (vBooleanFalse xor vTextOne), textZero: (vBooleanFalse xor vTextZero), textInvalid: (vBooleanFalse xor vTextInvalid), textEmpty: (vBooleanFalse xor vTextEmpty), tagTrue: (vBooleanFalse xor vTagTrue), tagFalse: (vBooleanFalse xor vTagFalse), tagTrueUpper: (vBooleanFalse xor vTagTrueUpper), tagPi: (vBooleanFalse xor vTagPi), tagPhi: (vBooleanFalse xor vTagPhi), tagInfinity: (vBooleanFalse xor vTagInfinity), tagNan: (vBooleanFalse xor vTagNan), tagCustom: (vBooleanFalse xor vTagCustom), vector: (vBooleanFalse xor vVector), vectorZero: (vBooleanFalse xor vVectorZero), point: (vBooleanFalse xor vPoint), pointZero: (vBooleanFalse xor vPointZero), list: (vBooleanFalse xor vList), map: (vBooleanFalse xor vMap), dice: (vBooleanFalse xor vDice))
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
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":boolean"
              value: true
          - name: "percentagePositive"
            value:
              type: ":boolean"
              value: true
          - name: "percentageZero"
            value:
              type: ":boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":boolean"
              value: true
          - name: "textTrueUpper"
            value:
              type: ":boolean"
              value: true
          - name: "textFalse"
            value:
              type: ":boolean"
              value: false
          - name: "textOne"
            value:
              type: ":boolean"
              value: true
          - name: "textZero"
            value:
              type: ":boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":boolean"
              value: true
          - name: "tagPhi"
            value:
              type: ":boolean"
              value: true
          - name: "tagInfinity"
            value:
              type: ":boolean"
              value: true
          - name: "tagNan"
            value:
              type: ":boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":nothing"
          - name: "map"
            value:
              type: ":nothing"
          - name: "dice"
            value:
              type: ":nothing"
```

---

## Test: xor integerOne

This runtime case exercises “xor integerOne” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0065
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor integerOne.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBM
on Start {
  let vNothing be nothing
  let vBooleanTrue be true
  let vBooleanFalse be false
  let vIntegerOne be 1
  let vIntegerZero be 0
  let vFloatPositive be 0.5
  let vPercentagePositive be 10%
  let vPercentageZero be 0%
  let vTextTrue be 'true'
  let vTextTrueUpper be 'TRUE'
  let vTextFalse be 'false'
  let vTextOne be '1'
  let vTextZero be '0'
  let vTextInvalid be 'hello'
  let vTextEmpty be ''
  let vTagTrue be #true
  let vTagFalse be #false
  let vTagTrueUpper as :tag be 'True'
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :vector(1, 2, 3)
  let vVectorZero be :vector(0, 0, 0)
  let vPoint be :point(1, 2, 3)
  let vPointZero be :point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice as :dice be [6, 2, 4]
  emit Done(nothing: (vIntegerOne xor vNothing), booleanTrue: (vIntegerOne xor vBooleanTrue), booleanFalse: (vIntegerOne xor vBooleanFalse), integerOne: (vIntegerOne xor vIntegerOne), integerZero: (vIntegerOne xor vIntegerZero), floatPositive: (vIntegerOne xor vFloatPositive), percentagePositive: (vIntegerOne xor vPercentagePositive), percentageZero: (vIntegerOne xor vPercentageZero), textTrue: (vIntegerOne xor vTextTrue), textTrueUpper: (vIntegerOne xor vTextTrueUpper), textFalse: (vIntegerOne xor vTextFalse), textOne: (vIntegerOne xor vTextOne), textZero: (vIntegerOne xor vTextZero), textInvalid: (vIntegerOne xor vTextInvalid), textEmpty: (vIntegerOne xor vTextEmpty), tagTrue: (vIntegerOne xor vTagTrue), tagFalse: (vIntegerOne xor vTagFalse), tagTrueUpper: (vIntegerOne xor vTagTrueUpper), tagPi: (vIntegerOne xor vTagPi), tagPhi: (vIntegerOne xor vTagPhi), tagInfinity: (vIntegerOne xor vTagInfinity), tagNan: (vIntegerOne xor vTagNan), tagCustom: (vIntegerOne xor vTagCustom), vector: (vIntegerOne xor vVector), vectorZero: (vIntegerOne xor vVectorZero), point: (vIntegerOne xor vPoint), pointZero: (vIntegerOne xor vPointZero), list: (vIntegerOne xor vList), map: (vIntegerOne xor vMap), dice: (vIntegerOne xor vDice))
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
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":boolean"
              value: true
          - name: "floatPositive"
            value:
              type: ":boolean"
              value: false
          - name: "percentagePositive"
            value:
              type: ":boolean"
              value: false
          - name: "percentageZero"
            value:
              type: ":boolean"
              value: true
          - name: "textTrue"
            value:
              type: ":boolean"
              value: false
          - name: "textTrueUpper"
            value:
              type: ":boolean"
              value: false
          - name: "textFalse"
            value:
              type: ":boolean"
              value: true
          - name: "textOne"
            value:
              type: ":boolean"
              value: false
          - name: "textZero"
            value:
              type: ":boolean"
              value: true
          - name: "textInvalid"
            value:
              type: ":boolean"
              value: true
          - name: "textEmpty"
            value:
              type: ":boolean"
              value: true
          - name: "tagTrue"
            value:
              type: ":boolean"
              value: true
          - name: "tagFalse"
            value:
              type: ":boolean"
              value: true
          - name: "tagTrueUpper"
            value:
              type: ":boolean"
              value: true
          - name: "tagPi"
            value:
              type: ":boolean"
              value: false
          - name: "tagPhi"
            value:
              type: ":boolean"
              value: false
          - name: "tagInfinity"
            value:
              type: ":boolean"
              value: false
          - name: "tagNan"
            value:
              type: ":boolean"
              value: true
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":nothing"
          - name: "map"
            value:
              type: ":nothing"
          - name: "dice"
            value:
              type: ":nothing"
```

---

## Test: xor integerZero

This runtime case exercises “xor integerZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0066
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor integerZero.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBN
on Start {
  let vNothing be nothing
  let vBooleanTrue be true
  let vBooleanFalse be false
  let vIntegerOne be 1
  let vIntegerZero be 0
  let vFloatPositive be 0.5
  let vPercentagePositive be 10%
  let vPercentageZero be 0%
  let vTextTrue be 'true'
  let vTextTrueUpper be 'TRUE'
  let vTextFalse be 'false'
  let vTextOne be '1'
  let vTextZero be '0'
  let vTextInvalid be 'hello'
  let vTextEmpty be ''
  let vTagTrue be #true
  let vTagFalse be #false
  let vTagTrueUpper as :tag be 'True'
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :vector(1, 2, 3)
  let vVectorZero be :vector(0, 0, 0)
  let vPoint be :point(1, 2, 3)
  let vPointZero be :point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice as :dice be [6, 2, 4]
  emit Done(nothing: (vIntegerZero xor vNothing), booleanTrue: (vIntegerZero xor vBooleanTrue), booleanFalse: (vIntegerZero xor vBooleanFalse), integerOne: (vIntegerZero xor vIntegerOne), integerZero: (vIntegerZero xor vIntegerZero), floatPositive: (vIntegerZero xor vFloatPositive), percentagePositive: (vIntegerZero xor vPercentagePositive), percentageZero: (vIntegerZero xor vPercentageZero), textTrue: (vIntegerZero xor vTextTrue), textTrueUpper: (vIntegerZero xor vTextTrueUpper), textFalse: (vIntegerZero xor vTextFalse), textOne: (vIntegerZero xor vTextOne), textZero: (vIntegerZero xor vTextZero), textInvalid: (vIntegerZero xor vTextInvalid), textEmpty: (vIntegerZero xor vTextEmpty), tagTrue: (vIntegerZero xor vTagTrue), tagFalse: (vIntegerZero xor vTagFalse), tagTrueUpper: (vIntegerZero xor vTagTrueUpper), tagPi: (vIntegerZero xor vTagPi), tagPhi: (vIntegerZero xor vTagPhi), tagInfinity: (vIntegerZero xor vTagInfinity), tagNan: (vIntegerZero xor vTagNan), tagCustom: (vIntegerZero xor vTagCustom), vector: (vIntegerZero xor vVector), vectorZero: (vIntegerZero xor vVectorZero), point: (vIntegerZero xor vPoint), pointZero: (vIntegerZero xor vPointZero), list: (vIntegerZero xor vList), map: (vIntegerZero xor vMap), dice: (vIntegerZero xor vDice))
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
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":boolean"
              value: true
          - name: "percentagePositive"
            value:
              type: ":boolean"
              value: true
          - name: "percentageZero"
            value:
              type: ":boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":boolean"
              value: true
          - name: "textTrueUpper"
            value:
              type: ":boolean"
              value: true
          - name: "textFalse"
            value:
              type: ":boolean"
              value: false
          - name: "textOne"
            value:
              type: ":boolean"
              value: true
          - name: "textZero"
            value:
              type: ":boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":boolean"
              value: true
          - name: "tagPhi"
            value:
              type: ":boolean"
              value: true
          - name: "tagInfinity"
            value:
              type: ":boolean"
              value: true
          - name: "tagNan"
            value:
              type: ":boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":nothing"
          - name: "map"
            value:
              type: ":nothing"
          - name: "dice"
            value:
              type: ":nothing"
```

---

## Test: xor floatPositive

This runtime case exercises “xor floatPositive” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0067
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor floatPositive.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBO
on Start {
  let vNothing be nothing
  let vBooleanTrue be true
  let vBooleanFalse be false
  let vIntegerOne be 1
  let vIntegerZero be 0
  let vFloatPositive be 0.5
  let vPercentagePositive be 10%
  let vPercentageZero be 0%
  let vTextTrue be 'true'
  let vTextTrueUpper be 'TRUE'
  let vTextFalse be 'false'
  let vTextOne be '1'
  let vTextZero be '0'
  let vTextInvalid be 'hello'
  let vTextEmpty be ''
  let vTagTrue be #true
  let vTagFalse be #false
  let vTagTrueUpper as :tag be 'True'
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :vector(1, 2, 3)
  let vVectorZero be :vector(0, 0, 0)
  let vPoint be :point(1, 2, 3)
  let vPointZero be :point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice as :dice be [6, 2, 4]
  emit Done(nothing: (vFloatPositive xor vNothing), booleanTrue: (vFloatPositive xor vBooleanTrue), booleanFalse: (vFloatPositive xor vBooleanFalse), integerOne: (vFloatPositive xor vIntegerOne), integerZero: (vFloatPositive xor vIntegerZero), floatPositive: (vFloatPositive xor vFloatPositive), percentagePositive: (vFloatPositive xor vPercentagePositive), percentageZero: (vFloatPositive xor vPercentageZero), textTrue: (vFloatPositive xor vTextTrue), textTrueUpper: (vFloatPositive xor vTextTrueUpper), textFalse: (vFloatPositive xor vTextFalse), textOne: (vFloatPositive xor vTextOne), textZero: (vFloatPositive xor vTextZero), textInvalid: (vFloatPositive xor vTextInvalid), textEmpty: (vFloatPositive xor vTextEmpty), tagTrue: (vFloatPositive xor vTagTrue), tagFalse: (vFloatPositive xor vTagFalse), tagTrueUpper: (vFloatPositive xor vTagTrueUpper), tagPi: (vFloatPositive xor vTagPi), tagPhi: (vFloatPositive xor vTagPhi), tagInfinity: (vFloatPositive xor vTagInfinity), tagNan: (vFloatPositive xor vTagNan), tagCustom: (vFloatPositive xor vTagCustom), vector: (vFloatPositive xor vVector), vectorZero: (vFloatPositive xor vVectorZero), point: (vFloatPositive xor vPoint), pointZero: (vFloatPositive xor vPointZero), list: (vFloatPositive xor vList), map: (vFloatPositive xor vMap), dice: (vFloatPositive xor vDice))
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
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":boolean"
              value: true
          - name: "floatPositive"
            value:
              type: ":boolean"
              value: false
          - name: "percentagePositive"
            value:
              type: ":boolean"
              value: false
          - name: "percentageZero"
            value:
              type: ":boolean"
              value: true
          - name: "textTrue"
            value:
              type: ":boolean"
              value: false
          - name: "textTrueUpper"
            value:
              type: ":boolean"
              value: false
          - name: "textFalse"
            value:
              type: ":boolean"
              value: true
          - name: "textOne"
            value:
              type: ":boolean"
              value: false
          - name: "textZero"
            value:
              type: ":boolean"
              value: true
          - name: "textInvalid"
            value:
              type: ":boolean"
              value: true
          - name: "textEmpty"
            value:
              type: ":boolean"
              value: true
          - name: "tagTrue"
            value:
              type: ":boolean"
              value: true
          - name: "tagFalse"
            value:
              type: ":boolean"
              value: true
          - name: "tagTrueUpper"
            value:
              type: ":boolean"
              value: true
          - name: "tagPi"
            value:
              type: ":boolean"
              value: false
          - name: "tagPhi"
            value:
              type: ":boolean"
              value: false
          - name: "tagInfinity"
            value:
              type: ":boolean"
              value: false
          - name: "tagNan"
            value:
              type: ":boolean"
              value: true
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":nothing"
          - name: "map"
            value:
              type: ":nothing"
          - name: "dice"
            value:
              type: ":nothing"
```

---

## Test: xor percentagePositive

This runtime case exercises “xor percentagePositive” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0068
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor percentagePositive.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBP
on Start {
  let vNothing be nothing
  let vBooleanTrue be true
  let vBooleanFalse be false
  let vIntegerOne be 1
  let vIntegerZero be 0
  let vFloatPositive be 0.5
  let vPercentagePositive be 10%
  let vPercentageZero be 0%
  let vTextTrue be 'true'
  let vTextTrueUpper be 'TRUE'
  let vTextFalse be 'false'
  let vTextOne be '1'
  let vTextZero be '0'
  let vTextInvalid be 'hello'
  let vTextEmpty be ''
  let vTagTrue be #true
  let vTagFalse be #false
  let vTagTrueUpper as :tag be 'True'
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :vector(1, 2, 3)
  let vVectorZero be :vector(0, 0, 0)
  let vPoint be :point(1, 2, 3)
  let vPointZero be :point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice as :dice be [6, 2, 4]
  emit Done(nothing: (vPercentagePositive xor vNothing), booleanTrue: (vPercentagePositive xor vBooleanTrue), booleanFalse: (vPercentagePositive xor vBooleanFalse), integerOne: (vPercentagePositive xor vIntegerOne), integerZero: (vPercentagePositive xor vIntegerZero), floatPositive: (vPercentagePositive xor vFloatPositive), percentagePositive: (vPercentagePositive xor vPercentagePositive), percentageZero: (vPercentagePositive xor vPercentageZero), textTrue: (vPercentagePositive xor vTextTrue), textTrueUpper: (vPercentagePositive xor vTextTrueUpper), textFalse: (vPercentagePositive xor vTextFalse), textOne: (vPercentagePositive xor vTextOne), textZero: (vPercentagePositive xor vTextZero), textInvalid: (vPercentagePositive xor vTextInvalid), textEmpty: (vPercentagePositive xor vTextEmpty), tagTrue: (vPercentagePositive xor vTagTrue), tagFalse: (vPercentagePositive xor vTagFalse), tagTrueUpper: (vPercentagePositive xor vTagTrueUpper), tagPi: (vPercentagePositive xor vTagPi), tagPhi: (vPercentagePositive xor vTagPhi), tagInfinity: (vPercentagePositive xor vTagInfinity), tagNan: (vPercentagePositive xor vTagNan), tagCustom: (vPercentagePositive xor vTagCustom), vector: (vPercentagePositive xor vVector), vectorZero: (vPercentagePositive xor vVectorZero), point: (vPercentagePositive xor vPoint), pointZero: (vPercentagePositive xor vPointZero), list: (vPercentagePositive xor vList), map: (vPercentagePositive xor vMap), dice: (vPercentagePositive xor vDice))
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
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":boolean"
              value: true
          - name: "floatPositive"
            value:
              type: ":boolean"
              value: false
          - name: "percentagePositive"
            value:
              type: ":boolean"
              value: false
          - name: "percentageZero"
            value:
              type: ":boolean"
              value: true
          - name: "textTrue"
            value:
              type: ":boolean"
              value: false
          - name: "textTrueUpper"
            value:
              type: ":boolean"
              value: false
          - name: "textFalse"
            value:
              type: ":boolean"
              value: true
          - name: "textOne"
            value:
              type: ":boolean"
              value: false
          - name: "textZero"
            value:
              type: ":boolean"
              value: true
          - name: "textInvalid"
            value:
              type: ":boolean"
              value: true
          - name: "textEmpty"
            value:
              type: ":boolean"
              value: true
          - name: "tagTrue"
            value:
              type: ":boolean"
              value: true
          - name: "tagFalse"
            value:
              type: ":boolean"
              value: true
          - name: "tagTrueUpper"
            value:
              type: ":boolean"
              value: true
          - name: "tagPi"
            value:
              type: ":boolean"
              value: false
          - name: "tagPhi"
            value:
              type: ":boolean"
              value: false
          - name: "tagInfinity"
            value:
              type: ":boolean"
              value: false
          - name: "tagNan"
            value:
              type: ":boolean"
              value: true
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":nothing"
          - name: "map"
            value:
              type: ":nothing"
          - name: "dice"
            value:
              type: ":nothing"
```

---

## Test: xor percentageZero

This runtime case exercises “xor percentageZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0069
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor percentageZero.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBQ
on Start {
  let vNothing be nothing
  let vBooleanTrue be true
  let vBooleanFalse be false
  let vIntegerOne be 1
  let vIntegerZero be 0
  let vFloatPositive be 0.5
  let vPercentagePositive be 10%
  let vPercentageZero be 0%
  let vTextTrue be 'true'
  let vTextTrueUpper be 'TRUE'
  let vTextFalse be 'false'
  let vTextOne be '1'
  let vTextZero be '0'
  let vTextInvalid be 'hello'
  let vTextEmpty be ''
  let vTagTrue be #true
  let vTagFalse be #false
  let vTagTrueUpper as :tag be 'True'
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :vector(1, 2, 3)
  let vVectorZero be :vector(0, 0, 0)
  let vPoint be :point(1, 2, 3)
  let vPointZero be :point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice as :dice be [6, 2, 4]
  emit Done(nothing: (vPercentageZero xor vNothing), booleanTrue: (vPercentageZero xor vBooleanTrue), booleanFalse: (vPercentageZero xor vBooleanFalse), integerOne: (vPercentageZero xor vIntegerOne), integerZero: (vPercentageZero xor vIntegerZero), floatPositive: (vPercentageZero xor vFloatPositive), percentagePositive: (vPercentageZero xor vPercentagePositive), percentageZero: (vPercentageZero xor vPercentageZero), textTrue: (vPercentageZero xor vTextTrue), textTrueUpper: (vPercentageZero xor vTextTrueUpper), textFalse: (vPercentageZero xor vTextFalse), textOne: (vPercentageZero xor vTextOne), textZero: (vPercentageZero xor vTextZero), textInvalid: (vPercentageZero xor vTextInvalid), textEmpty: (vPercentageZero xor vTextEmpty), tagTrue: (vPercentageZero xor vTagTrue), tagFalse: (vPercentageZero xor vTagFalse), tagTrueUpper: (vPercentageZero xor vTagTrueUpper), tagPi: (vPercentageZero xor vTagPi), tagPhi: (vPercentageZero xor vTagPhi), tagInfinity: (vPercentageZero xor vTagInfinity), tagNan: (vPercentageZero xor vTagNan), tagCustom: (vPercentageZero xor vTagCustom), vector: (vPercentageZero xor vVector), vectorZero: (vPercentageZero xor vVectorZero), point: (vPercentageZero xor vPoint), pointZero: (vPercentageZero xor vPointZero), list: (vPercentageZero xor vList), map: (vPercentageZero xor vMap), dice: (vPercentageZero xor vDice))
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
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":boolean"
              value: true
          - name: "percentagePositive"
            value:
              type: ":boolean"
              value: true
          - name: "percentageZero"
            value:
              type: ":boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":boolean"
              value: true
          - name: "textTrueUpper"
            value:
              type: ":boolean"
              value: true
          - name: "textFalse"
            value:
              type: ":boolean"
              value: false
          - name: "textOne"
            value:
              type: ":boolean"
              value: true
          - name: "textZero"
            value:
              type: ":boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":boolean"
              value: true
          - name: "tagPhi"
            value:
              type: ":boolean"
              value: true
          - name: "tagInfinity"
            value:
              type: ":boolean"
              value: true
          - name: "tagNan"
            value:
              type: ":boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":nothing"
          - name: "map"
            value:
              type: ":nothing"
          - name: "dice"
            value:
              type: ":nothing"
```

---

## Test: xor textTrue

This runtime case exercises “xor textTrue” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0070
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor textTrue.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBR
on Start {
  let vNothing be nothing
  let vBooleanTrue be true
  let vBooleanFalse be false
  let vIntegerOne be 1
  let vIntegerZero be 0
  let vFloatPositive be 0.5
  let vPercentagePositive be 10%
  let vPercentageZero be 0%
  let vTextTrue be 'true'
  let vTextTrueUpper be 'TRUE'
  let vTextFalse be 'false'
  let vTextOne be '1'
  let vTextZero be '0'
  let vTextInvalid be 'hello'
  let vTextEmpty be ''
  let vTagTrue be #true
  let vTagFalse be #false
  let vTagTrueUpper as :tag be 'True'
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :vector(1, 2, 3)
  let vVectorZero be :vector(0, 0, 0)
  let vPoint be :point(1, 2, 3)
  let vPointZero be :point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice as :dice be [6, 2, 4]
  emit Done(nothing: (vTextTrue xor vNothing), booleanTrue: (vTextTrue xor vBooleanTrue), booleanFalse: (vTextTrue xor vBooleanFalse), integerOne: (vTextTrue xor vIntegerOne), integerZero: (vTextTrue xor vIntegerZero), floatPositive: (vTextTrue xor vFloatPositive), percentagePositive: (vTextTrue xor vPercentagePositive), percentageZero: (vTextTrue xor vPercentageZero), textTrue: (vTextTrue xor vTextTrue), textTrueUpper: (vTextTrue xor vTextTrueUpper), textFalse: (vTextTrue xor vTextFalse), textOne: (vTextTrue xor vTextOne), textZero: (vTextTrue xor vTextZero), textInvalid: (vTextTrue xor vTextInvalid), textEmpty: (vTextTrue xor vTextEmpty), tagTrue: (vTextTrue xor vTagTrue), tagFalse: (vTextTrue xor vTagFalse), tagTrueUpper: (vTextTrue xor vTagTrueUpper), tagPi: (vTextTrue xor vTagPi), tagPhi: (vTextTrue xor vTagPhi), tagInfinity: (vTextTrue xor vTagInfinity), tagNan: (vTextTrue xor vTagNan), tagCustom: (vTextTrue xor vTagCustom), vector: (vTextTrue xor vVector), vectorZero: (vTextTrue xor vVectorZero), point: (vTextTrue xor vPoint), pointZero: (vTextTrue xor vPointZero), list: (vTextTrue xor vList), map: (vTextTrue xor vMap), dice: (vTextTrue xor vDice))
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
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":boolean"
              value: true
          - name: "floatPositive"
            value:
              type: ":boolean"
              value: false
          - name: "percentagePositive"
            value:
              type: ":boolean"
              value: false
          - name: "percentageZero"
            value:
              type: ":boolean"
              value: true
          - name: "textTrue"
            value:
              type: ":boolean"
              value: false
          - name: "textTrueUpper"
            value:
              type: ":boolean"
              value: false
          - name: "textFalse"
            value:
              type: ":boolean"
              value: true
          - name: "textOne"
            value:
              type: ":boolean"
              value: false
          - name: "textZero"
            value:
              type: ":boolean"
              value: true
          - name: "textInvalid"
            value:
              type: ":boolean"
              value: true
          - name: "textEmpty"
            value:
              type: ":boolean"
              value: true
          - name: "tagTrue"
            value:
              type: ":boolean"
              value: true
          - name: "tagFalse"
            value:
              type: ":boolean"
              value: true
          - name: "tagTrueUpper"
            value:
              type: ":boolean"
              value: true
          - name: "tagPi"
            value:
              type: ":boolean"
              value: false
          - name: "tagPhi"
            value:
              type: ":boolean"
              value: false
          - name: "tagInfinity"
            value:
              type: ":boolean"
              value: false
          - name: "tagNan"
            value:
              type: ":boolean"
              value: true
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":nothing"
          - name: "map"
            value:
              type: ":nothing"
          - name: "dice"
            value:
              type: ":nothing"
```

---

## Test: xor textTrueUpper

This runtime case exercises “xor textTrueUpper” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0071
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor textTrueUpper.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBS
on Start {
  let vNothing be nothing
  let vBooleanTrue be true
  let vBooleanFalse be false
  let vIntegerOne be 1
  let vIntegerZero be 0
  let vFloatPositive be 0.5
  let vPercentagePositive be 10%
  let vPercentageZero be 0%
  let vTextTrue be 'true'
  let vTextTrueUpper be 'TRUE'
  let vTextFalse be 'false'
  let vTextOne be '1'
  let vTextZero be '0'
  let vTextInvalid be 'hello'
  let vTextEmpty be ''
  let vTagTrue be #true
  let vTagFalse be #false
  let vTagTrueUpper as :tag be 'True'
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :vector(1, 2, 3)
  let vVectorZero be :vector(0, 0, 0)
  let vPoint be :point(1, 2, 3)
  let vPointZero be :point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice as :dice be [6, 2, 4]
  emit Done(nothing: (vTextTrueUpper xor vNothing), booleanTrue: (vTextTrueUpper xor vBooleanTrue), booleanFalse: (vTextTrueUpper xor vBooleanFalse), integerOne: (vTextTrueUpper xor vIntegerOne), integerZero: (vTextTrueUpper xor vIntegerZero), floatPositive: (vTextTrueUpper xor vFloatPositive), percentagePositive: (vTextTrueUpper xor vPercentagePositive), percentageZero: (vTextTrueUpper xor vPercentageZero), textTrue: (vTextTrueUpper xor vTextTrue), textTrueUpper: (vTextTrueUpper xor vTextTrueUpper), textFalse: (vTextTrueUpper xor vTextFalse), textOne: (vTextTrueUpper xor vTextOne), textZero: (vTextTrueUpper xor vTextZero), textInvalid: (vTextTrueUpper xor vTextInvalid), textEmpty: (vTextTrueUpper xor vTextEmpty), tagTrue: (vTextTrueUpper xor vTagTrue), tagFalse: (vTextTrueUpper xor vTagFalse), tagTrueUpper: (vTextTrueUpper xor vTagTrueUpper), tagPi: (vTextTrueUpper xor vTagPi), tagPhi: (vTextTrueUpper xor vTagPhi), tagInfinity: (vTextTrueUpper xor vTagInfinity), tagNan: (vTextTrueUpper xor vTagNan), tagCustom: (vTextTrueUpper xor vTagCustom), vector: (vTextTrueUpper xor vVector), vectorZero: (vTextTrueUpper xor vVectorZero), point: (vTextTrueUpper xor vPoint), pointZero: (vTextTrueUpper xor vPointZero), list: (vTextTrueUpper xor vList), map: (vTextTrueUpper xor vMap), dice: (vTextTrueUpper xor vDice))
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
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":boolean"
              value: true
          - name: "floatPositive"
            value:
              type: ":boolean"
              value: false
          - name: "percentagePositive"
            value:
              type: ":boolean"
              value: false
          - name: "percentageZero"
            value:
              type: ":boolean"
              value: true
          - name: "textTrue"
            value:
              type: ":boolean"
              value: false
          - name: "textTrueUpper"
            value:
              type: ":boolean"
              value: false
          - name: "textFalse"
            value:
              type: ":boolean"
              value: true
          - name: "textOne"
            value:
              type: ":boolean"
              value: false
          - name: "textZero"
            value:
              type: ":boolean"
              value: true
          - name: "textInvalid"
            value:
              type: ":boolean"
              value: true
          - name: "textEmpty"
            value:
              type: ":boolean"
              value: true
          - name: "tagTrue"
            value:
              type: ":boolean"
              value: true
          - name: "tagFalse"
            value:
              type: ":boolean"
              value: true
          - name: "tagTrueUpper"
            value:
              type: ":boolean"
              value: true
          - name: "tagPi"
            value:
              type: ":boolean"
              value: false
          - name: "tagPhi"
            value:
              type: ":boolean"
              value: false
          - name: "tagInfinity"
            value:
              type: ":boolean"
              value: false
          - name: "tagNan"
            value:
              type: ":boolean"
              value: true
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":nothing"
          - name: "map"
            value:
              type: ":nothing"
          - name: "dice"
            value:
              type: ":nothing"
```

---

## Test: xor textFalse

This runtime case exercises “xor textFalse” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0072
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor textFalse.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBT
on Start {
  let vNothing be nothing
  let vBooleanTrue be true
  let vBooleanFalse be false
  let vIntegerOne be 1
  let vIntegerZero be 0
  let vFloatPositive be 0.5
  let vPercentagePositive be 10%
  let vPercentageZero be 0%
  let vTextTrue be 'true'
  let vTextTrueUpper be 'TRUE'
  let vTextFalse be 'false'
  let vTextOne be '1'
  let vTextZero be '0'
  let vTextInvalid be 'hello'
  let vTextEmpty be ''
  let vTagTrue be #true
  let vTagFalse be #false
  let vTagTrueUpper as :tag be 'True'
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :vector(1, 2, 3)
  let vVectorZero be :vector(0, 0, 0)
  let vPoint be :point(1, 2, 3)
  let vPointZero be :point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice as :dice be [6, 2, 4]
  emit Done(nothing: (vTextFalse xor vNothing), booleanTrue: (vTextFalse xor vBooleanTrue), booleanFalse: (vTextFalse xor vBooleanFalse), integerOne: (vTextFalse xor vIntegerOne), integerZero: (vTextFalse xor vIntegerZero), floatPositive: (vTextFalse xor vFloatPositive), percentagePositive: (vTextFalse xor vPercentagePositive), percentageZero: (vTextFalse xor vPercentageZero), textTrue: (vTextFalse xor vTextTrue), textTrueUpper: (vTextFalse xor vTextTrueUpper), textFalse: (vTextFalse xor vTextFalse), textOne: (vTextFalse xor vTextOne), textZero: (vTextFalse xor vTextZero), textInvalid: (vTextFalse xor vTextInvalid), textEmpty: (vTextFalse xor vTextEmpty), tagTrue: (vTextFalse xor vTagTrue), tagFalse: (vTextFalse xor vTagFalse), tagTrueUpper: (vTextFalse xor vTagTrueUpper), tagPi: (vTextFalse xor vTagPi), tagPhi: (vTextFalse xor vTagPhi), tagInfinity: (vTextFalse xor vTagInfinity), tagNan: (vTextFalse xor vTagNan), tagCustom: (vTextFalse xor vTagCustom), vector: (vTextFalse xor vVector), vectorZero: (vTextFalse xor vVectorZero), point: (vTextFalse xor vPoint), pointZero: (vTextFalse xor vPointZero), list: (vTextFalse xor vList), map: (vTextFalse xor vMap), dice: (vTextFalse xor vDice))
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
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":boolean"
              value: true
          - name: "percentagePositive"
            value:
              type: ":boolean"
              value: true
          - name: "percentageZero"
            value:
              type: ":boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":boolean"
              value: true
          - name: "textTrueUpper"
            value:
              type: ":boolean"
              value: true
          - name: "textFalse"
            value:
              type: ":boolean"
              value: false
          - name: "textOne"
            value:
              type: ":boolean"
              value: true
          - name: "textZero"
            value:
              type: ":boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":boolean"
              value: true
          - name: "tagPhi"
            value:
              type: ":boolean"
              value: true
          - name: "tagInfinity"
            value:
              type: ":boolean"
              value: true
          - name: "tagNan"
            value:
              type: ":boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":nothing"
          - name: "map"
            value:
              type: ":nothing"
          - name: "dice"
            value:
              type: ":nothing"
```

---

## Test: xor textOne

This runtime case exercises “xor textOne” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0073
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor textOne.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBU
on Start {
  let vNothing be nothing
  let vBooleanTrue be true
  let vBooleanFalse be false
  let vIntegerOne be 1
  let vIntegerZero be 0
  let vFloatPositive be 0.5
  let vPercentagePositive be 10%
  let vPercentageZero be 0%
  let vTextTrue be 'true'
  let vTextTrueUpper be 'TRUE'
  let vTextFalse be 'false'
  let vTextOne be '1'
  let vTextZero be '0'
  let vTextInvalid be 'hello'
  let vTextEmpty be ''
  let vTagTrue be #true
  let vTagFalse be #false
  let vTagTrueUpper as :tag be 'True'
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :vector(1, 2, 3)
  let vVectorZero be :vector(0, 0, 0)
  let vPoint be :point(1, 2, 3)
  let vPointZero be :point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice as :dice be [6, 2, 4]
  emit Done(nothing: (vTextOne xor vNothing), booleanTrue: (vTextOne xor vBooleanTrue), booleanFalse: (vTextOne xor vBooleanFalse), integerOne: (vTextOne xor vIntegerOne), integerZero: (vTextOne xor vIntegerZero), floatPositive: (vTextOne xor vFloatPositive), percentagePositive: (vTextOne xor vPercentagePositive), percentageZero: (vTextOne xor vPercentageZero), textTrue: (vTextOne xor vTextTrue), textTrueUpper: (vTextOne xor vTextTrueUpper), textFalse: (vTextOne xor vTextFalse), textOne: (vTextOne xor vTextOne), textZero: (vTextOne xor vTextZero), textInvalid: (vTextOne xor vTextInvalid), textEmpty: (vTextOne xor vTextEmpty), tagTrue: (vTextOne xor vTagTrue), tagFalse: (vTextOne xor vTagFalse), tagTrueUpper: (vTextOne xor vTagTrueUpper), tagPi: (vTextOne xor vTagPi), tagPhi: (vTextOne xor vTagPhi), tagInfinity: (vTextOne xor vTagInfinity), tagNan: (vTextOne xor vTagNan), tagCustom: (vTextOne xor vTagCustom), vector: (vTextOne xor vVector), vectorZero: (vTextOne xor vVectorZero), point: (vTextOne xor vPoint), pointZero: (vTextOne xor vPointZero), list: (vTextOne xor vList), map: (vTextOne xor vMap), dice: (vTextOne xor vDice))
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
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":boolean"
              value: true
          - name: "floatPositive"
            value:
              type: ":boolean"
              value: false
          - name: "percentagePositive"
            value:
              type: ":boolean"
              value: false
          - name: "percentageZero"
            value:
              type: ":boolean"
              value: true
          - name: "textTrue"
            value:
              type: ":boolean"
              value: false
          - name: "textTrueUpper"
            value:
              type: ":boolean"
              value: false
          - name: "textFalse"
            value:
              type: ":boolean"
              value: true
          - name: "textOne"
            value:
              type: ":boolean"
              value: false
          - name: "textZero"
            value:
              type: ":boolean"
              value: true
          - name: "textInvalid"
            value:
              type: ":boolean"
              value: true
          - name: "textEmpty"
            value:
              type: ":boolean"
              value: true
          - name: "tagTrue"
            value:
              type: ":boolean"
              value: true
          - name: "tagFalse"
            value:
              type: ":boolean"
              value: true
          - name: "tagTrueUpper"
            value:
              type: ":boolean"
              value: true
          - name: "tagPi"
            value:
              type: ":boolean"
              value: false
          - name: "tagPhi"
            value:
              type: ":boolean"
              value: false
          - name: "tagInfinity"
            value:
              type: ":boolean"
              value: false
          - name: "tagNan"
            value:
              type: ":boolean"
              value: true
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":nothing"
          - name: "map"
            value:
              type: ":nothing"
          - name: "dice"
            value:
              type: ":nothing"
```

---

## Test: xor textZero

This runtime case exercises “xor textZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0074
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor textZero.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBV
on Start {
  let vNothing be nothing
  let vBooleanTrue be true
  let vBooleanFalse be false
  let vIntegerOne be 1
  let vIntegerZero be 0
  let vFloatPositive be 0.5
  let vPercentagePositive be 10%
  let vPercentageZero be 0%
  let vTextTrue be 'true'
  let vTextTrueUpper be 'TRUE'
  let vTextFalse be 'false'
  let vTextOne be '1'
  let vTextZero be '0'
  let vTextInvalid be 'hello'
  let vTextEmpty be ''
  let vTagTrue be #true
  let vTagFalse be #false
  let vTagTrueUpper as :tag be 'True'
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :vector(1, 2, 3)
  let vVectorZero be :vector(0, 0, 0)
  let vPoint be :point(1, 2, 3)
  let vPointZero be :point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice as :dice be [6, 2, 4]
  emit Done(nothing: (vTextZero xor vNothing), booleanTrue: (vTextZero xor vBooleanTrue), booleanFalse: (vTextZero xor vBooleanFalse), integerOne: (vTextZero xor vIntegerOne), integerZero: (vTextZero xor vIntegerZero), floatPositive: (vTextZero xor vFloatPositive), percentagePositive: (vTextZero xor vPercentagePositive), percentageZero: (vTextZero xor vPercentageZero), textTrue: (vTextZero xor vTextTrue), textTrueUpper: (vTextZero xor vTextTrueUpper), textFalse: (vTextZero xor vTextFalse), textOne: (vTextZero xor vTextOne), textZero: (vTextZero xor vTextZero), textInvalid: (vTextZero xor vTextInvalid), textEmpty: (vTextZero xor vTextEmpty), tagTrue: (vTextZero xor vTagTrue), tagFalse: (vTextZero xor vTagFalse), tagTrueUpper: (vTextZero xor vTagTrueUpper), tagPi: (vTextZero xor vTagPi), tagPhi: (vTextZero xor vTagPhi), tagInfinity: (vTextZero xor vTagInfinity), tagNan: (vTextZero xor vTagNan), tagCustom: (vTextZero xor vTagCustom), vector: (vTextZero xor vVector), vectorZero: (vTextZero xor vVectorZero), point: (vTextZero xor vPoint), pointZero: (vTextZero xor vPointZero), list: (vTextZero xor vList), map: (vTextZero xor vMap), dice: (vTextZero xor vDice))
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
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":boolean"
              value: true
          - name: "percentagePositive"
            value:
              type: ":boolean"
              value: true
          - name: "percentageZero"
            value:
              type: ":boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":boolean"
              value: true
          - name: "textTrueUpper"
            value:
              type: ":boolean"
              value: true
          - name: "textFalse"
            value:
              type: ":boolean"
              value: false
          - name: "textOne"
            value:
              type: ":boolean"
              value: true
          - name: "textZero"
            value:
              type: ":boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":boolean"
              value: true
          - name: "tagPhi"
            value:
              type: ":boolean"
              value: true
          - name: "tagInfinity"
            value:
              type: ":boolean"
              value: true
          - name: "tagNan"
            value:
              type: ":boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":nothing"
          - name: "map"
            value:
              type: ":nothing"
          - name: "dice"
            value:
              type: ":nothing"
```

---

## Test: xor textInvalid

This runtime case exercises “xor textInvalid” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0075
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor textInvalid.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBW
on Start {
  let vNothing be nothing
  let vBooleanTrue be true
  let vBooleanFalse be false
  let vIntegerOne be 1
  let vIntegerZero be 0
  let vFloatPositive be 0.5
  let vPercentagePositive be 10%
  let vPercentageZero be 0%
  let vTextTrue be 'true'
  let vTextTrueUpper be 'TRUE'
  let vTextFalse be 'false'
  let vTextOne be '1'
  let vTextZero be '0'
  let vTextInvalid be 'hello'
  let vTextEmpty be ''
  let vTagTrue be #true
  let vTagFalse be #false
  let vTagTrueUpper as :tag be 'True'
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :vector(1, 2, 3)
  let vVectorZero be :vector(0, 0, 0)
  let vPoint be :point(1, 2, 3)
  let vPointZero be :point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice as :dice be [6, 2, 4]
  emit Done(nothing: (vTextInvalid xor vNothing), booleanTrue: (vTextInvalid xor vBooleanTrue), booleanFalse: (vTextInvalid xor vBooleanFalse), integerOne: (vTextInvalid xor vIntegerOne), integerZero: (vTextInvalid xor vIntegerZero), floatPositive: (vTextInvalid xor vFloatPositive), percentagePositive: (vTextInvalid xor vPercentagePositive), percentageZero: (vTextInvalid xor vPercentageZero), textTrue: (vTextInvalid xor vTextTrue), textTrueUpper: (vTextInvalid xor vTextTrueUpper), textFalse: (vTextInvalid xor vTextFalse), textOne: (vTextInvalid xor vTextOne), textZero: (vTextInvalid xor vTextZero), textInvalid: (vTextInvalid xor vTextInvalid), textEmpty: (vTextInvalid xor vTextEmpty), tagTrue: (vTextInvalid xor vTagTrue), tagFalse: (vTextInvalid xor vTagFalse), tagTrueUpper: (vTextInvalid xor vTagTrueUpper), tagPi: (vTextInvalid xor vTagPi), tagPhi: (vTextInvalid xor vTagPhi), tagInfinity: (vTextInvalid xor vTagInfinity), tagNan: (vTextInvalid xor vTagNan), tagCustom: (vTextInvalid xor vTagCustom), vector: (vTextInvalid xor vVector), vectorZero: (vTextInvalid xor vVectorZero), point: (vTextInvalid xor vPoint), pointZero: (vTextInvalid xor vPointZero), list: (vTextInvalid xor vList), map: (vTextInvalid xor vMap), dice: (vTextInvalid xor vDice))
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
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":boolean"
              value: true
          - name: "percentagePositive"
            value:
              type: ":boolean"
              value: true
          - name: "percentageZero"
            value:
              type: ":boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":boolean"
              value: true
          - name: "textTrueUpper"
            value:
              type: ":boolean"
              value: true
          - name: "textFalse"
            value:
              type: ":boolean"
              value: false
          - name: "textOne"
            value:
              type: ":boolean"
              value: true
          - name: "textZero"
            value:
              type: ":boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":boolean"
              value: true
          - name: "tagPhi"
            value:
              type: ":boolean"
              value: true
          - name: "tagInfinity"
            value:
              type: ":boolean"
              value: true
          - name: "tagNan"
            value:
              type: ":boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":nothing"
          - name: "map"
            value:
              type: ":nothing"
          - name: "dice"
            value:
              type: ":nothing"
```

---

## Test: xor textEmpty

This runtime case exercises “xor textEmpty” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0076
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor textEmpty.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBX
on Start {
  let vNothing be nothing
  let vBooleanTrue be true
  let vBooleanFalse be false
  let vIntegerOne be 1
  let vIntegerZero be 0
  let vFloatPositive be 0.5
  let vPercentagePositive be 10%
  let vPercentageZero be 0%
  let vTextTrue be 'true'
  let vTextTrueUpper be 'TRUE'
  let vTextFalse be 'false'
  let vTextOne be '1'
  let vTextZero be '0'
  let vTextInvalid be 'hello'
  let vTextEmpty be ''
  let vTagTrue be #true
  let vTagFalse be #false
  let vTagTrueUpper as :tag be 'True'
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :vector(1, 2, 3)
  let vVectorZero be :vector(0, 0, 0)
  let vPoint be :point(1, 2, 3)
  let vPointZero be :point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice as :dice be [6, 2, 4]
  emit Done(nothing: (vTextEmpty xor vNothing), booleanTrue: (vTextEmpty xor vBooleanTrue), booleanFalse: (vTextEmpty xor vBooleanFalse), integerOne: (vTextEmpty xor vIntegerOne), integerZero: (vTextEmpty xor vIntegerZero), floatPositive: (vTextEmpty xor vFloatPositive), percentagePositive: (vTextEmpty xor vPercentagePositive), percentageZero: (vTextEmpty xor vPercentageZero), textTrue: (vTextEmpty xor vTextTrue), textTrueUpper: (vTextEmpty xor vTextTrueUpper), textFalse: (vTextEmpty xor vTextFalse), textOne: (vTextEmpty xor vTextOne), textZero: (vTextEmpty xor vTextZero), textInvalid: (vTextEmpty xor vTextInvalid), textEmpty: (vTextEmpty xor vTextEmpty), tagTrue: (vTextEmpty xor vTagTrue), tagFalse: (vTextEmpty xor vTagFalse), tagTrueUpper: (vTextEmpty xor vTagTrueUpper), tagPi: (vTextEmpty xor vTagPi), tagPhi: (vTextEmpty xor vTagPhi), tagInfinity: (vTextEmpty xor vTagInfinity), tagNan: (vTextEmpty xor vTagNan), tagCustom: (vTextEmpty xor vTagCustom), vector: (vTextEmpty xor vVector), vectorZero: (vTextEmpty xor vVectorZero), point: (vTextEmpty xor vPoint), pointZero: (vTextEmpty xor vPointZero), list: (vTextEmpty xor vList), map: (vTextEmpty xor vMap), dice: (vTextEmpty xor vDice))
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
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":boolean"
              value: true
          - name: "percentagePositive"
            value:
              type: ":boolean"
              value: true
          - name: "percentageZero"
            value:
              type: ":boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":boolean"
              value: true
          - name: "textTrueUpper"
            value:
              type: ":boolean"
              value: true
          - name: "textFalse"
            value:
              type: ":boolean"
              value: false
          - name: "textOne"
            value:
              type: ":boolean"
              value: true
          - name: "textZero"
            value:
              type: ":boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":boolean"
              value: true
          - name: "tagPhi"
            value:
              type: ":boolean"
              value: true
          - name: "tagInfinity"
            value:
              type: ":boolean"
              value: true
          - name: "tagNan"
            value:
              type: ":boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":nothing"
          - name: "map"
            value:
              type: ":nothing"
          - name: "dice"
            value:
              type: ":nothing"
```
