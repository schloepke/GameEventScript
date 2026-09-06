---
formatVersion: 1
suiteId: "runtime.atomic.boolean-logic.implies-scalar-and-text"
title: "Boolean Logic — Implies Scalars and Text"
categories: [conformance]
---

# Boolean Logic — Implies Scalars and Text

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers implies for nothing, boolean, numeric, percentage, and text operands.

---

## Test: implies nothing

This runtime case exercises “implies nothing” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0092
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies nothing.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogiccn
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
  emit Done(nothing: (vNothing -> vNothing), booleanTrue: (vNothing -> vBooleanTrue), booleanFalse: (vNothing -> vBooleanFalse), integerOne: (vNothing -> vIntegerOne), integerZero: (vNothing -> vIntegerZero), floatPositive: (vNothing -> vFloatPositive), percentagePositive: (vNothing -> vPercentagePositive), percentageZero: (vNothing -> vPercentageZero), textTrue: (vNothing -> vTextTrue), textTrueUpper: (vNothing -> vTextTrueUpper), textFalse: (vNothing -> vTextFalse), textOne: (vNothing -> vTextOne), textZero: (vNothing -> vTextZero), textInvalid: (vNothing -> vTextInvalid), textEmpty: (vNothing -> vTextEmpty), tagTrue: (vNothing -> vTagTrue), tagFalse: (vNothing -> vTagFalse), tagTrueUpper: (vNothing -> vTagTrueUpper), tagPi: (vNothing -> vTagPi), tagPhi: (vNothing -> vTagPhi), tagInfinity: (vNothing -> vTagInfinity), tagNan: (vNothing -> vTagNan), tagCustom: (vNothing -> vTagCustom), vector: (vNothing -> vVector), vectorZero: (vNothing -> vVectorZero), point: (vNothing -> vPoint), pointZero: (vNothing -> vPointZero), list: (vNothing -> vList), map: (vNothing -> vMap), dice: (vNothing -> vDice))
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
              type: ":Nothing"
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":Nothing"
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
              type: ":Nothing"
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
              type: ":Nothing"
          - name: "textOne"
            value:
              type: ":Boolean"
              value: true
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
              type: ":Nothing"
          - name: "tagCustom"
            value:
              type: ":Nothing"
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":Nothing"
          - name: "point"
            value:
              type: ":Boolean"
              value: true
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

## Test: implies booleanTrue

This runtime case exercises “implies booleanTrue” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0093
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies booleanTrue.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicco
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
  emit Done(nothing: (vBooleanTrue -> vNothing), booleanTrue: (vBooleanTrue -> vBooleanTrue), booleanFalse: (vBooleanTrue -> vBooleanFalse), integerOne: (vBooleanTrue -> vIntegerOne), integerZero: (vBooleanTrue -> vIntegerZero), floatPositive: (vBooleanTrue -> vFloatPositive), percentagePositive: (vBooleanTrue -> vPercentagePositive), percentageZero: (vBooleanTrue -> vPercentageZero), textTrue: (vBooleanTrue -> vTextTrue), textTrueUpper: (vBooleanTrue -> vTextTrueUpper), textFalse: (vBooleanTrue -> vTextFalse), textOne: (vBooleanTrue -> vTextOne), textZero: (vBooleanTrue -> vTextZero), textInvalid: (vBooleanTrue -> vTextInvalid), textEmpty: (vBooleanTrue -> vTextEmpty), tagTrue: (vBooleanTrue -> vTagTrue), tagFalse: (vBooleanTrue -> vTagFalse), tagTrueUpper: (vBooleanTrue -> vTagTrueUpper), tagPi: (vBooleanTrue -> vTagPi), tagPhi: (vBooleanTrue -> vTagPhi), tagInfinity: (vBooleanTrue -> vTagInfinity), tagNan: (vBooleanTrue -> vTagNan), tagCustom: (vBooleanTrue -> vTagCustom), vector: (vBooleanTrue -> vVector), vectorZero: (vBooleanTrue -> vVectorZero), point: (vBooleanTrue -> vPoint), pointZero: (vBooleanTrue -> vPointZero), list: (vBooleanTrue -> vList), map: (vBooleanTrue -> vMap), dice: (vBooleanTrue -> vDice))
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

## Test: implies booleanFalse

This runtime case exercises “implies booleanFalse” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0094
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies booleanFalse.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogiccp
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
  emit Done(nothing: (vBooleanFalse -> vNothing), booleanTrue: (vBooleanFalse -> vBooleanTrue), booleanFalse: (vBooleanFalse -> vBooleanFalse), integerOne: (vBooleanFalse -> vIntegerOne), integerZero: (vBooleanFalse -> vIntegerZero), floatPositive: (vBooleanFalse -> vFloatPositive), percentagePositive: (vBooleanFalse -> vPercentagePositive), percentageZero: (vBooleanFalse -> vPercentageZero), textTrue: (vBooleanFalse -> vTextTrue), textTrueUpper: (vBooleanFalse -> vTextTrueUpper), textFalse: (vBooleanFalse -> vTextFalse), textOne: (vBooleanFalse -> vTextOne), textZero: (vBooleanFalse -> vTextZero), textInvalid: (vBooleanFalse -> vTextInvalid), textEmpty: (vBooleanFalse -> vTextEmpty), tagTrue: (vBooleanFalse -> vTagTrue), tagFalse: (vBooleanFalse -> vTagFalse), tagTrueUpper: (vBooleanFalse -> vTagTrueUpper), tagPi: (vBooleanFalse -> vTagPi), tagPhi: (vBooleanFalse -> vTagPhi), tagInfinity: (vBooleanFalse -> vTagInfinity), tagNan: (vBooleanFalse -> vTagNan), tagCustom: (vBooleanFalse -> vTagCustom), vector: (vBooleanFalse -> vVector), vectorZero: (vBooleanFalse -> vVectorZero), point: (vBooleanFalse -> vPoint), pointZero: (vBooleanFalse -> vPointZero), list: (vBooleanFalse -> vList), map: (vBooleanFalse -> vMap), dice: (vBooleanFalse -> vDice))
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
              value: true
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: true
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
              value: true
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
              value: true
          - name: "textOne"
            value:
              type: ":Boolean"
              value: true
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
              value: true
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: implies integerOne

This runtime case exercises “implies integerOne” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0095
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies integerOne.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogiccq
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
  emit Done(nothing: (vIntegerOne -> vNothing), booleanTrue: (vIntegerOne -> vBooleanTrue), booleanFalse: (vIntegerOne -> vBooleanFalse), integerOne: (vIntegerOne -> vIntegerOne), integerZero: (vIntegerOne -> vIntegerZero), floatPositive: (vIntegerOne -> vFloatPositive), percentagePositive: (vIntegerOne -> vPercentagePositive), percentageZero: (vIntegerOne -> vPercentageZero), textTrue: (vIntegerOne -> vTextTrue), textTrueUpper: (vIntegerOne -> vTextTrueUpper), textFalse: (vIntegerOne -> vTextFalse), textOne: (vIntegerOne -> vTextOne), textZero: (vIntegerOne -> vTextZero), textInvalid: (vIntegerOne -> vTextInvalid), textEmpty: (vIntegerOne -> vTextEmpty), tagTrue: (vIntegerOne -> vTagTrue), tagFalse: (vIntegerOne -> vTagFalse), tagTrueUpper: (vIntegerOne -> vTagTrueUpper), tagPi: (vIntegerOne -> vTagPi), tagPhi: (vIntegerOne -> vTagPhi), tagInfinity: (vIntegerOne -> vTagInfinity), tagNan: (vIntegerOne -> vTagNan), tagCustom: (vIntegerOne -> vTagCustom), vector: (vIntegerOne -> vVector), vectorZero: (vIntegerOne -> vVectorZero), point: (vIntegerOne -> vPoint), pointZero: (vIntegerOne -> vPointZero), list: (vIntegerOne -> vList), map: (vIntegerOne -> vMap), dice: (vIntegerOne -> vDice))
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

## Test: implies integerZero

This runtime case exercises “implies integerZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0096
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies integerZero.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogiccr
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
  emit Done(nothing: (vIntegerZero -> vNothing), booleanTrue: (vIntegerZero -> vBooleanTrue), booleanFalse: (vIntegerZero -> vBooleanFalse), integerOne: (vIntegerZero -> vIntegerOne), integerZero: (vIntegerZero -> vIntegerZero), floatPositive: (vIntegerZero -> vFloatPositive), percentagePositive: (vIntegerZero -> vPercentagePositive), percentageZero: (vIntegerZero -> vPercentageZero), textTrue: (vIntegerZero -> vTextTrue), textTrueUpper: (vIntegerZero -> vTextTrueUpper), textFalse: (vIntegerZero -> vTextFalse), textOne: (vIntegerZero -> vTextOne), textZero: (vIntegerZero -> vTextZero), textInvalid: (vIntegerZero -> vTextInvalid), textEmpty: (vIntegerZero -> vTextEmpty), tagTrue: (vIntegerZero -> vTagTrue), tagFalse: (vIntegerZero -> vTagFalse), tagTrueUpper: (vIntegerZero -> vTagTrueUpper), tagPi: (vIntegerZero -> vTagPi), tagPhi: (vIntegerZero -> vTagPhi), tagInfinity: (vIntegerZero -> vTagInfinity), tagNan: (vIntegerZero -> vTagNan), tagCustom: (vIntegerZero -> vTagCustom), vector: (vIntegerZero -> vVector), vectorZero: (vIntegerZero -> vVectorZero), point: (vIntegerZero -> vPoint), pointZero: (vIntegerZero -> vPointZero), list: (vIntegerZero -> vList), map: (vIntegerZero -> vMap), dice: (vIntegerZero -> vDice))
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
              value: true
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: true
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
              value: true
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
              value: true
          - name: "textOne"
            value:
              type: ":Boolean"
              value: true
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
              value: true
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: implies floatPositive

This runtime case exercises “implies floatPositive” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0097
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies floatPositive.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogiccs
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
  emit Done(nothing: (vFloatPositive -> vNothing), booleanTrue: (vFloatPositive -> vBooleanTrue), booleanFalse: (vFloatPositive -> vBooleanFalse), integerOne: (vFloatPositive -> vIntegerOne), integerZero: (vFloatPositive -> vIntegerZero), floatPositive: (vFloatPositive -> vFloatPositive), percentagePositive: (vFloatPositive -> vPercentagePositive), percentageZero: (vFloatPositive -> vPercentageZero), textTrue: (vFloatPositive -> vTextTrue), textTrueUpper: (vFloatPositive -> vTextTrueUpper), textFalse: (vFloatPositive -> vTextFalse), textOne: (vFloatPositive -> vTextOne), textZero: (vFloatPositive -> vTextZero), textInvalid: (vFloatPositive -> vTextInvalid), textEmpty: (vFloatPositive -> vTextEmpty), tagTrue: (vFloatPositive -> vTagTrue), tagFalse: (vFloatPositive -> vTagFalse), tagTrueUpper: (vFloatPositive -> vTagTrueUpper), tagPi: (vFloatPositive -> vTagPi), tagPhi: (vFloatPositive -> vTagPhi), tagInfinity: (vFloatPositive -> vTagInfinity), tagNan: (vFloatPositive -> vTagNan), tagCustom: (vFloatPositive -> vTagCustom), vector: (vFloatPositive -> vVector), vectorZero: (vFloatPositive -> vVectorZero), point: (vFloatPositive -> vPoint), pointZero: (vFloatPositive -> vPointZero), list: (vFloatPositive -> vList), map: (vFloatPositive -> vMap), dice: (vFloatPositive -> vDice))
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

## Test: implies percentagePositive

This runtime case exercises “implies percentagePositive” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0098
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies percentagePositive.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicct
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
  emit Done(nothing: (vPercentagePositive -> vNothing), booleanTrue: (vPercentagePositive -> vBooleanTrue), booleanFalse: (vPercentagePositive -> vBooleanFalse), integerOne: (vPercentagePositive -> vIntegerOne), integerZero: (vPercentagePositive -> vIntegerZero), floatPositive: (vPercentagePositive -> vFloatPositive), percentagePositive: (vPercentagePositive -> vPercentagePositive), percentageZero: (vPercentagePositive -> vPercentageZero), textTrue: (vPercentagePositive -> vTextTrue), textTrueUpper: (vPercentagePositive -> vTextTrueUpper), textFalse: (vPercentagePositive -> vTextFalse), textOne: (vPercentagePositive -> vTextOne), textZero: (vPercentagePositive -> vTextZero), textInvalid: (vPercentagePositive -> vTextInvalid), textEmpty: (vPercentagePositive -> vTextEmpty), tagTrue: (vPercentagePositive -> vTagTrue), tagFalse: (vPercentagePositive -> vTagFalse), tagTrueUpper: (vPercentagePositive -> vTagTrueUpper), tagPi: (vPercentagePositive -> vTagPi), tagPhi: (vPercentagePositive -> vTagPhi), tagInfinity: (vPercentagePositive -> vTagInfinity), tagNan: (vPercentagePositive -> vTagNan), tagCustom: (vPercentagePositive -> vTagCustom), vector: (vPercentagePositive -> vVector), vectorZero: (vPercentagePositive -> vVectorZero), point: (vPercentagePositive -> vPoint), pointZero: (vPercentagePositive -> vPointZero), list: (vPercentagePositive -> vList), map: (vPercentagePositive -> vMap), dice: (vPercentagePositive -> vDice))
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

## Test: implies percentageZero

This runtime case exercises “implies percentageZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0099
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies percentageZero.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogiccu
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
  emit Done(nothing: (vPercentageZero -> vNothing), booleanTrue: (vPercentageZero -> vBooleanTrue), booleanFalse: (vPercentageZero -> vBooleanFalse), integerOne: (vPercentageZero -> vIntegerOne), integerZero: (vPercentageZero -> vIntegerZero), floatPositive: (vPercentageZero -> vFloatPositive), percentagePositive: (vPercentageZero -> vPercentagePositive), percentageZero: (vPercentageZero -> vPercentageZero), textTrue: (vPercentageZero -> vTextTrue), textTrueUpper: (vPercentageZero -> vTextTrueUpper), textFalse: (vPercentageZero -> vTextFalse), textOne: (vPercentageZero -> vTextOne), textZero: (vPercentageZero -> vTextZero), textInvalid: (vPercentageZero -> vTextInvalid), textEmpty: (vPercentageZero -> vTextEmpty), tagTrue: (vPercentageZero -> vTagTrue), tagFalse: (vPercentageZero -> vTagFalse), tagTrueUpper: (vPercentageZero -> vTagTrueUpper), tagPi: (vPercentageZero -> vTagPi), tagPhi: (vPercentageZero -> vTagPhi), tagInfinity: (vPercentageZero -> vTagInfinity), tagNan: (vPercentageZero -> vTagNan), tagCustom: (vPercentageZero -> vTagCustom), vector: (vPercentageZero -> vVector), vectorZero: (vPercentageZero -> vVectorZero), point: (vPercentageZero -> vPoint), pointZero: (vPercentageZero -> vPointZero), list: (vPercentageZero -> vList), map: (vPercentageZero -> vMap), dice: (vPercentageZero -> vDice))
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
              value: true
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: true
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
              value: true
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
              value: true
          - name: "textOne"
            value:
              type: ":Boolean"
              value: true
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
              value: true
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: implies textTrue

This runtime case exercises “implies textTrue” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0100
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies textTrue.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogiccv
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
  emit Done(nothing: (vTextTrue -> vNothing), booleanTrue: (vTextTrue -> vBooleanTrue), booleanFalse: (vTextTrue -> vBooleanFalse), integerOne: (vTextTrue -> vIntegerOne), integerZero: (vTextTrue -> vIntegerZero), floatPositive: (vTextTrue -> vFloatPositive), percentagePositive: (vTextTrue -> vPercentagePositive), percentageZero: (vTextTrue -> vPercentageZero), textTrue: (vTextTrue -> vTextTrue), textTrueUpper: (vTextTrue -> vTextTrueUpper), textFalse: (vTextTrue -> vTextFalse), textOne: (vTextTrue -> vTextOne), textZero: (vTextTrue -> vTextZero), textInvalid: (vTextTrue -> vTextInvalid), textEmpty: (vTextTrue -> vTextEmpty), tagTrue: (vTextTrue -> vTagTrue), tagFalse: (vTextTrue -> vTagFalse), tagTrueUpper: (vTextTrue -> vTagTrueUpper), tagPi: (vTextTrue -> vTagPi), tagPhi: (vTextTrue -> vTagPhi), tagInfinity: (vTextTrue -> vTagInfinity), tagNan: (vTextTrue -> vTagNan), tagCustom: (vTextTrue -> vTagCustom), vector: (vTextTrue -> vVector), vectorZero: (vTextTrue -> vVectorZero), point: (vTextTrue -> vPoint), pointZero: (vTextTrue -> vPointZero), list: (vTextTrue -> vList), map: (vTextTrue -> vMap), dice: (vTextTrue -> vDice))
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

## Test: implies textTrueUpper

This runtime case exercises “implies textTrueUpper” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0101
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies textTrueUpper.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogiccw
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
  emit Done(nothing: (vTextTrueUpper -> vNothing), booleanTrue: (vTextTrueUpper -> vBooleanTrue), booleanFalse: (vTextTrueUpper -> vBooleanFalse), integerOne: (vTextTrueUpper -> vIntegerOne), integerZero: (vTextTrueUpper -> vIntegerZero), floatPositive: (vTextTrueUpper -> vFloatPositive), percentagePositive: (vTextTrueUpper -> vPercentagePositive), percentageZero: (vTextTrueUpper -> vPercentageZero), textTrue: (vTextTrueUpper -> vTextTrue), textTrueUpper: (vTextTrueUpper -> vTextTrueUpper), textFalse: (vTextTrueUpper -> vTextFalse), textOne: (vTextTrueUpper -> vTextOne), textZero: (vTextTrueUpper -> vTextZero), textInvalid: (vTextTrueUpper -> vTextInvalid), textEmpty: (vTextTrueUpper -> vTextEmpty), tagTrue: (vTextTrueUpper -> vTagTrue), tagFalse: (vTextTrueUpper -> vTagFalse), tagTrueUpper: (vTextTrueUpper -> vTagTrueUpper), tagPi: (vTextTrueUpper -> vTagPi), tagPhi: (vTextTrueUpper -> vTagPhi), tagInfinity: (vTextTrueUpper -> vTagInfinity), tagNan: (vTextTrueUpper -> vTagNan), tagCustom: (vTextTrueUpper -> vTagCustom), vector: (vTextTrueUpper -> vVector), vectorZero: (vTextTrueUpper -> vVectorZero), point: (vTextTrueUpper -> vPoint), pointZero: (vTextTrueUpper -> vPointZero), list: (vTextTrueUpper -> vList), map: (vTextTrueUpper -> vMap), dice: (vTextTrueUpper -> vDice))
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

## Test: implies textFalse

This runtime case exercises “implies textFalse” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0102
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies textFalse.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogiccx
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
  emit Done(nothing: (vTextFalse -> vNothing), booleanTrue: (vTextFalse -> vBooleanTrue), booleanFalse: (vTextFalse -> vBooleanFalse), integerOne: (vTextFalse -> vIntegerOne), integerZero: (vTextFalse -> vIntegerZero), floatPositive: (vTextFalse -> vFloatPositive), percentagePositive: (vTextFalse -> vPercentagePositive), percentageZero: (vTextFalse -> vPercentageZero), textTrue: (vTextFalse -> vTextTrue), textTrueUpper: (vTextFalse -> vTextTrueUpper), textFalse: (vTextFalse -> vTextFalse), textOne: (vTextFalse -> vTextOne), textZero: (vTextFalse -> vTextZero), textInvalid: (vTextFalse -> vTextInvalid), textEmpty: (vTextFalse -> vTextEmpty), tagTrue: (vTextFalse -> vTagTrue), tagFalse: (vTextFalse -> vTagFalse), tagTrueUpper: (vTextFalse -> vTagTrueUpper), tagPi: (vTextFalse -> vTagPi), tagPhi: (vTextFalse -> vTagPhi), tagInfinity: (vTextFalse -> vTagInfinity), tagNan: (vTextFalse -> vTagNan), tagCustom: (vTextFalse -> vTagCustom), vector: (vTextFalse -> vVector), vectorZero: (vTextFalse -> vVectorZero), point: (vTextFalse -> vPoint), pointZero: (vTextFalse -> vPointZero), list: (vTextFalse -> vList), map: (vTextFalse -> vMap), dice: (vTextFalse -> vDice))
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
              value: true
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: true
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
              value: true
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
              value: true
          - name: "textOne"
            value:
              type: ":Boolean"
              value: true
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
              value: true
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: implies textOne

This runtime case exercises “implies textOne” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0103
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies textOne.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogiccy
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
  emit Done(nothing: (vTextOne -> vNothing), booleanTrue: (vTextOne -> vBooleanTrue), booleanFalse: (vTextOne -> vBooleanFalse), integerOne: (vTextOne -> vIntegerOne), integerZero: (vTextOne -> vIntegerZero), floatPositive: (vTextOne -> vFloatPositive), percentagePositive: (vTextOne -> vPercentagePositive), percentageZero: (vTextOne -> vPercentageZero), textTrue: (vTextOne -> vTextTrue), textTrueUpper: (vTextOne -> vTextTrueUpper), textFalse: (vTextOne -> vTextFalse), textOne: (vTextOne -> vTextOne), textZero: (vTextOne -> vTextZero), textInvalid: (vTextOne -> vTextInvalid), textEmpty: (vTextOne -> vTextEmpty), tagTrue: (vTextOne -> vTagTrue), tagFalse: (vTextOne -> vTagFalse), tagTrueUpper: (vTextOne -> vTagTrueUpper), tagPi: (vTextOne -> vTagPi), tagPhi: (vTextOne -> vTagPhi), tagInfinity: (vTextOne -> vTagInfinity), tagNan: (vTextOne -> vTagNan), tagCustom: (vTextOne -> vTagCustom), vector: (vTextOne -> vVector), vectorZero: (vTextOne -> vVectorZero), point: (vTextOne -> vPoint), pointZero: (vTextOne -> vPointZero), list: (vTextOne -> vList), map: (vTextOne -> vMap), dice: (vTextOne -> vDice))
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

## Test: implies textZero

This runtime case exercises “implies textZero” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0104
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies textZero.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogiccz
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
  emit Done(nothing: (vTextZero -> vNothing), booleanTrue: (vTextZero -> vBooleanTrue), booleanFalse: (vTextZero -> vBooleanFalse), integerOne: (vTextZero -> vIntegerOne), integerZero: (vTextZero -> vIntegerZero), floatPositive: (vTextZero -> vFloatPositive), percentagePositive: (vTextZero -> vPercentagePositive), percentageZero: (vTextZero -> vPercentageZero), textTrue: (vTextZero -> vTextTrue), textTrueUpper: (vTextZero -> vTextTrueUpper), textFalse: (vTextZero -> vTextFalse), textOne: (vTextZero -> vTextOne), textZero: (vTextZero -> vTextZero), textInvalid: (vTextZero -> vTextInvalid), textEmpty: (vTextZero -> vTextEmpty), tagTrue: (vTextZero -> vTagTrue), tagFalse: (vTextZero -> vTagFalse), tagTrueUpper: (vTextZero -> vTagTrueUpper), tagPi: (vTextZero -> vTagPi), tagPhi: (vTextZero -> vTagPhi), tagInfinity: (vTextZero -> vTagInfinity), tagNan: (vTextZero -> vTagNan), tagCustom: (vTextZero -> vTagCustom), vector: (vTextZero -> vVector), vectorZero: (vTextZero -> vVectorZero), point: (vTextZero -> vPoint), pointZero: (vTextZero -> vPointZero), list: (vTextZero -> vList), map: (vTextZero -> vMap), dice: (vTextZero -> vDice))
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
              value: true
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: true
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
              value: true
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
              value: true
          - name: "textOne"
            value:
              type: ":Boolean"
              value: true
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
              value: true
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: implies textInvalid

This runtime case exercises “implies textInvalid” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0105
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies textInvalid.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicda
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
  emit Done(nothing: (vTextInvalid -> vNothing), booleanTrue: (vTextInvalid -> vBooleanTrue), booleanFalse: (vTextInvalid -> vBooleanFalse), integerOne: (vTextInvalid -> vIntegerOne), integerZero: (vTextInvalid -> vIntegerZero), floatPositive: (vTextInvalid -> vFloatPositive), percentagePositive: (vTextInvalid -> vPercentagePositive), percentageZero: (vTextInvalid -> vPercentageZero), textTrue: (vTextInvalid -> vTextTrue), textTrueUpper: (vTextInvalid -> vTextTrueUpper), textFalse: (vTextInvalid -> vTextFalse), textOne: (vTextInvalid -> vTextOne), textZero: (vTextInvalid -> vTextZero), textInvalid: (vTextInvalid -> vTextInvalid), textEmpty: (vTextInvalid -> vTextEmpty), tagTrue: (vTextInvalid -> vTagTrue), tagFalse: (vTextInvalid -> vTagFalse), tagTrueUpper: (vTextInvalid -> vTagTrueUpper), tagPi: (vTextInvalid -> vTagPi), tagPhi: (vTextInvalid -> vTagPhi), tagInfinity: (vTextInvalid -> vTagInfinity), tagNan: (vTextInvalid -> vTagNan), tagCustom: (vTextInvalid -> vTagCustom), vector: (vTextInvalid -> vVector), vectorZero: (vTextInvalid -> vVectorZero), point: (vTextInvalid -> vPoint), pointZero: (vTextInvalid -> vPointZero), list: (vTextInvalid -> vList), map: (vTextInvalid -> vMap), dice: (vTextInvalid -> vDice))
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
              value: true
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: true
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
              value: true
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
              value: true
          - name: "textOne"
            value:
              type: ":Boolean"
              value: true
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
              value: true
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: implies textEmpty

This runtime case exercises “implies textEmpty” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0106
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "implies textEmpty.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanlogicdb
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
  emit Done(nothing: (vTextEmpty -> vNothing), booleanTrue: (vTextEmpty -> vBooleanTrue), booleanFalse: (vTextEmpty -> vBooleanFalse), integerOne: (vTextEmpty -> vIntegerOne), integerZero: (vTextEmpty -> vIntegerZero), floatPositive: (vTextEmpty -> vFloatPositive), percentagePositive: (vTextEmpty -> vPercentagePositive), percentageZero: (vTextEmpty -> vPercentageZero), textTrue: (vTextEmpty -> vTextTrue), textTrueUpper: (vTextEmpty -> vTextTrueUpper), textFalse: (vTextEmpty -> vTextFalse), textOne: (vTextEmpty -> vTextOne), textZero: (vTextEmpty -> vTextZero), textInvalid: (vTextEmpty -> vTextInvalid), textEmpty: (vTextEmpty -> vTextEmpty), tagTrue: (vTextEmpty -> vTagTrue), tagFalse: (vTextEmpty -> vTagFalse), tagTrueUpper: (vTextEmpty -> vTagTrueUpper), tagPi: (vTextEmpty -> vTagPi), tagPhi: (vTextEmpty -> vTagPhi), tagInfinity: (vTextEmpty -> vTagInfinity), tagNan: (vTextEmpty -> vTagNan), tagCustom: (vTextEmpty -> vTagCustom), vector: (vTextEmpty -> vVector), vectorZero: (vTextEmpty -> vVectorZero), point: (vTextEmpty -> vPoint), pointZero: (vTextEmpty -> vPointZero), list: (vTextEmpty -> vList), map: (vTextEmpty -> vMap), dice: (vTextEmpty -> vDice))
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
              value: true
          - name: "booleanTrue"
            value:
              type: ":Boolean"
              value: true
          - name: "booleanFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "integerOne"
            value:
              type: ":Boolean"
              value: true
          - name: "integerZero"
            value:
              type: ":Boolean"
              value: true
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
              value: true
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
              value: true
          - name: "textOne"
            value:
              type: ":Boolean"
              value: true
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
              value: true
          - name: "tagCustom"
            value:
              type: ":Boolean"
              value: true
          - name: "vector"
            value:
              type: ":Boolean"
              value: true
          - name: "vectorZero"
            value:
              type: ":Boolean"
              value: true
          - name: "point"
            value:
              type: ":Boolean"
              value: true
          - name: "pointZero"
            value:
              type: ":Boolean"
              value: true
          - name: "list"
            value:
              type: ":Boolean"
              value: true
          - name: "map"
            value:
              type: ":Boolean"
              value: true
          - name: "dice"
            value:
              type: ":Boolean"
              value: true
```
