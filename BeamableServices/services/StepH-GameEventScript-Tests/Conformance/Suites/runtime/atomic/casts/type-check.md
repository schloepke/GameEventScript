---
formatVersion: 1
suiteId: "runtime.atomic.casts.type-check"
title: "Cast Matrix — Type Checks"
categories: [conformance]
tags: [migrated-json-v1]
---

# Cast Matrix — Type Checks

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite covers non-mutating type checks across every supported source value.

---

## Test: check from nothing

This runtime case exercises “check from nothing” and verifies the declared messages, values, and execution result.

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
  - name: "check from nothing.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromNothing
on Start {
  let source be nothing
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: true
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: check from boolean

This runtime case exercises “check from boolean” and verifies the declared messages, values, and execution result.

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
  - name: "check from boolean.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromBoolean
on Start {
  let source be true
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: true
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":boolean"
              value: true
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: check from integer

This runtime case exercises “check from integer” and verifies the declared messages, values, and execution result.

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
  - name: "check from integer.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromInteger
on Start {
  let source be 12
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":boolean"
              value: true
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: check from float

This runtime case exercises “check from float” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0008
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "check from float.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromFloat
on Start {
  let source be 12.5
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: true
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: check from meter

This runtime case exercises “check from meter” and verifies the declared messages, values, and execution result.

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
  - name: "check from meter.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromMeter
on Start {
  let source be 12m
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":boolean"
              value: true
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: true
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: check from percentage

This runtime case exercises “check from percentage” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0012
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "check from percentage.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromPercentage
on Start {
  let source be 25%
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: true
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: true
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: check from numeric text

This runtime case exercises “check from numeric text” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0014
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "check from numeric text.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromNumericText
on Start {
  let source be '12.5'
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: true
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: check from invalid text

This runtime case exercises “check from invalid text” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0016
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "check from invalid text.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromInvalidText
on Start {
  let source be 'hello'
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: true
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: check from pi tag

This runtime case exercises “check from pi tag” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0018
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "check from pi tag.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromPiTag
on Start {
  let source be pi
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: true
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: check from custom tag

This runtime case exercises “check from custom tag” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0020
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "check from custom tag.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromCustomTag
on Start {
  let source be #custom
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: true
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: check from vector

This runtime case exercises “check from vector” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0022
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "check from vector.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromVector
on Start {
  let source be :vector(1, 2, 3)
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: true
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: check from point

This runtime case exercises “check from point” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0024
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "check from point.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromPoint
on Start {
  let source be :point(4, 5, 6)
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: true
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: check from list

This runtime case exercises “check from list” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0026
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "check from list.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromList
on Start {
  let source be [1, 2, 3]
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: true
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: check from map

This runtime case exercises “check from map” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0028
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "check from map.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromMap
on Start {
  let source be [x: 1, y: 2, z: 3]
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: true
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: check from dice

This runtime case exercises “check from dice” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0030
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "check from dice.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromDice
on Start {
  let source as :dice be [3, 2, 1]
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":boolean"
              value: true
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: true
          - name: "isRange"
            value:
              type: ":boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```

---

## Test: check from range

This runtime case exercises “check from range” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0032
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "check from range.ges"
    program: main
```

### Source code under test

```ges
module AtomicCheckFromRange
on Start {
  let source as :range be from 1 to 3
  emit Done(isNothing: source is nothing, isBoolean: source is :boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :percentage, isText: source is :text, isTag: source is :tag, isMeter: source is :quantity(m), isVector: source is :vector, isPoint: source is :point, isList: source is :list, isMap: source is :map, isDice: source is :dice, isRange: source is :range, isMessage: source is :message, isHandler: source is :handler, isSeries: source is :series)
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
          - name: "isNothing"
            value:
              type: ":boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":boolean"
              value: false
          - name: "isText"
            value:
              type: ":boolean"
              value: false
          - name: "isTag"
            value:
              type: ":boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":boolean"
              value: false
          - name: "isVector"
            value:
              type: ":boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":boolean"
              value: false
          - name: "isList"
            value:
              type: ":boolean"
              value: false
          - name: "isMap"
            value:
              type: ":boolean"
              value: false
          - name: "isDice"
            value:
              type: ":boolean"
              value: false
          - name: "isRange"
            value:
              type: ":boolean"
              value: true
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":boolean"
              value: false
```
