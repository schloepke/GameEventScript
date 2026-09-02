---
formatVersion: 1
suiteId: "runtime.types-and-values.text-collections-and-checks"
title: "Types and Values — Text, Collections, and Checks"
categories: [conformance]
tags: [migrated-json-v1]
---

# Types and Values — Text, Collections, and Checks

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers literals, text, collection access, containment, defaults, presence, and portable type checks.

---

## Test: inline literals escaped quotes and numeric helpers

This runtime case exercises “inline literals escaped quotes and numeric helpers” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0019
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "inline literals escaped quotes and numeric helpers.ges"
    program: main
```

### Source code under test

```ges
on Start(opt, missingValue, value, meterValue) {
  let text be 'Hello ''World'', I''m here'
  let doubleText be "didn't say ""stop"""
  let myList be [1, 2, 3]
  let myValues be [name: 'Hello', position: 1]
  let emptyValues be [:]
  let distanceValue be abs (0 - 12.5)
  let naturalOne be ln e
  let naturalZero be ln 0
  let naturalNegative be ln (0 - 1)
  let naturalInfinity be ln infinity
  let naturalInvalidTag be ln #nan
  let naturalNothing be ln missingValue
  let boundedHigh be clamp 120 between 0 and 99
  let boundedLow be clamp (0 - 3) between 0 and 99
  let boundedNothingValue be clamp missingValue between 0 and 99
  let boundedNothingMin be clamp 10 between missingValue and 99
  let boundedInvalid be clamp 'hello' between 0 and 99
  let boundedInfinity be clamp infinity between 0 and 99
  let highest be max of 4 and 9 and 2;
  let lowest be min of 4 and 9 and 2;
  emit Done(text: text, doubleText: doubleText, listLen: myList[:count], name: myValues.name, position: myValues.position, emptyLen: emptyValues[:count], distance: distanceValue, naturalOne: naturalOne, naturalZero: naturalZero, naturalNegative: naturalNegative, naturalInfinity: naturalInfinity, naturalInvalidTag: naturalInvalidTag, naturalNothing: naturalNothing, naturalRuntime: ln value > 1, naturalUnit: ln meterValue, boundedHigh: boundedHigh, boundedLow: boundedLow, boundedNothingValue: boundedNothingValue, boundedNothingMin: boundedNothingMin, boundedInvalid: boundedInvalid, boundedInfinity: boundedInfinity, highest: highest, lowest: lowest, presenceFallback: opt default 10, nothingFallback: missingValue default 20)
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
        - name: "opt"
          value:
            type: ":nothing"
        - name: "missingValue"
          value:
            type: ":nothing"
        - name: "value"
          value:
            type: ":integer"
            value: "3"
        - name: "meterValue"
          value:
            type: ":float"
            value: "5"
            unit: ":meter"
    local:
      - name: "Done"
        args:
          - name: "text"
            value:
              type: ":text"
              value: "Hello \u0027World\u0027, I\u0027m here"
          - name: "doubleText"
            value:
              type: ":text"
              value: "didn\u0027t say \u0022stop\u0022"
          - name: "listLen"
            value:
              type: ":integer"
              value: "3"
          - name: "name"
            value:
              type: ":text"
              value: "Hello"
          - name: "position"
            value:
              type: ":integer"
              value: "1"
          - name: "emptyLen"
            value:
              type: ":integer"
              value: "0"
          - name: "distance"
            value:
              type: ":float"
              value: "12.5"
          - name: "naturalOne"
            value:
              type: ":float"
              value: "1"
          - name: "naturalZero"
            value:
              type: ":float"
              value: "-Infinity"
          - name: "naturalNegative"
            value:
              type: ":float"
              value: "NaN"
          - name: "naturalInfinity"
            value:
              type: ":float"
              value: "Infinity"
          - name: "naturalInvalidTag"
            value:
              type: ":nothing"
          - name: "naturalNothing"
            value:
              type: ":nothing"
          - name: "naturalRuntime"
            value:
              type: ":boolean"
              value: true
          - name: "naturalUnit"
            value:
              type: ":float"
              value: "NaN"
          - name: "boundedHigh"
            value:
              type: ":float"
              value: "99"
          - name: "boundedLow"
            value:
              type: ":float"
              value: "0"
          - name: "boundedNothingValue"
            value:
              type: ":nothing"
          - name: "boundedNothingMin"
            value:
              type: ":nothing"
          - name: "boundedInvalid"
            value:
              type: ":float"
              value: "NaN"
          - name: "boundedInfinity"
            value:
              type: ":float"
              value: "99"
          - name: "highest"
            value:
              type: ":integer"
              value: "9"
          - name: "lowest"
            value:
              type: ":integer"
              value: "2"
          - name: "presenceFallback"
            value:
              type: ":integer"
              value: "10"
          - name: "nothingFallback"
            value:
              type: ":integer"
              value: "20"
```

---

## Test: lookup and empty checks are runtime-visible

This runtime case exercises “lookup and empty checks are runtime-visible” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0020
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "lookup and empty checks are runtime-visible.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let items be [10, 20, 30]
  let unit be [hp: 7, name: 'Knight']
  let emptyList be []
  let invalidNumber as :number be 'abc'
  emit Done(second: items[2], nothing: items[4], hp: unit[#hp], emptyList: emptyList is empty, invalidHasValue: invalidNumber has value, invalidIsEmpty: invalidNumber is empty)
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
          - name: "second"
            value:
              type: ":integer"
              value: "20"
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "hp"
            value:
              type: ":integer"
              value: "7"
          - name: "emptyList"
            value:
              type: ":boolean"
              value: true
          - name: "invalidHasValue"
            value:
              type: ":boolean"
              value: false
          - name: "invalidIsEmpty"
            value:
              type: ":boolean"
              value: true
```

---

## Test: dice like names remain ordinary identifiers

This runtime case exercises “dice like names remain ordinary identifiers” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0021
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "dice like names remain ordinary identifiers.ges"
    program: main
```

### Source code under test

```ges
on Start(d_6) {
  let kept be d_6
  emit Done(value: kept)
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
        - name: "d_6"
          value:
            type: ":integer"
            value: "6"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "6"
```

---

## Test: containment boundary sort and distinct semantics

This runtime case exercises “containment boundary sort and distinct semantics” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0022
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "containment boundary sort and distinct semantics.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let values be [3, 1, 2, 2]
  emit Done(textContains: 'club' in 'battle club', startsList: [1, 2, 3] starts with [1, 2], endsList: [1, 2, 3] ends with [1, 2], hasAll: [1, 2, 3][:contains all [1, 3]], hasAny: [1, 2, 3][:contains any [0, 3]], sorted: values[:sort ascending], distinct: values[:distinct])
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
          - name: "textContains"
            value:
              type: ":boolean"
              value: true
          - name: "startsList"
            value:
              type: ":boolean"
              value: true
          - name: "endsList"
            value:
              type: ":boolean"
              value: false
          - name: "hasAll"
            value:
              type: ":boolean"
              value: true
          - name: "hasAny"
            value:
              type: ":boolean"
              value: true
          - name: "sorted"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "distinct"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
```

---

## Test: one based lookup and dictionary property access

This runtime case exercises “one based lookup and dictionary property access” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0024
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "one based lookup and dictionary property access.ges"
    program: main
```

### Source code under test

```ges
on Start(entry, keyName) {
  let values be [10, 20, 30]
  let tags be ['alpha', 'beta']
  let lookupProperty be #name
  emit Done(first: values[1], third: values[3], zero: values[0], missingIndex: values[4], property: entry.name, textKey: entry['name'], tagKey: entry[#name], dynamicTag: entry[lookupProperty], dynamicText: entry[keyName], missingProperty: entry['missing'], firstTag: tags[1], missingTag: tags[3])
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
        - name: "entry"
          value:
            type: ":map"
            entries:
              - key: "name"
                value:
                  type: ":text"
                  value: "Ada"
        - name: "keyName"
          value:
            type: ":text"
            value: "name"
    local:
      - name: "Done"
        args:
          - name: "first"
            value:
              type: ":integer"
              value: "10"
          - name: "third"
            value:
              type: ":integer"
              value: "30"
          - name: "zero"
            value:
              type: ":nothing"
          - name: "missingIndex"
            value:
              type: ":nothing"
          - name: "property"
            value:
              type: ":text"
              value: "Ada"
          - name: "textKey"
            value:
              type: ":text"
              value: "Ada"
          - name: "tagKey"
            value:
              type: ":text"
              value: "Ada"
          - name: "dynamicTag"
            value:
              type: ":text"
              value: "Ada"
          - name: "dynamicText"
            value:
              type: ":text"
              value: "Ada"
          - name: "missingProperty"
            value:
              type: ":nothing"
          - name: "firstTag"
            value:
              type: ":text"
              value: "alpha"
          - name: "missingTag"
            value:
              type: ":nothing"
```

---

## Test: value checks defaults and presence

This runtime case exercises “value checks defaults and presence” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0025
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "value checks defaults and presence.ges"
    program: main
```

### Source code under test

```ges
on Start(opt, emptyText, emptyList, emptyDict, zeroValue, falseValue, diceValue, nanValue, infinityValue) {
  emit Done(hasOpt: opt has value, hasEmptyText: emptyText has value, hasEmptyList: emptyList has value, hasEmptyDict: emptyDict has value, hasZero: zeroValue has value, hasFalse: falseValue has value, hasNaN: nanValue has value, hasInfinity: infinityValue has value, isEmptyOpt: empty opt, isEmptyText: empty emptyText, isEmptyList: empty emptyList, isEmptyDict: empty emptyDict, isEmptyDice: empty diceValue, isEmptyNaN: empty nanValue, isEmptyInfinity: empty infinityValue, nanIsNothing: nanValue is nothing, notEmptyList: not empty [1], notFalse: not falseValue, optValue: opt default 10, listValue: (emptyList default [1, 2])[1], textValue: emptyText default 'fallback', dictValue: (emptyDict default [name: 'default']).name, missingTextValue: emptyDict['name'] default 'fallback')
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
        - name: "opt"
          value:
            type: ":integer"
            value: "7"
        - name: "emptyText"
          value:
            type: ":text"
            value: ""
        - name: "emptyList"
          value:
            type: ":list"
            items: []
        - name: "emptyDict"
          value:
            type: ":map"
            entries: []
        - name: "zeroValue"
          value:
            type: ":integer"
            value: "0"
        - name: "falseValue"
          value:
            type: ":boolean"
            value: false
        - name: "diceValue"
          value:
            type: ":dice"
            rolls: []
        - name: "nanValue"
          value:
            type: ":float"
            value: "NaN"
        - name: "infinityValue"
          value:
            type: ":float"
            value: "Infinity"
    local:
      - name: "Done"
        args:
          - name: "hasOpt"
            value:
              type: ":boolean"
              value: true
          - name: "hasEmptyText"
            value:
              type: ":boolean"
              value: false
          - name: "hasEmptyList"
            value:
              type: ":boolean"
              value: false
          - name: "hasEmptyDict"
            value:
              type: ":boolean"
              value: false
          - name: "hasZero"
            value:
              type: ":boolean"
              value: true
          - name: "hasFalse"
            value:
              type: ":boolean"
              value: true
          - name: "hasNaN"
            value:
              type: ":boolean"
              value: false
          - name: "hasInfinity"
            value:
              type: ":boolean"
              value: true
          - name: "isEmptyOpt"
            value:
              type: ":boolean"
              value: false
          - name: "isEmptyText"
            value:
              type: ":boolean"
              value: true
          - name: "isEmptyList"
            value:
              type: ":boolean"
              value: true
          - name: "isEmptyDict"
            value:
              type: ":boolean"
              value: true
          - name: "isEmptyDice"
            value:
              type: ":boolean"
              value: true
          - name: "isEmptyNaN"
            value:
              type: ":boolean"
              value: true
          - name: "isEmptyInfinity"
            value:
              type: ":boolean"
              value: false
          - name: "nanIsNothing"
            value:
              type: ":boolean"
              value: true
          - name: "notEmptyList"
            value:
              type: ":boolean"
              value: true
          - name: "notFalse"
            value:
              type: ":boolean"
              value: true
          - name: "optValue"
            value:
              type: ":integer"
              value: "7"
          - name: "listValue"
            value:
              type: ":integer"
              value: "1"
          - name: "textValue"
            value:
              type: ":text"
              value: "fallback"
          - name: "dictValue"
            value:
              type: ":text"
              value: "default"
          - name: "missingTextValue"
            value:
              type: ":text"
              value: "fallback"
```

---

## Test: type conversions length checks and type tags

This runtime case exercises “type conversions length checks and type tags” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0026
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["2", "6", "3", "5"]
sources:
  - name: "type conversions length checks and type tags.ges"
    program: main
```

### Source code under test

```ges
on Start(valueForLen, a, b, c, d, listArg, f, g) {
  let tagOk as :tag be #name
  let numberOk as :number be '12.2'
  let numberFail as :number be 'abc'
  let integerOk as :number be '12.7'
  let booleanOk as :boolean be 'true'
  let listOk as :list be 'ab'
  let diceOk as :dice be [6, 2, 4]
  emit Done(tagOk: tagOk, numberOk: numberOk, numberFail: numberFail, integerOk: integerOk, booleanOk: booleanOk, listLen: listOk[:count], diceFirst: diceOk[1], diceLen: diceOk[:count], textLen: valueForLen[:count], dictLen: [first: 1, second: 2][:count], valueForLenLen: valueForLen[:count], floatLen: 12.5[:count], booleanLen: true[:count], aIsTag: a is :tag, bIsFloat: b is :number, cIsInteger: c is :number, dIsText: d is :text, eIsList: listArg is :list, fIsMap: f is :map, gIsNothing: g is nothing)
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
        - name: "valueForLen"
          value:
            type: ":text"
            value: "x"
        - name: "a"
          value:
            type: ":tag"
            value: "name"
        - name: "b"
          value:
            type: ":float"
            value: "1.5"
        - name: "c"
          value:
            type: ":integer"
            value: "2"
        - name: "d"
          value:
            type: ":text"
            value: "x"
        - name: "listArg"
          value:
            type: ":list"
            items:
              - type: ":integer"
                value: "1"
        - name: "f"
          value:
            type: ":map"
            entries:
              - key: "v"
                value:
                  type: ":integer"
                  value: "1"
        - name: "g"
          value:
            type: ":nothing"
    local:
      - name: "Done"
        args:
          - name: "tagOk"
            value:
              type: ":tag"
              value: "name"
          - name: "numberOk"
            value:
              type: ":float"
              value: "12.2"
          - name: "numberFail"
            value:
              type: ":nothing"
          - name: "integerOk"
            value:
              type: ":float"
              value: "12.7"
          - name: "booleanOk"
            value:
              type: ":boolean"
              value: true
          - name: "listLen"
            value:
              type: ":integer"
              value: "2"
          - name: "diceFirst"
            value:
              type: ":integer"
              value: "6"
          - name: "diceLen"
            value:
              type: ":integer"
              value: "3"
          - name: "textLen"
            value:
              type: ":integer"
              value: "1"
          - name: "dictLen"
            value:
              type: ":integer"
              value: "2"
          - name: "valueForLenLen"
            value:
              type: ":integer"
              value: "1"
          - name: "floatLen"
            value:
              type: ":nothing"
          - name: "booleanLen"
            value:
              type: ":nothing"
          - name: "aIsTag"
            value:
              type: ":boolean"
              value: true
          - name: "bIsFloat"
            value:
              type: ":boolean"
              value: true
          - name: "cIsInteger"
            value:
              type: ":boolean"
              value: true
          - name: "dIsText"
            value:
              type: ":boolean"
              value: true
          - name: "eIsList"
            value:
              type: ":boolean"
              value: true
          - name: "fIsMap"
            value:
              type: ":boolean"
              value: true
          - name: "gIsNothing"
            value:
              type: ":boolean"
              value: true
```

---

## Test: missing invalid and large values remain lenient

This runtime case exercises “missing invalid and large values remain lenient” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0033
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "missing invalid and large values remain lenient.ges"
    program: main
```

### Source code under test

```ges
on Start(item) {
  let nanValue as :number be 'abc'
  let nanPropagated be nanValue + 5
  let nothingValue be nothing
  let nothingPropagated be nothingValue + 5
  let pos be 79228162514264337593543950335 + 1
  let neg be 0 - 79228162514264337593543950335 - 1
  emit Done(nanValue: nanValue, nanPropagated: nanPropagated, nothing: nothingValue, nothingPropagated: nothingPropagated, propertyFromNothing: item.name, pos: pos, neg: neg)
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
        - name: "item"
          value:
            type: ":nothing"
    local:
      - name: "Done"
        args:
          - name: "nanValue"
            value:
              type: ":nothing"
          - name: "nanPropagated"
            value:
              type: ":nothing"
          - name: "nothing"
            value:
              type: ":nothing"
          - name: "nothingPropagated"
            value:
              type: ":nothing"
          - name: "propertyFromNothing"
            value:
              type: ":nothing"
          - name: "pos"
            value:
              type: ":float"
              value: "7.92281625142643e28"
          - name: "neg"
            value:
              type: ":float"
              value: "-7.92281625142643e28"
```

---

## Test: type checks identify primitive custom and first class values

This runtime case exercises “type checks identify primitive custom and first class values” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0035
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "type checks identify primitive custom and first class values.ges"
    program: main
```

### Source code under test

```ges
on Start(custom) {
  let integerValue be 12
  let percentValue be 5%
  let degreeValue be 90°
  let meterValue be 100m
  let secondValue be 15s
  let listValue be [1]
  let dictValue be [name: 'Ada']
  let msg be Ping(value: 1)
  let handler be Ping(value)
  emit Done(intIsInteger: integerValue is :number, intIsFloat: integerValue is :number, percentIsFloat: percentValue is :number, degreeIsFloat: degreeValue is :number, degreeIsDegree: degreeValue is :quantity(°), meterIsMeter: meterValue is :quantity(m), secondIsSecond: secondValue is :quantity(s), textIsText: 'x' is :text, tagIsTag: #ready is :tag, boolIsBoolean: true is :boolean, listIsList: listValue is :list, dictIsMap: dictValue is :map, customIsGauge: custom is :gauge, customIsMap: custom is :map, msgIsMessage: msg is :message, msgIsMap: msg is :map, handlerIsHandler: handler is :handler, missingIsNothing: nothing is nothing)
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
        - name: "custom"
          value:
            type: ":gauge"
            entries:
              - key: "current"
                value:
                  type: ":integer"
                  value: "5"
    local:
      - name: "Done"
        args:
          - name: "intIsInteger"
            value:
              type: ":boolean"
              value: true
          - name: "intIsFloat"
            value:
              type: ":boolean"
              value: true
          - name: "percentIsFloat"
            value:
              type: ":boolean"
              value: true
          - name: "degreeIsFloat"
            value:
              type: ":boolean"
              value: true
          - name: "degreeIsDegree"
            value:
              type: ":boolean"
              value: true
          - name: "meterIsMeter"
            value:
              type: ":boolean"
              value: true
          - name: "secondIsSecond"
            value:
              type: ":boolean"
              value: true
          - name: "textIsText"
            value:
              type: ":boolean"
              value: true
          - name: "tagIsTag"
            value:
              type: ":boolean"
              value: true
          - name: "boolIsBoolean"
            value:
              type: ":boolean"
              value: true
          - name: "listIsList"
            value:
              type: ":boolean"
              value: true
          - name: "dictIsMap"
            value:
              type: ":boolean"
              value: true
          - name: "customIsGauge"
            value:
              type: ":boolean"
              value: true
          - name: "customIsMap"
            value:
              type: ":boolean"
              value: true
          - name: "msgIsMessage"
            value:
              type: ":boolean"
              value: true
          - name: "msgIsMap"
            value:
              type: ":boolean"
              value: false
          - name: "handlerIsHandler"
            value:
              type: ":boolean"
              value: true
          - name: "missingIsNothing"
            value:
              type: ":boolean"
              value: true
```

---

## Test: old unit names remain ordinary tags

This runtime case exercises “old unit names remain ordinary tags” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0037
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "old unit names remain ordinary tags.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Done(meter: #meter, degree: #degree, seconds: #seconds)
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
          - name: "meter"
            value:
              type: ":tag"
              value: "meter"
          - name: "degree"
            value:
              type: ":tag"
              value: "degree"
          - name: "seconds"
            value:
              type: ":tag"
              value: "seconds"
```

---

## Test: unicode text uses scalar length indexing and iteration

This runtime case exercises “unicode text uses scalar length indexing and iteration” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0039
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "unicode text uses scalar length indexing and iteration.ges"
    program: main
```

### Source code under test

```ges
﻿on Start {
  let text be 'A😀é'
  let scalars as :list be text
  emit Done(count: text[:count], first: text[:first], second: text[2], third: text[3], fourth: text[4], last: text[:last], single: '😀'[:single], listCount: scalars[:count], listSecond: scalars[2], hugeIndex: text[9223372036854775807])
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
          - name: "count"
            value:
              type: ":integer"
              value: "4"
          - name: "first"
            value:
              type: ":text"
              value: "A"
          - name: "second"
            value:
              type: ":text"
              value: "\uD83D\uDE00"
          - name: "third"
            value:
              type: ":text"
              value: "e"
          - name: "fourth"
            value:
              type: ":text"
              value: "\u0301"
          - name: "last"
            value:
              type: ":text"
              value: "\u0301"
          - name: "single"
            value:
              type: ":text"
              value: "\uD83D\uDE00"
          - name: "listCount"
            value:
              type: ":integer"
              value: "4"
          - name: "listSecond"
            value:
              type: ":text"
              value: "\uD83D\uDE00"
          - name: "hugeIndex"
            value:
              type: ":nothing"
```
