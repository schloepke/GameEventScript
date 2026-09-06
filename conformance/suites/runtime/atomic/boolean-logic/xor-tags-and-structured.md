---
formatVersion: 1
suiteId: "runtime.atomic.boolean-logic.xor-tags-and-structured"
title: "Boolean Logic — Xor Tags and Structured Values"
categories: [conformance]
---

# Boolean Logic — Xor Tags and Structured Values

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers xor for tags, spatial values, and collections.

---

## Test: xor tagTrue

This runtime case exercises “xor tagTrue” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0077
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor tagTrue.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicby
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
  emit Done(nothing: (vTagTrue xor vNothing), booleanTrue: (vTagTrue xor vBooleanTrue), booleanFalse: (vTagTrue xor vBooleanFalse), integerOne: (vTagTrue xor vIntegerOne), integerZero: (vTagTrue xor vIntegerZero), floatPositive: (vTagTrue xor vFloatPositive), percentagePositive: (vTagTrue xor vPercentagePositive), percentageZero: (vTagTrue xor vPercentageZero), textTrue: (vTagTrue xor vTextTrue), textTrueUpper: (vTagTrue xor vTextTrueUpper), textFalse: (vTagTrue xor vTextFalse), textOne: (vTagTrue xor vTextOne), textZero: (vTagTrue xor vTextZero), textInvalid: (vTagTrue xor vTextInvalid), textEmpty: (vTagTrue xor vTextEmpty), tagTrue: (vTagTrue xor vTagTrue), tagFalse: (vTagTrue xor vTagFalse), tagTrueUpper: (vTagTrue xor vTagTrueUpper), tagPi: (vTagTrue xor vTagPi), tagPhi: (vTagTrue xor vTagPhi), tagInfinity: (vTagTrue xor vTagInfinity), tagNan: (vTagTrue xor vTagNan), tagCustom: (vTagTrue xor vTagCustom), vector: (vTagTrue xor vVector), vectorZero: (vTagTrue xor vVectorZero), point: (vTagTrue xor vPoint), pointZero: (vTagTrue xor vPointZero), list: (vTagTrue xor vList), map: (vTagTrue xor vMap), dice: (vTagTrue xor vDice))
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

## Test: xor tagFalse

This runtime case exercises “xor tagFalse” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0078
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor tagFalse.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicbz
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
  emit Done(nothing: (vTagFalse xor vNothing), booleanTrue: (vTagFalse xor vBooleanTrue), booleanFalse: (vTagFalse xor vBooleanFalse), integerOne: (vTagFalse xor vIntegerOne), integerZero: (vTagFalse xor vIntegerZero), floatPositive: (vTagFalse xor vFloatPositive), percentagePositive: (vTagFalse xor vPercentagePositive), percentageZero: (vTagFalse xor vPercentageZero), textTrue: (vTagFalse xor vTextTrue), textTrueUpper: (vTagFalse xor vTextTrueUpper), textFalse: (vTagFalse xor vTextFalse), textOne: (vTagFalse xor vTextOne), textZero: (vTagFalse xor vTextZero), textInvalid: (vTagFalse xor vTextInvalid), textEmpty: (vTagFalse xor vTextEmpty), tagTrue: (vTagFalse xor vTagTrue), tagFalse: (vTagFalse xor vTagFalse), tagTrueUpper: (vTagFalse xor vTagTrueUpper), tagPi: (vTagFalse xor vTagPi), tagPhi: (vTagFalse xor vTagPhi), tagInfinity: (vTagFalse xor vTagInfinity), tagNan: (vTagFalse xor vTagNan), tagCustom: (vTagFalse xor vTagCustom), vector: (vTagFalse xor vVector), vectorZero: (vTagFalse xor vVectorZero), point: (vTagFalse xor vPoint), pointZero: (vTagFalse xor vPointZero), list: (vTagFalse xor vList), map: (vTagFalse xor vMap), dice: (vTagFalse xor vDice))
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

## Test: xor tagTrueUpper

This runtime case exercises “xor tagTrueUpper” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0079
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor tagTrueUpper.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicca
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
  emit Done(nothing: (vTagTrueUpper xor vNothing), booleanTrue: (vTagTrueUpper xor vBooleanTrue), booleanFalse: (vTagTrueUpper xor vBooleanFalse), integerOne: (vTagTrueUpper xor vIntegerOne), integerZero: (vTagTrueUpper xor vIntegerZero), floatPositive: (vTagTrueUpper xor vFloatPositive), percentagePositive: (vTagTrueUpper xor vPercentagePositive), percentageZero: (vTagTrueUpper xor vPercentageZero), textTrue: (vTagTrueUpper xor vTextTrue), textTrueUpper: (vTagTrueUpper xor vTextTrueUpper), textFalse: (vTagTrueUpper xor vTextFalse), textOne: (vTagTrueUpper xor vTextOne), textZero: (vTagTrueUpper xor vTextZero), textInvalid: (vTagTrueUpper xor vTextInvalid), textEmpty: (vTagTrueUpper xor vTextEmpty), tagTrue: (vTagTrueUpper xor vTagTrue), tagFalse: (vTagTrueUpper xor vTagFalse), tagTrueUpper: (vTagTrueUpper xor vTagTrueUpper), tagPi: (vTagTrueUpper xor vTagPi), tagPhi: (vTagTrueUpper xor vTagPhi), tagInfinity: (vTagTrueUpper xor vTagInfinity), tagNan: (vTagTrueUpper xor vTagNan), tagCustom: (vTagTrueUpper xor vTagCustom), vector: (vTagTrueUpper xor vVector), vectorZero: (vTagTrueUpper xor vVectorZero), point: (vTagTrueUpper xor vPoint), pointZero: (vTagTrueUpper xor vPointZero), list: (vTagTrueUpper xor vList), map: (vTagTrueUpper xor vMap), dice: (vTagTrueUpper xor vDice))
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

## Test: xor tagPi

This runtime case exercises “xor tagPi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0080
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor tagPi.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogiccb
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
  emit Done(nothing: (vTagPi xor vNothing), booleanTrue: (vTagPi xor vBooleanTrue), booleanFalse: (vTagPi xor vBooleanFalse), integerOne: (vTagPi xor vIntegerOne), integerZero: (vTagPi xor vIntegerZero), floatPositive: (vTagPi xor vFloatPositive), percentagePositive: (vTagPi xor vPercentagePositive), percentageZero: (vTagPi xor vPercentageZero), textTrue: (vTagPi xor vTextTrue), textTrueUpper: (vTagPi xor vTextTrueUpper), textFalse: (vTagPi xor vTextFalse), textOne: (vTagPi xor vTextOne), textZero: (vTagPi xor vTextZero), textInvalid: (vTagPi xor vTextInvalid), textEmpty: (vTagPi xor vTextEmpty), tagTrue: (vTagPi xor vTagTrue), tagFalse: (vTagPi xor vTagFalse), tagTrueUpper: (vTagPi xor vTagTrueUpper), tagPi: (vTagPi xor vTagPi), tagPhi: (vTagPi xor vTagPhi), tagInfinity: (vTagPi xor vTagInfinity), tagNan: (vTagPi xor vTagNan), tagCustom: (vTagPi xor vTagCustom), vector: (vTagPi xor vVector), vectorZero: (vTagPi xor vVectorZero), point: (vTagPi xor vPoint), pointZero: (vTagPi xor vPointZero), list: (vTagPi xor vList), map: (vTagPi xor vMap), dice: (vTagPi xor vDice))
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
              value: false
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: true
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
              value: true
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
              value: true
          - name: "textOne"
            value:
              type: ":Boolean"
              value: false
          - name: "textZero"
            value:
              type: ":Boolean"
              value: true
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: true
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: true
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: true
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
              value: true
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: true
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

## Test: xor tagPhi

This runtime case exercises “xor tagPhi” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0081
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor tagPhi.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogiccc
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
  emit Done(nothing: (vTagPhi xor vNothing), booleanTrue: (vTagPhi xor vBooleanTrue), booleanFalse: (vTagPhi xor vBooleanFalse), integerOne: (vTagPhi xor vIntegerOne), integerZero: (vTagPhi xor vIntegerZero), floatPositive: (vTagPhi xor vFloatPositive), percentagePositive: (vTagPhi xor vPercentagePositive), percentageZero: (vTagPhi xor vPercentageZero), textTrue: (vTagPhi xor vTextTrue), textTrueUpper: (vTagPhi xor vTextTrueUpper), textFalse: (vTagPhi xor vTextFalse), textOne: (vTagPhi xor vTextOne), textZero: (vTagPhi xor vTextZero), textInvalid: (vTagPhi xor vTextInvalid), textEmpty: (vTagPhi xor vTextEmpty), tagTrue: (vTagPhi xor vTagTrue), tagFalse: (vTagPhi xor vTagFalse), tagTrueUpper: (vTagPhi xor vTagTrueUpper), tagPi: (vTagPhi xor vTagPi), tagPhi: (vTagPhi xor vTagPhi), tagInfinity: (vTagPhi xor vTagInfinity), tagNan: (vTagPhi xor vTagNan), tagCustom: (vTagPhi xor vTagCustom), vector: (vTagPhi xor vVector), vectorZero: (vTagPhi xor vVectorZero), point: (vTagPhi xor vPoint), pointZero: (vTagPhi xor vPointZero), list: (vTagPhi xor vList), map: (vTagPhi xor vMap), dice: (vTagPhi xor vDice))
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
              value: false
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: true
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
              value: true
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
              value: true
          - name: "textOne"
            value:
              type: ":Boolean"
              value: false
          - name: "textZero"
            value:
              type: ":Boolean"
              value: true
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: true
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: true
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: true
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
              value: true
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: true
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

## Test: xor tagInfinity

This runtime case exercises “xor tagInfinity” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0082
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor tagInfinity.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogiccd
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
  emit Done(nothing: (vTagInfinity xor vNothing), booleanTrue: (vTagInfinity xor vBooleanTrue), booleanFalse: (vTagInfinity xor vBooleanFalse), integerOne: (vTagInfinity xor vIntegerOne), integerZero: (vTagInfinity xor vIntegerZero), floatPositive: (vTagInfinity xor vFloatPositive), percentagePositive: (vTagInfinity xor vPercentagePositive), percentageZero: (vTagInfinity xor vPercentageZero), textTrue: (vTagInfinity xor vTextTrue), textTrueUpper: (vTagInfinity xor vTextTrueUpper), textFalse: (vTagInfinity xor vTextFalse), textOne: (vTagInfinity xor vTextOne), textZero: (vTagInfinity xor vTextZero), textInvalid: (vTagInfinity xor vTextInvalid), textEmpty: (vTagInfinity xor vTextEmpty), tagTrue: (vTagInfinity xor vTagTrue), tagFalse: (vTagInfinity xor vTagFalse), tagTrueUpper: (vTagInfinity xor vTagTrueUpper), tagPi: (vTagInfinity xor vTagPi), tagPhi: (vTagInfinity xor vTagPhi), tagInfinity: (vTagInfinity xor vTagInfinity), tagNan: (vTagInfinity xor vTagNan), tagCustom: (vTagInfinity xor vTagCustom), vector: (vTagInfinity xor vVector), vectorZero: (vTagInfinity xor vVectorZero), point: (vTagInfinity xor vPoint), pointZero: (vTagInfinity xor vPointZero), list: (vTagInfinity xor vList), map: (vTagInfinity xor vMap), dice: (vTagInfinity xor vDice))
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
              value: false
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: true
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
              value: true
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
              value: true
          - name: "textOne"
            value:
              type: ":Boolean"
              value: false
          - name: "textZero"
            value:
              type: ":Boolean"
              value: true
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: true
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: true
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: true
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
              value: true
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: true
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

## Test: xor tagNan

This runtime case exercises “xor tagNan” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0083
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor tagNan.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicce
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
  emit Done(nothing: (vTagNan xor vNothing), booleanTrue: (vTagNan xor vBooleanTrue), booleanFalse: (vTagNan xor vBooleanFalse), integerOne: (vTagNan xor vIntegerOne), integerZero: (vTagNan xor vIntegerZero), floatPositive: (vTagNan xor vFloatPositive), percentagePositive: (vTagNan xor vPercentagePositive), percentageZero: (vTagNan xor vPercentageZero), textTrue: (vTagNan xor vTextTrue), textTrueUpper: (vTagNan xor vTextTrueUpper), textFalse: (vTagNan xor vTextFalse), textOne: (vTagNan xor vTextOne), textZero: (vTagNan xor vTextZero), textInvalid: (vTagNan xor vTextInvalid), textEmpty: (vTagNan xor vTextEmpty), tagTrue: (vTagNan xor vTagTrue), tagFalse: (vTagNan xor vTagFalse), tagTrueUpper: (vTagNan xor vTagTrueUpper), tagPi: (vTagNan xor vTagPi), tagPhi: (vTagNan xor vTagPhi), tagInfinity: (vTagNan xor vTagInfinity), tagNan: (vTagNan xor vTagNan), tagCustom: (vTagNan xor vTagCustom), vector: (vTagNan xor vVector), vectorZero: (vTagNan xor vVectorZero), point: (vTagNan xor vPoint), pointZero: (vTagNan xor vPointZero), list: (vTagNan xor vList), map: (vTagNan xor vMap), dice: (vTagNan xor vDice))
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

## Test: xor tagCustom

This runtime case exercises “xor tagCustom” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0084
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor tagCustom.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogiccf
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
  emit Done(nothing: (vTagCustom xor vNothing), booleanTrue: (vTagCustom xor vBooleanTrue), booleanFalse: (vTagCustom xor vBooleanFalse), integerOne: (vTagCustom xor vIntegerOne), integerZero: (vTagCustom xor vIntegerZero), floatPositive: (vTagCustom xor vFloatPositive), percentagePositive: (vTagCustom xor vPercentagePositive), percentageZero: (vTagCustom xor vPercentageZero), textTrue: (vTagCustom xor vTextTrue), textTrueUpper: (vTagCustom xor vTextTrueUpper), textFalse: (vTagCustom xor vTextFalse), textOne: (vTagCustom xor vTextOne), textZero: (vTagCustom xor vTextZero), textInvalid: (vTagCustom xor vTextInvalid), textEmpty: (vTagCustom xor vTextEmpty), tagTrue: (vTagCustom xor vTagTrue), tagFalse: (vTagCustom xor vTagFalse), tagTrueUpper: (vTagCustom xor vTagTrueUpper), tagPi: (vTagCustom xor vTagPi), tagPhi: (vTagCustom xor vTagPhi), tagInfinity: (vTagCustom xor vTagInfinity), tagNan: (vTagCustom xor vTagNan), tagCustom: (vTagCustom xor vTagCustom), vector: (vTagCustom xor vVector), vectorZero: (vTagCustom xor vVectorZero), point: (vTagCustom xor vPoint), pointZero: (vTagCustom xor vPointZero), list: (vTagCustom xor vList), map: (vTagCustom xor vMap), dice: (vTagCustom xor vDice))
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

## Test: xor vector

This runtime case exercises “xor vector” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0085
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor vector.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogiccg
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
  emit Done(nothing: (vVector xor vNothing), booleanTrue: (vVector xor vBooleanTrue), booleanFalse: (vVector xor vBooleanFalse), integerOne: (vVector xor vIntegerOne), integerZero: (vVector xor vIntegerZero), floatPositive: (vVector xor vFloatPositive), percentagePositive: (vVector xor vPercentagePositive), percentageZero: (vVector xor vPercentageZero), textTrue: (vVector xor vTextTrue), textTrueUpper: (vVector xor vTextTrueUpper), textFalse: (vVector xor vTextFalse), textOne: (vVector xor vTextOne), textZero: (vVector xor vTextZero), textInvalid: (vVector xor vTextInvalid), textEmpty: (vVector xor vTextEmpty), tagTrue: (vVector xor vTagTrue), tagFalse: (vVector xor vTagFalse), tagTrueUpper: (vVector xor vTagTrueUpper), tagPi: (vVector xor vTagPi), tagPhi: (vVector xor vTagPhi), tagInfinity: (vVector xor vTagInfinity), tagNan: (vVector xor vTagNan), tagCustom: (vVector xor vTagCustom), vector: (vVector xor vVector), vectorZero: (vVector xor vVectorZero), point: (vVector xor vPoint), pointZero: (vVector xor vPointZero), list: (vVector xor vList), map: (vVector xor vMap), dice: (vVector xor vDice))
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
              value: false
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: true
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
              value: true
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
              value: true
          - name: "textOne"
            value:
              type: ":Boolean"
              value: false
          - name: "textZero"
            value:
              type: ":Boolean"
              value: true
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: true
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: true
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: true
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
              value: true
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: true
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

## Test: xor vectorZero

This runtime case exercises “xor vectorZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0086
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor vectorZero.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicch
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
  emit Done(nothing: (vVectorZero xor vNothing), booleanTrue: (vVectorZero xor vBooleanTrue), booleanFalse: (vVectorZero xor vBooleanFalse), integerOne: (vVectorZero xor vIntegerOne), integerZero: (vVectorZero xor vIntegerZero), floatPositive: (vVectorZero xor vFloatPositive), percentagePositive: (vVectorZero xor vPercentagePositive), percentageZero: (vVectorZero xor vPercentageZero), textTrue: (vVectorZero xor vTextTrue), textTrueUpper: (vVectorZero xor vTextTrueUpper), textFalse: (vVectorZero xor vTextFalse), textOne: (vVectorZero xor vTextOne), textZero: (vVectorZero xor vTextZero), textInvalid: (vVectorZero xor vTextInvalid), textEmpty: (vVectorZero xor vTextEmpty), tagTrue: (vVectorZero xor vTagTrue), tagFalse: (vVectorZero xor vTagFalse), tagTrueUpper: (vVectorZero xor vTagTrueUpper), tagPi: (vVectorZero xor vTagPi), tagPhi: (vVectorZero xor vTagPhi), tagInfinity: (vVectorZero xor vTagInfinity), tagNan: (vVectorZero xor vTagNan), tagCustom: (vVectorZero xor vTagCustom), vector: (vVectorZero xor vVector), vectorZero: (vVectorZero xor vVectorZero), point: (vVectorZero xor vPoint), pointZero: (vVectorZero xor vPointZero), list: (vVectorZero xor vList), map: (vVectorZero xor vMap), dice: (vVectorZero xor vDice))
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

## Test: xor point

This runtime case exercises “xor point” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0087
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor point.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicci
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
  emit Done(nothing: (vPoint xor vNothing), booleanTrue: (vPoint xor vBooleanTrue), booleanFalse: (vPoint xor vBooleanFalse), integerOne: (vPoint xor vIntegerOne), integerZero: (vPoint xor vIntegerZero), floatPositive: (vPoint xor vFloatPositive), percentagePositive: (vPoint xor vPercentagePositive), percentageZero: (vPoint xor vPercentageZero), textTrue: (vPoint xor vTextTrue), textTrueUpper: (vPoint xor vTextTrueUpper), textFalse: (vPoint xor vTextFalse), textOne: (vPoint xor vTextOne), textZero: (vPoint xor vTextZero), textInvalid: (vPoint xor vTextInvalid), textEmpty: (vPoint xor vTextEmpty), tagTrue: (vPoint xor vTagTrue), tagFalse: (vPoint xor vTagFalse), tagTrueUpper: (vPoint xor vTagTrueUpper), tagPi: (vPoint xor vTagPi), tagPhi: (vPoint xor vTagPhi), tagInfinity: (vPoint xor vTagInfinity), tagNan: (vPoint xor vTagNan), tagCustom: (vPoint xor vTagCustom), vector: (vPoint xor vVector), vectorZero: (vPoint xor vVectorZero), point: (vPoint xor vPoint), pointZero: (vPoint xor vPointZero), list: (vPoint xor vList), map: (vPoint xor vMap), dice: (vPoint xor vDice))
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
              value: false
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: false
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: true
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
              value: true
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
              value: true
          - name: "textOne"
            value:
              type: ":Boolean"
              value: false
          - name: "textZero"
            value:
              type: ":Boolean"
              value: true
          - name: "textInvalid"
            value:
              type: ":Boolean"
              value: true
          - name: "textEmpty"
            value:
              type: ":Boolean"
              value: true
          - name: "tagTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "tagFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "tagTrueUpper"
            value:
              type: ":Boolean"
              value: true
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
              value: true
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: false
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: false
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: true
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

## Test: xor pointZero

This runtime case exercises “xor pointZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0088
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor pointZero.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogiccj
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
  emit Done(nothing: (vPointZero xor vNothing), booleanTrue: (vPointZero xor vBooleanTrue), booleanFalse: (vPointZero xor vBooleanFalse), integerOne: (vPointZero xor vIntegerOne), integerZero: (vPointZero xor vIntegerZero), floatPositive: (vPointZero xor vFloatPositive), percentagePositive: (vPointZero xor vPercentagePositive), percentageZero: (vPointZero xor vPercentageZero), textTrue: (vPointZero xor vTextTrue), textTrueUpper: (vPointZero xor vTextTrueUpper), textFalse: (vPointZero xor vTextFalse), textOne: (vPointZero xor vTextOne), textZero: (vPointZero xor vTextZero), textInvalid: (vPointZero xor vTextInvalid), textEmpty: (vPointZero xor vTextEmpty), tagTrue: (vPointZero xor vTagTrue), tagFalse: (vPointZero xor vTagFalse), tagTrueUpper: (vPointZero xor vTagTrueUpper), tagPi: (vPointZero xor vTagPi), tagPhi: (vPointZero xor vTagPhi), tagInfinity: (vPointZero xor vTagInfinity), tagNan: (vPointZero xor vTagNan), tagCustom: (vPointZero xor vTagCustom), vector: (vPointZero xor vVector), vectorZero: (vPointZero xor vVectorZero), point: (vPointZero xor vPoint), pointZero: (vPointZero xor vPointZero), list: (vPointZero xor vList), map: (vPointZero xor vMap), dice: (vPointZero xor vDice))
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

## Test: xor list

This runtime case exercises “xor list” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0089
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor list.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicck
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
  emit Done(nothing: (vList xor vNothing), booleanTrue: (vList xor vBooleanTrue), booleanFalse: (vList xor vBooleanFalse), integerOne: (vList xor vIntegerOne), integerZero: (vList xor vIntegerZero), floatPositive: (vList xor vFloatPositive), percentagePositive: (vList xor vPercentagePositive), percentageZero: (vList xor vPercentageZero), textTrue: (vList xor vTextTrue), textTrueUpper: (vList xor vTextTrueUpper), textFalse: (vList xor vTextFalse), textOne: (vList xor vTextOne), textZero: (vList xor vTextZero), textInvalid: (vList xor vTextInvalid), textEmpty: (vList xor vTextEmpty), tagTrue: (vList xor vTagTrue), tagFalse: (vList xor vTagFalse), tagTrueUpper: (vList xor vTagTrueUpper), tagPi: (vList xor vTagPi), tagPhi: (vList xor vTagPhi), tagInfinity: (vList xor vTagInfinity), tagNan: (vList xor vTagNan), tagCustom: (vList xor vTagCustom), vector: (vList xor vVector), vectorZero: (vList xor vVectorZero), point: (vList xor vPoint), pointZero: (vList xor vPointZero), list: (vList xor vList), map: (vList xor vMap), dice: (vList xor vDice))
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
              type: ":Nothing"
          - name: "integerOne"
            value:
              type: ":Nothing"
          - name: "integerZero"
            value:
              type: ":Nothing"
          - name: "floatPositive"
            value:
              type: ":Nothing"
          - name: "percentagePositive"
            value:
              type: ":Nothing"
          - name: "percentageZero"
            value:
              type: ":Nothing"
          - name: "textTrue"
            value:
              type: ":Nothing"
          - name: "textTrueUpper"
            value:
              type: ":Nothing"
          - name: "textFalse"
            value:
              type: ":Nothing"
          - name: "textOne"
            value:
              type: ":Nothing"
          - name: "textZero"
            value:
              type: ":Nothing"
          - name: "textInvalid"
            value:
              type: ":Nothing"
          - name: "textEmpty"
            value:
              type: ":Nothing"
          - name: "tagTrue"
            value:
              type: ":Nothing"
          - name: "tagFalse"
            value:
              type: ":Nothing"
          - name: "tagTrueUpper"
            value:
              type: ":Nothing"
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
              type: ":Nothing"
          - name: "tagCustom"
            value:
              type: ":Nothing"
          - name: "vector"
            value:
              type: ":Nothing"
          - name: "vectorZero"
            value:
              type: ":Nothing"
          - name: "point"
            value:
              type: ":Nothing"
          - name: "pointZero"
            value:
              type: ":Nothing"
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

## Test: xor map

This runtime case exercises “xor map” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0090
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor map.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogiccl
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
  emit Done(nothing: (vMap xor vNothing), booleanTrue: (vMap xor vBooleanTrue), booleanFalse: (vMap xor vBooleanFalse), integerOne: (vMap xor vIntegerOne), integerZero: (vMap xor vIntegerZero), floatPositive: (vMap xor vFloatPositive), percentagePositive: (vMap xor vPercentagePositive), percentageZero: (vMap xor vPercentageZero), textTrue: (vMap xor vTextTrue), textTrueUpper: (vMap xor vTextTrueUpper), textFalse: (vMap xor vTextFalse), textOne: (vMap xor vTextOne), textZero: (vMap xor vTextZero), textInvalid: (vMap xor vTextInvalid), textEmpty: (vMap xor vTextEmpty), tagTrue: (vMap xor vTagTrue), tagFalse: (vMap xor vTagFalse), tagTrueUpper: (vMap xor vTagTrueUpper), tagPi: (vMap xor vTagPi), tagPhi: (vMap xor vTagPhi), tagInfinity: (vMap xor vTagInfinity), tagNan: (vMap xor vTagNan), tagCustom: (vMap xor vTagCustom), vector: (vMap xor vVector), vectorZero: (vMap xor vVectorZero), point: (vMap xor vPoint), pointZero: (vMap xor vPointZero), list: (vMap xor vList), map: (vMap xor vMap), dice: (vMap xor vDice))
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
              type: ":Nothing"
          - name: "integerOne"
            value:
              type: ":Nothing"
          - name: "integerZero"
            value:
              type: ":Nothing"
          - name: "floatPositive"
            value:
              type: ":Nothing"
          - name: "percentagePositive"
            value:
              type: ":Nothing"
          - name: "percentageZero"
            value:
              type: ":Nothing"
          - name: "textTrue"
            value:
              type: ":Nothing"
          - name: "textTrueUpper"
            value:
              type: ":Nothing"
          - name: "textFalse"
            value:
              type: ":Nothing"
          - name: "textOne"
            value:
              type: ":Nothing"
          - name: "textZero"
            value:
              type: ":Nothing"
          - name: "textInvalid"
            value:
              type: ":Nothing"
          - name: "textEmpty"
            value:
              type: ":Nothing"
          - name: "tagTrue"
            value:
              type: ":Nothing"
          - name: "tagFalse"
            value:
              type: ":Nothing"
          - name: "tagTrueUpper"
            value:
              type: ":Nothing"
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
              type: ":Nothing"
          - name: "tagCustom"
            value:
              type: ":Nothing"
          - name: "vector"
            value:
              type: ":Nothing"
          - name: "vectorZero"
            value:
              type: ":Nothing"
          - name: "point"
            value:
              type: ":Nothing"
          - name: "pointZero"
            value:
              type: ":Nothing"
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

## Test: xor dice

This runtime case exercises “xor dice” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0091
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "xor dice.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogiccm
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
  emit Done(nothing: (vDice xor vNothing), booleanTrue: (vDice xor vBooleanTrue), booleanFalse: (vDice xor vBooleanFalse), integerOne: (vDice xor vIntegerOne), integerZero: (vDice xor vIntegerZero), floatPositive: (vDice xor vFloatPositive), percentagePositive: (vDice xor vPercentagePositive), percentageZero: (vDice xor vPercentageZero), textTrue: (vDice xor vTextTrue), textTrueUpper: (vDice xor vTextTrueUpper), textFalse: (vDice xor vTextFalse), textOne: (vDice xor vTextOne), textZero: (vDice xor vTextZero), textInvalid: (vDice xor vTextInvalid), textEmpty: (vDice xor vTextEmpty), tagTrue: (vDice xor vTagTrue), tagFalse: (vDice xor vTagFalse), tagTrueUpper: (vDice xor vTagTrueUpper), tagPi: (vDice xor vTagPi), tagPhi: (vDice xor vTagPhi), tagInfinity: (vDice xor vTagInfinity), tagNan: (vDice xor vTagNan), tagCustom: (vDice xor vTagCustom), vector: (vDice xor vVector), vectorZero: (vDice xor vVectorZero), point: (vDice xor vPoint), pointZero: (vDice xor vPointZero), list: (vDice xor vList), map: (vDice xor vMap), dice: (vDice xor vDice))
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
              type: ":Nothing"
          - name: "integerOne"
            value:
              type: ":Nothing"
          - name: "integerZero"
            value:
              type: ":Nothing"
          - name: "floatPositive"
            value:
              type: ":Nothing"
          - name: "percentagePositive"
            value:
              type: ":Nothing"
          - name: "percentageZero"
            value:
              type: ":Nothing"
          - name: "textTrue"
            value:
              type: ":Nothing"
          - name: "textTrueUpper"
            value:
              type: ":Nothing"
          - name: "textFalse"
            value:
              type: ":Nothing"
          - name: "textOne"
            value:
              type: ":Nothing"
          - name: "textZero"
            value:
              type: ":Nothing"
          - name: "textInvalid"
            value:
              type: ":Nothing"
          - name: "textEmpty"
            value:
              type: ":Nothing"
          - name: "tagTrue"
            value:
              type: ":Nothing"
          - name: "tagFalse"
            value:
              type: ":Nothing"
          - name: "tagTrueUpper"
            value:
              type: ":Nothing"
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
              type: ":Nothing"
          - name: "tagCustom"
            value:
              type: ":Nothing"
          - name: "vector"
            value:
              type: ":Nothing"
          - name: "vectorZero"
            value:
              type: ":Nothing"
          - name: "point"
            value:
              type: ":Nothing"
          - name: "pointZero"
            value:
              type: ":Nothing"
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
