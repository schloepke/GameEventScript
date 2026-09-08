---
formatVersion: 1
suiteId: runtime.numeric.integer-precision
title: Exact integer division modulo and remainder
kind: scriptApi
level: atomic
categories: [conformance]
---

# Exact integer division modulo and remainder

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite checks the exact integer rules in `specs/Semantics/Numbers.md` at the binary64 precision boundary and the Int64 limits. Literal folding, constant inlining, and input-driven execution have independent exact expectations, with and without Program serialization.

---

## Test: div above53 through direct Program

This case requires `9007199254740993 div 3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: div-above53-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module integerprecision
constant $dividend be 9007199254740993
constant $divisor be 3
on Start(value, divisor) {
  emit Literal(value: (9007199254740993) div (3))
  emit Constant(value: $dividend div $divisor)
  emit Runtime(value: value div divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "3002399751580331" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "3002399751580331" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "3002399751580331" }
```

---

## Test: div above53 through binary Program

This case requires `9007199254740993 div 3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: div-above53-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module integerprecision
constant $dividend be 9007199254740993
constant $divisor be 3
on Start(value, divisor) {
  emit Literal(value: (9007199254740993) div (3))
  emit Constant(value: $dividend div $divisor)
  emit Runtime(value: value div divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "3002399751580331" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "3002399751580331" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "3002399751580331" }
```

---

## Test: div negative-above53 through direct Program

This case requires `-9007199254740993 div 3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: div-negative-above53-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module integerprecision
constant $dividend be -9007199254740993
constant $divisor be 3
on Start(value, divisor) {
  emit Literal(value: (-9007199254740993) div (3))
  emit Constant(value: $dividend div $divisor)
  emit Runtime(value: value div divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "-3002399751580331" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "-3002399751580331" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "-3002399751580331" }
```

---

## Test: div negative-above53 through binary Program

This case requires `-9007199254740993 div 3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: div-negative-above53-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module integerprecision
constant $dividend be -9007199254740993
constant $divisor be 3
on Start(value, divisor) {
  emit Literal(value: (-9007199254740993) div (3))
  emit Constant(value: $dividend div $divisor)
  emit Runtime(value: value div divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "-3002399751580331" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "-3002399751580331" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "-3002399751580331" }
```

---

## Test: div negative-divisor through direct Program

This case requires `9007199254740993 div -3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: div-negative-divisor-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module integerprecision
constant $dividend be 9007199254740993
constant $divisor be -3
on Start(value, divisor) {
  emit Literal(value: (9007199254740993) div (-3))
  emit Constant(value: $dividend div $divisor)
  emit Runtime(value: value div divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "-3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "-3002399751580331" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "-3002399751580331" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "-3002399751580331" }
```

---

## Test: div negative-divisor through binary Program

This case requires `9007199254740993 div -3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: div-negative-divisor-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module integerprecision
constant $dividend be 9007199254740993
constant $divisor be -3
on Start(value, divisor) {
  emit Literal(value: (9007199254740993) div (-3))
  emit Constant(value: $dividend div $divisor)
  emit Runtime(value: value div divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "-3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "-3002399751580331" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "-3002399751580331" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "-3002399751580331" }
```

---

## Test: div int64-max through direct Program

This case requires `9223372036854775807 div 3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: div-int64-max-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module integerprecision
constant $dividend be 9223372036854775807
constant $divisor be 3
on Start(value, divisor) {
  emit Literal(value: (9223372036854775807) div (3))
  emit Constant(value: $dividend div $divisor)
  emit Runtime(value: value div divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "3074457345618258602" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "3074457345618258602" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "3074457345618258602" }
```

---

## Test: div int64-max through binary Program

This case requires `9223372036854775807 div 3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: div-int64-max-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module integerprecision
constant $dividend be 9223372036854775807
constant $divisor be 3
on Start(value, divisor) {
  emit Literal(value: (9223372036854775807) div (3))
  emit Constant(value: $dividend div $divisor)
  emit Runtime(value: value div divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "3074457345618258602" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "3074457345618258602" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "3074457345618258602" }
```

---

## Test: div int64-min-overflow through direct Program

This case requires `-9223372036854775808 div -1` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: div-int64-min-overflow-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module integerprecision
constant $dividend be -9223372036854775808
constant $divisor be -1
on Start(value, divisor) {
  emit Literal(value: (-9223372036854775808) div (-1))
  emit Constant(value: $dividend div $divisor)
  emit Runtime(value: value div divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "-1" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.binary64", value: "9.223372036854776e18" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.binary64", value: "9.223372036854776e18" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.binary64", value: "9.223372036854776e18" }
```

---

## Test: div int64-min-overflow through binary Program

This case requires `-9223372036854775808 div -1` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: div-int64-min-overflow-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module integerprecision
constant $dividend be -9223372036854775808
constant $divisor be -1
on Start(value, divisor) {
  emit Literal(value: (-9223372036854775808) div (-1))
  emit Constant(value: $dividend div $divisor)
  emit Runtime(value: value div divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "-1" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.binary64", value: "9.223372036854776e18" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.binary64", value: "9.223372036854776e18" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.binary64", value: "9.223372036854776e18" }
```

---

## Test: mod above53 through direct Program

This case requires `9007199254740993 mod 3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: mod-above53-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module integerprecision
constant $dividend be 9007199254740993
constant $divisor be 3
on Start(value, divisor) {
  emit Literal(value: (9007199254740993) mod (3))
  emit Constant(value: $dividend mod $divisor)
  emit Runtime(value: value mod divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
```

---

## Test: mod above53 through binary Program

This case requires `9007199254740993 mod 3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: mod-above53-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module integerprecision
constant $dividend be 9007199254740993
constant $divisor be 3
on Start(value, divisor) {
  emit Literal(value: (9007199254740993) mod (3))
  emit Constant(value: $dividend mod $divisor)
  emit Runtime(value: value mod divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
```

---

## Test: mod negative-above53 through direct Program

This case requires `-9007199254740993 mod 3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: mod-negative-above53-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module integerprecision
constant $dividend be -9007199254740993
constant $divisor be 3
on Start(value, divisor) {
  emit Literal(value: (-9007199254740993) mod (3))
  emit Constant(value: $dividend mod $divisor)
  emit Runtime(value: value mod divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
```

---

## Test: mod negative-above53 through binary Program

This case requires `-9007199254740993 mod 3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: mod-negative-above53-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module integerprecision
constant $dividend be -9007199254740993
constant $divisor be 3
on Start(value, divisor) {
  emit Literal(value: (-9007199254740993) mod (3))
  emit Constant(value: $dividend mod $divisor)
  emit Runtime(value: value mod divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
```

---

## Test: mod negative-divisor through direct Program

This case requires `9007199254740993 mod -3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: mod-negative-divisor-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module integerprecision
constant $dividend be 9007199254740993
constant $divisor be -3
on Start(value, divisor) {
  emit Literal(value: (9007199254740993) mod (-3))
  emit Constant(value: $dividend mod $divisor)
  emit Runtime(value: value mod divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "-3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
```

---

## Test: mod negative-divisor through binary Program

This case requires `9007199254740993 mod -3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: mod-negative-divisor-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module integerprecision
constant $dividend be 9007199254740993
constant $divisor be -3
on Start(value, divisor) {
  emit Literal(value: (9007199254740993) mod (-3))
  emit Constant(value: $dividend mod $divisor)
  emit Runtime(value: value mod divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "-3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
```

---

## Test: mod int64-max through direct Program

This case requires `9223372036854775807 mod 3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: mod-int64-max-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module integerprecision
constant $dividend be 9223372036854775807
constant $divisor be 3
on Start(value, divisor) {
  emit Literal(value: (9223372036854775807) mod (3))
  emit Constant(value: $dividend mod $divisor)
  emit Runtime(value: value mod divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "1" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "1" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "1" }
```

---

## Test: mod int64-max through binary Program

This case requires `9223372036854775807 mod 3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: mod-int64-max-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module integerprecision
constant $dividend be 9223372036854775807
constant $divisor be 3
on Start(value, divisor) {
  emit Literal(value: (9223372036854775807) mod (3))
  emit Constant(value: $dividend mod $divisor)
  emit Runtime(value: value mod divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "1" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "1" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "1" }
```

---

## Test: mod int64-min-overflow through direct Program

This case requires `-9223372036854775808 mod -1` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: mod-int64-min-overflow-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module integerprecision
constant $dividend be -9223372036854775808
constant $divisor be -1
on Start(value, divisor) {
  emit Literal(value: (-9223372036854775808) mod (-1))
  emit Constant(value: $dividend mod $divisor)
  emit Runtime(value: value mod divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "-1" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
```

---

## Test: mod int64-min-overflow through binary Program

This case requires `-9223372036854775808 mod -1` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: mod-int64-min-overflow-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module integerprecision
constant $dividend be -9223372036854775808
constant $divisor be -1
on Start(value, divisor) {
  emit Literal(value: (-9223372036854775808) mod (-1))
  emit Constant(value: $dividend mod $divisor)
  emit Runtime(value: value mod divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "-1" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
```

---

## Test: rem above53 through direct Program

This case requires `9007199254740993 rem 3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: rem-above53-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module integerprecision
constant $dividend be 9007199254740993
constant $divisor be 3
on Start(value, divisor) {
  emit Literal(value: (9007199254740993) rem (3))
  emit Constant(value: $dividend rem $divisor)
  emit Runtime(value: value rem divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
```

---

## Test: rem above53 through binary Program

This case requires `9007199254740993 rem 3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: rem-above53-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module integerprecision
constant $dividend be 9007199254740993
constant $divisor be 3
on Start(value, divisor) {
  emit Literal(value: (9007199254740993) rem (3))
  emit Constant(value: $dividend rem $divisor)
  emit Runtime(value: value rem divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
```

---

## Test: rem negative-above53 through direct Program

This case requires `-9007199254740993 rem 3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: rem-negative-above53-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module integerprecision
constant $dividend be -9007199254740993
constant $divisor be 3
on Start(value, divisor) {
  emit Literal(value: (-9007199254740993) rem (3))
  emit Constant(value: $dividend rem $divisor)
  emit Runtime(value: value rem divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
```

---

## Test: rem negative-above53 through binary Program

This case requires `-9007199254740993 rem 3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: rem-negative-above53-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module integerprecision
constant $dividend be -9007199254740993
constant $divisor be 3
on Start(value, divisor) {
  emit Literal(value: (-9007199254740993) rem (3))
  emit Constant(value: $dividend rem $divisor)
  emit Runtime(value: value rem divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
```

---

## Test: rem negative-divisor through direct Program

This case requires `9007199254740993 rem -3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: rem-negative-divisor-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module integerprecision
constant $dividend be 9007199254740993
constant $divisor be -3
on Start(value, divisor) {
  emit Literal(value: (9007199254740993) rem (-3))
  emit Constant(value: $dividend rem $divisor)
  emit Runtime(value: value rem divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "-3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
```

---

## Test: rem negative-divisor through binary Program

This case requires `9007199254740993 rem -3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: rem-negative-divisor-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module integerprecision
constant $dividend be 9007199254740993
constant $divisor be -3
on Start(value, divisor) {
  emit Literal(value: (9007199254740993) rem (-3))
  emit Constant(value: $dividend rem $divisor)
  emit Runtime(value: value rem divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "-3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
```

---

## Test: rem int64-max through direct Program

This case requires `9223372036854775807 rem 3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: rem-int64-max-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module integerprecision
constant $dividend be 9223372036854775807
constant $divisor be 3
on Start(value, divisor) {
  emit Literal(value: (9223372036854775807) rem (3))
  emit Constant(value: $dividend rem $divisor)
  emit Runtime(value: value rem divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "1" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "1" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "1" }
```

---

## Test: rem int64-max through binary Program

This case requires `9223372036854775807 rem 3` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: rem-int64-max-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module integerprecision
constant $dividend be 9223372036854775807
constant $divisor be 3
on Start(value, divisor) {
  emit Literal(value: (9223372036854775807) rem (3))
  emit Constant(value: $dividend rem $divisor)
  emit Runtime(value: value rem divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "3" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "1" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "1" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "1" }
```

---

## Test: rem int64-min-overflow through direct Program

This case requires `-9223372036854775808 rem -1` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: rem-int64-min-overflow-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module integerprecision
constant $dividend be -9223372036854775808
constant $divisor be -1
on Start(value, divisor) {
  emit Literal(value: (-9223372036854775808) rem (-1))
  emit Constant(value: $dividend rem $divisor)
  emit Runtime(value: value rem divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "-1" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
```

---

## Test: rem int64-min-overflow through binary Program

This case requires `-9223372036854775808 rem -1` to preserve exact integer arithmetic across all three evaluation paths.

### Case description

```yaml
gesBlock: case
id: rem-int64-min-overflow-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module integerprecision
constant $dividend be -9223372036854775808
constant $divisor be -1
on Start(value, divisor) {
  emit Literal(value: (-9223372036854775808) rem (-1))
  emit Constant(value: $dividend rem $divisor)
  emit Runtime(value: value rem divisor)
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
        - name: divisor
          value: { type: ":Number.int64", value: "-1" }
    local:
      - name: Literal
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Constant
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
      - name: Runtime
        args:
          - name: value
            value: { type: ":Number.int64", value: "0" }
```
