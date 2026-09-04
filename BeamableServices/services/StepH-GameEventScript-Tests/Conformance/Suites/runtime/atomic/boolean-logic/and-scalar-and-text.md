---
formatVersion: 1
suiteId: "runtime.atomic.boolean-logic.and-scalar-and-text"
title: "Boolean Logic — And Scalars and Text"
categories: [conformance]
---

# Boolean Logic — And Scalars and Text

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers and for nothing, boolean, numeric, percentage, and text operands.

---

## Test: and nothing

This runtime case exercises “and nothing” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0002
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and nothing.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicb
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
  let vTagTrueUpper be ('True') as :Tag
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :Vector(1, 2, 3)
  let vVectorZero be :Vector(0, 0, 0)
  let vPoint be :Point(1, 2, 3)
  let vPointZero be :Point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice be ([6, 2, 4]) as :Dice
  emit Done(nothing: (vNothing and vNothing), booleanTrue: (vNothing and vBooleanTrue), booleanFalse: (vNothing and vBooleanFalse), integerOne: (vNothing and vIntegerOne), integerZero: (vNothing and vIntegerZero), floatPositive: (vNothing and vFloatPositive), percentagePositive: (vNothing and vPercentagePositive), percentageZero: (vNothing and vPercentageZero), textTrue: (vNothing and vTextTrue), textTrueUpper: (vNothing and vTextTrueUpper), textFalse: (vNothing and vTextFalse), textOne: (vNothing and vTextOne), textZero: (vNothing and vTextZero), textInvalid: (vNothing and vTextInvalid), textEmpty: (vNothing and vTextEmpty), tagTrue: (vNothing and vTagTrue), tagFalse: (vNothing and vTagFalse), tagTrueUpper: (vNothing and vTagTrueUpper), tagPi: (vNothing and vTagPi), tagPhi: (vNothing and vTagPhi), tagInfinity: (vNothing and vTagInfinity), tagNan: (vNothing and vTagNan), tagCustom: (vNothing and vTagCustom), vector: (vNothing and vVector), vectorZero: (vNothing and vVectorZero), point: (vNothing and vPoint), pointZero: (vNothing and vPointZero), list: (vNothing and vList), map: (vNothing and vMap), dice: (vNothing and vDice))
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
              type: ":Nothing"
          - name: "booleanTrue"
            value:
              type: ":Nothing"
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":Nothing"
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":Nothing"
          - name: "percentagePositive"
            value:
              type: ":Nothing"
          - name: "percentageZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":Nothing"
          - name: "textTrueUpper"
            value:
              type: ":Nothing"
          - name: "textFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "textOne"
            value:
              type: ":Nothing"
          - name: "textZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":Nothing"
          - name: "tagPhi"
            value:
              type: ":Nothing"
          - name: "tagInfinity"
            value:
              type: ":Nothing"
          - name: "tagNan"
            value:
              type: ":Boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Nothing"
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Nothing"
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Nothing"
          - name: "map"
            value:
              type: ":Nothing"
          - name: "dice"
            value:
              type: ":Nothing"
```

---

## Test: and booleanTrue

This runtime case exercises “and booleanTrue” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0003
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and booleanTrue.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicc
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
  let vTagTrueUpper be ('True') as :Tag
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :Vector(1, 2, 3)
  let vVectorZero be :Vector(0, 0, 0)
  let vPoint be :Point(1, 2, 3)
  let vPointZero be :Point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice be ([6, 2, 4]) as :Dice
  emit Done(nothing: (vBooleanTrue and vNothing), booleanTrue: (vBooleanTrue and vBooleanTrue), booleanFalse: (vBooleanTrue and vBooleanFalse), integerOne: (vBooleanTrue and vIntegerOne), integerZero: (vBooleanTrue and vIntegerZero), floatPositive: (vBooleanTrue and vFloatPositive), percentagePositive: (vBooleanTrue and vPercentagePositive), percentageZero: (vBooleanTrue and vPercentageZero), textTrue: (vBooleanTrue and vTextTrue), textTrueUpper: (vBooleanTrue and vTextTrueUpper), textFalse: (vBooleanTrue and vTextFalse), textOne: (vBooleanTrue and vTextOne), textZero: (vBooleanTrue and vTextZero), textInvalid: (vBooleanTrue and vTextInvalid), textEmpty: (vBooleanTrue and vTextEmpty), tagTrue: (vBooleanTrue and vTagTrue), tagFalse: (vBooleanTrue and vTagFalse), tagTrueUpper: (vBooleanTrue and vTagTrueUpper), tagPi: (vBooleanTrue and vTagPi), tagPhi: (vBooleanTrue and vTagPhi), tagInfinity: (vBooleanTrue and vTagInfinity), tagNan: (vBooleanTrue and vTagNan), tagCustom: (vBooleanTrue and vTagCustom), vector: (vBooleanTrue and vVector), vectorZero: (vBooleanTrue and vVectorZero), point: (vBooleanTrue and vPoint), pointZero: (vBooleanTrue and vPointZero), list: (vBooleanTrue and vList), map: (vBooleanTrue and vMap), dice: (vBooleanTrue and vDice))
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
              type: ":Nothing"
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":Boolean"
              value: true
          - name: "percentagePositive"
            value:
              type: ":Boolean"
              value: true
          - name: "percentageZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "textTrueUpper"
            value:
              type: ":Boolean"
              value: true
          - name: "textFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "textOne"
            value:
              type: ":Boolean"
              value: true
          - name: "textZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":Boolean"
              value: true
          - name: "tagPhi"
            value:
              type: ":Boolean"
              value: true
          - name: "tagInfinity"
            value:
              type: ":Boolean"
              value: true
          - name: "tagNan"
            value:
              type: ":Boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Nothing"
          - name: "map"
            value:
              type: ":Nothing"
          - name: "dice"
            value:
              type: ":Nothing"
```

---

## Test: and booleanFalse

This runtime case exercises “and booleanFalse” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0004
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and booleanFalse.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicd
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
  let vTagTrueUpper be ('True') as :Tag
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :Vector(1, 2, 3)
  let vVectorZero be :Vector(0, 0, 0)
  let vPoint be :Point(1, 2, 3)
  let vPointZero be :Point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice be ([6, 2, 4]) as :Dice
  emit Done(nothing: (vBooleanFalse and vNothing), booleanTrue: (vBooleanFalse and vBooleanTrue), booleanFalse: (vBooleanFalse and vBooleanFalse), integerOne: (vBooleanFalse and vIntegerOne), integerZero: (vBooleanFalse and vIntegerZero), floatPositive: (vBooleanFalse and vFloatPositive), percentagePositive: (vBooleanFalse and vPercentagePositive), percentageZero: (vBooleanFalse and vPercentageZero), textTrue: (vBooleanFalse and vTextTrue), textTrueUpper: (vBooleanFalse and vTextTrueUpper), textFalse: (vBooleanFalse and vTextFalse), textOne: (vBooleanFalse and vTextOne), textZero: (vBooleanFalse and vTextZero), textInvalid: (vBooleanFalse and vTextInvalid), textEmpty: (vBooleanFalse and vTextEmpty), tagTrue: (vBooleanFalse and vTagTrue), tagFalse: (vBooleanFalse and vTagFalse), tagTrueUpper: (vBooleanFalse and vTagTrueUpper), tagPi: (vBooleanFalse and vTagPi), tagPhi: (vBooleanFalse and vTagPhi), tagInfinity: (vBooleanFalse and vTagInfinity), tagNan: (vBooleanFalse and vTagNan), tagCustom: (vBooleanFalse and vTagCustom), vector: (vBooleanFalse and vVector), vectorZero: (vBooleanFalse and vVectorZero), point: (vBooleanFalse and vPoint), pointZero: (vBooleanFalse and vPointZero), list: (vBooleanFalse and vList), map: (vBooleanFalse and vMap), dice: (vBooleanFalse and vDice))
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
              type: ":Boolean"
              value: false
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":Boolean"
              value: false
          - name: "percentagePositive"
            value:
              type: ":Boolean"
              value: false
          - name: "percentageZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "textFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "textOne"
            value:
              type: ":Boolean"
              value: false
          - name: "textZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPhi"
            value:
              type: ":Boolean"
              value: false
          - name: "tagInfinity"
            value:
              type: ":Boolean"
              value: false
          - name: "tagNan"
            value:
              type: ":Boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: and integerOne

This runtime case exercises “and integerOne” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0005
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and integerOne.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogice
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
  let vTagTrueUpper be ('True') as :Tag
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :Vector(1, 2, 3)
  let vVectorZero be :Vector(0, 0, 0)
  let vPoint be :Point(1, 2, 3)
  let vPointZero be :Point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice be ([6, 2, 4]) as :Dice
  emit Done(nothing: (vIntegerOne and vNothing), booleanTrue: (vIntegerOne and vBooleanTrue), booleanFalse: (vIntegerOne and vBooleanFalse), integerOne: (vIntegerOne and vIntegerOne), integerZero: (vIntegerOne and vIntegerZero), floatPositive: (vIntegerOne and vFloatPositive), percentagePositive: (vIntegerOne and vPercentagePositive), percentageZero: (vIntegerOne and vPercentageZero), textTrue: (vIntegerOne and vTextTrue), textTrueUpper: (vIntegerOne and vTextTrueUpper), textFalse: (vIntegerOne and vTextFalse), textOne: (vIntegerOne and vTextOne), textZero: (vIntegerOne and vTextZero), textInvalid: (vIntegerOne and vTextInvalid), textEmpty: (vIntegerOne and vTextEmpty), tagTrue: (vIntegerOne and vTagTrue), tagFalse: (vIntegerOne and vTagFalse), tagTrueUpper: (vIntegerOne and vTagTrueUpper), tagPi: (vIntegerOne and vTagPi), tagPhi: (vIntegerOne and vTagPhi), tagInfinity: (vIntegerOne and vTagInfinity), tagNan: (vIntegerOne and vTagNan), tagCustom: (vIntegerOne and vTagCustom), vector: (vIntegerOne and vVector), vectorZero: (vIntegerOne and vVectorZero), point: (vIntegerOne and vPoint), pointZero: (vIntegerOne and vPointZero), list: (vIntegerOne and vList), map: (vIntegerOne and vMap), dice: (vIntegerOne and vDice))
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
              type: ":Nothing"
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":Boolean"
              value: true
          - name: "percentagePositive"
            value:
              type: ":Boolean"
              value: true
          - name: "percentageZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "textTrueUpper"
            value:
              type: ":Boolean"
              value: true
          - name: "textFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "textOne"
            value:
              type: ":Boolean"
              value: true
          - name: "textZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":Boolean"
              value: true
          - name: "tagPhi"
            value:
              type: ":Boolean"
              value: true
          - name: "tagInfinity"
            value:
              type: ":Boolean"
              value: true
          - name: "tagNan"
            value:
              type: ":Boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Nothing"
          - name: "map"
            value:
              type: ":Nothing"
          - name: "dice"
            value:
              type: ":Nothing"
```

---

## Test: and integerZero

This runtime case exercises “and integerZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0006
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and integerZero.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicf
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
  let vTagTrueUpper be ('True') as :Tag
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :Vector(1, 2, 3)
  let vVectorZero be :Vector(0, 0, 0)
  let vPoint be :Point(1, 2, 3)
  let vPointZero be :Point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice be ([6, 2, 4]) as :Dice
  emit Done(nothing: (vIntegerZero and vNothing), booleanTrue: (vIntegerZero and vBooleanTrue), booleanFalse: (vIntegerZero and vBooleanFalse), integerOne: (vIntegerZero and vIntegerOne), integerZero: (vIntegerZero and vIntegerZero), floatPositive: (vIntegerZero and vFloatPositive), percentagePositive: (vIntegerZero and vPercentagePositive), percentageZero: (vIntegerZero and vPercentageZero), textTrue: (vIntegerZero and vTextTrue), textTrueUpper: (vIntegerZero and vTextTrueUpper), textFalse: (vIntegerZero and vTextFalse), textOne: (vIntegerZero and vTextOne), textZero: (vIntegerZero and vTextZero), textInvalid: (vIntegerZero and vTextInvalid), textEmpty: (vIntegerZero and vTextEmpty), tagTrue: (vIntegerZero and vTagTrue), tagFalse: (vIntegerZero and vTagFalse), tagTrueUpper: (vIntegerZero and vTagTrueUpper), tagPi: (vIntegerZero and vTagPi), tagPhi: (vIntegerZero and vTagPhi), tagInfinity: (vIntegerZero and vTagInfinity), tagNan: (vIntegerZero and vTagNan), tagCustom: (vIntegerZero and vTagCustom), vector: (vIntegerZero and vVector), vectorZero: (vIntegerZero and vVectorZero), point: (vIntegerZero and vPoint), pointZero: (vIntegerZero and vPointZero), list: (vIntegerZero and vList), map: (vIntegerZero and vMap), dice: (vIntegerZero and vDice))
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
              type: ":Boolean"
              value: false
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":Boolean"
              value: false
          - name: "percentagePositive"
            value:
              type: ":Boolean"
              value: false
          - name: "percentageZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "textFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "textOne"
            value:
              type: ":Boolean"
              value: false
          - name: "textZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPhi"
            value:
              type: ":Boolean"
              value: false
          - name: "tagInfinity"
            value:
              type: ":Boolean"
              value: false
          - name: "tagNan"
            value:
              type: ":Boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: and floatPositive

This runtime case exercises “and floatPositive” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0007
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and floatPositive.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicg
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
  let vTagTrueUpper be ('True') as :Tag
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :Vector(1, 2, 3)
  let vVectorZero be :Vector(0, 0, 0)
  let vPoint be :Point(1, 2, 3)
  let vPointZero be :Point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice be ([6, 2, 4]) as :Dice
  emit Done(nothing: (vFloatPositive and vNothing), booleanTrue: (vFloatPositive and vBooleanTrue), booleanFalse: (vFloatPositive and vBooleanFalse), integerOne: (vFloatPositive and vIntegerOne), integerZero: (vFloatPositive and vIntegerZero), floatPositive: (vFloatPositive and vFloatPositive), percentagePositive: (vFloatPositive and vPercentagePositive), percentageZero: (vFloatPositive and vPercentageZero), textTrue: (vFloatPositive and vTextTrue), textTrueUpper: (vFloatPositive and vTextTrueUpper), textFalse: (vFloatPositive and vTextFalse), textOne: (vFloatPositive and vTextOne), textZero: (vFloatPositive and vTextZero), textInvalid: (vFloatPositive and vTextInvalid), textEmpty: (vFloatPositive and vTextEmpty), tagTrue: (vFloatPositive and vTagTrue), tagFalse: (vFloatPositive and vTagFalse), tagTrueUpper: (vFloatPositive and vTagTrueUpper), tagPi: (vFloatPositive and vTagPi), tagPhi: (vFloatPositive and vTagPhi), tagInfinity: (vFloatPositive and vTagInfinity), tagNan: (vFloatPositive and vTagNan), tagCustom: (vFloatPositive and vTagCustom), vector: (vFloatPositive and vVector), vectorZero: (vFloatPositive and vVectorZero), point: (vFloatPositive and vPoint), pointZero: (vFloatPositive and vPointZero), list: (vFloatPositive and vList), map: (vFloatPositive and vMap), dice: (vFloatPositive and vDice))
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
              type: ":Nothing"
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":Boolean"
              value: true
          - name: "percentagePositive"
            value:
              type: ":Boolean"
              value: true
          - name: "percentageZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "textTrueUpper"
            value:
              type: ":Boolean"
              value: true
          - name: "textFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "textOne"
            value:
              type: ":Boolean"
              value: true
          - name: "textZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":Boolean"
              value: true
          - name: "tagPhi"
            value:
              type: ":Boolean"
              value: true
          - name: "tagInfinity"
            value:
              type: ":Boolean"
              value: true
          - name: "tagNan"
            value:
              type: ":Boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Nothing"
          - name: "map"
            value:
              type: ":Nothing"
          - name: "dice"
            value:
              type: ":Nothing"
```

---

## Test: and percentagePositive

This runtime case exercises “and percentagePositive” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0008
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and percentagePositive.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogich
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
  let vTagTrueUpper be ('True') as :Tag
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :Vector(1, 2, 3)
  let vVectorZero be :Vector(0, 0, 0)
  let vPoint be :Point(1, 2, 3)
  let vPointZero be :Point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice be ([6, 2, 4]) as :Dice
  emit Done(nothing: (vPercentagePositive and vNothing), booleanTrue: (vPercentagePositive and vBooleanTrue), booleanFalse: (vPercentagePositive and vBooleanFalse), integerOne: (vPercentagePositive and vIntegerOne), integerZero: (vPercentagePositive and vIntegerZero), floatPositive: (vPercentagePositive and vFloatPositive), percentagePositive: (vPercentagePositive and vPercentagePositive), percentageZero: (vPercentagePositive and vPercentageZero), textTrue: (vPercentagePositive and vTextTrue), textTrueUpper: (vPercentagePositive and vTextTrueUpper), textFalse: (vPercentagePositive and vTextFalse), textOne: (vPercentagePositive and vTextOne), textZero: (vPercentagePositive and vTextZero), textInvalid: (vPercentagePositive and vTextInvalid), textEmpty: (vPercentagePositive and vTextEmpty), tagTrue: (vPercentagePositive and vTagTrue), tagFalse: (vPercentagePositive and vTagFalse), tagTrueUpper: (vPercentagePositive and vTagTrueUpper), tagPi: (vPercentagePositive and vTagPi), tagPhi: (vPercentagePositive and vTagPhi), tagInfinity: (vPercentagePositive and vTagInfinity), tagNan: (vPercentagePositive and vTagNan), tagCustom: (vPercentagePositive and vTagCustom), vector: (vPercentagePositive and vVector), vectorZero: (vPercentagePositive and vVectorZero), point: (vPercentagePositive and vPoint), pointZero: (vPercentagePositive and vPointZero), list: (vPercentagePositive and vList), map: (vPercentagePositive and vMap), dice: (vPercentagePositive and vDice))
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
              type: ":Nothing"
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":Boolean"
              value: true
          - name: "percentagePositive"
            value:
              type: ":Boolean"
              value: true
          - name: "percentageZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "textTrueUpper"
            value:
              type: ":Boolean"
              value: true
          - name: "textFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "textOne"
            value:
              type: ":Boolean"
              value: true
          - name: "textZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":Boolean"
              value: true
          - name: "tagPhi"
            value:
              type: ":Boolean"
              value: true
          - name: "tagInfinity"
            value:
              type: ":Boolean"
              value: true
          - name: "tagNan"
            value:
              type: ":Boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Nothing"
          - name: "map"
            value:
              type: ":Nothing"
          - name: "dice"
            value:
              type: ":Nothing"
```

---

## Test: and percentageZero

This runtime case exercises “and percentageZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0009
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and percentageZero.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogici
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
  let vTagTrueUpper be ('True') as :Tag
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :Vector(1, 2, 3)
  let vVectorZero be :Vector(0, 0, 0)
  let vPoint be :Point(1, 2, 3)
  let vPointZero be :Point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice be ([6, 2, 4]) as :Dice
  emit Done(nothing: (vPercentageZero and vNothing), booleanTrue: (vPercentageZero and vBooleanTrue), booleanFalse: (vPercentageZero and vBooleanFalse), integerOne: (vPercentageZero and vIntegerOne), integerZero: (vPercentageZero and vIntegerZero), floatPositive: (vPercentageZero and vFloatPositive), percentagePositive: (vPercentageZero and vPercentagePositive), percentageZero: (vPercentageZero and vPercentageZero), textTrue: (vPercentageZero and vTextTrue), textTrueUpper: (vPercentageZero and vTextTrueUpper), textFalse: (vPercentageZero and vTextFalse), textOne: (vPercentageZero and vTextOne), textZero: (vPercentageZero and vTextZero), textInvalid: (vPercentageZero and vTextInvalid), textEmpty: (vPercentageZero and vTextEmpty), tagTrue: (vPercentageZero and vTagTrue), tagFalse: (vPercentageZero and vTagFalse), tagTrueUpper: (vPercentageZero and vTagTrueUpper), tagPi: (vPercentageZero and vTagPi), tagPhi: (vPercentageZero and vTagPhi), tagInfinity: (vPercentageZero and vTagInfinity), tagNan: (vPercentageZero and vTagNan), tagCustom: (vPercentageZero and vTagCustom), vector: (vPercentageZero and vVector), vectorZero: (vPercentageZero and vVectorZero), point: (vPercentageZero and vPoint), pointZero: (vPercentageZero and vPointZero), list: (vPercentageZero and vList), map: (vPercentageZero and vMap), dice: (vPercentageZero and vDice))
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
              type: ":Boolean"
              value: false
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":Boolean"
              value: false
          - name: "percentagePositive"
            value:
              type: ":Boolean"
              value: false
          - name: "percentageZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "textFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "textOne"
            value:
              type: ":Boolean"
              value: false
          - name: "textZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPhi"
            value:
              type: ":Boolean"
              value: false
          - name: "tagInfinity"
            value:
              type: ":Boolean"
              value: false
          - name: "tagNan"
            value:
              type: ":Boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: and textTrue

This runtime case exercises “and textTrue” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0010
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and textTrue.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicj
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
  let vTagTrueUpper be ('True') as :Tag
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :Vector(1, 2, 3)
  let vVectorZero be :Vector(0, 0, 0)
  let vPoint be :Point(1, 2, 3)
  let vPointZero be :Point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice be ([6, 2, 4]) as :Dice
  emit Done(nothing: (vTextTrue and vNothing), booleanTrue: (vTextTrue and vBooleanTrue), booleanFalse: (vTextTrue and vBooleanFalse), integerOne: (vTextTrue and vIntegerOne), integerZero: (vTextTrue and vIntegerZero), floatPositive: (vTextTrue and vFloatPositive), percentagePositive: (vTextTrue and vPercentagePositive), percentageZero: (vTextTrue and vPercentageZero), textTrue: (vTextTrue and vTextTrue), textTrueUpper: (vTextTrue and vTextTrueUpper), textFalse: (vTextTrue and vTextFalse), textOne: (vTextTrue and vTextOne), textZero: (vTextTrue and vTextZero), textInvalid: (vTextTrue and vTextInvalid), textEmpty: (vTextTrue and vTextEmpty), tagTrue: (vTextTrue and vTagTrue), tagFalse: (vTextTrue and vTagFalse), tagTrueUpper: (vTextTrue and vTagTrueUpper), tagPi: (vTextTrue and vTagPi), tagPhi: (vTextTrue and vTagPhi), tagInfinity: (vTextTrue and vTagInfinity), tagNan: (vTextTrue and vTagNan), tagCustom: (vTextTrue and vTagCustom), vector: (vTextTrue and vVector), vectorZero: (vTextTrue and vVectorZero), point: (vTextTrue and vPoint), pointZero: (vTextTrue and vPointZero), list: (vTextTrue and vList), map: (vTextTrue and vMap), dice: (vTextTrue and vDice))
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
              type: ":Nothing"
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":Boolean"
              value: true
          - name: "percentagePositive"
            value:
              type: ":Boolean"
              value: true
          - name: "percentageZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "textTrueUpper"
            value:
              type: ":Boolean"
              value: true
          - name: "textFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "textOne"
            value:
              type: ":Boolean"
              value: true
          - name: "textZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":Boolean"
              value: true
          - name: "tagPhi"
            value:
              type: ":Boolean"
              value: true
          - name: "tagInfinity"
            value:
              type: ":Boolean"
              value: true
          - name: "tagNan"
            value:
              type: ":Boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Nothing"
          - name: "map"
            value:
              type: ":Nothing"
          - name: "dice"
            value:
              type: ":Nothing"
```

---

## Test: and textTrueUpper

This runtime case exercises “and textTrueUpper” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0011
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and textTrueUpper.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogick
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
  let vTagTrueUpper be ('True') as :Tag
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :Vector(1, 2, 3)
  let vVectorZero be :Vector(0, 0, 0)
  let vPoint be :Point(1, 2, 3)
  let vPointZero be :Point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice be ([6, 2, 4]) as :Dice
  emit Done(nothing: (vTextTrueUpper and vNothing), booleanTrue: (vTextTrueUpper and vBooleanTrue), booleanFalse: (vTextTrueUpper and vBooleanFalse), integerOne: (vTextTrueUpper and vIntegerOne), integerZero: (vTextTrueUpper and vIntegerZero), floatPositive: (vTextTrueUpper and vFloatPositive), percentagePositive: (vTextTrueUpper and vPercentagePositive), percentageZero: (vTextTrueUpper and vPercentageZero), textTrue: (vTextTrueUpper and vTextTrue), textTrueUpper: (vTextTrueUpper and vTextTrueUpper), textFalse: (vTextTrueUpper and vTextFalse), textOne: (vTextTrueUpper and vTextOne), textZero: (vTextTrueUpper and vTextZero), textInvalid: (vTextTrueUpper and vTextInvalid), textEmpty: (vTextTrueUpper and vTextEmpty), tagTrue: (vTextTrueUpper and vTagTrue), tagFalse: (vTextTrueUpper and vTagFalse), tagTrueUpper: (vTextTrueUpper and vTagTrueUpper), tagPi: (vTextTrueUpper and vTagPi), tagPhi: (vTextTrueUpper and vTagPhi), tagInfinity: (vTextTrueUpper and vTagInfinity), tagNan: (vTextTrueUpper and vTagNan), tagCustom: (vTextTrueUpper and vTagCustom), vector: (vTextTrueUpper and vVector), vectorZero: (vTextTrueUpper and vVectorZero), point: (vTextTrueUpper and vPoint), pointZero: (vTextTrueUpper and vPointZero), list: (vTextTrueUpper and vList), map: (vTextTrueUpper and vMap), dice: (vTextTrueUpper and vDice))
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
              type: ":Nothing"
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":Boolean"
              value: true
          - name: "percentagePositive"
            value:
              type: ":Boolean"
              value: true
          - name: "percentageZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "textTrueUpper"
            value:
              type: ":Boolean"
              value: true
          - name: "textFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "textOne"
            value:
              type: ":Boolean"
              value: true
          - name: "textZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":Boolean"
              value: true
          - name: "tagPhi"
            value:
              type: ":Boolean"
              value: true
          - name: "tagInfinity"
            value:
              type: ":Boolean"
              value: true
          - name: "tagNan"
            value:
              type: ":Boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Nothing"
          - name: "map"
            value:
              type: ":Nothing"
          - name: "dice"
            value:
              type: ":Nothing"
```

---

## Test: and textFalse

This runtime case exercises “and textFalse” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0012
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and textFalse.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicl
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
  let vTagTrueUpper be ('True') as :Tag
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :Vector(1, 2, 3)
  let vVectorZero be :Vector(0, 0, 0)
  let vPoint be :Point(1, 2, 3)
  let vPointZero be :Point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice be ([6, 2, 4]) as :Dice
  emit Done(nothing: (vTextFalse and vNothing), booleanTrue: (vTextFalse and vBooleanTrue), booleanFalse: (vTextFalse and vBooleanFalse), integerOne: (vTextFalse and vIntegerOne), integerZero: (vTextFalse and vIntegerZero), floatPositive: (vTextFalse and vFloatPositive), percentagePositive: (vTextFalse and vPercentagePositive), percentageZero: (vTextFalse and vPercentageZero), textTrue: (vTextFalse and vTextTrue), textTrueUpper: (vTextFalse and vTextTrueUpper), textFalse: (vTextFalse and vTextFalse), textOne: (vTextFalse and vTextOne), textZero: (vTextFalse and vTextZero), textInvalid: (vTextFalse and vTextInvalid), textEmpty: (vTextFalse and vTextEmpty), tagTrue: (vTextFalse and vTagTrue), tagFalse: (vTextFalse and vTagFalse), tagTrueUpper: (vTextFalse and vTagTrueUpper), tagPi: (vTextFalse and vTagPi), tagPhi: (vTextFalse and vTagPhi), tagInfinity: (vTextFalse and vTagInfinity), tagNan: (vTextFalse and vTagNan), tagCustom: (vTextFalse and vTagCustom), vector: (vTextFalse and vVector), vectorZero: (vTextFalse and vVectorZero), point: (vTextFalse and vPoint), pointZero: (vTextFalse and vPointZero), list: (vTextFalse and vList), map: (vTextFalse and vMap), dice: (vTextFalse and vDice))
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
              type: ":Boolean"
              value: false
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":Boolean"
              value: false
          - name: "percentagePositive"
            value:
              type: ":Boolean"
              value: false
          - name: "percentageZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "textFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "textOne"
            value:
              type: ":Boolean"
              value: false
          - name: "textZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPhi"
            value:
              type: ":Boolean"
              value: false
          - name: "tagInfinity"
            value:
              type: ":Boolean"
              value: false
          - name: "tagNan"
            value:
              type: ":Boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: and textOne

This runtime case exercises “and textOne” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0013
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and textOne.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicm
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
  let vTagTrueUpper be ('True') as :Tag
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :Vector(1, 2, 3)
  let vVectorZero be :Vector(0, 0, 0)
  let vPoint be :Point(1, 2, 3)
  let vPointZero be :Point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice be ([6, 2, 4]) as :Dice
  emit Done(nothing: (vTextOne and vNothing), booleanTrue: (vTextOne and vBooleanTrue), booleanFalse: (vTextOne and vBooleanFalse), integerOne: (vTextOne and vIntegerOne), integerZero: (vTextOne and vIntegerZero), floatPositive: (vTextOne and vFloatPositive), percentagePositive: (vTextOne and vPercentagePositive), percentageZero: (vTextOne and vPercentageZero), textTrue: (vTextOne and vTextTrue), textTrueUpper: (vTextOne and vTextTrueUpper), textFalse: (vTextOne and vTextFalse), textOne: (vTextOne and vTextOne), textZero: (vTextOne and vTextZero), textInvalid: (vTextOne and vTextInvalid), textEmpty: (vTextOne and vTextEmpty), tagTrue: (vTextOne and vTagTrue), tagFalse: (vTextOne and vTagFalse), tagTrueUpper: (vTextOne and vTagTrueUpper), tagPi: (vTextOne and vTagPi), tagPhi: (vTextOne and vTagPhi), tagInfinity: (vTextOne and vTagInfinity), tagNan: (vTextOne and vTagNan), tagCustom: (vTextOne and vTagCustom), vector: (vTextOne and vVector), vectorZero: (vTextOne and vVectorZero), point: (vTextOne and vPoint), pointZero: (vTextOne and vPointZero), list: (vTextOne and vList), map: (vTextOne and vMap), dice: (vTextOne and vDice))
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
              type: ":Nothing"
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":Boolean"
              value: true
          - name: "percentagePositive"
            value:
              type: ":Boolean"
              value: true
          - name: "percentageZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "textTrueUpper"
            value:
              type: ":Boolean"
              value: true
          - name: "textFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "textOne"
            value:
              type: ":Boolean"
              value: true
          - name: "textZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":Boolean"
              value: true
          - name: "tagPhi"
            value:
              type: ":Boolean"
              value: true
          - name: "tagInfinity"
            value:
              type: ":Boolean"
              value: true
          - name: "tagNan"
            value:
              type: ":Boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Nothing"
          - name: "map"
            value:
              type: ":Nothing"
          - name: "dice"
            value:
              type: ":Nothing"
```

---

## Test: and textZero

This runtime case exercises “and textZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0014
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and textZero.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicn
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
  let vTagTrueUpper be ('True') as :Tag
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :Vector(1, 2, 3)
  let vVectorZero be :Vector(0, 0, 0)
  let vPoint be :Point(1, 2, 3)
  let vPointZero be :Point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice be ([6, 2, 4]) as :Dice
  emit Done(nothing: (vTextZero and vNothing), booleanTrue: (vTextZero and vBooleanTrue), booleanFalse: (vTextZero and vBooleanFalse), integerOne: (vTextZero and vIntegerOne), integerZero: (vTextZero and vIntegerZero), floatPositive: (vTextZero and vFloatPositive), percentagePositive: (vTextZero and vPercentagePositive), percentageZero: (vTextZero and vPercentageZero), textTrue: (vTextZero and vTextTrue), textTrueUpper: (vTextZero and vTextTrueUpper), textFalse: (vTextZero and vTextFalse), textOne: (vTextZero and vTextOne), textZero: (vTextZero and vTextZero), textInvalid: (vTextZero and vTextInvalid), textEmpty: (vTextZero and vTextEmpty), tagTrue: (vTextZero and vTagTrue), tagFalse: (vTextZero and vTagFalse), tagTrueUpper: (vTextZero and vTagTrueUpper), tagPi: (vTextZero and vTagPi), tagPhi: (vTextZero and vTagPhi), tagInfinity: (vTextZero and vTagInfinity), tagNan: (vTextZero and vTagNan), tagCustom: (vTextZero and vTagCustom), vector: (vTextZero and vVector), vectorZero: (vTextZero and vVectorZero), point: (vTextZero and vPoint), pointZero: (vTextZero and vPointZero), list: (vTextZero and vList), map: (vTextZero and vMap), dice: (vTextZero and vDice))
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
              type: ":Boolean"
              value: false
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":Boolean"
              value: false
          - name: "percentagePositive"
            value:
              type: ":Boolean"
              value: false
          - name: "percentageZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "textFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "textOne"
            value:
              type: ":Boolean"
              value: false
          - name: "textZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPhi"
            value:
              type: ":Boolean"
              value: false
          - name: "tagInfinity"
            value:
              type: ":Boolean"
              value: false
          - name: "tagNan"
            value:
              type: ":Boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: and textInvalid

This runtime case exercises “and textInvalid” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0015
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and textInvalid.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogico
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
  let vTagTrueUpper be ('True') as :Tag
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :Vector(1, 2, 3)
  let vVectorZero be :Vector(0, 0, 0)
  let vPoint be :Point(1, 2, 3)
  let vPointZero be :Point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice be ([6, 2, 4]) as :Dice
  emit Done(nothing: (vTextInvalid and vNothing), booleanTrue: (vTextInvalid and vBooleanTrue), booleanFalse: (vTextInvalid and vBooleanFalse), integerOne: (vTextInvalid and vIntegerOne), integerZero: (vTextInvalid and vIntegerZero), floatPositive: (vTextInvalid and vFloatPositive), percentagePositive: (vTextInvalid and vPercentagePositive), percentageZero: (vTextInvalid and vPercentageZero), textTrue: (vTextInvalid and vTextTrue), textTrueUpper: (vTextInvalid and vTextTrueUpper), textFalse: (vTextInvalid and vTextFalse), textOne: (vTextInvalid and vTextOne), textZero: (vTextInvalid and vTextZero), textInvalid: (vTextInvalid and vTextInvalid), textEmpty: (vTextInvalid and vTextEmpty), tagTrue: (vTextInvalid and vTagTrue), tagFalse: (vTextInvalid and vTagFalse), tagTrueUpper: (vTextInvalid and vTagTrueUpper), tagPi: (vTextInvalid and vTagPi), tagPhi: (vTextInvalid and vTagPhi), tagInfinity: (vTextInvalid and vTagInfinity), tagNan: (vTextInvalid and vTagNan), tagCustom: (vTextInvalid and vTagCustom), vector: (vTextInvalid and vVector), vectorZero: (vTextInvalid and vVectorZero), point: (vTextInvalid and vPoint), pointZero: (vTextInvalid and vPointZero), list: (vTextInvalid and vList), map: (vTextInvalid and vMap), dice: (vTextInvalid and vDice))
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
              type: ":Boolean"
              value: false
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":Boolean"
              value: false
          - name: "percentagePositive"
            value:
              type: ":Boolean"
              value: false
          - name: "percentageZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "textFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "textOne"
            value:
              type: ":Boolean"
              value: false
          - name: "textZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPhi"
            value:
              type: ":Boolean"
              value: false
          - name: "tagInfinity"
            value:
              type: ":Boolean"
              value: false
          - name: "tagNan"
            value:
              type: ":Boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: and textEmpty

This runtime case exercises “and textEmpty” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0016
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "and textEmpty.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicp
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
  let vTagTrueUpper be ('True') as :Tag
  let vTagPi be pi
  let vTagPhi be 1.6180339887498948
  let vTagInfinity be infinity
  let vTagNan be #nan
  let vTagCustom be #custom
  let vVector be :Vector(1, 2, 3)
  let vVectorZero be :Vector(0, 0, 0)
  let vPoint be :Point(1, 2, 3)
  let vPointZero be :Point(0, 0, 0)
  let vList be [1, 2]
  let vMap be [name: 'Ada']
  let vDice be ([6, 2, 4]) as :Dice
  emit Done(nothing: (vTextEmpty and vNothing), booleanTrue: (vTextEmpty and vBooleanTrue), booleanFalse: (vTextEmpty and vBooleanFalse), integerOne: (vTextEmpty and vIntegerOne), integerZero: (vTextEmpty and vIntegerZero), floatPositive: (vTextEmpty and vFloatPositive), percentagePositive: (vTextEmpty and vPercentagePositive), percentageZero: (vTextEmpty and vPercentageZero), textTrue: (vTextEmpty and vTextTrue), textTrueUpper: (vTextEmpty and vTextTrueUpper), textFalse: (vTextEmpty and vTextFalse), textOne: (vTextEmpty and vTextOne), textZero: (vTextEmpty and vTextZero), textInvalid: (vTextEmpty and vTextInvalid), textEmpty: (vTextEmpty and vTextEmpty), tagTrue: (vTextEmpty and vTagTrue), tagFalse: (vTextEmpty and vTagFalse), tagTrueUpper: (vTextEmpty and vTagTrueUpper), tagPi: (vTextEmpty and vTagPi), tagPhi: (vTextEmpty and vTagPhi), tagInfinity: (vTextEmpty and vTagInfinity), tagNan: (vTextEmpty and vTagNan), tagCustom: (vTextEmpty and vTagCustom), vector: (vTextEmpty and vVector), vectorZero: (vTextEmpty and vVectorZero), point: (vTextEmpty and vPoint), pointZero: (vTextEmpty and vPointZero), list: (vTextEmpty and vList), map: (vTextEmpty and vMap), dice: (vTextEmpty and vDice))
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
              type: ":Boolean"
              value: false
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: false
          - name: "floatPositive"
            value:
              type: ":Boolean"
              value: false
          - name: "percentagePositive"
            value:
              type: ":Boolean"
              value: false
          - name: "percentageZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "textTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "textFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "textOne"
            value:
              type: ":Boolean"
              value: false
          - name: "textZero"
            value:
              type: ":Boolean"
              value: false
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: false
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: false
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: false
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPi"
            value:
              type: ":Boolean"
              value: false
          - name: "tagPhi"
            value:
              type: ":Boolean"
              value: false
          - name: "tagInfinity"
            value:
              type: ":Boolean"
              value: false
          - name: "tagNan"
            value:
              type: ":Boolean"
              value: false
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: false
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: false
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: false
          - name: "list"
            value:
              type: ":Boolean"
              value: false
          - name: "map"
            value:
              type: ":Boolean"
              value: false
          - name: "dice"
            value:
              type: ":Boolean"
              value: false
```
