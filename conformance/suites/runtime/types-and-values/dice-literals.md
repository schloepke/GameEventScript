---
formatVersion: 1
suiteId: runtime.dice-literals
title: Deterministic Dice literals and text roundtrips
kind: scriptApi
level: atomic
categories: [conformance]
---

# Deterministic Dice literals and text roundtrips

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

These cases define portable Dice result literals, exact text reconstruction, parser limits, and source rejection independently of the runtime implementation.

---

## Test: source-values

This case verifies deterministic literal construction, descending order, duplicates, empty Dice, numeric spellings, selectors, casts, and unchanged random consumption.

### Case description

```yaml
gesBlock: case
id: "source-values"
compile:
  binaryRoundTrip: true
random:
  sequence:
    - "5"
```

### Source code under test

```ges
on Start {
  let literal be :Dice [1, 6, 3, 3]
  let parsed be parse ":Dice[2, 5]"
  let converted be :Dice([2, 4])
  let numericDice be :Dice[1.0, 2, 1_000, 2_147_483_647]
  let multiline be :Dice[
    1, // Source comments are normal trivia.
    2
  ]
  emit Done(literal: literal, emptyDice: :Dice[], numericDice: numericDice, multiline: multiline, parsed: parsed, converted: converted, scalarCast: :Dice(1), first: :Dice[1, 6, 3][1], highest: literal[:take highest 2], randomValue: random from 1 to 6)
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
      args: []
    local:
      - name: "Done"
        args:
          - name: "literal"
            value:
              type: ":Dice"
              rolls:
                - 6
                - 3
                - 3
                - 1
          - name: "emptyDice"
            value:
              type: ":Dice"
              rolls: []
          - name: "numericDice"
            value:
              type: ":Dice"
              rolls:
                - 2147483647
                - 1000
                - 2
                - 1
          - name: "multiline"
            value:
              type: ":Dice"
              rolls:
                - 2
                - 1
          - name: "parsed"
            value:
              type: ":Dice"
              rolls:
                - 5
                - 2
          - name: "converted"
            value:
              type: ":Dice"
              rolls:
                - 4
                - 2
          - name: "scalarCast"
            value:
              type: ":Dice"
              rolls: []
          - name: "first"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "highest"
            value:
              type: ":Dice"
              rolls:
                - 6
                - 3
          - name: "randomValue"
            value:
              type: ":Number.int64"
              value: "5"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: text-roundtrips

This case verifies typed Dice roundtrips at the root and inside nested containers while keeping similarly spelled Text quoted.

### Case description

```yaml
gesBlock: case
id: "text-roundtrips"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
  let value be :Dice[1, 6, 3, 3]
  let text be value as :Text
  let restored be parse text
  let data be [rolls: value, nested: [:Dice[], ':Dice[1]']]
  let nested be parse (data as :Text)
  emit Done(text: text, emptyText: :Dice[] as :Text, restored: restored, same: restored = value, rootIsDice: restored is :Dice, nestedSame: nested = data, nestedIsDice: nested.nested[1] is :Dice, nestedIsText: nested.nested[2] is :Text)
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
      args: []
    local:
      - name: "Done"
        args:
          - name: "text"
            value:
              type: ":Text"
              value: ":Dice[6, 3, 3, 1]"
          - name: "emptyText"
            value:
              type: ":Text"
              value: ":Dice[]"
          - name: "restored"
            value:
              type: ":Dice"
              rolls:
                - 6
                - 3
                - 3
                - 1
          - name: "same"
            value:
              type: ":Boolean"
              value: true
          - name: "rootIsDice"
            value:
              type: ":Boolean"
              value: true
          - name: "nestedSame"
            value:
              type: ":Boolean"
              value: true
          - name: "nestedIsDice"
            value:
              type: ":Boolean"
              value: true
          - name: "nestedIsText"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: parse-valid

This case verifies exact type recognition, allowed runtime whitespace, integer spellings, bounds, comma delimiters, and normalized text output.

### Case description

```yaml
gesBlock: case
id: "parse-valid"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) { let parsed be parse value; emit Done(parsed: parsed, text: parsed as :Text) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| input-01 | Start | completion | |
| input-02 | Start | completion | |
| input-03 | Start | completion | |
| input-04 | Start | completion | |
| input-05 | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  input-01:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Dice"
              rolls: []
          - name: "text"
            value:
              type: ":Text"
              value: ":Dice[]"
    runtimeLimits:
      exclude:
        - any: true
  input-02:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[1, 6, 3, 3]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Dice"
              rolls:
                - 6
                - 3
                - 3
                - 1
          - name: "text"
            value:
              type: ":Text"
              value: ":Dice[6, 3, 3, 1]"
    runtimeLimits:
      exclude:
        - any: true
  input-03:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: " \t:Dice \r\n[1,\t2,\r3]\n"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Dice"
              rolls:
                - 3
                - 2
                - 1
          - name: "text"
            value:
              type: ":Text"
              value: ":Dice[3, 2, 1]"
    runtimeLimits:
      exclude:
        - any: true
  input-04:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[1.0, 2e0, 1_000, 2_147_483_647]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Dice"
              rolls:
                - 2147483647
                - 1000
                - 2
                - 1
          - name: "text"
            value:
              type: ":Text"
              value: ":Dice[2147483647, 1000, 2, 1]"
    runtimeLimits:
      exclude:
        - any: true
  input-05:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[1,003]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Dice"
              rolls:
                - 3
                - 1
          - name: "text"
            value:
              type: ":Text"
              value: ":Dice[3, 1]"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: parse-invalid

This case verifies all-or-nothing recognition and exact original-Text fallback for invalid rolls, executable forms, malformed syntax, and invalid nested literals.

### Case description

```yaml
gesBlock: case
id: "parse-invalid"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) { emit Done(parsed: parse value) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| input-01 | Start | completion | |
| input-02 | Start | completion | |
| input-03 | Start | completion | |
| input-04 | Start | completion | |
| input-05 | Start | completion | |
| input-06 | Start | completion | |
| input-07 | Start | completion | |
| input-08 | Start | completion | |
| input-09 | Start | completion | |
| input-10 | Start | completion | |
| input-11 | Start | completion | |
| input-12 | Start | completion | |
| input-13 | Start | completion | |
| input-14 | Start | completion | |
| input-15 | Start | completion | |
| input-16 | Start | completion | |
| input-17 | Start | completion | |
| input-18 | Start | completion | |
| input-19 | Start | completion | |
| input-20 | Start | completion | |
| input-21 | Start | completion | |
| input-22 | Start | completion | |
| input-23 | Start | completion | |
| input-24 | Start | completion | |
| input-25 | Start | completion | |
| input-26 | Start | completion | |
| input-27 | Start | completion | |
| input-28 | Start | completion | |
| input-29 | Start | completion | |
| input-30 | Start | completion | |
| input-31 | Start | completion | |
| input-32 | Start | completion | |
| input-33 | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  input-01:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[0]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[0]"
    runtimeLimits:
      exclude:
        - any: true
  input-02:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[-1]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[-1]"
    runtimeLimits:
      exclude:
        - any: true
  input-03:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[+1]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[+1]"
    runtimeLimits:
      exclude:
        - any: true
  input-04:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[1.5]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[1.5]"
    runtimeLimits:
      exclude:
        - any: true
  input-05:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[2147483648]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[2147483648]"
    runtimeLimits:
      exclude:
        - any: true
  input-06:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[1m]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[1m]"
    runtimeLimits:
      exclude:
        - any: true
  input-07:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[100%]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[100%]"
    runtimeLimits:
      exclude:
        - any: true
  input-08:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[Infinity]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[Infinity]"
    runtimeLimits:
      exclude:
        - any: true
  input-09:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[true]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[true]"
    runtimeLimits:
      exclude:
        - any: true
  input-10:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[nothing]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[nothing]"
    runtimeLimits:
      exclude:
        - any: true
  input-11:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[\"1\"]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[\"1\"]"
    runtimeLimits:
      exclude:
        - any: true
  input-12:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[#ready]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[#ready]"
    runtimeLimits:
      exclude:
        - any: true
  input-13:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[value]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[value]"
    runtimeLimits:
      exclude:
        - any: true
  input-14:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[$value]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[$value]"
    runtimeLimits:
      exclude:
        - any: true
  input-15:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[1 + 2]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[1 + 2]"
    runtimeLimits:
      exclude:
        - any: true
  input-16:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[roll dice 1d6]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[roll dice 1d6]"
    runtimeLimits:
      exclude:
        - any: true
  input-17:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[[1]]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[[1]]"
    runtimeLimits:
      exclude:
        - any: true
  input-18:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[:Dice[1]]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[:Dice[1]]"
    runtimeLimits:
      exclude:
        - any: true
  input-19:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[x: 1]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[x: 1]"
    runtimeLimits:
      exclude:
        - any: true
  input-20:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[1,]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[1,]"
    runtimeLimits:
      exclude:
        - any: true
  input-21:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[1 2]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[1 2]"
    runtimeLimits:
      exclude:
        - any: true
  input-22:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice["
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice["
    runtimeLimits:
      exclude:
        - any: true
  input-23:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[1"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[1"
    runtimeLimits:
      exclude:
        - any: true
  input-24:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[1] trailing"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[1] trailing"
    runtimeLimits:
      exclude:
        - any: true
  input-25:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice([1, 2])"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice([1, 2])"
    runtimeLimits:
      exclude:
        - any: true
  input-26:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "dice[2, 1]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: "dice[2, 1]"
    runtimeLimits:
      exclude:
        - any: true
  input-27:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":dice[1]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":dice[1]"
    runtimeLimits:
      exclude:
        - any: true
  input-28:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":DiceExtra[1]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":DiceExtra[1]"
    runtimeLimits:
      exclude:
        - any: true
  input-29:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice[1 // comment\n]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice[1 // comment\n]"
    runtimeLimits:
      exclude:
        - any: true
  input-30:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Dice [1]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Dice [1]"
    runtimeLimits:
      exclude:
        - any: true
  input-31:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[:Dice[1], :Dice[0]]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: "[:Dice[1], :Dice[0]]"
    runtimeLimits:
      exclude:
        - any: true
  input-32:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[rolls: :Dice[1,]]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: "[rolls: :Dice[1,]]"
    runtimeLimits:
      exclude:
        - any: true
  input-33:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: " \t:Dice[0]\r\n"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: " \t:Dice[0]\r\n"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: depth-at-limit

This case counts an empty Dice literal as one container level beneath nested Lists.

### Case description

```yaml
gesBlock: case
id: "depth-at-limit"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) { emit Before; let parsed be parse value; emit Done(isList: parsed is :List) }
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
        - name: "value"
          value:
            type: ":Text"
            value: "[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[:Dice[]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]"
    local:
      - name: "Before"
        args: []
      - name: "Done"
        args:
          - name: "isList"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: depth-over-limit

This case counts an empty Dice literal as one container level beneath nested Lists.

### Case description

```yaml
gesBlock: case
id: "depth-over-limit"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) { emit Before; let parsed be parse value; emit Done(isList: parsed is :List) }
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
        - name: "value"
          value:
            type: ":Text"
            value: "[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[:Dice[]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]"
    local:
      - name: "Before"
        args: []
    runtimeLimits:
      include:
        - name: "MaxLiteralDepth"
```

---

## Test: items-at-limit

This case counts every Dice roll toward the shared parse item limit without imposing a random-draw limit.

### Case description

```yaml
gesBlock: case
id: "items-at-limit"
compile:
  binaryRoundTrip: true
runtimeLimits:
  maxExecutionSteps: 3000000
  maxLoopIterations: 200000
  maxRangeItems: 200000
  maxGeneratedCollectionItems: 200000
```

### Source code under test

```ges
on Start {
  let input be ":Dice" + (:List[:select item from 1 to 65536 => 1] as :Text)
  emit Before
  let parsed be parse input
  emit Done(count: parsed[:count], isDice: parsed is :Dice)
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
      args: []
    local:
      - name: "Before"
        args: []
      - name: "Done"
        args:
          - name: "count"
            value:
              type: ":Number.int64"
              value: "65536"
          - name: "isDice"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: items-over-limit

This case counts every Dice roll toward the shared parse item limit without imposing a random-draw limit.

### Case description

```yaml
gesBlock: case
id: "items-over-limit"
compile:
  binaryRoundTrip: true
runtimeLimits:
  maxExecutionSteps: 3000000
  maxLoopIterations: 200000
  maxRangeItems: 200000
  maxGeneratedCollectionItems: 200000
```

### Source code under test

```ges
on Start {
  let input be ":Dice" + (:List[:select item from 1 to 65537 => 1] as :Text)
  emit Before
  let parsed be parse input
  emit Done(count: parsed[:count], isDice: parsed is :Dice)
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
      args: []
    local:
      - name: "Before"
        args: []
    runtimeLimits:
      include:
        - name: "MaxLiteralItems"
```

---

## Test: nested-items-at-limit

This case shares item accounting between an enclosing Map entry and all stored Dice rolls.

### Case description

```yaml
gesBlock: case
id: "nested-items-at-limit"
compile:
  binaryRoundTrip: true
runtimeLimits:
  maxExecutionSteps: 3000000
  maxLoopIterations: 200000
  maxRangeItems: 200000
  maxGeneratedCollectionItems: 200000
```

### Source code under test

```ges
on Start {
  let rolls be :List[:select item from 1 to 65535 => 1] as :Text
  let input be "[rolls: :Dice" + rolls + "]"
  emit Before
  let parsed be parse input
  emit Done(count: parsed.rolls[:count])
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
      args: []
    local:
      - name: "Before"
        args: []
      - name: "Done"
        args:
          - name: "count"
            value:
              type: ":Number.int64"
              value: "65535"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: nested-items-over-limit

This case shares item accounting between an enclosing Map entry and all stored Dice rolls.

### Case description

```yaml
gesBlock: case
id: "nested-items-over-limit"
compile:
  binaryRoundTrip: true
runtimeLimits:
  maxExecutionSteps: 3000000
  maxLoopIterations: 200000
  maxRangeItems: 200000
  maxGeneratedCollectionItems: 200000
```

### Source code under test

```ges
on Start {
  let rolls be :List[:select item from 1 to 65536 => 1] as :Text
  let input be "[rolls: :Dice" + rolls + "]"
  emit Before
  let parsed be parse input
  emit Done(count: parsed.rolls[:count])
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
      args: []
    local:
      - name: "Before"
        args: []
    runtimeLimits:
      include:
        - name: "MaxLiteralItems"
```

---

## Test: source-rejects-zero

This case rejects a Dice source form outside the positive Int32 numeric-literal grammar.

### Case description

```yaml
gesBlock: case
id: "source-rejects-zero"
kind: "compileError"
```

### Source code under test

```ges
on Start { let value be 1; emit Done(value: :Dice[0]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: source-rejects-negative

This case rejects a Dice source form outside the positive Int32 numeric-literal grammar.

### Case description

```yaml
gesBlock: case
id: "source-rejects-negative"
kind: "compileError"
```

### Source code under test

```ges
on Start { let value be 1; emit Done(value: :Dice[-1]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: source-rejects-explicit-plus

This case rejects a Dice source form outside the positive Int32 numeric-literal grammar.

### Case description

```yaml
gesBlock: case
id: "source-rejects-explicit-plus"
kind: "compileError"
```

### Source code under test

```ges
on Start { let value be 1; emit Done(value: :Dice[+1]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: source-rejects-fraction

This case rejects a Dice source form outside the positive Int32 numeric-literal grammar.

### Case description

```yaml
gesBlock: case
id: "source-rejects-fraction"
kind: "compileError"
```

### Source code under test

```ges
on Start { let value be 1; emit Done(value: :Dice[1.5]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: source-rejects-overflow

This case rejects a Dice source form outside the positive Int32 numeric-literal grammar.

### Case description

```yaml
gesBlock: case
id: "source-rejects-overflow"
kind: "compileError"
```

### Source code under test

```ges
on Start { let value be 1; emit Done(value: :Dice[2147483648]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: source-rejects-unit

This case rejects a Dice source form outside the positive Int32 numeric-literal grammar.

### Case description

```yaml
gesBlock: case
id: "source-rejects-unit"
kind: "compileError"
```

### Source code under test

```ges
on Start { let value be 1; emit Done(value: :Dice[1m]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: source-rejects-percentage

This case rejects a Dice source form outside the positive Int32 numeric-literal grammar.

### Case description

```yaml
gesBlock: case
id: "source-rejects-percentage"
kind: "compileError"
```

### Source code under test

```ges
on Start { let value be 1; emit Done(value: :Dice[100%]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: source-rejects-boolean

This case rejects a Dice source form outside the positive Int32 numeric-literal grammar.

### Case description

```yaml
gesBlock: case
id: "source-rejects-boolean"
kind: "compileError"
```

### Source code under test

```ges
on Start { let value be 1; emit Done(value: :Dice[true]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: source-rejects-text

This case rejects a Dice source form outside the positive Int32 numeric-literal grammar.

### Case description

```yaml
gesBlock: case
id: "source-rejects-text"
kind: "compileError"
```

### Source code under test

```ges
on Start { let value be 1; emit Done(value: :Dice['1']) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: source-rejects-variable

This case rejects a Dice source form outside the positive Int32 numeric-literal grammar.

### Case description

```yaml
gesBlock: case
id: "source-rejects-variable"
kind: "compileError"
```

### Source code under test

```ges
on Start { let value be 1; emit Done(value: :Dice[value]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: source-rejects-expression

This case rejects a Dice source form outside the positive Int32 numeric-literal grammar.

### Case description

```yaml
gesBlock: case
id: "source-rejects-expression"
kind: "compileError"
```

### Source code under test

```ges
on Start { let value be 1; emit Done(value: :Dice[1 + 2]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: source-rejects-nested-list

This case rejects a Dice source form outside the positive Int32 numeric-literal grammar.

### Case description

```yaml
gesBlock: case
id: "source-rejects-nested-list"
kind: "compileError"
```

### Source code under test

```ges
on Start { let value be 1; emit Done(value: :Dice[[1]]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: source-rejects-map-entry

This case rejects a Dice source form outside the positive Int32 numeric-literal grammar.

### Case description

```yaml
gesBlock: case
id: "source-rejects-map-entry"
kind: "compileError"
```

### Source code under test

```ges
on Start { let value be 1; emit Done(value: :Dice[x: 1]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: source-rejects-trailing-comma

This case rejects a Dice source form outside the positive Int32 numeric-literal grammar.

### Case description

```yaml
gesBlock: case
id: "source-rejects-trailing-comma"
kind: "compileError"
```

### Source code under test

```ges
on Start { let value be 1; emit Done(value: :Dice[1,]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: source-rejects-missing-comma

This case rejects a Dice source form outside the positive Int32 numeric-literal grammar.

### Case description

```yaml
gesBlock: case
id: "source-rejects-missing-comma"
kind: "compileError"
```

### Source code under test

```ges
on Start { let value be 1; emit Done(value: :Dice[1 2]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```
