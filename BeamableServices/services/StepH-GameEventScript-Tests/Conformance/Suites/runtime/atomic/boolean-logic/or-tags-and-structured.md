---
formatVersion: 1
suiteId: "runtime.atomic.boolean-logic.or-tags-and-structured"
title: "Boolean Logic — Or Tags and Structured Values"
categories: [conformance]
---

# Boolean Logic — Or Tags and Structured Values

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers or for tags, spatial values, and collections.

---

## Test: or tagTrue

This runtime case exercises “or tagTrue” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0047
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or tagTrue.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAU
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
  emit Done(nothing: (vTagTrue or vNothing), booleanTrue: (vTagTrue or vBooleanTrue), booleanFalse: (vTagTrue or vBooleanFalse), integerOne: (vTagTrue or vIntegerOne), integerZero: (vTagTrue or vIntegerZero), floatPositive: (vTagTrue or vFloatPositive), percentagePositive: (vTagTrue or vPercentagePositive), percentageZero: (vTagTrue or vPercentageZero), textTrue: (vTagTrue or vTextTrue), textTrueUpper: (vTagTrue or vTextTrueUpper), textFalse: (vTagTrue or vTextFalse), textOne: (vTagTrue or vTextOne), textZero: (vTagTrue or vTextZero), textInvalid: (vTagTrue or vTextInvalid), textEmpty: (vTagTrue or vTextEmpty), tagTrue: (vTagTrue or vTagTrue), tagFalse: (vTagTrue or vTagFalse), tagTrueUpper: (vTagTrue or vTagTrueUpper), tagPi: (vTagTrue or vTagPi), tagPhi: (vTagTrue or vTagPhi), tagInfinity: (vTagTrue or vTagInfinity), tagNan: (vTagTrue or vTagNan), tagCustom: (vTagTrue or vTagCustom), vector: (vTagTrue or vVector), vectorZero: (vTagTrue or vVectorZero), point: (vTagTrue or vPoint), pointZero: (vTagTrue or vPointZero), list: (vTagTrue or vList), map: (vTagTrue or vMap), dice: (vTagTrue or vDice))
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

## Test: or tagFalse

This runtime case exercises “or tagFalse” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0048
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or tagFalse.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAV
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
  emit Done(nothing: (vTagFalse or vNothing), booleanTrue: (vTagFalse or vBooleanTrue), booleanFalse: (vTagFalse or vBooleanFalse), integerOne: (vTagFalse or vIntegerOne), integerZero: (vTagFalse or vIntegerZero), floatPositive: (vTagFalse or vFloatPositive), percentagePositive: (vTagFalse or vPercentagePositive), percentageZero: (vTagFalse or vPercentageZero), textTrue: (vTagFalse or vTextTrue), textTrueUpper: (vTagFalse or vTextTrueUpper), textFalse: (vTagFalse or vTextFalse), textOne: (vTagFalse or vTextOne), textZero: (vTagFalse or vTextZero), textInvalid: (vTagFalse or vTextInvalid), textEmpty: (vTagFalse or vTextEmpty), tagTrue: (vTagFalse or vTagTrue), tagFalse: (vTagFalse or vTagFalse), tagTrueUpper: (vTagFalse or vTagTrueUpper), tagPi: (vTagFalse or vTagPi), tagPhi: (vTagFalse or vTagPhi), tagInfinity: (vTagFalse or vTagInfinity), tagNan: (vTagFalse or vTagNan), tagCustom: (vTagFalse or vTagCustom), vector: (vTagFalse or vVector), vectorZero: (vTagFalse or vVectorZero), point: (vTagFalse or vPoint), pointZero: (vTagFalse or vPointZero), list: (vTagFalse or vList), map: (vTagFalse or vMap), dice: (vTagFalse or vDice))
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

## Test: or tagTrueUpper

This runtime case exercises “or tagTrueUpper” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0049
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or tagTrueUpper.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAW
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
  emit Done(nothing: (vTagTrueUpper or vNothing), booleanTrue: (vTagTrueUpper or vBooleanTrue), booleanFalse: (vTagTrueUpper or vBooleanFalse), integerOne: (vTagTrueUpper or vIntegerOne), integerZero: (vTagTrueUpper or vIntegerZero), floatPositive: (vTagTrueUpper or vFloatPositive), percentagePositive: (vTagTrueUpper or vPercentagePositive), percentageZero: (vTagTrueUpper or vPercentageZero), textTrue: (vTagTrueUpper or vTextTrue), textTrueUpper: (vTagTrueUpper or vTextTrueUpper), textFalse: (vTagTrueUpper or vTextFalse), textOne: (vTagTrueUpper or vTextOne), textZero: (vTagTrueUpper or vTextZero), textInvalid: (vTagTrueUpper or vTextInvalid), textEmpty: (vTagTrueUpper or vTextEmpty), tagTrue: (vTagTrueUpper or vTagTrue), tagFalse: (vTagTrueUpper or vTagFalse), tagTrueUpper: (vTagTrueUpper or vTagTrueUpper), tagPi: (vTagTrueUpper or vTagPi), tagPhi: (vTagTrueUpper or vTagPhi), tagInfinity: (vTagTrueUpper or vTagInfinity), tagNan: (vTagTrueUpper or vTagNan), tagCustom: (vTagTrueUpper or vTagCustom), vector: (vTagTrueUpper or vVector), vectorZero: (vTagTrueUpper or vVectorZero), point: (vTagTrueUpper or vPoint), pointZero: (vTagTrueUpper or vPointZero), list: (vTagTrueUpper or vList), map: (vTagTrueUpper or vMap), dice: (vTagTrueUpper or vDice))
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

## Test: or tagPi

This runtime case exercises “or tagPi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0050
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or tagPi.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAX
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
  emit Done(nothing: (vTagPi or vNothing), booleanTrue: (vTagPi or vBooleanTrue), booleanFalse: (vTagPi or vBooleanFalse), integerOne: (vTagPi or vIntegerOne), integerZero: (vTagPi or vIntegerZero), floatPositive: (vTagPi or vFloatPositive), percentagePositive: (vTagPi or vPercentagePositive), percentageZero: (vTagPi or vPercentageZero), textTrue: (vTagPi or vTextTrue), textTrueUpper: (vTagPi or vTextTrueUpper), textFalse: (vTagPi or vTextFalse), textOne: (vTagPi or vTextOne), textZero: (vTagPi or vTextZero), textInvalid: (vTagPi or vTextInvalid), textEmpty: (vTagPi or vTextEmpty), tagTrue: (vTagPi or vTagTrue), tagFalse: (vTagPi or vTagFalse), tagTrueUpper: (vTagPi or vTagTrueUpper), tagPi: (vTagPi or vTagPi), tagPhi: (vTagPi or vTagPhi), tagInfinity: (vTagPi or vTagInfinity), tagNan: (vTagPi or vTagNan), tagCustom: (vTagPi or vTagCustom), vector: (vTagPi or vVector), vectorZero: (vTagPi or vVectorZero), point: (vTagPi or vPoint), pointZero: (vTagPi or vPointZero), list: (vTagPi or vList), map: (vTagPi or vMap), dice: (vTagPi or vDice))
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

## Test: or tagPhi

This runtime case exercises “or tagPhi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0051
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or tagPhi.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAY
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
  emit Done(nothing: (vTagPhi or vNothing), booleanTrue: (vTagPhi or vBooleanTrue), booleanFalse: (vTagPhi or vBooleanFalse), integerOne: (vTagPhi or vIntegerOne), integerZero: (vTagPhi or vIntegerZero), floatPositive: (vTagPhi or vFloatPositive), percentagePositive: (vTagPhi or vPercentagePositive), percentageZero: (vTagPhi or vPercentageZero), textTrue: (vTagPhi or vTextTrue), textTrueUpper: (vTagPhi or vTextTrueUpper), textFalse: (vTagPhi or vTextFalse), textOne: (vTagPhi or vTextOne), textZero: (vTagPhi or vTextZero), textInvalid: (vTagPhi or vTextInvalid), textEmpty: (vTagPhi or vTextEmpty), tagTrue: (vTagPhi or vTagTrue), tagFalse: (vTagPhi or vTagFalse), tagTrueUpper: (vTagPhi or vTagTrueUpper), tagPi: (vTagPhi or vTagPi), tagPhi: (vTagPhi or vTagPhi), tagInfinity: (vTagPhi or vTagInfinity), tagNan: (vTagPhi or vTagNan), tagCustom: (vTagPhi or vTagCustom), vector: (vTagPhi or vVector), vectorZero: (vTagPhi or vVectorZero), point: (vTagPhi or vPoint), pointZero: (vTagPhi or vPointZero), list: (vTagPhi or vList), map: (vTagPhi or vMap), dice: (vTagPhi or vDice))
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

## Test: or tagInfinity

This runtime case exercises “or tagInfinity” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0052
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or tagInfinity.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicAZ
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
  emit Done(nothing: (vTagInfinity or vNothing), booleanTrue: (vTagInfinity or vBooleanTrue), booleanFalse: (vTagInfinity or vBooleanFalse), integerOne: (vTagInfinity or vIntegerOne), integerZero: (vTagInfinity or vIntegerZero), floatPositive: (vTagInfinity or vFloatPositive), percentagePositive: (vTagInfinity or vPercentagePositive), percentageZero: (vTagInfinity or vPercentageZero), textTrue: (vTagInfinity or vTextTrue), textTrueUpper: (vTagInfinity or vTextTrueUpper), textFalse: (vTagInfinity or vTextFalse), textOne: (vTagInfinity or vTextOne), textZero: (vTagInfinity or vTextZero), textInvalid: (vTagInfinity or vTextInvalid), textEmpty: (vTagInfinity or vTextEmpty), tagTrue: (vTagInfinity or vTagTrue), tagFalse: (vTagInfinity or vTagFalse), tagTrueUpper: (vTagInfinity or vTagTrueUpper), tagPi: (vTagInfinity or vTagPi), tagPhi: (vTagInfinity or vTagPhi), tagInfinity: (vTagInfinity or vTagInfinity), tagNan: (vTagInfinity or vTagNan), tagCustom: (vTagInfinity or vTagCustom), vector: (vTagInfinity or vVector), vectorZero: (vTagInfinity or vVectorZero), point: (vTagInfinity or vPoint), pointZero: (vTagInfinity or vPointZero), list: (vTagInfinity or vList), map: (vTagInfinity or vMap), dice: (vTagInfinity or vDice))
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

## Test: or tagNan

This runtime case exercises “or tagNan” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0053
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or tagNan.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBA
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
  emit Done(nothing: (vTagNan or vNothing), booleanTrue: (vTagNan or vBooleanTrue), booleanFalse: (vTagNan or vBooleanFalse), integerOne: (vTagNan or vIntegerOne), integerZero: (vTagNan or vIntegerZero), floatPositive: (vTagNan or vFloatPositive), percentagePositive: (vTagNan or vPercentagePositive), percentageZero: (vTagNan or vPercentageZero), textTrue: (vTagNan or vTextTrue), textTrueUpper: (vTagNan or vTextTrueUpper), textFalse: (vTagNan or vTextFalse), textOne: (vTagNan or vTextOne), textZero: (vTagNan or vTextZero), textInvalid: (vTagNan or vTextInvalid), textEmpty: (vTagNan or vTextEmpty), tagTrue: (vTagNan or vTagTrue), tagFalse: (vTagNan or vTagFalse), tagTrueUpper: (vTagNan or vTagTrueUpper), tagPi: (vTagNan or vTagPi), tagPhi: (vTagNan or vTagPhi), tagInfinity: (vTagNan or vTagInfinity), tagNan: (vTagNan or vTagNan), tagCustom: (vTagNan or vTagCustom), vector: (vTagNan or vVector), vectorZero: (vTagNan or vVectorZero), point: (vTagNan or vPoint), pointZero: (vTagNan or vPointZero), list: (vTagNan or vList), map: (vTagNan or vMap), dice: (vTagNan or vDice))
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

## Test: or tagCustom

This runtime case exercises “or tagCustom” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0054
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or tagCustom.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBB
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
  emit Done(nothing: (vTagCustom or vNothing), booleanTrue: (vTagCustom or vBooleanTrue), booleanFalse: (vTagCustom or vBooleanFalse), integerOne: (vTagCustom or vIntegerOne), integerZero: (vTagCustom or vIntegerZero), floatPositive: (vTagCustom or vFloatPositive), percentagePositive: (vTagCustom or vPercentagePositive), percentageZero: (vTagCustom or vPercentageZero), textTrue: (vTagCustom or vTextTrue), textTrueUpper: (vTagCustom or vTextTrueUpper), textFalse: (vTagCustom or vTextFalse), textOne: (vTagCustom or vTextOne), textZero: (vTagCustom or vTextZero), textInvalid: (vTagCustom or vTextInvalid), textEmpty: (vTagCustom or vTextEmpty), tagTrue: (vTagCustom or vTagTrue), tagFalse: (vTagCustom or vTagFalse), tagTrueUpper: (vTagCustom or vTagTrueUpper), tagPi: (vTagCustom or vTagPi), tagPhi: (vTagCustom or vTagPhi), tagInfinity: (vTagCustom or vTagInfinity), tagNan: (vTagCustom or vTagNan), tagCustom: (vTagCustom or vTagCustom), vector: (vTagCustom or vVector), vectorZero: (vTagCustom or vVectorZero), point: (vTagCustom or vPoint), pointZero: (vTagCustom or vPointZero), list: (vTagCustom or vList), map: (vTagCustom or vMap), dice: (vTagCustom or vDice))
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

## Test: or vector

This runtime case exercises “or vector” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0055
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or vector.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBC
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
  emit Done(nothing: (vVector or vNothing), booleanTrue: (vVector or vBooleanTrue), booleanFalse: (vVector or vBooleanFalse), integerOne: (vVector or vIntegerOne), integerZero: (vVector or vIntegerZero), floatPositive: (vVector or vFloatPositive), percentagePositive: (vVector or vPercentagePositive), percentageZero: (vVector or vPercentageZero), textTrue: (vVector or vTextTrue), textTrueUpper: (vVector or vTextTrueUpper), textFalse: (vVector or vTextFalse), textOne: (vVector or vTextOne), textZero: (vVector or vTextZero), textInvalid: (vVector or vTextInvalid), textEmpty: (vVector or vTextEmpty), tagTrue: (vVector or vTagTrue), tagFalse: (vVector or vTagFalse), tagTrueUpper: (vVector or vTagTrueUpper), tagPi: (vVector or vTagPi), tagPhi: (vVector or vTagPhi), tagInfinity: (vVector or vTagInfinity), tagNan: (vVector or vTagNan), tagCustom: (vVector or vTagCustom), vector: (vVector or vVector), vectorZero: (vVector or vVectorZero), point: (vVector or vPoint), pointZero: (vVector or vPointZero), list: (vVector or vList), map: (vVector or vMap), dice: (vVector or vDice))
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

## Test: or vectorZero

This runtime case exercises “or vectorZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0056
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or vectorZero.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBD
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
  emit Done(nothing: (vVectorZero or vNothing), booleanTrue: (vVectorZero or vBooleanTrue), booleanFalse: (vVectorZero or vBooleanFalse), integerOne: (vVectorZero or vIntegerOne), integerZero: (vVectorZero or vIntegerZero), floatPositive: (vVectorZero or vFloatPositive), percentagePositive: (vVectorZero or vPercentagePositive), percentageZero: (vVectorZero or vPercentageZero), textTrue: (vVectorZero or vTextTrue), textTrueUpper: (vVectorZero or vTextTrueUpper), textFalse: (vVectorZero or vTextFalse), textOne: (vVectorZero or vTextOne), textZero: (vVectorZero or vTextZero), textInvalid: (vVectorZero or vTextInvalid), textEmpty: (vVectorZero or vTextEmpty), tagTrue: (vVectorZero or vTagTrue), tagFalse: (vVectorZero or vTagFalse), tagTrueUpper: (vVectorZero or vTagTrueUpper), tagPi: (vVectorZero or vTagPi), tagPhi: (vVectorZero or vTagPhi), tagInfinity: (vVectorZero or vTagInfinity), tagNan: (vVectorZero or vTagNan), tagCustom: (vVectorZero or vTagCustom), vector: (vVectorZero or vVector), vectorZero: (vVectorZero or vVectorZero), point: (vVectorZero or vPoint), pointZero: (vVectorZero or vPointZero), list: (vVectorZero or vList), map: (vVectorZero or vMap), dice: (vVectorZero or vDice))
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

## Test: or point

This runtime case exercises “or point” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0057
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or point.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBE
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
  emit Done(nothing: (vPoint or vNothing), booleanTrue: (vPoint or vBooleanTrue), booleanFalse: (vPoint or vBooleanFalse), integerOne: (vPoint or vIntegerOne), integerZero: (vPoint or vIntegerZero), floatPositive: (vPoint or vFloatPositive), percentagePositive: (vPoint or vPercentagePositive), percentageZero: (vPoint or vPercentageZero), textTrue: (vPoint or vTextTrue), textTrueUpper: (vPoint or vTextTrueUpper), textFalse: (vPoint or vTextFalse), textOne: (vPoint or vTextOne), textZero: (vPoint or vTextZero), textInvalid: (vPoint or vTextInvalid), textEmpty: (vPoint or vTextEmpty), tagTrue: (vPoint or vTagTrue), tagFalse: (vPoint or vTagFalse), tagTrueUpper: (vPoint or vTagTrueUpper), tagPi: (vPoint or vTagPi), tagPhi: (vPoint or vTagPhi), tagInfinity: (vPoint or vTagInfinity), tagNan: (vPoint or vTagNan), tagCustom: (vPoint or vTagCustom), vector: (vPoint or vVector), vectorZero: (vPoint or vVectorZero), point: (vPoint or vPoint), pointZero: (vPoint or vPointZero), list: (vPoint or vList), map: (vPoint or vMap), dice: (vPoint or vDice))
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

## Test: or pointZero

This runtime case exercises “or pointZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0058
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or pointZero.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBF
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
  emit Done(nothing: (vPointZero or vNothing), booleanTrue: (vPointZero or vBooleanTrue), booleanFalse: (vPointZero or vBooleanFalse), integerOne: (vPointZero or vIntegerOne), integerZero: (vPointZero or vIntegerZero), floatPositive: (vPointZero or vFloatPositive), percentagePositive: (vPointZero or vPercentagePositive), percentageZero: (vPointZero or vPercentageZero), textTrue: (vPointZero or vTextTrue), textTrueUpper: (vPointZero or vTextTrueUpper), textFalse: (vPointZero or vTextFalse), textOne: (vPointZero or vTextOne), textZero: (vPointZero or vTextZero), textInvalid: (vPointZero or vTextInvalid), textEmpty: (vPointZero or vTextEmpty), tagTrue: (vPointZero or vTagTrue), tagFalse: (vPointZero or vTagFalse), tagTrueUpper: (vPointZero or vTagTrueUpper), tagPi: (vPointZero or vTagPi), tagPhi: (vPointZero or vTagPhi), tagInfinity: (vPointZero or vTagInfinity), tagNan: (vPointZero or vTagNan), tagCustom: (vPointZero or vTagCustom), vector: (vPointZero or vVector), vectorZero: (vPointZero or vVectorZero), point: (vPointZero or vPoint), pointZero: (vPointZero or vPointZero), list: (vPointZero or vList), map: (vPointZero or vMap), dice: (vPointZero or vDice))
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

## Test: or list

This runtime case exercises “or list” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0059
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or list.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBG
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
  emit Done(nothing: (vList or vNothing), booleanTrue: (vList or vBooleanTrue), booleanFalse: (vList or vBooleanFalse), integerOne: (vList or vIntegerOne), integerZero: (vList or vIntegerZero), floatPositive: (vList or vFloatPositive), percentagePositive: (vList or vPercentagePositive), percentageZero: (vList or vPercentageZero), textTrue: (vList or vTextTrue), textTrueUpper: (vList or vTextTrueUpper), textFalse: (vList or vTextFalse), textOne: (vList or vTextOne), textZero: (vList or vTextZero), textInvalid: (vList or vTextInvalid), textEmpty: (vList or vTextEmpty), tagTrue: (vList or vTagTrue), tagFalse: (vList or vTagFalse), tagTrueUpper: (vList or vTagTrueUpper), tagPi: (vList or vTagPi), tagPhi: (vList or vTagPhi), tagInfinity: (vList or vTagInfinity), tagNan: (vList or vTagNan), tagCustom: (vList or vTagCustom), vector: (vList or vVector), vectorZero: (vList or vVectorZero), point: (vList or vPoint), pointZero: (vList or vPointZero), list: (vList or vList), map: (vList or vMap), dice: (vList or vDice))
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

## Test: or map

This runtime case exercises “or map” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0060
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or map.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBH
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
  emit Done(nothing: (vMap or vNothing), booleanTrue: (vMap or vBooleanTrue), booleanFalse: (vMap or vBooleanFalse), integerOne: (vMap or vIntegerOne), integerZero: (vMap or vIntegerZero), floatPositive: (vMap or vFloatPositive), percentagePositive: (vMap or vPercentagePositive), percentageZero: (vMap or vPercentageZero), textTrue: (vMap or vTextTrue), textTrueUpper: (vMap or vTextTrueUpper), textFalse: (vMap or vTextFalse), textOne: (vMap or vTextOne), textZero: (vMap or vTextZero), textInvalid: (vMap or vTextInvalid), textEmpty: (vMap or vTextEmpty), tagTrue: (vMap or vTagTrue), tagFalse: (vMap or vTagFalse), tagTrueUpper: (vMap or vTagTrueUpper), tagPi: (vMap or vTagPi), tagPhi: (vMap or vTagPhi), tagInfinity: (vMap or vTagInfinity), tagNan: (vMap or vTagNan), tagCustom: (vMap or vTagCustom), vector: (vMap or vVector), vectorZero: (vMap or vVectorZero), point: (vMap or vPoint), pointZero: (vMap or vPointZero), list: (vMap or vList), map: (vMap or vMap), dice: (vMap or vDice))
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

## Test: or dice

This runtime case exercises “or dice” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0061
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "or dice.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicBI
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
  emit Done(nothing: (vDice or vNothing), booleanTrue: (vDice or vBooleanTrue), booleanFalse: (vDice or vBooleanFalse), integerOne: (vDice or vIntegerOne), integerZero: (vDice or vIntegerZero), floatPositive: (vDice or vFloatPositive), percentagePositive: (vDice or vPercentagePositive), percentageZero: (vDice or vPercentageZero), textTrue: (vDice or vTextTrue), textTrueUpper: (vDice or vTextTrueUpper), textFalse: (vDice or vTextFalse), textOne: (vDice or vTextOne), textZero: (vDice or vTextZero), textInvalid: (vDice or vTextInvalid), textEmpty: (vDice or vTextEmpty), tagTrue: (vDice or vTagTrue), tagFalse: (vDice or vTagFalse), tagTrueUpper: (vDice or vTagTrueUpper), tagPi: (vDice or vTagPi), tagPhi: (vDice or vTagPhi), tagInfinity: (vDice or vTagInfinity), tagNan: (vDice or vTagNan), tagCustom: (vDice or vTagCustom), vector: (vDice or vVector), vectorZero: (vDice or vVectorZero), point: (vDice or vPoint), pointZero: (vDice or vPointZero), list: (vDice or vList), map: (vDice or vMap), dice: (vDice or vDice))
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
