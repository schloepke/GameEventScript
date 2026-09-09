---
formatVersion: 1
suiteId: runtime.text-literal-conversion
title: Text literals and exact numeric conversion
kind: scriptApi
level: atomic
categories: [conformance]
---

# Text literals and exact numeric conversion

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

These cases independently assert literal recognition, exact numeric storage, Percentage ratios, original-Text fallback, and context-dependent Text formatting. Numeric output is verified by roundtrip rather than one implementation-selected decimal spelling.

---

## Test: numeric-text-grammar

This case checks the following contract: Accepted spellings use exact decimal scaling, invariant separators, and the correct target value kind. Commas are structural for parse but grouping is supported by explicit numeric casts.

### Case description

```yaml
gesBlock: case
id: "numeric-text-grammar"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start(value) { emit Done(number: value as :Number, percentage: value as :Percentage, parsed: parse value) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| input-01 | Start | completion | |
| input-02 | Start | completion | |
| input-03 | Start | completion | |
| input-04 | Start | completion | |
| input-05 | Start | completion | |
| input-06 | Start | completion | |
| input-07 | Start | completion | |
| input-08 | Start | completion | |
| input-09 | Start | completion | |
| input-10 | Start | completion | |
| input-11 | Start | completion | |
| input-12 | Start | completion | |
| input-13 | Start | completion | |
| input-14 | Start | completion | |
| input-15 | Start | completion | |
| input-16 | Start | completion | |
| input-17 | Start | completion | |
| input-18 | Start | completion | |
| input-19 | Start | completion | |
| input-20 | Start | completion | |
| input-21 | Start | completion | |
| input-22 | Start | completion | |
| input-23 | Start | completion | |
| input-24 | Start | completion | |
| input-25 | Start | completion | |
| input-26 | Start | completion | |
| input-27 | Start | completion | |
| input-28 | Start | completion | |
| input-29 | Start | completion | |
| input-30 | Start | completion | |
| input-31 | Start | completion | |
| input-32 | Start | completion | |
| input-33 | Start | completion | |
| input-34 | Start | completion | |
| input-35 | Start | completion | |
| input-36 | Start | completion | |
| input-37 | Start | completion | |
| input-38 | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  input-01:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1000"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "1000"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1000"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "1000"
    runtimeLimits:
      exclude:
        - any: true
  input-02:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "100.2"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "100.2"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "100.2"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "100.2"
    runtimeLimits:
      exclude:
        - any: true
  input-03:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1.02e20"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "1.02e20"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1.02e20"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "1.02e20"
    runtimeLimits:
      exclude:
        - any: true
  input-04:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1e03"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "1000"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1000"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "1000"
    runtimeLimits:
      exclude:
        - any: true
  input-05:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1E+003"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "1000"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1000"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "1000"
    runtimeLimits:
      exclude:
        - any: true
  input-06:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "+001000.00"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "1000"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1000"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "1000"
    runtimeLimits:
      exclude:
        - any: true
  input-07:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "-1000"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "-1000"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "-1000"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "-1000"
    runtimeLimits:
      exclude:
        - any: true
  input-08:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1_003.33"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "1003.33"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1003.33"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "1003.33"
    runtimeLimits:
      exclude:
        - any: true
  input-09:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1_223.32"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "1223.32"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1223.32"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "1223.32"
    runtimeLimits:
      exclude:
        - any: true
  input-10:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1000.2_5"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "1000.25"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1000.25"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "1000.25"
    runtimeLimits:
      exclude:
        - any: true
  input-11:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1e0_3"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "1000"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1000"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "1000"
    runtimeLimits:
      exclude:
        - any: true
  input-12:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1,003.24"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "1003.24"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1003.24"
          - name: "parsed"
            value:
              type: ":Text"
              value: "1,003.24"
    runtimeLimits:
      exclude:
        - any: true
  input-13:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1,234,567.89"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "1234567.89"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1234567.89"
          - name: "parsed"
            value:
              type: ":Text"
              value: "1,234,567.89"
    runtimeLimits:
      exclude:
        - any: true
  input-14:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "01,002"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "1002"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1002"
          - name: "parsed"
            value:
              type: ":Text"
              value: "01,002"
    runtimeLimits:
      exclude:
        - any: true
  input-15:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "  1000\t"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "1000"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1000"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "1000"
    runtimeLimits:
      exclude:
        - any: true
  input-16:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "10m"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: "m"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Quantity.int64"
              value: "10"
              unit: "m"
    runtimeLimits:
      exclude:
        - any: true
  input-17:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1e03m"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Quantity.int64"
              value: "1000"
              unit: "m"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Quantity.int64"
              value: "1000"
              unit: "m"
    runtimeLimits:
      exclude:
        - any: true
  input-18:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "100.2s"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Quantity.binary64"
              value: "100.2"
              unit: "s"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Quantity.binary64"
              value: "100.2"
              unit: "s"
    runtimeLimits:
      exclude:
        - any: true
  input-19:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "90°"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Quantity.int64"
              value: "90"
              unit: "°"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Quantity.int64"
              value: "90"
              unit: "°"
    runtimeLimits:
      exclude:
        - any: true
  input-20:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "10%"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "0.1"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "0.1"
    runtimeLimits:
      exclude:
        - any: true
  input-21:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1e1%"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "0.1"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "0.1"
    runtimeLimits:
      exclude:
        - any: true
  input-22:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "100%"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "1"
    runtimeLimits:
      exclude:
        - any: true
  input-23:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "102%"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "1.02"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1.02"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "1.02"
    runtimeLimits:
      exclude:
        - any: true
  input-24:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1%"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "0.01"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0.01"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "0.01"
    runtimeLimits:
      exclude:
        - any: true
  input-25:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "0.01"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "0.01"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0.01"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "0.01"
    runtimeLimits:
      exclude:
        - any: true
  input-26:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "-1%"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "-0.01"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "-0.01"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "-0.01"
    runtimeLimits:
      exclude:
        - any: true
  input-27:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "-20"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "-20"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "-20"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "-20"
    runtimeLimits:
      exclude:
        - any: true
  input-28:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "20"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "20"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "20"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "20"
    runtimeLimits:
      exclude:
        - any: true
  input-29:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "-0%"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "0"
    runtimeLimits:
      exclude:
        - any: true
  input-30:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1000.01"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "1000.01"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1000.01"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "1000.01"
    runtimeLimits:
      exclude:
        - any: true
  input-31:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1.00001e03"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "1000.01"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1000.01"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "1000.01"
    runtimeLimits:
      exclude:
        - any: true
  input-32:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1.00001e02"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "100.001"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "100.001"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "100.001"
    runtimeLimits:
      exclude:
        - any: true
  input-33:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "Infinity"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "Infinity"
    runtimeLimits:
      exclude:
        - any: true
  input-34:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "-Infinity"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "-Infinity"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "-Infinity"
    runtimeLimits:
      exclude:
        - any: true
  input-35:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "+Infinity"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "Infinity"
    runtimeLimits:
      exclude:
        - any: true
  input-36:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "Infinitym"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Quantity.binary64"
              value: "Infinity"
              unit: "m"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Quantity.binary64"
              value: "Infinity"
              unit: "m"
    runtimeLimits:
      exclude:
        - any: true
  input-37:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "NaN"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Nothing"
    runtimeLimits:
      exclude:
        - any: true
  input-38:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "NaN%"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Nothing"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: invalid-number-text-and-fallback

This case checks the following contract: Explicit numeric casts reject invalid numeric text. Parse preserves the complete original Text unless another complete literal is recognized; line breaks are parse trivia but not numeric-cast padding.

### Case description

```yaml
gesBlock: case
id: "invalid-number-text-and-fallback"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start(value) { emit Done(number: value as :Number, percentage: value as :Percentage, parsed: parse value) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| input-01 | Start | completion | |
| input-02 | Start | completion | |
| input-03 | Start | completion | |
| input-04 | Start | completion | |
| input-05 | Start | completion | |
| input-06 | Start | completion | |
| input-07 | Start | completion | |
| input-08 | Start | completion | |
| input-09 | Start | completion | |
| input-10 | Start | completion | |
| input-11 | Start | completion | |
| input-12 | Start | completion | |
| input-13 | Start | completion | |
| input-14 | Start | completion | |
| input-15 | Start | completion | |
| input-16 | Start | completion | |
| input-17 | Start | completion | |
| input-18 | Start | completion | |
| input-19 | Start | completion | |
| input-20 | Start | completion | |
| input-21 | Start | completion | |
| input-22 | Start | completion | |
| input-23 | Start | completion | |
| input-24 | Start | completion | |
| input-25 | Start | completion | |
| input-26 | Start | completion | |
| input-27 | Start | completion | |
| input-28 | Start | completion | |
| input-29 | Start | completion | |
| input-30 | Start | completion | |
| input-31 | Start | completion | |
| input-32 | Start | completion | |
| input-33 | Start | completion | |
| input-34 | Start | completion | |
| input-35 | Start | completion | |
| input-36 | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  input-01:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ""
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: ""
    runtimeLimits:
      exclude:
        - any: true
  input-02:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: " "
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: " "
    runtimeLimits:
      exclude:
        - any: true
  input-03:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "abc"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "abc"
    runtimeLimits:
      exclude:
        - any: true
  input-04:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "true"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-05:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "false"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Boolean"
              value: false
    runtimeLimits:
      exclude:
        - any: true
  input-06:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "12,34"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "12,34"
    runtimeLimits:
      exclude:
        - any: true
  input-07:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "100,2"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "100,2"
    runtimeLimits:
      exclude:
        - any: true
  input-08:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1__000"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "1__000"
    runtimeLimits:
      exclude:
        - any: true
  input-09:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "_100"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "_100"
    runtimeLimits:
      exclude:
        - any: true
  input-10:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "100_"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "100_"
    runtimeLimits:
      exclude:
        - any: true
  input-11:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1_.25"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "1_.25"
    runtimeLimits:
      exclude:
        - any: true
  input-12:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1._25"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "1._25"
    runtimeLimits:
      exclude:
        - any: true
  input-13:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1,_000"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "1,_000"
    runtimeLimits:
      exclude:
        - any: true
  input-14:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1,000.2_5"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "1,000.2_5"
    runtimeLimits:
      exclude:
        - any: true
  input-15:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1_000,000"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "1_000,000"
    runtimeLimits:
      exclude:
        - any: true
  input-16:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1.0,000"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "1.0,000"
    runtimeLimits:
      exclude:
        - any: true
  input-17:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1e1,000"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "1e1,000"
    runtimeLimits:
      exclude:
        - any: true
  input-18:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ".5"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: ".5"
    runtimeLimits:
      exclude:
        - any: true
  input-19:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1."
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "1."
    runtimeLimits:
      exclude:
        - any: true
  input-20:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1e"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "1e"
    runtimeLimits:
      exclude:
        - any: true
  input-21:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1e+"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "1e+"
    runtimeLimits:
      exclude:
        - any: true
  input-22:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1000-"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "1000-"
    runtimeLimits:
      exclude:
        - any: true
  input-23:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "10 m"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "10 m"
    runtimeLimits:
      exclude:
        - any: true
  input-24:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "10ms"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "10ms"
    runtimeLimits:
      exclude:
        - any: true
  input-25:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1%%"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "1%%"
    runtimeLimits:
      exclude:
        - any: true
  input-26:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "+NaN"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "+NaN"
    runtimeLimits:
      exclude:
        - any: true
  input-27:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "nan"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "nan"
    runtimeLimits:
      exclude:
        - any: true
  input-28:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "infinity"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "infinity"
    runtimeLimits:
      exclude:
        - any: true
  input-29:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "0x10"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "0x10"
    runtimeLimits:
      exclude:
        - any: true
  input-30:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "１２"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "１２"
    runtimeLimits:
      exclude:
        - any: true
  input-31:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "١٢"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "١٢"
    runtimeLimits:
      exclude:
        - any: true
  input-32:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1\n"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "1"
    runtimeLimits:
      exclude:
        - any: true
  input-33:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: " 1"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: " 1"
    runtimeLimits:
      exclude:
        - any: true
  input-34:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1 "
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "1 "
    runtimeLimits:
      exclude:
        - any: true
  input-35:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1 000"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "1 000"
    runtimeLimits:
      exclude:
        - any: true
  input-36:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1e_2"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Nothing"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "1e_2"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: exact-decimal-boundaries

This case checks the following contract: Fixed independent decimal inputs cover Int64 extrema, values above 2^53, ties-to-even, underflow, overflow, long significands, and Percentage scaling before rounding.

### Case description

```yaml
gesBlock: case
id: "exact-decimal-boundaries"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start(value) { emit Done(number: value as :Number, percentage: value as :Percentage, parsed: parse value) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| boundary-01 | Start | completion | |
| boundary-02 | Start | completion | |
| boundary-03 | Start | completion | |
| boundary-04 | Start | completion | |
| boundary-05 | Start | completion | |
| boundary-06 | Start | completion | |
| boundary-07 | Start | completion | |
| boundary-08 | Start | completion | |
| boundary-09 | Start | completion | |
| boundary-10 | Start | completion | |
| boundary-11 | Start | completion | |
| boundary-12 | Start | completion | |
| boundary-13 | Start | completion | |
| boundary-14 | Start | completion | |
| boundary-15 | Start | completion | |
| boundary-16 | Start | completion | |
| boundary-17 | Start | completion | |
| boundary-18 | Start | completion | |
| boundary-19 | Start | completion | |
| boundary-20 | Start | completion | |
| boundary-21 | Start | completion | |
| boundary-22 | Start | completion | |
| boundary-23 | Start | completion | |
| boundary-24 | Start | completion | |
| boundary-25 | Start | completion | |
| boundary-26 | Start | completion | |
| boundary-27 | Start | completion | |
| boundary-28 | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  boundary-01:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "9007199254740993"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "9007199254740993"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "9007199254740992"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "9007199254740993"
    runtimeLimits:
      exclude:
        - any: true
  boundary-02:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "9007199254740993.0"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "9007199254740993"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "9007199254740992"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "9007199254740993"
    runtimeLimits:
      exclude:
        - any: true
  boundary-03:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "9007199254740993e0"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "9007199254740993"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "9007199254740992"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "9007199254740993"
    runtimeLimits:
      exclude:
        - any: true
  boundary-04:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "9223372036854775807"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "9223372036854775807"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "9.223372036854776e18"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "9223372036854775807"
    runtimeLimits:
      exclude:
        - any: true
  boundary-05:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "9223372036854775807.0"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "9223372036854775807"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "9.223372036854776e18"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "9223372036854775807"
    runtimeLimits:
      exclude:
        - any: true
  boundary-06:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "9223372036854775807e0"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "9223372036854775807"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "9.223372036854776e18"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "9223372036854775807"
    runtimeLimits:
      exclude:
        - any: true
  boundary-07:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "-9223372036854775808"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "-9223372036854775808"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "-9.223372036854776e18"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "-9223372036854775808"
    runtimeLimits:
      exclude:
        - any: true
  boundary-08:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "-9223372036854775808.0"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "-9223372036854775808"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "-9.223372036854776e18"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "-9223372036854775808"
    runtimeLimits:
      exclude:
        - any: true
  boundary-09:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "9223372036854775808"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "9.223372036854776e18"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "9.223372036854776e18"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "9.223372036854776e18"
    runtimeLimits:
      exclude:
        - any: true
  boundary-10:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "9223372036854775809"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "9.223372036854776e18"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "9.223372036854776e18"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "9.223372036854776e18"
    runtimeLimits:
      exclude:
        - any: true
  boundary-11:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "-9223372036854775809"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "-9223372036854775808"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "-9.223372036854776e18"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "-9223372036854775808"
    runtimeLimits:
      exclude:
        - any: true
  boundary-12:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "92233720368547758070e-1"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "9223372036854775807"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "9.223372036854776e18"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "9223372036854775807"
    runtimeLimits:
      exclude:
        - any: true
  boundary-13:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "900719925474099300%"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "9007199254740993"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "9007199254740992"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "9007199254740992"
    runtimeLimits:
      exclude:
        - any: true
  boundary-14:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1.00000000000000011102230246251565404236316680908203125"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "1"
    runtimeLimits:
      exclude:
        - any: true
  boundary-15:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1.00000000000000033306690738754696212708950042724609375"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "1.0000000000000004"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1.0000000000000004"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "1.0000000000000004"
    runtimeLimits:
      exclude:
        - any: true
  boundary-16:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "2.4703282292062327208828439643411068618252990130716238221279284125e-324"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "0"
    runtimeLimits:
      exclude:
        - any: true
  boundary-17:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "2.4703282292062328e-324"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "5e-324"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "5e-324"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "5e-324"
    runtimeLimits:
      exclude:
        - any: true
  boundary-18:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "5e-324"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "5e-324"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "5e-324"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "5e-324"
    runtimeLimits:
      exclude:
        - any: true
  boundary-19:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "2.2250738585072014e-308"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "2.2250738585072014e-308"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "2.2250738585072014e-308"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "2.2250738585072014e-308"
    runtimeLimits:
      exclude:
        - any: true
  boundary-20:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1.7976931348623157e308"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "1.7976931348623157e308"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1.7976931348623157e308"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "1.7976931348623157e308"
    runtimeLimits:
      exclude:
        - any: true
  boundary-21:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1.7976931348623157e310%"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "1.7976931348623157e308"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1.7976931348623157e308"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "1.7976931348623157e308"
    runtimeLimits:
      exclude:
        - any: true
  boundary-22:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1.7976931348623159e308"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "Infinity"
    runtimeLimits:
      exclude:
        - any: true
  boundary-23:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1e99999999999999999999999"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "percentage"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "Infinity"
    runtimeLimits:
      exclude:
        - any: true
  boundary-24:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1e-99999999999999999999999"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "0"
    runtimeLimits:
      exclude:
        - any: true
  boundary-25:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "0e99999999999999999999999"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "0"
    runtimeLimits:
      exclude:
        - any: true
  boundary-26:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1_000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000e-273"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "1"
    runtimeLimits:
      exclude:
        - any: true
  boundary-27:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000%"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "1e298"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1e298"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "1e298"
    runtimeLimits:
      exclude:
        - any: true
  boundary-28:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1.005%"
    local:
      - name: "Done"
        args:
          - name: "number"
            value:
              type: ":Number.binary64"
              value: "0.01005"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "0.01005"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "0.01005"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: percentage-exact-roundtrips

This case checks the following contract: Every path preserves the exact Percentage kind and ratio, including integer Number views, subnormals, negative values, and magnitudes whose displayed percentage exceeds Binary64.

### Case description

```yaml
gesBlock: case
id: "percentage-exact-roundtrips"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start(value) {
  emit Done(direct: value as :Percentage,
    number: (value as :Number) as :Percentage,
    text: (value as :Text) as :Percentage,
    textNumber: ((value as :Text) as :Number) as :Percentage,
    numberText: ((value as :Number) as :Text) as :Percentage,
    parsed: parse (value as :Text))
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| ratio-01 | Start | completion | |
| ratio-02 | Start | completion | |
| ratio-03 | Start | completion | |
| ratio-04 | Start | completion | |
| ratio-05 | Start | completion | |
| ratio-06 | Start | completion | |
| ratio-07 | Start | completion | |
| ratio-08 | Start | completion | |
| ratio-09 | Start | completion | |
| ratio-10 | Start | completion | |
| ratio-11 | Start | completion | |
| ratio-12 | Start | completion | |
| ratio-13 | Start | completion | |
| ratio-14 | Start | completion | |
| ratio-15 | Start | completion | |
| ratio-16 | Start | completion | |
| ratio-17 | Start | completion | |
| ratio-18 | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  ratio-01:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "0"
    local:
      - name: "Done"
        args:
          - name: "direct"
            value:
              type: ":Percentage"
              value: "0"
          - name: "number"
            value:
              type: ":Percentage"
              value: "0"
          - name: "text"
            value:
              type: ":Percentage"
              value: "0"
          - name: "textNumber"
            value:
              type: ":Percentage"
              value: "0"
          - name: "numberText"
            value:
              type: ":Percentage"
              value: "0"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "0"
    runtimeLimits:
      exclude:
        - any: true
  ratio-02:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "0.01"
    local:
      - name: "Done"
        args:
          - name: "direct"
            value:
              type: ":Percentage"
              value: "0.01"
          - name: "number"
            value:
              type: ":Percentage"
              value: "0.01"
          - name: "text"
            value:
              type: ":Percentage"
              value: "0.01"
          - name: "textNumber"
            value:
              type: ":Percentage"
              value: "0.01"
          - name: "numberText"
            value:
              type: ":Percentage"
              value: "0.01"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "0.01"
    runtimeLimits:
      exclude:
        - any: true
  ratio-03:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "0.1"
    local:
      - name: "Done"
        args:
          - name: "direct"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "number"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "text"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "textNumber"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "numberText"
            value:
              type: ":Percentage"
              value: "0.1"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "0.1"
    runtimeLimits:
      exclude:
        - any: true
  ratio-04:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "0.5"
    local:
      - name: "Done"
        args:
          - name: "direct"
            value:
              type: ":Percentage"
              value: "0.5"
          - name: "number"
            value:
              type: ":Percentage"
              value: "0.5"
          - name: "text"
            value:
              type: ":Percentage"
              value: "0.5"
          - name: "textNumber"
            value:
              type: ":Percentage"
              value: "0.5"
          - name: "numberText"
            value:
              type: ":Percentage"
              value: "0.5"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "0.5"
    runtimeLimits:
      exclude:
        - any: true
  ratio-05:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "1"
    local:
      - name: "Done"
        args:
          - name: "direct"
            value:
              type: ":Percentage"
              value: "1"
          - name: "number"
            value:
              type: ":Percentage"
              value: "1"
          - name: "text"
            value:
              type: ":Percentage"
              value: "1"
          - name: "textNumber"
            value:
              type: ":Percentage"
              value: "1"
          - name: "numberText"
            value:
              type: ":Percentage"
              value: "1"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "1"
    runtimeLimits:
      exclude:
        - any: true
  ratio-06:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "1.02"
    local:
      - name: "Done"
        args:
          - name: "direct"
            value:
              type: ":Percentage"
              value: "1.02"
          - name: "number"
            value:
              type: ":Percentage"
              value: "1.02"
          - name: "text"
            value:
              type: ":Percentage"
              value: "1.02"
          - name: "textNumber"
            value:
              type: ":Percentage"
              value: "1.02"
          - name: "numberText"
            value:
              type: ":Percentage"
              value: "1.02"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "1.02"
    runtimeLimits:
      exclude:
        - any: true
  ratio-07:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "20"
    local:
      - name: "Done"
        args:
          - name: "direct"
            value:
              type: ":Percentage"
              value: "20"
          - name: "number"
            value:
              type: ":Percentage"
              value: "20"
          - name: "text"
            value:
              type: ":Percentage"
              value: "20"
          - name: "textNumber"
            value:
              type: ":Percentage"
              value: "20"
          - name: "numberText"
            value:
              type: ":Percentage"
              value: "20"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "20"
    runtimeLimits:
      exclude:
        - any: true
  ratio-08:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "-0.01"
    local:
      - name: "Done"
        args:
          - name: "direct"
            value:
              type: ":Percentage"
              value: "-0.01"
          - name: "number"
            value:
              type: ":Percentage"
              value: "-0.01"
          - name: "text"
            value:
              type: ":Percentage"
              value: "-0.01"
          - name: "textNumber"
            value:
              type: ":Percentage"
              value: "-0.01"
          - name: "numberText"
            value:
              type: ":Percentage"
              value: "-0.01"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "-0.01"
    runtimeLimits:
      exclude:
        - any: true
  ratio-09:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "-1"
    local:
      - name: "Done"
        args:
          - name: "direct"
            value:
              type: ":Percentage"
              value: "-1"
          - name: "number"
            value:
              type: ":Percentage"
              value: "-1"
          - name: "text"
            value:
              type: ":Percentage"
              value: "-1"
          - name: "textNumber"
            value:
              type: ":Percentage"
              value: "-1"
          - name: "numberText"
            value:
              type: ":Percentage"
              value: "-1"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "-1"
    runtimeLimits:
      exclude:
        - any: true
  ratio-10:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "-1.02"
    local:
      - name: "Done"
        args:
          - name: "direct"
            value:
              type: ":Percentage"
              value: "-1.02"
          - name: "number"
            value:
              type: ":Percentage"
              value: "-1.02"
          - name: "text"
            value:
              type: ":Percentage"
              value: "-1.02"
          - name: "textNumber"
            value:
              type: ":Percentage"
              value: "-1.02"
          - name: "numberText"
            value:
              type: ":Percentage"
              value: "-1.02"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "-1.02"
    runtimeLimits:
      exclude:
        - any: true
  ratio-11:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "-20"
    local:
      - name: "Done"
        args:
          - name: "direct"
            value:
              type: ":Percentage"
              value: "-20"
          - name: "number"
            value:
              type: ":Percentage"
              value: "-20"
          - name: "text"
            value:
              type: ":Percentage"
              value: "-20"
          - name: "textNumber"
            value:
              type: ":Percentage"
              value: "-20"
          - name: "numberText"
            value:
              type: ":Percentage"
              value: "-20"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "-20"
    runtimeLimits:
      exclude:
        - any: true
  ratio-12:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "5e-324"
    local:
      - name: "Done"
        args:
          - name: "direct"
            value:
              type: ":Percentage"
              value: "5e-324"
          - name: "number"
            value:
              type: ":Percentage"
              value: "5e-324"
          - name: "text"
            value:
              type: ":Percentage"
              value: "5e-324"
          - name: "textNumber"
            value:
              type: ":Percentage"
              value: "5e-324"
          - name: "numberText"
            value:
              type: ":Percentage"
              value: "5e-324"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "5e-324"
    runtimeLimits:
      exclude:
        - any: true
  ratio-13:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "-5e-324"
    local:
      - name: "Done"
        args:
          - name: "direct"
            value:
              type: ":Percentage"
              value: "-5e-324"
          - name: "number"
            value:
              type: ":Percentage"
              value: "-5e-324"
          - name: "text"
            value:
              type: ":Percentage"
              value: "-5e-324"
          - name: "textNumber"
            value:
              type: ":Percentage"
              value: "-5e-324"
          - name: "numberText"
            value:
              type: ":Percentage"
              value: "-5e-324"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "-5e-324"
    runtimeLimits:
      exclude:
        - any: true
  ratio-14:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "2.2250738585072014e-308"
    local:
      - name: "Done"
        args:
          - name: "direct"
            value:
              type: ":Percentage"
              value: "2.2250738585072014e-308"
          - name: "number"
            value:
              type: ":Percentage"
              value: "2.2250738585072014e-308"
          - name: "text"
            value:
              type: ":Percentage"
              value: "2.2250738585072014e-308"
          - name: "textNumber"
            value:
              type: ":Percentage"
              value: "2.2250738585072014e-308"
          - name: "numberText"
            value:
              type: ":Percentage"
              value: "2.2250738585072014e-308"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "2.2250738585072014e-308"
    runtimeLimits:
      exclude:
        - any: true
  ratio-15:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "1.7976931348623157e308"
    local:
      - name: "Done"
        args:
          - name: "direct"
            value:
              type: ":Percentage"
              value: "1.7976931348623157e308"
          - name: "number"
            value:
              type: ":Percentage"
              value: "1.7976931348623157e308"
          - name: "text"
            value:
              type: ":Percentage"
              value: "1.7976931348623157e308"
          - name: "textNumber"
            value:
              type: ":Percentage"
              value: "1.7976931348623157e308"
          - name: "numberText"
            value:
              type: ":Percentage"
              value: "1.7976931348623157e308"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "1.7976931348623157e308"
    runtimeLimits:
      exclude:
        - any: true
  ratio-16:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "-1.7976931348623157e308"
    local:
      - name: "Done"
        args:
          - name: "direct"
            value:
              type: ":Percentage"
              value: "-1.7976931348623157e308"
          - name: "number"
            value:
              type: ":Percentage"
              value: "-1.7976931348623157e308"
          - name: "text"
            value:
              type: ":Percentage"
              value: "-1.7976931348623157e308"
          - name: "textNumber"
            value:
              type: ":Percentage"
              value: "-1.7976931348623157e308"
          - name: "numberText"
            value:
              type: ":Percentage"
              value: "-1.7976931348623157e308"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "-1.7976931348623157e308"
    runtimeLimits:
      exclude:
        - any: true
  ratio-17:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "1.152921504606847e18"
    local:
      - name: "Done"
        args:
          - name: "direct"
            value:
              type: ":Percentage"
              value: "1.152921504606847e18"
          - name: "number"
            value:
              type: ":Percentage"
              value: "1.152921504606847e18"
          - name: "text"
            value:
              type: ":Percentage"
              value: "1.152921504606847e18"
          - name: "textNumber"
            value:
              type: ":Percentage"
              value: "1.152921504606847e18"
          - name: "numberText"
            value:
              type: ":Percentage"
              value: "1.152921504606847e18"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "1.152921504606847e18"
    runtimeLimits:
      exclude:
        - any: true
  ratio-18:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "9.223372036854775e18"
    local:
      - name: "Done"
        args:
          - name: "direct"
            value:
              type: ":Percentage"
              value: "9.223372036854775e18"
          - name: "number"
            value:
              type: ":Percentage"
              value: "9.223372036854775e18"
          - name: "text"
            value:
              type: ":Percentage"
              value: "9.223372036854775e18"
          - name: "textNumber"
            value:
              type: ":Percentage"
              value: "9.223372036854775e18"
          - name: "numberText"
            value:
              type: ":Percentage"
              value: "9.223372036854775e18"
          - name: "parsed"
            value:
              type: ":Percentage"
              value: "9.223372036854775e18"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: uniform-percentage-ratios

This case checks the following contract: Magnitude, sign, and integer storage never select a second interpretation of Percentage casts.

### Case description

```yaml
gesBlock: case
id: "uniform-percentage-ratios"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start(value) { emit Done(value: value as :Percentage) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| input-01 | Start | completion | |
| input-02 | Start | completion | |
| input-03 | Start | completion | |
| input-04 | Start | completion | |
| input-05 | Start | completion | |
| input-06 | Start | completion | |
| input-07 | Start | completion | |
| input-08 | Start | completion | |
| input-09 | Start | completion | |
| input-10 | Start | completion | |
| input-11 | Start | completion | |
| input-12 | Start | completion | |
| input-13 | Start | completion | |
| input-14 | Start | completion | |
| input-15 | Start | completion | |
| input-16 | Start | completion | |
| input-17 | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  input-01:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.int64"
            value: "0"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Percentage"
              value: "0"
    runtimeLimits:
      exclude:
        - any: true
  input-02:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.int64"
            value: "1"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Percentage"
              value: "1"
    runtimeLimits:
      exclude:
        - any: true
  input-03:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.int64"
            value: "20"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Percentage"
              value: "20"
    runtimeLimits:
      exclude:
        - any: true
  input-04:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.int64"
            value: "-20"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Percentage"
              value: "-20"
    runtimeLimits:
      exclude:
        - any: true
  input-05:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.binary64"
            value: "0.1"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Percentage"
              value: "0.1"
    runtimeLimits:
      exclude:
        - any: true
  input-06:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.binary64"
            value: "1.02"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Percentage"
              value: "1.02"
    runtimeLimits:
      exclude:
        - any: true
  input-07:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.binary64"
            value: "-1.02"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Percentage"
              value: "-1.02"
    runtimeLimits:
      exclude:
        - any: true
  input-08:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.int64"
            value: "9007199254740993"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Percentage"
              value: "9007199254740992"
    runtimeLimits:
      exclude:
        - any: true
  input-09:
    input:
      args:
        - name: "value"
          value:
            type: ":Boolean"
            value: true
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Percentage"
              value: "1"
    runtimeLimits:
      exclude:
        - any: true
  input-10:
    input:
      args:
        - name: "value"
          value:
            type: ":Boolean"
            value: false
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Percentage"
              value: "0"
    runtimeLimits:
      exclude:
        - any: true
  input-11:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Percentage"
              value: "1"
    runtimeLimits:
      exclude:
        - any: true
  input-12:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1%"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Percentage"
              value: "0.01"
    runtimeLimits:
      exclude:
        - any: true
  input-13:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "102%"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Percentage"
              value: "1.02"
    runtimeLimits:
      exclude:
        - any: true
  input-14:
    input:
      args:
        - name: "value"
          value:
            type: ":Quantity.int64"
            value: "1"
            unit: "m"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Nothing"
    runtimeLimits:
      exclude:
        - any: true
  input-15:
    input:
      args:
        - name: "value"
          value:
            type: ":Nothing"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Nothing"
    runtimeLimits:
      exclude:
        - any: true
  input-16:
    input:
      args:
        - name: "value"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Nothing"
    runtimeLimits:
      exclude:
        - any: true
  input-17:
    input:
      args:
        - name: "value"
          value:
            type: ":Dice"
            rolls:
              - 2
              - 3
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Percentage"
              value: "5"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: number-quantity-text-roundtrips

This case checks the following contract: Numeric output may select decimal or exponent notation; exact canonical value and unit are required on both reading paths.

### Case description

```yaml
gesBlock: case
id: "number-quantity-text-roundtrips"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start(value) { let output be value as :Text; emit Done(isText: output is :Text, cast: output as :Number, parsed: parse output) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| value-01 | Start | completion | |
| value-02 | Start | completion | |
| value-03 | Start | completion | |
| value-04 | Start | completion | |
| value-05 | Start | completion | |
| value-06 | Start | completion | |
| value-07 | Start | completion | |
| value-08 | Start | completion | |
| value-09 | Start | completion | |
| value-10 | Start | completion | |
| value-11 | Start | completion | |
| value-12 | Start | completion | |
| value-13 | Start | completion | |
| value-14 | Start | completion | |
| value-15 | Start | completion | |
| value-16 | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  value-01:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.int64"
            value: "0"
    local:
      - name: "Done"
        args:
          - name: "isText"
            value:
              type: ":Boolean"
              value: true
          - name: "cast"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "0"
    runtimeLimits:
      exclude:
        - any: true
  value-02:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.int64"
            value: "1"
    local:
      - name: "Done"
        args:
          - name: "isText"
            value:
              type: ":Boolean"
              value: true
          - name: "cast"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "1"
    runtimeLimits:
      exclude:
        - any: true
  value-03:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.int64"
            value: "-1"
    local:
      - name: "Done"
        args:
          - name: "isText"
            value:
              type: ":Boolean"
              value: true
          - name: "cast"
            value:
              type: ":Number.int64"
              value: "-1"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "-1"
    runtimeLimits:
      exclude:
        - any: true
  value-04:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.int64"
            value: "9007199254740993"
    local:
      - name: "Done"
        args:
          - name: "isText"
            value:
              type: ":Boolean"
              value: true
          - name: "cast"
            value:
              type: ":Number.int64"
              value: "9007199254740993"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "9007199254740993"
    runtimeLimits:
      exclude:
        - any: true
  value-05:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.int64"
            value: "9223372036854775807"
    local:
      - name: "Done"
        args:
          - name: "isText"
            value:
              type: ":Boolean"
              value: true
          - name: "cast"
            value:
              type: ":Number.int64"
              value: "9223372036854775807"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "9223372036854775807"
    runtimeLimits:
      exclude:
        - any: true
  value-06:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.int64"
            value: "-9223372036854775808"
    local:
      - name: "Done"
        args:
          - name: "isText"
            value:
              type: ":Boolean"
              value: true
          - name: "cast"
            value:
              type: ":Number.int64"
              value: "-9223372036854775808"
          - name: "parsed"
            value:
              type: ":Number.int64"
              value: "-9223372036854775808"
    runtimeLimits:
      exclude:
        - any: true
  value-07:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.binary64"
            value: "1000.01"
    local:
      - name: "Done"
        args:
          - name: "isText"
            value:
              type: ":Boolean"
              value: true
          - name: "cast"
            value:
              type: ":Number.binary64"
              value: "1000.01"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "1000.01"
    runtimeLimits:
      exclude:
        - any: true
  value-08:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.binary64"
            value: "1.02e20"
    local:
      - name: "Done"
        args:
          - name: "isText"
            value:
              type: ":Boolean"
              value: true
          - name: "cast"
            value:
              type: ":Number.binary64"
              value: "1.02e20"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "1.02e20"
    runtimeLimits:
      exclude:
        - any: true
  value-09:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.binary64"
            value: "5e-324"
    local:
      - name: "Done"
        args:
          - name: "isText"
            value:
              type: ":Boolean"
              value: true
          - name: "cast"
            value:
              type: ":Number.binary64"
              value: "5e-324"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "5e-324"
    runtimeLimits:
      exclude:
        - any: true
  value-10:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.binary64"
            value: "-5e-324"
    local:
      - name: "Done"
        args:
          - name: "isText"
            value:
              type: ":Boolean"
              value: true
          - name: "cast"
            value:
              type: ":Number.binary64"
              value: "-5e-324"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "-5e-324"
    runtimeLimits:
      exclude:
        - any: true
  value-11:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.binary64"
            value: "1.7976931348623157e308"
    local:
      - name: "Done"
        args:
          - name: "isText"
            value:
              type: ":Boolean"
              value: true
          - name: "cast"
            value:
              type: ":Number.binary64"
              value: "1.7976931348623157e308"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "1.7976931348623157e308"
    runtimeLimits:
      exclude:
        - any: true
  value-12:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.binary64"
            value: "Infinity"
    local:
      - name: "Done"
        args:
          - name: "isText"
            value:
              type: ":Boolean"
              value: true
          - name: "cast"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "Infinity"
    runtimeLimits:
      exclude:
        - any: true
  value-13:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.binary64"
            value: "-Infinity"
    local:
      - name: "Done"
        args:
          - name: "isText"
            value:
              type: ":Boolean"
              value: true
          - name: "cast"
            value:
              type: ":Number.binary64"
              value: "-Infinity"
          - name: "parsed"
            value:
              type: ":Number.binary64"
              value: "-Infinity"
    runtimeLimits:
      exclude:
        - any: true
  value-14:
    input:
      args:
        - name: "value"
          value:
            type: ":Quantity.int64"
            value: "9007199254740993"
            unit: "m"
    local:
      - name: "Done"
        args:
          - name: "isText"
            value:
              type: ":Boolean"
              value: true
          - name: "cast"
            value:
              type: ":Quantity.int64"
              value: "9007199254740993"
              unit: "m"
          - name: "parsed"
            value:
              type: ":Quantity.int64"
              value: "9007199254740993"
              unit: "m"
    runtimeLimits:
      exclude:
        - any: true
  value-15:
    input:
      args:
        - name: "value"
          value:
            type: ":Quantity.binary64"
            value: "1.02e20"
            unit: "s"
    local:
      - name: "Done"
        args:
          - name: "isText"
            value:
              type: ":Boolean"
              value: true
          - name: "cast"
            value:
              type: ":Quantity.binary64"
              value: "1.02e20"
              unit: "s"
          - name: "parsed"
            value:
              type: ":Quantity.binary64"
              value: "1.02e20"
              unit: "s"
    runtimeLimits:
      exclude:
        - any: true
  value-16:
    input:
      args:
        - name: "value"
          value:
            type: ":Quantity.int64"
            value: "90"
            unit: "°"
    local:
      - name: "Done"
        args:
          - name: "isText"
            value:
              type: ":Boolean"
              value: true
          - name: "cast"
            value:
              type: ":Quantity.int64"
              value: "90"
              unit: "°"
          - name: "parsed"
            value:
              type: ":Quantity.int64"
              value: "90"
              unit: "°"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: literal-recognition-and-exact-fallback

This case checks the following contract: Recognition consumes one complete literal. Quoted Text is decoded once, ordinary or malformed input falls back exactly, and nested invalid values do not become implicit Text.

### Case description

```yaml
gesBlock: case
id: "literal-recognition-and-exact-fallback"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start(value) { emit Done(value: parse value) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| input-01 | Start | completion | |
| input-02 | Start | completion | |
| input-03 | Start | completion | |
| input-04 | Start | completion | |
| input-05 | Start | completion | |
| input-06 | Start | completion | |
| input-07 | Start | completion | |
| input-08 | Start | completion | |
| input-09 | Start | completion | |
| input-10 | Start | completion | |
| input-11 | Start | completion | |
| input-12 | Start | completion | |
| input-13 | Start | completion | |
| input-14 | Start | completion | |
| input-15 | Start | completion | |
| input-16 | Start | completion | |
| input-17 | Start | completion | |
| input-18 | Start | completion | |
| input-19 | Start | completion | |
| input-20 | Start | completion | |
| input-21 | Start | completion | |
| input-22 | Start | completion | |
| input-23 | Start | completion | |
| input-24 | Start | completion | |
| input-25 | Start | completion | |
| input-26 | Start | completion | |
| input-27 | Start | completion | |
| input-28 | Start | completion | |
| input-29 | Start | completion | |
| input-30 | Start | completion | |
| input-31 | Start | completion | |
| input-32 | Start | completion | |
| input-33 | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  input-01:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "Hello"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "Hello"
    runtimeLimits:
      exclude:
        - any: true
  input-02:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "  Hello\r\n"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "  Hello\r\n"
    runtimeLimits:
      exclude:
        - any: true
  input-03:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "\"123\""
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "123"
    runtimeLimits:
      exclude:
        - any: true
  input-04:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "\"true\""
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "true"
    runtimeLimits:
      exclude:
        - any: true
  input-05:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "'nothing'"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "nothing"
    runtimeLimits:
      exclude:
        - any: true
  input-06:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "\"\""
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: ""
    runtimeLimits:
      exclude:
        - any: true
  input-07:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "\"say \"\"hi\"\"\""
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "say \"hi\""
    runtimeLimits:
      exclude:
        - any: true
  input-08:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "'didn''t'"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "didn't"
    runtimeLimits:
      exclude:
        - any: true
  input-09:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "\"😀é\nhello\""
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "😀é\nhello"
    runtimeLimits:
      exclude:
        - any: true
  input-10:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "\"\\n\""
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "\\n"
    runtimeLimits:
      exclude:
        - any: true
  input-11:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "  \"Hello\"\n"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "Hello"
    runtimeLimits:
      exclude:
        - any: true
  input-12:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[1, \"1\", true, nothing]"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Text"
                  value: "1"
                - type: ":Boolean"
                  value: true
                - type: ":Nothing"
    runtimeLimits:
      exclude:
        - any: true
  input-13:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[z: [\"a,b\", \"[x]\"], a: [flag:]]"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Map"
              entries:
                - key: "a"
                  value:
                    type: ":Map"
                    entries:
                      - key: "flag"
                        value:
                          type: ":Boolean"
                          value: true
                - key: "z"
                  value:
                    type: ":List"
                    items:
                      - type: ":Text"
                        value: "a,b"
                      - type: ":Text"
                        value: "[x]"
    runtimeLimits:
      exclude:
        - any: true
  input-14:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[]"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":List"
              items: []
    runtimeLimits:
      exclude:
        - any: true
  input-15:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[:]"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Map"
              entries: []
    runtimeLimits:
      exclude:
        - any: true
  input-16:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[a: 1, a: 2]"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Map"
              entries:
                - key: "a"
                  value:
                    type: ":Number.int64"
                    value: "2"
    runtimeLimits:
      exclude:
        - any: true
  input-17:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[1,003]"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "3"
    runtimeLimits:
      exclude:
        - any: true
  input-18:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "#ready"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Tag"
              value: "ready"
    runtimeLimits:
      exclude:
        - any: true
  input-19:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "\"Hello"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "\"Hello"
    runtimeLimits:
      exclude:
        - any: true
  input-20:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "\"Hello'"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "\"Hello'"
    runtimeLimits:
      exclude:
        - any: true
  input-21:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "\"a\"b\""
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "\"a\"b\""
    runtimeLimits:
      exclude:
        - any: true
  input-22:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "\"a\" \"b\""
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "\"a\" \"b\""
    runtimeLimits:
      exclude:
        - any: true
  input-23:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[1, broken]"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "[1, broken]"
    runtimeLimits:
      exclude:
        - any: true
  input-24:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[1, 2,]"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "[1, 2,]"
    runtimeLimits:
      exclude:
        - any: true
  input-25:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[x: 1,]"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "[x: 1,]"
    runtimeLimits:
      exclude:
        - any: true
  input-26:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[x: \"bad]"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "[x: \"bad]"
    runtimeLimits:
      exclude:
        - any: true
  input-27:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[1 + 2]"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "[1 + 2]"
    runtimeLimits:
      exclude:
        - any: true
  input-28:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1 extra"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "1 extra"
    runtimeLimits:
      exclude:
        - any: true
  input-29:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "random from 1 to 6"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "random from 1 to 6"
    runtimeLimits:
      exclude:
        - any: true
  input-30:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Text(\"x\")"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: ":Text(\"x\")"
    runtimeLimits:
      exclude:
        - any: true
  input-31:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "﻿1"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "﻿1"
    runtimeLimits:
      exclude:
        - any: true
  input-32:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1 // comment"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "1 // comment"
    runtimeLimits:
      exclude:
        - any: true
  input-33:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: " \"x\""
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: " \"x\""
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: parse-non-text

This case checks the following contract: A non-Text operand produces nothing without stringifying or evaluating the value.

### Case description

```yaml
gesBlock: case
id: "parse-non-text"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start(value) { emit Done(value: parse value) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| input-01 | Start | completion | |
| input-02 | Start | completion | |
| input-03 | Start | completion | |
| input-04 | Start | completion | |
| input-05 | Start | completion | |
| input-06 | Start | completion | |
| input-07 | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  input-01:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.int64"
            value: "1"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Nothing"
    runtimeLimits:
      exclude:
        - any: true
  input-02:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "0.01"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Nothing"
    runtimeLimits:
      exclude:
        - any: true
  input-03:
    input:
      args:
        - name: "value"
          value:
            type: ":Boolean"
            value: true
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Nothing"
    runtimeLimits:
      exclude:
        - any: true
  input-04:
    input:
      args:
        - name: "value"
          value:
            type: ":Nothing"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Nothing"
    runtimeLimits:
      exclude:
        - any: true
  input-05:
    input:
      args:
        - name: "value"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Nothing"
    runtimeLimits:
      exclude:
        - any: true
  input-06:
    input:
      args:
        - name: "value"
          value:
            type: ":Map"
            entries:
              - key: "a"
                value:
                  type: ":Number.int64"
                  value: "1"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Nothing"
    runtimeLimits:
      exclude:
        - any: true
  input-07:
    input:
      args:
        - name: "value"
          value:
            type: ":Tag"
            value: "ready"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Nothing"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: root-and-nested-text-formatting

This case checks the following contract: Root Text is unchanged; container Text is quoted with doubled delimiters. Booleans, absence, empty containers, and sorted Map fields have fixed readable spellings.

### Case description

```yaml
gesBlock: case
id: "root-and-nested-text-formatting"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start(value) { emit Done(value: value as :Text) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| value-01 | Start | completion | |
| value-02 | Start | completion | |
| value-03 | Start | completion | |
| value-04 | Start | completion | |
| value-05 | Start | completion | |
| value-06 | Start | completion | |
| value-07 | Start | completion | |
| value-08 | Start | completion | |
| value-09 | Start | completion | |
| value-10 | Start | completion | |
| value-11 | Start | completion | |
| value-12 | Start | completion | |
| value-13 | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  value-01:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "Hello"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "Hello"
    runtimeLimits:
      exclude:
        - any: true
  value-02:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "123"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "123"
    runtimeLimits:
      exclude:
        - any: true
  value-03:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "true"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "true"
    runtimeLimits:
      exclude:
        - any: true
  value-04:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "\"quoted\""
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "\"quoted\""
    runtimeLimits:
      exclude:
        - any: true
  value-05:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ""
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: ""
    runtimeLimits:
      exclude:
        - any: true
  value-06:
    input:
      args:
        - name: "value"
          value:
            type: ":Boolean"
            value: true
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "true"
    runtimeLimits:
      exclude:
        - any: true
  value-07:
    input:
      args:
        - name: "value"
          value:
            type: ":Boolean"
            value: false
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "false"
    runtimeLimits:
      exclude:
        - any: true
  value-08:
    input:
      args:
        - name: "value"
          value:
            type: ":Nothing"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "nothing"
    runtimeLimits:
      exclude:
        - any: true
  value-09:
    input:
      args:
        - name: "value"
          value:
            type: ":Tag"
            value: "ready"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "#ready"
    runtimeLimits:
      exclude:
        - any: true
  value-10:
    input:
      args:
        - name: "value"
          value:
            type: ":List"
            items: []
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "[]"
    runtimeLimits:
      exclude:
        - any: true
  value-11:
    input:
      args:
        - name: "value"
          value:
            type: ":Map"
            entries: []
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "[:]"
    runtimeLimits:
      exclude:
        - any: true
  value-12:
    input:
      args:
        - name: "value"
          value:
            type: ":List"
            items:
              - type: ":Text"
                value: "1"
              - type: ":Text"
                value: "say \"hi\""
              - type: ":Text"
                value: "a,b"
              - type: ":Boolean"
                value: true
              - type: ":Nothing"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "[\"1\", \"say \"\"hi\"\"\", \"a,b\", true, nothing]"
    runtimeLimits:
      exclude:
        - any: true
  value-13:
    input:
      args:
        - name: "value"
          value:
            type: ":Map"
            entries:
              - key: "a"
                value:
                  type: ":List"
                  items:
                    - type: ":Text"
                      value: "😀\n"
                    - type: ":Boolean"
                      value: false
              - key: "z"
                value:
                  type: ":Text"
                  value: "Ada"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "[a: [\"😀\n\", false], z: \"Ada\"]"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: text-truth-view-stays-independent

This case checks the following contract: Boolean casts keep their established truth view; explicit quoted literal decoding belongs to parse.

### Case description

```yaml
gesBlock: case
id: "text-truth-view-stays-independent"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start(value) { emit Done(value: value as :Boolean) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| text-01 | Start | completion | |
| text-02 | Start | completion | |
| text-03 | Start | completion | |
| text-04 | Start | completion | |
| text-05 | Start | completion | |
| text-06 | Start | completion | |
| text-07 | Start | completion | |
| text-08 | Start | completion | |
| text-09 | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  text-01:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "true"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  text-02:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "TRUE"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  text-03:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "True"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  text-04:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  text-05:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "false"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Boolean"
              value: false
    runtimeLimits:
      exclude:
        - any: true
  text-06:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "0"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Boolean"
              value: false
    runtimeLimits:
      exclude:
        - any: true
  text-07:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: " true "
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Boolean"
              value: false
    runtimeLimits:
      exclude:
        - any: true
  text-08:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "abc"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Boolean"
              value: false
    runtimeLimits:
      exclude:
        - any: true
  text-09:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "\"true\""
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Boolean"
              value: false
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: prefix-precedence

This case checks the following contract: Parse binds at unary precedence; parentheses explicitly include a cast in its operand.

### Case description

```yaml
gesBlock: case
id: "prefix-precedence"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start(value) { emit Done(beforeCast: parse value as :Text, parenthesized: parse (value as :Text)) }
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
            value: "\"123\""
    local:
      - name: "Done"
        args:
          - name: "beforeCast"
            value:
              type: ":Text"
              value: "123"
          - name: "parenthesized"
            value:
              type: ":Text"
              value: "123"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: compiler-literal-01

This case checks the following contract: Literal, program-wide constant, and dynamic Text conversion preserve the same exact numeric storage and value.

### Case description

```yaml
gesBlock: case
id: "compiler-literal-01"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
constant $original be 9007199254740993.0
on Start(value) { emit Done(literal: 9007199254740993.0, declared: $original, cast: value as :Number) }
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
            value: "9007199254740993.0"
    local:
      - name: "Done"
        args:
          - name: "literal"
            value:
              type: ":Number.int64"
              value: "9007199254740993"
          - name: "declared"
            value:
              type: ":Number.int64"
              value: "9007199254740993"
          - name: "cast"
            value:
              type: ":Number.int64"
              value: "9007199254740993"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: compiler-literal-02

This case checks the following contract: Literal, program-wide constant, and dynamic Text conversion preserve the same exact numeric storage and value.

### Case description

```yaml
gesBlock: case
id: "compiler-literal-02"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
constant $original be 9223372036854775807.0
on Start(value) { emit Done(literal: 9223372036854775807.0, declared: $original, cast: value as :Number) }
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
            value: "9223372036854775807.0"
    local:
      - name: "Done"
        args:
          - name: "literal"
            value:
              type: ":Number.int64"
              value: "9223372036854775807"
          - name: "declared"
            value:
              type: ":Number.int64"
              value: "9223372036854775807"
          - name: "cast"
            value:
              type: ":Number.int64"
              value: "9223372036854775807"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: compiler-literal-03

This case checks the following contract: Literal, program-wide constant, and dynamic Text conversion preserve the same exact numeric storage and value.

### Case description

```yaml
gesBlock: case
id: "compiler-literal-03"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
constant $original be -9223372036854775808.0
on Start(value) { emit Done(literal: -9223372036854775808.0, declared: $original, cast: value as :Number) }
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
            value: "-9223372036854775808.0"
    local:
      - name: "Done"
        args:
          - name: "literal"
            value:
              type: ":Number.int64"
              value: "-9223372036854775808"
          - name: "declared"
            value:
              type: ":Number.int64"
              value: "-9223372036854775808"
          - name: "cast"
            value:
              type: ":Number.int64"
              value: "-9223372036854775808"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: compiler-literal-04

This case checks the following contract: Literal, program-wide constant, and dynamic Text conversion preserve the same exact numeric storage and value.

### Case description

```yaml
gesBlock: case
id: "compiler-literal-04"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
constant $original be 9007199254740993.0m
on Start(value) { emit Done(literal: 9007199254740993.0m, declared: $original, cast: value as :Number) }
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
            value: "9007199254740993.0m"
    local:
      - name: "Done"
        args:
          - name: "literal"
            value:
              type: ":Quantity.int64"
              value: "9007199254740993"
              unit: "m"
          - name: "declared"
            value:
              type: ":Quantity.int64"
              value: "9007199254740993"
              unit: "m"
          - name: "cast"
            value:
              type: ":Quantity.int64"
              value: "9007199254740993"
              unit: "m"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: compiler-literal-05

This case checks the following contract: Literal, program-wide constant, and dynamic Text conversion preserve the same exact numeric storage and value.

### Case description

```yaml
gesBlock: case
id: "compiler-literal-05"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
constant $original be 1_003.33
on Start(value) { emit Done(literal: 1_003.33, declared: $original, cast: value as :Number) }
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
            value: "1_003.33"
    local:
      - name: "Done"
        args:
          - name: "literal"
            value:
              type: ":Number.binary64"
              value: "1003.33"
          - name: "declared"
            value:
              type: ":Number.binary64"
              value: "1003.33"
          - name: "cast"
            value:
              type: ":Number.binary64"
              value: "1003.33"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: compiler-literal-06

This case checks the following contract: Literal, program-wide constant, and dynamic Text conversion preserve the same exact numeric storage and value.

### Case description

```yaml
gesBlock: case
id: "compiler-literal-06"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
constant $original be 1.005%
on Start(value) { emit Done(literal: 1.005%, declared: $original, cast: value as :Percentage) }
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
            value: "1.005%"
    local:
      - name: "Done"
        args:
          - name: "literal"
            value:
              type: ":Percentage"
              value: "0.01005"
          - name: "declared"
            value:
              type: ":Percentage"
              value: "0.01005"
          - name: "cast"
            value:
              type: ":Percentage"
              value: "0.01005"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: compiler-literal-07

This case checks the following contract: Literal, program-wide constant, and dynamic Text conversion preserve the same exact numeric storage and value.

### Case description

```yaml
gesBlock: case
id: "compiler-literal-07"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
constant $original be 900719925474099300%
on Start(value) { emit Done(literal: 900719925474099300%, declared: $original, cast: value as :Percentage) }
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
            value: "900719925474099300%"
    local:
      - name: "Done"
        args:
          - name: "literal"
            value:
              type: ":Percentage"
              value: "9007199254740992"
          - name: "declared"
            value:
              type: ":Percentage"
              value: "9007199254740992"
          - name: "cast"
            value:
              type: ":Percentage"
              value: "9007199254740992"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: parse-depth-1

This case checks the following contract: Exactly the supported nesting boundary is accepted without exposing parser state to other instructions.

### Case description

```yaml
gesBlock: case
id: "parse-depth-1"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start(value) { let parsed be parse value; emit Done(isList: parsed is :List, text: parsed as :Text) }
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
            value: "[0]"
    local:
      - name: "Done"
        args:
          - name: "isList"
            value:
              type: ":Boolean"
              value: true
          - name: "text"
            value:
              type: ":Text"
              value: "[0]"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: parse-depth-64

This case checks the following contract: Exactly the supported nesting boundary is accepted without exposing parser state to other instructions.

### Case description

```yaml
gesBlock: case
id: "parse-depth-64"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start(value) { let parsed be parse value; emit Done(isList: parsed is :List, text: parsed as :Text) }
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
            value: "[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[0]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]"
    local:
      - name: "Done"
        args:
          - name: "isList"
            value:
              type: ":Boolean"
              value: true
          - name: "text"
            value:
              type: ":Text"
              value: "[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[0]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: parse-depth-limit

This case checks the following contract: The first excess nested container stops the handler. An otherwise unused parse must not be removed by optimization.

### Case description

```yaml
gesBlock: case
id: "parse-depth-limit"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start(value) { emit Before; parse value; emit After }
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
            value: "[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[0]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]"
    local:
      - name: "Before"
        args: []
    runtimeLimits:
      include:
        - name: "MaxLiteralDepth"
```

---

## Test: items-at-limit

This case checks the following contract: Input is built by portable script operations, keeping the boundary test in Markdown without storing a large fixture string.

### Case description

```yaml
gesBlock: case
id: "items-at-limit"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
runtimeLimits:
  maxExecutionSteps: 3000000
  maxLoopIterations: 200000
  maxRangeItems: 200000
  maxGeneratedCollectionItems: 200000
```

### Source code under test

```ges
on Start {
  let input be :List[:select item from 1 to 65536 => 0] as :Text
  emit Before
  let parsed be parse input
  emit Done(count: parsed[:count])
}
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
      args: []
    local:
      - name: "Before"
        args: []
      - name: "Done"
        args:
          - name: "count"
            value:
              type: ":Number.int64"
              value: "65536"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: items-over-limit

This case checks the following contract: Input is built by portable script operations, keeping the boundary test in Markdown without storing a large fixture string.

### Case description

```yaml
gesBlock: case
id: "items-over-limit"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
runtimeLimits:
  maxExecutionSteps: 3000000
  maxLoopIterations: 200000
  maxRangeItems: 200000
  maxGeneratedCollectionItems: 200000
```

### Source code under test

```ges
on Start {
  let input be :List[:select item from 1 to 65537 => 0] as :Text
  emit Before
  let parsed be parse input
  emit Done(count: parsed[:count])
}
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
      args: []
    local:
      - name: "Before"
        args: []
    runtimeLimits:
      include:
        - name: "MaxLiteralItems"
```

---

## Test: input-over-limit

This case checks the following contract: Input is built by portable script operations, keeping the boundary test in Markdown without storing a large fixture string.

### Case description

```yaml
gesBlock: case
id: "input-over-limit"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
runtimeLimits:
  maxExecutionSteps: 3000000
  maxLoopIterations: 200000
  maxRangeItems: 200000
  maxGeneratedCollectionItems: 200000
```

### Source code under test

```ges
on Start {
  let input be :List[:select item from 1 to 104858 => "abcdef"] as :Text
  emit Before
  let parsed be parse input
  emit Done(count: parsed[:count])
}
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
      args: []
    local:
      - name: "Before"
        args: []
    runtimeLimits:
      include:
        - name: "MaxLiteralInputScalars"
```

---

## Test: input-scalars-at-limit

This case checks the following contract: Successive doubling builds exactly 1,048,576 Unicode scalars without a large corpus fixture; supplementary characters occupy two UTF-16 code units each.

### Case description

```yaml
gesBlock: case
id: "input-scalars-at-limit"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start(value) {
  let chunk_0 be value
  let chunk_1 be chunk_0 + chunk_0
  let chunk_2 be chunk_1 + chunk_1
  let chunk_3 be chunk_2 + chunk_2
  let chunk_4 be chunk_3 + chunk_3
  let chunk_5 be chunk_4 + chunk_4
  let chunk_6 be chunk_5 + chunk_5
  let chunk_7 be chunk_6 + chunk_6
  let chunk_8 be chunk_7 + chunk_7
  let chunk_9 be chunk_8 + chunk_8
  let chunk_10 be chunk_9 + chunk_9
  let chunk_11 be chunk_10 + chunk_10
  let chunk_12 be chunk_11 + chunk_11
  let chunk_13 be chunk_12 + chunk_12
  let chunk_14 be chunk_13 + chunk_13
  let chunk_15 be chunk_14 + chunk_14
  let chunk_16 be chunk_15 + chunk_15
  let chunk_17 be chunk_16 + chunk_16
  emit Before
  let parsed be parse (chunk_17)
  emit Done(count: parsed[:count])
}
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
            value: "😀😀😀😀😀😀😀😀"
    local:
      - name: "Before"
        args: []
      - name: "Done"
        args:
          - name: "count"
            value:
              type: ":Number.int64"
              value: "1048576"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: input-scalars-over-limit

This case checks the following contract: Successive doubling builds exactly 1,048,576 Unicode scalars without a large corpus fixture; supplementary characters occupy two UTF-16 code units each.

### Case description

```yaml
gesBlock: case
id: "input-scalars-over-limit"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start(value) {
  let chunk_0 be value
  let chunk_1 be chunk_0 + chunk_0
  let chunk_2 be chunk_1 + chunk_1
  let chunk_3 be chunk_2 + chunk_2
  let chunk_4 be chunk_3 + chunk_3
  let chunk_5 be chunk_4 + chunk_4
  let chunk_6 be chunk_5 + chunk_5
  let chunk_7 be chunk_6 + chunk_6
  let chunk_8 be chunk_7 + chunk_7
  let chunk_9 be chunk_8 + chunk_8
  let chunk_10 be chunk_9 + chunk_9
  let chunk_11 be chunk_10 + chunk_10
  let chunk_12 be chunk_11 + chunk_11
  let chunk_13 be chunk_12 + chunk_12
  let chunk_14 be chunk_13 + chunk_13
  let chunk_15 be chunk_14 + chunk_14
  let chunk_16 be chunk_15 + chunk_15
  let chunk_17 be chunk_16 + chunk_16
  emit Before
  let parsed be parse (chunk_17 + "😀")
  emit Done(count: parsed[:count])
}
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
            value: "😀😀😀😀😀😀😀😀"
    local:
      - name: "Before"
        args: []
    runtimeLimits:
      include:
        - name: "MaxLiteralInputScalars"
```

---

## Test: parse-limit-recovery

This case checks the following contract: A limit fault stops only its current handler; the next message parses with fresh counters.

### Case description

```yaml
gesBlock: case
id: "parse-limit-recovery"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start(value) { emit Before; let parsed be parse value; emit Done(value: parsed) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| limited | Start | completion | |
| recovered | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  limited:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[0]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]"
    local:
      - name: "Before"
        args: []
    runtimeLimits:
      include:
        - name: "MaxLiteralDepth"
  recovered:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[1, \"1\"]"
    local:
      - name: "Before"
        args: []
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Text"
                  value: "1"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: nested-data-roundtrips

This case checks the following contract: Exact recursive transport comparison distinguishes quoted Text, large Int64, Quantity, Percentage, absence, and nested containers; script numeric equality is not the oracle.

### Case description

```yaml
gesBlock: case
id: "nested-data-roundtrips"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start(value) { emit Done(value: parse (value as :Text)) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| value-1 | Start | completion | |
| value-2 | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  value-1:
    input:
      args:
        - name: "value"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "9007199254740993"
              - type: ":Text"
                value: "9007199254740993"
              - type: ":Percentage"
                value: "1.02"
              - type: ":Map"
                entries:
                  - key: "flag"
                    value:
                      type: ":Boolean"
                      value: false
                  - key: "missing"
                    value:
                      type: ":Nothing"
                  - key: "unit"
                    value:
                      type: ":Quantity.binary64"
                      value: "1.02e20"
                      unit: "m"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "9007199254740993"
                - type: ":Text"
                  value: "9007199254740993"
                - type: ":Percentage"
                  value: "1.02"
                - type: ":Map"
                  entries:
                    - key: "flag"
                      value:
                        type: ":Boolean"
                        value: false
                    - key: "missing"
                      value:
                        type: ":Nothing"
                    - key: "unit"
                      value:
                        type: ":Quantity.binary64"
                        value: "1.02e20"
                        unit: "m"
    runtimeLimits:
      exclude:
        - any: true
  value-2:
    input:
      args:
        - name: "value"
          value:
            type: ":Map"
            entries:
              - key: "a"
                value:
                  type: ":List"
                  items:
                    - type: ":Text"
                      value: "\"quoted\""
                    - type: ":Text"
                      value: ""
                    - type: ":Number.binary64"
                      value: "5e-324"
                    - type: ":Percentage"
                      value: "5e-324"
              - key: "z"
                value:
                  type: ":Tag"
                  value: "ready2"
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Map"
              entries:
                - key: "a"
                  value:
                    type: ":List"
                    items:
                      - type: ":Text"
                        value: "\"quoted\""
                      - type: ":Text"
                        value: ""
                      - type: ":Number.binary64"
                        value: "5e-324"
                      - type: ":Percentage"
                        value: "5e-324"
                - key: "z"
                  value:
                    type: ":Tag"
                    value: "ready2"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: percentage-literal-overflow

This case checks the following contract: Non-finite Percentage decoding canonicalizes to nothing for both compiler literals and runtime Text conversion.

### Case description

```yaml
gesBlock: case
id: "percentage-literal-overflow"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start(value) { emit Done(literal: 999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999%, cast: value as :Percentage) }
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
            value: "999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999%"
    local:
      - name: "Done"
        args:
          - name: "literal"
            value:
              type: ":Nothing"
          - name: "cast"
            value:
              type: ":Nothing"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: duplicate-items-at-limit

This case checks the following contract: Every textual Map entry consumes one shared item slot, including overwritten duplicate keys and containers nested beneath a root entry.

### Case description

```yaml
gesBlock: case
id: "duplicate-items-at-limit"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start {
  let chunk_0 be "a: 0, "
  let chunk_1 be chunk_0 + chunk_0
  let chunk_2 be chunk_1 + chunk_1
  let chunk_3 be chunk_2 + chunk_2
  let chunk_4 be chunk_3 + chunk_3
  let chunk_5 be chunk_4 + chunk_4
  let chunk_6 be chunk_5 + chunk_5
  let chunk_7 be chunk_6 + chunk_6
  let chunk_8 be chunk_7 + chunk_7
  let chunk_9 be chunk_8 + chunk_8
  let chunk_10 be chunk_9 + chunk_9
  let chunk_11 be chunk_10 + chunk_10
  let chunk_12 be chunk_11 + chunk_11
  let chunk_13 be chunk_12 + chunk_12
  let chunk_14 be chunk_13 + chunk_13
  let chunk_15 be chunk_14 + chunk_14
  let chunk_16 be chunk_15 + chunk_15
  let input be "[" + chunk_0 + chunk_1 + chunk_2 + chunk_3 + chunk_4 + chunk_5 + chunk_6 + chunk_7 + chunk_8 + chunk_9 + chunk_10 + chunk_11 + chunk_12 + chunk_13 + chunk_14 + chunk_15 + "a: 1]"
  emit Before
  let parsed be parse input
  emit Done(value: parsed)
}
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
      args: []
    local:
      - name: "Before"
        args: []
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Map"
              entries:
                - key: "a"
                  value:
                    type: ":Number.int64"
                    value: "1"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: duplicate-items-over-limit

This case checks the following contract: Every textual Map entry consumes one shared item slot, including overwritten duplicate keys and containers nested beneath a root entry.

### Case description

```yaml
gesBlock: case
id: "duplicate-items-over-limit"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start {
  let chunk_0 be "a: 0, "
  let chunk_1 be chunk_0 + chunk_0
  let chunk_2 be chunk_1 + chunk_1
  let chunk_3 be chunk_2 + chunk_2
  let chunk_4 be chunk_3 + chunk_3
  let chunk_5 be chunk_4 + chunk_4
  let chunk_6 be chunk_5 + chunk_5
  let chunk_7 be chunk_6 + chunk_6
  let chunk_8 be chunk_7 + chunk_7
  let chunk_9 be chunk_8 + chunk_8
  let chunk_10 be chunk_9 + chunk_9
  let chunk_11 be chunk_10 + chunk_10
  let chunk_12 be chunk_11 + chunk_11
  let chunk_13 be chunk_12 + chunk_12
  let chunk_14 be chunk_13 + chunk_13
  let chunk_15 be chunk_14 + chunk_14
  let chunk_16 be chunk_15 + chunk_15
  let input be "[" + chunk_16 + "a: 1]"
  emit Before
  let parsed be parse input
  emit Done(value: parsed)
}
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
      args: []
    local:
      - name: "Before"
        args: []
    runtimeLimits:
      include:
        - name: "MaxLiteralItems"
```

---

## Test: nested-items-at-limit

This case checks the following contract: Every textual Map entry consumes one shared item slot, including overwritten duplicate keys and containers nested beneath a root entry.

### Case description

```yaml
gesBlock: case
id: "nested-items-at-limit"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start {
  let chunk_0 be "a: 0, "
  let chunk_1 be chunk_0 + chunk_0
  let chunk_2 be chunk_1 + chunk_1
  let chunk_3 be chunk_2 + chunk_2
  let chunk_4 be chunk_3 + chunk_3
  let chunk_5 be chunk_4 + chunk_4
  let chunk_6 be chunk_5 + chunk_5
  let chunk_7 be chunk_6 + chunk_6
  let chunk_8 be chunk_7 + chunk_7
  let chunk_9 be chunk_8 + chunk_8
  let chunk_10 be chunk_9 + chunk_9
  let chunk_11 be chunk_10 + chunk_10
  let chunk_12 be chunk_11 + chunk_11
  let chunk_13 be chunk_12 + chunk_12
  let chunk_14 be chunk_13 + chunk_13
  let chunk_15 be chunk_14 + chunk_14
  let chunk_16 be chunk_15 + chunk_15
  let input be "[outer: [" + chunk_1 + chunk_2 + chunk_3 + chunk_4 + chunk_5 + chunk_6 + chunk_7 + chunk_8 + chunk_9 + chunk_10 + chunk_11 + chunk_12 + chunk_13 + chunk_14 + chunk_15 + "a: 1]]"
  emit Before
  let parsed be parse input
  emit Done(value: parsed)
}
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
      args: []
    local:
      - name: "Before"
        args: []
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Map"
              entries:
                - key: "outer"
                  value:
                    type: ":Map"
                    entries:
                      - key: "a"
                        value:
                          type: ":Number.int64"
                          value: "1"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: nested-items-over-limit

This case checks the following contract: Every textual Map entry consumes one shared item slot, including overwritten duplicate keys and containers nested beneath a root entry.

### Case description

```yaml
gesBlock: case
id: "nested-items-over-limit"
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
on Start {
  let chunk_0 be "a: 0, "
  let chunk_1 be chunk_0 + chunk_0
  let chunk_2 be chunk_1 + chunk_1
  let chunk_3 be chunk_2 + chunk_2
  let chunk_4 be chunk_3 + chunk_3
  let chunk_5 be chunk_4 + chunk_4
  let chunk_6 be chunk_5 + chunk_5
  let chunk_7 be chunk_6 + chunk_6
  let chunk_8 be chunk_7 + chunk_7
  let chunk_9 be chunk_8 + chunk_8
  let chunk_10 be chunk_9 + chunk_9
  let chunk_11 be chunk_10 + chunk_10
  let chunk_12 be chunk_11 + chunk_11
  let chunk_13 be chunk_12 + chunk_12
  let chunk_14 be chunk_13 + chunk_13
  let chunk_15 be chunk_14 + chunk_14
  let chunk_16 be chunk_15 + chunk_15
  let input be "[outer: [" + chunk_0 + chunk_1 + chunk_2 + chunk_3 + chunk_4 + chunk_5 + chunk_6 + chunk_7 + chunk_8 + chunk_9 + chunk_10 + chunk_11 + chunk_12 + chunk_13 + chunk_14 + chunk_15 + "a: 1]]"
  emit Before
  let parsed be parse input
  emit Done(value: parsed)
}
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
      args: []
    local:
      - name: "Before"
        args: []
    runtimeLimits:
      include:
        - name: "MaxLiteralItems"
```

---

## Test: r22-map-key-roundtrip-01

This case preserves a dynamically produced Map key and its values through Text, including delimiter-like content.

### Case description

```yaml
gesBlock: case
id: r22-map-key-roundtrip-01
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) {
  let grouped be [1][:group by x => value]
  let restored be parse (grouped as :Text)
  emit Done(equal: restored = grouped, count: restored[:count], item: restored[:values][1][1])
}
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
            value: "name"
    local:
      - name: Done
        args:
          - name: "equal"
            value:
              type: ":Boolean"
              value: true
          - name: "count"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "item"
            value:
              type: ":Number.int64"
              value: "1"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: r22-map-key-roundtrip-02

This case preserves a dynamically produced Map key and its values through Text, including delimiter-like content.

### Case description

```yaml
gesBlock: case
id: r22-map-key-roundtrip-02
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) {
  let grouped be [1][:group by x => value]
  let restored be parse (grouped as :Text)
  emit Done(equal: restored = grouped, count: restored[:count], item: restored[:values][1][1])
}
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
            value: "1"
    local:
      - name: Done
        args:
          - name: "equal"
            value:
              type: ":Boolean"
              value: true
          - name: "count"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "item"
            value:
              type: ":Number.int64"
              value: "1"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: r22-map-key-roundtrip-03

This case preserves a dynamically produced Map key and its values through Text, including delimiter-like content.

### Case description

```yaml
gesBlock: case
id: r22-map-key-roundtrip-03
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) {
  let grouped be [1][:group by x => value]
  let restored be parse (grouped as :Text)
  emit Done(equal: restored = grouped, count: restored[:count], item: restored[:values][1][1])
}
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
            value: "a: 1, b"
    local:
      - name: Done
        args:
          - name: "equal"
            value:
              type: ":Boolean"
              value: true
          - name: "count"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "item"
            value:
              type: ":Number.int64"
              value: "1"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: r22-map-key-roundtrip-04

This case preserves a dynamically produced Map key and its values through Text, including delimiter-like content.

### Case description

```yaml
gesBlock: case
id: r22-map-key-roundtrip-04
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) {
  let grouped be [1][:group by x => value]
  let restored be parse (grouped as :Text)
  emit Done(equal: restored = grouped, count: restored[:count], item: restored[:values][1][1])
}
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
            value: ""
    local:
      - name: Done
        args:
          - name: "equal"
            value:
              type: ":Boolean"
              value: true
          - name: "count"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "item"
            value:
              type: ":Number.int64"
              value: "1"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: r22-map-key-roundtrip-05

This case preserves a dynamically produced Map key and its values through Text, including delimiter-like content.

### Case description

```yaml
gesBlock: case
id: r22-map-key-roundtrip-05
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) {
  let grouped be [1][:group by x => value]
  let restored be parse (grouped as :Text)
  emit Done(equal: restored = grouped, count: restored[:count], item: restored[:values][1][1])
}
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
            value: "Ready"
    local:
      - name: Done
        args:
          - name: "equal"
            value:
              type: ":Boolean"
              value: true
          - name: "count"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "item"
            value:
              type: ":Number.int64"
              value: "1"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: r22-map-key-roundtrip-06

This case preserves a dynamically produced Map key and its values through Text, including delimiter-like content.

### Case description

```yaml
gesBlock: case
id: r22-map-key-roundtrip-06
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) {
  let grouped be [1][:group by x => value]
  let restored be parse (grouped as :Text)
  emit Done(equal: restored = grouped, count: restored[:count], item: restored[:values][1][1])
}
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
            value: "a_b"
    local:
      - name: Done
        args:
          - name: "equal"
            value:
              type: ":Boolean"
              value: true
          - name: "count"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "item"
            value:
              type: ":Number.int64"
              value: "1"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: r22-map-key-roundtrip-07

This case preserves a dynamically produced Map key and its values through Text, including delimiter-like content.

### Case description

```yaml
gesBlock: case
id: r22-map-key-roundtrip-07
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) {
  let grouped be [1][:group by x => value]
  let restored be parse (grouped as :Text)
  emit Done(equal: restored = grouped, count: restored[:count], item: restored[:values][1][1])
}
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
            value: "say \"hi\""
    local:
      - name: Done
        args:
          - name: "equal"
            value:
              type: ":Boolean"
              value: true
          - name: "count"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "item"
            value:
              type: ":Number.int64"
              value: "1"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: r22-map-key-roundtrip-08

This case preserves a dynamically produced Map key and its values through Text, including delimiter-like content.

### Case description

```yaml
gesBlock: case
id: r22-map-key-roundtrip-08
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) {
  let grouped be [1][:group by x => value]
  let restored be parse (grouped as :Text)
  emit Done(equal: restored = grouped, count: restored[:count], item: restored[:values][1][1])
}
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
            value: "a\nb\\c"
    local:
      - name: Done
        args:
          - name: "equal"
            value:
              type: ":Boolean"
              value: true
          - name: "count"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "item"
            value:
              type: ":Number.int64"
              value: "1"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: r22-map-key-roundtrip-09

This case preserves a dynamically produced Map key and its values through Text, including delimiter-like content.

### Case description

```yaml
gesBlock: case
id: r22-map-key-roundtrip-09
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) {
  let grouped be [1][:group by x => value]
  let restored be parse (grouped as :Text)
  emit Done(equal: restored = grouped, count: restored[:count], item: restored[:values][1][1])
}
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
            value: "\u96ea\ud83d\ude00"
    local:
      - name: Done
        args:
          - name: "equal"
            value:
              type: ":Boolean"
              value: true
          - name: "count"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "item"
            value:
              type: ":Number.int64"
              value: "1"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: r22-quoted-and-bare-duplicate-key

This case treats quoted and bare spellings of the same Map key identically and keeps the last value.

### Case description

```yaml
gesBlock: case
id: r22-quoted-and-bare-duplicate-key
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) {
  let restored be parse value
  emit Done(count: restored[:count], name: restored.name)
}
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
            value: "[\"name\": 1, name: 2]"
    local:
      - name: Done
        args:
          - name: "count"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "name"
            value:
              type: ":Number.int64"
              value: "2"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: r22-quoted-key-only

This case decodes doubled single quotes in a key and retains the key-only true shorthand.

### Case description

```yaml
gesBlock: case
id: r22-quoted-key-only
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) {
  let restored be parse value
  emit Done(count: restored[:count], value: restored[:values][1])
}
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
            value: "['a''b':]"
    local:
      - name: Done
        args:
          - name: "count"
            value:
              type: ":Number.int64"
              value: "1"
          - name: "value"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: r22-malformed-quoted-key

This case preserves the entire original Text when a Map key has malformed quoting.

### Case description

```yaml
gesBlock: case
id: r22-malformed-quoted-key
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) { emit Done(value: parse value) }
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
            value: "[\"broken: 1]"
    local:
      - name: Done
        args:
          - name: "value"
            value:
              type: ":Text"
              value: "[\"broken: 1]"
    runtimeLimits:
      exclude:
        - any: true
```
