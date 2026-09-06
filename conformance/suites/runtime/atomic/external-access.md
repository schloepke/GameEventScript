---
formatVersion: 1
suiteId: "runtime.atomic.external-access"
title: "RuntimeAtomicExternalAccess"
categories: [conformance]
---

# RuntimeAtomicExternalAccess

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite isolates external-type construction and member access through the portable host boundary.

---

## Test: emit static message with one argument

This runtime case exercises “emit static message with one argument” and verifies the declared messages, values, and execution result.

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
  - name: "emit static message with one argument.ges"
    program: main
```

### Source code under test

```ges
module atomicemitmessage
on Start(value) {
  emit Done(value: value)
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
        - name: "value"
          value:
            type: ":Number.int64"
            value: "21"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "21"
```

---

## Test: emit message value loaded from literal

This runtime case exercises “emit message value loaded from literal” and verifies the declared messages, values, and execution result.

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
  - name: "emit message value loaded from literal.ges"
    program: main
```

### Source code under test

```ges
module atomicloadmessage
on Start(value) {
  let msg be Done(value: value)
  emit msg
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
        - name: "value"
          value:
            type: ":Number.int64"
            value: "21"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "21"
```

---

## Test: handler literal binds ordered arguments into message value

This runtime case exercises “handler literal binds ordered arguments into message value” and verifies the declared messages, values, and execution result.

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
  - name: "handler literal binds ordered arguments into message value.ges"
    program: main
```

### Source code under test

```ges
module atomicbindhandler
on Start(unit, target) {
  let shoot be Shoot(unit, target)
  let msg be shoot(unit: unit, target: target)
  emit msg
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
        - name: "unit"
          value:
            type: ":Text"
            value: "u1"
        - name: "target"
          value:
            type: ":Text"
            value: "t1"
    local:
      - name: "Shoot"
        args:
          - name: "unit"
            value:
              type: ":Text"
              value: "u1"
          - name: "target"
            value:
              type: ":Text"
              value: "t1"
```

---

## Test: handler binding with mismatched ordered labels yields no emitted message

This runtime case exercises “handler binding with mismatched ordered labels yields no emitted message” and verifies the declared messages, values, and execution result.

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
  - name: "handler binding with mismatched ordered labels yields no emitted message.ges"
    program: main
```

### Source code under test

```ges
module atomicinvalidbindhandler
on Start(unit, hp) {
  let shoot be Shoot(unit, target)
  let invalid be shoot(unit: unit, hp: hp)
  emit invalid
  emit Done
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
        - name: "unit"
          value:
            type: ":Text"
            value: "u1"
        - name: "hp"
          value:
            type: ":Number.int64"
            value: "10"
    local:
      - name: "Done"
        args: []
```

---

## Test: message input values roundtrip unchanged

This runtime case exercises “message input values roundtrip unchanged” and verifies the declared messages, values, and execution result.

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
  - name: "message input values roundtrip unchanged.ges"
    program: main
```

### Source code under test

```ges
module atomicmessageinputroundtrip
on Echo(nothingValue, falseValue, trueValue, integerValue, floatValue, infinityValue, unitInteger, unitFloat, percentageValue, textValue, emptyText, tagValue, vectorValue, unitVector, pointValue, unitPoint, listValue, emptyList, mapValue, emptyMap, customValue, diceValue, rangeValue, floatRangeValue, nestedMessage) {
  emit Done(nothingValue: nothingValue, falseValue: falseValue, trueValue: trueValue, integerValue: integerValue, floatValue: floatValue, infinityValue: infinityValue, unitInteger: unitInteger, unitFloat: unitFloat, percentageValue: percentageValue, textValue: textValue, emptyText: emptyText, tagValue: tagValue, vectorValue: vectorValue, unitVector: unitVector, pointValue: pointValue, unitPoint: unitPoint, listValue: listValue, emptyList: emptyList, mapValue: mapValue, emptyMap: emptyMap, customValue: customValue, diceValue: diceValue, rangeValue: rangeValue, floatRangeValue: floatRangeValue, nestedMessage: nestedMessage)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Echo | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "nothingValue"
          value:
            type: ":Nothing"
        - name: "falseValue"
          value:
            type: ":Boolean"
            value: false
        - name: "trueValue"
          value:
            type: ":Boolean"
            value: true
        - name: "integerValue"
          value:
            type: ":Number.int64"
            value: "42"
        - name: "floatValue"
          value:
            type: ":Number.binary64"
            value: "10.75"
        - name: "infinityValue"
          value:
            type: ":Number.binary64"
            value: "Infinity"
        - name: "unitInteger"
          value:
            type: ":Quantity.int64"
            value: "7"
            unit: ":meter"
        - name: "unitFloat"
          value:
            type: ":Quantity.binary64"
            value: "90"
            unit: ":degree"
        - name: "percentageValue"
          value:
            type: ":Percentage"
            value: "0.25"
        - name: "textValue"
          value:
            type: ":Text"
            value: "Hello"
        - name: "emptyText"
          value:
            type: ":Text"
            value: ""
        - name: "tagValue"
          value:
            type: ":Tag"
            value: "ready"
        - name: "vectorValue"
          value:
            type: ":Vector"
            x: "1.5"
            y: "-2"
            z: "3"
        - name: "unitVector"
          value:
            type: ":Vector"
            x: "1"
            y: "2"
            z: "3"
            unit: ":meter"
        - name: "pointValue"
          value:
            type: ":Point"
            x: "4"
            y: "5"
            z: "6"
        - name: "unitPoint"
          value:
            type: ":Point"
            x: "7"
            y: "8"
            z: "9"
            unit: ":meter"
        - name: "listValue"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
              - type: ":Text"
                value: "two"
              - type: ":Tag"
                value: "three"
        - name: "emptyList"
          value:
            type: ":List"
            items: []
        - name: "mapValue"
          value:
            type: ":Map"
            entries:
              - key: "alpha"
                value:
                  type: ":Number.int64"
                  value: "1"
              - key: "beta"
                value:
                  type: ":Text"
                  value: "two"
        - name: "emptyMap"
          value:
            type: ":Map"
            entries: []
        - name: "customValue"
          value:
            type: ":Unit"
            entries:
              - key: "hp"
                value:
                  type: ":Number.int64"
                  value: "7"
              - key: "name"
                value:
                  type: ":Text"
                  value: "Knight"
        - name: "diceValue"
          value:
            type: ":Dice"
            rolls:
              - 6
              - 5
              - 2
        - name: "rangeValue"
          value:
            type: ":Range.int64"
            from: "1"
            to: "3"
            step: "1"
        - name: "floatRangeValue"
          value:
            type: ":Range.binary64"
            from: "1.5"
            to: "3.5"
            step: "0.5"
        - name: "nestedMessage"
          value:
            type: ":Message"
            message:
              name: "Nested"
              tags:
                - "inner"
                - "debug"
              args:
                - name: "amount"
                  value:
                    type: ":Number.int64"
                    value: "12"
                - name: "payload"
                  value:
                    type: ":Text"
                    value: "ok"
    local:
      - name: "Done"
        args:
          - name: "nothingValue"
            value:
              type: ":Nothing"
          - name: "falseValue"
            value:
              type: ":Boolean"
              value: false
          - name: "trueValue"
            value:
              type: ":Boolean"
              value: true
          - name: "integerValue"
            value:
              type: ":Number.int64"
              value: "42"
          - name: "floatValue"
            value:
              type: ":Number.binary64"
              value: "10.75"
          - name: "infinityValue"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "unitInteger"
            value:
              type: ":Quantity.int64"
              value: "7"
              unit: ":meter"
          - name: "unitFloat"
            value:
              type: ":Quantity.binary64"
              value: "90"
              unit: ":degree"
          - name: "percentageValue"
            value:
              type: ":Percentage"
              value: "0.25"
          - name: "textValue"
            value:
              type: ":Text"
              value: "Hello"
          - name: "emptyText"
            value:
              type: ":Text"
              value: ""
          - name: "tagValue"
            value:
              type: ":Tag"
              value: "ready"
          - name: "vectorValue"
            value:
              type: ":Vector"
              x: "1.5"
              y: "-2"
              z: "3"
          - name: "unitVector"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
              unit: ":meter"
          - name: "pointValue"
            value:
              type: ":Point"
              x: "4"
              y: "5"
              z: "6"
          - name: "unitPoint"
            value:
              type: ":Point"
              x: "7"
              y: "8"
              z: "9"
              unit: ":meter"
          - name: "listValue"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Text"
                  value: "two"
                - type: ":Tag"
                  value: "three"
          - name: "emptyList"
            value:
              type: ":List"
              items: []
          - name: "mapValue"
            value:
              type: ":Map"
              entries:
                - key: "alpha"
                  value:
                    type: ":Number.int64"
                    value: "1"
                - key: "beta"
                  value:
                    type: ":Text"
                    value: "two"
          - name: "emptyMap"
            value:
              type: ":Map"
              entries: []
          - name: "customValue"
            value:
              type: ":Unit"
              entries:
                - key: "hp"
                  value:
                    type: ":Number.int64"
                    value: "7"
                - key: "name"
                  value:
                    type: ":Text"
                    value: "Knight"
          - name: "diceValue"
            value:
              type: ":Dice"
              rolls:
                - 6
                - 5
                - 2
          - name: "rangeValue"
            value:
              type: ":Range.int64"
              from: "1"
              to: "3"
              step: "1"
          - name: "floatRangeValue"
            value:
              type: ":Range.binary64"
              from: "1.5"
              to: "3.5"
              step: "0.5"
          - name: "nestedMessage"
            value:
              type: ":Message"
              message:
                name: "Nested"
                tags:
                  - "inner"
                  - "debug"
                args:
                  - name: "amount"
                    value:
                      type: ":Number.int64"
                      value: "12"
                  - name: "payload"
                    value:
                      type: ":Text"
                      value: "ok"
```

---

## Test: static emit and publish variants

This runtime case exercises “static emit and publish variants” and verifies the declared messages, values, and execution result.

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
  - name: "static emit and publish variants.ges"
    program: main
```

### Source code under test

```ges
module atomicemitpublishstatic
on Start {
  let tags be [#radio, #command, #radio]
  emit LocalZero
  emit LocalTagged(value: 1) with #local, #visible, #local
  publish OutZero
  publish OutTagged(value: 2, kind: #fire) with tags
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
      - name: "LocalZero"
        args: []
      - name: "LocalTagged"
        tags:
          - "local"
          - "visible"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      - name: "OutZero"
        args: []
      - name: "OutTagged"
        tags:
          - "radio"
          - "command"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "kind"
            value:
              type: ":Tag"
              value: "fire"
    outbound:
      - name: "OutZero"
        args: []
      - name: "OutTagged"
        tags:
          - "radio"
          - "command"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "kind"
            value:
              type: ":Tag"
              value: "fire"
```

---

## Test: message value emit and publish variants

This runtime case exercises “message value emit and publish variants” and verifies the declared messages, values, and execution result.

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
  - name: "message value emit and publish variants.ges"
    program: main
```

### Source code under test

```ges
module atomicemitpublishmessagevalues
on Start {
  let tags be [#alpha, #beta, #alpha]
  let emitMsg be DynamicEmit(value: 3, label: 'local')
  let publishMsg be DynamicPublish(value: 4, label: 'bus')
  emit emitMsg
  emit emitMsg with tags
  publish publishMsg
  publish publishMsg with #bus, tags
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
      - name: "DynamicEmit"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "label"
            value:
              type: ":Text"
              value: "local"
      - name: "DynamicEmit"
        tags:
          - "alpha"
          - "beta"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "label"
            value:
              type: ":Text"
              value: "local"
      - name: "DynamicPublish"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "label"
            value:
              type: ":Text"
              value: "bus"
      - name: "DynamicPublish"
        tags:
          - "bus"
          - "alpha"
          - "beta"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "label"
            value:
              type: ":Text"
              value: "bus"
    outbound:
      - name: "DynamicPublish"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "label"
            value:
              type: ":Text"
              value: "bus"
      - name: "DynamicPublish"
        tags:
          - "bus"
          - "alpha"
          - "beta"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "label"
            value:
              type: ":Text"
              value: "bus"
```

---

## Test: emit and publish dispatch locally while publish also reaches outbound sink

This runtime case exercises “emit and publish dispatch locally while publish also reaches outbound sink” and verifies the declared messages, values, and execution result.

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
  - name: "emit and publish dispatch locally while publish also reaches outbound sink.ges"
    program: main
```

### Source code under test

```ges
module atomicemitpublishdispatch
on Start {
  emit Local(value: 1)
  publish Remote(value: 2)
}

on Local(value) {
  emit Seen(value: value)
}

on Remote(value) {
  emit ShouldNotRun(value: value)
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
      - name: "Local"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      - name: "Remote"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "2"
      - name: "Seen"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      - name: "ShouldNotRun"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "2"
    outbound:
      - name: "Remote"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "2"
```

---

## Test: math intrinsics and series creation

This runtime case exercises “math intrinsics and series creation” and verifies the declared messages, values, and execution result.

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
  - name: "math intrinsics and series creation.ges"
    program: main
```

### Source code under test

```ges
module atomicmathintrinsicsandseries
on Start(value) {
  let natural be from 2 to 11 step 3
  let fib be series fibonacci
  let fact be series factorial
  let naturalZero be natural[1]
  let naturalThree be natural[4]
  let fibSeven be fib[:term 7]
  let factFive be fact[:term 5]
  emit Done(floorBare: floor value, ceilCall: ceil(value), truncateNegative: truncate -10.7, halfEven: round half even 12.5, halfUp: round half up -12.5, halfDown: round half down -12.5, wrapDegree: wrap degree 370°, radians: rad 180°, degrees: deg 3.1415926535897933, naturalIsSeries: natural is :Series, naturalZero: naturalZero, naturalThree: naturalThree, fibSeven: fibSeven, factFive: factFive, floorPredicate: nothing)
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
        - name: "value"
          value:
            type: ":Number.binary64"
            value: "10.75"
    local:
      - name: "Done"
        args:
          - name: "floorBare"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "ceilCall"
            value:
              type: ":Number.int64"
              value: "11"
          - name: "truncateNegative"
            value:
              type: ":Number.int64"
              value: "-10"
          - name: "halfEven"
            value:
              type: ":Number.int64"
              value: "12"
          - name: "halfUp"
            value:
              type: ":Number.int64"
              value: "-13"
          - name: "halfDown"
            value:
              type: ":Number.int64"
              value: "-12"
          - name: "wrapDegree"
            value:
              type: ":Quantity.binary64"
              value: "10"
              unit: ":degree"
          - name: "radians"
            value:
              type: ":Number.binary64"
              value: "3.141592653589793"
          - name: "degrees"
            value:
              type: ":Quantity.binary64"
              value: "180"
              unit: ":degree"
          - name: "naturalIsSeries"
            value:
              type: ":Boolean"
              value: false
          - name: "naturalZero"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "naturalThree"
            value:
              type: ":Number.int64"
              value: "11"
          - name: "fibSeven"
            value:
              type: ":Number.int64"
              value: "13"
          - name: "factFive"
            value:
              type: ":Number.int64"
              value: "120"
          - name: "floorPredicate"
            value:
              type: ":Nothing"
```

---

## Test: external extension calls

This runtime case exercises “external extension calls” and verifies the declared messages, values, and execution result.

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
  - name: "external extension calls.ges"
    program: main
```

### Source code under test

```ges
module atomiccallexternal
on Start(value, heading, target) {
  let nothingValue be nothing
  let listValue be [1, 'two', #three]
  let mapValue be [alpha: 1, beta: 'two']
  let diceValue be ([6, 4, 2]) as :Dice
  let messageValue be Ping(amount: 7, label: 'ok')
  emit Done(floored: floor value, flooredCall: floor(value), maxed: max of 2 and 8 and 5, turn: :nav.shortestTurn from: heading to: target, northPredicate: heading is :nav.isNorth, vectorTotal: :test.vectorSum :Vector(1m, 2m, 3m), echoNothing: :test.echo nothingValue, echoBoolean: :test.echo true, echoInteger: :test.echo 12, echoFloat: :test.echo 12.5, echoPercentage: :test.echo 25%, echoMeter: :test.echo 10m, echoText: :test.echo 'hello', echoTag: :test.echo #ready, echoVector: :test.echo :Vector(1m, 2m, 3m), echoPoint: :test.echo :Point(4m, 5m, 6m), echoList: :test.echo listValue, echoMap: :test.echo mapValue, echoDice: :test.echo diceValue, echoMessage: :test.echo messageValue, echoPredicate: value is :test.echo)
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
        - name: "value"
          value:
            type: ":Number.binary64"
            value: "10.75"
        - name: "heading"
          value:
            type: ":Quantity.binary64"
            value: "350"
            unit: ":degree"
        - name: "target"
          value:
            type: ":Quantity.binary64"
            value: "10"
            unit: ":degree"
    local:
      - name: "Done"
        args:
          - name: "floored"
            value:
              type: ":Number.binary64"
              value: "10"
          - name: "flooredCall"
            value:
              type: ":Number.binary64"
              value: "10"
          - name: "maxed"
            value:
              type: ":Number.binary64"
              value: "8"
          - name: "turn"
            value:
              type: ":Quantity.binary64"
              value: "20"
              unit: ":degree"
          - name: "northPredicate"
            value:
              type: ":Boolean"
              value: true
          - name: "vectorTotal"
            value:
              type: ":Quantity.binary64"
              value: "6"
              unit: ":meter"
          - name: "echoNothing"
            value:
              type: ":Nothing"
          - name: "echoBoolean"
            value:
              type: ":Boolean"
              value: true
          - name: "echoInteger"
            value:
              type: ":Number.int64"
              value: "12"
          - name: "echoFloat"
            value:
              type: ":Number.binary64"
              value: "12.5"
          - name: "echoPercentage"
            value:
              type: ":Percentage"
              value: "0.25"
          - name: "echoMeter"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: ":meter"
          - name: "echoText"
            value:
              type: ":Text"
              value: "hello"
          - name: "echoTag"
            value:
              type: ":Tag"
              value: "ready"
          - name: "echoVector"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
              unit: ":meter"
          - name: "echoPoint"
            value:
              type: ":Point"
              x: "4"
              y: "5"
              z: "6"
              unit: ":meter"
          - name: "echoList"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Text"
                  value: "two"
                - type: ":Tag"
                  value: "three"
          - name: "echoMap"
            value:
              type: ":Map"
              entries:
                - key: "alpha"
                  value:
                    type: ":Number.int64"
                    value: "1"
                - key: "beta"
                  value:
                    type: ":Text"
                    value: "two"
          - name: "echoDice"
            value:
              type: ":Dice"
              rolls:
                - 6
                - 4
                - 2
          - name: "echoMessage"
            value:
              type: ":Message"
              message:
                name: "Ping"
                args:
                  - name: "amount"
                    value:
                      type: ":Number.int64"
                      value: "7"
                  - name: "label"
                    value:
                      type: ":Text"
                      value: "ok"
          - name: "echoPredicate"
            value:
              type: ":Nothing"
```
