---
formatVersion: 1
suiteId: "runtime.atomic.random"
title: "RuntimeAtomicRandom"
categories: [conformance]
---

# RuntimeAtomicRandom

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite isolates deterministic random operations and their exact seeded outcomes.

---

## Test: random take integer and float opcode paths

This runtime case exercises “random take integer and float opcode paths” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["4", "0.25", "1.5"]
sources:
  - name: "random take integer and float opcode paths.ges"
    program: main
```

### Source code under test

```ges
module atomicrandomtakeopcodes
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
          - name: "integerValue"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "floatValue"
            value:
              type: ":Number.binary64"
              value: "0.25"
          - name: "mixedValue"
            value:
              type: ":Number.binary64"
              value: "1.5"
          - name: "nothingValue"
            value:
              type: ":Nothing"
```

---

## Test: seeded random uses full int64 seed

This runtime case exercises “seeded random uses full int64 seed” and verifies the declared messages, values, and execution result.

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
  - name: "seeded random uses full int64 seed.ges"
    program: main
```

### Source code under test

```ges
module atomicseededrandomlong
on Start {
  let value be random with 4294967297 (random from 1 to 1000000000000)
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
      args: []
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "820299227869"
```

---

## Test: seeded random matches portable known answer vectors

This runtime case exercises “seeded random matches portable known answer vectors” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomicseededrandomknownanswers
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

### Expectation

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
              type: ":Number.int64"
              value: "16"
          - name: "b"
            value:
              type: ":Number.int64"
              value: "-95"
          - name: "c"
            value:
              type: ":Number.int64"
              value: "-9"
          - name: "d"
            value:
              type: ":Number.int64"
              value: "-45"
          - name: "e"
            value:
              type: ":Number.int64"
              value: "-76"
          - name: "f"
            value:
              type: ":Number.int64"
              value: "43"
          - name: "g"
            value:
              type: ":Number.int64"
              value: "-62"
          - name: "h"
            value:
              type: ":Number.int64"
              value: "-21"
      - name: "Floats"
        args:
          - name: "a"
            value:
              type: ":Number.binary64"
              value: "0.6012629994179048"
          - name: "b"
            value:
              type: ":Number.binary64"
              value: "0.7477740925472398"
          - name: "c"
            value:
              type: ":Number.binary64"
              value: "0.10301998939503632"
          - name: "d"
            value:
              type: ":Number.binary64"
              value: "0.4165890778296456"
          - name: "e"
            value:
              type: ":Number.binary64"
              value: "0.7329967790569901"
```

---

## Test: nested seeded random scopes restore each outer stream

This runtime case exercises “nested seeded random scopes restore each outer stream” and verifies the declared messages, values, and execution result.

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
  - name: "nested seeded random scopes restore each outer stream.ges"
    program: main
```

### Source code under test

```ges
module atomicnestedseededrandom
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

### Expectation

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
              type: ":Number.int64"
              value: "21"
      - name: "Inner"
        args:
          - name: "first"
            value:
              type: ":Number.int64"
              value: "93"
          - name: "second"
            value:
              type: ":Number.int64"
              value: "70"
      - name: "Outer"
        args:
          - name: "first"
            value:
              type: ":Number.int64"
              value: "58"
          - name: "second"
            value:
              type: ":Number.int64"
              value: "23"
```

---

## Test: random bounds swap collapse and round portably

This runtime case exercises “random bounds swap collapse and round portably” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
module atomicrandombounds
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
          - name: "equalInteger"
            value:
              type: ":Number.int64"
              value: "7"
          - name: "equalFloat"
            value:
              type: ":Number.binary64"
              value: "3.5"
          - name: "orderedInteger"
            value:
              type: ":Number.int64"
              value: "16"
          - name: "reversedInteger"
            value:
              type: ":Number.int64"
              value: "16"
          - name: "orderedFloat"
            value:
              type: ":Number.binary64"
              value: "8.037889982537145"
          - name: "reversedFloat"
            value:
              type: ":Number.binary64"
              value: "8.037889982537145"
          - name: "roundedUpperBound"
            value:
              type: ":Number.binary64"
              value: "1.0000000000000002"
```

---

## Test: collapsed and NaN bounds do not consume random streams

This runtime case exercises “collapsed and NaN bounds do not consume random streams” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0006
kind: scriptApi
level: atomic
random:
  sequence: ["17", "0.75"]
comparison:
  binary64:
    mode: exact
sources:
  - name: "collapsed and nan bounds preserve streams.ges"
    program: main
```

### Source code under test

```ges
on Start(invalid) {
  random with 0 {
    let equalInteger be random from 7 to 7
    let equalFloat be random from 3.5 to 3.5
    let invalidFloat be random from invalid to 1.0
    emit Seeded(equalInteger: equalInteger, equalFloat: equalFloat, invalidFloat: invalidFloat, nextInteger: random from -100 to 100)
  }
  let equalInteger be random from 7 to 7
  let equalFloat be random from 3.5 to 3.5
  let invalidFloat be random from invalid to 1.0
  emit Sequence(equalInteger: equalInteger, equalFloat: equalFloat, invalidFloat: invalidFloat, nextInteger: random from 0 to 100, nextFloat: random from 0.0 to 1.0)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: invalid
          value: { type: ":Number.binary64", value: "NaN" }
    local:
      - name: Seeded
        args:
          - name: equalInteger
            value: { type: ":Number.int64", value: "7" }
          - name: equalFloat
            value: { type: ":Number.binary64", value: "3.5" }
          - name: invalidFloat
            value: { type: ":Number.binary64", value: "NaN" }
          - name: nextInteger
            value: { type: ":Number.int64", value: "16" }
      - name: Sequence
        args:
          - name: equalInteger
            value: { type: ":Number.int64", value: "7" }
          - name: equalFloat
            value: { type: ":Number.binary64", value: "3.5" }
          - name: invalidFloat
            value: { type: ":Number.binary64", value: "NaN" }
          - name: nextInteger
            value: { type: ":Number.int64", value: "17" }
          - name: nextFloat
            value: { type: ":Number.binary64", value: "0.75" }
```

---

## Test: seeded generator matches raw full-range vectors

This runtime case exercises “seeded generator matches raw full-range vectors” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0007
kind: scriptApi
level: atomic
sources:
  - name: "seeded raw full range vectors.ges"
    program: main
```

### Source code under test

```ges
function draws(minimum, maximum) be [
  random from minimum to maximum,
  random from minimum to maximum,
  random from minimum to maximum,
  random from minimum to maximum,
  random from minimum to maximum,
  random from minimum to maximum,
  random from minimum to maximum,
  random from minimum to maximum]

on Zero(minimum, maximum) { random with 0 { emit Values(values: draws(minimum: minimum, maximum: maximum)) } }
on One(minimum, maximum) { random with 1 { emit Values(values: draws(minimum: minimum, maximum: maximum)) } }
on NegativeOne(minimum, maximum) { random with -1 { emit Values(values: draws(minimum: minimum, maximum: maximum)) } }
on Minimum(minimum, maximum) {
  let seed be (0 - 9223372036854775807 - 1) as :Number
  random with seed { emit Values(values: draws(minimum: minimum, maximum: maximum)) }
}
on Maximum(minimum, maximum) { random with 9223372036854775807 { emit Values(values: draws(minimum: minimum, maximum: maximum)) } }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| zero | Zero | completion | |
| one | One | completion | |
| negative-one | NegativeOne | completion | |
| minimum | Minimum | completion | |
| maximum | Maximum | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  zero:
    input:
      args:
        - { name: minimum, value: { type: ":Number.int64", value: "-9223372036854775808" } }
        - { name: maximum, value: { type: ":Number.int64", value: "9223372036854775807" } }
    local:
      - name: Values
        args:
          - name: values
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "1867972634398290612" }
                - { type: ":Number.int64", value: "4570625273314559274" }
                - { type: ":Number.int64", value: "-7322988658008267040" }
                - { type: ":Number.int64", value: "-1538659934228632276" }
                - { type: ":Number.int64", value: "4298031953262947929" }
                - { type: ":Number.int64", value: "9218731504441215690" }
                - { type: ":Number.int64", value: "-1434944111878255464" }
                - { type: ":Number.int64", value: "657716193016351295" }
  one:
    input:
      args:
        - { name: minimum, value: { type: ":Number.int64", value: "-9223372036854775808" } }
        - { name: maximum, value: { type: ":Number.int64", value: "9223372036854775807" } }
    local:
      - name: Values
        args:
          - name: values
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "3743247123249303749" }
                - { type: ":Number.int64", value: "376989097743764714" }
                - { type: ":Number.int64", value: "1367008882666915092" }
                - { type: ":Number.int64", value: "-2004633466265230425" }
                - { type: ":Number.int64", value: "3637299787140904563" }
                - { type: ":Number.int64", value: "-6574935418888935646" }
                - { type: ":Number.int64", value: "-7912819118364618522" }
                - { type: ":Number.int64", value: "-2191760103874369379" }
  negative-one:
    input:
      args:
        - { name: minimum, value: { type: ":Number.int64", value: "-9223372036854775808" } }
        - { name: maximum, value: { type: ":Number.int64", value: "9223372036854775807" } }
    local:
      - name: Values
        args:
          - name: values
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "1104825383502392584" }
                - { type: ":Number.int64", value: "4933306470170198061" }
                - { type: ":Number.int64", value: "134599743100700318" }
                - { type: ":Number.int64", value: "4568212969449536559" }
                - { type: ":Number.int64", value: "1240059989959942954" }
                - { type: ":Number.int64", value: "4274864459242775845" }
                - { type: ":Number.int64", value: "-2392075413678006306" }
                - { type: ":Number.int64", value: "4937978806164953826" }
  minimum:
    input:
      args:
        - { name: minimum, value: { type: ":Number.int64", value: "-9223372036854775808" } }
        - { name: maximum, value: { type: ":Number.int64", value: "9223372036854775807" } }
    local:
      - name: Values
        args:
          - name: values
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "5772482892184262851" }
                - { type: ":Number.int64", value: "-469382119497855646" }
                - { type: ":Number.int64", value: "-4217990932651081551" }
                - { type: ":Number.int64", value: "7335871315672998252" }
                - { type: ":Number.int64", value: "2733347156748791322" }
                - { type: ":Number.int64", value: "5567399578683514215" }
                - { type: ":Number.int64", value: "-4465951018770553547" }
                - { type: ":Number.int64", value: "-1364060190999775145" }
  maximum:
    input:
      args:
        - { name: minimum, value: { type: ":Number.int64", value: "-9223372036854775808" } }
        - { name: maximum, value: { type: ":Number.int64", value: "9223372036854775807" } }
    local:
      - name: Values
        args:
          - name: values
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "-8206636817657052987" }
                - { type: ":Number.int64", value: "-7415605425696876517" }
                - { type: ":Number.int64", value: "-333518891481786691" }
                - { type: ":Number.int64", value: "-8279322368292135792" }
                - { type: ":Number.int64", value: "-8147555866602263392" }
                - { type: ":Number.int64", value: "4293791176154664783" }
                - { type: ":Number.int64", value: "4245911017751749998" }
                - { type: ":Number.int64", value: "-7832179929631733380" }
```


---

## Test: entropy zero-byte produces the specified stream

This case checks the entropy vector `00` with specified seed `7960286522194355700`. Two independent hosts receive the same bytes and must produce the exact expected Int64 and Binary64 draws across two messages. Expected values are fixed known answers, not computed by the runner from its entropy implementation.

### Case description

```yaml
gesBlock: case
id: entropy-zero-byte
kind: scriptApi
level: atomic
hostCount: 2
compile:
  binaryRoundTrip: true
random:
  entropy: "00"
```

### Source code under test

```ges
on Start {
  let integers be :List[:select item from 1 to 4 => random from -9223372036854775808 to 9223372036854775807]
  let fractions be :List[:select item from 1 to 4 => random from 0.0 to 1.0]
  emit Done(integers: integers, fractions: fractions)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| first | Start | completion | |
| second | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  first:
    input:
      args: []
    local:
      - name: Done
        args:
          - name: integers
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "-1801742914047273126"
                - type: ":Number.int64"
                  value: "6906618146802271930"
                - type: ":Number.int64"
                  value: "8906945353371085094"
                - type: ":Number.int64"
                  value: "-3516509875896380310"
          - name: fractions
            value:
              type: ":List"
              items:
                - type: ":Number.binary64"
                  value: "0.6223943266806276"
                - type: ":Number.binary64"
                  value: "0.26512990897891975"
                - type: ":Number.binary64"
                  value: "0.24935350628619746"
                - type: ":Number.binary64"
                  value: "0.15459849413800142"
  second:
    input:
      args: []
    local:
      - name: Done
        args:
          - name: integers
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "-8686397166268783544"
                - type: ":Number.int64"
                  value: "5241406879626227521"
                - type: ":Number.int64"
                  value: "-2582843479293753457"
                - type: ":Number.int64"
                  value: "-7605497857510631640"
          - name: fractions
            value:
              type: ":List"
              items:
                - type: ":Number.binary64"
                  value: "0.703556533304155"
                - type: ":Number.binary64"
                  value: "0.2105796006076005"
                - type: ":Number.binary64"
                  value: "0.8293769217688932"
                - type: ":Number.binary64"
                  value: "0.48143022033185934"
```


---

## Test: entropy eight-ordered-bytes produces the specified stream

This case checks the entropy vector `0102030405060708` with specified seed `1257585870541503724`. Two independent hosts receive the same bytes and must produce the exact expected Int64 and Binary64 draws across two messages. Expected values are fixed known answers, not computed by the runner from its entropy implementation.

### Case description

```yaml
gesBlock: case
id: entropy-eight-ordered-bytes
kind: scriptApi
level: atomic
hostCount: 2
compile:
  binaryRoundTrip: true
random:
  entropy: "0102030405060708"
```

### Source code under test

```ges
on Start {
  let integers be :List[:select item from 1 to 4 => random from -9223372036854775808 to 9223372036854775807]
  let fractions be :List[:select item from 1 to 4 => random from 0.0 to 1.0]
  emit Done(integers: integers, fractions: fractions)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| first | Start | completion | |
| second | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  first:
    input:
      args: []
    local:
      - name: Done
        args:
          - name: integers
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "-7574494408109740143"
                - type: ":Number.int64"
                  value: "7702978289446259023"
                - type: ":Number.int64"
                  value: "-8697775356578377073"
                - type: ":Number.int64"
                  value: "5318479663885001588"
          - name: fractions
            value:
              type: ":List"
              items:
                - type: ":Number.binary64"
                  value: "0.9344824465193561"
                - type: ":Number.binary64"
                  value: "0.08751081340657874"
                - type: ":Number.binary64"
                  value: "0.7124670705180005"
                - type: ":Number.binary64"
                  value: "0.7658160597423257"
  second:
    input:
      args: []
    local:
      - name: Done
        args:
          - name: integers
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "8338194038206138127"
                - type: ":Number.int64"
                  value: "-2709963078321457182"
                - type: ":Number.int64"
                  value: "-2915471211011131715"
                - type: ":Number.int64"
                  value: "8758672477004886073"
          - name: fractions
            value:
              type: ":List"
              items:
                - type: ":Number.binary64"
                  value: "0.1631142814931038"
                - type: ":Number.binary64"
                  value: "0.9256033898358259"
                - type: ":Number.binary64"
                  value: "0.6814499593074986"
                - type: ":Number.binary64"
                  value: "0.3095940071575567"
```


---

## Test: entropy ascii-bytes produces the specified stream

This case checks the entropy vector `47616D654576656E74536372697074` with specified seed `2502825403663315715`. Two independent hosts receive the same bytes and must produce the exact expected Int64 and Binary64 draws across two messages. Expected values are fixed known answers, not computed by the runner from its entropy implementation.

### Case description

```yaml
gesBlock: case
id: entropy-ascii-bytes
kind: scriptApi
level: atomic
hostCount: 2
compile:
  binaryRoundTrip: true
random:
  entropy: "47616D654576656E74536372697074"
```

### Source code under test

```ges
on Start {
  let integers be :List[:select item from 1 to 4 => random from -9223372036854775808 to 9223372036854775807]
  let fractions be :List[:select item from 1 to 4 => random from 0.0 to 1.0]
  emit Done(integers: integers, fractions: fractions)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| first | Start | completion | |
| second | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  first:
    input:
      args: []
    local:
      - name: Done
        args:
          - name: integers
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "5661967672262427672"
                - type: ":Number.int64"
                  value: "8397587441353728323"
                - type: ":Number.int64"
                  value: "-6511896062694124207"
                - type: ":Number.int64"
                  value: "-6137984034057242569"
          - name: fractions
            value:
              type: ":List"
              items:
                - type: ":Number.binary64"
                  value: "0.1429987804566636"
                - type: ":Number.binary64"
                  value: "0.7789488332206926"
                - type: ":Number.binary64"
                  value: "0.754718025528599"
                - type: ":Number.binary64"
                  value: "0.6813966483605702"
  second:
    input:
      args: []
    local:
      - name: Done
        args:
          - name: integers
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1973910319732067812"
                - type: ":Number.int64"
                  value: "-3378188145231525588"
                - type: ":Number.int64"
                  value: "8029391897131675318"
                - type: ":Number.int64"
                  value: "3268670417125390371"
          - name: fractions
            value:
              type: ":List"
              items:
                - type: ":Number.binary64"
                  value: "0.18079134806262132"
                - type: ":Number.binary64"
                  value: "0.11848819503364916"
                - type: ":Number.binary64"
                  value: "0.8563450483842715"
                - type: ":Number.binary64"
                  value: "0.9683315360179386"
```


---

## Test: entropy high-bit-bytes produces the specified stream

This case checks the entropy vector `FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF` with specified seed `-960758279163454167`. Two independent hosts receive the same bytes and must produce the exact expected Int64 and Binary64 draws across two messages. Expected values are fixed known answers, not computed by the runner from its entropy implementation.

### Case description

```yaml
gesBlock: case
id: entropy-high-bit-bytes
kind: scriptApi
level: atomic
hostCount: 2
compile:
  binaryRoundTrip: true
random:
  entropy: "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"
```

### Source code under test

```ges
on Start {
  let integers be :List[:select item from 1 to 4 => random from -9223372036854775808 to 9223372036854775807]
  let fractions be :List[:select item from 1 to 4 => random from 0.0 to 1.0]
  emit Done(integers: integers, fractions: fractions)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| first | Start | completion | |
| second | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  first:
    input:
      args: []
    local:
      - name: Done
        args:
          - name: integers
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "-5451881170504208589"
                - type: ":Number.int64"
                  value: "-8982560757595855800"
                - type: ":Number.int64"
                  value: "6175962179834172202"
                - type: ":Number.int64"
                  value: "5466366218387127367"
          - name: fractions
            value:
              type: ":List"
              items:
                - type: ":Number.binary64"
                  value: "0.945998168359484"
                - type: ":Number.binary64"
                  value: "0.11452655563981051"
                - type: ":Number.binary64"
                  value: "0.7356091639628609"
                - type: ":Number.binary64"
                  value: "0.7189526713655172"
  second:
    input:
      args: []
    local:
      - name: Done
        args:
          - name: integers
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "-2513461824255004494"
                - type: ":Number.int64"
                  value: "-4245606879026237042"
                - type: ":Number.int64"
                  value: "-852221283529000168"
                - type: ":Number.int64"
                  value: "-510922514678258511"
          - name: fractions
            value:
              type: ":List"
              items:
                - type: ":Number.binary64"
                  value: "0.6198394929706643"
                - type: ":Number.binary64"
                  value: "0.13814398629503621"
                - type: ":Number.binary64"
                  value: "0.51175595396124"
                - type: ":Number.binary64"
                  value: "0.7675657267355153"
```
