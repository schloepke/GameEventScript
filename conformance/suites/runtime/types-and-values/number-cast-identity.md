---
formatVersion: 1
suiteId: runtime.numeric.number-casts
title: Number cast identity and text special values
kind: scriptApi
level: atomic
categories: [conformance]
---

# Number cast identity and text special values

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite checks numeric conversion consistency across the compiler and VM, as required by `specs/Language.md` and `specs/Semantics/Numbers.md`. Exact Int64 identity includes Quantity storage; observable infinities survive text conversion and invalid text remains nothing.

---

## Test: Number identity above53 through direct Program

This case requires an existing integer to retain its exact value and unit when cast to Number.

### Case description

```yaml
gesBlock: case
id: identity-above53-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module numberidentity
constant $original be 9007199254740993
on Start(value) {
  emit Literal(value: (9007199254740993) as :Number)
  emit Constant(value: $original as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Number.int64", value: "9007199254740993" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "9007199254740993" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "9007199254740993" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "9007199254740993" }
```

---

## Test: Number identity above53 through binary Program

This case requires an existing integer to retain its exact value and unit when cast to Number.

### Case description

```yaml
gesBlock: case
id: identity-above53-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module numberidentity
constant $original be 9007199254740993
on Start(value) {
  emit Literal(value: (9007199254740993) as :Number)
  emit Constant(value: $original as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Number.int64", value: "9007199254740993" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "9007199254740993" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "9007199254740993" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "9007199254740993" }
```

---

## Test: Number identity negative-above53 through direct Program

This case requires an existing integer to retain its exact value and unit when cast to Number.

### Case description

```yaml
gesBlock: case
id: identity-negative-above53-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module numberidentity
constant $original be -9007199254740993
on Start(value) {
  emit Literal(value: (-9007199254740993) as :Number)
  emit Constant(value: $original as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Number.int64", value: "-9007199254740993" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "-9007199254740993" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "-9007199254740993" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "-9007199254740993" }
```

---

## Test: Number identity negative-above53 through binary Program

This case requires an existing integer to retain its exact value and unit when cast to Number.

### Case description

```yaml
gesBlock: case
id: identity-negative-above53-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module numberidentity
constant $original be -9007199254740993
on Start(value) {
  emit Literal(value: (-9007199254740993) as :Number)
  emit Constant(value: $original as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Number.int64", value: "-9007199254740993" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "-9007199254740993" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "-9007199254740993" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "-9007199254740993" }
```

---

## Test: Number identity int64-max through direct Program

This case requires an existing integer to retain its exact value and unit when cast to Number.

### Case description

```yaml
gesBlock: case
id: identity-int64-max-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module numberidentity
constant $original be 9223372036854775807
on Start(value) {
  emit Literal(value: (9223372036854775807) as :Number)
  emit Constant(value: $original as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Number.int64", value: "9223372036854775807" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "9223372036854775807" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "9223372036854775807" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "9223372036854775807" }
```

---

## Test: Number identity int64-max through binary Program

This case requires an existing integer to retain its exact value and unit when cast to Number.

### Case description

```yaml
gesBlock: case
id: identity-int64-max-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module numberidentity
constant $original be 9223372036854775807
on Start(value) {
  emit Literal(value: (9223372036854775807) as :Number)
  emit Constant(value: $original as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Number.int64", value: "9223372036854775807" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "9223372036854775807" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "9223372036854775807" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "9223372036854775807" }
```

---

## Test: Number identity int64-min through direct Program

This case requires an existing integer to retain its exact value and unit when cast to Number.

### Case description

```yaml
gesBlock: case
id: identity-int64-min-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module numberidentity
constant $original be -9223372036854775808
on Start(value) {
  emit Literal(value: (-9223372036854775808) as :Number)
  emit Constant(value: $original as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Number.int64", value: "-9223372036854775808" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "-9223372036854775808" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "-9223372036854775808" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "-9223372036854775808" }
```

---

## Test: Number identity int64-min through binary Program

This case requires an existing integer to retain its exact value and unit when cast to Number.

### Case description

```yaml
gesBlock: case
id: identity-int64-min-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module numberidentity
constant $original be -9223372036854775808
on Start(value) {
  emit Literal(value: (-9223372036854775808) as :Number)
  emit Constant(value: $original as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Number.int64", value: "-9223372036854775808" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "-9223372036854775808" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "-9223372036854775808" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "-9223372036854775808" }
```

---

## Test: Number identity quantity-above53 through direct Program

This case requires an existing integer to retain its exact value and unit when cast to Number.

### Case description

```yaml
gesBlock: case
id: identity-quantity-above53-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module numberidentity
constant $original be 9007199254740993m
on Start(value) {
  emit Literal(value: (9007199254740993m) as :Number)
  emit Constant(value: $original as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Quantity.int64", value: "9007199254740993", unit: "m" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Quantity.int64", value: "9007199254740993", unit: "m" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Quantity.int64", value: "9007199254740993", unit: "m" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Quantity.int64", value: "9007199254740993", unit: "m" }
```

---

## Test: Number identity quantity-above53 through binary Program

This case requires an existing integer to retain its exact value and unit when cast to Number.

### Case description

```yaml
gesBlock: case
id: identity-quantity-above53-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module numberidentity
constant $original be 9007199254740993m
on Start(value) {
  emit Literal(value: (9007199254740993m) as :Number)
  emit Constant(value: $original as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Quantity.int64", value: "9007199254740993", unit: "m" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Quantity.int64", value: "9007199254740993", unit: "m" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Quantity.int64", value: "9007199254740993", unit: "m" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Quantity.int64", value: "9007199254740993", unit: "m" }
```

---

## Test: Text positive-infinity through direct Program

This case requires folded and runtime text conversion to produce the same specified special or finite value.

### Case description

```yaml
gesBlock: case
id: text-positive-infinity-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module textnumber
constant $text be "Infinity"
on Start(value) {
  emit Literal(value: "Infinity" as :Number)
  emit Constant(value: $text as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Text", value: "Infinity" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.binary64", value: "Infinity" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.binary64", value: "Infinity" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.binary64", value: "Infinity" }
```

---

## Test: Text positive-infinity through binary Program

This case requires folded and runtime text conversion to produce the same specified special or finite value.

### Case description

```yaml
gesBlock: case
id: text-positive-infinity-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module textnumber
constant $text be "Infinity"
on Start(value) {
  emit Literal(value: "Infinity" as :Number)
  emit Constant(value: $text as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Text", value: "Infinity" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.binary64", value: "Infinity" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.binary64", value: "Infinity" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.binary64", value: "Infinity" }
```

---

## Test: Text negative-infinity through direct Program

This case requires folded and runtime text conversion to produce the same specified special or finite value.

### Case description

```yaml
gesBlock: case
id: text-negative-infinity-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module textnumber
constant $text be "-Infinity"
on Start(value) {
  emit Literal(value: "-Infinity" as :Number)
  emit Constant(value: $text as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Text", value: "-Infinity" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.binary64", value: "-Infinity" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.binary64", value: "-Infinity" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.binary64", value: "-Infinity" }
```

---

## Test: Text negative-infinity through binary Program

This case requires folded and runtime text conversion to produce the same specified special or finite value.

### Case description

```yaml
gesBlock: case
id: text-negative-infinity-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module textnumber
constant $text be "-Infinity"
on Start(value) {
  emit Literal(value: "-Infinity" as :Number)
  emit Constant(value: $text as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Text", value: "-Infinity" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.binary64", value: "-Infinity" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.binary64", value: "-Infinity" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.binary64", value: "-Infinity" }
```

---

## Test: Text nan through direct Program

This case requires folded and runtime text conversion to produce the same specified special or finite value.

### Case description

```yaml
gesBlock: case
id: text-nan-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module textnumber
constant $text be "NaN"
on Start(value) {
  emit Literal(value: "NaN" as :Number)
  emit Constant(value: $text as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Text", value: "NaN" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Nothing" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Nothing" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Nothing" }
```

---

## Test: Text nan through binary Program

This case requires folded and runtime text conversion to produce the same specified special or finite value.

### Case description

```yaml
gesBlock: case
id: text-nan-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module textnumber
constant $text be "NaN"
on Start(value) {
  emit Literal(value: "NaN" as :Number)
  emit Constant(value: $text as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Text", value: "NaN" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Nothing" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Nothing" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Nothing" }
```

---

## Test: Text invalid through direct Program

This case requires folded and runtime text conversion to produce the same specified special or finite value.

### Case description

```yaml
gesBlock: case
id: text-invalid-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module textnumber
constant $text be "invalid"
on Start(value) {
  emit Literal(value: "invalid" as :Number)
  emit Constant(value: $text as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Text", value: "invalid" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Nothing" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Nothing" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Nothing" }
```

---

## Test: Text invalid through binary Program

This case requires folded and runtime text conversion to produce the same specified special or finite value.

### Case description

```yaml
gesBlock: case
id: text-invalid-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module textnumber
constant $text be "invalid"
on Start(value) {
  emit Literal(value: "invalid" as :Number)
  emit Constant(value: $text as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Text", value: "invalid" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Nothing" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Nothing" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Nothing" }
```

---

## Test: Text finite through direct Program

This case requires folded and runtime text conversion to produce the same specified special or finite value.

### Case description

```yaml
gesBlock: case
id: text-finite-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module textnumber
constant $text be "1.5"
on Start(value) {
  emit Literal(value: "1.5" as :Number)
  emit Constant(value: $text as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Text", value: "1.5" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.binary64", value: "1.5" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.binary64", value: "1.5" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.binary64", value: "1.5" }
```

---

## Test: Text finite through binary Program

This case requires folded and runtime text conversion to produce the same specified special or finite value.

### Case description

```yaml
gesBlock: case
id: text-finite-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module textnumber
constant $text be "1.5"
on Start(value) {
  emit Literal(value: "1.5" as :Number)
  emit Constant(value: $text as :Number)
  emit Runtime(value: value as :Number)
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
        - name: value
          value: { type: ":Text", value: "1.5" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.binary64", value: "1.5" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.binary64", value: "1.5" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.binary64", value: "1.5" }
```

---

## Test: Explicit dynamic random seed preserves above53 through direct Program

This case checks eight independent known-answer samples derived from the
SplitMix64 and xoshiro256** rules in `specs/Semantics/Determinism.md`. The explicit
Number conversion required for an unknown seed must preserve all Int64 bits.
Each scope also restores the parent stream before the final sample.

### Case description

```yaml
gesBlock: case
id: random-seed-above53-direct
random:
  seed: 0
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module castseed
constant $original be 9007199254740993
on Start(value) {
  random with 9007199254740993 {
    emit Literal(value: [
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100
    ])
  }
  random with $original {
    emit Constant(value: [
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100
    ])
  }
  random with (value as :Number) {
    emit Runtime(value: [
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100
    ])
  }
  emit Parent(value: random from -100 to 100)
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
        - name: value
          value: { type: ":Number.int64", value: "9007199254740993" }
    local:
      - name: Literal
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "87" }
                - { type: ":Number.int64", value: "-38" }
                - { type: ":Number.int64", value: "-99" }
                - { type: ":Number.int64", value: "-45" }
                - { type: ":Number.int64", value: "-14" }
                - { type: ":Number.int64", value: "49" }
                - { type: ":Number.int64", value: "58" }
                - { type: ":Number.int64", value: "-99" }
      - name: Constant
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "87" }
                - { type: ":Number.int64", value: "-38" }
                - { type: ":Number.int64", value: "-99" }
                - { type: ":Number.int64", value: "-45" }
                - { type: ":Number.int64", value: "-14" }
                - { type: ":Number.int64", value: "49" }
                - { type: ":Number.int64", value: "58" }
                - { type: ":Number.int64", value: "-99" }
      - name: Runtime
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "87" }
                - { type: ":Number.int64", value: "-38" }
                - { type: ":Number.int64", value: "-99" }
                - { type: ":Number.int64", value: "-45" }
                - { type: ":Number.int64", value: "-14" }
                - { type: ":Number.int64", value: "49" }
                - { type: ":Number.int64", value: "58" }
                - { type: ":Number.int64", value: "-99" }
      - name: Parent
        args:
          - name: value
            value: { type: ":Number.int64", value: "16" }
```

---

## Test: Explicit dynamic random seed preserves above53 through binary Program

This case checks eight independent known-answer samples derived from the
SplitMix64 and xoshiro256** rules in `specs/Semantics/Determinism.md`. The explicit
Number conversion required for an unknown seed must preserve all Int64 bits.
Each scope also restores the parent stream before the final sample.

### Case description

```yaml
gesBlock: case
id: random-seed-above53-binary
random:
  seed: 0
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module castseed
constant $original be 9007199254740993
on Start(value) {
  random with 9007199254740993 {
    emit Literal(value: [
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100
    ])
  }
  random with $original {
    emit Constant(value: [
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100
    ])
  }
  random with (value as :Number) {
    emit Runtime(value: [
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100
    ])
  }
  emit Parent(value: random from -100 to 100)
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
        - name: value
          value: { type: ":Number.int64", value: "9007199254740993" }
    local:
      - name: Literal
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "87" }
                - { type: ":Number.int64", value: "-38" }
                - { type: ":Number.int64", value: "-99" }
                - { type: ":Number.int64", value: "-45" }
                - { type: ":Number.int64", value: "-14" }
                - { type: ":Number.int64", value: "49" }
                - { type: ":Number.int64", value: "58" }
                - { type: ":Number.int64", value: "-99" }
      - name: Constant
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "87" }
                - { type: ":Number.int64", value: "-38" }
                - { type: ":Number.int64", value: "-99" }
                - { type: ":Number.int64", value: "-45" }
                - { type: ":Number.int64", value: "-14" }
                - { type: ":Number.int64", value: "49" }
                - { type: ":Number.int64", value: "58" }
                - { type: ":Number.int64", value: "-99" }
      - name: Runtime
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "87" }
                - { type: ":Number.int64", value: "-38" }
                - { type: ":Number.int64", value: "-99" }
                - { type: ":Number.int64", value: "-45" }
                - { type: ":Number.int64", value: "-14" }
                - { type: ":Number.int64", value: "49" }
                - { type: ":Number.int64", value: "58" }
                - { type: ":Number.int64", value: "-99" }
      - name: Parent
        args:
          - name: value
            value: { type: ":Number.int64", value: "16" }
```

---

## Test: Explicit dynamic random seed preserves int64-max through direct Program

This case checks eight independent known-answer samples derived from the
SplitMix64 and xoshiro256** rules in `specs/Semantics/Determinism.md`. The explicit
Number conversion required for an unknown seed must preserve all Int64 bits.
Each scope also restores the parent stream before the final sample.

### Case description

```yaml
gesBlock: case
id: random-seed-int64-max-direct
random:
  seed: 0
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module castseed
constant $original be 9223372036854775807
on Start(value) {
  random with 9223372036854775807 {
    emit Literal(value: [
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100
    ])
  }
  random with $original {
    emit Constant(value: [
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100
    ])
  }
  random with (value as :Number) {
    emit Runtime(value: [
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100
    ])
  }
  emit Parent(value: random from -100 to 100)
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
        - name: value
          value: { type: ":Number.int64", value: "9223372036854775807" }
    local:
      - name: Literal
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "73" }
                - { type: ":Number.int64", value: "15" }
                - { type: ":Number.int64", value: "63" }
                - { type: ":Number.int64", value: "-23" }
                - { type: ":Number.int64", value: "93" }
                - { type: ":Number.int64", value: "-74" }
                - { type: ":Number.int64", value: "25" }
                - { type: ":Number.int64", value: "-90" }
      - name: Constant
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "73" }
                - { type: ":Number.int64", value: "15" }
                - { type: ":Number.int64", value: "63" }
                - { type: ":Number.int64", value: "-23" }
                - { type: ":Number.int64", value: "93" }
                - { type: ":Number.int64", value: "-74" }
                - { type: ":Number.int64", value: "25" }
                - { type: ":Number.int64", value: "-90" }
      - name: Runtime
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "73" }
                - { type: ":Number.int64", value: "15" }
                - { type: ":Number.int64", value: "63" }
                - { type: ":Number.int64", value: "-23" }
                - { type: ":Number.int64", value: "93" }
                - { type: ":Number.int64", value: "-74" }
                - { type: ":Number.int64", value: "25" }
                - { type: ":Number.int64", value: "-90" }
      - name: Parent
        args:
          - name: value
            value: { type: ":Number.int64", value: "16" }
```

---

## Test: Explicit dynamic random seed preserves int64-max through binary Program

This case checks eight independent known-answer samples derived from the
SplitMix64 and xoshiro256** rules in `specs/Semantics/Determinism.md`. The explicit
Number conversion required for an unknown seed must preserve all Int64 bits.
Each scope also restores the parent stream before the final sample.

### Case description

```yaml
gesBlock: case
id: random-seed-int64-max-binary
random:
  seed: 0
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module castseed
constant $original be 9223372036854775807
on Start(value) {
  random with 9223372036854775807 {
    emit Literal(value: [
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100
    ])
  }
  random with $original {
    emit Constant(value: [
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100
    ])
  }
  random with (value as :Number) {
    emit Runtime(value: [
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100
    ])
  }
  emit Parent(value: random from -100 to 100)
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
        - name: value
          value: { type: ":Number.int64", value: "9223372036854775807" }
    local:
      - name: Literal
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "73" }
                - { type: ":Number.int64", value: "15" }
                - { type: ":Number.int64", value: "63" }
                - { type: ":Number.int64", value: "-23" }
                - { type: ":Number.int64", value: "93" }
                - { type: ":Number.int64", value: "-74" }
                - { type: ":Number.int64", value: "25" }
                - { type: ":Number.int64", value: "-90" }
      - name: Constant
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "73" }
                - { type: ":Number.int64", value: "15" }
                - { type: ":Number.int64", value: "63" }
                - { type: ":Number.int64", value: "-23" }
                - { type: ":Number.int64", value: "93" }
                - { type: ":Number.int64", value: "-74" }
                - { type: ":Number.int64", value: "25" }
                - { type: ":Number.int64", value: "-90" }
      - name: Runtime
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "73" }
                - { type: ":Number.int64", value: "15" }
                - { type: ":Number.int64", value: "63" }
                - { type: ":Number.int64", value: "-23" }
                - { type: ":Number.int64", value: "93" }
                - { type: ":Number.int64", value: "-74" }
                - { type: ":Number.int64", value: "25" }
                - { type: ":Number.int64", value: "-90" }
      - name: Parent
        args:
          - name: value
            value: { type: ":Number.int64", value: "16" }
```

---

## Test: Explicit dynamic random seed preserves int64-min through direct Program

This case checks eight independent known-answer samples derived from the
SplitMix64 and xoshiro256** rules in `specs/Semantics/Determinism.md`. The explicit
Number conversion required for an unknown seed must preserve all Int64 bits.
Each scope also restores the parent stream before the final sample.

### Case description

```yaml
gesBlock: case
id: random-seed-int64-min-direct
random:
  seed: 0
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module castseed
constant $original be -9223372036854775808
on Start(value) {
  random with (-9223372036854775808 as :Number) {
    emit Literal(value: [
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100
    ])
  }
  random with ($original as :Number) {
    emit Constant(value: [
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100
    ])
  }
  random with (value as :Number) {
    emit Runtime(value: [
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100
    ])
  }
  emit Parent(value: random from -100 to 100)
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
        - name: value
          value: { type: ":Number.int64", value: "-9223372036854775808" }
    local:
      - name: Literal
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "41" }
                - { type: ":Number.int64", value: "96" }
                - { type: ":Number.int64", value: "-53" }
                - { type: ":Number.int64", value: "-71" }
                - { type: ":Number.int64", value: "-74" }
                - { type: ":Number.int64", value: "43" }
                - { type: ":Number.int64", value: "56" }
                - { type: ":Number.int64", value: "-52" }
      - name: Constant
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "41" }
                - { type: ":Number.int64", value: "96" }
                - { type: ":Number.int64", value: "-53" }
                - { type: ":Number.int64", value: "-71" }
                - { type: ":Number.int64", value: "-74" }
                - { type: ":Number.int64", value: "43" }
                - { type: ":Number.int64", value: "56" }
                - { type: ":Number.int64", value: "-52" }
      - name: Runtime
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "41" }
                - { type: ":Number.int64", value: "96" }
                - { type: ":Number.int64", value: "-53" }
                - { type: ":Number.int64", value: "-71" }
                - { type: ":Number.int64", value: "-74" }
                - { type: ":Number.int64", value: "43" }
                - { type: ":Number.int64", value: "56" }
                - { type: ":Number.int64", value: "-52" }
      - name: Parent
        args:
          - name: value
            value: { type: ":Number.int64", value: "16" }
```

---

## Test: Explicit dynamic random seed preserves int64-min through binary Program

This case checks eight independent known-answer samples derived from the
SplitMix64 and xoshiro256** rules in `specs/Semantics/Determinism.md`. The explicit
Number conversion required for an unknown seed must preserve all Int64 bits.
Each scope also restores the parent stream before the final sample.

### Case description

```yaml
gesBlock: case
id: random-seed-int64-min-binary
random:
  seed: 0
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module castseed
constant $original be -9223372036854775808
on Start(value) {
  random with (-9223372036854775808 as :Number) {
    emit Literal(value: [
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100
    ])
  }
  random with ($original as :Number) {
    emit Constant(value: [
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100
    ])
  }
  random with (value as :Number) {
    emit Runtime(value: [
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100,
      random from -100 to 100
    ])
  }
  emit Parent(value: random from -100 to 100)
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
        - name: value
          value: { type: ":Number.int64", value: "-9223372036854775808" }
    local:
      - name: Literal
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "41" }
                - { type: ":Number.int64", value: "96" }
                - { type: ":Number.int64", value: "-53" }
                - { type: ":Number.int64", value: "-71" }
                - { type: ":Number.int64", value: "-74" }
                - { type: ":Number.int64", value: "43" }
                - { type: ":Number.int64", value: "56" }
                - { type: ":Number.int64", value: "-52" }
      - name: Constant
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "41" }
                - { type: ":Number.int64", value: "96" }
                - { type: ":Number.int64", value: "-53" }
                - { type: ":Number.int64", value: "-71" }
                - { type: ":Number.int64", value: "-74" }
                - { type: ":Number.int64", value: "43" }
                - { type: ":Number.int64", value: "56" }
                - { type: ":Number.int64", value: "-52" }
      - name: Runtime
        args:
          - name: value
            value:
              type: ":List"
              items:
                - { type: ":Number.int64", value: "41" }
                - { type: ":Number.int64", value: "96" }
                - { type: ":Number.int64", value: "-53" }
                - { type: ":Number.int64", value: "-71" }
                - { type: ":Number.int64", value: "-74" }
                - { type: ":Number.int64", value: "43" }
                - { type: ":Number.int64", value: "56" }
                - { type: ":Number.int64", value: "-52" }
      - name: Parent
        args:
          - name: value
            value: { type: ":Number.int64", value: "16" }
```
