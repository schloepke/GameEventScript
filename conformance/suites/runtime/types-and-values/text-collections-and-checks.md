---
formatVersion: 1
suiteId: "runtime.types-and-values.text-collections-and-checks"
title: "Types and Values — Text, Collections, and Checks"
categories: [conformance]
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
            type: ":Nothing"
        - name: "missingValue"
          value:
            type: ":Nothing"
        - name: "value"
          value:
            type: ":Number.int64"
            value: "3"
        - name: "meterValue"
          value:
            type: ":Quantity.binary64"
            value: "5"
            unit: ":meter"
    local:
      - name: "Done"
        args:
          - name: "text"
            value:
              type: ":Text"
              value: "Hello \u0027World\u0027, I\u0027m here"
          - name: "doubleText"
            value:
              type: ":Text"
              value: "didn\u0027t say \u0022stop\u0022"
          - name: "listLen"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "name"
            value:
              type: ":Text"
              value: "Hello"
          - name: "position"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "emptyLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "distance"
            value:
              type: ":Number.binary64"
              value: "12.5"
          - name: "naturalOne"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "naturalZero"
            value:
              type: ":Number.binary64"
              value: "-Infinity"
          - name: "naturalNegative"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "naturalInfinity"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "naturalInvalidTag"
            value:
              type: ":Nothing"
          - name: "naturalNothing"
            value:
              type: ":Nothing"
          - name: "naturalRuntime"
            value:
              type: ":Boolean"
              value: true
          - name: "naturalUnit"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "boundedHigh"
            value:
              type: ":Number.int64"
              value: "99"
          - name: "boundedLow"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "boundedNothingValue"
            value:
              type: ":Nothing"
          - name: "boundedNothingMin"
            value:
              type: ":Nothing"
          - name: "boundedInvalid"
            value:
              type: ":Number.binary64"
              value: "NaN"
          - name: "boundedInfinity"
            value:
              type: ":Number.int64"
              value: "99"
          - name: "highest"
            value:
              type: ":Number.int64"
              value: "9"
          - name: "lowest"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "presenceFallback"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "nothingFallback"
            value:
              type: ":Number.int64"
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
  let invalidNumber be ('abc') as :Number
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
              type: ":Number.int64"
              value: "20"
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "hp"
            value:
              type: ":Number.int64"
              value: "7"
          - name: "emptyList"
            value:
              type: ":Boolean"
              value: true
          - name: "invalidHasValue"
            value:
              type: ":Boolean"
              value: false
          - name: "invalidIsEmpty"
            value:
              type: ":Boolean"
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
on Start(d6) {
  let kept be d6
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
        - name: "d6"
          value:
            type: ":Number.int64"
            value: "6"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
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
              type: ":Boolean"
              value: true
          - name: "startsList"
            value:
              type: ":Boolean"
              value: true
          - name: "endsList"
            value:
              type: ":Boolean"
              value: false
          - name: "hasAll"
            value:
              type: ":Boolean"
              value: true
          - name: "hasAny"
            value:
              type: ":Boolean"
              value: true
          - name: "sorted"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
          - name: "distinct"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "3"
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
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
            type: ":Map"
            entries:
              - key: "name"
                value:
                  type: ":Text"
                  value: "Ada"
        - name: "keyName"
          value:
            type: ":Text"
            value: "name"
    local:
      - name: "Done"
        args:
          - name: "first"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "third"
            value:
              type: ":Number.int64"
              value: "30"
          - name: "zero"
            value:
              type: ":Nothing"
          - name: "missingIndex"
            value:
              type: ":Nothing"
          - name: "property"
            value:
              type: ":Text"
              value: "Ada"
          - name: "textKey"
            value:
              type: ":Text"
              value: "Ada"
          - name: "tagKey"
            value:
              type: ":Text"
              value: "Ada"
          - name: "dynamicTag"
            value:
              type: ":Text"
              value: "Ada"
          - name: "dynamicText"
            value:
              type: ":Text"
              value: "Ada"
          - name: "missingProperty"
            value:
              type: ":Nothing"
          - name: "firstTag"
            value:
              type: ":Text"
              value: "alpha"
          - name: "missingTag"
            value:
              type: ":Nothing"
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
            type: ":Number.int64"
            value: "7"
        - name: "emptyText"
          value:
            type: ":Text"
            value: ""
        - name: "emptyList"
          value:
            type: ":List"
            items: []
        - name: "emptyDict"
          value:
            type: ":Map"
            entries: []
        - name: "zeroValue"
          value:
            type: ":Number.int64"
            value: "0"
        - name: "falseValue"
          value:
            type: ":Boolean"
            value: false
        - name: "diceValue"
          value:
            type: ":Dice"
            rolls: []
        - name: "nanValue"
          value:
            type: ":Number.binary64"
            value: "NaN"
        - name: "infinityValue"
          value:
            type: ":Number.binary64"
            value: "Infinity"
    local:
      - name: "Done"
        args:
          - name: "hasOpt"
            value:
              type: ":Boolean"
              value: true
          - name: "hasEmptyText"
            value:
              type: ":Boolean"
              value: false
          - name: "hasEmptyList"
            value:
              type: ":Boolean"
              value: false
          - name: "hasEmptyDict"
            value:
              type: ":Boolean"
              value: false
          - name: "hasZero"
            value:
              type: ":Boolean"
              value: true
          - name: "hasFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "hasNaN"
            value:
              type: ":Boolean"
              value: false
          - name: "hasInfinity"
            value:
              type: ":Boolean"
              value: true
          - name: "isEmptyOpt"
            value:
              type: ":Boolean"
              value: false
          - name: "isEmptyText"
            value:
              type: ":Boolean"
              value: true
          - name: "isEmptyList"
            value:
              type: ":Boolean"
              value: true
          - name: "isEmptyDict"
            value:
              type: ":Boolean"
              value: true
          - name: "isEmptyDice"
            value:
              type: ":Boolean"
              value: true
          - name: "isEmptyNaN"
            value:
              type: ":Boolean"
              value: true
          - name: "isEmptyInfinity"
            value:
              type: ":Boolean"
              value: false
          - name: "nanIsNothing"
            value:
              type: ":Boolean"
              value: true
          - name: "notEmptyList"
            value:
              type: ":Boolean"
              value: true
          - name: "notFalse"
            value:
              type: ":Boolean"
              value: true
          - name: "optValue"
            value:
              type: ":Number.int64"
              value: "7"
          - name: "listValue"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "textValue"
            value:
              type: ":Text"
              value: "fallback"
          - name: "dictValue"
            value:
              type: ":Text"
              value: "default"
          - name: "missingTextValue"
            value:
              type: ":Text"
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
  let tagOk be (#name) as :Tag
  let numberOk be ('12.2') as :Number
  let numberFail be ('abc') as :Number
  let integerOk be ('12.7') as :Number
  let booleanOk be ('true') as :Boolean
  let listOk be ('ab') as :List
  let diceOk be ([6, 2, 4]) as :Dice
  emit Done(tagOk: tagOk, numberOk: numberOk, numberFail: numberFail, integerOk: integerOk, booleanOk: booleanOk, listLen: listOk[:count], diceFirst: diceOk[1], diceLen: diceOk[:count], textLen: valueForLen[:count], dictLen: [first: 1, second: 2][:count], valueForLenLen: valueForLen[:count], floatLen: 12.5[:count], booleanLen: true[:count], aIsTag: a is :Tag, bIsFloat: b is :Number, cIsInteger: c is :Number, dIsText: d is :Text, eIsList: listArg is :List, fIsMap: f is :Map, gIsNothing: g is nothing)
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
            type: ":Text"
            value: "x"
        - name: "a"
          value:
            type: ":Tag"
            value: "name"
        - name: "b"
          value:
            type: ":Number.binary64"
            value: "1.5"
        - name: "c"
          value:
            type: ":Number.int64"
            value: "2"
        - name: "d"
          value:
            type: ":Text"
            value: "x"
        - name: "listArg"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
        - name: "f"
          value:
            type: ":Map"
            entries:
              - key: "v"
                value:
                  type: ":Number.int64"
                  value: "1"
        - name: "g"
          value:
            type: ":Nothing"
    local:
      - name: "Done"
        args:
          - name: "tagOk"
            value:
              type: ":Tag"
              value: "name"
          - name: "numberOk"
            value:
              type: ":Number.binary64"
              value: "12.2"
          - name: "numberFail"
            value:
              type: ":Nothing"
          - name: "integerOk"
            value:
              type: ":Number.binary64"
              value: "12.7"
          - name: "booleanOk"
            value:
              type: ":Boolean"
              value: true
          - name: "listLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "diceFirst"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "diceLen"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "textLen"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "dictLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "valueForLenLen"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "floatLen"
            value:
              type: ":Nothing"
          - name: "booleanLen"
            value:
              type: ":Nothing"
          - name: "aIsTag"
            value:
              type: ":Boolean"
              value: true
          - name: "bIsFloat"
            value:
              type: ":Boolean"
              value: true
          - name: "cIsInteger"
            value:
              type: ":Boolean"
              value: true
          - name: "dIsText"
            value:
              type: ":Boolean"
              value: true
          - name: "eIsList"
            value:
              type: ":Boolean"
              value: true
          - name: "fIsMap"
            value:
              type: ":Boolean"
              value: true
          - name: "gIsNothing"
            value:
              type: ":Boolean"
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
  let nanValue be ('abc') as :Number
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
            type: ":Nothing"
    local:
      - name: "Done"
        args:
          - name: "nanValue"
            value:
              type: ":Nothing"
          - name: "nanPropagated"
            value:
              type: ":Nothing"
          - name: "nothing"
            value:
              type: ":Nothing"
          - name: "nothingPropagated"
            value:
              type: ":Nothing"
          - name: "propertyFromNothing"
            value:
              type: ":Nothing"
          - name: "pos"
            value:
              type: ":Number.binary64"
              value: "7.92281625142643e28"
          - name: "neg"
            value:
              type: ":Number.binary64"
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
  emit Done(intIsInteger: integerValue is :Number, intIsFloat: integerValue is :Number, percentIsFloat: percentValue is :Number, degreeIsFloat: degreeValue is :Number, degreeIsDegree: degreeValue is :Quantity(°), meterIsMeter: meterValue is :Quantity(m), secondIsSecond: secondValue is :Quantity(s), textIsText: 'x' is :Text, tagIsTag: #ready is :Tag, boolIsBoolean: true is :Boolean, listIsList: listValue is :List, dictIsMap: dictValue is :Map, customIsGauge: custom is :Gauge, customIsMap: custom is :Map, msgIsMessage: msg is :Message, msgIsMap: msg is :Map, handlerIsHandler: handler is :Handler, missingIsNothing: nothing is nothing)
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
            type: ":Gauge"
            entries:
              - key: "current"
                value:
                  type: ":Number.int64"
                  value: "5"
    local:
      - name: "Done"
        args:
          - name: "intIsInteger"
            value:
              type: ":Boolean"
              value: true
          - name: "intIsFloat"
            value:
              type: ":Boolean"
              value: true
          - name: "percentIsFloat"
            value:
              type: ":Boolean"
              value: true
          - name: "degreeIsFloat"
            value:
              type: ":Boolean"
              value: true
          - name: "degreeIsDegree"
            value:
              type: ":Boolean"
              value: true
          - name: "meterIsMeter"
            value:
              type: ":Boolean"
              value: true
          - name: "secondIsSecond"
            value:
              type: ":Boolean"
              value: true
          - name: "textIsText"
            value:
              type: ":Boolean"
              value: true
          - name: "tagIsTag"
            value:
              type: ":Boolean"
              value: true
          - name: "boolIsBoolean"
            value:
              type: ":Boolean"
              value: true
          - name: "listIsList"
            value:
              type: ":Boolean"
              value: true
          - name: "dictIsMap"
            value:
              type: ":Boolean"
              value: true
          - name: "customIsGauge"
            value:
              type: ":Boolean"
              value: true
          - name: "customIsMap"
            value:
              type: ":Boolean"
              value: true
          - name: "msgIsMessage"
            value:
              type: ":Boolean"
              value: true
          - name: "msgIsMap"
            value:
              type: ":Boolean"
              value: false
          - name: "handlerIsHandler"
            value:
              type: ":Boolean"
              value: true
          - name: "missingIsNothing"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: word-like unit names are ordinary tags

This runtime case verifies that names such as `meter`, `degree`, and `seconds` have no special meaning when written as tags.

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
  - name: "word-like unit names are ordinary tags.ges"
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
              type: ":Tag"
              value: "meter"
          - name: "degree"
            value:
              type: ":Tag"
              value: "degree"
          - name: "seconds"
            value:
              type: ":Tag"
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
  let scalars be (text) as :List
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
              type: ":Number.int64"
              value: "4"
          - name: "first"
            value:
              type: ":Text"
              value: "A"
          - name: "second"
            value:
              type: ":Text"
              value: "\uD83D\uDE00"
          - name: "third"
            value:
              type: ":Text"
              value: "e"
          - name: "fourth"
            value:
              type: ":Text"
              value: "\u0301"
          - name: "last"
            value:
              type: ":Text"
              value: "\u0301"
          - name: "single"
            value:
              type: ":Text"
              value: "\uD83D\uDE00"
          - name: "listCount"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "listSecond"
            value:
              type: ":Text"
              value: "\uD83D\uDE00"
          - name: "hugeIndex"
            value:
              type: ":Nothing"
```
