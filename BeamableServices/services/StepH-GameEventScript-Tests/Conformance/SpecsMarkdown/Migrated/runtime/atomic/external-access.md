---
formatVersion: 1
suiteId: "runtime.atomic.external-access"
title: "RuntimeAtomicExternalAccess"
categories: [conformance]
tags: [migrated-json-v1]
---

# RuntimeAtomicExternalAccess

Mechanically migrated from the former JSON conformance corpus.

## Test: emit static message with one argument

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

```ges
module AtomicEmitMessage
on Start(value) {
  emit Done(value: value)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "value"
          value:
            type: ":integer"
            value: "21"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "21"
```

## Test: emit message value loaded from literal

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

```ges
module AtomicLoadMessage
on Start(value) {
  let msg be Done(value: value)
  emit msg
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "value"
          value:
            type: ":integer"
            value: "21"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "21"
```

## Test: handler literal binds ordered arguments into message value

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

```ges
module AtomicBindHandler
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

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "unit"
          value:
            type: ":text"
            value: "u_1"
        - name: "target"
          value:
            type: ":text"
            value: "t_1"
    local:
      - name: "Shoot"
        args:
          - name: "unit"
            value:
              type: ":text"
              value: "u_1"
          - name: "target"
            value:
              type: ":text"
              value: "t_1"
```

## Test: handler binding with mismatched ordered labels yields no emitted message

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

```ges
module AtomicInvalidBindHandler
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

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "unit"
          value:
            type: ":text"
            value: "u_1"
        - name: "hp"
          value:
            type: ":integer"
            value: "10"
    local:
      - name: "Done"
        args: []
```

## Test: message input values roundtrip unchanged

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

```ges
module AtomicMessageInputRoundtrip
on Echo(nothingValue, falseValue, trueValue, integerValue, floatValue, infinityValue, unitInteger, unitFloat, percentageValue, textValue, emptyText, tagValue, vectorValue, unitVector, pointValue, unitPoint, listValue, emptyList, mapValue, emptyMap, customValue, diceValue, rangeValue, floatRangeValue, nestedMessage) {
  emit Done(nothingValue: nothingValue, falseValue: falseValue, trueValue: trueValue, integerValue: integerValue, floatValue: floatValue, infinityValue: infinityValue, unitInteger: unitInteger, unitFloat: unitFloat, percentageValue: percentageValue, textValue: textValue, emptyText: emptyText, tagValue: tagValue, vectorValue: vectorValue, unitVector: unitVector, pointValue: pointValue, unitPoint: unitPoint, listValue: listValue, emptyList: emptyList, mapValue: mapValue, emptyMap: emptyMap, customValue: customValue, diceValue: diceValue, rangeValue: rangeValue, floatRangeValue: floatRangeValue, nestedMessage: nestedMessage)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Echo | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "nothingValue"
          value:
            type: ":nothing"
        - name: "falseValue"
          value:
            type: ":boolean"
            value: false
        - name: "trueValue"
          value:
            type: ":boolean"
            value: true
        - name: "integerValue"
          value:
            type: ":integer"
            value: "42"
        - name: "floatValue"
          value:
            type: ":float"
            value: "10.75"
        - name: "infinityValue"
          value:
            type: ":float"
            value: "Infinity"
        - name: "unitInteger"
          value:
            type: ":integer"
            value: "7"
            unit: ":meter"
        - name: "unitFloat"
          value:
            type: ":float"
            value: "90"
            unit: ":degree"
        - name: "percentageValue"
          value:
            type: ":percentage"
            value: "0.25"
        - name: "textValue"
          value:
            type: ":text"
            value: "Hello"
        - name: "emptyText"
          value:
            type: ":text"
            value: ""
        - name: "tagValue"
          value:
            type: ":tag"
            value: "ready"
        - name: "vectorValue"
          value:
            type: ":vector"
            x: "1.5"
            y: "-2"
            z: "3"
        - name: "unitVector"
          value:
            type: ":vector"
            x: "1"
            y: "2"
            z: "3"
            unit: ":meter"
        - name: "pointValue"
          value:
            type: ":point"
            x: "4"
            y: "5"
            z: "6"
        - name: "unitPoint"
          value:
            type: ":point"
            x: "7"
            y: "8"
            z: "9"
            unit: ":meter"
        - name: "listValue"
          value:
            type: ":list"
            items:
              - type: ":integer"
                value: "1"
              - type: ":text"
                value: "two"
              - type: ":tag"
                value: "three"
        - name: "emptyList"
          value:
            type: ":list"
            items: []
        - name: "mapValue"
          value:
            type: ":map"
            entries:
              - key: "alpha"
                value:
                  type: ":integer"
                  value: "1"
              - key: "beta"
                value:
                  type: ":text"
                  value: "two"
        - name: "emptyMap"
          value:
            type: ":map"
            entries: []
        - name: "customValue"
          value:
            type: ":Unit"
            entries:
              - key: "hp"
                value:
                  type: ":integer"
                  value: "7"
              - key: "name"
                value:
                  type: ":text"
                  value: "Knight"
        - name: "diceValue"
          value:
            type: ":dice"
            rolls:
              - 6
              - 5
              - 2
        - name: "rangeValue"
          value:
            type: ":range"
            from: "1"
            to: "3"
            step: "1"
        - name: "floatRangeValue"
          value:
            type: ":range"
            from: "1.5"
            to: "3.5"
            step: "0.5"
        - name: "nestedMessage"
          value:
            type: ":message"
            message:
              name: "Nested"
              tags:
                - "inner"
                - "debug"
              args:
                - name: "amount"
                  value:
                    type: ":integer"
                    value: "12"
                - name: "payload"
                  value:
                    type: ":text"
                    value: "ok"
    local:
      - name: "Done"
        args:
          - name: "nothingValue"
            value:
              type: ":nothing"
          - name: "falseValue"
            value:
              type: ":boolean"
              value: false
          - name: "trueValue"
            value:
              type: ":boolean"
              value: true
          - name: "integerValue"
            value:
              type: ":integer"
              value: "42"
          - name: "floatValue"
            value:
              type: ":float"
              value: "10.75"
          - name: "infinityValue"
            value:
              type: ":float"
              value: "Infinity"
          - name: "unitInteger"
            value:
              type: ":integer"
              value: "7"
              unit: ":meter"
          - name: "unitFloat"
            value:
              type: ":float"
              value: "90"
              unit: ":degree"
          - name: "percentageValue"
            value:
              type: ":percentage"
              value: "0.25"
          - name: "textValue"
            value:
              type: ":text"
              value: "Hello"
          - name: "emptyText"
            value:
              type: ":text"
              value: ""
          - name: "tagValue"
            value:
              type: ":tag"
              value: "ready"
          - name: "vectorValue"
            value:
              type: ":vector"
              x: "1.5"
              y: "-2"
              z: "3"
          - name: "unitVector"
            value:
              type: ":vector"
              x: "1"
              y: "2"
              z: "3"
              unit: ":meter"
          - name: "pointValue"
            value:
              type: ":point"
              x: "4"
              y: "5"
              z: "6"
          - name: "unitPoint"
            value:
              type: ":point"
              x: "7"
              y: "8"
              z: "9"
              unit: ":meter"
          - name: "listValue"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":text"
                  value: "two"
                - type: ":tag"
                  value: "three"
          - name: "emptyList"
            value:
              type: ":list"
              items: []
          - name: "mapValue"
            value:
              type: ":map"
              entries:
                - key: "alpha"
                  value:
                    type: ":integer"
                    value: "1"
                - key: "beta"
                  value:
                    type: ":text"
                    value: "two"
          - name: "emptyMap"
            value:
              type: ":map"
              entries: []
          - name: "customValue"
            value:
              type: ":Unit"
              entries:
                - key: "hp"
                  value:
                    type: ":integer"
                    value: "7"
                - key: "name"
                  value:
                    type: ":text"
                    value: "Knight"
          - name: "diceValue"
            value:
              type: ":dice"
              rolls:
                - 6
                - 5
                - 2
          - name: "rangeValue"
            value:
              type: ":range"
              from: "1"
              to: "3"
              step: "1"
          - name: "floatRangeValue"
            value:
              type: ":range"
              from: "1.5"
              to: "3.5"
              step: "0.5"
          - name: "nestedMessage"
            value:
              type: ":message"
              message:
                name: "Nested"
                tags:
                  - "inner"
                  - "debug"
                args:
                  - name: "amount"
                    value:
                      type: ":integer"
                      value: "12"
                  - name: "payload"
                    value:
                      type: ":text"
                      value: "ok"
```

## Test: static emit and publish variants

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

```ges
module AtomicEmitPublishStatic
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
              type: ":integer"
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
              type: ":integer"
              value: "2"
          - name: "kind"
            value:
              type: ":tag"
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
              type: ":integer"
              value: "2"
          - name: "kind"
            value:
              type: ":tag"
              value: "fire"
```

## Test: message value emit and publish variants

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

```ges
module AtomicEmitPublishMessageValues
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
              type: ":integer"
              value: "3"
          - name: "label"
            value:
              type: ":text"
              value: "local"
      - name: "DynamicEmit"
        tags:
          - "alpha"
          - "beta"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "3"
          - name: "label"
            value:
              type: ":text"
              value: "local"
      - name: "DynamicPublish"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "4"
          - name: "label"
            value:
              type: ":text"
              value: "bus"
      - name: "DynamicPublish"
        tags:
          - "bus"
          - "alpha"
          - "beta"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "4"
          - name: "label"
            value:
              type: ":text"
              value: "bus"
    outbound:
      - name: "DynamicPublish"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "4"
          - name: "label"
            value:
              type: ":text"
              value: "bus"
      - name: "DynamicPublish"
        tags:
          - "bus"
          - "alpha"
          - "beta"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "4"
          - name: "label"
            value:
              type: ":text"
              value: "bus"
```

## Test: emit and publish dispatch locally while publish also reaches outbound sink

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

```ges
module AtomicEmitPublishDispatch
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
              type: ":integer"
              value: "1"
      - name: "Remote"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "2"
      - name: "Seen"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "1"
      - name: "ShouldNotRun"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "2"
    outbound:
      - name: "Remote"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "2"
```

## Test: math intrinsics and series creation

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

```ges
module AtomicMathIntrinsicsAndSeries
on Start(value) {
  let natural be from 2 to 11 step 3
  let fib be series fibonacci
  let fact be series factorial
  let naturalZero be natural[1]
  let naturalThree be natural[4]
  let fibSeven be fib[:term 7]
  let factFive be fact[:term 5]
  emit Done(floorBare: floor value, ceilCall: ceil(value), truncateNegative: truncate -10.7, halfEven: round half even 12.5, halfUp: round half up -12.5, halfDown: round half down -12.5, wrapDegree: wrap degree 370°, radians: rad 180°, degrees: deg 3.1415926535897933, naturalIsSeries: natural is :series, naturalZero: naturalZero, naturalThree: naturalThree, fibSeven: fibSeven, factFive: factFive, floorPredicate: nothing)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "value"
          value:
            type: ":float"
            value: "10.75"
    local:
      - name: "Done"
        args:
          - name: "floorBare"
            value:
              type: ":integer"
              value: "10"
          - name: "ceilCall"
            value:
              type: ":integer"
              value: "11"
          - name: "truncateNegative"
            value:
              type: ":integer"
              value: "-10"
          - name: "halfEven"
            value:
              type: ":integer"
              value: "12"
          - name: "halfUp"
            value:
              type: ":integer"
              value: "-13"
          - name: "halfDown"
            value:
              type: ":integer"
              value: "-12"
          - name: "wrapDegree"
            value:
              type: ":float"
              value: "10"
              unit: ":degree"
          - name: "radians"
            value:
              type: ":float"
              value: "3.141592653589793"
          - name: "degrees"
            value:
              type: ":float"
              value: "180"
              unit: ":degree"
          - name: "naturalIsSeries"
            value:
              type: ":boolean"
              value: false
          - name: "naturalZero"
            value:
              type: ":integer"
              value: "2"
          - name: "naturalThree"
            value:
              type: ":integer"
              value: "11"
          - name: "fibSeven"
            value:
              type: ":integer"
              value: "13"
          - name: "factFive"
            value:
              type: ":integer"
              value: "120"
          - name: "floorPredicate"
            value:
              type: ":nothing"
```

## Test: external extension calls

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

```ges
module AtomicCallExternal
on Start(value, heading, target) {
  let nothingValue be nothing
  let listValue be [1, 'two', #three]
  let mapValue be [alpha: 1, beta: 'two']
  let diceValue as :dice be [6, 4, 2]
  let messageValue be Ping(amount: 7, label: 'ok')
  emit Done(floored: floor value, flooredCall: floor(value), maxed: max of 2 and 8 and 5, turn: :nav.shortestTurn from: heading to: target, northPredicate: heading is :nav.isNorth, vectorTotal: :test.vectorSum :vector(1m, 2m, 3m), echoNothing: :test.echo nothingValue, echoBoolean: :test.echo true, echoInteger: :test.echo 12, echoFloat: :test.echo 12.5, echoPercentage: :test.echo 25%, echoMeter: :test.echo 10m, echoText: :test.echo 'hello', echoTag: :test.echo #ready, echoVector: :test.echo :vector(1m, 2m, 3m), echoPoint: :test.echo :point(4m, 5m, 6m), echoList: :test.echo listValue, echoMap: :test.echo mapValue, echoDice: :test.echo diceValue, echoMessage: :test.echo messageValue, echoPredicate: value is :test.echo)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "value"
          value:
            type: ":float"
            value: "10.75"
        - name: "heading"
          value:
            type: ":float"
            value: "350"
            unit: ":degree"
        - name: "target"
          value:
            type: ":float"
            value: "10"
            unit: ":degree"
    local:
      - name: "Done"
        args:
          - name: "floored"
            value:
              type: ":float"
              value: "10"
          - name: "flooredCall"
            value:
              type: ":float"
              value: "10"
          - name: "maxed"
            value:
              type: ":float"
              value: "8"
          - name: "turn"
            value:
              type: ":float"
              value: "20"
              unit: ":degree"
          - name: "northPredicate"
            value:
              type: ":boolean"
              value: true
          - name: "vectorTotal"
            value:
              type: ":float"
              value: "6"
              unit: ":meter"
          - name: "echoNothing"
            value:
              type: ":nothing"
          - name: "echoBoolean"
            value:
              type: ":boolean"
              value: true
          - name: "echoInteger"
            value:
              type: ":integer"
              value: "12"
          - name: "echoFloat"
            value:
              type: ":float"
              value: "12.5"
          - name: "echoPercentage"
            value:
              type: ":percentage"
              value: "0.25"
          - name: "echoMeter"
            value:
              type: ":integer"
              value: "10"
              unit: ":meter"
          - name: "echoText"
            value:
              type: ":text"
              value: "hello"
          - name: "echoTag"
            value:
              type: ":tag"
              value: "ready"
          - name: "echoVector"
            value:
              type: ":vector"
              x: "1"
              y: "2"
              z: "3"
              unit: ":meter"
          - name: "echoPoint"
            value:
              type: ":point"
              x: "4"
              y: "5"
              z: "6"
              unit: ":meter"
          - name: "echoList"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":text"
                  value: "two"
                - type: ":tag"
                  value: "three"
          - name: "echoMap"
            value:
              type: ":map"
              entries:
                - key: "alpha"
                  value:
                    type: ":integer"
                    value: "1"
                - key: "beta"
                  value:
                    type: ":text"
                    value: "two"
          - name: "echoDice"
            value:
              type: ":dice"
              rolls:
                - 6
                - 4
                - 2
          - name: "echoMessage"
            value:
              type: ":message"
              message:
                name: "Ping"
                args:
                  - name: "amount"
                    value:
                      type: ":integer"
                      value: "7"
                  - name: "label"
                    value:
                      type: ":text"
                      value: "ok"
          - name: "echoPredicate"
            value:
              type: ":nothing"
```
