---
formatVersion: 1
suiteId: "runtime.atomic.casts"
title: "RuntimeAtomicCasts"
categories: [conformance]
tags: [migrated-json-v1]
---

# RuntimeAtomicCasts

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite isolates explicit value casts and verifies their successful and failing portable results.

---

## Test: cast from nothing

This runtime case exercises “cast from nothing” and verifies the declared messages, values, and execution result.

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
  - name: "cast from nothing.ges"
    program: main
```

### Source code under test

```ges
module AtomicCastFromNothing
on Start {
  let source be nothing
  let toNothing as nothing be source
  let toBoolean as :boolean be source
  let toNumber as :number be source
  let toPercentage as :percentage be source
  let toText as :text be source
  let toTag as :tag be source
  let toMeter as :quantity(m) be source
  let toVector as :vector be source
  let toPoint as :point be source
  let toList as :list be source
  let toMap as :map be source
  let toDice as :dice be source
  let toRange as :range be source
  let toMessage as :message be source
  let toSeries as :series be source
  emit Done(toNothing: toNothing, toBoolean: toBoolean, toNumber: toNumber, toPercentage: toPercentage, toText: toText, toTag: toTag, toMeter: toMeter, toVector: toVector, toPoint: toPoint, toList: toList, toMap: toMap, toDice: toDice, toRange: toRange, toMessage: toMessage, toSeries: toSeries)
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
          - name: "toNothing"
            value:
              type: ":nothing"
          - name: "toBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "toNumber"
            value:
              type: ":nothing"
          - name: "toPercentage"
            value:
              type: ":nothing"
          - name: "toText"
            value:
              type: ":text"
              value: ""
          - name: "toTag"
            value:
              type: ":nothing"
          - name: "toMeter"
            value:
              type: ":nothing"
          - name: "toVector"
            value:
              type: ":nothing"
          - name: "toPoint"
            value:
              type: ":nothing"
          - name: "toList"
            value:
              type: ":list"
              items: []
          - name: "toMap"
            value:
              type: ":map"
              entries: []
          - name: "toDice"
            value:
              type: ":dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":nothing"
          - name: "toMessage"
            value:
              type: ":nothing"
          - name: "toSeries"
            value:
              type: ":nothing"
```

---

## Test: check from nothing

This runtime case exercises “check from nothing” and verifies the declared messages, values, and execution result.

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
  - name: "check from nothing.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromNothing
on Start {
  let source be nothing
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: true
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: cast from boolean

This runtime case exercises “cast from boolean” and verifies the declared messages, values, and execution result.

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
  - name: "cast from boolean.ges"
    program: main
```

### Source code under test

```ges
module AtomicCastFromBoolean
on Start {
  let source be true
  let toNothing as nothing be source
  let toBoolean as :boolean be source
  let toNumber as :number be source
  let toPercentage as :percentage be source
  let toText as :text be source
  let toTag as :tag be source
  let toMeter as :quantity(m) be source
  let toVector as :vector be source
  let toPoint as :point be source
  let toList as :list be source
  let toMap as :map be source
  let toDice as :dice be source
  let toRange as :range be source
  let toMessage as :message be source
  let toSeries as :series be source
  emit Done(toNothing: toNothing, toBoolean: toBoolean, toNumber: toNumber, toPercentage: toPercentage, toText: toText, toTag: toTag, toMeter: toMeter, toVector: toVector, toPoint: toPoint, toList: toList, toMap: toMap, toDice: toDice, toRange: toRange, toMessage: toMessage, toSeries: toSeries)
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
          - name: "toNothing"
            value:
              type: ":nothing"
          - name: "toBoolean"
            value:
              type: ":boolean"
              value: true
          - name: "toNumber"
            value:
              type: ":integer"
              value: "1"
          - name: "toPercentage"
            value:
              type: ":percentage"
              value: "1"
          - name: "toText"
            value:
              type: ":text"
              value: "True"
          - name: "toTag"
            value:
              type: ":tag"
              value: "true"
          - name: "toMeter"
            value:
              type: ":nothing"
          - name: "toVector"
            value:
              type: ":vector"
              x: "1"
              y: "0"
              z: "0"
          - name: "toPoint"
            value:
              type: ":point"
              x: "1"
              y: "0"
              z: "0"
          - name: "toList"
            value:
              type: ":list"
              items: []
          - name: "toMap"
            value:
              type: ":map"
              entries: []
          - name: "toDice"
            value:
              type: ":dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":nothing"
          - name: "toMessage"
            value:
              type: ":nothing"
          - name: "toSeries"
            value:
              type: ":nothing"
```

---

## Test: check from boolean

This runtime case exercises “check from boolean” and verifies the declared messages, values, and execution result.

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
  - name: "check from boolean.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromBoolean
on Start {
  let source be true
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: true
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":boolean"
              value: true
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: cast from integer

This runtime case exercises “cast from integer” and verifies the declared messages, values, and execution result.

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
  - name: "cast from integer.ges"
    program: main
```

### Source code under test

```ges
module AtomicCastFromInteger
on Start {
  let source be 12
  let toNothing as nothing be source
  let toBoolean as :boolean be source
  let toNumber as :number be source
  let toPercentage as :percentage be source
  let toText as :text be source
  let toTag as :tag be source
  let toMeter as :quantity(m) be source
  let toVector as :vector be source
  let toPoint as :point be source
  let toList as :list be source
  let toMap as :map be source
  let toDice as :dice be source
  let toRange as :range be source
  let toMessage as :message be source
  let toSeries as :series be source
  emit Done(toNothing: toNothing, toBoolean: toBoolean, toNumber: toNumber, toPercentage: toPercentage, toText: toText, toTag: toTag, toMeter: toMeter, toVector: toVector, toPoint: toPoint, toList: toList, toMap: toMap, toDice: toDice, toRange: toRange, toMessage: toMessage, toSeries: toSeries)
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
          - name: "toNothing"
            value:
              type: ":nothing"
          - name: "toBoolean"
            value:
              type: ":boolean"
              value: true
          - name: "toNumber"
            value:
              type: ":integer"
              value: "12"
          - name: "toPercentage"
            value:
              type: ":percentage"
              value: "0.12"
          - name: "toText"
            value:
              type: ":text"
              value: "12"
          - name: "toTag"
            value:
              type: ":nothing"
          - name: "toMeter"
            value:
              type: ":integer"
              value: "12"
              unit: ":meter"
          - name: "toVector"
            value:
              type: ":vector"
              x: "12"
              y: "0"
              z: "0"
          - name: "toPoint"
            value:
              type: ":point"
              x: "12"
              y: "0"
              z: "0"
          - name: "toList"
            value:
              type: ":list"
              items: []
          - name: "toMap"
            value:
              type: ":map"
              entries: []
          - name: "toDice"
            value:
              type: ":dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":nothing"
          - name: "toMessage"
            value:
              type: ":nothing"
          - name: "toSeries"
            value:
              type: ":nothing"
```

---

## Test: check from integer

This runtime case exercises “check from integer” and verifies the declared messages, values, and execution result.

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
  - name: "check from integer.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromInteger
on Start {
  let source be 12
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":boolean"
              value: true
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: cast from float

This runtime case exercises “cast from float” and verifies the declared messages, values, and execution result.

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
  - name: "cast from float.ges"
    program: main
```

### Source code under test

```ges
module AtomicCastFromFloat
on Start {
  let source be 12.5
  let toNothing as nothing be source
  let toBoolean as :boolean be source
  let toNumber as :number be source
  let toPercentage as :percentage be source
  let toText as :text be source
  let toTag as :tag be source
  let toMeter as :quantity(m) be source
  let toVector as :vector be source
  let toPoint as :point be source
  let toList as :list be source
  let toMap as :map be source
  let toDice as :dice be source
  let toRange as :range be source
  let toMessage as :message be source
  let toSeries as :series be source
  emit Done(toNothing: toNothing, toBoolean: toBoolean, toNumber: toNumber, toPercentage: toPercentage, toText: toText, toTag: toTag, toMeter: toMeter, toVector: toVector, toPoint: toPoint, toList: toList, toMap: toMap, toDice: toDice, toRange: toRange, toMessage: toMessage, toSeries: toSeries)
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
          - name: "toNothing"
            value:
              type: ":nothing"
          - name: "toBoolean"
            value:
              type: ":boolean"
              value: true
          - name: "toNumber"
            value:
              type: ":float"
              value: "12.5"
          - name: "toPercentage"
            value:
              type: ":percentage"
              value: "0.125"
          - name: "toText"
            value:
              type: ":text"
              value: "12.5"
          - name: "toTag"
            value:
              type: ":nothing"
          - name: "toMeter"
            value:
              type: ":float"
              value: "12.5"
              unit: ":meter"
          - name: "toVector"
            value:
              type: ":vector"
              x: "12.5"
              y: "0"
              z: "0"
          - name: "toPoint"
            value:
              type: ":point"
              x: "12.5"
              y: "0"
              z: "0"
          - name: "toList"
            value:
              type: ":list"
              items: []
          - name: "toMap"
            value:
              type: ":map"
              entries: []
          - name: "toDice"
            value:
              type: ":dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":nothing"
          - name: "toMessage"
            value:
              type: ":nothing"
          - name: "toSeries"
            value:
              type: ":nothing"
```

---

## Test: check from float

This runtime case exercises “check from float” and verifies the declared messages, values, and execution result.

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
  - name: "check from float.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromFloat
on Start {
  let source be 12.5
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: true
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: cast from meter

This runtime case exercises “cast from meter” and verifies the declared messages, values, and execution result.

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
  - name: "cast from meter.ges"
    program: main
```

### Source code under test

```ges
module AtomicCastFromMeter
on Start {
  let source be 12m
  let toNothing as nothing be source
  let toBoolean as :boolean be source
  let toNumber as :number be source
  let toPercentage as :percentage be source
  let toText as :text be source
  let toTag as :tag be source
  let toMeter as :quantity(m) be source
  let toVector as :vector be source
  let toPoint as :point be source
  let toList as :list be source
  let toMap as :map be source
  let toDice as :dice be source
  let toRange as :range be source
  let toMessage as :message be source
  let toSeries as :series be source
  emit Done(toNothing: toNothing, toBoolean: toBoolean, toNumber: toNumber, toPercentage: toPercentage, toText: toText, toTag: toTag, toMeter: toMeter, toVector: toVector, toPoint: toPoint, toList: toList, toMap: toMap, toDice: toDice, toRange: toRange, toMessage: toMessage, toSeries: toSeries)
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
          - name: "toNothing"
            value:
              type: ":nothing"
          - name: "toBoolean"
            value:
              type: ":boolean"
              value: true
          - name: "toNumber"
            value:
              type: ":integer"
              value: "12"
              unit: ":meter"
          - name: "toPercentage"
            value:
              type: ":nothing"
          - name: "toText"
            value:
              type: ":text"
              value: "12m"
          - name: "toTag"
            value:
              type: ":nothing"
          - name: "toMeter"
            value:
              type: ":integer"
              value: "12"
              unit: ":meter"
          - name: "toVector"
            value:
              type: ":vector"
              x: "12"
              y: "0"
              z: "0"
              unit: ":meter"
          - name: "toPoint"
            value:
              type: ":point"
              x: "12"
              y: "0"
              z: "0"
              unit: ":meter"
          - name: "toList"
            value:
              type: ":list"
              items: []
          - name: "toMap"
            value:
              type: ":map"
              entries: []
          - name: "toDice"
            value:
              type: ":dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":nothing"
          - name: "toMessage"
            value:
              type: ":nothing"
          - name: "toSeries"
            value:
              type: ":nothing"
```

---

## Test: check from meter

This runtime case exercises “check from meter” and verifies the declared messages, values, and execution result.

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
  - name: "check from meter.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromMeter
on Start {
  let source be 12m
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":boolean"
              value: true
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: true
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: cast from percentage

This runtime case exercises “cast from percentage” and verifies the declared messages, values, and execution result.

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
  - name: "cast from percentage.ges"
    program: main
```

### Source code under test

```ges
module AtomicCastFromPercentage
on Start {
  let source be 25%
  let toNothing as nothing be source
  let toBoolean as :boolean be source
  let toNumber as :number be source
  let toPercentage as :percentage be source
  let toText as :text be source
  let toTag as :tag be source
  let toMeter as :quantity(m) be source
  let toVector as :vector be source
  let toPoint as :point be source
  let toList as :list be source
  let toMap as :map be source
  let toDice as :dice be source
  let toRange as :range be source
  let toMessage as :message be source
  let toSeries as :series be source
  emit Done(toNothing: toNothing, toBoolean: toBoolean, toNumber: toNumber, toPercentage: toPercentage, toText: toText, toTag: toTag, toMeter: toMeter, toVector: toVector, toPoint: toPoint, toList: toList, toMap: toMap, toDice: toDice, toRange: toRange, toMessage: toMessage, toSeries: toSeries)
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
          - name: "toNothing"
            value:
              type: ":nothing"
          - name: "toBoolean"
            value:
              type: ":boolean"
              value: true
          - name: "toNumber"
            value:
              type: ":float"
              value: "0.25"
          - name: "toPercentage"
            value:
              type: ":percentage"
              value: "0.25"
          - name: "toText"
            value:
              type: ":text"
              value: "25%"
          - name: "toTag"
            value:
              type: ":nothing"
          - name: "toMeter"
            value:
              type: ":nothing"
          - name: "toVector"
            value:
              type: ":vector"
              x: "0.25"
              y: "0"
              z: "0"
          - name: "toPoint"
            value:
              type: ":point"
              x: "0.25"
              y: "0"
              z: "0"
          - name: "toList"
            value:
              type: ":list"
              items: []
          - name: "toMap"
            value:
              type: ":map"
              entries: []
          - name: "toDice"
            value:
              type: ":dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":nothing"
          - name: "toMessage"
            value:
              type: ":nothing"
          - name: "toSeries"
            value:
              type: ":nothing"
```

---

## Test: check from percentage

This runtime case exercises “check from percentage” and verifies the declared messages, values, and execution result.

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
  - name: "check from percentage.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromPercentage
on Start {
  let source be 25%
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: true
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: true
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: cast from numeric text

This runtime case exercises “cast from numeric text” and verifies the declared messages, values, and execution result.

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
  - name: "cast from numeric text.ges"
    program: main
```

### Source code under test

```ges
module AtomicCastFromNumericText
on Start {
  let source be '12.5'
  let toNothing as nothing be source
  let toBoolean as :boolean be source
  let toNumber as :number be source
  let toPercentage as :percentage be source
  let toText as :text be source
  let toTag as :tag be source
  let toMeter as :quantity(m) be source
  let toVector as :vector be source
  let toPoint as :point be source
  let toList as :list be source
  let toMap as :map be source
  let toDice as :dice be source
  let toRange as :range be source
  let toMessage as :message be source
  let toSeries as :series be source
  emit Done(toNothing: toNothing, toBoolean: toBoolean, toNumber: toNumber, toPercentage: toPercentage, toText: toText, toTag: toTag, toMeter: toMeter, toVector: toVector, toPoint: toPoint, toList: toList, toMap: toMap, toDice: toDice, toRange: toRange, toMessage: toMessage, toSeries: toSeries)
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
          - name: "toNothing"
            value:
              type: ":nothing"
          - name: "toBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "toNumber"
            value:
              type: ":float"
              value: "12.5"
          - name: "toPercentage"
            value:
              type: ":nothing"
          - name: "toText"
            value:
              type: ":text"
              value: "12.5"
          - name: "toTag"
            value:
              type: ":nothing"
          - name: "toMeter"
            value:
              type: ":nothing"
          - name: "toVector"
            value:
              type: ":nothing"
          - name: "toPoint"
            value:
              type: ":nothing"
          - name: "toList"
            value:
              type: ":list"
              items:
                - type: ":text"
                  value: "1"
                - type: ":text"
                  value: "2"
                - type: ":text"
                  value: "."
                - type: ":text"
                  value: "5"
          - name: "toMap"
            value:
              type: ":map"
              entries: []
          - name: "toDice"
            value:
              type: ":dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":nothing"
          - name: "toMessage"
            value:
              type: ":nothing"
          - name: "toSeries"
            value:
              type: ":nothing"
```

---

## Test: check from numeric text

This runtime case exercises “check from numeric text” and verifies the declared messages, values, and execution result.

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
  - name: "check from numeric text.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromNumericText
on Start {
  let source be '12.5'
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: true
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: cast from invalid text

This runtime case exercises “cast from invalid text” and verifies the declared messages, values, and execution result.

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
  - name: "cast from invalid text.ges"
    program: main
```

### Source code under test

```ges
module AtomicCastFromInvalidText
on Start {
  let source be 'hello'
  let toNothing as nothing be source
  let toBoolean as :boolean be source
  let toNumber as :number be source
  let toPercentage as :percentage be source
  let toText as :text be source
  let toTag as :tag be source
  let toMeter as :quantity(m) be source
  let toVector as :vector be source
  let toPoint as :point be source
  let toList as :list be source
  let toMap as :map be source
  let toDice as :dice be source
  let toRange as :range be source
  let toMessage as :message be source
  let toSeries as :series be source
  emit Done(toNothing: toNothing, toBoolean: toBoolean, toNumber: toNumber, toPercentage: toPercentage, toText: toText, toTag: toTag, toMeter: toMeter, toVector: toVector, toPoint: toPoint, toList: toList, toMap: toMap, toDice: toDice, toRange: toRange, toMessage: toMessage, toSeries: toSeries)
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
          - name: "toNothing"
            value:
              type: ":nothing"
          - name: "toBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "toNumber"
            value:
              type: ":nothing"
          - name: "toPercentage"
            value:
              type: ":nothing"
          - name: "toText"
            value:
              type: ":text"
              value: "hello"
          - name: "toTag"
            value:
              type: ":tag"
              value: "hello"
          - name: "toMeter"
            value:
              type: ":nothing"
          - name: "toVector"
            value:
              type: ":nothing"
          - name: "toPoint"
            value:
              type: ":nothing"
          - name: "toList"
            value:
              type: ":list"
              items:
                - type: ":text"
                  value: "h"
                - type: ":text"
                  value: "e"
                - type: ":text"
                  value: "l"
                - type: ":text"
                  value: "l"
                - type: ":text"
                  value: "o"
          - name: "toMap"
            value:
              type: ":map"
              entries: []
          - name: "toDice"
            value:
              type: ":dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":nothing"
          - name: "toMessage"
            value:
              type: ":nothing"
          - name: "toSeries"
            value:
              type: ":nothing"
```

---

## Test: check from invalid text

This runtime case exercises “check from invalid text” and verifies the declared messages, values, and execution result.

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
  - name: "check from invalid text.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromInvalidText
on Start {
  let source be 'hello'
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: true
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: cast from pi tag

This runtime case exercises “cast from pi tag” and verifies the declared messages, values, and execution result.

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
  - name: "cast from pi tag.ges"
    program: main
```

### Source code under test

```ges
module AtomicCastFromPiTag
on Start {
  let source be pi
  let toNothing as nothing be source
  let toBoolean as :boolean be source
  let toNumber as :number be source
  let toPercentage as :percentage be source
  let toText as :text be source
  let toTag as :tag be source
  let toMeter as :quantity(m) be source
  let toVector as :vector be source
  let toPoint as :point be source
  let toList as :list be source
  let toMap as :map be source
  let toDice as :dice be source
  let toRange as :range be source
  let toMessage as :message be source
  let toSeries as :series be source
  emit Done(toNothing: toNothing, toBoolean: toBoolean, toNumber: toNumber, toPercentage: toPercentage, toText: toText, toTag: toTag, toMeter: toMeter, toVector: toVector, toPoint: toPoint, toList: toList, toMap: toMap, toDice: toDice, toRange: toRange, toMessage: toMessage, toSeries: toSeries)
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
          - name: "toNothing"
            value:
              type: ":nothing"
          - name: "toBoolean"
            value:
              type: ":boolean"
              value: true
          - name: "toNumber"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "toPercentage"
            value:
              type: ":percentage"
              value: "0.0314159265358979"
          - name: "toText"
            value:
              type: ":text"
              value: "3.141592653589793"
          - name: "toTag"
            value:
              type: ":nothing"
          - name: "toMeter"
            value:
              type: ":float"
              value: "3.141592653589793"
              unit: ":meter"
          - name: "toVector"
            value:
              type: ":vector"
              x: "3.141592653589793"
              y: "0"
              z: "0"
          - name: "toPoint"
            value:
              type: ":point"
              x: "3.141592653589793"
              y: "0"
              z: "0"
          - name: "toList"
            value:
              type: ":list"
              items: []
          - name: "toMap"
            value:
              type: ":map"
              entries: []
          - name: "toDice"
            value:
              type: ":dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":nothing"
          - name: "toMessage"
            value:
              type: ":nothing"
          - name: "toSeries"
            value:
              type: ":nothing"
```

---

## Test: check from pi tag

This runtime case exercises “check from pi tag” and verifies the declared messages, values, and execution result.

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
  - name: "check from pi tag.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromPiTag
on Start {
  let source be pi
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: true
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: cast from custom tag

This runtime case exercises “cast from custom tag” and verifies the declared messages, values, and execution result.

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
  - name: "cast from custom tag.ges"
    program: main
```

### Source code under test

```ges
module AtomicCastFromCustomTag
on Start {
  let source be #custom
  let toNothing as nothing be source
  let toBoolean as :boolean be source
  let toNumber as :number be source
  let toPercentage as :percentage be source
  let toText as :text be source
  let toTag as :tag be source
  let toMeter as :quantity(m) be source
  let toVector as :vector be source
  let toPoint as :point be source
  let toList as :list be source
  let toMap as :map be source
  let toDice as :dice be source
  let toRange as :range be source
  let toMessage as :message be source
  let toSeries as :series be source
  emit Done(toNothing: toNothing, toBoolean: toBoolean, toNumber: toNumber, toPercentage: toPercentage, toText: toText, toTag: toTag, toMeter: toMeter, toVector: toVector, toPoint: toPoint, toList: toList, toMap: toMap, toDice: toDice, toRange: toRange, toMessage: toMessage, toSeries: toSeries)
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
          - name: "toNothing"
            value:
              type: ":nothing"
          - name: "toBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "toNumber"
            value:
              type: ":nothing"
          - name: "toPercentage"
            value:
              type: ":nothing"
          - name: "toText"
            value:
              type: ":text"
              value: ":custom"
          - name: "toTag"
            value:
              type: ":tag"
              value: "custom"
          - name: "toMeter"
            value:
              type: ":nothing"
          - name: "toVector"
            value:
              type: ":nothing"
          - name: "toPoint"
            value:
              type: ":nothing"
          - name: "toList"
            value:
              type: ":list"
              items:
                - type: ":text"
                  value: "c"
                - type: ":text"
                  value: "u"
                - type: ":text"
                  value: "s"
                - type: ":text"
                  value: "t"
                - type: ":text"
                  value: "o"
                - type: ":text"
                  value: "m"
          - name: "toMap"
            value:
              type: ":map"
              entries: []
          - name: "toDice"
            value:
              type: ":dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":nothing"
          - name: "toMessage"
            value:
              type: ":nothing"
          - name: "toSeries"
            value:
              type: ":nothing"
```

---

## Test: check from custom tag

This runtime case exercises “check from custom tag” and verifies the declared messages, values, and execution result.

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
  - name: "check from custom tag.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromCustomTag
on Start {
  let source be #custom
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: true
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: cast from vector

This runtime case exercises “cast from vector” and verifies the declared messages, values, and execution result.

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
  - name: "cast from vector.ges"
    program: main
```

### Source code under test

```ges
module AtomicCastFromVector
on Start {
  let source be :vector(1, 2, 3)
  let toNothing as nothing be source
  let toBoolean as :boolean be source
  let toNumber as :number be source
  let toPercentage as :percentage be source
  let toText as :text be source
  let toTag as :tag be source
  let toMeter as :quantity(m) be source
  let toVector as :vector be source
  let toPoint as :point be source
  let toList as :list be source
  let toMap as :map be source
  let toDice as :dice be source
  let toRange as :range be source
  let toMessage as :message be source
  let toSeries as :series be source
  emit Done(toNothing: toNothing, toBoolean: toBoolean, toNumber: toNumber, toPercentage: toPercentage, toText: toText, toTag: toTag, toMeter: toMeter, toVector: toVector, toPoint: toPoint, toList: toList, toMap: toMap, toDice: toDice, toRange: toRange, toMessage: toMessage, toSeries: toSeries)
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
          - name: "toNothing"
            value:
              type: ":nothing"
          - name: "toBoolean"
            value:
              type: ":boolean"
              value: true
          - name: "toNumber"
            value:
              type: ":nothing"
          - name: "toPercentage"
            value:
              type: ":nothing"
          - name: "toText"
            value:
              type: ":text"
              value: "vector[x: 1, y: 2, z: 3]"
          - name: "toTag"
            value:
              type: ":nothing"
          - name: "toMeter"
            value:
              type: ":vector"
              x: "1"
              y: "2"
              z: "3"
              unit: ":meter"
          - name: "toVector"
            value:
              type: ":vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "toPoint"
            value:
              type: ":point"
              x: "1"
              y: "2"
              z: "3"
          - name: "toList"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "toMap"
            value:
              type: ":map"
              entries:
                - key: "x"
                  value:
                    type: ":integer"
                    value: "1"
                - key: "y"
                  value:
                    type: ":integer"
                    value: "2"
                - key: "z"
                  value:
                    type: ":integer"
                    value: "3"
          - name: "toDice"
            value:
              type: ":dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":nothing"
          - name: "toMessage"
            value:
              type: ":nothing"
          - name: "toSeries"
            value:
              type: ":nothing"
```

---

## Test: check from vector

This runtime case exercises “check from vector” and verifies the declared messages, values, and execution result.

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
  - name: "check from vector.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromVector
on Start {
  let source be :vector(1, 2, 3)
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: true
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: cast from point

This runtime case exercises “cast from point” and verifies the declared messages, values, and execution result.

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
  - name: "cast from point.ges"
    program: main
```

### Source code under test

```ges
module AtomicCastFromPoint
on Start {
  let source be :point(4, 5, 6)
  let toNothing as nothing be source
  let toBoolean as :boolean be source
  let toNumber as :number be source
  let toPercentage as :percentage be source
  let toText as :text be source
  let toTag as :tag be source
  let toMeter as :quantity(m) be source
  let toVector as :vector be source
  let toPoint as :point be source
  let toList as :list be source
  let toMap as :map be source
  let toDice as :dice be source
  let toRange as :range be source
  let toMessage as :message be source
  let toSeries as :series be source
  emit Done(toNothing: toNothing, toBoolean: toBoolean, toNumber: toNumber, toPercentage: toPercentage, toText: toText, toTag: toTag, toMeter: toMeter, toVector: toVector, toPoint: toPoint, toList: toList, toMap: toMap, toDice: toDice, toRange: toRange, toMessage: toMessage, toSeries: toSeries)
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
          - name: "toNothing"
            value:
              type: ":nothing"
          - name: "toBoolean"
            value:
              type: ":boolean"
              value: true
          - name: "toNumber"
            value:
              type: ":nothing"
          - name: "toPercentage"
            value:
              type: ":nothing"
          - name: "toText"
            value:
              type: ":text"
              value: "point[x: 4, y: 5, z: 6]"
          - name: "toTag"
            value:
              type: ":nothing"
          - name: "toMeter"
            value:
              type: ":point"
              x: "4"
              y: "5"
              z: "6"
              unit: ":meter"
          - name: "toVector"
            value:
              type: ":vector"
              x: "4"
              y: "5"
              z: "6"
          - name: "toPoint"
            value:
              type: ":point"
              x: "4"
              y: "5"
              z: "6"
          - name: "toList"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "4"
                - type: ":integer"
                  value: "5"
                - type: ":integer"
                  value: "6"
          - name: "toMap"
            value:
              type: ":map"
              entries:
                - key: "x"
                  value:
                    type: ":integer"
                    value: "4"
                - key: "y"
                  value:
                    type: ":integer"
                    value: "5"
                - key: "z"
                  value:
                    type: ":integer"
                    value: "6"
          - name: "toDice"
            value:
              type: ":dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":nothing"
          - name: "toMessage"
            value:
              type: ":nothing"
          - name: "toSeries"
            value:
              type: ":nothing"
```

---

## Test: check from point

This runtime case exercises “check from point” and verifies the declared messages, values, and execution result.

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
  - name: "check from point.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromPoint
on Start {
  let source be :point(4, 5, 6)
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: true
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: cast from list

This runtime case exercises “cast from list” and verifies the declared messages, values, and execution result.

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
  - name: "cast from list.ges"
    program: main
```

### Source code under test

```ges
module AtomicCastFromList
on Start {
  let source be [1, 2, 3]
  let toNothing as nothing be source
  let toBoolean as :boolean be source
  let toNumber as :number be source
  let toPercentage as :percentage be source
  let toText as :text be source
  let toTag as :tag be source
  let toMeter as :quantity(m) be source
  let toVector as :vector be source
  let toPoint as :point be source
  let toList as :list be source
  let toMap as :map be source
  let toDice as :dice be source
  let toRange as :range be source
  let toMessage as :message be source
  let toSeries as :series be source
  emit Done(toNothing: toNothing, toBoolean: toBoolean, toNumber: toNumber, toPercentage: toPercentage, toText: toText, toTag: toTag, toMeter: toMeter, toVector: toVector, toPoint: toPoint, toList: toList, toMap: toMap, toDice: toDice, toRange: toRange, toMessage: toMessage, toSeries: toSeries)
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
          - name: "toNothing"
            value:
              type: ":nothing"
          - name: "toBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "toNumber"
            value:
              type: ":nothing"
          - name: "toPercentage"
            value:
              type: ":nothing"
          - name: "toText"
            value:
              type: ":text"
              value: "[1, 2, 3]"
          - name: "toTag"
            value:
              type: ":nothing"
          - name: "toMeter"
            value:
              type: ":nothing"
          - name: "toVector"
            value:
              type: ":vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "toPoint"
            value:
              type: ":point"
              x: "1"
              y: "2"
              z: "3"
          - name: "toList"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "toMap"
            value:
              type: ":map"
              entries: []
          - name: "toDice"
            value:
              type: ":dice"
              rolls:
                - 3
                - 2
                - 1
          - name: "toRange"
            value:
              type: ":nothing"
          - name: "toMessage"
            value:
              type: ":nothing"
          - name: "toSeries"
            value:
              type: ":nothing"
```

---

## Test: check from list

This runtime case exercises “check from list” and verifies the declared messages, values, and execution result.

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
  - name: "check from list.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromList
on Start {
  let source be [1, 2, 3]
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: true
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: cast from map

This runtime case exercises “cast from map” and verifies the declared messages, values, and execution result.

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
  - name: "cast from map.ges"
    program: main
```

### Source code under test

```ges
module AtomicCastFromMap
on Start {
  let source be [x: 1, y: 2, z: 3]
  let toNothing as nothing be source
  let toBoolean as :boolean be source
  let toNumber as :number be source
  let toPercentage as :percentage be source
  let toText as :text be source
  let toTag as :tag be source
  let toMeter as :quantity(m) be source
  let toVector as :vector be source
  let toPoint as :point be source
  let toList as :list be source
  let toMap as :map be source
  let toDice as :dice be source
  let toRange as :range be source
  let toMessage as :message be source
  let toSeries as :series be source
  emit Done(toNothing: toNothing, toBoolean: toBoolean, toNumber: toNumber, toPercentage: toPercentage, toText: toText, toTag: toTag, toMeter: toMeter, toVector: toVector, toPoint: toPoint, toList: toList, toMap: toMap, toDice: toDice, toRange: toRange, toMessage: toMessage, toSeries: toSeries)
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
          - name: "toNothing"
            value:
              type: ":nothing"
          - name: "toBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "toNumber"
            value:
              type: ":nothing"
          - name: "toPercentage"
            value:
              type: ":nothing"
          - name: "toText"
            value:
              type: ":text"
              value: "map[x: 1, y: 2, z: 3]"
          - name: "toTag"
            value:
              type: ":nothing"
          - name: "toMeter"
            value:
              type: ":nothing"
          - name: "toVector"
            value:
              type: ":vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "toPoint"
            value:
              type: ":point"
              x: "1"
              y: "2"
              z: "3"
          - name: "toList"
            value:
              type: ":list"
              items: []
          - name: "toMap"
            value:
              type: ":map"
              entries:
                - key: "x"
                  value:
                    type: ":integer"
                    value: "1"
                - key: "y"
                  value:
                    type: ":integer"
                    value: "2"
                - key: "z"
                  value:
                    type: ":integer"
                    value: "3"
          - name: "toDice"
            value:
              type: ":dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":nothing"
          - name: "toMessage"
            value:
              type: ":nothing"
          - name: "toSeries"
            value:
              type: ":nothing"
```

---

## Test: check from map

This runtime case exercises “check from map” and verifies the declared messages, values, and execution result.

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
  - name: "check from map.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromMap
on Start {
  let source be [x: 1, y: 2, z: 3]
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: true
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: cast from dice

This runtime case exercises “cast from dice” and verifies the declared messages, values, and execution result.

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
  - name: "cast from dice.ges"
    program: main
```

### Source code under test

```ges
module AtomicCastFromDice
on Start {
  let source as :dice be [3, 2, 1]
  let toNothing as nothing be source
  let toBoolean as :boolean be source
  let toNumber as :number be source
  let toPercentage as :percentage be source
  let toText as :text be source
  let toTag as :tag be source
  let toMeter as :quantity(m) be source
  let toVector as :vector be source
  let toPoint as :point be source
  let toList as :list be source
  let toMap as :map be source
  let toDice as :dice be source
  let toRange as :range be source
  let toMessage as :message be source
  let toSeries as :series be source
  emit Done(toNothing: toNothing, toBoolean: toBoolean, toNumber: toNumber, toPercentage: toPercentage, toText: toText, toTag: toTag, toMeter: toMeter, toVector: toVector, toPoint: toPoint, toList: toList, toMap: toMap, toDice: toDice, toRange: toRange, toMessage: toMessage, toSeries: toSeries)
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
          - name: "toNothing"
            value:
              type: ":nothing"
          - name: "toBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "toNumber"
            value:
              type: ":integer"
              value: "6"
          - name: "toPercentage"
            value:
              type: ":percentage"
              value: "0.06"
          - name: "toText"
            value:
              type: ":text"
              value: "dice[3, 2, 1]"
          - name: "toTag"
            value:
              type: ":nothing"
          - name: "toMeter"
            value:
              type: ":nothing"
          - name: "toVector"
            value:
              type: ":vector"
              x: "3"
              y: "2"
              z: "1"
          - name: "toPoint"
            value:
              type: ":point"
              x: "3"
              y: "2"
              z: "1"
          - name: "toList"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "1"
          - name: "toMap"
            value:
              type: ":map"
              entries: []
          - name: "toDice"
            value:
              type: ":dice"
              rolls:
                - 3
                - 2
                - 1
          - name: "toRange"
            value:
              type: ":nothing"
          - name: "toMessage"
            value:
              type: ":nothing"
          - name: "toSeries"
            value:
              type: ":nothing"
```

---

## Test: check from dice

This runtime case exercises “check from dice” and verifies the declared messages, values, and execution result.

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
  - name: "check from dice.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromDice
on Start {
  let source as :dice be [3, 2, 1]
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":boolean"
              value: true
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: true
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: cast from range

This runtime case exercises “cast from range” and verifies the declared messages, values, and execution result.

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
  - name: "cast from range.ges"
    program: main
```

### Source code under test

```ges
module AtomicCastFromRange
on Start {
  let source as :range be from 1 to 3
  let toNothing as nothing be source
  let toBoolean as :boolean be source
  let toNumber as :number be source
  let toPercentage as :percentage be source
  let toText as :text be source
  let toTag as :tag be source
  let toMeter as :quantity(m) be source
  let toVector as :vector be source
  let toPoint as :point be source
  let toList as :list be source
  let toMap as :map be source
  let toDice as :dice be source
  let toRange as :range be source
  let toMessage as :message be source
  let toSeries as :series be source
  emit Done(toNothing: toNothing, toBoolean: toBoolean, toNumber: toNumber, toPercentage: toPercentage, toText: toText, toTag: toTag, toMeter: toMeter, toVector: toVector, toPoint: toPoint, toList: toList, toMap: toMap, toDice: toDice, toRange: toRange, toMessage: toMessage, toSeries: toSeries)
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
          - name: "toNothing"
            value:
              type: ":nothing"
          - name: "toBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "toNumber"
            value:
              type: ":nothing"
          - name: "toPercentage"
            value:
              type: ":nothing"
          - name: "toText"
            value:
              type: ":text"
              value: "range[1 to 3 step 1]"
          - name: "toTag"
            value:
              type: ":nothing"
          - name: "toMeter"
            value:
              type: ":nothing"
          - name: "toVector"
            value:
              type: ":vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "toPoint"
            value:
              type: ":point"
              x: "1"
              y: "2"
              z: "3"
          - name: "toList"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
          - name: "toMap"
            value:
              type: ":map"
              entries: []
          - name: "toDice"
            value:
              type: ":dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":range"
              from: "1"
              to: "3"
              step: "1"
          - name: "toMessage"
            value:
              type: ":nothing"
          - name: "toSeries"
            value:
              type: ":nothing"
```

---

## Test: check from range

This runtime case exercises “check from range” and verifies the declared messages, values, and execution result.

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
  - name: "check from range.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromRange
on Start {
  let source as :range be from 1 to 3
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: true
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: cast boolean false to tag

This runtime case exercises “cast boolean false to tag” and verifies the declared messages, values, and execution result.

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
  - name: "cast boolean false to tag.ges"
    program: main
```

### Source code under test

```ges
module AtomicCastBooleanFalseToTag
on Start {
  let source be false
  let toTag as :tag be source
  emit Done(toTag: toTag)
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
          - name: "toTag"
            value:
              type: ":tag"
              value: "false"
```

---

## Test: cast boolean text words to tag

This runtime case exercises “cast boolean text words to tag” and verifies the declared messages, values, and execution result.

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
  - name: "cast boolean text words to tag.ges"
    program: main
```

### Source code under test

```ges
module AtomicCastBooleanTextWordsToTag
on Start {
  let upperTrue as :tag be 'True'
  let lowerTrue as :tag be 'true'
  let upperFalse as :tag be 'False'
  let lowerFalse as :tag be 'false'
  let upperWord as :tag be 'Hello'
  emit Done(upperTrue: upperTrue, lowerTrue: lowerTrue, upperFalse: upperFalse, lowerFalse: lowerFalse, upperWord: upperWord)
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
          - name: "upperTrue"
            value:
              type: ":tag"
              value: "true"
          - name: "lowerTrue"
            value:
              type: ":tag"
              value: "true"
          - name: "upperFalse"
            value:
              type: ":tag"
              value: "false"
          - name: "lowerFalse"
            value:
              type: ":tag"
              value: "false"
          - name: "upperWord"
            value:
              type: ":nothing"
```
