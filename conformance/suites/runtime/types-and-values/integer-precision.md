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

---

## Test: quantity-negative-floor through direct Program

This case requires floor division with a nonzero remainder above 2^53 to cancel matching units, while modulo and remainder retain the dividend unit and their distinct sign rules.

### Case description

```yaml
gesBlock: case
id: quantity-negative-floor-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module integerprecision
constant $dividend be -9007199254740994m
constant $divisor be 3m
on Start(value, divisor) {
  emit Literal(quotient: (-9007199254740994m) div (3m), modulo: (-9007199254740994m) mod (3m), remainder: (-9007199254740994m) rem (3m))
  emit Constant(quotient: $dividend div $divisor, modulo: $dividend mod $divisor, remainder: $dividend rem $divisor)
  emit Runtime(quotient: value div divisor, modulo: value mod divisor, remainder: value rem divisor)
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
          value: { type: ":Quantity.int64", value: "-9007199254740994", unit: ":meter" }
        - name: divisor
          value: { type: ":Quantity.int64", value: "3", unit: ":meter" }
    local:
      - name: Literal
        args:
          - name: quotient
            value: { type: ":Number.int64", value: "-3002399751580332" }
          - name: modulo
            value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
          - name: remainder
            value: { type: ":Quantity.int64", value: "-1", unit: ":meter" }
      - name: Constant
        args:
          - name: quotient
            value: { type: ":Number.int64", value: "-3002399751580332" }
          - name: modulo
            value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
          - name: remainder
            value: { type: ":Quantity.int64", value: "-1", unit: ":meter" }
      - name: Runtime
        args:
          - name: quotient
            value: { type: ":Number.int64", value: "-3002399751580332" }
          - name: modulo
            value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
          - name: remainder
            value: { type: ":Quantity.int64", value: "-1", unit: ":meter" }
```

---

## Test: quantity-negative-floor through binary Program

This case requires floor division with a nonzero remainder above 2^53 to cancel matching units, while modulo and remainder retain the dividend unit and their distinct sign rules.

### Case description

```yaml
gesBlock: case
id: quantity-negative-floor-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module integerprecision
constant $dividend be -9007199254740994m
constant $divisor be 3m
on Start(value, divisor) {
  emit Literal(quotient: (-9007199254740994m) div (3m), modulo: (-9007199254740994m) mod (3m), remainder: (-9007199254740994m) rem (3m))
  emit Constant(quotient: $dividend div $divisor, modulo: $dividend mod $divisor, remainder: $dividend rem $divisor)
  emit Runtime(quotient: value div divisor, modulo: value mod divisor, remainder: value rem divisor)
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
          value: { type: ":Quantity.int64", value: "-9007199254740994", unit: ":meter" }
        - name: divisor
          value: { type: ":Quantity.int64", value: "3", unit: ":meter" }
    local:
      - name: Literal
        args:
          - name: quotient
            value: { type: ":Number.int64", value: "-3002399751580332" }
          - name: modulo
            value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
          - name: remainder
            value: { type: ":Quantity.int64", value: "-1", unit: ":meter" }
      - name: Constant
        args:
          - name: quotient
            value: { type: ":Number.int64", value: "-3002399751580332" }
          - name: modulo
            value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
          - name: remainder
            value: { type: ":Quantity.int64", value: "-1", unit: ":meter" }
      - name: Runtime
        args:
          - name: quotient
            value: { type: ":Number.int64", value: "-3002399751580332" }
          - name: modulo
            value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
          - name: remainder
            value: { type: ":Quantity.int64", value: "-1", unit: ":meter" }
```

---

## Test: zero-divisor through direct Program

This case requires a zero divisor to produce negative infinity for div and nothing for mod/rem in every evaluation path, without an implementation arithmetic exception.

### Case description

```yaml
gesBlock: case
id: zero-divisor-direct
compile:
  binaryRoundTrip: false
```

### Source code under test

```ges
module integerprecision
constant $dividend be -9223372036854775808
constant $divisor be 0
on Start(value, divisor) {
  emit Literal(quotient: (-9223372036854775808) div (0), modulo: (-9223372036854775808) mod (0), remainder: (-9223372036854775808) rem (0))
  emit Constant(quotient: $dividend div $divisor, modulo: $dividend mod $divisor, remainder: $dividend rem $divisor)
  emit Runtime(quotient: value div divisor, modulo: value mod divisor, remainder: value rem divisor)
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
          value: { type: ":Number.int64", value: "0" }
    local:
      - name: Literal
        args:
          - name: quotient
            value: { type: ":Number.binary64", value: "-Infinity" }
          - name: modulo
            value: { type: ":Nothing" }
          - name: remainder
            value: { type: ":Nothing" }
      - name: Constant
        args:
          - name: quotient
            value: { type: ":Number.binary64", value: "-Infinity" }
          - name: modulo
            value: { type: ":Nothing" }
          - name: remainder
            value: { type: ":Nothing" }
      - name: Runtime
        args:
          - name: quotient
            value: { type: ":Number.binary64", value: "-Infinity" }
          - name: modulo
            value: { type: ":Nothing" }
          - name: remainder
            value: { type: ":Nothing" }
```

---

## Test: zero-divisor through binary Program

This case requires a zero divisor to produce negative infinity for div and nothing for mod/rem in every evaluation path, without an implementation arithmetic exception.

### Case description

```yaml
gesBlock: case
id: zero-divisor-binary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
module integerprecision
constant $dividend be -9223372036854775808
constant $divisor be 0
on Start(value, divisor) {
  emit Literal(quotient: (-9223372036854775808) div (0), modulo: (-9223372036854775808) mod (0), remainder: (-9223372036854775808) rem (0))
  emit Constant(quotient: $dividend div $divisor, modulo: $dividend mod $divisor, remainder: $dividend rem $divisor)
  emit Runtime(quotient: value div divisor, modulo: value mod divisor, remainder: value rem divisor)
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
          value: { type: ":Number.int64", value: "0" }
    local:
      - name: Literal
        args:
          - name: quotient
            value: { type: ":Number.binary64", value: "-Infinity" }
          - name: modulo
            value: { type: ":Nothing" }
          - name: remainder
            value: { type: ":Nothing" }
      - name: Constant
        args:
          - name: quotient
            value: { type: ":Number.binary64", value: "-Infinity" }
          - name: modulo
            value: { type: ":Nothing" }
          - name: remainder
            value: { type: ":Nothing" }
      - name: Runtime
        args:
          - name: quotient
            value: { type: ":Number.binary64", value: "-Infinity" }
          - name: modulo
            value: { type: ":Nothing" }
          - name: remainder
            value: { type: ":Nothing" }
```
