---
formatVersion: 1
suiteId: "runtime.atomic.member-index-access"
title: "RuntimeAtomicMemberIndexAccess"
categories: [conformance]
tags: [migrated-json-v1]
---

# RuntimeAtomicMemberIndexAccess

Mechanically migrated from the former JSON conformance corpus.

## Test: nothing member and index access

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
  - name: "nothing member and index access.ges"
    program: main
```

```ges
module AtomicNothingAccess
on Start {
  let value be nothing
  emit Done(member: value.missing, index: value[1], indexOut: value[0])
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
      - name: "Done"
        args:
          - name: "member"
            value:
              type: ":nothing"
          - name: "index"
            value:
              type: ":nothing"
          - name: "indexOut"
            value:
              type: ":nothing"
```

## Test: boolean member and index access

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
  - name: "boolean member and index access.ges"
    program: main
```

```ges
module AtomicBooleanAccess
on Start {
  let value be true
  emit Done(member: value.missing, index: value[1], indexOut: value[0])
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
      - name: "Done"
        args:
          - name: "member"
            value:
              type: ":nothing"
          - name: "index"
            value:
              type: ":nothing"
          - name: "indexOut"
            value:
              type: ":nothing"
```

## Test: integer member and index access

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
  - name: "integer member and index access.ges"
    program: main
```

```ges
module AtomicIntegerAccess
on Start {
  let value be 10
  emit Done(member: value.missing, index: value[1], indexOut: value[0])
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
      - name: "Done"
        args:
          - name: "member"
            value:
              type: ":nothing"
          - name: "index"
            value:
              type: ":nothing"
          - name: "indexOut"
            value:
              type: ":nothing"
```

## Test: float member and index access

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
  - name: "float member and index access.ges"
    program: main
```

```ges
module AtomicFloatAccess
on Start {
  let value be 10.5
  emit Done(member: value.missing, index: value[1], indexOut: value[0])
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
      - name: "Done"
        args:
          - name: "member"
            value:
              type: ":nothing"
          - name: "index"
            value:
              type: ":nothing"
          - name: "indexOut"
            value:
              type: ":nothing"
```

## Test: percentage member and index access

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
  - name: "percentage member and index access.ges"
    program: main
```

```ges
module AtomicPercentageAccess
on Start {
  let value be 25%
  emit Done(member: value.missing, index: value[1], indexOut: value[0])
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
      - name: "Done"
        args:
          - name: "member"
            value:
              type: ":nothing"
          - name: "index"
            value:
              type: ":nothing"
          - name: "indexOut"
            value:
              type: ":nothing"
```

## Test: text member and index access

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
  - name: "text member and index access.ges"
    program: main
```

```ges
module AtomicTextAccess
on Start {
  let value be 'ab'
  emit Done(member: value.missing, first: value[1], second: value[2], indexZero: value[0], indexOut: value[3])
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
      - name: "Done"
        args:
          - name: "member"
            value:
              type: ":nothing"
          - name: "first"
            value:
              type: ":text"
              value: "a"
          - name: "second"
            value:
              type: ":text"
              value: "b"
          - name: "indexZero"
            value:
              type: ":nothing"
          - name: "indexOut"
            value:
              type: ":nothing"
```

## Test: tag member and index access

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
  - name: "tag member and index access.ges"
    program: main
```

```ges
module AtomicTagAccess
on Start {
  let value be #ab
  emit Done(member: value.missing, first: value[1], second: value[2], indexZero: value[0], indexOut: value[3])
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
      - name: "Done"
        args:
          - name: "member"
            value:
              type: ":nothing"
          - name: "first"
            value:
              type: ":text"
              value: "a"
          - name: "second"
            value:
              type: ":text"
              value: "b"
          - name: "indexZero"
            value:
              type: ":nothing"
          - name: "indexOut"
            value:
              type: ":nothing"
```

## Test: vector member and index access

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
  - name: "vector member and index access.ges"
    program: main
```

```ges
module AtomicVectorAccess
on Start {
  let value be :vector(1m, 2m, 3m)
  emit Done(memberX: value.x, memberMissing: value.missing, indexFirst: value[1], indexThird: value[3], indexZero: value[0], indexOut: value[4])
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
      - name: "Done"
        args:
          - name: "memberX"
            value:
              type: ":integer"
              unit: ":meter"
              value: "1"
          - name: "memberMissing"
            value:
              type: ":nothing"
          - name: "indexFirst"
            value:
              type: ":integer"
              unit: ":meter"
              value: "1"
          - name: "indexThird"
            value:
              type: ":integer"
              unit: ":meter"
              value: "3"
          - name: "indexZero"
            value:
              type: ":nothing"
          - name: "indexOut"
            value:
              type: ":nothing"
```

## Test: point member and index access

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
  - name: "point member and index access.ges"
    program: main
```

```ges
module AtomicPointAccess
on Start {
  let value be :point(4m, 5m, 6m)
  emit Done(memberY: value.y, memberMissing: value.missing, indexFirst: value[1], indexThird: value[3], indexZero: value[0], indexOut: value[4])
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
      - name: "Done"
        args:
          - name: "memberY"
            value:
              type: ":integer"
              unit: ":meter"
              value: "5"
          - name: "memberMissing"
            value:
              type: ":nothing"
          - name: "indexFirst"
            value:
              type: ":integer"
              unit: ":meter"
              value: "4"
          - name: "indexThird"
            value:
              type: ":integer"
              unit: ":meter"
              value: "6"
          - name: "indexZero"
            value:
              type: ":nothing"
          - name: "indexOut"
            value:
              type: ":nothing"
```

## Test: list member and index access

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
  - name: "list member and index access.ges"
    program: main
```

```ges
module AtomicListAccess
on Start {
  let value be [10, 20, 30]
  let index be 2
  emit Done(member: value.missing, first: value[1], dynamicSecond: value[index], third: value[3], indexZero: value[0], indexOut: value[4])
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
      - name: "Done"
        args:
          - name: "member"
            value:
              type: ":nothing"
          - name: "first"
            value:
              type: ":integer"
              value: "10"
          - name: "dynamicSecond"
            value:
              type: ":integer"
              value: "20"
          - name: "third"
            value:
              type: ":integer"
              value: "30"
          - name: "indexZero"
            value:
              type: ":nothing"
          - name: "indexOut"
            value:
              type: ":nothing"
```

## Test: map member and index access

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
  - name: "map member and index access.ges"
    program: main
```

```ges
module AtomicMapAccess
on Start {
  let value be [name: 'Ada', hp: 10]
  let key be 'name'
  emit Done(memberName: value.name, memberMissing: value.missing, indexText: value['name'], indexTag: value[#hp], dynamicText: value[key], indexMissing: value[#missing], indexOut: value[1])
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
      - name: "Done"
        args:
          - name: "memberName"
            value:
              type: ":text"
              value: "Ada"
          - name: "memberMissing"
            value:
              type: ":nothing"
          - name: "indexText"
            value:
              type: ":text"
              value: "Ada"
          - name: "indexTag"
            value:
              type: ":integer"
              value: "10"
          - name: "dynamicText"
            value:
              type: ":text"
              value: "Ada"
          - name: "indexMissing"
            value:
              type: ":nothing"
          - name: "indexOut"
            value:
              type: ":nothing"
```

## Test: dice member and index access

```yaml
gesBlock: case
id: case-0012
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["6", "4", "2"]
sources:
  - name: "dice member and index access.ges"
    program: main
```

```ges
module AtomicDiceAccess
on Start {
  let value be roll dice 3d6
  emit Done(member: value.missing, first: value[1], third: value[3], indexZero: value[0], indexOut: value[4])
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
      - name: "Done"
        args:
          - name: "member"
            value:
              type: ":nothing"
          - name: "first"
            value:
              type: ":integer"
              value: "6"
          - name: "third"
            value:
              type: ":integer"
              value: "2"
          - name: "indexZero"
            value:
              type: ":nothing"
          - name: "indexOut"
            value:
              type: ":nothing"
```

## Test: range member and index access

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
  - name: "range member and index access.ges"
    program: main
```

```ges
module AtomicRangeAccess
on Start {
  let value as :range be from 10 to 20 step 5
  let descending as :range be from 20 to 10 step (0 - 5)
  let fractional as :range be from 1.5 to 2.5 step 0.5
  emit Done(member: value.missing, first: value[1], third: value[3], descendingSecond: descending[2], fractionalSecond: fractional[2], indexZero: value[0], indexOut: value[4])
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
      - name: "Done"
        args:
          - name: "member"
            value:
              type: ":nothing"
          - name: "first"
            value:
              type: ":integer"
              value: "10"
          - name: "third"
            value:
              type: ":integer"
              value: "20"
          - name: "descendingSecond"
            value:
              type: ":integer"
              value: "15"
          - name: "fractionalSecond"
            value:
              type: ":float"
              value: "2"
          - name: "indexZero"
            value:
              type: ":nothing"
          - name: "indexOut"
            value:
              type: ":nothing"
```

## Test: message member and index access

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
  - name: "message member and index access.ges"
    program: main
```

```ges
module AtomicMessageAccess
on Start {
  let value be Ping(amount: 7, target: 'orc')
  emit Done(memberName: value.name, memberArg: value.arguments.amount, memberMissing: value.missing, indexName: value[#name], indexArg: value[#arguments].target, indexMissing: value[#missing], indexOut: value[1])
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
      - name: "Done"
        args:
          - name: "memberName"
            value:
              type: ":text"
              value: "Ping"
          - name: "memberArg"
            value:
              type: ":integer"
              value: "7"
          - name: "memberMissing"
            value:
              type: ":nothing"
          - name: "indexName"
            value:
              type: ":text"
              value: "Ping"
          - name: "indexArg"
            value:
              type: ":text"
              value: "orc"
          - name: "indexMissing"
            value:
              type: ":nothing"
          - name: "indexOut"
            value:
              type: ":nothing"
```

## Test: handler member and index access

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
  - name: "handler member and index access.ges"
    program: main
```

```ges
module AtomicHandlerAccess
on Start {
  let value be Ping(amount, target)
  emit Done(memberName: value.name, memberParam: value.parameters[1], memberMissing: value.missing, indexName: value[#name], indexParam: value[#parameters][2], indexMissing: value[#missing], indexOut: value[1])
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
      - name: "Done"
        args:
          - name: "memberName"
            value:
              type: ":text"
              value: "Ping"
          - name: "memberParam"
            value:
              type: ":text"
              value: "amount"
          - name: "memberMissing"
            value:
              type: ":nothing"
          - name: "indexName"
            value:
              type: ":text"
              value: "Ping"
          - name: "indexParam"
            value:
              type: ":text"
              value: "target"
          - name: "indexMissing"
            value:
              type: ":nothing"
          - name: "indexOut"
            value:
              type: ":nothing"
```

## Test: custom record member and index access

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
  - name: "custom record member and index access.ges"
    program: main
```

```ges
module AtomicCustomRecordAccess
record :unit as {
  name: :text,
  hp: :number
}

on Start {
  let value be :unit(name: 'Ada', hp: 10)
  emit Done(memberName: value.name, memberMissing: value.missing, indexText: value['name'], indexTag: value[#hp], indexMissing: value[#missing], indexOut: value[1], isUnit: value is :unit)
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
      - name: "Done"
        args:
          - name: "memberName"
            value:
              type: ":text"
              value: "Ada"
          - name: "memberMissing"
            value:
              type: ":nothing"
          - name: "indexText"
            value:
              type: ":text"
              value: "Ada"
          - name: "indexTag"
            value:
              type: ":integer"
              value: "10"
          - name: "indexMissing"
            value:
              type: ":nothing"
          - name: "indexOut"
            value:
              type: ":nothing"
          - name: "isUnit"
            value:
              type: ":boolean"
              value: true
```

## Test: dynamic property access selectors

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
  - name: "dynamic property access selectors.ges"
    program: main
```

```ges
module AtomicDynamicPropertyAccess
on Start {
  let listValue be [10, 20, 30]
  let mapValue be [name: 'Ada', hp: 10]
  let vectorValue be :vector(1m, 2m, 3m)
  let pointValue be :point(4m, 5m, 6m)
  let textValue be 'ab'
  let indexKey be 2
  let textKey be 'name'
  let tagKey be #hp
  let vectorKey be 'y'
  let pointKey be #z
  let invalidKey be true
  emit Done(listDynamic: listValue[indexKey], mapText: mapValue[textKey], mapTag: mapValue[tagKey], vectorDynamic: vectorValue[vectorKey], pointDynamic: pointValue[pointKey], textDynamic: textValue[indexKey], invalidProperty: mapValue[invalidKey])
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
      - name: "Done"
        args:
          - name: "listDynamic"
            value:
              type: ":integer"
              value: "20"
          - name: "mapText"
            value:
              type: ":text"
              value: "Ada"
          - name: "mapTag"
            value:
              type: ":integer"
              value: "10"
          - name: "vectorDynamic"
            value:
              type: ":integer"
              unit: ":meter"
              value: "2"
          - name: "pointDynamic"
            value:
              type: ":integer"
              unit: ":meter"
              value: "6"
          - name: "textDynamic"
            value:
              type: ":text"
              value: "b"
          - name: "invalidProperty"
            value:
              type: ":nothing"
```

## Test: message handler member and index access

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
  - name: "message member and index access.ges"
    program: main
```

```ges
module AtomicMessageAccess
on Start {
  emit Ping(amount: 7) with #radio
}

on Ping as message {
  emit Done(memberName: message.name, memberTag: message.tags[1], memberMissing: message.missing, indexName: message[#name], indexTag: message['tags'][1], indexMissing: message[#missing], indexOut: message[1])
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
      - name: "Ping"
        tags:
          - "radio"
        args:
          - name: "amount"
            value:
              type: ":integer"
              value: "7"
      - name: "Done"
        args:
          - name: "memberName"
            value:
              type: ":text"
              value: "Ping"
          - name: "memberTag"
            value:
              type: ":tag"
              value: "radio"
          - name: "memberMissing"
            value:
              type: ":nothing"
          - name: "indexName"
            value:
              type: ":text"
              value: "Ping"
          - name: "indexTag"
            value:
              type: ":tag"
              value: "radio"
          - name: "indexMissing"
            value:
              type: ":nothing"
          - name: "indexOut"
            value:
              type: ":nothing"
```
