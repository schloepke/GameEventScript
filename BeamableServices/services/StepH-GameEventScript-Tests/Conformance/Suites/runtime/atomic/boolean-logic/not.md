---
formatVersion: 1
suiteId: "runtime.atomic.boolean-logic.not"
title: "Boolean Logic — Not"
categories: [conformance]
tags: [migrated-json-v1]
---

# Boolean Logic — Not

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers unary boolean negation and portable truth coercion.

---

## Test: not

This runtime case exercises “not” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0001
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "not.ges"
    program: main
```

### Source code under test

```ges
module AtomicBooleanLogicA
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
  emit Done(nothing: not vNothing, booleanTrue: not vBooleanTrue, booleanFalse: not vBooleanFalse, integerOne: not vIntegerOne, integerZero: not vIntegerZero, floatPositive: not vFloatPositive, percentagePositive: not vPercentagePositive, percentageZero: not vPercentageZero, textTrue: not vTextTrue, textTrueUpper: not vTextTrueUpper, textFalse: not vTextFalse, textOne: not vTextOne, textZero: not vTextZero, textInvalid: not vTextInvalid, textEmpty: not vTextEmpty, tagTrue: not vTagTrue, tagFalse: not vTagFalse, tagTrueUpper: not vTagTrueUpper, tagPi: not vTagPi, tagPhi: not vTagPhi, tagInfinity: not vTagInfinity, tagNan: not vTagNan, tagCustom: not vTagCustom, vector: not vVector, vectorZero: not vVectorZero, point: not vPoint, pointZero: not vPointZero, list: not vList, map: not vMap, dice: not vDice)
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
