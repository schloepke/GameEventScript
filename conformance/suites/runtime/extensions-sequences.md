---
formatVersion: 1
suiteId: "runtime.extensions-sequences"
title: "RuntimeExtensionsSequences"
categories: [conformance]
---

# RuntimeExtensionsSequences

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies portable extension calls together with sequence and pipeline execution.

---

## Test: extensions support unary variadic labeled and predicate calls

This runtime case exercises “extensions support unary variadic labeled and predicate calls” and verifies the declared messages, values, and execution result.

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
  - name: "extensions support unary variadic labeled and predicate calls.ges"
    program: main
```

### Source code under test

```ges
on Start(value, heading, target) {
  let values be [10, 20, 30]
  let floored be floor value
  let flooredCall be floor(value)
  let floorPredicate be nothing
  let maxed be max of 2 and 8 and 5
  let turnA be :nav.shortestTurn from: heading to: target
  let turnB be :nav.shortestTurn(from: heading, to: target)
  let northA be heading is :nav.isNorth
  let northB be :nav.isNorth heading
  let vectorTotal be :test.vectorSum :Vector(1m, 2m, 3m)
  let asList be ([10, 20, 30]) as :List
  emit Done(floored: floored, flooredCall: flooredCall, floorPredicate: floorPredicate, maxed: maxed, turnA: turnA, turnB: turnB, northA: northA, northB: northB, vectorTotal: vectorTotal, valuesIsList: values is :List, valuesLen: values[:count], list2: asList[2])
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
              type: ":Number.int64"
              value: "10"
          - name: "flooredCall"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "floorPredicate"
            value:
              type: ":Nothing"
          - name: "maxed"
            value:
              type: ":Number.int64"
              value: "8"
          - name: "turnA"
            value:
              type: ":Quantity.int64"
              value: "20"
              unit: ":degree"
          - name: "turnB"
            value:
              type: ":Quantity.int64"
              value: "20"
              unit: ":degree"
          - name: "northA"
            value:
              type: ":Boolean"
              value: true
          - name: "northB"
            value:
              type: ":Boolean"
              value: true
          - name: "vectorTotal"
            value:
              type: ":Quantity.int64"
              value: "6"
              unit: ":meter"
          - name: "valuesIsList"
            value:
              type: ":Boolean"
              value: true
          - name: "valuesLen"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "list2"
            value:
              type: ":Number.int64"
              value: "20"
```

---

## Test: built-in series support term take drop and first term coercion

This runtime case exercises “built-in series support term take drop and first term coercion” and verifies the declared messages, values, and execution result.

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
  - name: "built-in series support term take drop and first term coercion.ges"
    program: main
```

### Source code under test

```ges
on Start() {
  let fib be series fibonacci
  let fact be series factorial
  let naturals be from 0 to 9
  let odd be from 1 to 9 step 2
  let dropped be fib[:drop first 5]
  emit Done(fibIsSeries: fib is :Series, droppedIsSeries: dropped is :Series, noLookup: fib[1], seriesLen: fib[:count], badTerm: fib[:term 'x'], takeLast: fib[:take last 2], dropLast: fib[:drop last 1], filterNothing: fib[:filter item where true], bareFirst: fib as :Number, naturalZero: naturals[1], naturalThree: naturals[4], oddFourth: odd[5], fibZero: fib[:term 0], fibOne: fib[:term 1], fibSeven: fib[:term 7], droppedZero: dropped[:term 0], factZero: fact[:term 0], factFive: fact[:term 5], firstNaturals: naturals[:take first 4], firstOdd: odd[:take first 3])
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
    local:
      - name: "Done"
        args:
          - name: "fibIsSeries"
            value:
              type: ":Boolean"
              value: true
          - name: "droppedIsSeries"
            value:
              type: ":Boolean"
              value: true
          - name: "noLookup"
            value:
              type: ":Nothing"
          - name: "seriesLen"
            value:
              type: ":Nothing"
          - name: "badTerm"
            value:
              type: ":Nothing"
          - name: "takeLast"
            value:
              type: ":Nothing"
          - name: "dropLast"
            value:
              type: ":Nothing"
          - name: "filterNothing"
            value:
              type: ":Nothing"
          - name: "bareFirst"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "naturalZero"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "naturalThree"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "oddFourth"
            value:
              type: ":Number.int64"
              value: "9"
          - name: "fibZero"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "fibOne"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "fibSeven"
            value:
              type: ":Number.int64"
              value: "13"
          - name: "droppedZero"
            value:
              type: ":Number.int64"
              value: "5"
          - name: "factZero"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "factFive"
            value:
              type: ":Number.int64"
              value: "120"
          - name: "firstNaturals"
            value:
              type: ":Range.int64"
              from: "0"
              to: "3"
              step: "1"
          - name: "firstOdd"
            value:
              type: ":Range.int64"
              from: "1"
              to: "5"
              step: "2"
```

---

## Test: r25-extension-short-form-parse-number

This case accepts parse as a unary extension argument and agrees with the parenthesized call form.

### Case description

```yaml
gesBlock: case
id: r25-extension-short-form-parse-number
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) { emit Done(short: :test.echo parse value, parenthesized: :test.echo(parse value)) }
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
            value: "42"
    local:
      - name: Done
        args:
          - name: "short"
            value:
              type: ":Number.int64"
              value: "42"
          - name: "parenthesized"
            value:
              type: ":Number.int64"
              value: "42"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: r25-extension-short-form-parse-text

This case accepts parse as a unary extension argument and agrees with the parenthesized call form.

### Case description

```yaml
gesBlock: case
id: r25-extension-short-form-parse-text
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) { emit Done(short: :test.echo parse value, parenthesized: :test.echo(parse value)) }
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
            value: "Hello"
    local:
      - name: Done
        args:
          - name: "short"
            value:
              type: ":Text"
              value: "Hello"
          - name: "parenthesized"
            value:
              type: ":Text"
              value: "Hello"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: r25-extension-parse-precedence

This case keeps parse inside the unary extension argument and addition outside that argument.

### Case description

```yaml
gesBlock: case
id: r25-extension-parse-precedence
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) { emit Done(value: :math.floor parse value + 1.5) }
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
            value: "2.7"
    local:
      - name: Done
        args:
          - name: "value"
            value:
              type: ":Number.binary64"
              value: "3.5"
    runtimeLimits:
      exclude:
        - any: true
```
