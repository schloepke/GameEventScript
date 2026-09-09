---
formatVersion: 1
suiteId: runtime.numeric.quantity-powers
title: Quantity power consistency
kind: scriptApi
level: atomic
categories: [conformance]
---

# Quantity power consistency

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite checks the power contract in `specs/Language.md` with literal, program-constant, and message operands. It observes exact numeric values and units, plus invalid mathematics canonicalized to nothing at the value boundary, both directly and after a binary Program roundtrip.

---

## Test: integer unit exponents through direct Program

This case verifies that quantity exponents are invalid even when their unit matches the integer base and their value is zero or one.

### Case description

```yaml
gesBlock: case
id: integer-unit-exponents-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module quantitypowers
constant $base be 10m
constant $zero be 0m
constant $one be 1m
constant $two be 2m
constant $fraction be 0.5m
constant $other be 1s
on Start(value, zero, one, two, fraction, other) {
  emit Literal(zero: (10m) ^ (0m), one: (10m) ^ (1m), two: (10m) ^ (2m), fraction: (10m) ^ (0.5m), other: (10m) ^ (1s))
  emit Constant(zero: $base ^ $zero, one: $base ^ $one, two: $base ^ $two, fraction: $base ^ $fraction, other: $base ^ $other)
  emit Runtime(zero: value ^ zero, one: value ^ one, two: value ^ two, fraction: value ^ fraction, other: value ^ other)
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
          value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
        - name: zero
          value: { type: ":Quantity.int64", value: "0", unit: ":meter" }
        - name: one
          value: { type: ":Quantity.int64", value: "1", unit: ":meter" }
        - name: two
          value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
        - name: fraction
          value: { type: ":Quantity.binary64", value: "0.5", unit: ":meter" }
        - name: other
          value: { type: ":Quantity.int64", value: "1", unit: ":second" }
    local:
      - name: Literal
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: one
            value: { type: ":Nothing" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: other
            value: { type: ":Nothing" }
      - name: Constant
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: one
            value: { type: ":Nothing" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: other
            value: { type: ":Nothing" }
      - name: Runtime
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: one
            value: { type: ":Nothing" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: other
            value: { type: ":Nothing" }
```

---

## Test: integer unit exponents through binary roundtrip

This case verifies that quantity exponents are invalid even when their unit matches the integer base and their value is zero or one.

### Case description

```yaml
gesBlock: case
id: integer-unit-exponents-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module quantitypowers
constant $base be 10m
constant $zero be 0m
constant $one be 1m
constant $two be 2m
constant $fraction be 0.5m
constant $other be 1s
on Start(value, zero, one, two, fraction, other) {
  emit Literal(zero: (10m) ^ (0m), one: (10m) ^ (1m), two: (10m) ^ (2m), fraction: (10m) ^ (0.5m), other: (10m) ^ (1s))
  emit Constant(zero: $base ^ $zero, one: $base ^ $one, two: $base ^ $two, fraction: $base ^ $fraction, other: $base ^ $other)
  emit Runtime(zero: value ^ zero, one: value ^ one, two: value ^ two, fraction: value ^ fraction, other: value ^ other)
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
          value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
        - name: zero
          value: { type: ":Quantity.int64", value: "0", unit: ":meter" }
        - name: one
          value: { type: ":Quantity.int64", value: "1", unit: ":meter" }
        - name: two
          value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
        - name: fraction
          value: { type: ":Quantity.binary64", value: "0.5", unit: ":meter" }
        - name: other
          value: { type: ":Quantity.int64", value: "1", unit: ":second" }
    local:
      - name: Literal
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: one
            value: { type: ":Nothing" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: other
            value: { type: ":Nothing" }
      - name: Constant
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: one
            value: { type: ":Nothing" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: other
            value: { type: ":Nothing" }
      - name: Runtime
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: one
            value: { type: ":Nothing" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: other
            value: { type: ":Nothing" }
```

---

## Test: fractional unit exponents through direct Program

This case verifies that quantity exponents are invalid for a binary64 Quantity base, including mixed numeric storage and different units.

### Case description

```yaml
gesBlock: case
id: fractional-unit-exponents-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module quantitypowers
constant $base be 10.5m
constant $zero be 0m
constant $one be 1m
constant $two be 2m
constant $fraction be 0.5m
constant $other be 1s
on Start(value, zero, one, two, fraction, other) {
  emit Literal(zero: (10.5m) ^ (0m), one: (10.5m) ^ (1m), two: (10.5m) ^ (2m), fraction: (10.5m) ^ (0.5m), other: (10.5m) ^ (1s))
  emit Constant(zero: $base ^ $zero, one: $base ^ $one, two: $base ^ $two, fraction: $base ^ $fraction, other: $base ^ $other)
  emit Runtime(zero: value ^ zero, one: value ^ one, two: value ^ two, fraction: value ^ fraction, other: value ^ other)
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
          value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
        - name: zero
          value: { type: ":Quantity.int64", value: "0", unit: ":meter" }
        - name: one
          value: { type: ":Quantity.int64", value: "1", unit: ":meter" }
        - name: two
          value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
        - name: fraction
          value: { type: ":Quantity.binary64", value: "0.5", unit: ":meter" }
        - name: other
          value: { type: ":Quantity.int64", value: "1", unit: ":second" }
    local:
      - name: Literal
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: one
            value: { type: ":Nothing" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: other
            value: { type: ":Nothing" }
      - name: Constant
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: one
            value: { type: ":Nothing" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: other
            value: { type: ":Nothing" }
      - name: Runtime
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: one
            value: { type: ":Nothing" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: other
            value: { type: ":Nothing" }
```

---

## Test: fractional unit exponents through binary roundtrip

This case verifies that quantity exponents are invalid for a binary64 Quantity base, including mixed numeric storage and different units.

### Case description

```yaml
gesBlock: case
id: fractional-unit-exponents-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module quantitypowers
constant $base be 10.5m
constant $zero be 0m
constant $one be 1m
constant $two be 2m
constant $fraction be 0.5m
constant $other be 1s
on Start(value, zero, one, two, fraction, other) {
  emit Literal(zero: (10.5m) ^ (0m), one: (10.5m) ^ (1m), two: (10.5m) ^ (2m), fraction: (10.5m) ^ (0.5m), other: (10.5m) ^ (1s))
  emit Constant(zero: $base ^ $zero, one: $base ^ $one, two: $base ^ $two, fraction: $base ^ $fraction, other: $base ^ $other)
  emit Runtime(zero: value ^ zero, one: value ^ one, two: value ^ two, fraction: value ^ fraction, other: value ^ other)
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
          value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
        - name: zero
          value: { type: ":Quantity.int64", value: "0", unit: ":meter" }
        - name: one
          value: { type: ":Quantity.int64", value: "1", unit: ":meter" }
        - name: two
          value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
        - name: fraction
          value: { type: ":Quantity.binary64", value: "0.5", unit: ":meter" }
        - name: other
          value: { type: ":Quantity.int64", value: "1", unit: ":second" }
    local:
      - name: Literal
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: one
            value: { type: ":Nothing" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: other
            value: { type: ":Nothing" }
      - name: Constant
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: one
            value: { type: ":Nothing" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: other
            value: { type: ":Nothing" }
      - name: Runtime
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: one
            value: { type: ":Nothing" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: other
            value: { type: ":Nothing" }
```

---

## Test: integer unitless exponents through direct Program

This case verifies that only unitless zero and one are valid Quantity exponents, including Boolean and Percentage numeric coercion.

### Case description

```yaml
gesBlock: case
id: integer-unitless-exponents-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module quantitypowers
constant $base be 10m
constant $zero be 0
constant $one be 1
constant $two be 2
constant $fraction be 0.5
constant $enabled be true
constant $disabled be false
constant $full be 100%
constant $zeroPercent be 0%
on Start(value, zero, one, two, fraction, enabled, disabled, full, zeroPercent) {
  emit Literal(zero: (10m) ^ (0), one: (10m) ^ (1), two: (10m) ^ (2), fraction: (10m) ^ (0.5), enabled: (10m) ^ (true), disabled: (10m) ^ (false), full: (10m) ^ (100%), zeroPercent: (10m) ^ (0%))
  emit Constant(zero: $base ^ $zero, one: $base ^ $one, two: $base ^ $two, fraction: $base ^ $fraction, enabled: $base ^ $enabled, disabled: $base ^ $disabled, full: $base ^ $full, zeroPercent: $base ^ $zeroPercent)
  emit Runtime(zero: value ^ zero, one: value ^ one, two: value ^ two, fraction: value ^ fraction, enabled: value ^ enabled, disabled: value ^ disabled, full: value ^ full, zeroPercent: value ^ zeroPercent)
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
          value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
        - name: zero
          value: { type: ":Number.int64", value: "0" }
        - name: one
          value: { type: ":Number.int64", value: "1" }
        - name: two
          value: { type: ":Number.int64", value: "2" }
        - name: fraction
          value: { type: ":Number.binary64", value: "0.5" }
        - name: enabled
          value: { type: ":Boolean", value: true }
        - name: disabled
          value: { type: ":Boolean", value: false }
        - name: full
          value: { type: ":Percentage", value: "1" }
        - name: zeroPercent
          value: { type: ":Percentage", value: "0" }
    local:
      - name: Literal
        args:
          - name: zero
            value: { type: ":Number.int64", value: "1" }
          - name: one
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: enabled
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: disabled
            value: { type: ":Number.int64", value: "1" }
          - name: full
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: zeroPercent
            value: { type: ":Number.int64", value: "1" }
      - name: Constant
        args:
          - name: zero
            value: { type: ":Number.int64", value: "1" }
          - name: one
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: enabled
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: disabled
            value: { type: ":Number.int64", value: "1" }
          - name: full
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: zeroPercent
            value: { type: ":Number.int64", value: "1" }
      - name: Runtime
        args:
          - name: zero
            value: { type: ":Number.int64", value: "1" }
          - name: one
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: enabled
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: disabled
            value: { type: ":Number.int64", value: "1" }
          - name: full
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: zeroPercent
            value: { type: ":Number.int64", value: "1" }
```

---

## Test: integer unitless exponents through binary roundtrip

This case verifies that only unitless zero and one are valid Quantity exponents, including Boolean and Percentage numeric coercion.

### Case description

```yaml
gesBlock: case
id: integer-unitless-exponents-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module quantitypowers
constant $base be 10m
constant $zero be 0
constant $one be 1
constant $two be 2
constant $fraction be 0.5
constant $enabled be true
constant $disabled be false
constant $full be 100%
constant $zeroPercent be 0%
on Start(value, zero, one, two, fraction, enabled, disabled, full, zeroPercent) {
  emit Literal(zero: (10m) ^ (0), one: (10m) ^ (1), two: (10m) ^ (2), fraction: (10m) ^ (0.5), enabled: (10m) ^ (true), disabled: (10m) ^ (false), full: (10m) ^ (100%), zeroPercent: (10m) ^ (0%))
  emit Constant(zero: $base ^ $zero, one: $base ^ $one, two: $base ^ $two, fraction: $base ^ $fraction, enabled: $base ^ $enabled, disabled: $base ^ $disabled, full: $base ^ $full, zeroPercent: $base ^ $zeroPercent)
  emit Runtime(zero: value ^ zero, one: value ^ one, two: value ^ two, fraction: value ^ fraction, enabled: value ^ enabled, disabled: value ^ disabled, full: value ^ full, zeroPercent: value ^ zeroPercent)
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
          value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
        - name: zero
          value: { type: ":Number.int64", value: "0" }
        - name: one
          value: { type: ":Number.int64", value: "1" }
        - name: two
          value: { type: ":Number.int64", value: "2" }
        - name: fraction
          value: { type: ":Number.binary64", value: "0.5" }
        - name: enabled
          value: { type: ":Boolean", value: true }
        - name: disabled
          value: { type: ":Boolean", value: false }
        - name: full
          value: { type: ":Percentage", value: "1" }
        - name: zeroPercent
          value: { type: ":Percentage", value: "0" }
    local:
      - name: Literal
        args:
          - name: zero
            value: { type: ":Number.int64", value: "1" }
          - name: one
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: enabled
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: disabled
            value: { type: ":Number.int64", value: "1" }
          - name: full
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: zeroPercent
            value: { type: ":Number.int64", value: "1" }
      - name: Constant
        args:
          - name: zero
            value: { type: ":Number.int64", value: "1" }
          - name: one
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: enabled
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: disabled
            value: { type: ":Number.int64", value: "1" }
          - name: full
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: zeroPercent
            value: { type: ":Number.int64", value: "1" }
      - name: Runtime
        args:
          - name: zero
            value: { type: ":Number.int64", value: "1" }
          - name: one
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: enabled
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: disabled
            value: { type: ":Number.int64", value: "1" }
          - name: full
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: zeroPercent
            value: { type: ":Number.int64", value: "1" }
```

---

## Test: fractional unitless exponents through direct Program

This case verifies that exponent one retains the fractional Quantity value and unit; exponent zero returns unitless one.

### Case description

```yaml
gesBlock: case
id: fractional-unitless-exponents-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module quantitypowers
constant $base be 10.5m
constant $zero be 0
constant $one be 1
constant $two be 2
constant $fraction be 0.5
constant $enabled be true
constant $disabled be false
constant $full be 100%
constant $zeroPercent be 0%
on Start(value, zero, one, two, fraction, enabled, disabled, full, zeroPercent) {
  emit Literal(zero: (10.5m) ^ (0), one: (10.5m) ^ (1), two: (10.5m) ^ (2), fraction: (10.5m) ^ (0.5), enabled: (10.5m) ^ (true), disabled: (10.5m) ^ (false), full: (10.5m) ^ (100%), zeroPercent: (10.5m) ^ (0%))
  emit Constant(zero: $base ^ $zero, one: $base ^ $one, two: $base ^ $two, fraction: $base ^ $fraction, enabled: $base ^ $enabled, disabled: $base ^ $disabled, full: $base ^ $full, zeroPercent: $base ^ $zeroPercent)
  emit Runtime(zero: value ^ zero, one: value ^ one, two: value ^ two, fraction: value ^ fraction, enabled: value ^ enabled, disabled: value ^ disabled, full: value ^ full, zeroPercent: value ^ zeroPercent)
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
          value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
        - name: zero
          value: { type: ":Number.int64", value: "0" }
        - name: one
          value: { type: ":Number.int64", value: "1" }
        - name: two
          value: { type: ":Number.int64", value: "2" }
        - name: fraction
          value: { type: ":Number.binary64", value: "0.5" }
        - name: enabled
          value: { type: ":Boolean", value: true }
        - name: disabled
          value: { type: ":Boolean", value: false }
        - name: full
          value: { type: ":Percentage", value: "1" }
        - name: zeroPercent
          value: { type: ":Percentage", value: "0" }
    local:
      - name: Literal
        args:
          - name: zero
            value: { type: ":Number.int64", value: "1" }
          - name: one
            value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: enabled
            value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
          - name: disabled
            value: { type: ":Number.int64", value: "1" }
          - name: full
            value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
          - name: zeroPercent
            value: { type: ":Number.int64", value: "1" }
      - name: Constant
        args:
          - name: zero
            value: { type: ":Number.int64", value: "1" }
          - name: one
            value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: enabled
            value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
          - name: disabled
            value: { type: ":Number.int64", value: "1" }
          - name: full
            value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
          - name: zeroPercent
            value: { type: ":Number.int64", value: "1" }
      - name: Runtime
        args:
          - name: zero
            value: { type: ":Number.int64", value: "1" }
          - name: one
            value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: enabled
            value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
          - name: disabled
            value: { type: ":Number.int64", value: "1" }
          - name: full
            value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
          - name: zeroPercent
            value: { type: ":Number.int64", value: "1" }
```

---

## Test: fractional unitless exponents through binary roundtrip

This case verifies that exponent one retains the fractional Quantity value and unit; exponent zero returns unitless one.

### Case description

```yaml
gesBlock: case
id: fractional-unitless-exponents-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module quantitypowers
constant $base be 10.5m
constant $zero be 0
constant $one be 1
constant $two be 2
constant $fraction be 0.5
constant $enabled be true
constant $disabled be false
constant $full be 100%
constant $zeroPercent be 0%
on Start(value, zero, one, two, fraction, enabled, disabled, full, zeroPercent) {
  emit Literal(zero: (10.5m) ^ (0), one: (10.5m) ^ (1), two: (10.5m) ^ (2), fraction: (10.5m) ^ (0.5), enabled: (10.5m) ^ (true), disabled: (10.5m) ^ (false), full: (10.5m) ^ (100%), zeroPercent: (10.5m) ^ (0%))
  emit Constant(zero: $base ^ $zero, one: $base ^ $one, two: $base ^ $two, fraction: $base ^ $fraction, enabled: $base ^ $enabled, disabled: $base ^ $disabled, full: $base ^ $full, zeroPercent: $base ^ $zeroPercent)
  emit Runtime(zero: value ^ zero, one: value ^ one, two: value ^ two, fraction: value ^ fraction, enabled: value ^ enabled, disabled: value ^ disabled, full: value ^ full, zeroPercent: value ^ zeroPercent)
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
          value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
        - name: zero
          value: { type: ":Number.int64", value: "0" }
        - name: one
          value: { type: ":Number.int64", value: "1" }
        - name: two
          value: { type: ":Number.int64", value: "2" }
        - name: fraction
          value: { type: ":Number.binary64", value: "0.5" }
        - name: enabled
          value: { type: ":Boolean", value: true }
        - name: disabled
          value: { type: ":Boolean", value: false }
        - name: full
          value: { type: ":Percentage", value: "1" }
        - name: zeroPercent
          value: { type: ":Percentage", value: "0" }
    local:
      - name: Literal
        args:
          - name: zero
            value: { type: ":Number.int64", value: "1" }
          - name: one
            value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: enabled
            value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
          - name: disabled
            value: { type: ":Number.int64", value: "1" }
          - name: full
            value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
          - name: zeroPercent
            value: { type: ":Number.int64", value: "1" }
      - name: Constant
        args:
          - name: zero
            value: { type: ":Number.int64", value: "1" }
          - name: one
            value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: enabled
            value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
          - name: disabled
            value: { type: ":Number.int64", value: "1" }
          - name: full
            value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
          - name: zeroPercent
            value: { type: ":Number.int64", value: "1" }
      - name: Runtime
        args:
          - name: zero
            value: { type: ":Number.int64", value: "1" }
          - name: one
            value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
          - name: two
            value: { type: ":Nothing" }
          - name: fraction
            value: { type: ":Nothing" }
          - name: enabled
            value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
          - name: disabled
            value: { type: ":Number.int64", value: "1" }
          - name: full
            value: { type: ":Quantity.binary64", value: "10.5", unit: ":meter" }
          - name: zeroPercent
            value: { type: ":Number.int64", value: "1" }
```

---

## Test: unitless base controls through direct Program

This case verifies that a unitless base also rejects Quantity exponents while ordinary numeric powers retain their values.

### Case description

```yaml
gesBlock: case
id: unitless-base-controls-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module quantitypowers
constant $base be 9
constant $unitZero be 0m
constant $unitOne be 1m
constant $unitTwo be 2m
constant $zero be 0
constant $one be 1
constant $two be 2
constant $root be 0.5
on Start(value, unitZero, unitOne, unitTwo, zero, one, two, root) {
  emit Literal(unitZero: (9) ^ (0m), unitOne: (9) ^ (1m), unitTwo: (9) ^ (2m), zero: (9) ^ (0), one: (9) ^ (1), two: (9) ^ (2), root: (9) ^ (0.5))
  emit Constant(unitZero: $base ^ $unitZero, unitOne: $base ^ $unitOne, unitTwo: $base ^ $unitTwo, zero: $base ^ $zero, one: $base ^ $one, two: $base ^ $two, root: $base ^ $root)
  emit Runtime(unitZero: value ^ unitZero, unitOne: value ^ unitOne, unitTwo: value ^ unitTwo, zero: value ^ zero, one: value ^ one, two: value ^ two, root: value ^ root)
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
          value: { type: ":Number.int64", value: "9" }
        - name: unitZero
          value: { type: ":Quantity.int64", value: "0", unit: ":meter" }
        - name: unitOne
          value: { type: ":Quantity.int64", value: "1", unit: ":meter" }
        - name: unitTwo
          value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
        - name: zero
          value: { type: ":Number.int64", value: "0" }
        - name: one
          value: { type: ":Number.int64", value: "1" }
        - name: two
          value: { type: ":Number.int64", value: "2" }
        - name: root
          value: { type: ":Number.binary64", value: "0.5" }
    local:
      - name: Literal
        args:
          - name: unitZero
            value: { type: ":Nothing" }
          - name: unitOne
            value: { type: ":Nothing" }
          - name: unitTwo
            value: { type: ":Nothing" }
          - name: zero
            value: { type: ":Number.int64", value: "1" }
          - name: one
            value: { type: ":Number.int64", value: "9" }
          - name: two
            value: { type: ":Number.int64", value: "81" }
          - name: root
            value: { type: ":Number.int64", value: "3" }
      - name: Constant
        args:
          - name: unitZero
            value: { type: ":Nothing" }
          - name: unitOne
            value: { type: ":Nothing" }
          - name: unitTwo
            value: { type: ":Nothing" }
          - name: zero
            value: { type: ":Number.int64", value: "1" }
          - name: one
            value: { type: ":Number.int64", value: "9" }
          - name: two
            value: { type: ":Number.int64", value: "81" }
          - name: root
            value: { type: ":Number.int64", value: "3" }
      - name: Runtime
        args:
          - name: unitZero
            value: { type: ":Nothing" }
          - name: unitOne
            value: { type: ":Nothing" }
          - name: unitTwo
            value: { type: ":Nothing" }
          - name: zero
            value: { type: ":Number.int64", value: "1" }
          - name: one
            value: { type: ":Number.int64", value: "9" }
          - name: two
            value: { type: ":Number.int64", value: "81" }
          - name: root
            value: { type: ":Number.int64", value: "3" }
```

---

## Test: unitless base controls through binary roundtrip

This case verifies that a unitless base also rejects Quantity exponents while ordinary numeric powers retain their values.

### Case description

```yaml
gesBlock: case
id: unitless-base-controls-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module quantitypowers
constant $base be 9
constant $unitZero be 0m
constant $unitOne be 1m
constant $unitTwo be 2m
constant $zero be 0
constant $one be 1
constant $two be 2
constant $root be 0.5
on Start(value, unitZero, unitOne, unitTwo, zero, one, two, root) {
  emit Literal(unitZero: (9) ^ (0m), unitOne: (9) ^ (1m), unitTwo: (9) ^ (2m), zero: (9) ^ (0), one: (9) ^ (1), two: (9) ^ (2), root: (9) ^ (0.5))
  emit Constant(unitZero: $base ^ $unitZero, unitOne: $base ^ $unitOne, unitTwo: $base ^ $unitTwo, zero: $base ^ $zero, one: $base ^ $one, two: $base ^ $two, root: $base ^ $root)
  emit Runtime(unitZero: value ^ unitZero, unitOne: value ^ unitOne, unitTwo: value ^ unitTwo, zero: value ^ zero, one: value ^ one, two: value ^ two, root: value ^ root)
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
          value: { type: ":Number.int64", value: "9" }
        - name: unitZero
          value: { type: ":Quantity.int64", value: "0", unit: ":meter" }
        - name: unitOne
          value: { type: ":Quantity.int64", value: "1", unit: ":meter" }
        - name: unitTwo
          value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
        - name: zero
          value: { type: ":Number.int64", value: "0" }
        - name: one
          value: { type: ":Number.int64", value: "1" }
        - name: two
          value: { type: ":Number.int64", value: "2" }
        - name: root
          value: { type: ":Number.binary64", value: "0.5" }
    local:
      - name: Literal
        args:
          - name: unitZero
            value: { type: ":Nothing" }
          - name: unitOne
            value: { type: ":Nothing" }
          - name: unitTwo
            value: { type: ":Nothing" }
          - name: zero
            value: { type: ":Number.int64", value: "1" }
          - name: one
            value: { type: ":Number.int64", value: "9" }
          - name: two
            value: { type: ":Number.int64", value: "81" }
          - name: root
            value: { type: ":Number.int64", value: "3" }
      - name: Constant
        args:
          - name: unitZero
            value: { type: ":Nothing" }
          - name: unitOne
            value: { type: ":Nothing" }
          - name: unitTwo
            value: { type: ":Nothing" }
          - name: zero
            value: { type: ":Number.int64", value: "1" }
          - name: one
            value: { type: ":Number.int64", value: "9" }
          - name: two
            value: { type: ":Number.int64", value: "81" }
          - name: root
            value: { type: ":Number.int64", value: "3" }
      - name: Runtime
        args:
          - name: unitZero
            value: { type: ":Nothing" }
          - name: unitOne
            value: { type: ":Nothing" }
          - name: unitTwo
            value: { type: ":Nothing" }
          - name: zero
            value: { type: ":Number.int64", value: "1" }
          - name: one
            value: { type: ":Number.int64", value: "9" }
          - name: two
            value: { type: ":Number.int64", value: "81" }
          - name: root
            value: { type: ":Number.int64", value: "3" }
```

---

## Test: absent base through direct Program

This case verifies that an absent base remains nothing regardless of whether its exponent is numeric, a Quantity, or also absent.

### Case description

```yaml
gesBlock: case
id: absent-base-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module quantitypowers
constant $base be nothing
constant $zero be 0
constant $unit be 2m
constant $absent be nothing
on Start(value, zero, unit, absent) {
  emit Literal(zero: (nothing) ^ (0), unit: (nothing) ^ (2m), absent: (nothing) ^ (nothing))
  emit Constant(zero: $base ^ $zero, unit: $base ^ $unit, absent: $base ^ $absent)
  emit Runtime(zero: value ^ zero, unit: value ^ unit, absent: value ^ absent)
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
          value: { type: ":Nothing" }
        - name: zero
          value: { type: ":Number.int64", value: "0" }
        - name: unit
          value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
        - name: absent
          value: { type: ":Nothing" }
    local:
      - name: Literal
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: unit
            value: { type: ":Nothing" }
          - name: absent
            value: { type: ":Nothing" }
      - name: Constant
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: unit
            value: { type: ":Nothing" }
          - name: absent
            value: { type: ":Nothing" }
      - name: Runtime
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: unit
            value: { type: ":Nothing" }
          - name: absent
            value: { type: ":Nothing" }
```

---

## Test: absent base through binary roundtrip

This case verifies that an absent base remains nothing regardless of whether its exponent is numeric, a Quantity, or also absent.

### Case description

```yaml
gesBlock: case
id: absent-base-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module quantitypowers
constant $base be nothing
constant $zero be 0
constant $unit be 2m
constant $absent be nothing
on Start(value, zero, unit, absent) {
  emit Literal(zero: (nothing) ^ (0), unit: (nothing) ^ (2m), absent: (nothing) ^ (nothing))
  emit Constant(zero: $base ^ $zero, unit: $base ^ $unit, absent: $base ^ $absent)
  emit Runtime(zero: value ^ zero, unit: value ^ unit, absent: value ^ absent)
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
          value: { type: ":Nothing" }
        - name: zero
          value: { type: ":Number.int64", value: "0" }
        - name: unit
          value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
        - name: absent
          value: { type: ":Nothing" }
    local:
      - name: Literal
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: unit
            value: { type: ":Nothing" }
          - name: absent
            value: { type: ":Nothing" }
      - name: Constant
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: unit
            value: { type: ":Nothing" }
          - name: absent
            value: { type: ":Nothing" }
      - name: Runtime
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: unit
            value: { type: ":Nothing" }
          - name: absent
            value: { type: ":Nothing" }
```

---

## Test: absent exponent through direct Program

This case verifies that an absent exponent produces nothing for a Quantity base in every evaluation path.

### Case description

```yaml
gesBlock: case
id: absent-exponent-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module quantitypowers
constant $base be 10m
constant $absent be nothing
on Start(value, absent) {
  emit Literal(absent: (10m) ^ (nothing))
  emit Constant(absent: $base ^ $absent)
  emit Runtime(absent: value ^ absent)
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
          value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
        - name: absent
          value: { type: ":Nothing" }
    local:
      - name: Literal
        args:
          - name: absent
            value: { type: ":Nothing" }
      - name: Constant
        args:
          - name: absent
            value: { type: ":Nothing" }
      - name: Runtime
        args:
          - name: absent
            value: { type: ":Nothing" }
```

---

## Test: absent exponent through binary roundtrip

This case verifies that an absent exponent produces nothing for a Quantity base in every evaluation path.

### Case description

```yaml
gesBlock: case
id: absent-exponent-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module quantitypowers
constant $base be 10m
constant $absent be nothing
on Start(value, absent) {
  emit Literal(absent: (10m) ^ (nothing))
  emit Constant(absent: $base ^ $absent)
  emit Runtime(absent: value ^ absent)
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
          value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
        - name: absent
          value: { type: ":Nothing" }
    local:
      - name: Literal
        args:
          - name: absent
            value: { type: ":Nothing" }
      - name: Constant
        args:
          - name: absent
            value: { type: ":Nothing" }
      - name: Runtime
        args:
          - name: absent
            value: { type: ":Nothing" }
```

---

## Test: nonnumeric base through direct Program

This case verifies that a nonnumeric base remains invalid even for exponent zero; a math-library special case must not turn it into one.

### Case description

```yaml
gesBlock: case
id: nonnumeric-base-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module quantitypowers
constant $base be 'invalid'
constant $zero be 0
constant $one be 1
constant $unit be 1m
on Start(value, zero, one, unit) {
  emit Literal(zero: ('invalid') ^ (0), one: ('invalid') ^ (1), unit: ('invalid') ^ (1m))
  emit Constant(zero: $base ^ $zero, one: $base ^ $one, unit: $base ^ $unit)
  emit Runtime(zero: value ^ zero, one: value ^ one, unit: value ^ unit)
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
        - name: zero
          value: { type: ":Number.int64", value: "0" }
        - name: one
          value: { type: ":Number.int64", value: "1" }
        - name: unit
          value: { type: ":Quantity.int64", value: "1", unit: ":meter" }
    local:
      - name: Literal
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: one
            value: { type: ":Nothing" }
          - name: unit
            value: { type: ":Nothing" }
      - name: Constant
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: one
            value: { type: ":Nothing" }
          - name: unit
            value: { type: ":Nothing" }
      - name: Runtime
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: one
            value: { type: ":Nothing" }
          - name: unit
            value: { type: ":Nothing" }
```

---

## Test: nonnumeric base through binary roundtrip

This case verifies that a nonnumeric base remains invalid even for exponent zero; a math-library special case must not turn it into one.

### Case description

```yaml
gesBlock: case
id: nonnumeric-base-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module quantitypowers
constant $base be 'invalid'
constant $zero be 0
constant $one be 1
constant $unit be 1m
on Start(value, zero, one, unit) {
  emit Literal(zero: ('invalid') ^ (0), one: ('invalid') ^ (1), unit: ('invalid') ^ (1m))
  emit Constant(zero: $base ^ $zero, one: $base ^ $one, unit: $base ^ $unit)
  emit Runtime(zero: value ^ zero, one: value ^ one, unit: value ^ unit)
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
        - name: zero
          value: { type: ":Number.int64", value: "0" }
        - name: one
          value: { type: ":Number.int64", value: "1" }
        - name: unit
          value: { type: ":Quantity.int64", value: "1", unit: ":meter" }
    local:
      - name: Literal
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: one
            value: { type: ":Nothing" }
          - name: unit
            value: { type: ":Nothing" }
      - name: Constant
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: one
            value: { type: ":Nothing" }
          - name: unit
            value: { type: ":Nothing" }
      - name: Runtime
        args:
          - name: zero
            value: { type: ":Nothing" }
          - name: one
            value: { type: ":Nothing" }
          - name: unit
            value: { type: ":Nothing" }
```
