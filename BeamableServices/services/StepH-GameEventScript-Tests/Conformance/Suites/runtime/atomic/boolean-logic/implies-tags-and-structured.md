---
formatVersion: 1
suiteId: "runtime.atomic.boolean-logic.implies-tags-and-structured"
title: "Boolean Logic — Implies Tags and Structured Values"
categories: [conformance]
tags: [migrated-json-v1]
---

# Boolean Logic — Implies Tags and Structured Values

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers implies for tags, spatial values, and collections.

---

## Test: implies tagTrue

This runtime case exercises “implies tagTrue” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0107
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies tagTrue.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicDC
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
  emit Done(nothing: (vTagTrue -> vNothing), booleanTrue: (vTagTrue -> vBooleanTrue), booleanFalse: (vTagTrue -> vBooleanFalse), integerOne: (vTagTrue -> vIntegerOne), integerZero: (vTagTrue -> vIntegerZero), floatPositive: (vTagTrue -> vFloatPositive), percentagePositive: (vTagTrue -> vPercentagePositive), percentageZero: (vTagTrue -> vPercentageZero), textTrue: (vTagTrue -> vTextTrue), textTrueUpper: (vTagTrue -> vTextTrueUpper), textFalse: (vTagTrue -> vTextFalse), textOne: (vTagTrue -> vTextOne), textZero: (vTagTrue -> vTextZero), textInvalid: (vTagTrue -> vTextInvalid), textEmpty: (vTagTrue -> vTextEmpty), tagTrue: (vTagTrue -> vTagTrue), tagFalse: (vTagTrue -> vTagFalse), tagTrueUpper: (vTagTrue -> vTagTrueUpper), tagPi: (vTagTrue -> vTagPi), tagPhi: (vTagTrue -> vTagPhi), tagInfinity: (vTagTrue -> vTagInfinity), tagNan: (vTagTrue -> vTagNan), tagCustom: (vTagTrue -> vTagCustom), vector: (vTagTrue -> vVector), vectorZero: (vTagTrue -> vVectorZero), point: (vTagTrue -> vPoint), pointZero: (vTagTrue -> vPointZero), list: (vTagTrue -> vList), map: (vTagTrue -> vMap), dice: (vTagTrue -> vDice))
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

## Test: implies tagFalse

This runtime case exercises “implies tagFalse” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0108
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies tagFalse.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicDD
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
  emit Done(nothing: (vTagFalse -> vNothing), booleanTrue: (vTagFalse -> vBooleanTrue), booleanFalse: (vTagFalse -> vBooleanFalse), integerOne: (vTagFalse -> vIntegerOne), integerZero: (vTagFalse -> vIntegerZero), floatPositive: (vTagFalse -> vFloatPositive), percentagePositive: (vTagFalse -> vPercentagePositive), percentageZero: (vTagFalse -> vPercentageZero), textTrue: (vTagFalse -> vTextTrue), textTrueUpper: (vTagFalse -> vTextTrueUpper), textFalse: (vTagFalse -> vTextFalse), textOne: (vTagFalse -> vTextOne), textZero: (vTagFalse -> vTextZero), textInvalid: (vTagFalse -> vTextInvalid), textEmpty: (vTagFalse -> vTextEmpty), tagTrue: (vTagFalse -> vTagTrue), tagFalse: (vTagFalse -> vTagFalse), tagTrueUpper: (vTagFalse -> vTagTrueUpper), tagPi: (vTagFalse -> vTagPi), tagPhi: (vTagFalse -> vTagPhi), tagInfinity: (vTagFalse -> vTagInfinity), tagNan: (vTagFalse -> vTagNan), tagCustom: (vTagFalse -> vTagCustom), vector: (vTagFalse -> vVector), vectorZero: (vTagFalse -> vVectorZero), point: (vTagFalse -> vPoint), pointZero: (vTagFalse -> vPointZero), list: (vTagFalse -> vList), map: (vTagFalse -> vMap), dice: (vTagFalse -> vDice))
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

## Test: implies tagTrueUpper

This runtime case exercises “implies tagTrueUpper” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0109
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies tagTrueUpper.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicDE
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
  emit Done(nothing: (vTagTrueUpper -> vNothing), booleanTrue: (vTagTrueUpper -> vBooleanTrue), booleanFalse: (vTagTrueUpper -> vBooleanFalse), integerOne: (vTagTrueUpper -> vIntegerOne), integerZero: (vTagTrueUpper -> vIntegerZero), floatPositive: (vTagTrueUpper -> vFloatPositive), percentagePositive: (vTagTrueUpper -> vPercentagePositive), percentageZero: (vTagTrueUpper -> vPercentageZero), textTrue: (vTagTrueUpper -> vTextTrue), textTrueUpper: (vTagTrueUpper -> vTextTrueUpper), textFalse: (vTagTrueUpper -> vTextFalse), textOne: (vTagTrueUpper -> vTextOne), textZero: (vTagTrueUpper -> vTextZero), textInvalid: (vTagTrueUpper -> vTextInvalid), textEmpty: (vTagTrueUpper -> vTextEmpty), tagTrue: (vTagTrueUpper -> vTagTrue), tagFalse: (vTagTrueUpper -> vTagFalse), tagTrueUpper: (vTagTrueUpper -> vTagTrueUpper), tagPi: (vTagTrueUpper -> vTagPi), tagPhi: (vTagTrueUpper -> vTagPhi), tagInfinity: (vTagTrueUpper -> vTagInfinity), tagNan: (vTagTrueUpper -> vTagNan), tagCustom: (vTagTrueUpper -> vTagCustom), vector: (vTagTrueUpper -> vVector), vectorZero: (vTagTrueUpper -> vVectorZero), point: (vTagTrueUpper -> vPoint), pointZero: (vTagTrueUpper -> vPointZero), list: (vTagTrueUpper -> vList), map: (vTagTrueUpper -> vMap), dice: (vTagTrueUpper -> vDice))
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

## Test: implies tagPi

This runtime case exercises “implies tagPi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0110
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies tagPi.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicDF
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
  emit Done(nothing: (vTagPi -> vNothing), booleanTrue: (vTagPi -> vBooleanTrue), booleanFalse: (vTagPi -> vBooleanFalse), integerOne: (vTagPi -> vIntegerOne), integerZero: (vTagPi -> vIntegerZero), floatPositive: (vTagPi -> vFloatPositive), percentagePositive: (vTagPi -> vPercentagePositive), percentageZero: (vTagPi -> vPercentageZero), textTrue: (vTagPi -> vTextTrue), textTrueUpper: (vTagPi -> vTextTrueUpper), textFalse: (vTagPi -> vTextFalse), textOne: (vTagPi -> vTextOne), textZero: (vTagPi -> vTextZero), textInvalid: (vTagPi -> vTextInvalid), textEmpty: (vTagPi -> vTextEmpty), tagTrue: (vTagPi -> vTagTrue), tagFalse: (vTagPi -> vTagFalse), tagTrueUpper: (vTagPi -> vTagTrueUpper), tagPi: (vTagPi -> vTagPi), tagPhi: (vTagPi -> vTagPhi), tagInfinity: (vTagPi -> vTagInfinity), tagNan: (vTagPi -> vTagNan), tagCustom: (vTagPi -> vTagCustom), vector: (vTagPi -> vVector), vectorZero: (vTagPi -> vVectorZero), point: (vTagPi -> vPoint), pointZero: (vTagPi -> vPointZero), list: (vTagPi -> vList), map: (vTagPi -> vMap), dice: (vTagPi -> vDice))
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

## Test: implies tagPhi

This runtime case exercises “implies tagPhi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0111
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies tagPhi.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicDG
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
  emit Done(nothing: (vTagPhi -> vNothing), booleanTrue: (vTagPhi -> vBooleanTrue), booleanFalse: (vTagPhi -> vBooleanFalse), integerOne: (vTagPhi -> vIntegerOne), integerZero: (vTagPhi -> vIntegerZero), floatPositive: (vTagPhi -> vFloatPositive), percentagePositive: (vTagPhi -> vPercentagePositive), percentageZero: (vTagPhi -> vPercentageZero), textTrue: (vTagPhi -> vTextTrue), textTrueUpper: (vTagPhi -> vTextTrueUpper), textFalse: (vTagPhi -> vTextFalse), textOne: (vTagPhi -> vTextOne), textZero: (vTagPhi -> vTextZero), textInvalid: (vTagPhi -> vTextInvalid), textEmpty: (vTagPhi -> vTextEmpty), tagTrue: (vTagPhi -> vTagTrue), tagFalse: (vTagPhi -> vTagFalse), tagTrueUpper: (vTagPhi -> vTagTrueUpper), tagPi: (vTagPhi -> vTagPi), tagPhi: (vTagPhi -> vTagPhi), tagInfinity: (vTagPhi -> vTagInfinity), tagNan: (vTagPhi -> vTagNan), tagCustom: (vTagPhi -> vTagCustom), vector: (vTagPhi -> vVector), vectorZero: (vTagPhi -> vVectorZero), point: (vTagPhi -> vPoint), pointZero: (vTagPhi -> vPointZero), list: (vTagPhi -> vList), map: (vTagPhi -> vMap), dice: (vTagPhi -> vDice))
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

## Test: implies tagInfinity

This runtime case exercises “implies tagInfinity” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0112
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies tagInfinity.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicDH
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
  emit Done(nothing: (vTagInfinity -> vNothing), booleanTrue: (vTagInfinity -> vBooleanTrue), booleanFalse: (vTagInfinity -> vBooleanFalse), integerOne: (vTagInfinity -> vIntegerOne), integerZero: (vTagInfinity -> vIntegerZero), floatPositive: (vTagInfinity -> vFloatPositive), percentagePositive: (vTagInfinity -> vPercentagePositive), percentageZero: (vTagInfinity -> vPercentageZero), textTrue: (vTagInfinity -> vTextTrue), textTrueUpper: (vTagInfinity -> vTextTrueUpper), textFalse: (vTagInfinity -> vTextFalse), textOne: (vTagInfinity -> vTextOne), textZero: (vTagInfinity -> vTextZero), textInvalid: (vTagInfinity -> vTextInvalid), textEmpty: (vTagInfinity -> vTextEmpty), tagTrue: (vTagInfinity -> vTagTrue), tagFalse: (vTagInfinity -> vTagFalse), tagTrueUpper: (vTagInfinity -> vTagTrueUpper), tagPi: (vTagInfinity -> vTagPi), tagPhi: (vTagInfinity -> vTagPhi), tagInfinity: (vTagInfinity -> vTagInfinity), tagNan: (vTagInfinity -> vTagNan), tagCustom: (vTagInfinity -> vTagCustom), vector: (vTagInfinity -> vVector), vectorZero: (vTagInfinity -> vVectorZero), point: (vTagInfinity -> vPoint), pointZero: (vTagInfinity -> vPointZero), list: (vTagInfinity -> vList), map: (vTagInfinity -> vMap), dice: (vTagInfinity -> vDice))
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

## Test: implies tagNan

This runtime case exercises “implies tagNan” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0113
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies tagNan.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicDI
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
  emit Done(nothing: (vTagNan -> vNothing), booleanTrue: (vTagNan -> vBooleanTrue), booleanFalse: (vTagNan -> vBooleanFalse), integerOne: (vTagNan -> vIntegerOne), integerZero: (vTagNan -> vIntegerZero), floatPositive: (vTagNan -> vFloatPositive), percentagePositive: (vTagNan -> vPercentagePositive), percentageZero: (vTagNan -> vPercentageZero), textTrue: (vTagNan -> vTextTrue), textTrueUpper: (vTagNan -> vTextTrueUpper), textFalse: (vTagNan -> vTextFalse), textOne: (vTagNan -> vTextOne), textZero: (vTagNan -> vTextZero), textInvalid: (vTagNan -> vTextInvalid), textEmpty: (vTagNan -> vTextEmpty), tagTrue: (vTagNan -> vTagTrue), tagFalse: (vTagNan -> vTagFalse), tagTrueUpper: (vTagNan -> vTagTrueUpper), tagPi: (vTagNan -> vTagPi), tagPhi: (vTagNan -> vTagPhi), tagInfinity: (vTagNan -> vTagInfinity), tagNan: (vTagNan -> vTagNan), tagCustom: (vTagNan -> vTagCustom), vector: (vTagNan -> vVector), vectorZero: (vTagNan -> vVectorZero), point: (vTagNan -> vPoint), pointZero: (vTagNan -> vPointZero), list: (vTagNan -> vList), map: (vTagNan -> vMap), dice: (vTagNan -> vDice))
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

## Test: implies tagCustom

This runtime case exercises “implies tagCustom” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0114
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies tagCustom.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicDJ
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
  emit Done(nothing: (vTagCustom -> vNothing), booleanTrue: (vTagCustom -> vBooleanTrue), booleanFalse: (vTagCustom -> vBooleanFalse), integerOne: (vTagCustom -> vIntegerOne), integerZero: (vTagCustom -> vIntegerZero), floatPositive: (vTagCustom -> vFloatPositive), percentagePositive: (vTagCustom -> vPercentagePositive), percentageZero: (vTagCustom -> vPercentageZero), textTrue: (vTagCustom -> vTextTrue), textTrueUpper: (vTagCustom -> vTextTrueUpper), textFalse: (vTagCustom -> vTextFalse), textOne: (vTagCustom -> vTextOne), textZero: (vTagCustom -> vTextZero), textInvalid: (vTagCustom -> vTextInvalid), textEmpty: (vTagCustom -> vTextEmpty), tagTrue: (vTagCustom -> vTagTrue), tagFalse: (vTagCustom -> vTagFalse), tagTrueUpper: (vTagCustom -> vTagTrueUpper), tagPi: (vTagCustom -> vTagPi), tagPhi: (vTagCustom -> vTagPhi), tagInfinity: (vTagCustom -> vTagInfinity), tagNan: (vTagCustom -> vTagNan), tagCustom: (vTagCustom -> vTagCustom), vector: (vTagCustom -> vVector), vectorZero: (vTagCustom -> vVectorZero), point: (vTagCustom -> vPoint), pointZero: (vTagCustom -> vPointZero), list: (vTagCustom -> vList), map: (vTagCustom -> vMap), dice: (vTagCustom -> vDice))
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

## Test: implies vector

This runtime case exercises “implies vector” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0115
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies vector.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicDK
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
  emit Done(nothing: (vVector -> vNothing), booleanTrue: (vVector -> vBooleanTrue), booleanFalse: (vVector -> vBooleanFalse), integerOne: (vVector -> vIntegerOne), integerZero: (vVector -> vIntegerZero), floatPositive: (vVector -> vFloatPositive), percentagePositive: (vVector -> vPercentagePositive), percentageZero: (vVector -> vPercentageZero), textTrue: (vVector -> vTextTrue), textTrueUpper: (vVector -> vTextTrueUpper), textFalse: (vVector -> vTextFalse), textOne: (vVector -> vTextOne), textZero: (vVector -> vTextZero), textInvalid: (vVector -> vTextInvalid), textEmpty: (vVector -> vTextEmpty), tagTrue: (vVector -> vTagTrue), tagFalse: (vVector -> vTagFalse), tagTrueUpper: (vVector -> vTagTrueUpper), tagPi: (vVector -> vTagPi), tagPhi: (vVector -> vTagPhi), tagInfinity: (vVector -> vTagInfinity), tagNan: (vVector -> vTagNan), tagCustom: (vVector -> vTagCustom), vector: (vVector -> vVector), vectorZero: (vVector -> vVectorZero), point: (vVector -> vPoint), pointZero: (vVector -> vPointZero), list: (vVector -> vList), map: (vVector -> vMap), dice: (vVector -> vDice))
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

## Test: implies vectorZero

This runtime case exercises “implies vectorZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0116
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies vectorZero.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicDL
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
  emit Done(nothing: (vVectorZero -> vNothing), booleanTrue: (vVectorZero -> vBooleanTrue), booleanFalse: (vVectorZero -> vBooleanFalse), integerOne: (vVectorZero -> vIntegerOne), integerZero: (vVectorZero -> vIntegerZero), floatPositive: (vVectorZero -> vFloatPositive), percentagePositive: (vVectorZero -> vPercentagePositive), percentageZero: (vVectorZero -> vPercentageZero), textTrue: (vVectorZero -> vTextTrue), textTrueUpper: (vVectorZero -> vTextTrueUpper), textFalse: (vVectorZero -> vTextFalse), textOne: (vVectorZero -> vTextOne), textZero: (vVectorZero -> vTextZero), textInvalid: (vVectorZero -> vTextInvalid), textEmpty: (vVectorZero -> vTextEmpty), tagTrue: (vVectorZero -> vTagTrue), tagFalse: (vVectorZero -> vTagFalse), tagTrueUpper: (vVectorZero -> vTagTrueUpper), tagPi: (vVectorZero -> vTagPi), tagPhi: (vVectorZero -> vTagPhi), tagInfinity: (vVectorZero -> vTagInfinity), tagNan: (vVectorZero -> vTagNan), tagCustom: (vVectorZero -> vTagCustom), vector: (vVectorZero -> vVector), vectorZero: (vVectorZero -> vVectorZero), point: (vVectorZero -> vPoint), pointZero: (vVectorZero -> vPointZero), list: (vVectorZero -> vList), map: (vVectorZero -> vMap), dice: (vVectorZero -> vDice))
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

## Test: implies point

This runtime case exercises “implies point” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0117
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies point.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicDM
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
  emit Done(nothing: (vPoint -> vNothing), booleanTrue: (vPoint -> vBooleanTrue), booleanFalse: (vPoint -> vBooleanFalse), integerOne: (vPoint -> vIntegerOne), integerZero: (vPoint -> vIntegerZero), floatPositive: (vPoint -> vFloatPositive), percentagePositive: (vPoint -> vPercentagePositive), percentageZero: (vPoint -> vPercentageZero), textTrue: (vPoint -> vTextTrue), textTrueUpper: (vPoint -> vTextTrueUpper), textFalse: (vPoint -> vTextFalse), textOne: (vPoint -> vTextOne), textZero: (vPoint -> vTextZero), textInvalid: (vPoint -> vTextInvalid), textEmpty: (vPoint -> vTextEmpty), tagTrue: (vPoint -> vTagTrue), tagFalse: (vPoint -> vTagFalse), tagTrueUpper: (vPoint -> vTagTrueUpper), tagPi: (vPoint -> vTagPi), tagPhi: (vPoint -> vTagPhi), tagInfinity: (vPoint -> vTagInfinity), tagNan: (vPoint -> vTagNan), tagCustom: (vPoint -> vTagCustom), vector: (vPoint -> vVector), vectorZero: (vPoint -> vVectorZero), point: (vPoint -> vPoint), pointZero: (vPoint -> vPointZero), list: (vPoint -> vList), map: (vPoint -> vMap), dice: (vPoint -> vDice))
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

## Test: implies pointZero

This runtime case exercises “implies pointZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0118
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies pointZero.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicDN
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
  emit Done(nothing: (vPointZero -> vNothing), booleanTrue: (vPointZero -> vBooleanTrue), booleanFalse: (vPointZero -> vBooleanFalse), integerOne: (vPointZero -> vIntegerOne), integerZero: (vPointZero -> vIntegerZero), floatPositive: (vPointZero -> vFloatPositive), percentagePositive: (vPointZero -> vPercentagePositive), percentageZero: (vPointZero -> vPercentageZero), textTrue: (vPointZero -> vTextTrue), textTrueUpper: (vPointZero -> vTextTrueUpper), textFalse: (vPointZero -> vTextFalse), textOne: (vPointZero -> vTextOne), textZero: (vPointZero -> vTextZero), textInvalid: (vPointZero -> vTextInvalid), textEmpty: (vPointZero -> vTextEmpty), tagTrue: (vPointZero -> vTagTrue), tagFalse: (vPointZero -> vTagFalse), tagTrueUpper: (vPointZero -> vTagTrueUpper), tagPi: (vPointZero -> vTagPi), tagPhi: (vPointZero -> vTagPhi), tagInfinity: (vPointZero -> vTagInfinity), tagNan: (vPointZero -> vTagNan), tagCustom: (vPointZero -> vTagCustom), vector: (vPointZero -> vVector), vectorZero: (vPointZero -> vVectorZero), point: (vPointZero -> vPoint), pointZero: (vPointZero -> vPointZero), list: (vPointZero -> vList), map: (vPointZero -> vMap), dice: (vPointZero -> vDice))
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

## Test: implies list

This runtime case exercises “implies list” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0119
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies list.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicDO
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
  emit Done(nothing: (vList -> vNothing), booleanTrue: (vList -> vBooleanTrue), booleanFalse: (vList -> vBooleanFalse), integerOne: (vList -> vIntegerOne), integerZero: (vList -> vIntegerZero), floatPositive: (vList -> vFloatPositive), percentagePositive: (vList -> vPercentagePositive), percentageZero: (vList -> vPercentageZero), textTrue: (vList -> vTextTrue), textTrueUpper: (vList -> vTextTrueUpper), textFalse: (vList -> vTextFalse), textOne: (vList -> vTextOne), textZero: (vList -> vTextZero), textInvalid: (vList -> vTextInvalid), textEmpty: (vList -> vTextEmpty), tagTrue: (vList -> vTagTrue), tagFalse: (vList -> vTagFalse), tagTrueUpper: (vList -> vTagTrueUpper), tagPi: (vList -> vTagPi), tagPhi: (vList -> vTagPhi), tagInfinity: (vList -> vTagInfinity), tagNan: (vList -> vTagNan), tagCustom: (vList -> vTagCustom), vector: (vList -> vVector), vectorZero: (vList -> vVectorZero), point: (vList -> vPoint), pointZero: (vList -> vPointZero), list: (vList -> vList), map: (vList -> vMap), dice: (vList -> vDice))
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

## Test: implies map

This runtime case exercises “implies map” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0120
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies map.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicDP
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
  emit Done(nothing: (vMap -> vNothing), booleanTrue: (vMap -> vBooleanTrue), booleanFalse: (vMap -> vBooleanFalse), integerOne: (vMap -> vIntegerOne), integerZero: (vMap -> vIntegerZero), floatPositive: (vMap -> vFloatPositive), percentagePositive: (vMap -> vPercentagePositive), percentageZero: (vMap -> vPercentageZero), textTrue: (vMap -> vTextTrue), textTrueUpper: (vMap -> vTextTrueUpper), textFalse: (vMap -> vTextFalse), textOne: (vMap -> vTextOne), textZero: (vMap -> vTextZero), textInvalid: (vMap -> vTextInvalid), textEmpty: (vMap -> vTextEmpty), tagTrue: (vMap -> vTagTrue), tagFalse: (vMap -> vTagFalse), tagTrueUpper: (vMap -> vTagTrueUpper), tagPi: (vMap -> vTagPi), tagPhi: (vMap -> vTagPhi), tagInfinity: (vMap -> vTagInfinity), tagNan: (vMap -> vTagNan), tagCustom: (vMap -> vTagCustom), vector: (vMap -> vVector), vectorZero: (vMap -> vVectorZero), point: (vMap -> vPoint), pointZero: (vMap -> vPointZero), list: (vMap -> vList), map: (vMap -> vMap), dice: (vMap -> vDice))
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

## Test: implies dice

This runtime case exercises “implies dice” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0121
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies dice.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicDQ
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
  emit Done(nothing: (vDice -> vNothing), booleanTrue: (vDice -> vBooleanTrue), booleanFalse: (vDice -> vBooleanFalse), integerOne: (vDice -> vIntegerOne), integerZero: (vDice -> vIntegerZero), floatPositive: (vDice -> vFloatPositive), percentagePositive: (vDice -> vPercentagePositive), percentageZero: (vDice -> vPercentageZero), textTrue: (vDice -> vTextTrue), textTrueUpper: (vDice -> vTextTrueUpper), textFalse: (vDice -> vTextFalse), textOne: (vDice -> vTextOne), textZero: (vDice -> vTextZero), textInvalid: (vDice -> vTextInvalid), textEmpty: (vDice -> vTextEmpty), tagTrue: (vDice -> vTagTrue), tagFalse: (vDice -> vTagFalse), tagTrueUpper: (vDice -> vTagTrueUpper), tagPi: (vDice -> vTagPi), tagPhi: (vDice -> vTagPhi), tagInfinity: (vDice -> vTagInfinity), tagNan: (vDice -> vTagNan), tagCustom: (vDice -> vTagCustom), vector: (vDice -> vVector), vectorZero: (vDice -> vVectorZero), point: (vDice -> vPoint), pointZero: (vDice -> vPointZero), list: (vDice -> vList), map: (vDice -> vMap), dice: (vDice -> vDice))
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
