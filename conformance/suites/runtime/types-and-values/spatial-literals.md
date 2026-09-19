---
formatVersion: 1
suiteId: runtime.spatial-literals
title: Vector and Point text literals and roundtrips
kind: scriptApi
level: atomic
categories: [conformance]
---

# Vector and Point text literals and roundtrips

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

These cases define constructor-shaped spatial data in Text, exact reconstruction, numeric and unit rules, all-or-nothing recognition, and portable parse limits.

---

## Test: vector-source-roundtrip

This case verifies constructor-shaped output, type and unit preservation, and nested roundtrips alongside Dice and similarly spelled Text.

### Case description

```yaml
gesBlock: case
id: "vector-source-roundtrip"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
  let value be :Vector(10, -20, 0)
  let labeled be :Vector(x: 1m, y: 2m, z: 3m)
  let sparse be :Vector(y: 4m)
  let restored be parse (labeled as :Text)
  let data be [position: labeled, nested: [value, :Vector(), :Dice[1, 6], ":Vector(1)"]]
  let roundtrip be parse (data as :Text)
  emit Done(text: value as :Text, unitText: labeled as :Text, zeroText: :Vector() as :Text, sparse: parse (sparse as :Text), restored: restored, isSpatial: restored is :Vector, same: roundtrip = data, nestedIsText: roundtrip.nested[4] is :Text)
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
      - name: "Done"
        args:
          - name: "text"
            value:
              type: ":Text"
              value: ":Vector(x: 10, y: -20, z: 0)"
          - name: "unitText"
            value:
              type: ":Text"
              value: ":Vector(x: 1m, y: 2m, z: 3m)"
          - name: "zeroText"
            value:
              type: ":Text"
              value: ":Vector(x: 0, y: 0, z: 0)"
          - name: "sparse"
            value:
              type: ":Vector"
              x: "0"
              y: "4"
              z: "0"
              unit: ":meter"
          - name: "restored"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
              unit: ":meter"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
          - name: "same"
            value:
              type: ":Boolean"
              value: true
          - name: "nestedIsText"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: vector-parse-valid

This case verifies positional and ordered labeled numeric components, zero defaults, all units, signs, exponents, percentages, infinities, and permitted whitespace.

### Case description

```yaml
gesBlock: case
id: "vector-parse-valid"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) { emit Done(parsed: parse value) }
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
| whitespace | Start | completion | |

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
            value: ":Vector()"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Vector"
              x: "0"
              y: "0"
              z: "0"
    runtimeLimits:
      exclude:
        - any: true
  input-02:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(1)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Vector"
              x: "1"
              y: "0"
              z: "0"
    runtimeLimits:
      exclude:
        - any: true
  input-03:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(1, -2)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Vector"
              x: "1"
              y: "-2"
              z: "0"
    runtimeLimits:
      exclude:
        - any: true
  input-04:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(1, 2, 3)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
    runtimeLimits:
      exclude:
        - any: true
  input-05:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(x: 1, y: 2, z: 3)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
    runtimeLimits:
      exclude:
        - any: true
  input-06:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(y: 4m)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Vector"
              x: "0"
              y: "4"
              z: "0"
              unit: ":meter"
    runtimeLimits:
      exclude:
        - any: true
  input-07:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(z: -5s)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Vector"
              x: "0"
              y: "0"
              z: "-5"
              unit: ":second"
    runtimeLimits:
      exclude:
        - any: true
  input-08:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(x: 1m, z: 3m)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Vector"
              x: "1"
              y: "0"
              z: "3"
              unit: ":meter"
    runtimeLimits:
      exclude:
        - any: true
  input-09:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(y: 1s, z: 2s)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Vector"
              x: "0"
              y: "1"
              z: "2"
              unit: ":second"
    runtimeLimits:
      exclude:
        - any: true
  input-10:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(1°, 2°, 3°)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
              unit: ":degree"
    runtimeLimits:
      exclude:
        - any: true
  input-11:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(1m, 2m)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "0"
              unit: ":meter"
    runtimeLimits:
      exclude:
        - any: true
  input-12:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(1_000.5, -2e-1, +3E2)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Vector"
              x: "1000.5"
              y: "-0.2"
              z: "300"
    runtimeLimits:
      exclude:
        - any: true
  input-13:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(-0, 0.0, -0e100)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Vector"
              x: "0"
              y: "0"
              z: "0"
    runtimeLimits:
      exclude:
        - any: true
  input-14:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(10%, 100%, -20%)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Vector"
              x: "0.1"
              y: "1"
              z: "-0.2"
    runtimeLimits:
      exclude:
        - any: true
  input-15:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(Infinity, -Infinity, 0)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Vector"
              x: "Infinity"
              y: "-Infinity"
              z: "0"
    runtimeLimits:
      exclude:
        - any: true
  input-16:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(x: Infinitym, y: -Infinitym)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Vector"
              x: "Infinity"
              y: "-Infinity"
              z: "0"
              unit: ":meter"
    runtimeLimits:
      exclude:
        - any: true
  whitespace:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: " \t:Vector\r\n( x \t: 1,\r y: 2,\n z: 3 )\n"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: vector-parse-invalid

This case verifies exact original-Text fallback for malformed or executable forms, incompatible units, invalid components, and invalid nested values.

### Case description

```yaml
gesBlock: case
id: "vector-parse-invalid"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) { emit Done(parsed: parse value) }
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
| input-39 | Start | completion | |
| input-40 | Start | completion | |
| input-41 | Start | completion | |
| input-42 | Start | completion | |
| input-43 | Start | completion | |
| input-44 | Start | completion | |

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
            value: ":Vector(1, 2, 3, 4)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(1, 2, 3, 4)"
    runtimeLimits:
      exclude:
        - any: true
  input-02:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(x: 1, x: 2)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(x: 1, x: 2)"
    runtimeLimits:
      exclude:
        - any: true
  input-03:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(y: 1, x: 2)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(y: 1, x: 2)"
    runtimeLimits:
      exclude:
        - any: true
  input-04:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(z: 1, y: 2)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(z: 1, y: 2)"
    runtimeLimits:
      exclude:
        - any: true
  input-05:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(w: 1)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(w: 1)"
    runtimeLimits:
      exclude:
        - any: true
  input-06:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(X: 1)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(X: 1)"
    runtimeLimits:
      exclude:
        - any: true
  input-07:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(x: 1, 2)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(x: 1, 2)"
    runtimeLimits:
      exclude:
        - any: true
  input-08:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(1, y: 2)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(1, y: 2)"
    runtimeLimits:
      exclude:
        - any: true
  input-09:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(x:)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(x:)"
    runtimeLimits:
      exclude:
        - any: true
  input-10:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(1,)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(1,)"
    runtimeLimits:
      exclude:
        - any: true
  input-11:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(,1)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(,1)"
    runtimeLimits:
      exclude:
        - any: true
  input-12:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(1 2)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(1 2)"
    runtimeLimits:
      exclude:
        - any: true
  input-13:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(1"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(1"
    runtimeLimits:
      exclude:
        - any: true
  input-14:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector("
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector("
    runtimeLimits:
      exclude:
        - any: true
  input-15:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector"
    runtimeLimits:
      exclude:
        - any: true
  input-16:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(1) extra"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(1) extra"
    runtimeLimits:
      exclude:
        - any: true
  input-17:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":VectorExtra(1)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":VectorExtra(1)"
    runtimeLimits:
      exclude:
        - any: true
  input-18:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(1m, 0)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(1m, 0)"
    runtimeLimits:
      exclude:
        - any: true
  input-19:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(1m, 2s)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(1m, 2s)"
    runtimeLimits:
      exclude:
        - any: true
  input-20:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(NaN)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(NaN)"
    runtimeLimits:
      exclude:
        - any: true
  input-21:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(NaNm)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(NaNm)"
    runtimeLimits:
      exclude:
        - any: true
  input-22:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(Infinity%)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(Infinity%)"
    runtimeLimits:
      exclude:
        - any: true
  input-23:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(true)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(true)"
    runtimeLimits:
      exclude:
        - any: true
  input-24:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(nothing)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(nothing)"
    runtimeLimits:
      exclude:
        - any: true
  input-25:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(\"1\")"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(\"1\")"
    runtimeLimits:
      exclude:
        - any: true
  input-26:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(#ready)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(#ready)"
    runtimeLimits:
      exclude:
        - any: true
  input-27:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector($value)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector($value)"
    runtimeLimits:
      exclude:
        - any: true
  input-28:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(value)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(value)"
    runtimeLimits:
      exclude:
        - any: true
  input-29:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(1 + 2)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(1 + 2)"
    runtimeLimits:
      exclude:
        - any: true
  input-30:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(1 / 0)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(1 / 0)"
    runtimeLimits:
      exclude:
        - any: true
  input-31:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(random from 1 to 6)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(random from 1 to 6)"
    runtimeLimits:
      exclude:
        - any: true
  input-32:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(pi)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(pi)"
    runtimeLimits:
      exclude:
        - any: true
  input-33:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector([1, 2])"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector([1, 2])"
    runtimeLimits:
      exclude:
        - any: true
  input-34:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(:Vector(1))"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(:Vector(1))"
    runtimeLimits:
      exclude:
        - any: true
  input-35:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(1 /* comment */)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(1 /* comment */)"
    runtimeLimits:
      exclude:
        - any: true
  input-36:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(1 // comment\n)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(1 // comment\n)"
    runtimeLimits:
      exclude:
        - any: true
  input-37:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector (1)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector (1)"
    runtimeLimits:
      exclude:
        - any: true
  input-38:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector(1 )"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector(1 )"
    runtimeLimits:
      exclude:
        - any: true
  input-39:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Vector[x: 1, y: 2, z: 3]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Vector[x: 1, y: 2, z: 3]"
    runtimeLimits:
      exclude:
        - any: true
  input-40:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "vector(1, 2, 3)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: "vector(1, 2, 3)"
    runtimeLimits:
      exclude:
        - any: true
  input-41:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":vector(1)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":vector(1)"
    runtimeLimits:
      exclude:
        - any: true
  input-42:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[:Vector(1), :Vector(1m, 2s)]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: "[:Vector(1), :Vector(1m, 2s)]"
    runtimeLimits:
      exclude:
        - any: true
  input-43:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[position: :Vector(1,)]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: "[position: :Vector(1,)]"
    runtimeLimits:
      exclude:
        - any: true
  input-44:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: " \t:Vector(0, NaN)\r\n"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: " \t:Vector(0, NaN)\r\n"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: vector-binary64-roundtrip

This case preserves exact binary64 components and units through text formatting and parsing, including subnormals, large integral values, extremes, and signed infinities.

### Case description

```yaml
gesBlock: case
id: "vector-binary64-roundtrip"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) { let restored be parse (value as :Text); emit Done(restored: restored, isSpatial: restored is :Vector) }
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

### Expectation

```yaml
gesBlock: expect
steps:
  input-01:
    input:
      args:
        - name: "value"
          value:
            type: ":Vector"
            x: "0"
            y: "0"
            z: "0"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Vector"
              x: "0"
              y: "0"
              z: "0"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-02:
    input:
      args:
        - name: "value"
          value:
            type: ":Vector"
            x: "0.1"
            y: "-0.2"
            z: "0.30000000000000004"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Vector"
              x: "0.1"
              y: "-0.2"
              z: "0.30000000000000004"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-03:
    input:
      args:
        - name: "value"
          value:
            type: ":Vector"
            x: "5e-324"
            y: "-5e-324"
            z: "2.2250738585072014e-308"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Vector"
              x: "5e-324"
              y: "-5e-324"
              z: "2.2250738585072014e-308"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-04:
    input:
      args:
        - name: "value"
          value:
            type: ":Vector"
            x: "1.7976931348623157e308"
            y: "-1.7976931348623157e308"
            z: "1e20"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Vector"
              x: "1.7976931348623157e308"
              y: "-1.7976931348623157e308"
              z: "1e20"
          - name: "isSpatial"
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
            type: ":Vector"
            x: "9007199254740992"
            y: "9007199254740994"
            z: "-9.223372036854776e18"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Vector"
              x: "9007199254740992"
              y: "9007199254740994"
              z: "-9.223372036854776e18"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-06:
    input:
      args:
        - name: "value"
          value:
            type: ":Vector"
            x: "Infinity"
            y: "-Infinity"
            z: "0"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Vector"
              x: "Infinity"
              y: "-Infinity"
              z: "0"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-07:
    input:
      args:
        - name: "value"
          value:
            type: ":Vector"
            x: "Infinity"
            y: "-Infinity"
            z: "0"
            unit: ":meter"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Vector"
              x: "Infinity"
              y: "-Infinity"
              z: "0"
              unit: ":meter"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-08:
    input:
      args:
        - name: "value"
          value:
            type: ":Vector"
            x: "Infinity"
            y: "-Infinity"
            z: "0"
            unit: ":second"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Vector"
              x: "Infinity"
              y: "-Infinity"
              z: "0"
              unit: ":second"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-09:
    input:
      args:
        - name: "value"
          value:
            type: ":Vector"
            x: "Infinity"
            y: "-Infinity"
            z: "0"
            unit: ":degree"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Vector"
              x: "Infinity"
              y: "-Infinity"
              z: "0"
              unit: ":degree"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-10:
    input:
      args:
        - name: "value"
          value:
            type: ":Vector"
            x: "0.1"
            y: "-0.2"
            z: "1e20"
            unit: ":meter"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Vector"
              x: "0.1"
              y: "-0.2"
              z: "1e20"
              unit: ":meter"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-11:
    input:
      args:
        - name: "value"
          value:
            type: ":Vector"
            x: "0.1"
            y: "-0.2"
            z: "1e20"
            unit: ":second"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Vector"
              x: "0.1"
              y: "-0.2"
              z: "1e20"
              unit: ":second"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-12:
    input:
      args:
        - name: "value"
          value:
            type: ":Vector"
            x: "0.1"
            y: "-0.2"
            z: "1e20"
            unit: ":degree"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Vector"
              x: "0.1"
              y: "-0.2"
              z: "1e20"
              unit: ":degree"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: vector-depth-at-limit

This case counts an empty spatial value as one nesting level beneath Lists.

### Case description

```yaml
gesBlock: case
id: "vector-depth-at-limit"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) { emit Before; let parsed be parse value; emit Done(isList: parsed is :List) }
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
            value: "[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[:Vector()]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]"
    local:
      - name: "Before"
        args: []
      - name: "Done"
        args:
          - name: "isList"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: vector-depth-over-limit

This case counts an empty spatial value as one nesting level beneath Lists.

### Case description

```yaml
gesBlock: case
id: "vector-depth-over-limit"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) { emit Before; let parsed be parse value; emit Done(isList: parsed is :List) }
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
            value: "[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[:Vector()]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]"
    local:
      - name: "Before"
        args: []
    runtimeLimits:
      include:
        - name: "MaxLiteralDepth"
```

---

## Test: vector-items-at-limit

This case shares item accounting with enclosing Map entries and Dice rolls, counting only explicitly present spatial components.

### Case description

```yaml
gesBlock: case
id: "vector-items-at-limit"
compile:
  binaryRoundTrip: true
runtimeLimits:
  maxExecutionSteps: 3000000
  maxLoopIterations: 200000
  maxRangeItems: 200000
  maxGeneratedCollectionItems: 200000
```

### Source code under test

```ges
on Start {
  let rolls be :List[:select item from 1 to 65531 => 1] as :Text
  let input be "[filler: :Dice" + rolls + ", position: :Vector(1, 2, 3)]"
  emit Before
  let parsed be parse input
  emit Done(position: parsed.position)
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
          - name: "position"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: vector-items-over-limit

This case shares item accounting with enclosing Map entries and Dice rolls, counting only explicitly present spatial components.

### Case description

```yaml
gesBlock: case
id: "vector-items-over-limit"
compile:
  binaryRoundTrip: true
runtimeLimits:
  maxExecutionSteps: 3000000
  maxLoopIterations: 200000
  maxRangeItems: 200000
  maxGeneratedCollectionItems: 200000
```

### Source code under test

```ges
on Start {
  let rolls be :List[:select item from 1 to 65532 => 1] as :Text
  let input be "[filler: :Dice" + rolls + ", position: :Vector(1, 2, 3)]"
  emit Before
  let parsed be parse input
  emit Done(position: parsed.position)
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

## Test: vector-empty-items-at-limit

This case shares item accounting with enclosing Map entries and Dice rolls, counting only explicitly present spatial components.

### Case description

```yaml
gesBlock: case
id: "vector-empty-items-at-limit"
compile:
  binaryRoundTrip: true
runtimeLimits:
  maxExecutionSteps: 3000000
  maxLoopIterations: 200000
  maxRangeItems: 200000
  maxGeneratedCollectionItems: 200000
```

### Source code under test

```ges
on Start {
  let rolls be :List[:select item from 1 to 65534 => 1] as :Text
  let input be "[filler: :Dice" + rolls + ", position: :Vector()]"
  emit Before
  let parsed be parse input
  emit Done(position: parsed.position)
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
          - name: "position"
            value:
              type: ":Vector"
              x: "0"
              y: "0"
              z: "0"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: vector-empty-items-over-limit

This case shares item accounting with enclosing Map entries and Dice rolls, counting only explicitly present spatial components.

### Case description

```yaml
gesBlock: case
id: "vector-empty-items-over-limit"
compile:
  binaryRoundTrip: true
runtimeLimits:
  maxExecutionSteps: 3000000
  maxLoopIterations: 200000
  maxRangeItems: 200000
  maxGeneratedCollectionItems: 200000
```

### Source code under test

```ges
on Start {
  let rolls be :List[:select item from 1 to 65535 => 1] as :Text
  let input be "[filler: :Dice" + rolls + ", position: :Vector()]"
  emit Before
  let parsed be parse input
  emit Done(position: parsed.position)
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

## Test: point-source-roundtrip

This case verifies constructor-shaped output, type and unit preservation, and nested roundtrips alongside Dice and similarly spelled Text.

### Case description

```yaml
gesBlock: case
id: "point-source-roundtrip"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
  let value be :Point(10, -20, 0)
  let labeled be :Point(x: 1m, y: 2m, z: 3m)
  let sparse be :Point(y: 4m)
  let restored be parse (labeled as :Text)
  let data be [position: labeled, nested: [value, :Point(), :Dice[1, 6], ":Point(1)"]]
  let roundtrip be parse (data as :Text)
  emit Done(text: value as :Text, unitText: labeled as :Text, zeroText: :Point() as :Text, sparse: parse (sparse as :Text), restored: restored, isSpatial: restored is :Point, same: roundtrip = data, nestedIsText: roundtrip.nested[4] is :Text)
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
      - name: "Done"
        args:
          - name: "text"
            value:
              type: ":Text"
              value: ":Point(x: 10, y: -20, z: 0)"
          - name: "unitText"
            value:
              type: ":Text"
              value: ":Point(x: 1m, y: 2m, z: 3m)"
          - name: "zeroText"
            value:
              type: ":Text"
              value: ":Point(x: 0, y: 0, z: 0)"
          - name: "sparse"
            value:
              type: ":Point"
              x: "0"
              y: "4"
              z: "0"
              unit: ":meter"
          - name: "restored"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
              unit: ":meter"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
          - name: "same"
            value:
              type: ":Boolean"
              value: true
          - name: "nestedIsText"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: point-parse-valid

This case verifies positional and ordered labeled numeric components, zero defaults, all units, signs, exponents, percentages, infinities, and permitted whitespace.

### Case description

```yaml
gesBlock: case
id: "point-parse-valid"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) { emit Done(parsed: parse value) }
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
| whitespace | Start | completion | |

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
            value: ":Point()"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Point"
              x: "0"
              y: "0"
              z: "0"
    runtimeLimits:
      exclude:
        - any: true
  input-02:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(1)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Point"
              x: "1"
              y: "0"
              z: "0"
    runtimeLimits:
      exclude:
        - any: true
  input-03:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(1, -2)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Point"
              x: "1"
              y: "-2"
              z: "0"
    runtimeLimits:
      exclude:
        - any: true
  input-04:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(1, 2, 3)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
    runtimeLimits:
      exclude:
        - any: true
  input-05:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(x: 1, y: 2, z: 3)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
    runtimeLimits:
      exclude:
        - any: true
  input-06:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(y: 4m)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Point"
              x: "0"
              y: "4"
              z: "0"
              unit: ":meter"
    runtimeLimits:
      exclude:
        - any: true
  input-07:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(z: -5s)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Point"
              x: "0"
              y: "0"
              z: "-5"
              unit: ":second"
    runtimeLimits:
      exclude:
        - any: true
  input-08:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(x: 1m, z: 3m)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Point"
              x: "1"
              y: "0"
              z: "3"
              unit: ":meter"
    runtimeLimits:
      exclude:
        - any: true
  input-09:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(y: 1s, z: 2s)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Point"
              x: "0"
              y: "1"
              z: "2"
              unit: ":second"
    runtimeLimits:
      exclude:
        - any: true
  input-10:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(1°, 2°, 3°)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
              unit: ":degree"
    runtimeLimits:
      exclude:
        - any: true
  input-11:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(1m, 2m)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "0"
              unit: ":meter"
    runtimeLimits:
      exclude:
        - any: true
  input-12:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(1_000.5, -2e-1, +3E2)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Point"
              x: "1000.5"
              y: "-0.2"
              z: "300"
    runtimeLimits:
      exclude:
        - any: true
  input-13:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(-0, 0.0, -0e100)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Point"
              x: "0"
              y: "0"
              z: "0"
    runtimeLimits:
      exclude:
        - any: true
  input-14:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(10%, 100%, -20%)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Point"
              x: "0.1"
              y: "1"
              z: "-0.2"
    runtimeLimits:
      exclude:
        - any: true
  input-15:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(Infinity, -Infinity, 0)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Point"
              x: "Infinity"
              y: "-Infinity"
              z: "0"
    runtimeLimits:
      exclude:
        - any: true
  input-16:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(x: Infinitym, y: -Infinitym)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Point"
              x: "Infinity"
              y: "-Infinity"
              z: "0"
              unit: ":meter"
    runtimeLimits:
      exclude:
        - any: true
  whitespace:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: " \t:Point\r\n( x \t: 1,\r y: 2,\n z: 3 )\n"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: point-parse-invalid

This case verifies exact original-Text fallback for malformed or executable forms, incompatible units, invalid components, and invalid nested values.

### Case description

```yaml
gesBlock: case
id: "point-parse-invalid"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) { emit Done(parsed: parse value) }
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
| input-39 | Start | completion | |
| input-40 | Start | completion | |
| input-41 | Start | completion | |
| input-42 | Start | completion | |
| input-43 | Start | completion | |
| input-44 | Start | completion | |

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
            value: ":Point(1, 2, 3, 4)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(1, 2, 3, 4)"
    runtimeLimits:
      exclude:
        - any: true
  input-02:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(x: 1, x: 2)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(x: 1, x: 2)"
    runtimeLimits:
      exclude:
        - any: true
  input-03:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(y: 1, x: 2)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(y: 1, x: 2)"
    runtimeLimits:
      exclude:
        - any: true
  input-04:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(z: 1, y: 2)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(z: 1, y: 2)"
    runtimeLimits:
      exclude:
        - any: true
  input-05:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(w: 1)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(w: 1)"
    runtimeLimits:
      exclude:
        - any: true
  input-06:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(X: 1)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(X: 1)"
    runtimeLimits:
      exclude:
        - any: true
  input-07:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(x: 1, 2)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(x: 1, 2)"
    runtimeLimits:
      exclude:
        - any: true
  input-08:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(1, y: 2)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(1, y: 2)"
    runtimeLimits:
      exclude:
        - any: true
  input-09:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(x:)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(x:)"
    runtimeLimits:
      exclude:
        - any: true
  input-10:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(1,)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(1,)"
    runtimeLimits:
      exclude:
        - any: true
  input-11:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(,1)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(,1)"
    runtimeLimits:
      exclude:
        - any: true
  input-12:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(1 2)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(1 2)"
    runtimeLimits:
      exclude:
        - any: true
  input-13:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(1"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(1"
    runtimeLimits:
      exclude:
        - any: true
  input-14:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point("
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point("
    runtimeLimits:
      exclude:
        - any: true
  input-15:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point"
    runtimeLimits:
      exclude:
        - any: true
  input-16:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(1) extra"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(1) extra"
    runtimeLimits:
      exclude:
        - any: true
  input-17:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":PointExtra(1)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":PointExtra(1)"
    runtimeLimits:
      exclude:
        - any: true
  input-18:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(1m, 0)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(1m, 0)"
    runtimeLimits:
      exclude:
        - any: true
  input-19:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(1m, 2s)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(1m, 2s)"
    runtimeLimits:
      exclude:
        - any: true
  input-20:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(NaN)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(NaN)"
    runtimeLimits:
      exclude:
        - any: true
  input-21:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(NaNm)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(NaNm)"
    runtimeLimits:
      exclude:
        - any: true
  input-22:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(Infinity%)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(Infinity%)"
    runtimeLimits:
      exclude:
        - any: true
  input-23:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(true)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(true)"
    runtimeLimits:
      exclude:
        - any: true
  input-24:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(nothing)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(nothing)"
    runtimeLimits:
      exclude:
        - any: true
  input-25:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(\"1\")"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(\"1\")"
    runtimeLimits:
      exclude:
        - any: true
  input-26:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(#ready)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(#ready)"
    runtimeLimits:
      exclude:
        - any: true
  input-27:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point($value)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point($value)"
    runtimeLimits:
      exclude:
        - any: true
  input-28:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(value)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(value)"
    runtimeLimits:
      exclude:
        - any: true
  input-29:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(1 + 2)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(1 + 2)"
    runtimeLimits:
      exclude:
        - any: true
  input-30:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(1 / 0)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(1 / 0)"
    runtimeLimits:
      exclude:
        - any: true
  input-31:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(random from 1 to 6)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(random from 1 to 6)"
    runtimeLimits:
      exclude:
        - any: true
  input-32:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(pi)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(pi)"
    runtimeLimits:
      exclude:
        - any: true
  input-33:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point([1, 2])"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point([1, 2])"
    runtimeLimits:
      exclude:
        - any: true
  input-34:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(:Point(1))"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(:Point(1))"
    runtimeLimits:
      exclude:
        - any: true
  input-35:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(1 /* comment */)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(1 /* comment */)"
    runtimeLimits:
      exclude:
        - any: true
  input-36:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(1 // comment\n)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(1 // comment\n)"
    runtimeLimits:
      exclude:
        - any: true
  input-37:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point (1)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point (1)"
    runtimeLimits:
      exclude:
        - any: true
  input-38:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point(1 )"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point(1 )"
    runtimeLimits:
      exclude:
        - any: true
  input-39:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":Point[x: 1, y: 2, z: 3]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":Point[x: 1, y: 2, z: 3]"
    runtimeLimits:
      exclude:
        - any: true
  input-40:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "point(1, 2, 3)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: "point(1, 2, 3)"
    runtimeLimits:
      exclude:
        - any: true
  input-41:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: ":point(1)"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: ":point(1)"
    runtimeLimits:
      exclude:
        - any: true
  input-42:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[:Point(1), :Point(1m, 2s)]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: "[:Point(1), :Point(1m, 2s)]"
    runtimeLimits:
      exclude:
        - any: true
  input-43:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[position: :Point(1,)]"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: "[position: :Point(1,)]"
    runtimeLimits:
      exclude:
        - any: true
  input-44:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: " \t:Point(0, NaN)\r\n"
    local:
      - name: "Done"
        args:
          - name: "parsed"
            value:
              type: ":Text"
              value: " \t:Point(0, NaN)\r\n"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: point-binary64-roundtrip

This case preserves exact binary64 components and units through text formatting and parsing, including subnormals, large integral values, extremes, and signed infinities.

### Case description

```yaml
gesBlock: case
id: "point-binary64-roundtrip"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) { let restored be parse (value as :Text); emit Done(restored: restored, isSpatial: restored is :Point) }
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

### Expectation

```yaml
gesBlock: expect
steps:
  input-01:
    input:
      args:
        - name: "value"
          value:
            type: ":Point"
            x: "0"
            y: "0"
            z: "0"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Point"
              x: "0"
              y: "0"
              z: "0"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-02:
    input:
      args:
        - name: "value"
          value:
            type: ":Point"
            x: "0.1"
            y: "-0.2"
            z: "0.30000000000000004"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Point"
              x: "0.1"
              y: "-0.2"
              z: "0.30000000000000004"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-03:
    input:
      args:
        - name: "value"
          value:
            type: ":Point"
            x: "5e-324"
            y: "-5e-324"
            z: "2.2250738585072014e-308"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Point"
              x: "5e-324"
              y: "-5e-324"
              z: "2.2250738585072014e-308"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-04:
    input:
      args:
        - name: "value"
          value:
            type: ":Point"
            x: "1.7976931348623157e308"
            y: "-1.7976931348623157e308"
            z: "1e20"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Point"
              x: "1.7976931348623157e308"
              y: "-1.7976931348623157e308"
              z: "1e20"
          - name: "isSpatial"
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
            type: ":Point"
            x: "9007199254740992"
            y: "9007199254740994"
            z: "-9.223372036854776e18"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Point"
              x: "9007199254740992"
              y: "9007199254740994"
              z: "-9.223372036854776e18"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-06:
    input:
      args:
        - name: "value"
          value:
            type: ":Point"
            x: "Infinity"
            y: "-Infinity"
            z: "0"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Point"
              x: "Infinity"
              y: "-Infinity"
              z: "0"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-07:
    input:
      args:
        - name: "value"
          value:
            type: ":Point"
            x: "Infinity"
            y: "-Infinity"
            z: "0"
            unit: ":meter"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Point"
              x: "Infinity"
              y: "-Infinity"
              z: "0"
              unit: ":meter"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-08:
    input:
      args:
        - name: "value"
          value:
            type: ":Point"
            x: "Infinity"
            y: "-Infinity"
            z: "0"
            unit: ":second"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Point"
              x: "Infinity"
              y: "-Infinity"
              z: "0"
              unit: ":second"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-09:
    input:
      args:
        - name: "value"
          value:
            type: ":Point"
            x: "Infinity"
            y: "-Infinity"
            z: "0"
            unit: ":degree"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Point"
              x: "Infinity"
              y: "-Infinity"
              z: "0"
              unit: ":degree"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-10:
    input:
      args:
        - name: "value"
          value:
            type: ":Point"
            x: "0.1"
            y: "-0.2"
            z: "1e20"
            unit: ":meter"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Point"
              x: "0.1"
              y: "-0.2"
              z: "1e20"
              unit: ":meter"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-11:
    input:
      args:
        - name: "value"
          value:
            type: ":Point"
            x: "0.1"
            y: "-0.2"
            z: "1e20"
            unit: ":second"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Point"
              x: "0.1"
              y: "-0.2"
              z: "1e20"
              unit: ":second"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
  input-12:
    input:
      args:
        - name: "value"
          value:
            type: ":Point"
            x: "0.1"
            y: "-0.2"
            z: "1e20"
            unit: ":degree"
    local:
      - name: "Done"
        args:
          - name: "restored"
            value:
              type: ":Point"
              x: "0.1"
              y: "-0.2"
              z: "1e20"
              unit: ":degree"
          - name: "isSpatial"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: point-depth-at-limit

This case counts an empty spatial value as one nesting level beneath Lists.

### Case description

```yaml
gesBlock: case
id: "point-depth-at-limit"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) { emit Before; let parsed be parse value; emit Done(isList: parsed is :List) }
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
            value: "[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[:Point()]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]"
    local:
      - name: "Before"
        args: []
      - name: "Done"
        args:
          - name: "isList"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: point-depth-over-limit

This case counts an empty spatial value as one nesting level beneath Lists.

### Case description

```yaml
gesBlock: case
id: "point-depth-over-limit"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(value) { emit Before; let parsed be parse value; emit Done(isList: parsed is :List) }
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
            value: "[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[:Point()]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]"
    local:
      - name: "Before"
        args: []
    runtimeLimits:
      include:
        - name: "MaxLiteralDepth"
```

---

## Test: point-items-at-limit

This case shares item accounting with enclosing Map entries and Dice rolls, counting only explicitly present spatial components.

### Case description

```yaml
gesBlock: case
id: "point-items-at-limit"
compile:
  binaryRoundTrip: true
runtimeLimits:
  maxExecutionSteps: 3000000
  maxLoopIterations: 200000
  maxRangeItems: 200000
  maxGeneratedCollectionItems: 200000
```

### Source code under test

```ges
on Start {
  let rolls be :List[:select item from 1 to 65531 => 1] as :Text
  let input be "[filler: :Dice" + rolls + ", position: :Point(1, 2, 3)]"
  emit Before
  let parsed be parse input
  emit Done(position: parsed.position)
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
          - name: "position"
            value:
              type: ":Point"
              x: "1"
              y: "2"
              z: "3"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: point-items-over-limit

This case shares item accounting with enclosing Map entries and Dice rolls, counting only explicitly present spatial components.

### Case description

```yaml
gesBlock: case
id: "point-items-over-limit"
compile:
  binaryRoundTrip: true
runtimeLimits:
  maxExecutionSteps: 3000000
  maxLoopIterations: 200000
  maxRangeItems: 200000
  maxGeneratedCollectionItems: 200000
```

### Source code under test

```ges
on Start {
  let rolls be :List[:select item from 1 to 65532 => 1] as :Text
  let input be "[filler: :Dice" + rolls + ", position: :Point(1, 2, 3)]"
  emit Before
  let parsed be parse input
  emit Done(position: parsed.position)
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

## Test: point-empty-items-at-limit

This case shares item accounting with enclosing Map entries and Dice rolls, counting only explicitly present spatial components.

### Case description

```yaml
gesBlock: case
id: "point-empty-items-at-limit"
compile:
  binaryRoundTrip: true
runtimeLimits:
  maxExecutionSteps: 3000000
  maxLoopIterations: 200000
  maxRangeItems: 200000
  maxGeneratedCollectionItems: 200000
```

### Source code under test

```ges
on Start {
  let rolls be :List[:select item from 1 to 65534 => 1] as :Text
  let input be "[filler: :Dice" + rolls + ", position: :Point()]"
  emit Before
  let parsed be parse input
  emit Done(position: parsed.position)
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
          - name: "position"
            value:
              type: ":Point"
              x: "0"
              y: "0"
              z: "0"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: point-empty-items-over-limit

This case shares item accounting with enclosing Map entries and Dice rolls, counting only explicitly present spatial components.

### Case description

```yaml
gesBlock: case
id: "point-empty-items-over-limit"
compile:
  binaryRoundTrip: true
runtimeLimits:
  maxExecutionSteps: 3000000
  maxLoopIterations: 200000
  maxRangeItems: 200000
  maxGeneratedCollectionItems: 200000
```

### Source code under test

```ges
on Start {
  let rolls be :List[:select item from 1 to 65535 => 1] as :Text
  let input be "[filler: :Dice" + rolls + ", position: :Point()]"
  emit Before
  let parsed be parse input
  emit Done(position: parsed.position)
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
