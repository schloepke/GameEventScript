---
formatVersion: 1
suiteId: "runtime.random-dice-ranges"
title: "RuntimeRandomDiceRanges"
categories: [conformance]
---

# RuntimeRandomDiceRanges

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies deterministic random scopes, dice values, integer and floating ranges, and their boundary behavior.

---

## Test: random and dice outcomes are deterministic with sequence

This runtime case exercises “random and dice outcomes are deterministic with sequence” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0001
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["4", "1", "6", "3", "5"]
sources:
  - name: "random and dice outcomes are deterministic with sequence.ges"
    program: main
```

### Source code under test

```ges
on Roll {
  let a be random 1 to 6
  let b be roll dice 4d6[:take highest 2]
  emit Result(randomValue: a, diceValue: b)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Roll | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Result"
        args:
          - name: "randomValue"
            value:
              type: ":integer"
              value: "4"
          - name: "diceValue"
            value:
              type: ":dice"
              rolls:
                - 6
                - 5
```

---

## Test: sequential steps share random sequence

This runtime case exercises “sequential steps share random sequence” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0002
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["1", "6", "3", "2"]
sources:
  - name: "sequential steps share random sequence.ges"
    program: main
```

### Source code under test

```ges
on Roll {
  let value be roll dice 2d6
  emit Rolled(value: value)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Roll | completion |  |
| step-0002 | Roll | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Rolled"
        args:
          - name: "value"
            value:
              type: ":dice"
              rolls:
                - 6
                - 1
  step-0002:
    input:
      args: []
    local:
      - name: "Rolled"
        args:
          - name: "value"
            value:
              type: ":dice"
              rolls:
                - 3
                - 2
```

---

## Test: chance and weighted choices are deterministic

This runtime case exercises “chance and weighted choices are deterministic” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0003
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["0.249999", "9", "0", "8.99999"]
sources:
  - name: "chance and weighted choices are deterministic.ges"
    program: main
```

### Source code under test

```ges
on Start(units) {
  let alwaysHit be chance 100%
  let neverHit be chance 0%
  let quarterHit be chance 25%
  let weightedTarget be units[:choose 1 weighted by unit => unit.weight]
  let weightedPair be units[:choose 2 weighted by unit => unit.weight]
  emit Done(alwaysHit: alwaysHit, neverHit: neverHit, quarterHit: quarterHit, weightedTarget: weightedTarget.name, weighted_1: weightedPair[1].name, weighted_2: weightedPair[2].name)
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
        - name: "units"
          value:
            type: ":list"
            items:
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "A"
                  - key: "weight"
                    value:
                      type: ":integer"
                      value: "1"
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "B"
                  - key: "weight"
                    value:
                      type: ":integer"
                      value: "3"
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "C"
                  - key: "weight"
                    value:
                      type: ":integer"
                      value: "6"
    local:
      - name: "Done"
        args:
          - name: "alwaysHit"
            value:
              type: ":boolean"
              value: true
          - name: "neverHit"
            value:
              type: ":boolean"
              value: false
          - name: "quarterHit"
            value:
              type: ":boolean"
              value: true
          - name: "weightedTarget"
            value:
              type: ":text"
              value: "C"
          - name: "weighted_1"
            value:
              type: ":text"
              value: "A"
          - name: "weighted_2"
            value:
              type: ":text"
              value: "C"
```

---

## Test: shuffle draw choose and seeded random scopes are deterministic

This runtime case exercises “shuffle draw choose and seeded random scopes are deterministic” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["1", "0", "1", "1", "0", "1"]
sources:
  - name: "shuffle draw choose and seeded random scopes are deterministic.ges"
    program: main
```

### Source code under test

```ges
on Start(enemies, seed as :number) {
  let cards be [1, 2, 3, 4, 5][:shuffle]
  let hand be cards[:draw 3]
  let restCards be cards[:drop first 3]
  let target be enemies[:choose 1 enemy where enemy.alive]
  let randomTargets be enemies[:choose 2 at random enemy where enemy.alive]
  let seededFirst be random with seed :list[:select item from 1 to 4 => random from 1 to 20]
  let seededSecond be random with seed :list[:select item from 1 to 4 => random from 1 to 20]
  emit Done(hand_1: hand[1], hand_2: hand[2], hand_3: hand[3], rest_1: restCards[1], rest_2: restCards[2], target: target.name, random_1: randomTargets[1].name, random_2: randomTargets[2].name, seededSame: seededFirst = seededSecond)
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
        - name: "enemies"
          value:
            type: ":list"
            items:
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "Rook"
                  - key: "alive"
                    value:
                      type: ":boolean"
                      value: false
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "Orc"
                  - key: "alive"
                    value:
                      type: ":boolean"
                      value: true
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "Mage"
                  - key: "alive"
                    value:
                      type: ":boolean"
                      value: true
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "Knight"
                  - key: "alive"
                    value:
                      type: ":boolean"
                      value: true
        - name: "seed"
          value:
            type: ":integer"
            value: "42"
    local:
      - name: "Done"
        args:
          - name: "hand_1"
            value:
              type: ":integer"
              value: "4"
          - name: "hand_2"
            value:
              type: ":integer"
              value: "3"
          - name: "hand_3"
            value:
              type: ":integer"
              value: "5"
          - name: "rest_1"
            value:
              type: ":integer"
              value: "1"
          - name: "rest_2"
            value:
              type: ":integer"
              value: "2"
          - name: "target"
            value:
              type: ":text"
              value: "Orc"
          - name: "random_1"
            value:
              type: ":text"
              value: "Orc"
          - name: "random_2"
            value:
              type: ":text"
              value: "Knight"
          - name: "seededSame"
            value:
              type: ":boolean"
              value: true
```

---

## Test: dynamic invalid seeded random seed still restores outer random scope

This runtime case exercises “dynamic invalid seeded random seed still restores outer random scope” and verifies the declared messages, values, and execution result.

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
  - name: "dynamic invalid seeded random seed still restores outer random scope.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let bad as :number be nothing
  random with bad {
    let inner be random from 1 to 6
  }
  emit Done(valuePresent: (random from 1 to 6) has value)
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
          - name: "valuePresent"
            value:
              type: ":boolean"
              value: true
```

---

## Test: reversed random bounds and invalid collection draws stay lenient

This runtime case exercises “reversed random bounds and invalid collection draws stay lenient” and verifies the declared messages, values, and execution result.

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
  - name: "reversed random bounds and invalid collection draws stay lenient.ges"
    program: main
```

### Source code under test

```ges
on Roll(low, high) {
  let value be random from low to high
  let none as :number be nothing
  let nothingValue be random from none to high
  let invalidValue be random from 'hello' to high
  let tags be 123
  let shuffledTags be tags[:shuffle]
  let drawnTag be tags[:draw 1]
  emit Done(valuePresent: value has value, nothingValue: nothingValue, invalidValue: invalidValue, shuffledTags: shuffledTags, drawnTag: drawnTag)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Roll | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "low"
          value:
            type: ":integer"
            value: "9"
        - name: "high"
          value:
            type: ":integer"
            value: "2"
    local:
      - name: "Done"
        args:
          - name: "valuePresent"
            value:
              type: ":boolean"
              value: true
          - name: "nothingValue"
            value:
              type: ":nothing"
          - name: "invalidValue"
            value:
              type: ":float"
              value: "NaN"
          - name: "shuffledTags"
            value:
              type: ":nothing"
          - name: "drawnTag"
            value:
              type: ":nothing"
```

---

## Test: random can produce integer double and mixed values

This runtime case exercises “random can produce integer double and mixed values” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0007
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
random:
  sequence: ["4", "0.25", "1.5"]
sources:
  - name: "random can produce integer double and mixed values.ges"
    program: main
```

### Source code under test

```ges
on Roll {
  let integerValue be random from 1 to 6
  let floatValue be random from 0.0 to 1.0
  let mixedValue be random from 1 to 2.0
  emit Done(integerValue: integerValue, floatValue: floatValue, mixedValue: mixedValue)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Roll | completion |  |

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
```

---

## Test: random integer bounds preserve int64 values

This runtime case exercises “random integer bounds preserve int64 values” and verifies the declared messages, values, and execution result.

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
  - name: "random integer bounds preserve int64 values.ges"
    program: main
```

### Source code under test

```ges
on Roll {
  let maxValue be random from 9223372036854775807 to 9223372036854775807
  let negativeValue be random from 0 - 9223372036854775807 to 0 - 9223372036854775807
  emit Done(maxValue: maxValue, negativeValue: negativeValue)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Roll | completion |  |

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
          - name: "maxValue"
            value:
              type: ":integer"
              value: "9223372036854775807"
          - name: "negativeValue"
            value:
              type: ":integer"
              value: "-9223372036854775807"
```

---

## Test: random upper bound keeps arithmetic together

This runtime case exercises “random upper bound keeps arithmetic together” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["5"]
sources:
  - name: "random upper bound keeps arithmetic together.ges"
    program: main
```

### Source code under test

```ges
on Mixed(high) {
  if random 1 to high * 2 > 3 {
    emit Passed
  }
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Mixed | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "high"
          value:
            type: ":integer"
            value: "3"
    local:
      - name: "Passed"
        args: []
```

---

## Test: range lookup length and containment preserve int64 edges

This runtime case exercises “range lookup length and containment preserve int64 edges” and verifies the declared messages, values, and execution result.

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
  - name: "range lookup length and containment preserve int64 edges.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let high be from 9223372036854775806 to 9223372036854775807
  let low be from (0 - 9223372036854775807 - 1) to (0 - 9223372036854775807)
  emit Done(highLen: high[:count], highFirst: high[1], highSecond: high[2], highContainsMax: 9223372036854775807 in high, highContainsBefore: 9223372036854775805 in high, lowLen: low[:count], lowFirst: low[1], lowSecond: low[2], lowContainsMin: (0 - 9223372036854775807 - 1) in low)
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
          - name: "highLen"
            value:
              type: ":integer"
              value: "2"
          - name: "highFirst"
            value:
              type: ":integer"
              value: "9223372036854775806"
          - name: "highSecond"
            value:
              type: ":integer"
              value: "9223372036854775807"
          - name: "highContainsMax"
            value:
              type: ":boolean"
              value: true
          - name: "highContainsBefore"
            value:
              type: ":boolean"
              value: false
          - name: "lowLen"
            value:
              type: ":integer"
              value: "2"
          - name: "lowFirst"
            value:
              type: ":integer"
              value: "-9223372036854775808"
          - name: "lowSecond"
            value:
              type: ":integer"
              value: "-9223372036854775807"
          - name: "lowContainsMin"
            value:
              type: ":boolean"
              value: true
```

---

## Test: ranges iterate and generated collections materialize

This runtime case exercises “ranges iterate and generated collections materialize” and verifies the declared messages, values, and execution result.

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
  - name: "ranges iterate and generated collections materialize.ges"
    program: main
```

### Source code under test

```ges
on Start(values) {
  let fullRange as :range be from 1 to 3
  let odds as :range be from 1 to 5 step 2
  let descending be from 5 to 1 step (0 - 2)
  let zeroStep be from 1 to 5 step 0
  let squares be :list[:select item from 1 to 5 => item * item]
  let filteredSquares be :list[:select item from 1 to 6 where item mod 2 = 0 => item * item]
  let tags be :list[:select item from 1 to 4 where item >= 2 => item mod 2]
  let doubled be :list[:select item in values => item * 2]
  let filtered be :list[:select item in values where item > 3 => item mod 2]
  for item in fullRange emit Full(value: item)
  for item from 1 to 5 step 2 emit Direct(value: item)
  for item in odds emit Indirect(value: item)
  for item in zeroStep emit Zero(value: item)
  emit Done(isRange: odds is :range, descendingFirst: descending[1], descendingSecond: descending[2], descendingThird: descending[3], zeroLen: zeroStep[:count], square_1: squares[1], square_5: squares[5], filtered_1: filteredSquares[1], filtered_3: filteredSquares[3], tagCount: tags[:count], tagsHasZero: 0 in tags, tagsHasOne: 1 in tags, doubledFirst: doubled[1], doubledSecond: doubled[2], filteredLen: filtered[:count], hasZero: 0 in filtered, hasOne: 1 in filtered)
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
        - name: "values"
          value:
            type: ":list"
            items:
              - type: ":integer"
                value: "2"
              - type: ":integer"
                value: "4"
              - type: ":integer"
                value: "5"
    local:
      - name: "Full"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "1"
      - name: "Full"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "2"
      - name: "Full"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "3"
      - name: "Direct"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "1"
      - name: "Direct"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "3"
      - name: "Direct"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "5"
      - name: "Indirect"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "1"
      - name: "Indirect"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "3"
      - name: "Indirect"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "5"
      - name: "Done"
        args:
          - name: "isRange"
            value:
              type: ":boolean"
              value: true
          - name: "descendingFirst"
            value:
              type: ":integer"
              value: "5"
          - name: "descendingSecond"
            value:
              type: ":integer"
              value: "3"
          - name: "descendingThird"
            value:
              type: ":integer"
              value: "1"
          - name: "zeroLen"
            value:
              type: ":integer"
              value: "0"
          - name: "square_1"
            value:
              type: ":integer"
              value: "1"
          - name: "square_5"
            value:
              type: ":integer"
              value: "25"
          - name: "filtered_1"
            value:
              type: ":integer"
              value: "4"
          - name: "filtered_3"
            value:
              type: ":integer"
              value: "36"
          - name: "tagCount"
            value:
              type: ":integer"
              value: "3"
          - name: "tagsHasZero"
            value:
              type: ":boolean"
              value: true
          - name: "tagsHasOne"
            value:
              type: ":boolean"
              value: true
          - name: "doubledFirst"
            value:
              type: ":integer"
              value: "4"
          - name: "doubledSecond"
            value:
              type: ":integer"
              value: "8"
          - name: "filteredLen"
            value:
              type: ":integer"
              value: "2"
          - name: "hasZero"
            value:
              type: ":boolean"
              value: true
          - name: "hasOne"
            value:
              type: ":boolean"
              value: true
```

---

## Test: range direct lookup is not capped by materialization limit

This runtime case exercises “range direct lookup is not capped by materialization limit” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0012
kind: scriptApi
level: scenario
runtimeLimits:
  maxRangeItems: 5
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "range direct lookup is not capped by materialization limit.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let values as :range be from 1 to 1000000
  emit Done(value: values[1000000], contains: 999999 in values, fractional: 1.5 in values)
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
              type: ":integer"
              value: "1000000"
          - name: "contains"
            value:
              type: ":boolean"
              value: true
          - name: "fractional"
            value:
              type: ":boolean"
              value: false
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: range limit observer event

This runtime case exercises “range limit observer event” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0013
kind: scriptApi
level: scenario
runtimeLimits:
  maxRangeItems: 5
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "range limit observer event.ges"
    program: main
```

### Source code under test

```ges
on Start {
  for item from 1 to 10 emit Tick(value: item)
  let values as :list be from 1 to 10
  emit Done(count: values[:count])
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
              type: ":integer"
              value: "0"
    runtimeLimits:
      include:
        - name: "MaxRangeItems"
```

---

## Test: loop budget stops run after published iterations

This runtime case exercises “loop budget stops run after published iterations” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0014
kind: scriptApi
level: scenario
runtimeLimits:
  maxLoopIterations: 3
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "loop budget stops run after published iterations.ges"
    program: main
```

### Source code under test

```ges
on Start {
  for item from 1 to 10 emit Tick(value: item)
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
      - name: "Tick"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "1"
      - name: "Tick"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "2"
      - name: "Tick"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "3"
    runtimeLimits:
      include:
        - name: "MaxLoopIterations"
```

---

## Test: dice limit observer event

This runtime case exercises “dice limit observer event” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0015
kind: scriptApi
level: scenario
runtimeLimits:
  maxDiceCount: 4
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "dice limit observer event.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let diceRoll be roll dice 6d6
  emit Done(count: diceRoll[:count])
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
              type: ":integer"
              value: "0"
    runtimeLimits:
      include:
        - name: "MaxDiceCount"
```

---

## Test: dice slicing and sorting semantics

This runtime case exercises “dice slicing and sorting semantics” and verifies the declared messages, values, and execution result.

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
random:
  sequence: ["2", "6", "3", "5", "1", "4", "6", "2", "3", "2", "6", "1", "1", "2", "4", "6", "5", "3", "1", "4", "2"]
sources:
  - name: "dice slicing and sorting semantics.ges"
    program: main
```

### Source code under test

```ges
on Roll {
  let baseDice be roll dice 4d6
  let kept be roll dice 4d6[:take highest 2]
  let dropped be roll dice 4d6[:drop lowest 1]
  let resorted be baseDice[:sort ascending]
  let emptyDrop be roll dice 2d6[:drop lowest 3]
  let takeTooMany be roll dice 4d6[:take highest 5]
  let diceTotal be roll dice 2d6 + 1
  emit Done(baseDice: baseDice, baseLen: baseDice[:count], base_1: baseDice[1], base_2: baseDice[2], base_3: baseDice[3], base_4: baseDice[4], kept: kept, dropped: dropped, resorted: resorted, emptyDropLen: emptyDrop[:count], takeTooManyLen: takeTooMany[:count], diceTotal: diceTotal)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Roll | completion |  |

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
          - name: "baseDice"
            value:
              type: ":dice"
              rolls:
                - 6
                - 5
                - 3
                - 2
          - name: "baseLen"
            value:
              type: ":integer"
              value: "4"
          - name: "base_1"
            value:
              type: ":integer"
              value: "6"
          - name: "base_2"
            value:
              type: ":integer"
              value: "5"
          - name: "base_3"
            value:
              type: ":integer"
              value: "3"
          - name: "base_4"
            value:
              type: ":integer"
              value: "2"
          - name: "kept"
            value:
              type: ":dice"
              rolls:
                - 6
                - 4
          - name: "dropped"
            value:
              type: ":dice"
              rolls:
                - 6
                - 3
                - 2
          - name: "resorted"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "2"
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "5"
                - type: ":integer"
                  value: "6"
          - name: "emptyDropLen"
            value:
              type: ":integer"
              value: "0"
          - name: "takeTooManyLen"
            value:
              type: ":integer"
              value: "4"
          - name: "diceTotal"
            value:
              type: ":dice"
              rolls:
                - 4
                - 1
                - 1
```

---

## Test: dice and list pattern checks

This runtime case exercises “dice and list pattern checks” and verifies the declared messages, values, and execution result.

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
  - name: "dice and list pattern checks.ges"
    program: main
```

### Source code under test

```ges
on Start(pairRoll, tripleRoll, fullHouseRoll, straightRoll, sixRoll, sevenRoll, cards, orderedCards, plainText) {
  let pairTaken be fullHouseRoll[:take pair]
  let fullHouseTaken be fullHouseRoll[:take full house]
  let straightCards be orderedCards[:take straight]
  let missingPair be orderedCards[:take pair] default ['fallback']
  emit Done(hasPair: pairRoll[:has pair], hasPairOfSix: pairRoll[:has pair of 6], pairHasThree: pairRoll[:has three of a kind], tripleHasThree: tripleRoll[:has three of a kind], tripleHasThreeSix: tripleRoll[:has three of 6], hasSixKind: sixRoll[:has six of a kind], hasSevenSix: sevenRoll[:has seven of 6], hasFullHouse: fullHouseRoll[:has full house], hasStraight: straightRoll[:has straight], listPair: cards[:has pair], listKingTriple: cards[:has three of 'King'], listFullHouse: cards[:has full house], orderedStraight: orderedCards[:has straight], textPair: plainText[:has pair], pairTakeLen: pairTaken[:count], fullHouseFirst: fullHouseTaken[1], straightFirst: straightCards[1], missingPairFirst: missingPair[1])
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
        - name: "pairRoll"
          value:
            type: ":dice"
            rolls:
              - 6
              - 6
              - 4
              - 3
              - 2
        - name: "tripleRoll"
          value:
            type: ":dice"
            rolls:
              - 6
              - 6
              - 6
              - 4
              - 3
        - name: "fullHouseRoll"
          value:
            type: ":dice"
            rolls:
              - 5
              - 5
              - 5
              - 2
              - 2
        - name: "straightRoll"
          value:
            type: ":dice"
            rolls:
              - 6
              - 6
              - 5
              - 4
              - 3
              - 2
        - name: "sixRoll"
          value:
            type: ":dice"
            rolls:
              - 4
              - 4
              - 4
              - 4
              - 4
              - 4
        - name: "sevenRoll"
          value:
            type: ":dice"
            rolls:
              - 6
              - 6
              - 6
              - 6
              - 6
              - 6
              - 6
        - name: "cards"
          value:
            type: ":list"
            items:
              - type: ":text"
                value: "King"
              - type: ":text"
                value: "King"
              - type: ":text"
                value: "King"
              - type: ":text"
                value: "Queen"
              - type: ":text"
                value: "Queen"
        - name: "orderedCards"
          value:
            type: ":list"
            items:
              - type: ":integer"
                value: "1"
              - type: ":integer"
                value: "2"
              - type: ":integer"
                value: "3"
              - type: ":integer"
                value: "4"
              - type: ":integer"
                value: "5"
        - name: "plainText"
          value:
            type: ":text"
            value: "Hello"
    local:
      - name: "Done"
        args:
          - name: "hasPair"
            value:
              type: ":boolean"
              value: true
          - name: "hasPairOfSix"
            value:
              type: ":boolean"
              value: true
          - name: "pairHasThree"
            value:
              type: ":boolean"
              value: false
          - name: "tripleHasThree"
            value:
              type: ":boolean"
              value: true
          - name: "tripleHasThreeSix"
            value:
              type: ":boolean"
              value: true
          - name: "hasSixKind"
            value:
              type: ":boolean"
              value: true
          - name: "hasSevenSix"
            value:
              type: ":boolean"
              value: true
          - name: "hasFullHouse"
            value:
              type: ":boolean"
              value: true
          - name: "hasStraight"
            value:
              type: ":boolean"
              value: true
          - name: "listPair"
            value:
              type: ":boolean"
              value: true
          - name: "listKingTriple"
            value:
              type: ":boolean"
              value: true
          - name: "listFullHouse"
            value:
              type: ":boolean"
              value: true
          - name: "orderedStraight"
            value:
              type: ":boolean"
              value: true
          - name: "textPair"
            value:
              type: ":boolean"
              value: false
          - name: "pairTakeLen"
            value:
              type: ":integer"
              value: "2"
          - name: "fullHouseFirst"
            value:
              type: ":integer"
              value: "5"
          - name: "straightFirst"
            value:
              type: ":integer"
              value: "1"
          - name: "missingPairFirst"
            value:
              type: ":text"
              value: "fallback"
```
