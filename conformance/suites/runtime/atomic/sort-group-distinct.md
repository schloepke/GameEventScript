---
formatVersion: 1
suiteId: "runtime.atomic.sort-group-distinct"
title: "RuntimeAtomicSortGroupDistinct"
categories: [conformance]
---

# RuntimeAtomicSortGroupDistinct

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite isolates stable sorting, grouping, and distinct-value behavior.

---

## Test: distinct direct source kinds

This runtime case exercises “distinct direct source kinds” and verifies the declared messages, values, and execution result.

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
sources:
  - name: "distinct direct source kinds.ges"
    program: main
```

### Source code under test

```ges
module atomicdistinctdirectkinds
on Start {
  let vNothing be nothing
  let vBoolean be true
  let vInteger be 10
  let vFloat be 10.5
  let vPercentage be 25%
  let vText be 'abba'
  let vTag be #abba
  let vVector be :Vector(1, 2, 1)
  let vPoint be :Point(1, 2, 1)
  let vList be [3, 1, 3, 2, 1]
  let vEmptyList be []
  let vMap be [b: 2, a: 1, c: 2]
  let vDice be ([6, 5, 5, 3, 3]) as :Dice
  let vEmptyDice be ([]) as :Dice
  let vRange be (from 1 to 3) as :Range
  emit Done(nothingValue: vNothing[:distinct], booleanValue: vBoolean[:distinct], integerValue: vInteger[:distinct], floatValue: vFloat[:distinct], percentageValue: vPercentage[:distinct], textValue: vText[:distinct], tagValue: vTag[:distinct], vectorValue: vVector[:distinct], pointValue: vPoint[:distinct], listValue: vList[:distinct], emptyListValue: vEmptyList[:distinct], mapValue: vMap[:distinct], diceValue: vDice[:distinct], emptyDiceValue: vEmptyDice[:distinct], rangeValue: vRange[:distinct])
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
          - name: "nothingValue"
            value:
              type: ":Nothing"
          - name: "booleanValue"
            value:
              type: ":Nothing"
          - name: "integerValue"
            value:
              type: ":Nothing"
          - name: "floatValue"
            value:
              type: ":Nothing"
          - name: "percentageValue"
            value:
              type: ":Nothing"
          - name: "textValue"
            value:
              type: ":Nothing"
          - name: "tagValue"
            value:
              type: ":Nothing"
          - name: "vectorValue"
            value:
              type: ":Nothing"
          - name: "pointValue"
            value:
              type: ":Nothing"
          - name: "listValue"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "3"
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
          - name: "emptyListValue"
            value:
              type: ":List"
              items: []
          - name: "mapValue"
            value:
              type: ":Nothing"
          - name: "diceValue"
            value:
              type: ":Dice"
              rolls:
                - 6
                - 5
                - 3
          - name: "emptyDiceValue"
            value:
              type: ":Dice"
              rolls: []
          - name: "rangeValue"
            value:
              type: ":Nothing"
```

---

## Test: distinct by direct source kinds

This runtime case exercises “distinct by direct source kinds” and verifies the declared messages, values, and execution result.

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
  - name: "distinct by direct source kinds.ges"
    program: main
```

### Source code under test

```ges
module atomicdistinctbydirectkinds
on Start {
  let units be [[name: 'Knight', faction: #melee, hp: 10], [name: 'Rook', faction: #melee, hp: 8], [name: 'Archer', faction: #ranged, hp: 6], [name: 'Scout', faction: #ranged, hp: 3]]
  let values be [1, 2, 3, 4, 5]
  let words be ['alpha', 'alpine', 'beta', 'bravo']
  let emptyList be []
  let dice be ([6, 5, 5, 3]) as :Dice
  let range be (from 1 to 3) as :Range
  let map be [a: 1, b: 2]
  let byFaction be units[:distinct by unit => unit.faction]
  let byParity be values[:distinct by value => value mod 2]
  let byHasHighHp be units[:distinct by unit => unit.hp > 7]
  let byFirstLetter be words[:distinct by word => word[1]]
  let byEmpty be emptyList[:distinct by item => item]
  emit Done(factionLen: byFaction[:count], factionFirst: byFaction[1].name, factionSecond: byFaction[2].name, parityLen: byParity[:count], parityFirst: byParity[1], paritySecond: byParity[2], boolLen: byHasHighHp[:count], boolFirst: byHasHighHp[1].name, boolSecond: byHasHighHp[2].name, textLen: byFirstLetter[:count], textFirst: byFirstLetter[1], textSecond: byFirstLetter[2], emptyLen: byEmpty[:count], nothingValue: nothing[:distinct by value => value], booleanValue: true[:distinct by value => value], integerValue: 10[:distinct by value => value], floatValue: 10.5[:distinct by value => value], percentageValue: 25%[:distinct by value => value], textValue: 'abba'[:distinct by value => value], tagValue: #abba[:distinct by value => value], vectorValue: :Vector(1, 2, 3)[:distinct by value => value], pointValue: :Point(1, 2, 3)[:distinct by value => value], diceValue: dice[:distinct by value => value], rangeValue: range[:distinct by value => value], mapValue: map[:distinct by value => value])
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
          - name: "factionLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "factionFirst"
            value:
              type: ":Text"
              value: "Knight"
          - name: "factionSecond"
            value:
              type: ":Text"
              value: "Archer"
          - name: "parityLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "parityFirst"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "paritySecond"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "boolLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "boolFirst"
            value:
              type: ":Text"
              value: "Knight"
          - name: "boolSecond"
            value:
              type: ":Text"
              value: "Archer"
          - name: "textLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "textFirst"
            value:
              type: ":Text"
              value: "alpha"
          - name: "textSecond"
            value:
              type: ":Text"
              value: "beta"
          - name: "emptyLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "nothingValue"
            value:
              type: ":Nothing"
          - name: "booleanValue"
            value:
              type: ":Nothing"
          - name: "integerValue"
            value:
              type: ":Nothing"
          - name: "floatValue"
            value:
              type: ":Nothing"
          - name: "percentageValue"
            value:
              type: ":Nothing"
          - name: "textValue"
            value:
              type: ":Nothing"
          - name: "tagValue"
            value:
              type: ":Nothing"
          - name: "vectorValue"
            value:
              type: ":Nothing"
          - name: "pointValue"
            value:
              type: ":Nothing"
          - name: "diceValue"
            value:
              type: ":Nothing"
          - name: "rangeValue"
            value:
              type: ":Nothing"
          - name: "mapValue"
            value:
              type: ":Nothing"
```

---

## Test: distinct and distinct by transformed iterators

This runtime case exercises “distinct and distinct by transformed iterators” and verifies the declared messages, values, and execution result.

### Case description

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
  - name: "distinct and distinct by transformed iterators.ges"
    program: main
```

### Source code under test

```ges
module atomicdistinctiterators
on Start {
  let units be [[name: 'Knight', faction: #melee, hp: 10], [name: 'Rook', faction: #melee, hp: 8], [name: 'Archer', faction: #ranged, hp: 6], [name: 'Scout', faction: #ranged, hp: 3]]
  let dice be ([6, 5, 5, 3]) as :Dice
  let iteratorDistinct be [1, 2, 1, 3, 2][:select value => value][:distinct]
  let iteratorDistinctBy be units[:filter unit where unit.hp > 0][:distinct by unit => unit.faction]
  let iteratorDistinctByBool be units[:filter unit where true][:distinct by unit => unit.hp > 7]
  let diceIteratorDistinctBy be dice[:filter value where true][:distinct by value => value]
  let emptyIteratorDistinct be [1, 2][:filter value where false][:distinct]
  let emptyIteratorDistinctBy be units[:filter unit where false][:distinct by unit => unit.faction]
  emit Done(iteratorLen: iteratorDistinct[:count], iteratorFirst: iteratorDistinct[1], iteratorSecond: iteratorDistinct[2], iteratorThird: iteratorDistinct[3], byLen: iteratorDistinctBy[:count], byFirst: iteratorDistinctBy[1].name, bySecond: iteratorDistinctBy[2].name, boolLen: iteratorDistinctByBool[:count], boolFirst: iteratorDistinctByBool[1].name, boolSecond: iteratorDistinctByBool[2].name, diceLen: diceIteratorDistinctBy[:count], diceFirst: diceIteratorDistinctBy[1], diceSecond: diceIteratorDistinctBy[2], diceThird: diceIteratorDistinctBy[3], emptyLen: emptyIteratorDistinct[:count], emptyByLen: emptyIteratorDistinctBy[:count])
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
          - name: "iteratorLen"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "iteratorFirst"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "iteratorSecond"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "iteratorThird"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "byLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "byFirst"
            value:
              type: ":Text"
              value: "Knight"
          - name: "bySecond"
            value:
              type: ":Text"
              value: "Archer"
          - name: "boolLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "boolFirst"
            value:
              type: ":Text"
              value: "Knight"
          - name: "boolSecond"
            value:
              type: ":Text"
              value: "Archer"
          - name: "diceLen"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "diceFirst"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "diceSecond"
            value:
              type: ":Number.int64"
              value: "5"
          - name: "diceThird"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "emptyLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "emptyByLen"
            value:
              type: ":Number.int64"
              value: "0"
```

---

## Test: sort direct and iterator source kinds

This runtime case exercises “sort direct and iterator source kinds” and verifies the declared messages, values, and execution result.

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
  - name: "sort direct and iterator source kinds.ges"
    program: main
```

### Source code under test

```ges
module atomicsortkinds
on Start {
  let values be [3, 1, 2, 2]
  let textValues be ['beta', 'alpha', 'alpha']
  let tagValues be [#beta, #alpha, #gamma]
  let unitValues be [1m, 3m, 2m]
  let emptyList be []
  let dice be ([6, 4, 2]) as :Dice
  let emptyDice be ([]) as :Dice
  let range be (from 3 to 1 step -1) as :Range
  let floatRange be (from 3.5 to 2.5 step -0.5) as :Range
  let map be [b: 2, a: 1]
  let iteratorSorted be [1, 2, 3][:select value => 0 - value][:sort ascending]
  let iteratorDescending be [1, 2, 3][:filter value where true][:sort descending]
  let emptyIteratorSorted be [1, 2][:filter value where false][:sort ascending]
  let ascending be values[:sort ascending]
  let descending be values[:sort descending]
  let textAscending be textValues[:sort ascending]
  let tagDescending be tagValues[:sort descending]
  let unitAscending be unitValues[:sort ascending]
  let diceAscending be dice[:sort ascending]
  let diceDescending be dice[:sort descending]
  let rangeAscending be range[:sort ascending]
  let rangeDescending be range[:sort descending]
  let floatRangeAscending be floatRange[:sort ascending]
  emit Done(asc1: ascending[1], asc2: ascending[2], asc3: ascending[3], asc4: ascending[4], desc1: descending[1], desc2: descending[2], desc3: descending[3], desc4: descending[4], text1: textAscending[1], text2: textAscending[2], text3: textAscending[3], tag1: tagDescending[1], tag2: tagDescending[2], tag3: tagDescending[3], unit1: unitAscending[1], unit2: unitAscending[2], unit3: unitAscending[3], emptyListLen: emptyList[:count], dice1: diceAscending[1], dice2: diceAscending[2], dice3: diceAscending[3], diceAscIsList: diceAscending is :List, diceDesc1: diceDescending[1], diceDesc2: diceDescending[2], diceDesc3: diceDescending[3], diceDescIsList: diceDescending is :List, emptyDiceLen: emptyDice[:count], range1: rangeAscending[1], range2: rangeAscending[2], range3: rangeAscending[3], rangeAscIsRange: rangeAscending is :Range, rangeDesc1: rangeDescending[1], rangeDesc2: rangeDescending[2], rangeDesc3: rangeDescending[3], rangeDescIsRange: rangeDescending is :Range, floatRange1: floatRangeAscending[1], floatRange2: floatRangeAscending[2], floatRange3: floatRangeAscending[3], iterator1: iteratorSorted[1], iterator2: iteratorSorted[2], iterator3: iteratorSorted[3], iteratorDesc1: iteratorDescending[1], iteratorDesc2: iteratorDescending[2], iteratorDesc3: iteratorDescending[3], emptyIteratorLen: emptyIteratorSorted[:count], nothingValue: nothing[:sort ascending], integerValue: 10[:sort ascending], textValue: 'ba'[:sort ascending], tagValue: #ba[:sort ascending], mapValue: map[:sort ascending])
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
          - name: "asc1"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "asc2"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "asc3"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "asc4"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "desc1"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "desc2"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "desc3"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "desc4"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "text1"
            value:
              type: ":Text"
              value: "alpha"
          - name: "text2"
            value:
              type: ":Text"
              value: "alpha"
          - name: "text3"
            value:
              type: ":Text"
              value: "beta"
          - name: "tag1"
            value:
              type: ":Tag"
              value: "gamma"
          - name: "tag2"
            value:
              type: ":Tag"
              value: "beta"
          - name: "tag3"
            value:
              type: ":Tag"
              value: "alpha"
          - name: "unit1"
            value:
              type: ":Quantity.int64"
              value: "1"
              unit: ":meter"
          - name: "unit2"
            value:
              type: ":Quantity.int64"
              value: "2"
              unit: ":meter"
          - name: "unit3"
            value:
              type: ":Quantity.int64"
              value: "3"
              unit: ":meter"
          - name: "emptyListLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "dice1"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "dice2"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "dice3"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "diceAscIsList"
            value:
              type: ":Boolean"
              value: true
          - name: "diceDesc1"
            value:
              type: ":Number.int64"
              value: "6"
          - name: "diceDesc2"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "diceDesc3"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "diceDescIsList"
            value:
              type: ":Boolean"
              value: true
          - name: "emptyDiceLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "range1"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "range2"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "range3"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "rangeAscIsRange"
            value:
              type: ":Boolean"
              value: true
          - name: "rangeDesc1"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "rangeDesc2"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "rangeDesc3"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "rangeDescIsRange"
            value:
              type: ":Boolean"
              value: true
          - name: "floatRange1"
            value:
              type: ":Number.binary64"
              value: "2.5"
          - name: "floatRange2"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "floatRange3"
            value:
              type: ":Number.binary64"
              value: "3.5"
          - name: "iterator1"
            value:
              type: ":Number.int64"
              value: "-3"
          - name: "iterator2"
            value:
              type: ":Number.int64"
              value: "-2"
          - name: "iterator3"
            value:
              type: ":Number.int64"
              value: "-1"
          - name: "iteratorDesc1"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "iteratorDesc2"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "iteratorDesc3"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "emptyIteratorLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "nothingValue"
            value:
              type: ":Nothing"
          - name: "integerValue"
            value:
              type: ":Nothing"
          - name: "textValue"
            value:
              type: ":Nothing"
          - name: "tagValue"
            value:
              type: ":Nothing"
          - name: "mapValue"
            value:
              type: ":Nothing"
```

---

## Test: order by source kinds

This runtime case exercises “order by source kinds” and verifies the declared messages, values, and execution result.

### Case description

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
  - name: "order by source kinds.ges"
    program: main
```

### Source code under test

```ges
module atomicorderbykinds
on Start {
  let units be [[name: 'Knight', hp: 10, faction: #melee], [name: 'Rook', hp: 8, faction: #melee], [name: 'Archer', hp: 6, faction: #ranged], [name: 'Scout', hp: 8, faction: #ranged]]
  let values be [1, 3, 2]
  let textValues be ['bb', 'a', 'ccc']
  let emptyList be []
  let dice be ([6, 5, 3]) as :Dice
  let range be (from 1 to 3) as :Range
  let map be [b: 2, a: 1]
  let byHpAscending be units[:order by unit => unit.hp ascending]
  let byHpDescending be units[:order by unit => unit.hp descending]
  let byFactionAscending be units[:order by unit => unit.faction ascending]
  let valuesDescending be values[:order by value => value descending]
  let textByLength be textValues[:order by text => text[:count] ascending]
  let emptyOrdered be emptyList[:order by item => item ascending]
  let diceAscending be dice[:order by value => value ascending]
  let rangeDescending be range[:order by value => value descending]
  let iteratorOrdered be units[:filter unit where unit.hp > 0][:order by unit => unit.name ascending]
  let iteratorByHpDescending be units[:filter unit where true][:order by unit => unit.hp descending]
  let emptyIteratorOrdered be units[:filter unit where false][:order by unit => unit.hp ascending]
  emit Done(hpAsc1: byHpAscending[1].name, hpAsc2: byHpAscending[2].name, hpAsc3: byHpAscending[3].name, hpAsc4: byHpAscending[4].name, hpDesc1: byHpDescending[1].name, hpDesc2: byHpDescending[2].name, hpDesc3: byHpDescending[3].name, hpDesc4: byHpDescending[4].name, faction1: byFactionAscending[1].name, faction2: byFactionAscending[2].name, faction3: byFactionAscending[3].name, faction4: byFactionAscending[4].name, values1: valuesDescending[1], values2: valuesDescending[2], values3: valuesDescending[3], text1: textByLength[1], text2: textByLength[2], text3: textByLength[3], emptyLen: emptyOrdered[:count], dice1: diceAscending[1], dice2: diceAscending[2], dice3: diceAscending[3], range1: rangeDescending[1], range2: rangeDescending[2], range3: rangeDescending[3], iterator1: iteratorOrdered[1].name, iterator2: iteratorOrdered[2].name, iterator3: iteratorOrdered[3].name, iterator4: iteratorOrdered[4].name, iteratorHp1: iteratorByHpDescending[1].name, iteratorHp2: iteratorByHpDescending[2].name, iteratorHp3: iteratorByHpDescending[3].name, iteratorHp4: iteratorByHpDescending[4].name, emptyIteratorLen: emptyIteratorOrdered[:count], nothingValue: nothing[:order by value => value ascending], integerValue: 10[:order by value => value ascending], textValue: 'ba'[:order by value => value ascending], mapValue: map[:order by value => value ascending])
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
          - name: "hpAsc1"
            value:
              type: ":Text"
              value: "Archer"
          - name: "hpAsc2"
            value:
              type: ":Text"
              value: "Rook"
          - name: "hpAsc3"
            value:
              type: ":Text"
              value: "Scout"
          - name: "hpAsc4"
            value:
              type: ":Text"
              value: "Knight"
          - name: "hpDesc1"
            value:
              type: ":Text"
              value: "Knight"
          - name: "hpDesc2"
            value:
              type: ":Text"
              value: "Rook"
          - name: "hpDesc3"
            value:
              type: ":Text"
              value: "Scout"
          - name: "hpDesc4"
            value:
              type: ":Text"
              value: "Archer"
          - name: "faction1"
            value:
              type: ":Text"
              value: "Knight"
          - name: "faction2"
            value:
              type: ":Text"
              value: "Rook"
          - name: "faction3"
            value:
              type: ":Text"
              value: "Archer"
          - name: "faction4"
            value:
              type: ":Text"
              value: "Scout"
          - name: "values1"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "values2"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "values3"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "text1"
            value:
              type: ":Text"
              value: "a"
          - name: "text2"
            value:
              type: ":Text"
              value: "bb"
          - name: "text3"
            value:
              type: ":Text"
              value: "ccc"
          - name: "emptyLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "dice1"
            value:
              type: ":Nothing"
          - name: "dice2"
            value:
              type: ":Nothing"
          - name: "dice3"
            value:
              type: ":Nothing"
          - name: "range1"
            value:
              type: ":Nothing"
          - name: "range2"
            value:
              type: ":Nothing"
          - name: "range3"
            value:
              type: ":Nothing"
          - name: "iterator1"
            value:
              type: ":Text"
              value: "Archer"
          - name: "iterator2"
            value:
              type: ":Text"
              value: "Knight"
          - name: "iterator3"
            value:
              type: ":Text"
              value: "Rook"
          - name: "iterator4"
            value:
              type: ":Text"
              value: "Scout"
          - name: "iteratorHp1"
            value:
              type: ":Text"
              value: "Knight"
          - name: "iteratorHp2"
            value:
              type: ":Text"
              value: "Rook"
          - name: "iteratorHp3"
            value:
              type: ":Text"
              value: "Scout"
          - name: "iteratorHp4"
            value:
              type: ":Text"
              value: "Archer"
          - name: "emptyIteratorLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "nothingValue"
            value:
              type: ":Nothing"
          - name: "integerValue"
            value:
              type: ":Nothing"
          - name: "textValue"
            value:
              type: ":Nothing"
          - name: "mapValue"
            value:
              type: ":Nothing"
```

---

## Test: group by source kinds

This runtime case exercises “group by source kinds” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0006
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "group by source kinds.ges"
    program: main
```

### Source code under test

```ges
module atomicgroupbykinds
on Start {
  let units be [[name: 'Knight', faction: #melee, hp: 10], [name: 'Rook', faction: #melee, hp: 8], [name: 'Archer', faction: #ranged, hp: 6], [name: 'Scout', faction: #ranged, hp: 3]]
  let values be [1, 2, 3, 4]
  let emptyList be []
  let dice be ([6, 5, 5, 3]) as :Dice
  let range be (from 1 to 4) as :Range
  let map be [b: 'aa', a: 'z', c: 'bb']
  let emptyMap be [:]
  let unitGroups be units[:group by unit => unit.faction]
  let parityGroups be values[:group by value => value mod 2]
  let boolGroups be units[:group by unit => unit.hp > 7]
  let emptyListGroups be emptyList[:group by value => value]
  let mapGroups be map[:group by value => value[:count]]
  let emptyMapGroups be emptyMap[:group by value => value]
  let iteratorGroups be units[:filter unit where unit.hp > 0][:group by unit => unit.faction]
  let emptyIteratorGroups be units[:filter unit where false][:group by unit => unit.faction]
  let diceGroups be dice[:group by value => value]
  let rangeGroups be range[:group by value => value mod 2]
  let textGroups be 'aba'[:group by value => value]
  let emptyGroups be 10[:group by value => value]
  emit Done(meleeLen: unitGroups[#melee][:count], meleeFirst: unitGroups[#melee][1].name, meleeSecond: unitGroups[#melee][2].name, rangedLen: unitGroups[#ranged][:count], rangedFirst: unitGroups[#ranged][1].name, rangedSecond: unitGroups[#ranged][2].name, oddLen: parityGroups['1'][:count], oddFirst: parityGroups['1'][1], evenLen: parityGroups['0'][:count], evenFirst: parityGroups['0'][1], trueLen: boolGroups['True'][:count], trueFirst: boolGroups['True'][1].name, falseLen: boolGroups['False'][:count], falseFirst: boolGroups['False'][1].name, emptyListLen: emptyList[:count], mapOneLen: mapGroups['1'][:count], mapOneFirst: mapGroups['1'][1], mapTwoLen: mapGroups['2'][:count], mapTwoFirst: mapGroups['2'][1], mapTwoSecond: mapGroups['2'][2], emptyMapLen: emptyMap[:count], iteratorMeleeLen: iteratorGroups[#melee][:count], iteratorRangedSecond: iteratorGroups[#ranged][2].name, emptyIteratorLen: emptyIteratorGroups[:count], diceFiveLen: diceGroups['5'][:count], diceSixFirst: diceGroups['6'][1], rangeEvenLen: rangeGroups['0'][:count], textALen: textGroups['a'][:count], emptyLen: emptyGroups[:count])
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
          - name: "meleeLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "meleeFirst"
            value:
              type: ":Text"
              value: "Knight"
          - name: "meleeSecond"
            value:
              type: ":Text"
              value: "Rook"
          - name: "rangedLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "rangedFirst"
            value:
              type: ":Text"
              value: "Archer"
          - name: "rangedSecond"
            value:
              type: ":Text"
              value: "Scout"
          - name: "oddLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "oddFirst"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "evenLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "evenFirst"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "trueLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "trueFirst"
            value:
              type: ":Text"
              value: "Knight"
          - name: "falseLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "falseFirst"
            value:
              type: ":Text"
              value: "Archer"
          - name: "emptyListLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "mapOneLen"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "mapOneFirst"
            value:
              type: ":Text"
              value: "z"
          - name: "mapTwoLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "mapTwoFirst"
            value:
              type: ":Text"
              value: "aa"
          - name: "mapTwoSecond"
            value:
              type: ":Text"
              value: "bb"
          - name: "emptyMapLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "iteratorMeleeLen"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "iteratorRangedSecond"
            value:
              type: ":Text"
              value: "Scout"
          - name: "emptyIteratorLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "diceFiveLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "diceSixFirst"
            value:
              type: ":Nothing"
          - name: "rangeEvenLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "textALen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "emptyLen"
            value:
              type: ":Number.int64"
              value: "0"
```

---

## Test: text and map ordering follows unicode scalar order

This runtime case exercises “text and map ordering follows unicode scalar order” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0007
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "text and map ordering follows unicode scalar order.ges"
    program: main
```

### Source code under test

```ges
module atomicunicodescalarsort
on Start(values) {
  let sorted be ['', '𐀀'][:sort ascending]
  emit Done(firstText: sorted[1], secondText: sorted[2], firstMapValue: values[:first], lastMapValue: values[:last])
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
            type: ":Map"
            entries:
              - key: "\uD800\uDC00"
                value:
                  type: ":Number.int64"
                  value: "2"
              - key: "\uE000"
                value:
                  type: ":Number.int64"
                  value: "1"
    local:
      - name: "Done"
        args:
          - name: "firstText"
            value:
              type: ":Text"
              value: "\uE000"
          - name: "secondText"
            value:
              type: ":Text"
              value: "\uD800\uDC00"
          - name: "firstMapValue"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "lastMapValue"
            value:
              type: ":Number.int64"
              value: "2"
```
