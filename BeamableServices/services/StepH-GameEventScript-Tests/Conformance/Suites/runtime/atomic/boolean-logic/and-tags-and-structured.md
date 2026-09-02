---
formatVersion: 1
suiteId: "runtime.atomic.boolean-logic.and-tags-and-structured"
title: "Boolean Logic — And Tags and Structured Values"
categories: [conformance]
---

# Boolean Logic — And Tags and Structured Values

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers and for tags, spatial values, and collections.

---

## Test: and tagTrue

This runtime case exercises “and tagTrue” and verifies the declared messages, values, and execution result.

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
  - name: "and tagTrue.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicQ
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
  emit Done(nothing: (vTagTrue and vNothing), booleanTrue: (vTagTrue and vBooleanTrue), booleanFalse: (vTagTrue and vBooleanFalse), integerOne: (vTagTrue and vIntegerOne), integerZero: (vTagTrue and vIntegerZero), floatPositive: (vTagTrue and vFloatPositive), percentagePositive: (vTagTrue and vPercentagePositive), percentageZero: (vTagTrue and vPercentageZero), textTrue: (vTagTrue and vTextTrue), textTrueUpper: (vTagTrue and vTextTrueUpper), textFalse: (vTagTrue and vTextFalse), textOne: (vTagTrue and vTextOne), textZero: (vTagTrue and vTextZero), textInvalid: (vTagTrue and vTextInvalid), textEmpty: (vTagTrue and vTextEmpty), tagTrue: (vTagTrue and vTagTrue), tagFalse: (vTagTrue and vTagFalse), tagTrueUpper: (vTagTrue and vTagTrueUpper), tagPi: (vTagTrue and vTagPi), tagPhi: (vTagTrue and vTagPhi), tagInfinity: (vTagTrue and vTagInfinity), tagNan: (vTagTrue and vTagNan), tagCustom: (vTagTrue and vTagCustom), vector: (vTagTrue and vVector), vectorZero: (vTagTrue and vVectorZero), point: (vTagTrue and vPoint), pointZero: (vTagTrue and vPointZero), list: (vTagTrue and vList), map: (vTagTrue and vMap), dice: (vTagTrue and vDice))
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
              value: false
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":boolean"
              value: false
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
              value: false
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
              value: false
          - name: "textOne"
            value:
              type: ":boolean"
              value: false
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
              value: false
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
```

---

## Test: and tagFalse

This runtime case exercises “and tagFalse” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0018
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and tagFalse.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicR
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
  emit Done(nothing: (vTagFalse and vNothing), booleanTrue: (vTagFalse and vBooleanTrue), booleanFalse: (vTagFalse and vBooleanFalse), integerOne: (vTagFalse and vIntegerOne), integerZero: (vTagFalse and vIntegerZero), floatPositive: (vTagFalse and vFloatPositive), percentagePositive: (vTagFalse and vPercentagePositive), percentageZero: (vTagFalse and vPercentageZero), textTrue: (vTagFalse and vTextTrue), textTrueUpper: (vTagFalse and vTextTrueUpper), textFalse: (vTagFalse and vTextFalse), textOne: (vTagFalse and vTextOne), textZero: (vTagFalse and vTextZero), textInvalid: (vTagFalse and vTextInvalid), textEmpty: (vTagFalse and vTextEmpty), tagTrue: (vTagFalse and vTagTrue), tagFalse: (vTagFalse and vTagFalse), tagTrueUpper: (vTagFalse and vTagTrueUpper), tagPi: (vTagFalse and vTagPi), tagPhi: (vTagFalse and vTagPhi), tagInfinity: (vTagFalse and vTagInfinity), tagNan: (vTagFalse and vTagNan), tagCustom: (vTagFalse and vTagCustom), vector: (vTagFalse and vVector), vectorZero: (vTagFalse and vVectorZero), point: (vTagFalse and vPoint), pointZero: (vTagFalse and vPointZero), list: (vTagFalse and vList), map: (vTagFalse and vMap), dice: (vTagFalse and vDice))
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
              value: false
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":boolean"
              value: false
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
              value: false
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
              value: false
          - name: "textOne"
            value:
              type: ":boolean"
              value: false
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
              value: false
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
```

---

## Test: and tagTrueUpper

This runtime case exercises “and tagTrueUpper” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0019
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and tagTrueUpper.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicS
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
  emit Done(nothing: (vTagTrueUpper and vNothing), booleanTrue: (vTagTrueUpper and vBooleanTrue), booleanFalse: (vTagTrueUpper and vBooleanFalse), integerOne: (vTagTrueUpper and vIntegerOne), integerZero: (vTagTrueUpper and vIntegerZero), floatPositive: (vTagTrueUpper and vFloatPositive), percentagePositive: (vTagTrueUpper and vPercentagePositive), percentageZero: (vTagTrueUpper and vPercentageZero), textTrue: (vTagTrueUpper and vTextTrue), textTrueUpper: (vTagTrueUpper and vTextTrueUpper), textFalse: (vTagTrueUpper and vTextFalse), textOne: (vTagTrueUpper and vTextOne), textZero: (vTagTrueUpper and vTextZero), textInvalid: (vTagTrueUpper and vTextInvalid), textEmpty: (vTagTrueUpper and vTextEmpty), tagTrue: (vTagTrueUpper and vTagTrue), tagFalse: (vTagTrueUpper and vTagFalse), tagTrueUpper: (vTagTrueUpper and vTagTrueUpper), tagPi: (vTagTrueUpper and vTagPi), tagPhi: (vTagTrueUpper and vTagPhi), tagInfinity: (vTagTrueUpper and vTagInfinity), tagNan: (vTagTrueUpper and vTagNan), tagCustom: (vTagTrueUpper and vTagCustom), vector: (vTagTrueUpper and vVector), vectorZero: (vTagTrueUpper and vVectorZero), point: (vTagTrueUpper and vPoint), pointZero: (vTagTrueUpper and vPointZero), list: (vTagTrueUpper and vList), map: (vTagTrueUpper and vMap), dice: (vTagTrueUpper and vDice))
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
              value: false
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":boolean"
              value: false
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
              value: false
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
              value: false
          - name: "textOne"
            value:
              type: ":boolean"
              value: false
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
              value: false
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
```

---

## Test: and tagPi

This runtime case exercises “and tagPi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0020
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and tagPi.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicT
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
  emit Done(nothing: (vTagPi and vNothing), booleanTrue: (vTagPi and vBooleanTrue), booleanFalse: (vTagPi and vBooleanFalse), integerOne: (vTagPi and vIntegerOne), integerZero: (vTagPi and vIntegerZero), floatPositive: (vTagPi and vFloatPositive), percentagePositive: (vTagPi and vPercentagePositive), percentageZero: (vTagPi and vPercentageZero), textTrue: (vTagPi and vTextTrue), textTrueUpper: (vTagPi and vTextTrueUpper), textFalse: (vTagPi and vTextFalse), textOne: (vTagPi and vTextOne), textZero: (vTagPi and vTextZero), textInvalid: (vTagPi and vTextInvalid), textEmpty: (vTagPi and vTextEmpty), tagTrue: (vTagPi and vTagTrue), tagFalse: (vTagPi and vTagFalse), tagTrueUpper: (vTagPi and vTagTrueUpper), tagPi: (vTagPi and vTagPi), tagPhi: (vTagPi and vTagPhi), tagInfinity: (vTagPi and vTagInfinity), tagNan: (vTagPi and vTagNan), tagCustom: (vTagPi and vTagCustom), vector: (vTagPi and vVector), vectorZero: (vTagPi and vVectorZero), point: (vTagPi and vPoint), pointZero: (vTagPi and vPointZero), list: (vTagPi and vList), map: (vTagPi and vMap), dice: (vTagPi and vDice))
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

## Test: and tagPhi

This runtime case exercises “and tagPhi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0021
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and tagPhi.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicU
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
  emit Done(nothing: (vTagPhi and vNothing), booleanTrue: (vTagPhi and vBooleanTrue), booleanFalse: (vTagPhi and vBooleanFalse), integerOne: (vTagPhi and vIntegerOne), integerZero: (vTagPhi and vIntegerZero), floatPositive: (vTagPhi and vFloatPositive), percentagePositive: (vTagPhi and vPercentagePositive), percentageZero: (vTagPhi and vPercentageZero), textTrue: (vTagPhi and vTextTrue), textTrueUpper: (vTagPhi and vTextTrueUpper), textFalse: (vTagPhi and vTextFalse), textOne: (vTagPhi and vTextOne), textZero: (vTagPhi and vTextZero), textInvalid: (vTagPhi and vTextInvalid), textEmpty: (vTagPhi and vTextEmpty), tagTrue: (vTagPhi and vTagTrue), tagFalse: (vTagPhi and vTagFalse), tagTrueUpper: (vTagPhi and vTagTrueUpper), tagPi: (vTagPhi and vTagPi), tagPhi: (vTagPhi and vTagPhi), tagInfinity: (vTagPhi and vTagInfinity), tagNan: (vTagPhi and vTagNan), tagCustom: (vTagPhi and vTagCustom), vector: (vTagPhi and vVector), vectorZero: (vTagPhi and vVectorZero), point: (vTagPhi and vPoint), pointZero: (vTagPhi and vPointZero), list: (vTagPhi and vList), map: (vTagPhi and vMap), dice: (vTagPhi and vDice))
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

## Test: and tagInfinity

This runtime case exercises “and tagInfinity” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0022
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and tagInfinity.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicV
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
  emit Done(nothing: (vTagInfinity and vNothing), booleanTrue: (vTagInfinity and vBooleanTrue), booleanFalse: (vTagInfinity and vBooleanFalse), integerOne: (vTagInfinity and vIntegerOne), integerZero: (vTagInfinity and vIntegerZero), floatPositive: (vTagInfinity and vFloatPositive), percentagePositive: (vTagInfinity and vPercentagePositive), percentageZero: (vTagInfinity and vPercentageZero), textTrue: (vTagInfinity and vTextTrue), textTrueUpper: (vTagInfinity and vTextTrueUpper), textFalse: (vTagInfinity and vTextFalse), textOne: (vTagInfinity and vTextOne), textZero: (vTagInfinity and vTextZero), textInvalid: (vTagInfinity and vTextInvalid), textEmpty: (vTagInfinity and vTextEmpty), tagTrue: (vTagInfinity and vTagTrue), tagFalse: (vTagInfinity and vTagFalse), tagTrueUpper: (vTagInfinity and vTagTrueUpper), tagPi: (vTagInfinity and vTagPi), tagPhi: (vTagInfinity and vTagPhi), tagInfinity: (vTagInfinity and vTagInfinity), tagNan: (vTagInfinity and vTagNan), tagCustom: (vTagInfinity and vTagCustom), vector: (vTagInfinity and vVector), vectorZero: (vTagInfinity and vVectorZero), point: (vTagInfinity and vPoint), pointZero: (vTagInfinity and vPointZero), list: (vTagInfinity and vList), map: (vTagInfinity and vMap), dice: (vTagInfinity and vDice))
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

## Test: and tagNan

This runtime case exercises “and tagNan” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0023
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and tagNan.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicW
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
  emit Done(nothing: (vTagNan and vNothing), booleanTrue: (vTagNan and vBooleanTrue), booleanFalse: (vTagNan and vBooleanFalse), integerOne: (vTagNan and vIntegerOne), integerZero: (vTagNan and vIntegerZero), floatPositive: (vTagNan and vFloatPositive), percentagePositive: (vTagNan and vPercentagePositive), percentageZero: (vTagNan and vPercentageZero), textTrue: (vTagNan and vTextTrue), textTrueUpper: (vTagNan and vTextTrueUpper), textFalse: (vTagNan and vTextFalse), textOne: (vTagNan and vTextOne), textZero: (vTagNan and vTextZero), textInvalid: (vTagNan and vTextInvalid), textEmpty: (vTagNan and vTextEmpty), tagTrue: (vTagNan and vTagTrue), tagFalse: (vTagNan and vTagFalse), tagTrueUpper: (vTagNan and vTagTrueUpper), tagPi: (vTagNan and vTagPi), tagPhi: (vTagNan and vTagPhi), tagInfinity: (vTagNan and vTagInfinity), tagNan: (vTagNan and vTagNan), tagCustom: (vTagNan and vTagCustom), vector: (vTagNan and vVector), vectorZero: (vTagNan and vVectorZero), point: (vTagNan and vPoint), pointZero: (vTagNan and vPointZero), list: (vTagNan and vList), map: (vTagNan and vMap), dice: (vTagNan and vDice))
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
              value: false
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":boolean"
              value: false
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
              value: false
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
              value: false
          - name: "textOne"
            value:
              type: ":boolean"
              value: false
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
              value: false
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
```

---

## Test: and tagCustom

This runtime case exercises “and tagCustom” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0024
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and tagCustom.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicX
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
  emit Done(nothing: (vTagCustom and vNothing), booleanTrue: (vTagCustom and vBooleanTrue), booleanFalse: (vTagCustom and vBooleanFalse), integerOne: (vTagCustom and vIntegerOne), integerZero: (vTagCustom and vIntegerZero), floatPositive: (vTagCustom and vFloatPositive), percentagePositive: (vTagCustom and vPercentagePositive), percentageZero: (vTagCustom and vPercentageZero), textTrue: (vTagCustom and vTextTrue), textTrueUpper: (vTagCustom and vTextTrueUpper), textFalse: (vTagCustom and vTextFalse), textOne: (vTagCustom and vTextOne), textZero: (vTagCustom and vTextZero), textInvalid: (vTagCustom and vTextInvalid), textEmpty: (vTagCustom and vTextEmpty), tagTrue: (vTagCustom and vTagTrue), tagFalse: (vTagCustom and vTagFalse), tagTrueUpper: (vTagCustom and vTagTrueUpper), tagPi: (vTagCustom and vTagPi), tagPhi: (vTagCustom and vTagPhi), tagInfinity: (vTagCustom and vTagInfinity), tagNan: (vTagCustom and vTagNan), tagCustom: (vTagCustom and vTagCustom), vector: (vTagCustom and vVector), vectorZero: (vTagCustom and vVectorZero), point: (vTagCustom and vPoint), pointZero: (vTagCustom and vPointZero), list: (vTagCustom and vList), map: (vTagCustom and vMap), dice: (vTagCustom and vDice))
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
              value: false
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":boolean"
              value: false
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
              value: false
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
              value: false
          - name: "textOne"
            value:
              type: ":boolean"
              value: false
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
              value: false
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
```

---

## Test: and vector

This runtime case exercises “and vector” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0025
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and vector.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicY
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
  emit Done(nothing: (vVector and vNothing), booleanTrue: (vVector and vBooleanTrue), booleanFalse: (vVector and vBooleanFalse), integerOne: (vVector and vIntegerOne), integerZero: (vVector and vIntegerZero), floatPositive: (vVector and vFloatPositive), percentagePositive: (vVector and vPercentagePositive), percentageZero: (vVector and vPercentageZero), textTrue: (vVector and vTextTrue), textTrueUpper: (vVector and vTextTrueUpper), textFalse: (vVector and vTextFalse), textOne: (vVector and vTextOne), textZero: (vVector and vTextZero), textInvalid: (vVector and vTextInvalid), textEmpty: (vVector and vTextEmpty), tagTrue: (vVector and vTagTrue), tagFalse: (vVector and vTagFalse), tagTrueUpper: (vVector and vTagTrueUpper), tagPi: (vVector and vTagPi), tagPhi: (vVector and vTagPhi), tagInfinity: (vVector and vTagInfinity), tagNan: (vVector and vTagNan), tagCustom: (vVector and vTagCustom), vector: (vVector and vVector), vectorZero: (vVector and vVectorZero), point: (vVector and vPoint), pointZero: (vVector and vPointZero), list: (vVector and vList), map: (vVector and vMap), dice: (vVector and vDice))
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

## Test: and vectorZero

This runtime case exercises “and vectorZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0026
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and vectorZero.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicZ
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
  emit Done(nothing: (vVectorZero and vNothing), booleanTrue: (vVectorZero and vBooleanTrue), booleanFalse: (vVectorZero and vBooleanFalse), integerOne: (vVectorZero and vIntegerOne), integerZero: (vVectorZero and vIntegerZero), floatPositive: (vVectorZero and vFloatPositive), percentagePositive: (vVectorZero and vPercentagePositive), percentageZero: (vVectorZero and vPercentageZero), textTrue: (vVectorZero and vTextTrue), textTrueUpper: (vVectorZero and vTextTrueUpper), textFalse: (vVectorZero and vTextFalse), textOne: (vVectorZero and vTextOne), textZero: (vVectorZero and vTextZero), textInvalid: (vVectorZero and vTextInvalid), textEmpty: (vVectorZero and vTextEmpty), tagTrue: (vVectorZero and vTagTrue), tagFalse: (vVectorZero and vTagFalse), tagTrueUpper: (vVectorZero and vTagTrueUpper), tagPi: (vVectorZero and vTagPi), tagPhi: (vVectorZero and vTagPhi), tagInfinity: (vVectorZero and vTagInfinity), tagNan: (vVectorZero and vTagNan), tagCustom: (vVectorZero and vTagCustom), vector: (vVectorZero and vVector), vectorZero: (vVectorZero and vVectorZero), point: (vVectorZero and vPoint), pointZero: (vVectorZero and vPointZero), list: (vVectorZero and vList), map: (vVectorZero and vMap), dice: (vVectorZero and vDice))
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
              value: false
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":boolean"
              value: false
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
              value: false
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
              value: false
          - name: "textOne"
            value:
              type: ":boolean"
              value: false
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
              value: false
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
```

---

## Test: and point

This runtime case exercises “and point” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0027
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and point.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAA
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
  emit Done(nothing: (vPoint and vNothing), booleanTrue: (vPoint and vBooleanTrue), booleanFalse: (vPoint and vBooleanFalse), integerOne: (vPoint and vIntegerOne), integerZero: (vPoint and vIntegerZero), floatPositive: (vPoint and vFloatPositive), percentagePositive: (vPoint and vPercentagePositive), percentageZero: (vPoint and vPercentageZero), textTrue: (vPoint and vTextTrue), textTrueUpper: (vPoint and vTextTrueUpper), textFalse: (vPoint and vTextFalse), textOne: (vPoint and vTextOne), textZero: (vPoint and vTextZero), textInvalid: (vPoint and vTextInvalid), textEmpty: (vPoint and vTextEmpty), tagTrue: (vPoint and vTagTrue), tagFalse: (vPoint and vTagFalse), tagTrueUpper: (vPoint and vTagTrueUpper), tagPi: (vPoint and vTagPi), tagPhi: (vPoint and vTagPhi), tagInfinity: (vPoint and vTagInfinity), tagNan: (vPoint and vTagNan), tagCustom: (vPoint and vTagCustom), vector: (vPoint and vVector), vectorZero: (vPoint and vVectorZero), point: (vPoint and vPoint), pointZero: (vPoint and vPointZero), list: (vPoint and vList), map: (vPoint and vMap), dice: (vPoint and vDice))
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

## Test: and pointZero

This runtime case exercises “and pointZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0028
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and pointZero.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAB
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
  emit Done(nothing: (vPointZero and vNothing), booleanTrue: (vPointZero and vBooleanTrue), booleanFalse: (vPointZero and vBooleanFalse), integerOne: (vPointZero and vIntegerOne), integerZero: (vPointZero and vIntegerZero), floatPositive: (vPointZero and vFloatPositive), percentagePositive: (vPointZero and vPercentagePositive), percentageZero: (vPointZero and vPercentageZero), textTrue: (vPointZero and vTextTrue), textTrueUpper: (vPointZero and vTextTrueUpper), textFalse: (vPointZero and vTextFalse), textOne: (vPointZero and vTextOne), textZero: (vPointZero and vTextZero), textInvalid: (vPointZero and vTextInvalid), textEmpty: (vPointZero and vTextEmpty), tagTrue: (vPointZero and vTagTrue), tagFalse: (vPointZero and vTagFalse), tagTrueUpper: (vPointZero and vTagTrueUpper), tagPi: (vPointZero and vTagPi), tagPhi: (vPointZero and vTagPhi), tagInfinity: (vPointZero and vTagInfinity), tagNan: (vPointZero and vTagNan), tagCustom: (vPointZero and vTagCustom), vector: (vPointZero and vVector), vectorZero: (vPointZero and vVectorZero), point: (vPointZero and vPoint), pointZero: (vPointZero and vPointZero), list: (vPointZero and vList), map: (vPointZero and vMap), dice: (vPointZero and vDice))
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
              value: false
          - name: "booleanTrue"
            value:
              type: ":boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":boolean"
              value: false
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
              value: false
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
              value: false
          - name: "textOne"
            value:
              type: ":boolean"
              value: false
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
              value: false
          - name: "tagCustom"
            value:
              type: ":boolean"
              value: false
          - name: "vector"
            value:
              type: ":boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":boolean"
              value: false
          - name: "list"
            value:
              type: ":boolean"
              value: false
          - name: "map"
            value:
              type: ":boolean"
              value: false
          - name: "dice"
            value:
              type: ":boolean"
              value: false
```

---

## Test: and list

This runtime case exercises “and list” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0029
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and list.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAC
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
  emit Done(nothing: (vList and vNothing), booleanTrue: (vList and vBooleanTrue), booleanFalse: (vList and vBooleanFalse), integerOne: (vList and vIntegerOne), integerZero: (vList and vIntegerZero), floatPositive: (vList and vFloatPositive), percentagePositive: (vList and vPercentagePositive), percentageZero: (vList and vPercentageZero), textTrue: (vList and vTextTrue), textTrueUpper: (vList and vTextTrueUpper), textFalse: (vList and vTextFalse), textOne: (vList and vTextOne), textZero: (vList and vTextZero), textInvalid: (vList and vTextInvalid), textEmpty: (vList and vTextEmpty), tagTrue: (vList and vTagTrue), tagFalse: (vList and vTagFalse), tagTrueUpper: (vList and vTagTrueUpper), tagPi: (vList and vTagPi), tagPhi: (vList and vTagPhi), tagInfinity: (vList and vTagInfinity), tagNan: (vList and vTagNan), tagCustom: (vList and vTagCustom), vector: (vList and vVector), vectorZero: (vList and vVectorZero), point: (vList and vPoint), pointZero: (vList and vPointZero), list: (vList and vList), map: (vList and vMap), dice: (vList and vDice))
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
              type: ":boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":nothing"
          - name: "integerZero"
            value:
              type: ":boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":nothing"
          - name: "percentagePositive"
            value:
              type: ":nothing"
          - name: "percentageZero"
            value:
              type: ":boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":nothing"
          - name: "textTrueUpper"
            value:
              type: ":nothing"
          - name: "textFalse"
            value:
              type: ":boolean"
              value: false
          - name: "textOne"
            value:
              type: ":nothing"
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
              type: ":nothing"
          - name: "tagPhi"
            value:
              type: ":nothing"
          - name: "tagInfinity"
            value:
              type: ":nothing"
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
              type: ":nothing"
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":nothing"
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

## Test: and map

This runtime case exercises “and map” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0030
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and map.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAD
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
  emit Done(nothing: (vMap and vNothing), booleanTrue: (vMap and vBooleanTrue), booleanFalse: (vMap and vBooleanFalse), integerOne: (vMap and vIntegerOne), integerZero: (vMap and vIntegerZero), floatPositive: (vMap and vFloatPositive), percentagePositive: (vMap and vPercentagePositive), percentageZero: (vMap and vPercentageZero), textTrue: (vMap and vTextTrue), textTrueUpper: (vMap and vTextTrueUpper), textFalse: (vMap and vTextFalse), textOne: (vMap and vTextOne), textZero: (vMap and vTextZero), textInvalid: (vMap and vTextInvalid), textEmpty: (vMap and vTextEmpty), tagTrue: (vMap and vTagTrue), tagFalse: (vMap and vTagFalse), tagTrueUpper: (vMap and vTagTrueUpper), tagPi: (vMap and vTagPi), tagPhi: (vMap and vTagPhi), tagInfinity: (vMap and vTagInfinity), tagNan: (vMap and vTagNan), tagCustom: (vMap and vTagCustom), vector: (vMap and vVector), vectorZero: (vMap and vVectorZero), point: (vMap and vPoint), pointZero: (vMap and vPointZero), list: (vMap and vList), map: (vMap and vMap), dice: (vMap and vDice))
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
              type: ":boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":nothing"
          - name: "integerZero"
            value:
              type: ":boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":nothing"
          - name: "percentagePositive"
            value:
              type: ":nothing"
          - name: "percentageZero"
            value:
              type: ":boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":nothing"
          - name: "textTrueUpper"
            value:
              type: ":nothing"
          - name: "textFalse"
            value:
              type: ":boolean"
              value: false
          - name: "textOne"
            value:
              type: ":nothing"
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
              type: ":nothing"
          - name: "tagPhi"
            value:
              type: ":nothing"
          - name: "tagInfinity"
            value:
              type: ":nothing"
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
              type: ":nothing"
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":nothing"
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

## Test: and dice

This runtime case exercises “and dice” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0031
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and dice.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAE
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
  emit Done(nothing: (vDice and vNothing), booleanTrue: (vDice and vBooleanTrue), booleanFalse: (vDice and vBooleanFalse), integerOne: (vDice and vIntegerOne), integerZero: (vDice and vIntegerZero), floatPositive: (vDice and vFloatPositive), percentagePositive: (vDice and vPercentagePositive), percentageZero: (vDice and vPercentageZero), textTrue: (vDice and vTextTrue), textTrueUpper: (vDice and vTextTrueUpper), textFalse: (vDice and vTextFalse), textOne: (vDice and vTextOne), textZero: (vDice and vTextZero), textInvalid: (vDice and vTextInvalid), textEmpty: (vDice and vTextEmpty), tagTrue: (vDice and vTagTrue), tagFalse: (vDice and vTagFalse), tagTrueUpper: (vDice and vTagTrueUpper), tagPi: (vDice and vTagPi), tagPhi: (vDice and vTagPhi), tagInfinity: (vDice and vTagInfinity), tagNan: (vDice and vTagNan), tagCustom: (vDice and vTagCustom), vector: (vDice and vVector), vectorZero: (vDice and vVectorZero), point: (vDice and vPoint), pointZero: (vDice and vPointZero), list: (vDice and vList), map: (vDice and vMap), dice: (vDice and vDice))
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
              type: ":boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":nothing"
          - name: "integerZero"
            value:
              type: ":boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":nothing"
          - name: "percentagePositive"
            value:
              type: ":nothing"
          - name: "percentageZero"
            value:
              type: ":boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":nothing"
          - name: "textTrueUpper"
            value:
              type: ":nothing"
          - name: "textFalse"
            value:
              type: ":boolean"
              value: false
          - name: "textOne"
            value:
              type: ":nothing"
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
              type: ":nothing"
          - name: "tagPhi"
            value:
              type: ":nothing"
          - name: "tagInfinity"
            value:
              type: ":nothing"
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
              type: ":nothing"
          - name: "vectorZero"
            value:
              type: ":boolean"
              value: false
          - name: "point"
            value:
              type: ":nothing"
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
