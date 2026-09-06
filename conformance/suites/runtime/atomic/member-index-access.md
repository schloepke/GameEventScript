---
formatVersion: 1
suiteId: "runtime.atomic.member-index-access"
title: "RuntimeAtomicMemberIndexAccess"
categories: [conformance]
---

# RuntimeAtomicMemberIndexAccess

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite isolates named-member and indexed access for portable values and their out-of-range behavior.

---

## Test: nothing member and index access

This runtime case exercises “nothing member and index access” and verifies the declared messages, values, and execution result.

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
  - name: "nothing member and index access.ges"
    program: main
```

### Source code under test

```ges
module atomicnothingaccess
on Start {
  let value be nothing
  emit Done(member: value.missing, index: value[1], indexOut: value[0])
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
          - name: "member"
            value:
              type: ":Nothing"
          - name: "index"
            value:
              type: ":Nothing"
          - name: "indexOut"
            value:
              type: ":Nothing"
```

---

## Test: boolean member and index access

This runtime case exercises “boolean member and index access” and verifies the declared messages, values, and execution result.

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
  - name: "boolean member and index access.ges"
    program: main
```

### Source code under test

```ges
module atomicbooleanaccess
on Start {
  let value be true
  emit Done(member: value.missing, index: value[1], indexOut: value[0])
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
          - name: "member"
            value:
              type: ":Nothing"
          - name: "index"
            value:
              type: ":Nothing"
          - name: "indexOut"
            value:
              type: ":Nothing"
```

---

## Test: integer member and index access

This runtime case exercises “integer member and index access” and verifies the declared messages, values, and execution result.

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
  - name: "integer member and index access.ges"
    program: main
```

### Source code under test

```ges
module atomicintegeraccess
on Start {
  let value be 10
  emit Done(member: value.missing, index: value[1], indexOut: value[0])
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
          - name: "member"
            value:
              type: ":Nothing"
          - name: "index"
            value:
              type: ":Nothing"
          - name: "indexOut"
            value:
              type: ":Nothing"
```

---

## Test: float member and index access

This runtime case exercises “float member and index access” and verifies the declared messages, values, and execution result.

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
  - name: "float member and index access.ges"
    program: main
```

### Source code under test

```ges
module atomicfloataccess
on Start {
  let value be 10.5
  emit Done(member: value.missing, index: value[1], indexOut: value[0])
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
          - name: "member"
            value:
              type: ":Nothing"
          - name: "index"
            value:
              type: ":Nothing"
          - name: "indexOut"
            value:
              type: ":Nothing"
```

---

## Test: percentage member and index access

This runtime case exercises “percentage member and index access” and verifies the declared messages, values, and execution result.

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
  - name: "percentage member and index access.ges"
    program: main
```

### Source code under test

```ges
module atomicpercentageaccess
on Start {
  let value be 25%
  emit Done(member: value.missing, index: value[1], indexOut: value[0])
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
          - name: "member"
            value:
              type: ":Nothing"
          - name: "index"
            value:
              type: ":Nothing"
          - name: "indexOut"
            value:
              type: ":Nothing"
```

---

## Test: text member and index access

This runtime case exercises “text member and index access” and verifies the declared messages, values, and execution result.

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
  - name: "text member and index access.ges"
    program: main
```

### Source code under test

```ges
module atomictextaccess
on Start {
  let value be 'ab'
  emit Done(member: value.missing, first: value[1], second: value[2], indexZero: value[0], indexOut: value[3])
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
          - name: "member"
            value:
              type: ":Nothing"
          - name: "first"
            value:
              type: ":Text"
              value: "a"
          - name: "second"
            value:
              type: ":Text"
              value: "b"
          - name: "indexZero"
            value:
              type: ":Nothing"
          - name: "indexOut"
            value:
              type: ":Nothing"
```

---

## Test: tag member and index access

This runtime case exercises “tag member and index access” and verifies the declared messages, values, and execution result.

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
  - name: "tag member and index access.ges"
    program: main
```

### Source code under test

```ges
module atomictagaccess
on Start {
  let value be #ab
  emit Done(member: value.missing, first: value[1], second: value[2], indexZero: value[0], indexOut: value[3])
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
          - name: "member"
            value:
              type: ":Nothing"
          - name: "first"
            value:
              type: ":Text"
              value: "a"
          - name: "second"
            value:
              type: ":Text"
              value: "b"
          - name: "indexZero"
            value:
              type: ":Nothing"
          - name: "indexOut"
            value:
              type: ":Nothing"
```

---

## Test: vector member and index access

This runtime case exercises “vector member and index access” and verifies the declared messages, values, and execution result.

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
  - name: "vector member and index access.ges"
    program: main
```

### Source code under test

```ges
module atomicvectoraccess
on Start {
  let value be :Vector(1m, 2m, 3m)
  emit Done(memberX: value.x, memberMissing: value.missing, indexFirst: value[1], indexThird: value[3], indexZero: value[0], indexOut: value[4])
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
          - name: "memberX"
            value:
              type: ":Quantity.int64"
              unit: ":meter"
              value: "1"
          - name: "memberMissing"
            value:
              type: ":Nothing"
          - name: "indexFirst"
            value:
              type: ":Quantity.int64"
              unit: ":meter"
              value: "1"
          - name: "indexThird"
            value:
              type: ":Quantity.int64"
              unit: ":meter"
              value: "3"
          - name: "indexZero"
            value:
              type: ":Nothing"
          - name: "indexOut"
            value:
              type: ":Nothing"
```

---

## Test: point member and index access

This runtime case exercises “point member and index access” and verifies the declared messages, values, and execution result.

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
  - name: "point member and index access.ges"
    program: main
```

### Source code under test

```ges
module atomicpointaccess
on Start {
  let value be :Point(4m, 5m, 6m)
  emit Done(memberY: value.y, memberMissing: value.missing, indexFirst: value[1], indexThird: value[3], indexZero: value[0], indexOut: value[4])
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
          - name: "memberY"
            value:
              type: ":Quantity.int64"
              unit: ":meter"
              value: "5"
          - name: "memberMissing"
            value:
              type: ":Nothing"
          - name: "indexFirst"
            value:
              type: ":Quantity.int64"
              unit: ":meter"
              value: "4"
          - name: "indexThird"
            value:
              type: ":Quantity.int64"
              unit: ":meter"
              value: "6"
          - name: "indexZero"
            value:
              type: ":Nothing"
          - name: "indexOut"
            value:
              type: ":Nothing"
```

---

## Test: list member and index access

This runtime case exercises “list member and index access” and verifies the declared messages, values, and execution result.

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
  - name: "list member and index access.ges"
    program: main
```

### Source code under test

```ges
module atomiclistaccess
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
          - name: "member"
            value:
              type: ":Nothing"
          - name: "first"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "dynamicSecond"
            value:
              type: ":Number.int64"
              value: "20"
          - name: "third"
            value:
              type: ":Number.int64"
              value: "30"
          - name: "indexZero"
            value:
              type: ":Nothing"
          - name: "indexOut"
            value:
              type: ":Nothing"
```

---

## Test: map member and index access

This runtime case exercises “map member and index access” and verifies the declared messages, values, and execution result.

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
  - name: "map member and index access.ges"
    program: main
```

### Source code under test

```ges
module atomicmapaccess
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
          - name: "memberName"
            value:
              type: ":Text"
              value: "Ada"
          - name: "memberMissing"
            value:
              type: ":Nothing"
          - name: "indexText"
            value:
              type: ":Text"
              value: "Ada"
          - name: "indexTag"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "dynamicText"
            value:
              type: ":Text"
              value: "Ada"
          - name: "indexMissing"
            value:
              type: ":Nothing"
          - name: "indexOut"
            value:
              type: ":Nothing"
```

---

## Test: dice member and index access

This runtime case exercises “dice member and index access” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["6", "4", "2"]
sources:
  - name: "dice member and index access.ges"
    program: main
```

### Source code under test

```ges
module atomicdiceaccess
on Start {
  let value be roll dice 3d6
  emit Done(member: value.missing, first: value[1], third: value[3], indexZero: value[0], indexOut: value[4])
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
          - name: "member"
            value:
              type: ":Nothing"
          - name: "first"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "third"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "indexZero"
            value:
              type: ":Nothing"
          - name: "indexOut"
            value:
              type: ":Nothing"
```

---

## Test: range member and index access

This runtime case exercises “range member and index access” and verifies the declared messages, values, and execution result.

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
  - name: "range member and index access.ges"
    program: main
```

### Source code under test

```ges
module atomicrangeaccess
on Start {
  let value be (from 10 to 20 step 5) as :Range
  let descending be (from 20 to 10 step (0 - 5)) as :Range
  let fractional be (from 1.5 to 2.5 step 0.5) as :Range
  emit Done(member: value.missing, first: value[1], third: value[3], descendingSecond: descending[2], fractionalSecond: fractional[2], indexZero: value[0], indexOut: value[4])
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
          - name: "member"
            value:
              type: ":Nothing"
          - name: "first"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "third"
            value:
              type: ":Number.int64"
              value: "20"
          - name: "descendingSecond"
            value:
              type: ":Number.int64"
              value: "15"
          - name: "fractionalSecond"
            value:
              type: ":Number.binary64"
              value: "2"
          - name: "indexZero"
            value:
              type: ":Nothing"
          - name: "indexOut"
            value:
              type: ":Nothing"
```

---

## Test: message member and index access

This runtime case exercises “message member and index access” and verifies the declared messages, values, and execution result.

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
  - name: "message member and index access.ges"
    program: main
```

### Source code under test

```ges
module atomicmessageaccess
on Start {
  let value be Ping(amount: 7, target: 'orc')
  emit Done(memberName: value.name, memberArg: value.arguments.amount, memberMissing: value.missing, indexName: value[#name], indexArg: value[#arguments].target, indexMissing: value[#missing], indexOut: value[1])
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
          - name: "memberName"
            value:
              type: ":Text"
              value: "Ping"
          - name: "memberArg"
            value:
              type: ":Number.int64"
              value: "7"
          - name: "memberMissing"
            value:
              type: ":Nothing"
          - name: "indexName"
            value:
              type: ":Text"
              value: "Ping"
          - name: "indexArg"
            value:
              type: ":Text"
              value: "orc"
          - name: "indexMissing"
            value:
              type: ":Nothing"
          - name: "indexOut"
            value:
              type: ":Nothing"
```

---

## Test: handler member and index access

This runtime case exercises “handler member and index access” and verifies the declared messages, values, and execution result.

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
  - name: "handler member and index access.ges"
    program: main
```

### Source code under test

```ges
module atomichandleraccess
on Start {
  let value be Ping(amount, target)
  emit Done(memberName: value.name, memberParam: value.parameters[1], memberMissing: value.missing, indexName: value[#name], indexParam: value[#parameters][2], indexMissing: value[#missing], indexOut: value[1])
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
          - name: "memberName"
            value:
              type: ":Text"
              value: "Ping"
          - name: "memberParam"
            value:
              type: ":Text"
              value: "amount"
          - name: "memberMissing"
            value:
              type: ":Nothing"
          - name: "indexName"
            value:
              type: ":Text"
              value: "Ping"
          - name: "indexParam"
            value:
              type: ":Text"
              value: "target"
          - name: "indexMissing"
            value:
              type: ":Nothing"
          - name: "indexOut"
            value:
              type: ":Nothing"
```

---

## Test: custom record member and index access

This runtime case exercises “custom record member and index access” and verifies the declared messages, values, and execution result.

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
  - name: "custom record member and index access.ges"
    program: main
```

### Source code under test

```ges
module atomiccustomrecordaccess
record :Unit as {
  name: :Text,
  hp: :Number
}

on Start {
  let value be :Unit(name: 'Ada', hp: 10)
  emit Done(memberName: value.name, memberMissing: value.missing, indexText: value['name'], indexTag: value[#hp], indexMissing: value[#missing], indexOut: value[1], isUnit: value is :Unit)
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
          - name: "memberName"
            value:
              type: ":Text"
              value: "Ada"
          - name: "memberMissing"
            value:
              type: ":Nothing"
          - name: "indexText"
            value:
              type: ":Text"
              value: "Ada"
          - name: "indexTag"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "indexMissing"
            value:
              type: ":Nothing"
          - name: "indexOut"
            value:
              type: ":Nothing"
          - name: "isUnit"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: dynamic property access selectors

This runtime case exercises “dynamic property access selectors” and verifies the declared messages, values, and execution result.

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
  - name: "dynamic property access selectors.ges"
    program: main
```

### Source code under test

```ges
module atomicdynamicpropertyaccess
on Start {
  let listValue be [10, 20, 30]
  let mapValue be [name: 'Ada', hp: 10]
  let vectorValue be :Vector(1m, 2m, 3m)
  let pointValue be :Point(4m, 5m, 6m)
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
          - name: "listDynamic"
            value:
              type: ":Number.int64"
              value: "20"
          - name: "mapText"
            value:
              type: ":Text"
              value: "Ada"
          - name: "mapTag"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "vectorDynamic"
            value:
              type: ":Quantity.int64"
              unit: ":meter"
              value: "2"
          - name: "pointDynamic"
            value:
              type: ":Quantity.int64"
              unit: ":meter"
              value: "6"
          - name: "textDynamic"
            value:
              type: ":Text"
              value: "b"
          - name: "invalidProperty"
            value:
              type: ":Nothing"
```

---

## Test: message handler member and index access

This runtime case exercises “message handler member and index access” and verifies the declared messages, values, and execution result.

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
  - name: "message member and index access.ges"
    program: main
```

### Source code under test

```ges
module atomicmessageaccess
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

### Expectation

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
              type: ":Number.int64"
              value: "7"
      - name: "Done"
        args:
          - name: "memberName"
            value:
              type: ":Text"
              value: "Ping"
          - name: "memberTag"
            value:
              type: ":Tag"
              value: "radio"
          - name: "memberMissing"
            value:
              type: ":Nothing"
          - name: "indexName"
            value:
              type: ":Text"
              value: "Ping"
          - name: "indexTag"
            value:
              type: ":Tag"
              value: "radio"
          - name: "indexMissing"
            value:
              type: ":Nothing"
          - name: "indexOut"
            value:
              type: ":Nothing"
```
