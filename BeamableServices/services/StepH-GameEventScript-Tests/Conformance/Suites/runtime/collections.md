---
formatVersion: 1
suiteId: "runtime.collections"
title: "RuntimeCollections"
categories: [conformance]
tags: [migrated-json-v1]
---

# RuntimeCollections

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies collection behavior across multi-step scenarios and interacting language features.

---

## Test: collections project filter aggregate and choose edges

This runtime case exercises “collections project filter aggregate and choose edges” and verifies the declared messages, values, and execution result.

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
  - name: "collections project filter aggregate and choose edges.ges"
    program: main
```

### Source code under test

```ges
on Compute(items) {
  let names be items[:select item => item.name]
  let positive be items[:filter item where item.points > 0]
  let total be items[:sum item => item.points]
  let positiveCount be items[:count item where item.points > 0]
  let averagePoints be items[:average item => item.points]
  let highestItem be items[:highest item => item.points]
  let lowestItem be items[:lowest item => item.points]
  emit Done(nameCount: names[:count], positiveCount: positive[:count], total: total, counted: positiveCount, average: averagePoints, highest: highestItem.name, lowest: lowestItem.name)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Compute | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "items"
          value:
            type: ":list"
            items:
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "a"
                  - key: "points"
                    value:
                      type: ":integer"
                      value: "3"
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "b"
                  - key: "points"
                    value:
                      type: ":integer"
                      value: "-1"
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "c"
                  - key: "points"
                    value:
                      type: ":integer"
                      value: "4"
    local:
      - name: "Done"
        args:
          - name: "nameCount"
            value:
              type: ":integer"
              value: "3"
          - name: "positiveCount"
            value:
              type: ":integer"
              value: "2"
          - name: "total"
            value:
              type: ":integer"
              value: "6"
          - name: "counted"
            value:
              type: ":integer"
              value: "2"
          - name: "average"
            value:
              type: ":integer"
              value: "2"
          - name: "highest"
            value:
              type: ":text"
              value: "c"
          - name: "lowest"
            value:
              type: ":text"
              value: "b"
```

---

## Test: streamable selector chains preserve observable results

This runtime case exercises “streamable selector chains preserve observable results” and verifies the declared messages, values, and execution result.

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
  - name: "streamable selector chains preserve observable results.ges"
    program: main
```

### Source code under test

```ges
on Start(values) {
  let sorted be values[:filter value where value > 1][:select value => value * -1][:sort ascending]
  emit Done(filterSelectSum: values[:filter value where value > 2][:select value => value * 3][:sum value => value], filterCount: values[:filter value where value mod 2 = 0][:count value where value > 2], firstSelected: values[:select value => value + 10][:first value where value > 12], lastSelected: values[:select value => value + 10][:last value where value < 15], singleSelected: values[:select value => value + 10][:single value where value = 13], sorted_1: sorted[1], sorted_2: sorted[2], sorted_3: sorted[3])
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
                value: "1"
              - type: ":integer"
                value: "2"
              - type: ":integer"
                value: "3"
              - type: ":integer"
                value: "4"
    local:
      - name: "Done"
        args:
          - name: "filterSelectSum"
            value:
              type: ":integer"
              value: "21"
          - name: "filterCount"
            value:
              type: ":integer"
              value: "1"
          - name: "firstSelected"
            value:
              type: ":integer"
              value: "13"
          - name: "lastSelected"
            value:
              type: ":integer"
              value: "14"
          - name: "singleSelected"
            value:
              type: ":integer"
              value: "13"
          - name: "sorted_1"
            value:
              type: ":integer"
              value: "-4"
          - name: "sorted_2"
            value:
              type: ":integer"
              value: "-3"
          - name: "sorted_3"
            value:
              type: ":integer"
              value: "-2"
```

---

## Test: selector variants are observable through runtime behavior

This runtime case exercises “selector variants are observable through runtime behavior” and verifies the declared messages, values, and execution result.

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
  - name: "selector variants are observable through runtime behavior.ges"
    program: main
```

### Source code under test

```ges
on Start(items) {
  let hasAny be items[:any item where item.points > 10]
  let allValid be items[:all item where item.points >= 0]
  let firstTwo be items[:take first 2]
  let withoutLast be items[:drop last 1]
  let firstAlive be items[:first item where item.alive]
  let lastAlive be items[:last item where item.alive]
  let singleBoss be items[:single item where item.role = #boss]
  let weakest be items[:min item => item.points]
  let strongest be items[:max item => item.points]
  let topItem be items[:highest item => item.points]
  let lowItem be items[:lowest item => item.points]
  emit Done(hasAny: hasAny, allValid: allValid, firstTwoCount: firstTwo[:count], withoutLastCount: withoutLast[:count], firstAlive: firstAlive.name, lastAlive: lastAlive.name, singleBoss: singleBoss.name, weakest: weakest.name, strongest: strongest.name, topItem: topItem.name, lowItem: lowItem.name)
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
        - name: "items"
          value:
            type: ":list"
            items:
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "A"
                  - key: "points"
                    value:
                      type: ":integer"
                      value: "3"
                  - key: "alive"
                    value:
                      type: ":boolean"
                      value: true
                  - key: "role"
                    value:
                      type: ":tag"
                      value: "tank"
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "B"
                  - key: "points"
                    value:
                      type: ":integer"
                      value: "12"
                  - key: "alive"
                    value:
                      type: ":boolean"
                      value: false
                  - key: "role"
                    value:
                      type: ":tag"
                      value: "boss"
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "C"
                  - key: "points"
                    value:
                      type: ":integer"
                      value: "7"
                  - key: "alive"
                    value:
                      type: ":boolean"
                      value: true
                  - key: "role"
                    value:
                      type: ":tag"
                      value: "scout"
    local:
      - name: "Done"
        args:
          - name: "hasAny"
            value:
              type: ":boolean"
              value: true
          - name: "allValid"
            value:
              type: ":boolean"
              value: true
          - name: "firstTwoCount"
            value:
              type: ":integer"
              value: "2"
          - name: "withoutLastCount"
            value:
              type: ":integer"
              value: "2"
          - name: "firstAlive"
            value:
              type: ":text"
              value: "A"
          - name: "lastAlive"
            value:
              type: ":text"
              value: "C"
          - name: "singleBoss"
            value:
              type: ":text"
              value: "B"
          - name: "weakest"
            value:
              type: ":text"
              value: "A"
          - name: "strongest"
            value:
              type: ":text"
              value: "B"
          - name: "topItem"
            value:
              type: ":text"
              value: "B"
          - name: "lowItem"
            value:
              type: ":text"
              value: "A"
```

---

## Test: streamable range selectors short circuit before range materialization

This runtime case exercises “streamable range selectors short circuit before range materialization” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0004
kind: scriptApi
level: scenario
runtimeLimits:
  maxRangeItems: 5
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "streamable range selectors short circuit before range materialization.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let values be from 1 to 100
  emit Done(anyEarly: values[:any item where item = 1], allEarlyFalse: values[:all item where item < 1], firstEarly: values[:first item where item = 1], firstSelected: values[:select item => item + 10][:first item where item = 11], containsLast: values[:contains 100])
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
          - name: "anyEarly"
            value:
              type: ":boolean"
              value: true
          - name: "allEarlyFalse"
            value:
              type: ":boolean"
              value: false
          - name: "firstEarly"
            value:
              type: ":integer"
              value: "1"
          - name: "firstSelected"
            value:
              type: ":integer"
              value: "11"
          - name: "containsLast"
            value:
              type: ":boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: any and all use true checks

This runtime case exercises “any and all use true checks” and verifies the declared messages, values, and execution result.

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
  - name: "any and all use true checks.ges"
    program: main
```

### Source code under test

```ges
on Start(items) {
  let anyKnownFalse be items[:any item where item.points > 10]
  let allKnownFalse be items[:all item where item.points > 10]
  let anyUnknown be items[:any item where item.missing > 10]
  let allUnknown be items[:all item where item.missing > 10]
  emit Done(anyKnownFalse: anyKnownFalse, allKnownFalse: allKnownFalse, anyUnknown: anyUnknown, allUnknown: allUnknown)
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
        - name: "items"
          value:
            type: ":list"
            items:
              - type: ":map"
                entries:
                  - key: "points"
                    value:
                      type: ":integer"
                      value: "3"
              - type: ":map"
                entries:
                  - key: "points"
                    value:
                      type: ":integer"
                      value: "7"
    local:
      - name: "Done"
        args:
          - name: "anyKnownFalse"
            value:
              type: ":boolean"
              value: false
          - name: "allKnownFalse"
            value:
              type: ":boolean"
              value: false
          - name: "anyUnknown"
            value:
              type: ":boolean"
              value: false
          - name: "allUnknown"
            value:
              type: ":boolean"
              value: false
```

---

## Test: selectors can be nested inside projections

This runtime case exercises “selectors can be nested inside projections” and verifies the declared messages, values, and execution result.

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
  - name: "selectors can be nested inside projections.ges"
    program: main
```

### Source code under test

```ges
on Nested(items) {
  let values be items[:select item => item.points[:all point where point.x > 0]]
  emit Done(first: values[1], second: values[2])
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Nested | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "items"
          value:
            type: ":list"
            items:
              - type: ":map"
                entries:
                  - key: "points"
                    value:
                      type: ":list"
                      items:
                        - type: ":map"
                          entries:
                            - key: "x"
                              value:
                                type: ":integer"
                                value: "1"
                        - type: ":map"
                          entries:
                            - key: "x"
                              value:
                                type: ":integer"
                                value: "2"
              - type: ":map"
                entries:
                  - key: "points"
                    value:
                      type: ":list"
                      items:
                        - type: ":map"
                          entries:
                            - key: "x"
                              value:
                                type: ":integer"
                                value: "1"
                        - type: ":map"
                          entries:
                            - key: "x"
                              value:
                                type: ":integer"
                                value: "0"
    local:
      - name: "Done"
        args:
          - name: "first"
            value:
              type: ":boolean"
              value: true
          - name: "second"
            value:
              type: ":boolean"
              value: false
```

---

## Test: dictionary selectors use last wins semantics

This runtime case exercises “dictionary selectors use last wins semantics” and verifies the declared messages, values, and execution result.

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
  - name: "dictionary selectors use last wins semantics.ges"
    program: main
```

### Source code under test

```ges
on Start(items) {
  let byId be items[:map item by item.id]
  let namesById be items[:map item by item.id => item.name]
  emit Done(rookHp: byId[#rook].hp, mageHp: byId[#mage].hp, rookName: namesById[#rook], mageName: namesById[#mage], count: byId[:count])
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
        - name: "items"
          value:
            type: ":list"
            items:
              - type: ":map"
                entries:
                  - key: "id"
                    value:
                      type: ":tag"
                      value: "rook"
                  - key: "name"
                    value:
                      type: ":text"
                      value: "Rook A"
                  - key: "hp"
                    value:
                      type: ":integer"
                      value: "4"
              - type: ":map"
                entries:
                  - key: "id"
                    value:
                      type: ":tag"
                      value: "mage"
                  - key: "name"
                    value:
                      type: ":text"
                      value: "Mage"
                  - key: "hp"
                    value:
                      type: ":integer"
                      value: "7"
              - type: ":map"
                entries:
                  - key: "id"
                    value:
                      type: ":tag"
                      value: "rook"
                  - key: "name"
                    value:
                      type: ":text"
                      value: "Rook B"
                  - key: "hp"
                    value:
                      type: ":integer"
                      value: "9"
    local:
      - name: "Done"
        args:
          - name: "rookHp"
            value:
              type: ":integer"
              value: "9"
          - name: "mageHp"
            value:
              type: ":integer"
              value: "7"
          - name: "rookName"
            value:
              type: ":text"
              value: "Rook B"
          - name: "mageName"
            value:
              type: ":text"
              value: "Mage"
          - name: "count"
            value:
              type: ":integer"
              value: "2"
```

---

## Test: collections sort order distinct and group

This runtime case exercises “collections sort order distinct and group” and verifies the declared messages, values, and execution result.

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
  - name: "collections sort order distinct and group.ges"
    program: main
```

### Source code under test

```ges
on Start(values, units) {
  let ascendingValues be values[:sort ascending]
  let descendingValues be values[:sort descending]
  let byPriority be units[:order by item => item.priority descending]
  let firstValue be values[:first]
  let lastValue be values[:last]
  let distinctValues be values[:distinct]
  let distinctFactions be units[:distinct by unit => unit.faction]
  let groups be units[:group by unit => unit.faction]
  emit Done(asc_1: ascendingValues[1], asc_2: ascendingValues[2], asc_3: ascendingValues[3], desc_1: descendingValues[1], desc_2: descendingValues[2], desc_3: descendingValues[3], priority_1: byPriority[1].name, priority_2: byPriority[2].name, priority_3: byPriority[3].name, first: firstValue, last: lastValue, distinctCount: distinctValues[:count], distinct_1: distinctFactions[1].name, distinct_2: distinctFactions[2].name, melee_1: groups[#melee][1].name, melee_2: groups[#melee][2].name, ranged_1: groups[#ranged][1].name)
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
                value: "3"
              - type: ":integer"
                value: "3"
              - type: ":integer"
                value: "1"
              - type: ":integer"
                value: "2"
              - type: ":integer"
                value: "2"
        - name: "units"
          value:
            type: ":list"
            items:
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "Knight"
                  - key: "faction"
                    value:
                      type: ":text"
                      value: "melee"
                  - key: "priority"
                    value:
                      type: ":integer"
                      value: "2"
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "Rook"
                  - key: "faction"
                    value:
                      type: ":text"
                      value: "melee"
                  - key: "priority"
                    value:
                      type: ":integer"
                      value: "3"
              - type: ":map"
                entries:
                  - key: "name"
                    value:
                      type: ":text"
                      value: "Archer"
                  - key: "faction"
                    value:
                      type: ":text"
                      value: "ranged"
                  - key: "priority"
                    value:
                      type: ":integer"
                      value: "1"
    local:
      - name: "Done"
        args:
          - name: "asc_1"
            value:
              type: ":integer"
              value: "1"
          - name: "asc_2"
            value:
              type: ":integer"
              value: "2"
          - name: "asc_3"
            value:
              type: ":integer"
              value: "2"
          - name: "desc_1"
            value:
              type: ":integer"
              value: "3"
          - name: "desc_2"
            value:
              type: ":integer"
              value: "3"
          - name: "desc_3"
            value:
              type: ":integer"
              value: "2"
          - name: "priority_1"
            value:
              type: ":text"
              value: "Rook"
          - name: "priority_2"
            value:
              type: ":text"
              value: "Knight"
          - name: "priority_3"
            value:
              type: ":text"
              value: "Archer"
          - name: "first"
            value:
              type: ":integer"
              value: "3"
          - name: "last"
            value:
              type: ":integer"
              value: "2"
          - name: "distinctCount"
            value:
              type: ":integer"
              value: "3"
          - name: "distinct_1"
            value:
              type: ":text"
              value: "Knight"
          - name: "distinct_2"
            value:
              type: ":text"
              value: "Archer"
          - name: "melee_1"
            value:
              type: ":text"
              value: "Knight"
          - name: "melee_2"
            value:
              type: ":text"
              value: "Rook"
          - name: "ranged_1"
            value:
              type: ":text"
              value: "Archer"
```

---

## Test: collections add subtract intersect and zip

This runtime case exercises “collections add subtract intersect and zip” and verifies the declared messages, values, and execution result.

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
  - name: "collections add subtract intersect and zip.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let appended be [1, 2, 3] + 4
  let prepended be 0 + [1, 2, 3]
  let mergedList be [1, 2, 3] | [4, 5]
  let commonList be [1, 2, 2, 3] & [2, 2, 4]
  let leftOnly be [1, 2, 2, 3] - [2]
  let flags be [enemy:, visible:]
  let mixedFlags be [enemy:, hp: 10]
  let dictA be [name: 'Mark', age: 32]
  let dictB be [city: 'Somewhere', age: 33]
  let mergedDict be dictA | dictB
  let minusAge be mergedDict - #age
  let zipped be ['a', 'b', 'c'] zip [1, 2]
  emit Done(appended: appended[4], prepended: prepended[1], mergedList: mergedList[5], common_1: commonList[1], common_2: commonList[2], leftOnlyLen: leftOnly[:count], leftOnlySecond: leftOnly[2], enemyFlag: flags.enemy, visibleIn: #visible in flags, missingIn: #missing in flags, numericLookup: flags[1], numericIn: 1 in flags, mixedEnemy: mixedFlags.enemy, mixedHp: mixedFlags.hp, mergedAge: mergedDict.age, mergedCity: mergedDict.city, minusAgeMissing: minusAge.age, minusAgeName: minusAge.name, zipOneLeft: zipped[1].left, zipOneRight: zipped[1].right, zipTwoLeft: zipped[2].left, zipTwoRight: zipped[2].right, zipLen: zipped[:count])
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
          - name: "appended"
            value:
              type: ":integer"
              value: "4"
          - name: "prepended"
            value:
              type: ":integer"
              value: "0"
          - name: "mergedList"
            value:
              type: ":integer"
              value: "5"
          - name: "common_1"
            value:
              type: ":integer"
              value: "2"
          - name: "common_2"
            value:
              type: ":integer"
              value: "2"
          - name: "leftOnlyLen"
            value:
              type: ":integer"
              value: "3"
          - name: "leftOnlySecond"
            value:
              type: ":integer"
              value: "2"
          - name: "enemyFlag"
            value:
              type: ":boolean"
              value: true
          - name: "visibleIn"
            value:
              type: ":boolean"
              value: true
          - name: "missingIn"
            value:
              type: ":boolean"
              value: false
          - name: "numericLookup"
            value:
              type: ":nothing"
          - name: "numericIn"
            value:
              type: ":boolean"
              value: false
          - name: "mixedEnemy"
            value:
              type: ":boolean"
              value: true
          - name: "mixedHp"
            value:
              type: ":integer"
              value: "10"
          - name: "mergedAge"
            value:
              type: ":integer"
              value: "33"
          - name: "mergedCity"
            value:
              type: ":text"
              value: "Somewhere"
          - name: "minusAgeMissing"
            value:
              type: ":nothing"
          - name: "minusAgeName"
            value:
              type: ":text"
              value: "Mark"
          - name: "zipOneLeft"
            value:
              type: ":text"
              value: "a"
          - name: "zipOneRight"
            value:
              type: ":integer"
              value: "1"
          - name: "zipTwoLeft"
            value:
              type: ":text"
              value: "b"
          - name: "zipTwoRight"
            value:
              type: ":integer"
              value: "2"
          - name: "zipLen"
            value:
              type: ":integer"
              value: "2"
```

---

## Test: collections reverse contains and object matching

This runtime case exercises “collections reverse contains and object matching” and verifies the declared messages, values, and execution result.

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
  - name: "collections reverse contains and object matching.ges"
    program: main
```

### Source code under test

```ges
on Start(units) {
  let reversed be [1, 2, 3][:reverse]
  let invalidReverse be 123[:reverse]
  let hasTwo be [1, 2, 3][:contains 2]
  let hasAll be [1, 2, 3][:contains all [1, 3]]
  let hasAny be [1, 2, 3][:contains any [0, 3]]
  let textContains be 'battle'[:contains 'tt']
  let textContainsAll be 'battle'[:contains all ['ba', 'tt']]
  let dictContains be [name: 'Ada', team: 'red'][:contains 'name']
  let hasRook be units[:has [faction: 'rook', alive: true]]
  let hasNestedOwner be units[:has [owner: [team: 'red']]]
  emit Done(reverse_1: reversed[1], reverse_2: reversed[2], reverse_3: reversed[3], invalidReverse: invalidReverse, hasTwo: hasTwo, hasAll: hasAll, hasAny: hasAny, textContains: textContains, textContainsAll: textContainsAll, dictContains: dictContains, hasRook: hasRook, hasNestedOwner: hasNestedOwner)
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
                  - key: "faction"
                    value:
                      type: ":text"
                      value: "rook"
                  - key: "alive"
                    value:
                      type: ":boolean"
                      value: true
                  - key: "owner"
                    value:
                      type: ":map"
                      entries:
                        - key: "team"
                          value:
                            type: ":text"
                            value: "red"
              - type: ":map"
                entries:
                  - key: "faction"
                    value:
                      type: ":text"
                      value: "human"
                  - key: "alive"
                    value:
                      type: ":boolean"
                      value: true
                  - key: "owner"
                    value:
                      type: ":map"
                      entries:
                        - key: "team"
                          value:
                            type: ":text"
                            value: "blue"
    local:
      - name: "Done"
        args:
          - name: "reverse_1"
            value:
              type: ":integer"
              value: "3"
          - name: "reverse_2"
            value:
              type: ":integer"
              value: "2"
          - name: "reverse_3"
            value:
              type: ":integer"
              value: "1"
          - name: "invalidReverse"
            value:
              type: ":nothing"
          - name: "hasTwo"
            value:
              type: ":boolean"
              value: true
          - name: "hasAll"
            value:
              type: ":boolean"
              value: true
          - name: "hasAny"
            value:
              type: ":boolean"
              value: true
          - name: "textContains"
            value:
              type: ":boolean"
              value: true
          - name: "textContainsAll"
            value:
              type: ":boolean"
              value: true
          - name: "dictContains"
            value:
              type: ":boolean"
              value: true
          - name: "hasRook"
            value:
              type: ":boolean"
              value: true
          - name: "hasNestedOwner"
            value:
              type: ":boolean"
              value: true
```

---

## Test: keys values and entries iterate through public output

This runtime case exercises “keys values and entries iterate through public output” and verifies the declared messages, values, and execution result.

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
  - name: "keys values and entries iterate through public output.ges"
    program: main
```

### Source code under test

```ges
on Start(player, values, maybeTarget) {
  let dictKeys be player[:keys]
  let valueSequence be values[:values]
  let maybeValues be maybeTarget[:values]
  let entries be player[:entries]
  for key in dictKeys {
    emit SeenKey(key: key, value: player[key])
  }
  for item in valueSequence {
    emit SeenValue(value: item)
  }
  for target in maybeValues {
    emit SeenMaybe(value: target)
  }
  for entry in entries {
    emit SeenEntry(key: entry.key, value: entry[#value])
  }
  emit Done(keyCount: dictKeys[:count], valueCount: valueSequence[:count], maybeCount: maybeValues[:count], entryCount: entries[:count])
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
        - name: "player"
          value:
            type: ":map"
            entries:
              - key: "age"
                value:
                  type: ":integer"
                  value: "25"
              - key: "name"
                value:
                  type: ":text"
                  value: "Mark"
        - name: "values"
          value:
            type: ":map"
            entries:
              - key: "a"
                value:
                  type: ":integer"
                  value: "10"
              - key: "b"
                value:
                  type: ":integer"
                  value: "20"
              - key: "c"
                value:
                  type: ":integer"
                  value: "30"
        - name: "maybeTarget"
          value:
            type: ":list"
            items:
              - type: ":text"
                value: "boss"
    local:
      - name: "SeenKey"
        args:
          - name: "key"
            value:
              type: ":tag"
              value: "age"
          - name: "value"
            value:
              type: ":integer"
              value: "25"
      - name: "SeenKey"
        args:
          - name: "key"
            value:
              type: ":tag"
              value: "name"
          - name: "value"
            value:
              type: ":text"
              value: "Mark"
      - name: "SeenValue"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "10"
      - name: "SeenValue"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "20"
      - name: "SeenValue"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "30"
      - name: "SeenEntry"
        args:
          - name: "key"
            value:
              type: ":tag"
              value: "age"
          - name: "value"
            value:
              type: ":integer"
              value: "25"
      - name: "SeenEntry"
        args:
          - name: "key"
            value:
              type: ":tag"
              value: "name"
          - name: "value"
            value:
              type: ":text"
              value: "Mark"
      - name: "Done"
        args:
          - name: "keyCount"
            value:
              type: ":integer"
              value: "2"
          - name: "valueCount"
            value:
              type: ":integer"
              value: "3"
          - name: "maybeCount"
            value:
              type: ":integer"
              value: "0"
          - name: "entryCount"
            value:
              type: ":integer"
              value: "2"
```
