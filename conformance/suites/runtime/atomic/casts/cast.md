---
formatVersion: 1
suiteId: "runtime.atomic.casts.cast"
title: "Cast Matrix — Value Conversion"
categories: [conformance]
---

# Cast Matrix — Value Conversion

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers explicit value conversion, including special boolean-to-tag conversions.

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
module atomiccastfromnothing
on Start {
  let source be nothing
  let toNothing be (source) as :Nothing
  let toBoolean be (source) as :Boolean
  let toNumber be (source) as :Number
  let toPercentage be (source) as :Percentage
  let toText be (source) as :Text
  let toTag be (source) as :Tag
  let toMeter be (source) as :Quantity(m)
  let toVector be (source) as :Vector
  let toPoint be (source) as :Point
  let toList be (source) as :List
  let toMap be (source) as :Map
  let toDice be (source) as :Dice
  let toRange be (source) as :Range
  let toMessage be (source) as :Message
  let toSeries be (source) as :Series
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
              type: ":Nothing"
          - name: "toBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "toNumber"
            value:
              type: ":Nothing"
          - name: "toPercentage"
            value:
              type: ":Nothing"
          - name: "toText"
            value:
              type: ":Text"
              value: ""
          - name: "toTag"
            value:
              type: ":Nothing"
          - name: "toMeter"
            value:
              type: ":Nothing"
          - name: "toVector"
            value:
              type: ":Nothing"
          - name: "toPoint"
            value:
              type: ":Nothing"
          - name: "toList"
            value:
              type: ":List"
              items: []
          - name: "toMap"
            value:
              type: ":Map"
              entries: []
          - name: "toDice"
            value:
              type: ":Dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":Nothing"
          - name: "toMessage"
            value:
              type: ":Nothing"
          - name: "toSeries"
            value:
              type: ":Nothing"
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
module atomiccastfromboolean
on Start {
  let source be true
  let toNothing be (source) as :Nothing
  let toBoolean be (source) as :Boolean
  let toNumber be (source) as :Number
  let toPercentage be (source) as :Percentage
  let toText be (source) as :Text
  let toTag be (source) as :Tag
  let toMeter be (source) as :Quantity(m)
  let toVector be (source) as :Vector
  let toPoint be (source) as :Point
  let toList be (source) as :List
  let toMap be (source) as :Map
  let toDice be (source) as :Dice
  let toRange be (source) as :Range
  let toMessage be (source) as :Message
  let toSeries be (source) as :Series
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
              type: ":Nothing"
          - name: "toBoolean"
            value:
              type: ":Boolean"
              value: true
          - name: "toNumber"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "toPercentage"
            value:
              type: ":Percentage"
              value: "1"
          - name: "toText"
            value:
              type: ":Text"
              value: "True"
          - name: "toTag"
            value:
              type: ":Tag"
              value: "true"
          - name: "toMeter"
            value:
              type: ":Nothing"
          - name: "toVector"
            value:
              type: ":Vector"
              x: "1"
              y: "0"
              z: "0"
          - name: "toPoint"
            value:
              type: ":Point"
              x: "1"
              y: "0"
              z: "0"
          - name: "toList"
            value:
              type: ":List"
              items: []
          - name: "toMap"
            value:
              type: ":Map"
              entries: []
          - name: "toDice"
            value:
              type: ":Dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":Nothing"
          - name: "toMessage"
            value:
              type: ":Nothing"
          - name: "toSeries"
            value:
              type: ":Nothing"
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
module atomiccastfrominteger
on Start {
  let source be 12
  let toNothing be (source) as :Nothing
  let toBoolean be (source) as :Boolean
  let toNumber be (source) as :Number
  let toPercentage be (source) as :Percentage
  let toText be (source) as :Text
  let toTag be (source) as :Tag
  let toMeter be (source) as :Quantity(m)
  let toVector be (source) as :Vector
  let toPoint be (source) as :Point
  let toList be (source) as :List
  let toMap be (source) as :Map
  let toDice be (source) as :Dice
  let toRange be (source) as :Range
  let toMessage be (source) as :Message
  let toSeries be (source) as :Series
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
              type: ":Nothing"
          - name: "toBoolean"
            value:
              type: ":Boolean"
              value: true
          - name: "toNumber"
            value:
              type: ":Number.int64"
              value: "12"
          - name: "toPercentage"
            value:
              type: ":Percentage"
              value: "0.12"
          - name: "toText"
            value:
              type: ":Text"
              value: "12"
          - name: "toTag"
            value:
              type: ":Nothing"
          - name: "toMeter"
            value:
              type: ":Quantity.int64"
              value: "12"
              unit: ":meter"
          - name: "toVector"
            value:
              type: ":Vector"
              x: "12"
              y: "0"
              z: "0"
          - name: "toPoint"
            value:
              type: ":Point"
              x: "12"
              y: "0"
              z: "0"
          - name: "toList"
            value:
              type: ":List"
              items: []
          - name: "toMap"
            value:
              type: ":Map"
              entries: []
          - name: "toDice"
            value:
              type: ":Dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":Nothing"
          - name: "toMessage"
            value:
              type: ":Nothing"
          - name: "toSeries"
            value:
              type: ":Nothing"
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
module atomiccastfromfloat
on Start {
  let source be 12.5
  let toNothing be (source) as :Nothing
  let toBoolean be (source) as :Boolean
  let toNumber be (source) as :Number
  let toPercentage be (source) as :Percentage
  let toText be (source) as :Text
  let toTag be (source) as :Tag
  let toMeter be (source) as :Quantity(m)
  let toVector be (source) as :Vector
  let toPoint be (source) as :Point
  let toList be (source) as :List
  let toMap be (source) as :Map
  let toDice be (source) as :Dice
  let toRange be (source) as :Range
  let toMessage be (source) as :Message
  let toSeries be (source) as :Series
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
              type: ":Nothing"
          - name: "toBoolean"
            value:
              type: ":Boolean"
              value: true
          - name: "toNumber"
            value:
              type: ":Number.binary64"
              value: "12.5"
          - name: "toPercentage"
            value:
              type: ":Percentage"
              value: "0.125"
          - name: "toText"
            value:
              type: ":Text"
              value: "12.5"
          - name: "toTag"
            value:
              type: ":Nothing"
          - name: "toMeter"
            value:
              type: ":Quantity.binary64"
              value: "12.5"
              unit: ":meter"
          - name: "toVector"
            value:
              type: ":Vector"
              x: "12.5"
              y: "0"
              z: "0"
          - name: "toPoint"
            value:
              type: ":Point"
              x: "12.5"
              y: "0"
              z: "0"
          - name: "toList"
            value:
              type: ":List"
              items: []
          - name: "toMap"
            value:
              type: ":Map"
              entries: []
          - name: "toDice"
            value:
              type: ":Dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":Nothing"
          - name: "toMessage"
            value:
              type: ":Nothing"
          - name: "toSeries"
            value:
              type: ":Nothing"
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
module atomiccastfrommeter
on Start {
  let source be 12m
  let toNothing be (source) as :Nothing
  let toBoolean be (source) as :Boolean
  let toNumber be (source) as :Number
  let toPercentage be (source) as :Percentage
  let toText be (source) as :Text
  let toTag be (source) as :Tag
  let toMeter be (source) as :Quantity(m)
  let toVector be (source) as :Vector
  let toPoint be (source) as :Point
  let toList be (source) as :List
  let toMap be (source) as :Map
  let toDice be (source) as :Dice
  let toRange be (source) as :Range
  let toMessage be (source) as :Message
  let toSeries be (source) as :Series
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
              type: ":Nothing"
          - name: "toBoolean"
            value:
              type: ":Boolean"
              value: true
          - name: "toNumber"
            value:
              type: ":Quantity.int64"
              value: "12"
              unit: ":meter"
          - name: "toPercentage"
            value:
              type: ":Nothing"
          - name: "toText"
            value:
              type: ":Text"
              value: "12m"
          - name: "toTag"
            value:
              type: ":Nothing"
          - name: "toMeter"
            value:
              type: ":Quantity.int64"
              value: "12"
              unit: ":meter"
          - name: "toVector"
            value:
              type: ":Vector"
              x: "12"
              y: "0"
              z: "0"
              unit: ":meter"
          - name: "toPoint"
            value:
              type: ":Point"
              x: "12"
              y: "0"
              z: "0"
              unit: ":meter"
          - name: "toList"
            value:
              type: ":List"
              items: []
          - name: "toMap"
            value:
              type: ":Map"
              entries: []
          - name: "toDice"
            value:
              type: ":Dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":Nothing"
          - name: "toMessage"
            value:
              type: ":Nothing"
          - name: "toSeries"
            value:
              type: ":Nothing"
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
module atomiccastfrompercentage
on Start {
  let source be 25%
  let toNothing be (source) as :Nothing
  let toBoolean be (source) as :Boolean
  let toNumber be (source) as :Number
  let toPercentage be (source) as :Percentage
  let toText be (source) as :Text
  let toTag be (source) as :Tag
  let toMeter be (source) as :Quantity(m)
  let toVector be (source) as :Vector
  let toPoint be (source) as :Point
  let toList be (source) as :List
  let toMap be (source) as :Map
  let toDice be (source) as :Dice
  let toRange be (source) as :Range
  let toMessage be (source) as :Message
  let toSeries be (source) as :Series
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
              type: ":Nothing"
          - name: "toBoolean"
            value:
              type: ":Boolean"
              value: true
          - name: "toNumber"
            value:
              type: ":Number.binary64"
              value: "0.25"
          - name: "toPercentage"
            value:
              type: ":Percentage"
              value: "0.25"
          - name: "toText"
            value:
              type: ":Text"
              value: "25%"
          - name: "toTag"
            value:
              type: ":Nothing"
          - name: "toMeter"
            value:
              type: ":Nothing"
          - name: "toVector"
            value:
              type: ":Vector"
              x: "0.25"
              y: "0"
              z: "0"
          - name: "toPoint"
            value:
              type: ":Point"
              x: "0.25"
              y: "0"
              z: "0"
          - name: "toList"
            value:
              type: ":List"
              items: []
          - name: "toMap"
            value:
              type: ":Map"
              entries: []
          - name: "toDice"
            value:
              type: ":Dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":Nothing"
          - name: "toMessage"
            value:
              type: ":Nothing"
          - name: "toSeries"
            value:
              type: ":Nothing"
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
module atomiccastfromnumerictext
on Start {
  let source be '12.5'
  let toNothing be (source) as :Nothing
  let toBoolean be (source) as :Boolean
  let toNumber be (source) as :Number
  let toPercentage be (source) as :Percentage
  let toText be (source) as :Text
  let toTag be (source) as :Tag
  let toMeter be (source) as :Quantity(m)
  let toVector be (source) as :Vector
  let toPoint be (source) as :Point
  let toList be (source) as :List
  let toMap be (source) as :Map
  let toDice be (source) as :Dice
  let toRange be (source) as :Range
  let toMessage be (source) as :Message
  let toSeries be (source) as :Series
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
              type: ":Nothing"
          - name: "toBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "toNumber"
            value:
              type: ":Number.binary64"
              value: "12.5"
          - name: "toPercentage"
            value:
              type: ":Nothing"
          - name: "toText"
            value:
              type: ":Text"
              value: "12.5"
          - name: "toTag"
            value:
              type: ":Nothing"
          - name: "toMeter"
            value:
              type: ":Nothing"
          - name: "toVector"
            value:
              type: ":Nothing"
          - name: "toPoint"
            value:
              type: ":Nothing"
          - name: "toList"
            value:
              type: ":List"
              items:
                - type: ":Text"
                  value: "1"
                - type: ":Text"
                  value: "2"
                - type: ":Text"
                  value: "."
                - type: ":Text"
                  value: "5"
          - name: "toMap"
            value:
              type: ":Map"
              entries: []
          - name: "toDice"
            value:
              type: ":Dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":Nothing"
          - name: "toMessage"
            value:
              type: ":Nothing"
          - name: "toSeries"
            value:
              type: ":Nothing"
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
module atomiccastfrominvalidtext
on Start {
  let source be 'hello'
  let toNothing be (source) as :Nothing
  let toBoolean be (source) as :Boolean
  let toNumber be (source) as :Number
  let toPercentage be (source) as :Percentage
  let toText be (source) as :Text
  let toTag be (source) as :Tag
  let toMeter be (source) as :Quantity(m)
  let toVector be (source) as :Vector
  let toPoint be (source) as :Point
  let toList be (source) as :List
  let toMap be (source) as :Map
  let toDice be (source) as :Dice
  let toRange be (source) as :Range
  let toMessage be (source) as :Message
  let toSeries be (source) as :Series
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
              type: ":Nothing"
          - name: "toBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "toNumber"
            value:
              type: ":Nothing"
          - name: "toPercentage"
            value:
              type: ":Nothing"
          - name: "toText"
            value:
              type: ":Text"
              value: "hello"
          - name: "toTag"
            value:
              type: ":Tag"
              value: "hello"
          - name: "toMeter"
            value:
              type: ":Nothing"
          - name: "toVector"
            value:
              type: ":Nothing"
          - name: "toPoint"
            value:
              type: ":Nothing"
          - name: "toList"
            value:
              type: ":List"
              items:
                - type: ":Text"
                  value: "h"
                - type: ":Text"
                  value: "e"
                - type: ":Text"
                  value: "l"
                - type: ":Text"
                  value: "l"
                - type: ":Text"
                  value: "o"
          - name: "toMap"
            value:
              type: ":Map"
              entries: []
          - name: "toDice"
            value:
              type: ":Dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":Nothing"
          - name: "toMessage"
            value:
              type: ":Nothing"
          - name: "toSeries"
            value:
              type: ":Nothing"
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
module atomiccastfrompitag
on Start {
  let source be pi
  let toNothing be (source) as :Nothing
  let toBoolean be (source) as :Boolean
  let toNumber be (source) as :Number
  let toPercentage be (source) as :Percentage
  let toText be (source) as :Text
  let toTag be (source) as :Tag
  let toMeter be (source) as :Quantity(m)
  let toVector be (source) as :Vector
  let toPoint be (source) as :Point
  let toList be (source) as :List
  let toMap be (source) as :Map
  let toDice be (source) as :Dice
  let toRange be (source) as :Range
  let toMessage be (source) as :Message
  let toSeries be (source) as :Series
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
              type: ":Nothing"
          - name: "toBoolean"
            value:
              type: ":Boolean"
              value: true
          - name: "toNumber"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "toPercentage"
            value:
              type: ":Percentage"
              value: "0.0314159265358979"
          - name: "toText"
            value:
              type: ":Text"
              value: "3.141592653589793"
          - name: "toTag"
            value:
              type: ":Nothing"
          - name: "toMeter"
            value:
              type: ":Quantity.binary64"
              value: "3.141592653589793"
              unit: ":meter"
          - name: "toVector"
            value:
              type: ":Vector"
              x: "3.141592653589793"
              y: "0"
              z: "0"
          - name: "toPoint"
            value:
              type: ":Point"
              x: "3.141592653589793"
              y: "0"
              z: "0"
          - name: "toList"
            value:
              type: ":List"
              items: []
          - name: "toMap"
            value:
              type: ":Map"
              entries: []
          - name: "toDice"
            value:
              type: ":Dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":Nothing"
          - name: "toMessage"
            value:
              type: ":Nothing"
          - name: "toSeries"
            value:
              type: ":Nothing"
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
module atomiccastfromcustomtag
on Start {
  let source be #custom
  let toNothing be (source) as :Nothing
  let toBoolean be (source) as :Boolean
  let toNumber be (source) as :Number
  let toPercentage be (source) as :Percentage
  let toText be (source) as :Text
  let toTag be (source) as :Tag
  let toMeter be (source) as :Quantity(m)
  let toVector be (source) as :Vector
  let toPoint be (source) as :Point
  let toList be (source) as :List
  let toMap be (source) as :Map
  let toDice be (source) as :Dice
  let toRange be (source) as :Range
  let toMessage be (source) as :Message
  let toSeries be (source) as :Series
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
              type: ":Nothing"
          - name: "toBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "toNumber"
            value:
              type: ":Nothing"
          - name: "toPercentage"
            value:
              type: ":Nothing"
          - name: "toText"
            value:
              type: ":Text"
              value: ":custom"
          - name: "toTag"
            value:
              type: ":Tag"
              value: "custom"
          - name: "toMeter"
            value:
              type: ":Nothing"
          - name: "toVector"
            value:
              type: ":Nothing"
          - name: "toPoint"
            value:
              type: ":Nothing"
          - name: "toList"
            value:
              type: ":List"
              items:
                - type: ":Text"
                  value: "c"
                - type: ":Text"
                  value: "u"
                - type: ":Text"
                  value: "s"
                - type: ":Text"
                  value: "t"
                - type: ":Text"
                  value: "o"
                - type: ":Text"
                  value: "m"
          - name: "toMap"
            value:
              type: ":Map"
              entries: []
          - name: "toDice"
            value:
              type: ":Dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":Nothing"
          - name: "toMessage"
            value:
              type: ":Nothing"
          - name: "toSeries"
            value:
              type: ":Nothing"
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
module atomiccastfromvector
on Start {
  let source be :Vector(1, 2, 3)
  let toNothing be (source) as :Nothing
  let toBoolean be (source) as :Boolean
  let toNumber be (source) as :Number
  let toPercentage be (source) as :Percentage
  let toText be (source) as :Text
  let toTag be (source) as :Tag
  let toMeter be (source) as :Quantity(m)
  let toVector be (source) as :Vector
  let toPoint be (source) as :Point
  let toList be (source) as :List
  let toMap be (source) as :Map
  let toDice be (source) as :Dice
  let toRange be (source) as :Range
  let toMessage be (source) as :Message
  let toSeries be (source) as :Series
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
              type: ":Nothing"
          - name: "toBoolean"
            value:
              type: ":Boolean"
              value: true
          - name: "toNumber"
            value:
              type: ":Nothing"
          - name: "toPercentage"
            value:
              type: ":Nothing"
          - name: "toText"
            value:
              type: ":Text"
              value: "vector[x: 1, y: 2, z: 3]"
          - name: "toTag"
            value:
              type: ":Nothing"
          - name: "toMeter"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
              unit: ":meter"
          - name: "toVector"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "toPoint"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
          - name: "toList"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
          - name: "toMap"
            value:
              type: ":Map"
              entries:
                - key: "x"
                  value:
                    type: ":Number.int64"
                    value: "1"
                - key: "y"
                  value:
                    type: ":Number.int64"
                    value: "2"
                - key: "z"
                  value:
                    type: ":Number.int64"
                    value: "3"
          - name: "toDice"
            value:
              type: ":Dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":Nothing"
          - name: "toMessage"
            value:
              type: ":Nothing"
          - name: "toSeries"
            value:
              type: ":Nothing"
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
module atomiccastfrompoint
on Start {
  let source be :Point(4, 5, 6)
  let toNothing be (source) as :Nothing
  let toBoolean be (source) as :Boolean
  let toNumber be (source) as :Number
  let toPercentage be (source) as :Percentage
  let toText be (source) as :Text
  let toTag be (source) as :Tag
  let toMeter be (source) as :Quantity(m)
  let toVector be (source) as :Vector
  let toPoint be (source) as :Point
  let toList be (source) as :List
  let toMap be (source) as :Map
  let toDice be (source) as :Dice
  let toRange be (source) as :Range
  let toMessage be (source) as :Message
  let toSeries be (source) as :Series
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
              type: ":Nothing"
          - name: "toBoolean"
            value:
              type: ":Boolean"
              value: true
          - name: "toNumber"
            value:
              type: ":Nothing"
          - name: "toPercentage"
            value:
              type: ":Nothing"
          - name: "toText"
            value:
              type: ":Text"
              value: "point[x: 4, y: 5, z: 6]"
          - name: "toTag"
            value:
              type: ":Nothing"
          - name: "toMeter"
            value:
              type: ":Point"
              x: "4"
              y: "5"
              z: "6"
              unit: ":meter"
          - name: "toVector"
            value:
              type: ":Vector"
              x: "4"
              y: "5"
              z: "6"
          - name: "toPoint"
            value:
              type: ":Point"
              x: "4"
              y: "5"
              z: "6"
          - name: "toList"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "4"
                - type: ":Number.int64"
                  value: "5"
                - type: ":Number.int64"
                  value: "6"
          - name: "toMap"
            value:
              type: ":Map"
              entries:
                - key: "x"
                  value:
                    type: ":Number.int64"
                    value: "4"
                - key: "y"
                  value:
                    type: ":Number.int64"
                    value: "5"
                - key: "z"
                  value:
                    type: ":Number.int64"
                    value: "6"
          - name: "toDice"
            value:
              type: ":Dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":Nothing"
          - name: "toMessage"
            value:
              type: ":Nothing"
          - name: "toSeries"
            value:
              type: ":Nothing"
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
module atomiccastfromlist
on Start {
  let source be [1, 2, 3]
  let toNothing be (source) as :Nothing
  let toBoolean be (source) as :Boolean
  let toNumber be (source) as :Number
  let toPercentage be (source) as :Percentage
  let toText be (source) as :Text
  let toTag be (source) as :Tag
  let toMeter be (source) as :Quantity(m)
  let toVector be (source) as :Vector
  let toPoint be (source) as :Point
  let toList be (source) as :List
  let toMap be (source) as :Map
  let toDice be (source) as :Dice
  let toRange be (source) as :Range
  let toMessage be (source) as :Message
  let toSeries be (source) as :Series
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
              type: ":Nothing"
          - name: "toBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "toNumber"
            value:
              type: ":Nothing"
          - name: "toPercentage"
            value:
              type: ":Nothing"
          - name: "toText"
            value:
              type: ":Text"
              value: "[1, 2, 3]"
          - name: "toTag"
            value:
              type: ":Nothing"
          - name: "toMeter"
            value:
              type: ":Nothing"
          - name: "toVector"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "toPoint"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
          - name: "toList"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
          - name: "toMap"
            value:
              type: ":Map"
              entries: []
          - name: "toDice"
            value:
              type: ":Dice"
              rolls:
                - 3
                - 2
                - 1
          - name: "toRange"
            value:
              type: ":Nothing"
          - name: "toMessage"
            value:
              type: ":Nothing"
          - name: "toSeries"
            value:
              type: ":Nothing"
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
module atomiccastfrommap
on Start {
  let source be [x: 1, y: 2, z: 3]
  let toNothing be (source) as :Nothing
  let toBoolean be (source) as :Boolean
  let toNumber be (source) as :Number
  let toPercentage be (source) as :Percentage
  let toText be (source) as :Text
  let toTag be (source) as :Tag
  let toMeter be (source) as :Quantity(m)
  let toVector be (source) as :Vector
  let toPoint be (source) as :Point
  let toList be (source) as :List
  let toMap be (source) as :Map
  let toDice be (source) as :Dice
  let toRange be (source) as :Range
  let toMessage be (source) as :Message
  let toSeries be (source) as :Series
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
              type: ":Nothing"
          - name: "toBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "toNumber"
            value:
              type: ":Nothing"
          - name: "toPercentage"
            value:
              type: ":Nothing"
          - name: "toText"
            value:
              type: ":Text"
              value: "map[x: 1, y: 2, z: 3]"
          - name: "toTag"
            value:
              type: ":Nothing"
          - name: "toMeter"
            value:
              type: ":Nothing"
          - name: "toVector"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "toPoint"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
          - name: "toList"
            value:
              type: ":List"
              items: []
          - name: "toMap"
            value:
              type: ":Map"
              entries:
                - key: "x"
                  value:
                    type: ":Number.int64"
                    value: "1"
                - key: "y"
                  value:
                    type: ":Number.int64"
                    value: "2"
                - key: "z"
                  value:
                    type: ":Number.int64"
                    value: "3"
          - name: "toDice"
            value:
              type: ":Dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":Nothing"
          - name: "toMessage"
            value:
              type: ":Nothing"
          - name: "toSeries"
            value:
              type: ":Nothing"
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
module atomiccastfromdice
on Start {
  let source be ([3, 2, 1]) as :Dice
  let toNothing be (source) as :Nothing
  let toBoolean be (source) as :Boolean
  let toNumber be (source) as :Number
  let toPercentage be (source) as :Percentage
  let toText be (source) as :Text
  let toTag be (source) as :Tag
  let toMeter be (source) as :Quantity(m)
  let toVector be (source) as :Vector
  let toPoint be (source) as :Point
  let toList be (source) as :List
  let toMap be (source) as :Map
  let toDice be (source) as :Dice
  let toRange be (source) as :Range
  let toMessage be (source) as :Message
  let toSeries be (source) as :Series
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
              type: ":Nothing"
          - name: "toBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "toNumber"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "toPercentage"
            value:
              type: ":Percentage"
              value: "0.06"
          - name: "toText"
            value:
              type: ":Text"
              value: "dice[3, 2, 1]"
          - name: "toTag"
            value:
              type: ":Nothing"
          - name: "toMeter"
            value:
              type: ":Nothing"
          - name: "toVector"
            value:
              type: ":Vector"
              x: "3"
              y: "2"
              z: "1"
          - name: "toPoint"
            value:
              type: ":Point"
              x: "3"
              y: "2"
              z: "1"
          - name: "toList"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "3"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "1"
          - name: "toMap"
            value:
              type: ":Map"
              entries: []
          - name: "toDice"
            value:
              type: ":Dice"
              rolls:
                - 3
                - 2
                - 1
          - name: "toRange"
            value:
              type: ":Nothing"
          - name: "toMessage"
            value:
              type: ":Nothing"
          - name: "toSeries"
            value:
              type: ":Nothing"
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
module atomiccastfromrange
on Start {
  let source be (from 1 to 3) as :Range
  let toNothing be (source) as :Nothing
  let toBoolean be (source) as :Boolean
  let toNumber be (source) as :Number
  let toPercentage be (source) as :Percentage
  let toText be (source) as :Text
  let toTag be (source) as :Tag
  let toMeter be (source) as :Quantity(m)
  let toVector be (source) as :Vector
  let toPoint be (source) as :Point
  let toList be (source) as :List
  let toMap be (source) as :Map
  let toDice be (source) as :Dice
  let toRange be (source) as :Range
  let toMessage be (source) as :Message
  let toSeries be (source) as :Series
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
              type: ":Nothing"
          - name: "toBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "toNumber"
            value:
              type: ":Nothing"
          - name: "toPercentage"
            value:
              type: ":Nothing"
          - name: "toText"
            value:
              type: ":Text"
              value: "range[1 to 3 step 1]"
          - name: "toTag"
            value:
              type: ":Nothing"
          - name: "toMeter"
            value:
              type: ":Nothing"
          - name: "toVector"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
          - name: "toPoint"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
          - name: "toList"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "3"
          - name: "toMap"
            value:
              type: ":Map"
              entries: []
          - name: "toDice"
            value:
              type: ":Dice"
              rolls: []
          - name: "toRange"
            value:
              type: ":Range.int64"
              from: "1"
              to: "3"
              step: "1"
          - name: "toMessage"
            value:
              type: ":Nothing"
          - name: "toSeries"
            value:
              type: ":Nothing"
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
module atomiccastbooleanfalsetotag
on Start {
  let source be false
  let toTag be (source) as :Tag
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
              type: ":Tag"
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
module atomiccastbooleantextwordstotag
on Start {
  let upperTrue be ('True') as :Tag
  let lowerTrue be ('true') as :Tag
  let upperFalse be ('False') as :Tag
  let lowerFalse be ('false') as :Tag
  let upperWord be ('Hello') as :Tag
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
              type: ":Tag"
              value: "true"
          - name: "lowerTrue"
            value:
              type: ":Tag"
              value: "true"
          - name: "upperFalse"
            value:
              type: ":Tag"
              value: "false"
          - name: "lowerFalse"
            value:
              type: ":Tag"
              value: "false"
          - name: "upperWord"
            value:
              type: ":Nothing"
```
