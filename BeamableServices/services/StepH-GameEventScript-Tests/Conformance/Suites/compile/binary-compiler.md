---
formatVersion: 1
suiteId: "compile.binary-compiler"
title: "CompileBinaryCompiler"
categories: [conformance]
---

# CompileBinaryCompiler

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies compiler output that is persisted in portable programs and remains observable after loading and execution.

---

## Test: handler and program resource requirements include nested calls

This compiler case exercises “handler and program resource requirements include nested calls” and verifies the declared portable output.

### Case description

```yaml
gesBlock: case
id: case-0001
kind: compileMetadata
level: scenario
sources:
  - name: "handler and program resource requirements include nested calls.ges"
    program: main
```

### Source code under test

```ges
module binarycompilerresources

function leaf(value) be value + 1

function middle(value) be leaf(value: value)

on Start(value) {
  let result be middle(value: value)
  emit Done(result: result)
}

on Ping(value) {
  emit Pong(value: value)
}

```

### Expectation

```yaml
gesBlock: expect
metadata:
  programResources:
    requiredRegisterCount: 7
    requiredCallStackDepth: 2
  handlerResources:
    - name: "Start"
      requiredRegisterCount: 7
      requiredCallStackDepth: 2
    - name: "Ping"
      requiredRegisterCount: 1
      requiredCallStackDepth: 0
```

---

## Test: host rejects program whose call depth exceeds its limit

This negative loading case exercises “host rejects program whose call depth exceeds its limit” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0002
kind: loadError
level: scenario
runtimeLimits:
  maxCallDepth: 1
sources:
  - name: "host rejects program whose call depth exceeds its limit.ges"
    program: main
```

### Source code under test

```ges
module binarycompilercalldepthlimit

function leaf(value) be value + 1

function middle(value) be leaf(value: value)

on Start(value) {
  let result be middle(value: value)
  emit Done(result: result)
}

```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "link"
  code: "link.requiredCallStackDepthExceeded"
```

---

## Test: host rejects program whose register requirement exceeds its limit

This negative loading case exercises “host rejects program whose register requirement exceeds its limit” and verifies the required portable diagnostic.

### Case description

```yaml
gesBlock: case
id: case-0003
kind: loadError
level: scenario
runtimeLimits:
  maxRegisterValues: 6
sources:
  - name: "host rejects program whose register requirement exceeds its limit.ges"
    program: main
```

### Source code under test

```ges
module binarycompilerregisterlimit

function leaf(value) be value + 1

function middle(value) be leaf(value: value)

on Start(value) {
  let result be middle(value: value)
  emit Done(result: result)
}

```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "link"
  code: "link.requiredRegisterCountExceeded"
```

---

## Test: simple handler compiles to runnable binary

This runtime case exercises “simple handler compiles to runnable binary” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0004
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "simple handler compiles to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompilersmoke

on Start(value) {
  let doubled be value * 2
  emit Done(result: doubled, text: 'ok')
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
            value: "6"
    local:
      - name: "Done"
        args:
          - name: "result"
            value:
              type: ":Number.int64"
              value: "12"
          - name: "text"
            value:
              type: ":Text"
              value: "ok"
```

---

## Test: function call and branch compile to runnable binary

This runtime case exercises “function call and branch compile to runnable binary” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0005
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "function call and branch compile to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompilerfunction

function double(value) be value * 2

on Start(value) {
  let result be double(value: value)
  if result > 10 {
    emit Done(result: result, status: #high)
  } else {
    emit Done(result: result, status: #low)
  }
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
            value: "7"
    local:
      - name: "Done"
        args:
          - name: "result"
            value:
              type: ":Number.int64"
              value: "14"
          - name: "status"
            value:
              type: ":Tag"
              value: "high"
```

---

## Test: record constructor compiles to runnable binary

This runtime case exercises “record constructor compiles to runnable binary” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0006
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "record constructor compiles to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompilerrecord

record :Gauge as {
  current: :Number clamped between 0 and maximum,
  maximum: :Number clamped between 0 and infinity,
  percentage: :Percentage computed by
    0% when maximum <= 0,
    otherwise (current / maximum) as :Percentage
}

on Start {
  let hp be :Gauge(current: 125, maximum: 100)
  emit Done(current: hp.current, maximum: hp.maximum, percentage: hp.percentage, isGauge: hp is :Gauge)
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
          - name: "current"
            value:
              type: ":Number.int64"
              value: "100"
          - name: "maximum"
            value:
              type: ":Number.int64"
              value: "100"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0.01"
          - name: "isGauge"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: external type constructor compiles to runnable binary

This runtime case exercises “external type constructor compiles to runnable binary” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0007
kind: scriptApi
level: scenario
requires:
  core: [external-types]
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "external type constructor compiles to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompilerexternaltype

on Start {
  let aim be :Aim(range: 12m, bearing: 90°, steps: 4m, direction: :Vector(1m, 2m, 3m))
  emit Done(isAim: aim is :Aim, bearing: aim.bearing, range: aim.range, steps: aim.steps, directionZ: aim.direction.z, checksum: aim.checksum)
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
          - name: "isAim"
            value:
              type: ":Boolean"
              value: true
          - name: "bearing"
            value:
              type: ":Quantity.int64"
              value: "90"
              unit: ":degree"
          - name: "range"
            value:
              type: ":Quantity.int64"
              value: "12"
              unit: ":meter"
          - name: "steps"
            value:
              type: ":Quantity.int64"
              value: "4"
              unit: ":meter"
          - name: "directionZ"
            value:
              type: ":Quantity.int64"
              value: "3"
              unit: ":meter"
          - name: "checksum"
            value:
              type: ":Number.int64"
              value: "106"
```

---

## Test: standard and external calls compile to runnable binary

This runtime case exercises “standard and external calls compile to runnable binary” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0008
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "standard and external calls compile to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompilerextensioncalls

on Start {
  let direction be :Vector(1m, 2m, 3m)
  let floored be floor 10.75
  let vectorTotal be :test.vectorSum direction
  let turn be :nav.shortestTurn from: 350° to: 10°
  let normalizedPredicate be 0° is :nav.isNorth
  emit Done(floored: floored, vectorTotal: vectorTotal, turn: turn, normalizedPredicate: normalizedPredicate)
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
          - name: "floored"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "vectorTotal"
            value:
              type: ":Quantity.int64"
              value: "6"
              unit: ":meter"
          - name: "turn"
            value:
              type: ":Quantity.int64"
              value: "20"
              unit: ":degree"
          - name: "normalizedPredicate"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: handler literal binding compiles to runnable binary

This runtime case exercises “handler literal binding compiles to runnable binary” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0009
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "handler literal binding compiles to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompilerhandlerbinding

on Start(unit, target, hp) {
  let shoot be Shoot(unit, target)
  let msg be shoot(unit: unit, target: target)
  let invalid be shoot(unit: unit, hp: hp)
  emit msg
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
        - name: "target"
          value:
            type: ":Text"
            value: "t1"
        - name: "hp"
          value:
            type: ":Number.int64"
            value: "10"
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
      - name: "Done"
        args: []
```

---

## Test: message name handler and tags compile to runnable binary

This runtime case exercises “message name handler and tags compile to runnable binary” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0010
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "message name handler and tags compile to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompilermessagenamehandler

on Start {
  emit Ping(amount: 7, kind: #fire) with #radio, #urgent
}

on Ping as message {
  emit Done(name: message.name, signature: message.signature, amount: message.arguments.amount, kind: message.arguments.kind, tagCount: message.tags[:count], firstTag: message.tags[1], secondTag: message.tags[2], isMessage: message is :Message)
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
        args:
          - name: "amount"
            value:
              type: ":Number.int64"
              value: "7"
          - name: "kind"
            value:
              type: ":Tag"
              value: "fire"
        tags:
          - "radio"
          - "urgent"
      - name: "Done"
        args:
          - name: "name"
            value:
              type: ":Text"
              value: "Ping"
          - name: "signature"
            value:
              type: ":Text"
              value: "Ping(amount,kind)"
          - name: "amount"
            value:
              type: ":Number.int64"
              value: "7"
          - name: "kind"
            value:
              type: ":Tag"
              value: "fire"
          - name: "tagCount"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "firstTag"
            value:
              type: ":Tag"
              value: "radio"
          - name: "secondTag"
            value:
              type: ":Tag"
              value: "urgent"
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: publish and message value tags compile to runnable binary

This runtime case exercises “publish and message value tags compile to runnable binary” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0011
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "publish and message value tags compile to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompilerpublish

on Start {
  let dynamicTags be [#radio, #relay]
  let relay be Relay(amount: 5)
  emit relay with dynamicTags
  publish Remote(amount: 6) with #network
  publish relay with #copy
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
      - name: "Relay"
        args:
          - name: "amount"
            value:
              type: ":Number.int64"
              value: "5"
        tags:
          - "radio"
          - "relay"
      - name: "Remote"
        args:
          - name: "amount"
            value:
              type: ":Number.int64"
              value: "6"
        tags:
          - "network"
      - name: "Relay"
        args:
          - name: "amount"
            value:
              type: ":Number.int64"
              value: "5"
        tags:
          - "copy"
    outbound:
      - name: "Remote"
        args:
          - name: "amount"
            value:
              type: ":Number.int64"
              value: "6"
        tags:
          - "network"
      - name: "Relay"
        args:
          - name: "amount"
            value:
              type: ":Number.int64"
              value: "5"
        tags:
          - "copy"
```

---

## Test: for loops compile to runnable binary

This runtime case exercises “for loops compile to runnable binary” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0012
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "for loops compile to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompilerfor

on Start {
  for item in [1, 2] {
    emit Item(kind: 'list', value: item)
  }

  for item from 3 to 5 step 2 {
    emit Item(kind: 'range', value: item)
  }

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
      args: []
    local:
      - name: "Item"
        args:
          - name: "kind"
            value:
              type: ":Text"
              value: "list"
          - name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      - name: "Item"
        args:
          - name: "kind"
            value:
              type: ":Text"
              value: "list"
          - name: "value"
            value:
              type: ":Number.int64"
              value: "2"
      - name: "Item"
        args:
          - name: "kind"
            value:
              type: ":Text"
              value: "range"
          - name: "value"
            value:
              type: ":Number.int64"
              value: "3"
      - name: "Item"
        args:
          - name: "kind"
            value:
              type: ":Text"
              value: "range"
          - name: "value"
            value:
              type: ":Number.int64"
              value: "5"
      - name: "Done"
        args: []
```

---

## Test: generated lists compile to runnable binary

This runtime case exercises “generated lists compile to runnable binary” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0013
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "generated lists compile to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompilergeneratedlist

on Start {
  let values be :List[:select item from 1 to 5 where item mod 2 = 1 => item * 10]
  emit Done(count: values[:count], first: values[1], second: values[2], third: values[3])
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
          - name: "count"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "first"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "second"
            value:
              type: ":Number.int64"
              value: "30"
          - name: "third"
            value:
              type: ":Number.int64"
              value: "50"
```

---

## Test: guarded choices compile to runnable binary

This runtime case exercises “guarded choices compile to runnable binary” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0014
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "guarded choices compile to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompilerguardedchoice

on Start(value) {
  let result be #large when value > 10, #medium when value > 5 otherwise #small
  emit Done(result: result)
}

```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |
| step-0002 | Start | completion |  |
| step-0003 | Start | completion |  |

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
            value: "12"
    local:
      - name: "Done"
        args:
          - name: "result"
            value:
              type: ":Tag"
              value: "large"
  step-0002:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.int64"
            value: "7"
    local:
      - name: "Done"
        args:
          - name: "result"
            value:
              type: ":Tag"
              value: "medium"
  step-0003:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.int64"
            value: "3"
    local:
      - name: "Done"
        args:
          - name: "result"
            value:
              type: ":Tag"
              value: "small"
```

---

## Test: direct draw and choose selectors compile to runnable binary

This runtime case exercises “direct draw and choose selectors compile to runnable binary” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0015
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "direct draw and choose selectors compile to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompilerdirectchoose

on Start {
  let values be [1, 2, 3]
  let oneValue be values[:draw 1]
  let drawnValues be values[:draw 2]
  let chosenOne be values[:choose 1]
  let chosenValues be values[:choose 2]
  let randomOne be [42][:choose 1 at random]
  let randomValues be [7, 8][:choose 2 at random]
  emit Done(oneValue: oneValue, drawnLen: drawnValues[:count], drawnSecond: drawnValues[2], chosenOne: chosenOne, chosenLen: chosenValues[:count], randomOne: randomOne, randomLen: randomValues[:count])
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
          - name: "oneValue"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "drawnLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "drawnSecond"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "chosenOne"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "chosenLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "randomOne"
            value:
              type: ":Number.int64"
              value: "42"
          - name: "randomLen"
            value:
              type: ":Number.int64"
              value: "2"
```

---

## Test: direct pattern selectors compile to runnable binary

This runtime case exercises “direct pattern selectors compile to runnable binary” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0016
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "direct pattern selectors compile to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompilerdirectpatterns

on Start {
  let dice be ([6, 6, 5, 5, 5]) as :Dice
  let straight be ([6, 5, 4, 3, 2]) as :Dice
  let list be ['a', 'b', 'a']
  let dicePair be dice[:take pair]
  let diceFull be dice[:take full house]
  let straightTaken be straight[:take straight]
  let listPair be list[:take pair]
  let tags be [#fire, #ice, #fire]
  let dicePairOfSix be dice[:take pair of 6]
  let listPairOfA be list[:take pair of 'a']
  let tagPairOfFire be tags[:take pair of #fire]
  emit Done(hasDicePair: dice[:has pair], hasListPair: list[:has pair], hasPairOfSix: dice[:has pair of 6], hasPairOfFour: dice[:has pair of 4], pairLen: dicePair[:count], fullLen: diceFull[:count], straightLen: straightTaken[:count], listPairLen: listPair[:count], pairOfSixLen: dicePairOfSix[:count], pairOfAFirst: listPairOfA[1], tagPairSecond: tagPairOfFire[2])
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
          - name: "hasDicePair"
            value:
              type: ":Boolean"
              value: true
          - name: "hasListPair"
            value:
              type: ":Boolean"
              value: true
          - name: "hasPairOfSix"
            value:
              type: ":Boolean"
              value: true
          - name: "hasPairOfFour"
            value:
              type: ":Boolean"
              value: false
          - name: "pairLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "fullLen"
            value:
              type: ":Number.int64"
              value: "5"
          - name: "straightLen"
            value:
              type: ":Number.int64"
              value: "5"
          - name: "listPairLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "pairOfSixLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "pairOfAFirst"
            value:
              type: ":Text"
              value: "a"
          - name: "tagPairSecond"
            value:
              type: ":Tag"
              value: "fire"
```

---

## Test: direct sort group distinct selectors compile to runnable binary

This runtime case exercises “direct sort group distinct selectors compile to runnable binary” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0017
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "direct sort group distinct selectors compile to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompilerdirectsortgroupdistinct

on Start {
  let units be [[name: 'Knight', faction: #melee, hp: 10], [name: 'Rook', faction: #melee, hp: 8], [name: 'Archer', faction: #ranged, hp: 6]]
  let firstFaction be units[1].faction
  let thirdFaction be units[3].faction
  let distinctFactions be units[:distinct by unit => unit.faction]
  let groups be units[:group by unit => unit.faction]
  let orderedAscending be units[:order by unit => unit.hp ascending]
  let orderedDescending be units[:order by unit => unit.hp descending]
  emit Done(firstFaction: firstFaction, thirdFaction: thirdFaction, distinctLen: distinctFactions[:count], distinctFirst: distinctFactions[1].name, distinctSecond: distinctFactions[2].name, meleeLen: groups[#melee][:count], rangedFirst: groups[#ranged][1].name, ascFirst: orderedAscending[1].name, descFirst: orderedDescending[1].name)
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
          - name: "firstFaction"
            value:
              type: ":Tag"
              value: "melee"
          - name: "thirdFaction"
            value:
              type: ":Tag"
              value: "ranged"
          - name: "distinctLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "distinctFirst"
            value:
              type: ":Text"
              value: "Knight"
          - name: "distinctSecond"
            value:
              type: ":Text"
              value: "Archer"
          - name: "meleeLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "rangedFirst"
            value:
              type: ":Text"
              value: "Archer"
          - name: "ascFirst"
            value:
              type: ":Text"
              value: "Archer"
          - name: "descFirst"
            value:
              type: ":Text"
              value: "Knight"
```

---

## Test: filter and select selectors compile to runnable binary

This runtime case exercises “filter and select selectors compile to runnable binary” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0018
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "filter and select selectors compile to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompilerfilterselect

on Start {
  let values be [1, 2, 3, 4]
  let threshold be 2
  let base be 100
  let filtered be values[:filter value where value > 2]
  let selected be values[:select value => value * 10]
  let capturedFilter be values[:filter value where value > threshold]
  let capturedSelect be values[:select value => value + base]
  let chained be values[:filter value where value > 1][:select value => value + 5]
  emit Done(filteredLen: filtered[:count], filteredFirst: filtered[1], selectedSecond: selected[2], capturedFilterFirst: capturedFilter[1], capturedSelectThird: capturedSelect[3], chainedLen: chained[:count], chainedFirst: chained[1], chainedThird: chained[3])
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
          - name: "filteredLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "filteredFirst"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "selectedSecond"
            value:
              type: ":Number.int64"
              value: "20"
          - name: "capturedFilterFirst"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "capturedSelectThird"
            value:
              type: ":Number.int64"
              value: "103"
          - name: "chainedLen"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "chainedFirst"
            value:
              type: ":Number.int64"
              value: "7"
          - name: "chainedThird"
            value:
              type: ":Number.int64"
              value: "9"
```

---

## Test: iterator terminal selectors compile to runnable binary

This runtime case exercises “iterator terminal selectors compile to runnable binary” and verifies the declared messages, values, and execution result.

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
  - name: "iterator terminal selectors compile to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompileriteratorterminals

on Start {
  let values be [1, 2, 3, 4]
  let threshold be 2
  let offset be 10
  let units be [[name: 'Knight', hp: 10], [name: 'Mage', hp: 6], [name: 'Guard', hp: 8]]
  let counted be values[:count value where value > threshold]
  let summed be values[:sum value => value + offset]
  let averaged be values[:average value => value]
  let chainedSum be values[:filter value where value > 1][:sum value => value]
  let weakest be units[:min unit => unit.hp]
  let strongest be units[:max unit => unit.hp + offset]
  emit Done(counted: counted, summed: summed, averaged: averaged, chainedSum: chainedSum, weakest: weakest.name, strongest: strongest.name)
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
          - name: "counted"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "summed"
            value:
              type: ":Number.int64"
              value: "50"
          - name: "averaged"
            value:
              type: ":Number.binary64"
              value: "2.5"
          - name: "chainedSum"
            value:
              type: ":Number.int64"
              value: "9"
          - name: "weakest"
            value:
              type: ":Text"
              value: "Mage"
          - name: "strongest"
            value:
              type: ":Text"
              value: "Knight"
```

---

## Test: predicate and filtered edge selectors compile to runnable binary

This runtime case exercises “predicate and filtered edge selectors compile to runnable binary” and verifies the declared messages, values, and execution result.

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
  - name: "predicate and filtered edge selectors compile to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompilerpredicateedges

on Start {
  let values be [1, 2, 3, 4]
  let threshold be 2
  let units be [[name: 'Knight', alive: true, role: #fighter], [name: 'Mage', alive: false, role: #boss], [name: 'Guard', alive: true, role: #fighter]]
  let hasHigh be values[:any value where value > threshold]
  let allPositive be values[:all value where value > 0]
  let firstAlive be units[:first unit where unit.alive]
  let lastAlive be units[:last unit where unit.alive]
  let boss be units[:single unit where unit.role = #boss]
  emit Done(hasHigh: hasHigh, allPositive: allPositive, firstAlive: firstAlive.name, lastAlive: lastAlive.name, boss: boss.name)
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
          - name: "hasHigh"
            value:
              type: ":Boolean"
              value: true
          - name: "allPositive"
            value:
              type: ":Boolean"
              value: true
          - name: "firstAlive"
            value:
              type: ":Text"
              value: "Knight"
          - name: "lastAlive"
            value:
              type: ":Text"
              value: "Guard"
          - name: "boss"
            value:
              type: ":Text"
              value: "Mage"
```

---

## Test: object match selector compiles to runnable binary

This runtime case exercises “object match selector compiles to runnable binary” and verifies the declared messages, values, and execution result.

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
  - name: "object match selector compiles to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompilerobjectmatch

on Start {
  let targetRole be #boss
  let units be [[name: 'Knight', role: #fighter, stats: [hp: 10]], [name: 'Mage', role: #boss, stats: [hp: 6]], [name: 'Guard', role: #fighter, stats: [hp: 8]]]
  let hasBoss be units[:has [role: targetRole]]
  let hasNestedHp be units[:has [stats: [hp: 8]]]
  let hasMissing be units[:has [role: #healer]]
  emit Done(hasBoss: hasBoss, hasNestedHp: hasNestedHp, hasMissing: hasMissing)
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
          - name: "hasBoss"
            value:
              type: ":Boolean"
              value: true
          - name: "hasNestedHp"
            value:
              type: ":Boolean"
              value: true
          - name: "hasMissing"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: map selector compiles to runnable binary

This runtime case exercises “map selector compiles to runnable binary” and verifies the declared messages, values, and execution result.

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
  - name: "map selector compiles to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompilermapselector

on Start {
  let units be [[id: #rook, hp: 10, team: #blue], [id: #mage, hp: 6, team: #red], [id: #guard, hp: 8, team: #blue]]
  let byId be units[:map unit by unit.id]
  let hpById be units[:map unit by unit.id => unit.hp]
  let blueById be units[:filter unit where unit.team = #blue][:map unit by unit.id => unit.hp]
  emit Done(byIdLen: byId[:count], rookTeam: byId[#rook].team, mageHp: byId[#mage].hp, hpRook: hpById[#rook], hpMage: hpById[#mage], blueLen: blueById[:count], blueGuard: blueById[#guard])
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
          - name: "byIdLen"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "rookTeam"
            value:
              type: ":Tag"
              value: "blue"
          - name: "mageHp"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "hpRook"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "hpMage"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "blueLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "blueGuard"
            value:
              type: ":Number.int64"
              value: "8"
```

---

## Test: filtered and weighted choose selectors compile to runnable binary

This runtime case exercises “filtered and weighted choose selectors compile to runnable binary” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0023
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "filtered and weighted choose selectors compile to runnable binary.ges"
    program: main
```

### Source code under test

```ges
module binarycompilerchooseselectors

on Start {
  let scale be 2
  let units be [[name: 'Low', weight: 0, team: #red], [name: 'High', weight: 3, team: #blue], [name: 'Zero', weight: 0, team: #blue]]
  let chosenBlue be units[:choose 1 unit where unit.team = #blue]
  let chosenRandomBlue be units[:choose 1 at random unit where unit.weight > 0]
  let weightedOne be units[:choose 1 weighted by unit => unit.weight * scale]
  let weightedMany be units[:choose 2 weighted by unit => unit.weight * scale]
  let weightedFiltered be units[:choose 2 unit where unit.team = #blue weighted by unit => unit.weight * scale]
  emit Done(chosenBlue: chosenBlue.name, chosenRandomBlue: chosenRandomBlue.name, weightedOne: weightedOne.name, weightedManyLen: weightedMany[:count], weightedManyFirst: weightedMany[1].name, weightedFilteredLen: weightedFiltered[:count], weightedFilteredFirst: weightedFiltered[1].name)
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
          - name: "chosenBlue"
            value:
              type: ":Text"
              value: "High"
          - name: "chosenRandomBlue"
            value:
              type: ":Text"
              value: "High"
          - name: "weightedOne"
            value:
              type: ":Text"
              value: "High"
          - name: "weightedManyLen"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "weightedManyFirst"
            value:
              type: ":Text"
              value: "High"
          - name: "weightedFilteredLen"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "weightedFilteredFirst"
            value:
              type: ":Text"
              value: "High"
```
