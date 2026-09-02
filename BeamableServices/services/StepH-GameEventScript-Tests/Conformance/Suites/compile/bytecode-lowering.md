---
formatVersion: 1
suiteId: "compile.bytecode-lowering"
title: "Compile Bytecode Lowering"
categories: [conformance]
---

# Compile Bytecode Lowering

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies that language constructs lower to the required portable bytecode operations and metadata.

---

## Test: emit arguments read existing registers without temporary moves

This compiler case exercises “emit arguments read existing registers without temporary moves” and verifies the declared portable output.

### Case description

```yaml
gesBlock: case
id: case-0001
kind: bytecode
level: atomic
sources:
  - name: "emit arguments read existing registers without temporary moves.ges"
    program: main
```

### Source code under test

```ges
module EmitArgumentReads

on Start(value) {
  let doubled be value * 2
  emit Done(original: value, result: doubled)
}

```

### Expectation

```yaml
gesBlock: expect
opcodes:
  contains: ["Multiply", "EmitMessage"]
  excludes: ["Move"]
```

---

## Test: random take lowers integer and float variants separately

This compiler case exercises “random take lowers integer and float variants separately” and verifies the declared portable output.

### Case description

```yaml
gesBlock: case
id: case-0002
kind: bytecode
level: atomic
sources:
  - name: "random take lowers integer and float variants separately.ges"
    program: main
```

### Source code under test

```ges
module RandomTakes

on Start {
  let integerValue be random from 1 to 6
  let floatValue be random from 0.0 to 1.0
  let mixedValue be random from 1 to 2.0
  emit Done(integerValue: integerValue, floatValue: floatValue, mixedValue: mixedValue)
}

```

### Expectation

```yaml
gesBlock: expect
opcodes:
  counts:
    RandomTake: 1
    RandomTakeFloat: 2
```

---

## Test: stage literal value kinds lower to stage opcodes

This compiler case exercises “stage literal value kinds lower to stage opcodes” and verifies the declared portable output.

### Case description

```yaml
gesBlock: case
id: case-0003
kind: bytecode
level: atomic
sources:
  - name: "stage literal value kinds lower to stage opcodes.ges"
    program: main
```

### Source code under test

```ges
module StageOpcodes

record :sample as {
  falseValue: :boolean,
  trueValue: :boolean,
  integerValue: :number,
  floatValue: :number,
  meterInteger: :number,
  meterFloat: :number,
  percentageValue: :percentage,
  textValue: :text,
  tagValue: :tag,
  omitted: :number
}

on Start {
  let values be [false, true, 12, 12.5, 7m, 1.5m, 25%, 'txt', #ready]
  let sample be :sample(falseValue: false, trueValue: true, integerValue: 12, floatValue: 12.5, meterInteger: 7m, meterFloat: 1.5m, percentageValue: 25%, textValue: 'txt', tagValue: #ready)
  emit Done(values: values, omitted: sample.omitted)
}

```

### Expectation

```yaml
gesBlock: expect
opcodes:
  contains: ["StageNothing", "StageTrue", "StageFalse", "StageInteger", "StageFloat", "StageText", "StageTag", "StagePercentage"]
```

---

## Test: dynamic selectors lower to property access

This compiler case exercises “dynamic selectors lower to property access” and verifies the declared portable output.

### Case description

```yaml
gesBlock: case
id: case-0004
kind: bytecode
level: atomic
sources:
  - name: "dynamic selectors lower to property access.ges"
    program: main
```

### Source code under test

```ges
module PropertyAccessOpcodes

on Start {
  let listValue be [10, 20, 30]
  let mapValue be [name: 'Ada', hp: 10]
  let vectorValue be :vector(1m, 2m, 3m)
  let pointValue be :point(4m, 5m, 6m)
  let textValue be 'ab'
  let indexKey be 2
  let textKey be 'name'
  let tagKey be #hp
  let vectorKey be 'y'
  let pointKey be #z
  let invalidKey be true
  emit Done(listDynamic: listValue[indexKey], mapText: mapValue[textKey], mapTag: mapValue[tagKey], vectorDynamic: vectorValue[vectorKey], pointDynamic: pointValue[pointKey], textDynamic: textValue[indexKey], invalidProperty: mapValue[invalidKey])
}

```

### Expectation

```yaml
gesBlock: expect
opcodes:
  minimumCounts:
    PropertyAccess: 7
```

---

## Test: generated range iterators lower to all range iterator variants

This compiler case exercises “generated range iterators lower to all range iterator variants” and verifies the declared portable output.

### Case description

```yaml
gesBlock: case
id: case-0005
kind: bytecode
level: atomic
sources:
  - name: "generated range iterators lower to all range iterator variants.ges"
    program: main
```

### Source code under test

```ges
module RangeIteratorOpcodes

on Start {
  let fromValue be 2
  let toValue be 5
  let stepValue be 2
  let shortRange be :list[:select item from 1 to 3 => item]
  let dynamicRange be :list[:select item from fromValue to toValue => item]
  let dynamicSteppedRange be :list[:select item from fromValue to toValue step stepValue => item]
  emit Done(shortRange: shortRange, dynamicRange: dynamicRange, dynamicSteppedRange: dynamicSteppedRange)
}

```

### Expectation

```yaml
gesBlock: expect
opcodes:
  contains: ["CreateRangeIteratorShort", "CreateRangeIterator", "CreateRangeIteratorWithStep"]
```

---

## Test: implication lowers to branching tri-state shape

This compiler case exercises “implication lowers to branching tri-state shape” and verifies the declared portable output.

### Case description

```yaml
gesBlock: case
id: case-0006
kind: bytecode
level: atomic
sources:
  - name: "implication lowers to branching tri-state shape.ges"
    program: main
```

### Source code under test

```ges
module ShortCircuit

on Start(flag, value) {
  let skipped be flag -> value
  let resolved be value -> true
  emit Done(skipped: skipped, resolved: resolved)
}

```

### Expectation

```yaml
gesBlock: expect
opcodes:
  contains: ["JumpIfFalse"]
  minimumCounts:
    Implies: 2
```

---

## Test: numeric helper lowers to numeric opcodes

This compiler case exercises “numeric helper lowers to numeric opcodes” and verifies the declared portable output.

### Case description

```yaml
gesBlock: case
id: case-0007
kind: bytecode
level: atomic
sources:
  - name: "numeric helper lowers to numeric opcodes.ges"
    program: main
```

### Source code under test

```ges
module NumericSugar

on Start(value) {
  let numericValue be value as :number
  let isNumeric be numericValue is numeric
  emit Done(numericValue: numericValue, isNumeric: isNumeric)
}

```

### Expectation

```yaml
gesBlock: expect
opcodes:
  contains: ["CastNumeric", "CheckNumeric"]
  excludes: ["Cast", "CheckType"]
```

---

## Test: trig and navigation intrinsics lower to direct opcodes

This compiler case exercises “trig and navigation intrinsics lower to direct opcodes” and verifies the declared portable output.

### Case description

```yaml
gesBlock: case
id: case-0008
kind: bytecode
level: atomic
sources:
  - name: "trig and navigation intrinsics lower to direct opcodes.ges"
    program: main
```

### Source code under test

```ges
module TrigNavigationOpcodes

on Start(a, b) {
  let vectorA be :vector(1, 0, 0)
  let vectorB be :vector(0, 1, 0)
  emit Done(s: sin a, c: cos a, t: tan a, asinValue: asin a, acosValue: acos a, atanValue: atan a, atanTwoValue: atan2(a, b), hypotTwo: hypot(a, b), hypotThree: hypot(a, b, 3), distanceObject: distance(vectorA, vectorB), distanceTwo: distance(0, 0, 1, 1), distanceThree: distance(0, 0, 0, 1, 1, 1), distanceSquaredObject: distance squared(vectorA, vectorB), distanceSquaredTwo: distance squared(0, 0, 1, 1), distanceSquaredThree: distance squared(0, 0, 0, 1, 1, 1), lengthSquaredObject: length squared(vectorA), lengthSquaredTwo: length squared(a, b), lengthSquaredThree: length squared(a, b, 3), normalizeObject: normalize(vectorA), normalizeTwo: normalize(a, b), normalizeThree: normalize(a, b, 3), dotObject: dot(vectorA, vectorB), dotTwo: dot(1, 2, 3, 4), dotThree: dot(1, 2, 3, 4, 5, 6), crossObject: cross(vectorA, vectorB), crossTwo: cross(1, 2, 3, 4), crossThree: cross(1, 2, 3, 4, 5, 6), angleObject: angle between(vectorA, vectorB), angleTwo: angle between(1, 0, 0, 1), angleThree: angle between(1, 0, 0, 0, 1, 0))
}

```

### Expectation

```yaml
gesBlock: expect
opcodes:
  contains: ["Sin", "Cos", "Tan", "Asin", "Acos", "Atan", "Atan2", "Hypot2D", "Hypot3D", "Distance", "Distance2D", "Distance3D", "DistanceSquared", "DistanceSquared2D", "DistanceSquared3D", "LengthSquared", "LengthSquared2D", "LengthSquared3D", "Normalize", "Normalize2D", "Normalize3D", "Dot", "Dot2D", "Dot3D", "Cross", "Cross2D", "Cross3D", "AngleBetween", "AngleBetween2D", "AngleBetween3D"]
  excludes: ["CallExternal"]
```

---

## Test: spatial constructors lower to dedicated opcodes

This compiler case exercises “spatial constructors lower to dedicated opcodes” and verifies the declared portable output.

### Case description

```yaml
gesBlock: case
id: case-0009
kind: bytecode
level: atomic
sources:
  - name: "spatial constructors lower to dedicated opcodes.ges"
    program: main
```

### Source code under test

```ges
module SpatialCreation

on Start(value) {
  let position be :vector(y: value, z: 3)
  let target be :point(1, value)
  emit Done(position: position, target: target)
}

```

### Expectation

```yaml
gesBlock: expect
opcodes:
  contains: ["CreateVector", "CreatePoint"]
  excludes: ["CreateRecord"]
```

---

## Test: loop iterators and constant random scopes lower to direct opcodes

This compiler case exercises “loop iterators and constant random scopes lower to direct opcodes” and verifies the declared portable output.

### Case description

```yaml
gesBlock: case
id: case-0010
kind: bytecode
level: atomic
sources:
  - name: "loop iterators and constant random scopes lower to direct opcodes.ges"
    program: main
```

### Source code under test

```ges
module SideTables

on Start {
  for item from 1 to 3 emit Tick(value: item)
  random with -7 {
    emit Done(value: random from 1 to 6)
  }
}

```

### Expectation

```yaml
gesBlock: expect
opcodes:
  contains: ["CreateRangeIteratorShort", "IteratorNext", "IteratorClose", "RandomPushConstant", "RandomTake", "RandomPop"]
```

---

## Test: dynamic for sources lower to iterator opcodes

This compiler case exercises “dynamic for sources lower to iterator opcodes” and verifies the declared portable output.

### Case description

```yaml
gesBlock: case
id: case-0011
kind: bytecode
level: atomic
sources:
  - name: "dynamic for sources lower to iterator opcodes.ges"
    program: main
```

### Source code under test

```ges
module LoopIterators

on Start(begin, finish, step) {
  for item from begin to finish emit RangeItem(value: item)
  for item from begin to finish step step emit StepItem(value: item)
  let items be [1, 2, 3]
  for item in items emit CollectionItem(value: item)
}

```

### Expectation

```yaml
gesBlock: expect
opcodes:
  contains: ["CreateRangeIterator", "CreateRangeIteratorWithStep", "IteratorCreate"]
  minimumCounts:
    IteratorClose: 3
    IteratorNext: 3
```

---

## Test: explicit integer seed lowers to dynamic random push

This compiler case exercises “explicit integer seed lowers to dynamic random push” and verifies the declared portable output.

### Case description

```yaml
gesBlock: case
id: case-0012
kind: bytecode
level: atomic
sources:
  - name: "explicit integer seed lowers to dynamic random push.ges"
    program: main
```

### Source code under test

```ges
module RandomScopes

on Start(seed) {
  let value be random with (seed as :number) 1
  emit Done(value: value)
}

```

### Expectation

```yaml
gesBlock: expect
opcodes:
  contains: ["RandomPush", "RandomPop"]
  excludes: ["RandomPushConstant"]
```

---

## Test: pipeline selector lowers to explicit iterator loop and list builder

This compiler case exercises “pipeline selector lowers to explicit iterator loop and list builder” and verifies the declared portable output.

### Case description

```yaml
gesBlock: case
id: case-0013
kind: bytecode
level: atomic
sources:
  - name: "pipeline selector lowers to explicit iterator loop and list builder.ges"
    program: main
```

### Source code under test

```ges
module SideTables

on Start(values) {
  let selected be values[:select item => item + 1]
  emit Done(count: selected[:count])
}

```

### Expectation

```yaml
gesBlock: expect
opcodes:
  contains: ["IteratorCreateOrJump", "IteratorNext", "ListBuilderCreate", "ListBuilderAdd", "ListBuilderFinish"]
```

---

## Test: sum and average lower to explicit iterator loops

This compiler case exercises “sum and average lower to explicit iterator loops” and verifies the declared portable output.

### Case description

```yaml
gesBlock: case
id: case-0014
kind: bytecode
level: atomic
sources:
  - name: "sum and average lower to explicit iterator loops.ges"
    program: main
```

### Source code under test

```ges
module AggregateLoops

on Start(values) {
  let total be values[:sum]
  let average be values[:average]
  let projectedTotal be values[:sum value => value * 2]
  let projectedAverage be values[:average value => value * 2]
  emit Done(total: total, average: average, projectedTotal: projectedTotal, projectedAverage: projectedAverage)
}

```

### Expectation

```yaml
gesBlock: expect
opcodes:
  contains: ["IteratorCreateOrJump", "IteratorNext", "IteratorClose"]
```

---

## Test: series term take drop lower without iterator collection opcodes

This compiler case exercises “series term take drop lower without iterator collection opcodes” and verifies the declared portable output.

### Case description

```yaml
gesBlock: case
id: case-0015
kind: bytecode
level: atomic
sources:
  - name: "series term take drop lower without iterator collection opcodes.ges"
    program: main
```

### Source code under test

```ges
module SeriesAtomicShape

on Start {
  let naturals be series fibonacci
  let term be naturals[:term 3]
  let firstValues be naturals[:take first 4]
  let dropped be naturals[:drop first 2]
  let lastValues be naturals[:take last 2]
  let droppedLast be naturals[:drop last 1]
  let highestValues be naturals[:take highest 2]
  let lowestValues be naturals[:take lowest 2]
  let droppedHighest be naturals[:drop highest 1]
  let droppedLowest be naturals[:drop lowest 1]
  emit Done(term: term, firstValues: firstValues, droppedIsSeries: dropped is :series, lastValues: lastValues, droppedLast: droppedLast, highestValues: highestValues, lowestValues: lowestValues, droppedHighest: droppedHighest, droppedLowest: droppedLowest)
}

```

### Expectation

```yaml
gesBlock: expect
opcodes:
  contains: ["CreateSeries", "Term", "TakeFirst", "DropFirst", "TakeLast", "DropLast", "TakeHighest", "TakeLowest", "DropHighest", "DropLowest"]
  excludes: ["Count", "Distinct", "SortAscending", "SortDescending", "Reverse", "Shuffle", "DistinctBuilderCreate", "DistinctBuilderAdd", "DistinctBuilderFinish", "GroupBuilderCreate", "GroupBuilderAdd", "GroupBuilderFinish", "OrderBuilderCreate", "OrderBuilderAdd", "OrderBuilderFinishAscending", "OrderBuilderFinishDescending"]
```
