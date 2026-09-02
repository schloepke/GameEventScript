---
formatVersion: 1
suiteId: "runtime.atomic.series"
title: "RuntimeAtomicSeries"
categories: [conformance]
tags: [migrated-json-v1]
---

# RuntimeAtomicSeries

Mechanically migrated from the former JSON conformance corpus.

## Test: term is series only

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
  - name: "term is series only.ges"
    program: main
```

```ges
module AtomicSeriesTerm
on Start {
  let naturals be from 0 to 9
  let odd be from 1 to 9 step 2
  let fib be series fibonacci
  let fact be series factorial
  let values be [1, 2, 3]
  let dice as :dice be [6, 4, 2]
  let range as :range be from 1 to 3
  emit Done(naturalZero: naturals[1], naturalThree: naturals[4], oddFour: odd[5], fibSeven: fib[:term 7], factFive: fact[:term 5], invalidText: fib[:term 'x'], listTerm: values[:term 0], diceTerm: dice[:term 0], rangeTerm: range[:term 0])
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
          - name: "naturalZero"
            value:
              type: ":integer"
              value: "0"
          - name: "naturalThree"
            value:
              type: ":integer"
              value: "3"
          - name: "oddFour"
            value:
              type: ":integer"
              value: "9"
          - name: "fibSeven"
            value:
              type: ":integer"
              value: "13"
          - name: "factFive"
            value:
              type: ":integer"
              value: "120"
          - name: "invalidText"
            value:
              type: ":nothing"
          - name: "listTerm"
            value:
              type: ":nothing"
          - name: "diceTerm"
            value:
              type: ":nothing"
          - name: "rangeTerm"
            value:
              type: ":nothing"
```

## Test: take first direct sequence values

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
  - name: "take first direct sequence values.ges"
    program: main
```

```ges
module AtomicSeriesTakeFirst
on Start {
  let naturals be from 0 to 9
  let odd be from 1 to 9 step 2
  let values be [1, 2, 3, 4]
  let dice as :dice be [6, 5, 3, 1]
  let range as :range be from 1 to 5
  let floatRange as :range be from 1.5 to 3.5 step 0.5
  let integerValue be 10
  emit Done(firstNaturals: naturals[:take first 4], firstOdd: odd[:take first 3], listFirst: values[:take first 2], diceFirst: dice[:take first 2], rangeFirst: range[:take first 3], floatRangeFirst: floatRange[:take first 2], invalid: integerValue[:take first 2])
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
          - name: "listFirst"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
          - name: "diceFirst"
            value:
              type: ":dice"
              rolls:
                - 6
                - 5
          - name: "rangeFirst"
            value:
              type: ":range"
              from: "1"
              to: "3"
              step: "1"
          - name: "floatRangeFirst"
            value:
              type: ":range"
              from: "1.5"
              to: "2"
              step: "0.5"
          - name: "invalid"
            value:
              type: ":nothing"
```

## Test: drop first direct sequence values

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
  - name: "drop first direct sequence values.ges"
    program: main
```

```ges
module AtomicSeriesDropFirst
on Start {
  let fib be series fibonacci
  let dropped be fib[:drop first 5]
  let values be [1, 2, 3, 4]
  let dice as :dice be [6, 5, 3, 1]
  let range as :range be from 1 to 5
  let integerValue be 10
  emit Done(droppedIsSeries: dropped is :series, droppedZero: dropped[:term 0], droppedTwo: dropped[:term 2], listDrop: values[:drop first 2], diceDrop: dice[:drop first 2], rangeDrop: range[:drop first 2], invalid: integerValue[:drop first 2])
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
          - name: "droppedIsSeries"
            value:
              type: ":boolean"
              value: true
          - name: "droppedZero"
            value:
              type: ":integer"
              value: "5"
          - name: "droppedTwo"
            value:
              type: ":integer"
              value: "13"
          - name: "listDrop"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "4"
          - name: "diceDrop"
            value:
              type: ":dice"
              rolls:
                - 3
                - 1
          - name: "rangeDrop"
            value:
              type: ":range"
              from: "3"
              to: "5"
              step: "1"
          - name: "invalid"
            value:
              type: ":nothing"
```

## Test: take last direct finite sequence values

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
  - name: "take last direct finite sequence values.ges"
    program: main
```

```ges
module AtomicSeriesTakeLast
on Start {
  let fib be series fibonacci
  let values be [1, 2, 3, 4]
  let dice as :dice be [6, 5, 3, 1]
  let range as :range be from 1 to 5
  let descending as :range be from 5 to 1 step (0 - 2)
  emit Done(seriesLast: fib[:take last 2], listLast: values[:take last 2], diceLast: dice[:take last 2], rangeLast: range[:take last 2], descendingLast: descending[:take last 2])
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
          - name: "seriesLast"
            value:
              type: ":nothing"
          - name: "listLast"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "4"
          - name: "diceLast"
            value:
              type: ":dice"
              rolls:
                - 3
                - 1
          - name: "rangeLast"
            value:
              type: ":range"
              from: "4"
              to: "5"
              step: "1"
          - name: "descendingLast"
            value:
              type: ":range"
              from: "3"
              to: "1"
              step: "-2"
```

## Test: drop last direct finite sequence values

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
  - name: "drop last direct finite sequence values.ges"
    program: main
```

```ges
module AtomicSeriesDropLast
on Start {
  let fib be series fibonacci
  let values be [1, 2, 3, 4]
  let dice as :dice be [6, 5, 3, 1]
  let range as :range be from 1 to 5
  let descending as :range be from 5 to 1 step (0 - 2)
  emit Done(seriesDropLast: fib[:drop last 2], listDropLast: values[:drop last 2], diceDropLast: dice[:drop last 2], rangeDropLast: range[:drop last 2], descendingDropLast: descending[:drop last 1])
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
          - name: "seriesDropLast"
            value:
              type: ":nothing"
          - name: "listDropLast"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "1"
                - type: ":integer"
                  value: "2"
          - name: "diceDropLast"
            value:
              type: ":dice"
              rolls:
                - 6
                - 5
          - name: "rangeDropLast"
            value:
              type: ":range"
              from: "1"
              to: "3"
              step: "1"
          - name: "descendingDropLast"
            value:
              type: ":range"
              from: "5"
              to: "3"
              step: "-2"
```
