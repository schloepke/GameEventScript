---
formatVersion: 1
suiteId: "runtime.atomic.random"
title: "RuntimeAtomicRandom"
categories: [conformance]
tags: [migrated-json-v1]
---

# RuntimeAtomicRandom

Mechanically migrated from the former JSON conformance corpus.

## Test: random take integer and float opcode paths

```yaml
gesBlock: case
id: case-0001
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["4", "0.25", "1.5"]
sources:
  - name: "random take integer and float opcode paths.ges"
    program: main
```

```ges
module AtomicRandomTakeOpcodes
on Start {
  let integerValue be random from 1 to 6
  let floatValue be random from 0.0 to 1.0
  let mixedValue be random from 1 to 2.0
  let nothingValue be random from nothing to 6
  emit Done(integerValue: integerValue, floatValue: floatValue, mixedValue: mixedValue, nothingValue: nothingValue)
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
          - name: "integerValue"
            value:
              type: ":integer"
              value: "4"
          - name: "floatValue"
            value:
              type: ":float"
              value: "0.25"
          - name: "mixedValue"
            value:
              type: ":float"
              value: "1.5"
          - name: "nothingValue"
            value:
              type: ":nothing"
```

## Test: seeded random uses full int64 seed

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
  - name: "seeded random uses full int64 seed.ges"
    program: main
```

```ges
module AtomicSeededRandomLong
on Start {
  let value be random with 4294967297 (random from 1 to 1000000000000)
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
      args: []
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "820299227869"
```

## Test: seeded random matches portable known answer vectors

```yaml
gesBlock: case
id: case-0003
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: exact
sources:
  - name: "seeded random matches portable known answer vectors.ges"
    program: main
```

```ges
module AtomicSeededRandomKnownAnswers
on Start {
  random with 0 {
    emit Integers(a: random from -100 to 100, b: random from -100 to 100, c: random from -100 to 100, d: random from -100 to 100, e: random from -100 to 100, f: random from -100 to 100, g: random from -100 to 100, h: random from -100 to 100)
  }
  random with 0 {
    emit Floats(a: random from 0.0 to 1.0, b: random from 0.0 to 1.0, c: random from 0.0 to 1.0, d: random from 0.0 to 1.0, e: random from 0.0 to 1.0)
  }
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
      - name: "Integers"
        args:
          - name: "a"
            value:
              type: ":integer"
              value: "16"
          - name: "b"
            value:
              type: ":integer"
              value: "-95"
          - name: "c"
            value:
              type: ":integer"
              value: "-9"
          - name: "d"
            value:
              type: ":integer"
              value: "-45"
          - name: "e"
            value:
              type: ":integer"
              value: "-76"
          - name: "f"
            value:
              type: ":integer"
              value: "43"
          - name: "g"
            value:
              type: ":integer"
              value: "-62"
          - name: "h"
            value:
              type: ":integer"
              value: "-21"
      - name: "Floats"
        args:
          - name: "a"
            value:
              type: ":float"
              value: "0.6012629994179048"
          - name: "b"
            value:
              type: ":float"
              value: "0.7477740925472398"
          - name: "c"
            value:
              type: ":float"
              value: "0.10301998939503632"
          - name: "d"
            value:
              type: ":float"
              value: "0.4165890778296456"
          - name: "e"
            value:
              type: ":float"
              value: "0.7329967790569901"
```

## Test: nested seeded random scopes restore each outer stream

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
  - name: "nested seeded random scopes restore each outer stream.ges"
    program: main
```

```ges
module AtomicNestedSeededRandom
on Start {
  random with 1 {
    let outerFirst be random from 1 to 100
    random with -1 {
      let innerFirst be random from 1 to 100
      random with 0 {
        emit Deep(value: random from 1 to 100)
      }
      let innerSecond be random from 1 to 100
      emit Inner(first: innerFirst, second: innerSecond)
    }
    let outerSecond be random from 1 to 100
    emit Outer(first: outerFirst, second: outerSecond)
  }
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
      - name: "Deep"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "21"
      - name: "Inner"
        args:
          - name: "first"
            value:
              type: ":integer"
              value: "93"
          - name: "second"
            value:
              type: ":integer"
              value: "70"
      - name: "Outer"
        args:
          - name: "first"
            value:
              type: ":integer"
              value: "58"
          - name: "second"
            value:
              type: ":integer"
              value: "23"
```

## Test: random bounds swap collapse and round portably

```yaml
gesBlock: case
id: case-0005
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: exact
sources:
  - name: "random bounds swap collapse and round portably.ges"
    program: main
```

```ges
module AtomicRandomBounds
on Start {
  let orderedInteger be random with 0 (random from -100 to 100)
  let reversedInteger be random with 0 (random from 100 to -100)
  let orderedFloat be random with 0 (random from -10.0 to 20.0)
  let reversedFloat be random with 0 (random from 20.0 to -10.0)
  let roundedUpperBound be random with 0 (random from 1.0 to 1.0000000000000002)
  emit Done(equalInteger: random from 7 to 7, equalFloat: random from 3.5 to 3.5, orderedInteger: orderedInteger, reversedInteger: reversedInteger, orderedFloat: orderedFloat, reversedFloat: reversedFloat, roundedUpperBound: roundedUpperBound)
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
          - name: "equalInteger"
            value:
              type: ":integer"
              value: "7"
          - name: "equalFloat"
            value:
              type: ":float"
              value: "3.5"
          - name: "orderedInteger"
            value:
              type: ":integer"
              value: "16"
          - name: "reversedInteger"
            value:
              type: ":integer"
              value: "16"
          - name: "orderedFloat"
            value:
              type: ":float"
              value: "8.037889982537145"
          - name: "reversedFloat"
            value:
              type: ":float"
              value: "8.037889982537145"
          - name: "roundedUpperBound"
            value:
              type: ":float"
              value: "1.0000000000000002"
```
