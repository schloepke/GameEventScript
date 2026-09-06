---
formatVersion: 1
suiteId: "runtime.atomic.casts.type-check"
title: "Cast Matrix — Type Checks"
categories: [conformance]
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
module atomiccheckfromnothing
on Start {
  let source be nothing
  emit Done(isNothing: source is nothing, isBoolean: source is :Boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :Percentage, isText: source is :Text, isTag: source is :Tag, isMeter: source is :Quantity(m), isVector: source is :Vector, isPoint: source is :Point, isList: source is :List, isMap: source is :Map, isDice: source is :Dice, isRange: source is :Range, isMessage: source is :Message, isHandler: source is :Handler, isSeries: source is :Series)
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
              type: ":Boolean"
              value: true
          - name: "isBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":Boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":Boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":Boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":Boolean"
              value: false
          - name: "isText"
            value:
              type: ":Boolean"
              value: false
          - name: "isTag"
            value:
              type: ":Boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":Boolean"
              value: false
          - name: "isVector"
            value:
              type: ":Boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":Boolean"
              value: false
          - name: "isList"
            value:
              type: ":Boolean"
              value: false
          - name: "isMap"
            value:
              type: ":Boolean"
              value: false
          - name: "isDice"
            value:
              type: ":Boolean"
              value: false
          - name: "isRange"
            value:
              type: ":Boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":Boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":Boolean"
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
module atomiccheckfromboolean
on Start {
  let source be true
  emit Done(isNothing: source is nothing, isBoolean: source is :Boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :Percentage, isText: source is :Text, isTag: source is :Tag, isMeter: source is :Quantity(m), isVector: source is :Vector, isPoint: source is :Point, isList: source is :List, isMap: source is :Map, isDice: source is :Dice, isRange: source is :Range, isMessage: source is :Message, isHandler: source is :Handler, isSeries: source is :Series)
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
              type: ":Boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":Boolean"
              value: true
          - name: "isNumeric"
            value:
              type: ":Boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":Boolean"
              value: true
          - name: "isFractional"
            value:
              type: ":Boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":Boolean"
              value: false
          - name: "isText"
            value:
              type: ":Boolean"
              value: false
          - name: "isTag"
            value:
              type: ":Boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":Boolean"
              value: false
          - name: "isVector"
            value:
              type: ":Boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":Boolean"
              value: false
          - name: "isList"
            value:
              type: ":Boolean"
              value: false
          - name: "isMap"
            value:
              type: ":Boolean"
              value: false
          - name: "isDice"
            value:
              type: ":Boolean"
              value: false
          - name: "isRange"
            value:
              type: ":Boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":Boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":Boolean"
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
module atomiccheckfrominteger
on Start {
  let source be 12
  emit Done(isNothing: source is nothing, isBoolean: source is :Boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :Percentage, isText: source is :Text, isTag: source is :Tag, isMeter: source is :Quantity(m), isVector: source is :Vector, isPoint: source is :Point, isList: source is :List, isMap: source is :Map, isDice: source is :Dice, isRange: source is :Range, isMessage: source is :Message, isHandler: source is :Handler, isSeries: source is :Series)
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
              type: ":Boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":Boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":Boolean"
              value: true
          - name: "isFractional"
            value:
              type: ":Boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":Boolean"
              value: false
          - name: "isText"
            value:
              type: ":Boolean"
              value: false
          - name: "isTag"
            value:
              type: ":Boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":Boolean"
              value: false
          - name: "isVector"
            value:
              type: ":Boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":Boolean"
              value: false
          - name: "isList"
            value:
              type: ":Boolean"
              value: false
          - name: "isMap"
            value:
              type: ":Boolean"
              value: false
          - name: "isDice"
            value:
              type: ":Boolean"
              value: false
          - name: "isRange"
            value:
              type: ":Boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":Boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":Boolean"
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
module atomiccheckfromfloat
on Start {
  let source be 12.5
  emit Done(isNothing: source is nothing, isBoolean: source is :Boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :Percentage, isText: source is :Text, isTag: source is :Tag, isMeter: source is :Quantity(m), isVector: source is :Vector, isPoint: source is :Point, isList: source is :List, isMap: source is :Map, isDice: source is :Dice, isRange: source is :Range, isMessage: source is :Message, isHandler: source is :Handler, isSeries: source is :Series)
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
              type: ":Boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":Boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":Boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":Boolean"
              value: true
          - name: "isPercentage"
            value:
              type: ":Boolean"
              value: false
          - name: "isText"
            value:
              type: ":Boolean"
              value: false
          - name: "isTag"
            value:
              type: ":Boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":Boolean"
              value: false
          - name: "isVector"
            value:
              type: ":Boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":Boolean"
              value: false
          - name: "isList"
            value:
              type: ":Boolean"
              value: false
          - name: "isMap"
            value:
              type: ":Boolean"
              value: false
          - name: "isDice"
            value:
              type: ":Boolean"
              value: false
          - name: "isRange"
            value:
              type: ":Boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":Boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":Boolean"
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
module atomiccheckfrommeter
on Start {
  let source be 12m
  emit Done(isNothing: source is nothing, isBoolean: source is :Boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :Percentage, isText: source is :Text, isTag: source is :Tag, isMeter: source is :Quantity(m), isVector: source is :Vector, isPoint: source is :Point, isList: source is :List, isMap: source is :Map, isDice: source is :Dice, isRange: source is :Range, isMessage: source is :Message, isHandler: source is :Handler, isSeries: source is :Series)
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
              type: ":Boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":Boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":Boolean"
              value: true
          - name: "isFractional"
            value:
              type: ":Boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":Boolean"
              value: false
          - name: "isText"
            value:
              type: ":Boolean"
              value: false
          - name: "isTag"
            value:
              type: ":Boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":Boolean"
              value: true
          - name: "isVector"
            value:
              type: ":Boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":Boolean"
              value: false
          - name: "isList"
            value:
              type: ":Boolean"
              value: false
          - name: "isMap"
            value:
              type: ":Boolean"
              value: false
          - name: "isDice"
            value:
              type: ":Boolean"
              value: false
          - name: "isRange"
            value:
              type: ":Boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":Boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":Boolean"
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
module atomiccheckfrompercentage
on Start {
  let source be 25%
  emit Done(isNothing: source is nothing, isBoolean: source is :Boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :Percentage, isText: source is :Text, isTag: source is :Tag, isMeter: source is :Quantity(m), isVector: source is :Vector, isPoint: source is :Point, isList: source is :List, isMap: source is :Map, isDice: source is :Dice, isRange: source is :Range, isMessage: source is :Message, isHandler: source is :Handler, isSeries: source is :Series)
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
              type: ":Boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":Boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":Boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":Boolean"
              value: true
          - name: "isPercentage"
            value:
              type: ":Boolean"
              value: true
          - name: "isText"
            value:
              type: ":Boolean"
              value: false
          - name: "isTag"
            value:
              type: ":Boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":Boolean"
              value: false
          - name: "isVector"
            value:
              type: ":Boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":Boolean"
              value: false
          - name: "isList"
            value:
              type: ":Boolean"
              value: false
          - name: "isMap"
            value:
              type: ":Boolean"
              value: false
          - name: "isDice"
            value:
              type: ":Boolean"
              value: false
          - name: "isRange"
            value:
              type: ":Boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":Boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":Boolean"
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
module atomiccheckfromnumerictext
on Start {
  let source be '12.5'
  emit Done(isNothing: source is nothing, isBoolean: source is :Boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :Percentage, isText: source is :Text, isTag: source is :Tag, isMeter: source is :Quantity(m), isVector: source is :Vector, isPoint: source is :Point, isList: source is :List, isMap: source is :Map, isDice: source is :Dice, isRange: source is :Range, isMessage: source is :Message, isHandler: source is :Handler, isSeries: source is :Series)
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
              type: ":Boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":Boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":Boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":Boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":Boolean"
              value: false
          - name: "isText"
            value:
              type: ":Boolean"
              value: true
          - name: "isTag"
            value:
              type: ":Boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":Boolean"
              value: false
          - name: "isVector"
            value:
              type: ":Boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":Boolean"
              value: false
          - name: "isList"
            value:
              type: ":Boolean"
              value: false
          - name: "isMap"
            value:
              type: ":Boolean"
              value: false
          - name: "isDice"
            value:
              type: ":Boolean"
              value: false
          - name: "isRange"
            value:
              type: ":Boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":Boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":Boolean"
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
module atomiccheckfrominvalidtext
on Start {
  let source be 'hello'
  emit Done(isNothing: source is nothing, isBoolean: source is :Boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :Percentage, isText: source is :Text, isTag: source is :Tag, isMeter: source is :Quantity(m), isVector: source is :Vector, isPoint: source is :Point, isList: source is :List, isMap: source is :Map, isDice: source is :Dice, isRange: source is :Range, isMessage: source is :Message, isHandler: source is :Handler, isSeries: source is :Series)
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
              type: ":Boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":Boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":Boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":Boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":Boolean"
              value: false
          - name: "isText"
            value:
              type: ":Boolean"
              value: true
          - name: "isTag"
            value:
              type: ":Boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":Boolean"
              value: false
          - name: "isVector"
            value:
              type: ":Boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":Boolean"
              value: false
          - name: "isList"
            value:
              type: ":Boolean"
              value: false
          - name: "isMap"
            value:
              type: ":Boolean"
              value: false
          - name: "isDice"
            value:
              type: ":Boolean"
              value: false
          - name: "isRange"
            value:
              type: ":Boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":Boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":Boolean"
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
module atomiccheckfrompitag
on Start {
  let source be pi
  emit Done(isNothing: source is nothing, isBoolean: source is :Boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :Percentage, isText: source is :Text, isTag: source is :Tag, isMeter: source is :Quantity(m), isVector: source is :Vector, isPoint: source is :Point, isList: source is :List, isMap: source is :Map, isDice: source is :Dice, isRange: source is :Range, isMessage: source is :Message, isHandler: source is :Handler, isSeries: source is :Series)
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
              type: ":Boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":Boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":Boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":Boolean"
              value: true
          - name: "isPercentage"
            value:
              type: ":Boolean"
              value: false
          - name: "isText"
            value:
              type: ":Boolean"
              value: false
          - name: "isTag"
            value:
              type: ":Boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":Boolean"
              value: false
          - name: "isVector"
            value:
              type: ":Boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":Boolean"
              value: false
          - name: "isList"
            value:
              type: ":Boolean"
              value: false
          - name: "isMap"
            value:
              type: ":Boolean"
              value: false
          - name: "isDice"
            value:
              type: ":Boolean"
              value: false
          - name: "isRange"
            value:
              type: ":Boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":Boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":Boolean"
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
module atomiccheckfromcustomtag
on Start {
  let source be #custom
  emit Done(isNothing: source is nothing, isBoolean: source is :Boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :Percentage, isText: source is :Text, isTag: source is :Tag, isMeter: source is :Quantity(m), isVector: source is :Vector, isPoint: source is :Point, isList: source is :List, isMap: source is :Map, isDice: source is :Dice, isRange: source is :Range, isMessage: source is :Message, isHandler: source is :Handler, isSeries: source is :Series)
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
              type: ":Boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":Boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":Boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":Boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":Boolean"
              value: false
          - name: "isText"
            value:
              type: ":Boolean"
              value: false
          - name: "isTag"
            value:
              type: ":Boolean"
              value: true
          - name: "isMeter"
            value:
              type: ":Boolean"
              value: false
          - name: "isVector"
            value:
              type: ":Boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":Boolean"
              value: false
          - name: "isList"
            value:
              type: ":Boolean"
              value: false
          - name: "isMap"
            value:
              type: ":Boolean"
              value: false
          - name: "isDice"
            value:
              type: ":Boolean"
              value: false
          - name: "isRange"
            value:
              type: ":Boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":Boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":Boolean"
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
module atomiccheckfromvector
on Start {
  let source be :Vector(1, 2, 3)
  emit Done(isNothing: source is nothing, isBoolean: source is :Boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :Percentage, isText: source is :Text, isTag: source is :Tag, isMeter: source is :Quantity(m), isVector: source is :Vector, isPoint: source is :Point, isList: source is :List, isMap: source is :Map, isDice: source is :Dice, isRange: source is :Range, isMessage: source is :Message, isHandler: source is :Handler, isSeries: source is :Series)
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
              type: ":Boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":Boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":Boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":Boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":Boolean"
              value: false
          - name: "isText"
            value:
              type: ":Boolean"
              value: false
          - name: "isTag"
            value:
              type: ":Boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":Boolean"
              value: false
          - name: "isVector"
            value:
              type: ":Boolean"
              value: true
          - name: "isPoint"
            value:
              type: ":Boolean"
              value: false
          - name: "isList"
            value:
              type: ":Boolean"
              value: false
          - name: "isMap"
            value:
              type: ":Boolean"
              value: false
          - name: "isDice"
            value:
              type: ":Boolean"
              value: false
          - name: "isRange"
            value:
              type: ":Boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":Boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":Boolean"
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
module atomiccheckfrompoint
on Start {
  let source be :Point(4, 5, 6)
  emit Done(isNothing: source is nothing, isBoolean: source is :Boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :Percentage, isText: source is :Text, isTag: source is :Tag, isMeter: source is :Quantity(m), isVector: source is :Vector, isPoint: source is :Point, isList: source is :List, isMap: source is :Map, isDice: source is :Dice, isRange: source is :Range, isMessage: source is :Message, isHandler: source is :Handler, isSeries: source is :Series)
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
              type: ":Boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":Boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":Boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":Boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":Boolean"
              value: false
          - name: "isText"
            value:
              type: ":Boolean"
              value: false
          - name: "isTag"
            value:
              type: ":Boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":Boolean"
              value: false
          - name: "isVector"
            value:
              type: ":Boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":Boolean"
              value: true
          - name: "isList"
            value:
              type: ":Boolean"
              value: false
          - name: "isMap"
            value:
              type: ":Boolean"
              value: false
          - name: "isDice"
            value:
              type: ":Boolean"
              value: false
          - name: "isRange"
            value:
              type: ":Boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":Boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":Boolean"
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
module atomiccheckfromlist
on Start {
  let source be [1, 2, 3]
  emit Done(isNothing: source is nothing, isBoolean: source is :Boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :Percentage, isText: source is :Text, isTag: source is :Tag, isMeter: source is :Quantity(m), isVector: source is :Vector, isPoint: source is :Point, isList: source is :List, isMap: source is :Map, isDice: source is :Dice, isRange: source is :Range, isMessage: source is :Message, isHandler: source is :Handler, isSeries: source is :Series)
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
              type: ":Boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":Boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":Boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":Boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":Boolean"
              value: false
          - name: "isText"
            value:
              type: ":Boolean"
              value: false
          - name: "isTag"
            value:
              type: ":Boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":Boolean"
              value: false
          - name: "isVector"
            value:
              type: ":Boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":Boolean"
              value: false
          - name: "isList"
            value:
              type: ":Boolean"
              value: true
          - name: "isMap"
            value:
              type: ":Boolean"
              value: false
          - name: "isDice"
            value:
              type: ":Boolean"
              value: false
          - name: "isRange"
            value:
              type: ":Boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":Boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":Boolean"
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
module atomiccheckfrommap
on Start {
  let source be [x: 1, y: 2, z: 3]
  emit Done(isNothing: source is nothing, isBoolean: source is :Boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :Percentage, isText: source is :Text, isTag: source is :Tag, isMeter: source is :Quantity(m), isVector: source is :Vector, isPoint: source is :Point, isList: source is :List, isMap: source is :Map, isDice: source is :Dice, isRange: source is :Range, isMessage: source is :Message, isHandler: source is :Handler, isSeries: source is :Series)
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
              type: ":Boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":Boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":Boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":Boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":Boolean"
              value: false
          - name: "isText"
            value:
              type: ":Boolean"
              value: false
          - name: "isTag"
            value:
              type: ":Boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":Boolean"
              value: false
          - name: "isVector"
            value:
              type: ":Boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":Boolean"
              value: false
          - name: "isList"
            value:
              type: ":Boolean"
              value: false
          - name: "isMap"
            value:
              type: ":Boolean"
              value: true
          - name: "isDice"
            value:
              type: ":Boolean"
              value: false
          - name: "isRange"
            value:
              type: ":Boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":Boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":Boolean"
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
module atomiccheckfromdice
on Start {
  let source be ([3, 2, 1]) as :Dice
  emit Done(isNothing: source is nothing, isBoolean: source is :Boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :Percentage, isText: source is :Text, isTag: source is :Tag, isMeter: source is :Quantity(m), isVector: source is :Vector, isPoint: source is :Point, isList: source is :List, isMap: source is :Map, isDice: source is :Dice, isRange: source is :Range, isMessage: source is :Message, isHandler: source is :Handler, isSeries: source is :Series)
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
              type: ":Boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":Boolean"
              value: true
          - name: "isInteger"
            value:
              type: ":Boolean"
              value: true
          - name: "isFractional"
            value:
              type: ":Boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":Boolean"
              value: false
          - name: "isText"
            value:
              type: ":Boolean"
              value: false
          - name: "isTag"
            value:
              type: ":Boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":Boolean"
              value: false
          - name: "isVector"
            value:
              type: ":Boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":Boolean"
              value: false
          - name: "isList"
            value:
              type: ":Boolean"
              value: false
          - name: "isMap"
            value:
              type: ":Boolean"
              value: false
          - name: "isDice"
            value:
              type: ":Boolean"
              value: true
          - name: "isRange"
            value:
              type: ":Boolean"
              value: false
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":Boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":Boolean"
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
module atomiccheckfromrange
on Start {
  let source be (from 1 to 3) as :Range
  emit Done(isNothing: source is nothing, isBoolean: source is :Boolean, isNumeric: source is numeric, isInteger: source is integer, isFractional: source is fractional, isPercentage: source is :Percentage, isText: source is :Text, isTag: source is :Tag, isMeter: source is :Quantity(m), isVector: source is :Vector, isPoint: source is :Point, isList: source is :List, isMap: source is :Map, isDice: source is :Dice, isRange: source is :Range, isMessage: source is :Message, isHandler: source is :Handler, isSeries: source is :Series)
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
              type: ":Boolean"
              value: false
          - name: "isBoolean"
            value:
              type: ":Boolean"
              value: false
          - name: "isNumeric"
            value:
              type: ":Boolean"
              value: false
          - name: "isInteger"
            value:
              type: ":Boolean"
              value: false
          - name: "isFractional"
            value:
              type: ":Boolean"
              value: false
          - name: "isPercentage"
            value:
              type: ":Boolean"
              value: false
          - name: "isText"
            value:
              type: ":Boolean"
              value: false
          - name: "isTag"
            value:
              type: ":Boolean"
              value: false
          - name: "isMeter"
            value:
              type: ":Boolean"
              value: false
          - name: "isVector"
            value:
              type: ":Boolean"
              value: false
          - name: "isPoint"
            value:
              type: ":Boolean"
              value: false
          - name: "isList"
            value:
              type: ":Boolean"
              value: false
          - name: "isMap"
            value:
              type: ":Boolean"
              value: false
          - name: "isDice"
            value:
              type: ":Boolean"
              value: false
          - name: "isRange"
            value:
              type: ":Boolean"
              value: true
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: false
          - name: "isHandler"
            value:
              type: ":Boolean"
              value: false
          - name: "isSeries"
            value:
              type: ":Boolean"
              value: false
```
