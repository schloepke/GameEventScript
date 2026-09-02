---
formatVersion: 1
suiteId: "runtime.atomic.boolean-logic.or-scalar-and-text"
title: "Boolean Logic — Or Scalars and Text"
categories: [conformance]
tags: [migrated-json-v1]
---

# Boolean Logic — Or Scalars and Text

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers or for nothing, boolean, numeric, percentage, and text operands.

---

## Test: or nothing

This runtime case exercises “or nothing” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0032
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or nothing.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAF
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
  emit Done(nothing: (vNothing or vNothing), booleanTrue: (vNothing or vBooleanTrue), booleanFalse: (vNothing or vBooleanFalse), integerOne: (vNothing or vIntegerOne), integerZero: (vNothing or vIntegerZero), floatPositive: (vNothing or vFloatPositive), percentagePositive: (vNothing or vPercentagePositive), percentageZero: (vNothing or vPercentageZero), textTrue: (vNothing or vTextTrue), textTrueUpper: (vNothing or vTextTrueUpper), textFalse: (vNothing or vTextFalse), textOne: (vNothing or vTextOne), textZero: (vNothing or vTextZero), textInvalid: (vNothing or vTextInvalid), textEmpty: (vNothing or vTextEmpty), tagTrue: (vNothing or vTagTrue), tagFalse: (vNothing or vTagFalse), tagTrueUpper: (vNothing or vTagTrueUpper), tagPi: (vNothing or vTagPi), tagPhi: (vNothing or vTagPhi), tagInfinity: (vNothing or vTagInfinity), tagNan: (vNothing or vTagNan), tagCustom: (vNothing or vTagCustom), vector: (vNothing or vVector), vectorZero: (vNothing or vVectorZero), point: (vNothing or vPoint), pointZero: (vNothing or vPointZero), list: (vNothing or vList), map: (vNothing or vMap), dice: (vNothing or vDice))
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
              type: ":nothing"
          - name: "integerOne"
            value:
              type: ":boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":nothing"
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
              type: ":nothing"
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
              type: ":nothing"
          - name: "textOne"
            value:
              type: ":boolean"
              value: true
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
              type: ":nothing"
          - name: "tagCustom"
            value:
              type: ":nothing"
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":nothing"
          - name: "point"
            value:
              type: ":boolean"
              value: true
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

## Test: or booleanTrue

This runtime case exercises “or booleanTrue” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0033
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or booleanTrue.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAG
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
  emit Done(nothing: (vBooleanTrue or vNothing), booleanTrue: (vBooleanTrue or vBooleanTrue), booleanFalse: (vBooleanTrue or vBooleanFalse), integerOne: (vBooleanTrue or vIntegerOne), integerZero: (vBooleanTrue or vIntegerZero), floatPositive: (vBooleanTrue or vFloatPositive), percentagePositive: (vBooleanTrue or vPercentagePositive), percentageZero: (vBooleanTrue or vPercentageZero), textTrue: (vBooleanTrue or vTextTrue), textTrueUpper: (vBooleanTrue or vTextTrueUpper), textFalse: (vBooleanTrue or vTextFalse), textOne: (vBooleanTrue or vTextOne), textZero: (vBooleanTrue or vTextZero), textInvalid: (vBooleanTrue or vTextInvalid), textEmpty: (vBooleanTrue or vTextEmpty), tagTrue: (vBooleanTrue or vTagTrue), tagFalse: (vBooleanTrue or vTagFalse), tagTrueUpper: (vBooleanTrue or vTagTrueUpper), tagPi: (vBooleanTrue or vTagPi), tagPhi: (vBooleanTrue or vTagPhi), tagInfinity: (vBooleanTrue or vTagInfinity), tagNan: (vBooleanTrue or vTagNan), tagCustom: (vBooleanTrue or vTagCustom), vector: (vBooleanTrue or vVector), vectorZero: (vBooleanTrue or vVectorZero), point: (vBooleanTrue or vPoint), pointZero: (vBooleanTrue or vPointZero), list: (vBooleanTrue or vList), map: (vBooleanTrue or vMap), dice: (vBooleanTrue or vDice))
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
              type: ":boolean"
              value: true
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":boolean"
              value: true
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
              value: true
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
              value: true
          - name: "textOne"
            value:
              type: ":boolean"
              value: true
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
              value: true
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
```

---

## Test: or booleanFalse

This runtime case exercises “or booleanFalse” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0034
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or booleanFalse.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAH
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
  emit Done(nothing: (vBooleanFalse or vNothing), booleanTrue: (vBooleanFalse or vBooleanTrue), booleanFalse: (vBooleanFalse or vBooleanFalse), integerOne: (vBooleanFalse or vIntegerOne), integerZero: (vBooleanFalse or vIntegerZero), floatPositive: (vBooleanFalse or vFloatPositive), percentagePositive: (vBooleanFalse or vPercentagePositive), percentageZero: (vBooleanFalse or vPercentageZero), textTrue: (vBooleanFalse or vTextTrue), textTrueUpper: (vBooleanFalse or vTextTrueUpper), textFalse: (vBooleanFalse or vTextFalse), textOne: (vBooleanFalse or vTextOne), textZero: (vBooleanFalse or vTextZero), textInvalid: (vBooleanFalse or vTextInvalid), textEmpty: (vBooleanFalse or vTextEmpty), tagTrue: (vBooleanFalse or vTagTrue), tagFalse: (vBooleanFalse or vTagFalse), tagTrueUpper: (vBooleanFalse or vTagTrueUpper), tagPi: (vBooleanFalse or vTagPi), tagPhi: (vBooleanFalse or vTagPhi), tagInfinity: (vBooleanFalse or vTagInfinity), tagNan: (vBooleanFalse or vTagNan), tagCustom: (vBooleanFalse or vTagCustom), vector: (vBooleanFalse or vVector), vectorZero: (vBooleanFalse or vVectorZero), point: (vBooleanFalse or vPoint), pointZero: (vBooleanFalse or vPointZero), list: (vBooleanFalse or vList), map: (vBooleanFalse or vMap), dice: (vBooleanFalse or vDice))
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

## Test: or integerOne

This runtime case exercises “or integerOne” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0035
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or integerOne.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAI
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
  emit Done(nothing: (vIntegerOne or vNothing), booleanTrue: (vIntegerOne or vBooleanTrue), booleanFalse: (vIntegerOne or vBooleanFalse), integerOne: (vIntegerOne or vIntegerOne), integerZero: (vIntegerOne or vIntegerZero), floatPositive: (vIntegerOne or vFloatPositive), percentagePositive: (vIntegerOne or vPercentagePositive), percentageZero: (vIntegerOne or vPercentageZero), textTrue: (vIntegerOne or vTextTrue), textTrueUpper: (vIntegerOne or vTextTrueUpper), textFalse: (vIntegerOne or vTextFalse), textOne: (vIntegerOne or vTextOne), textZero: (vIntegerOne or vTextZero), textInvalid: (vIntegerOne or vTextInvalid), textEmpty: (vIntegerOne or vTextEmpty), tagTrue: (vIntegerOne or vTagTrue), tagFalse: (vIntegerOne or vTagFalse), tagTrueUpper: (vIntegerOne or vTagTrueUpper), tagPi: (vIntegerOne or vTagPi), tagPhi: (vIntegerOne or vTagPhi), tagInfinity: (vIntegerOne or vTagInfinity), tagNan: (vIntegerOne or vTagNan), tagCustom: (vIntegerOne or vTagCustom), vector: (vIntegerOne or vVector), vectorZero: (vIntegerOne or vVectorZero), point: (vIntegerOne or vPoint), pointZero: (vIntegerOne or vPointZero), list: (vIntegerOne or vList), map: (vIntegerOne or vMap), dice: (vIntegerOne or vDice))
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
              type: ":boolean"
              value: true
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":boolean"
              value: true
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
              value: true
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
              value: true
          - name: "textOne"
            value:
              type: ":boolean"
              value: true
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
              value: true
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
```

---

## Test: or integerZero

This runtime case exercises “or integerZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0036
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or integerZero.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAJ
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
  emit Done(nothing: (vIntegerZero or vNothing), booleanTrue: (vIntegerZero or vBooleanTrue), booleanFalse: (vIntegerZero or vBooleanFalse), integerOne: (vIntegerZero or vIntegerOne), integerZero: (vIntegerZero or vIntegerZero), floatPositive: (vIntegerZero or vFloatPositive), percentagePositive: (vIntegerZero or vPercentagePositive), percentageZero: (vIntegerZero or vPercentageZero), textTrue: (vIntegerZero or vTextTrue), textTrueUpper: (vIntegerZero or vTextTrueUpper), textFalse: (vIntegerZero or vTextFalse), textOne: (vIntegerZero or vTextOne), textZero: (vIntegerZero or vTextZero), textInvalid: (vIntegerZero or vTextInvalid), textEmpty: (vIntegerZero or vTextEmpty), tagTrue: (vIntegerZero or vTagTrue), tagFalse: (vIntegerZero or vTagFalse), tagTrueUpper: (vIntegerZero or vTagTrueUpper), tagPi: (vIntegerZero or vTagPi), tagPhi: (vIntegerZero or vTagPhi), tagInfinity: (vIntegerZero or vTagInfinity), tagNan: (vIntegerZero or vTagNan), tagCustom: (vIntegerZero or vTagCustom), vector: (vIntegerZero or vVector), vectorZero: (vIntegerZero or vVectorZero), point: (vIntegerZero or vPoint), pointZero: (vIntegerZero or vPointZero), list: (vIntegerZero or vList), map: (vIntegerZero or vMap), dice: (vIntegerZero or vDice))
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

## Test: or floatPositive

This runtime case exercises “or floatPositive” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0037
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or floatPositive.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAK
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
  emit Done(nothing: (vFloatPositive or vNothing), booleanTrue: (vFloatPositive or vBooleanTrue), booleanFalse: (vFloatPositive or vBooleanFalse), integerOne: (vFloatPositive or vIntegerOne), integerZero: (vFloatPositive or vIntegerZero), floatPositive: (vFloatPositive or vFloatPositive), percentagePositive: (vFloatPositive or vPercentagePositive), percentageZero: (vFloatPositive or vPercentageZero), textTrue: (vFloatPositive or vTextTrue), textTrueUpper: (vFloatPositive or vTextTrueUpper), textFalse: (vFloatPositive or vTextFalse), textOne: (vFloatPositive or vTextOne), textZero: (vFloatPositive or vTextZero), textInvalid: (vFloatPositive or vTextInvalid), textEmpty: (vFloatPositive or vTextEmpty), tagTrue: (vFloatPositive or vTagTrue), tagFalse: (vFloatPositive or vTagFalse), tagTrueUpper: (vFloatPositive or vTagTrueUpper), tagPi: (vFloatPositive or vTagPi), tagPhi: (vFloatPositive or vTagPhi), tagInfinity: (vFloatPositive or vTagInfinity), tagNan: (vFloatPositive or vTagNan), tagCustom: (vFloatPositive or vTagCustom), vector: (vFloatPositive or vVector), vectorZero: (vFloatPositive or vVectorZero), point: (vFloatPositive or vPoint), pointZero: (vFloatPositive or vPointZero), list: (vFloatPositive or vList), map: (vFloatPositive or vMap), dice: (vFloatPositive or vDice))
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
              type: ":boolean"
              value: true
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":boolean"
              value: true
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
              value: true
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
              value: true
          - name: "textOne"
            value:
              type: ":boolean"
              value: true
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
              value: true
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
```

---

## Test: or percentagePositive

This runtime case exercises “or percentagePositive” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0038
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or percentagePositive.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAL
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
  emit Done(nothing: (vPercentagePositive or vNothing), booleanTrue: (vPercentagePositive or vBooleanTrue), booleanFalse: (vPercentagePositive or vBooleanFalse), integerOne: (vPercentagePositive or vIntegerOne), integerZero: (vPercentagePositive or vIntegerZero), floatPositive: (vPercentagePositive or vFloatPositive), percentagePositive: (vPercentagePositive or vPercentagePositive), percentageZero: (vPercentagePositive or vPercentageZero), textTrue: (vPercentagePositive or vTextTrue), textTrueUpper: (vPercentagePositive or vTextTrueUpper), textFalse: (vPercentagePositive or vTextFalse), textOne: (vPercentagePositive or vTextOne), textZero: (vPercentagePositive or vTextZero), textInvalid: (vPercentagePositive or vTextInvalid), textEmpty: (vPercentagePositive or vTextEmpty), tagTrue: (vPercentagePositive or vTagTrue), tagFalse: (vPercentagePositive or vTagFalse), tagTrueUpper: (vPercentagePositive or vTagTrueUpper), tagPi: (vPercentagePositive or vTagPi), tagPhi: (vPercentagePositive or vTagPhi), tagInfinity: (vPercentagePositive or vTagInfinity), tagNan: (vPercentagePositive or vTagNan), tagCustom: (vPercentagePositive or vTagCustom), vector: (vPercentagePositive or vVector), vectorZero: (vPercentagePositive or vVectorZero), point: (vPercentagePositive or vPoint), pointZero: (vPercentagePositive or vPointZero), list: (vPercentagePositive or vList), map: (vPercentagePositive or vMap), dice: (vPercentagePositive or vDice))
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
              type: ":boolean"
              value: true
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":boolean"
              value: true
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
              value: true
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
              value: true
          - name: "textOne"
            value:
              type: ":boolean"
              value: true
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
              value: true
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
```

---

## Test: or percentageZero

This runtime case exercises “or percentageZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0039
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or percentageZero.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAM
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
  emit Done(nothing: (vPercentageZero or vNothing), booleanTrue: (vPercentageZero or vBooleanTrue), booleanFalse: (vPercentageZero or vBooleanFalse), integerOne: (vPercentageZero or vIntegerOne), integerZero: (vPercentageZero or vIntegerZero), floatPositive: (vPercentageZero or vFloatPositive), percentagePositive: (vPercentageZero or vPercentagePositive), percentageZero: (vPercentageZero or vPercentageZero), textTrue: (vPercentageZero or vTextTrue), textTrueUpper: (vPercentageZero or vTextTrueUpper), textFalse: (vPercentageZero or vTextFalse), textOne: (vPercentageZero or vTextOne), textZero: (vPercentageZero or vTextZero), textInvalid: (vPercentageZero or vTextInvalid), textEmpty: (vPercentageZero or vTextEmpty), tagTrue: (vPercentageZero or vTagTrue), tagFalse: (vPercentageZero or vTagFalse), tagTrueUpper: (vPercentageZero or vTagTrueUpper), tagPi: (vPercentageZero or vTagPi), tagPhi: (vPercentageZero or vTagPhi), tagInfinity: (vPercentageZero or vTagInfinity), tagNan: (vPercentageZero or vTagNan), tagCustom: (vPercentageZero or vTagCustom), vector: (vPercentageZero or vVector), vectorZero: (vPercentageZero or vVectorZero), point: (vPercentageZero or vPoint), pointZero: (vPercentageZero or vPointZero), list: (vPercentageZero or vList), map: (vPercentageZero or vMap), dice: (vPercentageZero or vDice))
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

## Test: or textTrue

This runtime case exercises “or textTrue” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0040
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or textTrue.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAN
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
  emit Done(nothing: (vTextTrue or vNothing), booleanTrue: (vTextTrue or vBooleanTrue), booleanFalse: (vTextTrue or vBooleanFalse), integerOne: (vTextTrue or vIntegerOne), integerZero: (vTextTrue or vIntegerZero), floatPositive: (vTextTrue or vFloatPositive), percentagePositive: (vTextTrue or vPercentagePositive), percentageZero: (vTextTrue or vPercentageZero), textTrue: (vTextTrue or vTextTrue), textTrueUpper: (vTextTrue or vTextTrueUpper), textFalse: (vTextTrue or vTextFalse), textOne: (vTextTrue or vTextOne), textZero: (vTextTrue or vTextZero), textInvalid: (vTextTrue or vTextInvalid), textEmpty: (vTextTrue or vTextEmpty), tagTrue: (vTextTrue or vTagTrue), tagFalse: (vTextTrue or vTagFalse), tagTrueUpper: (vTextTrue or vTagTrueUpper), tagPi: (vTextTrue or vTagPi), tagPhi: (vTextTrue or vTagPhi), tagInfinity: (vTextTrue or vTagInfinity), tagNan: (vTextTrue or vTagNan), tagCustom: (vTextTrue or vTagCustom), vector: (vTextTrue or vVector), vectorZero: (vTextTrue or vVectorZero), point: (vTextTrue or vPoint), pointZero: (vTextTrue or vPointZero), list: (vTextTrue or vList), map: (vTextTrue or vMap), dice: (vTextTrue or vDice))
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
              type: ":boolean"
              value: true
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":boolean"
              value: true
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
              value: true
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
              value: true
          - name: "textOne"
            value:
              type: ":boolean"
              value: true
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
              value: true
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
```

---

## Test: or textTrueUpper

This runtime case exercises “or textTrueUpper” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0041
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or textTrueUpper.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAO
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
  emit Done(nothing: (vTextTrueUpper or vNothing), booleanTrue: (vTextTrueUpper or vBooleanTrue), booleanFalse: (vTextTrueUpper or vBooleanFalse), integerOne: (vTextTrueUpper or vIntegerOne), integerZero: (vTextTrueUpper or vIntegerZero), floatPositive: (vTextTrueUpper or vFloatPositive), percentagePositive: (vTextTrueUpper or vPercentagePositive), percentageZero: (vTextTrueUpper or vPercentageZero), textTrue: (vTextTrueUpper or vTextTrue), textTrueUpper: (vTextTrueUpper or vTextTrueUpper), textFalse: (vTextTrueUpper or vTextFalse), textOne: (vTextTrueUpper or vTextOne), textZero: (vTextTrueUpper or vTextZero), textInvalid: (vTextTrueUpper or vTextInvalid), textEmpty: (vTextTrueUpper or vTextEmpty), tagTrue: (vTextTrueUpper or vTagTrue), tagFalse: (vTextTrueUpper or vTagFalse), tagTrueUpper: (vTextTrueUpper or vTagTrueUpper), tagPi: (vTextTrueUpper or vTagPi), tagPhi: (vTextTrueUpper or vTagPhi), tagInfinity: (vTextTrueUpper or vTagInfinity), tagNan: (vTextTrueUpper or vTagNan), tagCustom: (vTextTrueUpper or vTagCustom), vector: (vTextTrueUpper or vVector), vectorZero: (vTextTrueUpper or vVectorZero), point: (vTextTrueUpper or vPoint), pointZero: (vTextTrueUpper or vPointZero), list: (vTextTrueUpper or vList), map: (vTextTrueUpper or vMap), dice: (vTextTrueUpper or vDice))
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
              type: ":boolean"
              value: true
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":boolean"
              value: true
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
              value: true
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
              value: true
          - name: "textOne"
            value:
              type: ":boolean"
              value: true
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
              value: true
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
```

---

## Test: or textFalse

This runtime case exercises “or textFalse” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0042
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or textFalse.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAP
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
  emit Done(nothing: (vTextFalse or vNothing), booleanTrue: (vTextFalse or vBooleanTrue), booleanFalse: (vTextFalse or vBooleanFalse), integerOne: (vTextFalse or vIntegerOne), integerZero: (vTextFalse or vIntegerZero), floatPositive: (vTextFalse or vFloatPositive), percentagePositive: (vTextFalse or vPercentagePositive), percentageZero: (vTextFalse or vPercentageZero), textTrue: (vTextFalse or vTextTrue), textTrueUpper: (vTextFalse or vTextTrueUpper), textFalse: (vTextFalse or vTextFalse), textOne: (vTextFalse or vTextOne), textZero: (vTextFalse or vTextZero), textInvalid: (vTextFalse or vTextInvalid), textEmpty: (vTextFalse or vTextEmpty), tagTrue: (vTextFalse or vTagTrue), tagFalse: (vTextFalse or vTagFalse), tagTrueUpper: (vTextFalse or vTagTrueUpper), tagPi: (vTextFalse or vTagPi), tagPhi: (vTextFalse or vTagPhi), tagInfinity: (vTextFalse or vTagInfinity), tagNan: (vTextFalse or vTagNan), tagCustom: (vTextFalse or vTagCustom), vector: (vTextFalse or vVector), vectorZero: (vTextFalse or vVectorZero), point: (vTextFalse or vPoint), pointZero: (vTextFalse or vPointZero), list: (vTextFalse or vList), map: (vTextFalse or vMap), dice: (vTextFalse or vDice))
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

## Test: or textOne

This runtime case exercises “or textOne” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0043
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or textOne.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAQ
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
  emit Done(nothing: (vTextOne or vNothing), booleanTrue: (vTextOne or vBooleanTrue), booleanFalse: (vTextOne or vBooleanFalse), integerOne: (vTextOne or vIntegerOne), integerZero: (vTextOne or vIntegerZero), floatPositive: (vTextOne or vFloatPositive), percentagePositive: (vTextOne or vPercentagePositive), percentageZero: (vTextOne or vPercentageZero), textTrue: (vTextOne or vTextTrue), textTrueUpper: (vTextOne or vTextTrueUpper), textFalse: (vTextOne or vTextFalse), textOne: (vTextOne or vTextOne), textZero: (vTextOne or vTextZero), textInvalid: (vTextOne or vTextInvalid), textEmpty: (vTextOne or vTextEmpty), tagTrue: (vTextOne or vTagTrue), tagFalse: (vTextOne or vTagFalse), tagTrueUpper: (vTextOne or vTagTrueUpper), tagPi: (vTextOne or vTagPi), tagPhi: (vTextOne or vTagPhi), tagInfinity: (vTextOne or vTagInfinity), tagNan: (vTextOne or vTagNan), tagCustom: (vTextOne or vTagCustom), vector: (vTextOne or vVector), vectorZero: (vTextOne or vVectorZero), point: (vTextOne or vPoint), pointZero: (vTextOne or vPointZero), list: (vTextOne or vList), map: (vTextOne or vMap), dice: (vTextOne or vDice))
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
              type: ":boolean"
              value: true
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":boolean"
              value: true
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
              value: true
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
              value: true
          - name: "textOne"
            value:
              type: ":boolean"
              value: true
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
              value: true
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: true
          - name: "vector"
            value:
              type: ":boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: true
          - name: "point"
            value:
              type: ":boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":boolean"
              value: true
          - name: "list"
            value:
              type: ":boolean"
              value: true
          - name: "map"
            value:
              type: ":boolean"
              value: true
          - name: "dice"
            value:
              type: ":boolean"
              value: true
```

---

## Test: or textZero

This runtime case exercises “or textZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0044
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or textZero.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAR
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
  emit Done(nothing: (vTextZero or vNothing), booleanTrue: (vTextZero or vBooleanTrue), booleanFalse: (vTextZero or vBooleanFalse), integerOne: (vTextZero or vIntegerOne), integerZero: (vTextZero or vIntegerZero), floatPositive: (vTextZero or vFloatPositive), percentagePositive: (vTextZero or vPercentagePositive), percentageZero: (vTextZero or vPercentageZero), textTrue: (vTextZero or vTextTrue), textTrueUpper: (vTextZero or vTextTrueUpper), textFalse: (vTextZero or vTextFalse), textOne: (vTextZero or vTextOne), textZero: (vTextZero or vTextZero), textInvalid: (vTextZero or vTextInvalid), textEmpty: (vTextZero or vTextEmpty), tagTrue: (vTextZero or vTagTrue), tagFalse: (vTextZero or vTagFalse), tagTrueUpper: (vTextZero or vTagTrueUpper), tagPi: (vTextZero or vTagPi), tagPhi: (vTextZero or vTagPhi), tagInfinity: (vTextZero or vTagInfinity), tagNan: (vTextZero or vTagNan), tagCustom: (vTextZero or vTagCustom), vector: (vTextZero or vVector), vectorZero: (vTextZero or vVectorZero), point: (vTextZero or vPoint), pointZero: (vTextZero or vPointZero), list: (vTextZero or vList), map: (vTextZero or vMap), dice: (vTextZero or vDice))
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

## Test: or textInvalid

This runtime case exercises “or textInvalid” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0045
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or textInvalid.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAS
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
  emit Done(nothing: (vTextInvalid or vNothing), booleanTrue: (vTextInvalid or vBooleanTrue), booleanFalse: (vTextInvalid or vBooleanFalse), integerOne: (vTextInvalid or vIntegerOne), integerZero: (vTextInvalid or vIntegerZero), floatPositive: (vTextInvalid or vFloatPositive), percentagePositive: (vTextInvalid or vPercentagePositive), percentageZero: (vTextInvalid or vPercentageZero), textTrue: (vTextInvalid or vTextTrue), textTrueUpper: (vTextInvalid or vTextTrueUpper), textFalse: (vTextInvalid or vTextFalse), textOne: (vTextInvalid or vTextOne), textZero: (vTextInvalid or vTextZero), textInvalid: (vTextInvalid or vTextInvalid), textEmpty: (vTextInvalid or vTextEmpty), tagTrue: (vTextInvalid or vTagTrue), tagFalse: (vTextInvalid or vTagFalse), tagTrueUpper: (vTextInvalid or vTagTrueUpper), tagPi: (vTextInvalid or vTagPi), tagPhi: (vTextInvalid or vTagPhi), tagInfinity: (vTextInvalid or vTagInfinity), tagNan: (vTextInvalid or vTagNan), tagCustom: (vTextInvalid or vTagCustom), vector: (vTextInvalid or vVector), vectorZero: (vTextInvalid or vVectorZero), point: (vTextInvalid or vPoint), pointZero: (vTextInvalid or vPointZero), list: (vTextInvalid or vList), map: (vTextInvalid or vMap), dice: (vTextInvalid or vDice))
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

## Test: or textEmpty

This runtime case exercises “or textEmpty” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0046
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or textEmpty.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAT
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
  emit Done(nothing: (vTextEmpty or vNothing), booleanTrue: (vTextEmpty or vBooleanTrue), booleanFalse: (vTextEmpty or vBooleanFalse), integerOne: (vTextEmpty or vIntegerOne), integerZero: (vTextEmpty or vIntegerZero), floatPositive: (vTextEmpty or vFloatPositive), percentagePositive: (vTextEmpty or vPercentagePositive), percentageZero: (vTextEmpty or vPercentageZero), textTrue: (vTextEmpty or vTextTrue), textTrueUpper: (vTextEmpty or vTextTrueUpper), textFalse: (vTextEmpty or vTextFalse), textOne: (vTextEmpty or vTextOne), textZero: (vTextEmpty or vTextZero), textInvalid: (vTextEmpty or vTextInvalid), textEmpty: (vTextEmpty or vTextEmpty), tagTrue: (vTextEmpty or vTagTrue), tagFalse: (vTextEmpty or vTagFalse), tagTrueUpper: (vTextEmpty or vTagTrueUpper), tagPi: (vTextEmpty or vTagPi), tagPhi: (vTextEmpty or vTagPhi), tagInfinity: (vTextEmpty or vTagInfinity), tagNan: (vTextEmpty or vTagNan), tagCustom: (vTextEmpty or vTagCustom), vector: (vTextEmpty or vVector), vectorZero: (vTextEmpty or vVectorZero), point: (vTextEmpty or vPoint), pointZero: (vTextEmpty or vPointZero), list: (vTextEmpty or vList), map: (vTextEmpty or vMap), dice: (vTextEmpty or vDice))
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
