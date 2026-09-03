---
formatVersion: 1
suiteId: "runtime.predicates-functions"
title: "RuntimePredicatesFunctions"
categories: [conformance]
---

# RuntimePredicatesFunctions

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies function and predicate calls, cross-source definitions, frames, and returned values.

---

## Test: predicates and functions are reusable in expressions and collections

This runtime case exercises “predicates and functions are reusable in expressions and collections” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "predicates and functions are reusable in expressions and collections.ges"
    program: main
```

### Source code under test

```ges
predicate wounded(_ unit) be unit.hp < unit.maxHp
function woundedUnits(_ units) be units[:filter unit where unit is wounded]

on Start(unit, units) {
  let byCall be wounded(unit)
  let byPredicate be unit is wounded
  let woundedList be woundedUnits(units)
  emit Done(byCall: byCall, byPredicate: byPredicate, woundedCount: woundedList[:count], first: woundedList[1].name, second: woundedList[2].name)
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
            type: ":map"
            entries:
              - key: "name"
                value:
                  type: ":text"
                  value: "Ada"
              - key: "hp"
                value:
                  type: ":integer"
                  value: "2"
              - key: "maxHp"
                value:
                  type: ":integer"
                  value: "5"
        - name: "units"
          value:
            type: ":list"
            items:
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "Ada"
                  - key: "hp"
                    value:
                      type: ":integer"
                      value: "2"
                  - key: "maxHp"
                    value:
                      type: ":integer"
                      value: "5"
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "Bert"
                  - key: "hp"
                    value:
                      type: ":integer"
                      value: "4"
                  - key: "maxHp"
                    value:
                      type: ":integer"
                      value: "4"
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "Cara"
                  - key: "hp"
                    value:
                      type: ":integer"
                      value: "1"
                  - key: "maxHp"
                    value:
                      type: ":integer"
                      value: "3"
    local:
      - name: "Done"
        args:
          - name: "byCall"
            value:
              type: ":boolean"
              value: true
          - name: "byPredicate"
            value:
              type: ":boolean"
              value: true
          - name: "woundedCount"
            value:
              type: ":integer"
              value: "2"
          - name: "first"
            value:
              type: ":text"
              value: "Ada"
          - name: "second"
            value:
              type: ":text"
              value: "Cara"
```

---

## Test: predicates and functions are lenient when expected keys are missing

This runtime case exercises “predicates and functions are lenient when expected keys are missing” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "predicates and functions are lenient when expected keys are missing.ges"
    program: main
```

### Source code under test

```ges
predicate wounded(_ unit) be unit.hp < unit.maxHp
function woundedUnits(_ units) be units[:filter unit where unit is wounded]

on Start(unit, units) {
  let byCall be wounded(unit)
  let byPredicate be unit is wounded
  let woundedList be woundedUnits(units)
  emit Done(byCall: byCall, byPredicate: byPredicate, woundedCount: woundedList[:count])
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
            type: ":map"
            entries:
              - key: "name"
                value:
                  type: ":text"
                  value: "NoHp"
        - name: "units"
          value:
            type: ":list"
            items:
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "NoHp"
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "Healthy"
                  - key: "hp"
                    value:
                      type: ":integer"
                      value: "5"
                  - key: "maxHp"
                    value:
                      type: ":integer"
                      value: "5"
    local:
      - name: "Done"
        args:
          - name: "byCall"
            value:
              type: ":nothing"
          - name: "byPredicate"
            value:
              type: ":nothing"
          - name: "woundedCount"
            value:
              type: ":integer"
              value: "0"
```

---

## Test: is operations support inline negation

This runtime case exercises “is operations support inline negation” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "is operations support inline negation.ges"
    program: main
```

### Source code under test

```ges
predicate high(_ value) be value > 3

on Start(value) {
  emit Done(byWord: value is not high, byBang: value is !high, byType: value is not :text, byEmpty: value is not empty, byCompare: value is not 3 or less)
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
            type: ":integer"
            value: "2"
    local:
      - name: "Done"
        args:
          - name: "byWord"
            value:
              type: ":boolean"
              value: true
          - name: "byBang"
            value:
              type: ":boolean"
              value: true
          - name: "byType"
            value:
              type: ":boolean"
              value: true
          - name: "byEmpty"
            value:
              type: ":boolean"
              value: true
          - name: "byCompare"
            value:
              type: ":boolean"
              value: false
```

---

## Test: comparison phrases cover strict and inclusive bounds

This runtime case exercises “comparison phrases cover strict and inclusive bounds” and verifies the declared messages, values, and execution result.

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
  - name: "comparison phrases cover strict and inclusive bounds.ges"
    program: main
```

### Source code under test

```ges
on Start(value) {
  emit Done(lessThan: value is less than 10, moreThan: value is more than 10, orLess: value is 9 or less, orMore: value is 9 or more, atMost: value is at most 9, atLeast: value is at least 9)
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
            type: ":integer"
            value: "9"
    local:
      - name: "Done"
        args:
          - name: "lessThan"
            value:
              type: ":boolean"
              value: true
          - name: "moreThan"
            value:
              type: ":boolean"
              value: false
          - name: "orLess"
            value:
              type: ":boolean"
              value: true
          - name: "orMore"
            value:
              type: ":boolean"
              value: true
          - name: "atMost"
            value:
              type: ":boolean"
              value: true
          - name: "atLeast"
            value:
              type: ":boolean"
              value: true
```

---

## Test: predicates accept typed boolean record fields

This runtime case exercises “predicates accept typed boolean record fields” and verifies the declared messages, values, and execution result.

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
  - name: "predicates accept typed boolean record fields.ges"
    program: main
```

### Source code under test

```ges
record :unit as {
  alive: :boolean,
  hidden: :boolean
}

predicate targetable(_ unit as :unit) be unit.alive and not unit.hidden

on Start(unit) {
  emit Done(targetable: unit is targetable)
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
            type: ":map"
            entries:
              - key: "alive"
                value:
                  type: ":boolean"
                  value: true
              - key: "hidden"
                value:
                  type: ":boolean"
                  value: false
    local:
      - name: "Done"
        args:
          - name: "targetable"
            value:
              type: ":boolean"
              value: true
```

---

## Test: predicates allow explicit boolean coercion while functions keep original value

This runtime case exercises “predicates allow explicit boolean coercion while functions keep original value” and verifies the declared messages, values, and execution result.

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
  - name: "predicates allow explicit boolean coercion while functions keep original value.ges"
    program: main
```

### Source code under test

```ges
predicate numericPredicate(_ value) be value * 2 as :boolean
function numericSelect(_ value) be value * 2

on Start {
  let byPredicateCallTwo be numericPredicate(2)
  let byPredicateCallZero be numericPredicate(0)
  let byFunction be numericSelect(2)
  let byPredicateOperator be 2 is numericPredicate
  emit Done(byPredicateCallTwo: byPredicateCallTwo, byPredicateCallZero: byPredicateCallZero, byFunction: byFunction, byPredicateOperator: byPredicateOperator)
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
          - name: "byPredicateCallTwo"
            value:
              type: ":boolean"
              value: true
          - name: "byPredicateCallZero"
            value:
              type: ":boolean"
              value: false
          - name: "byFunction"
            value:
              type: ":integer"
              value: "4"
          - name: "byPredicateOperator"
            value:
              type: ":boolean"
              value: true
```

---

## Test: multi argument predicates can be called directly

This runtime case exercises “multi argument predicates can be called directly” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "multi argument predicates can be called directly.ges"
    program: main
```

### Source code under test

```ges
predicate higher(left, right) be left > right

on Start(left, right) {
  emit Done(ok: higher(left: left, right: right), reversed: higher(left: right, right: left))
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
        - name: "left"
          value:
            type: ":integer"
            value: "7"
        - name: "right"
          value:
            type: ":integer"
            value: "4"
    local:
      - name: "Done"
        args:
          - name: "ok"
            value:
              type: ":boolean"
              value: true
          - name: "reversed"
            value:
              type: ":boolean"
              value: false
```

---

## Test: typed handler callable and record parameters coerce inputs

This runtime case exercises “typed handler callable and record parameters coerce inputs” and verifies the declared messages, values, and execution result.

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
  - name: "typed handler callable and record parameters coerce inputs.ges"
    program: main
```

### Source code under test

```ges
record :gauge as {
  current: :number
}

predicate high(value as :number) be value > 2
function boosted(_ value as :number) be value + 1

on Start(value as :number, hp as :gauge) {
  emit Done(handlerValue: value + 1, ok: value is high, boosted: boosted(value), current: hp.current)
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
            type: ":float"
            value: "2"
        - name: "hp"
          value:
            type: ":map"
            entries:
              - key: "current"
                value:
                  type: ":float"
                  value: "4"
    local:
      - name: "Done"
        args:
          - name: "handlerValue"
            value:
              type: ":integer"
              value: "3"
          - name: "ok"
            value:
              type: ":boolean"
              value: false
          - name: "boosted"
            value:
              type: ":integer"
              value: "3"
          - name: "current"
            value:
              type: ":integer"
              value: "4"
```

---

## Test: multiple modules share predicate function and type definitions

This runtime case exercises “multiple modules share predicate function and type definitions” and verifies the declared messages, values, and execution result.

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
  - name: "shared.ges"
    program: main
  - name: "runtime.ges"
    program: main
```

### Source code under test

```ges
module Shared
record :gauge as {
  current: :number,
  maximum: :number
}

predicate wounded(_ unit) be unit.hp < unit.maxHp
function woundedUnits(_ units) be units[:filter unit where unit is wounded]
```

```ges
module Runtime
on Start(unit, units) {
  let hp as :gauge be [current: unit.hp, maximum: unit.maxHp]
  let anyWounded be wounded(unit)
  let candidates be woundedUnits(units)
  emit Done(anyWounded: anyWounded, candidateCount: candidates[:count], maximum: hp.maximum)
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
            type: ":map"
            entries:
              - key: "hp"
                value:
                  type: ":integer"
                  value: "2"
              - key: "maxHp"
                value:
                  type: ":integer"
                  value: "5"
        - name: "units"
          value:
            type: ":list"
            items:
              - type: ":map"
                entries:
                  - key: "hp"
                    value:
                      type: ":integer"
                      value: "2"
                  - key: "maxHp"
                    value:
                      type: ":integer"
                      value: "5"
              - type: ":map"
                entries:
                  - key: "hp"
                    value:
                      type: ":integer"
                      value: "5"
                  - key: "maxHp"
                    value:
                      type: ":integer"
                      value: "5"
    local:
      - name: "Done"
        args:
          - name: "anyWounded"
            value:
              type: ":boolean"
              value: true
          - name: "candidateCount"
            value:
              type: ":integer"
              value: "1"
          - name: "maximum"
            value:
              type: ":integer"
              value: "5"
```

---

## Test: callable overloads resolve by ordered argument labels

This runtime case verifies that functions with the same name and arity remain distinct when their ordered external argument labels differ, and that each call selects the matching signature without type-based dispatch.

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
  - name: "callable overloads resolve by ordered argument labels.ges"
    program: main
```

### Source code under test

```ges
module CallableSignatureOverloads

function takeDamage(unit, enemy) be unit + enemy
function takeDamage(unit, collision) be unit * collision

on Start {
  let fromEnemy be takeDamage(unit: 5, enemy: 3)
  let fromCollision be takeDamage(unit: 5, collision: 3)
  emit Done(fromEnemy: fromEnemy, fromCollision: fromCollision)
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
          - name: "fromEnemy"
            value:
              type: ":integer"
              value: "8"
          - name: "fromCollision"
            value:
              type: ":integer"
              value: "15"
```
