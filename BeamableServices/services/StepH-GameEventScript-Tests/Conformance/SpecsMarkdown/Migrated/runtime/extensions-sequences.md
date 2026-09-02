---
formatVersion: 1
suiteId: "runtime.extensions-sequences"
title: "RuntimeExtensionsSequences"
categories: [conformance]
tags: [migrated-json-v1]
---

# RuntimeExtensionsSequences

Mechanically migrated from the former JSON conformance corpus.

## Test: extensions support unary variadic labeled and predicate calls

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

```ges
on Start(value, heading, target) {
  let values be of 10 and 20 and 30
  let floored be floor value
  let flooredCall be floor(value)
  let floorPredicate be nothing
  let maxed be max of 2 and 8 and 5
  let turnA be :nav.shortestTurn from: heading to: target
  let turnB be :nav.shortestTurn(from: heading, to: target)
  let northA be heading is :nav.isNorth
  let northB be :nav.isNorth heading
  let vectorTotal be :test.vectorSum :vector(1m, 2m, 3m)
  let asList as :list be of 10 and 20 and 30
  emit Done(floored: floored, flooredCall: flooredCall, floorPredicate: floorPredicate, maxed: maxed, turnA: turnA, turnB: turnB, northA: northA, northB: northB, vectorTotal: vectorTotal, valuesIsList: values is :list, valuesLen: values[:count], list_2: asList[2])
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
          - name: "floorPredicate"
            value:
              type: ":nothing"
          - name: "maxed"
            value:
              type: ":float"
              value: "8"
          - name: "turnA"
            value:
              type: ":float"
              value: "20"
              unit: ":degree"
          - name: "turnB"
            value:
              type: ":float"
              value: "20"
              unit: ":degree"
          - name: "northA"
            value:
              type: ":boolean"
              value: true
          - name: "northB"
            value:
              type: ":boolean"
              value: true
          - name: "vectorTotal"
            value:
              type: ":float"
              value: "6"
              unit: ":meter"
          - name: "valuesIsList"
            value:
              type: ":boolean"
              value: true
          - name: "valuesLen"
            value:
              type: ":integer"
              value: "3"
          - name: "list_2"
            value:
              type: ":integer"
              value: "20"
```

## Test: built-in series support term take drop and first term coercion

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

```ges
on Start() {
  let fib be series fibonacci
  let fact be series factorial
  let naturals be from 0 to 9
  let odd be from 1 to 9 step 2
  let dropped be fib[:drop first 5]
  emit Done(fibIsSeries: fib is :series, droppedIsSeries: dropped is :series, noLookup: fib[1], seriesLen: fib[:count], badTerm: fib[:term 'x'], takeLast: fib[:take last 2], dropLast: fib[:drop last 1], filterNothing: fib[:filter item where true], bareFirst: fib as :number, naturalZero: naturals[1], naturalThree: naturals[4], oddFourth: odd[5], fibZero: fib[:term 0], fibOne: fib[:term 1], fibSeven: fib[:term 7], droppedZero: dropped[:term 0], factZero: fact[:term 0], factFive: fact[:term 5], firstNaturals: naturals[:take first 4], firstOdd: odd[:take first 3])
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
    local:
      - name: "Done"
        args:
          - name: "fibIsSeries"
            value:
              type: ":boolean"
              value: true
          - name: "droppedIsSeries"
            value:
              type: ":boolean"
              value: true
          - name: "noLookup"
            value:
              type: ":nothing"
          - name: "seriesLen"
            value:
              type: ":nothing"
          - name: "badTerm"
            value:
              type: ":nothing"
          - name: "takeLast"
            value:
              type: ":nothing"
          - name: "dropLast"
            value:
              type: ":nothing"
          - name: "filterNothing"
            value:
              type: ":nothing"
          - name: "bareFirst"
            value:
              type: ":integer"
              value: "0"
          - name: "naturalZero"
            value:
              type: ":integer"
              value: "0"
          - name: "naturalThree"
            value:
              type: ":integer"
              value: "3"
          - name: "oddFourth"
            value:
              type: ":integer"
              value: "9"
          - name: "fibZero"
            value:
              type: ":integer"
              value: "0"
          - name: "fibOne"
            value:
              type: ":integer"
              value: "1"
          - name: "fibSeven"
            value:
              type: ":integer"
              value: "13"
          - name: "droppedZero"
            value:
              type: ":integer"
              value: "5"
          - name: "factZero"
            value:
              type: ":integer"
              value: "1"
          - name: "factFive"
            value:
              type: ":integer"
              value: "120"
          - name: "firstNaturals"
            value:
              type: ":range"
              from: "0"
              to: "3"
              step: "1"
          - name: "firstOdd"
            value:
              type: ":range"
              from: "1"
              to: "5"
              step: "2"
```
