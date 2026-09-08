---
formatVersion: 1
suiteId: api.conformance-comparison
title: Conformance message comparison counterexamples
kind: messageApi
level: atomic
categories: [conformance]
---

# Conformance message comparison counterexamples

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite tests the runner comparison contract in `specs/Conformance/MarkdownFormat.md`, using the same comparison operation as runtime message expectations. Negative cases must reject incorrect storage variants and units even when public value factories normalize inputs. Positive controls ensure the operation does not simply reject every message.

---

## Test: same-int64 with exact comparison

This case expects the runner comparison to accept matching values.

### Case description

```yaml
gesBlock: case
id: same-int64-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Number.int64", value: "1" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Number.int64", value: "1" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: true
```

---

## Test: different-int64 with exact comparison

This case expects the runner comparison to reject this transport expectation; numeric tolerance cannot change storage kinds or units.

### Case description

```yaml
gesBlock: case
id: different-int64-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Number.int64", value: "1" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Number.int64", value: "2" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: binary64-is-not-int64 with exact comparison

This case expects the runner comparison to reject this transport expectation; numeric tolerance cannot change storage kinds or units.

### Case description

```yaml
gesBlock: case
id: binary64-is-not-int64-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Number.int64", value: "1" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Number.binary64", value: "1" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: quantity-binary64-is-not-int64 with exact comparison

This case expects the runner comparison to reject this transport expectation; numeric tolerance cannot change storage kinds or units.

### Case description

```yaml
gesBlock: case
id: quantity-binary64-is-not-int64-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.int64", value: "1", unit: "m" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "1", unit: "m" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: same-fraction with exact comparison

This case expects the runner comparison to accept matching values.

### Case description

```yaml
gesBlock: case
id: same-fraction-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Number.binary64", value: "1.5" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Number.binary64", value: "1.5" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: true
```

---

## Test: same-infinite-unit with exact comparison

This case expects the runner comparison to accept matching values.

### Case description

```yaml
gesBlock: case
id: same-infinite-unit-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "Infinity", unit: "m" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "Infinity", unit: "m" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: true
```

---

## Test: infinite-units-differ with exact comparison

This case expects the runner comparison to reject this transport expectation; numeric tolerance cannot change storage kinds or units.

### Case description

```yaml
gesBlock: case
id: infinite-units-differ-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "Infinity", unit: "m" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "Infinity", unit: "s" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: negative-infinite-units-differ with exact comparison

This case expects the runner comparison to reject this transport expectation; numeric tolerance cannot change storage kinds or units.

### Case description

```yaml
gesBlock: case
id: negative-infinite-units-differ-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "-Infinity", unit: "m" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "-Infinity", unit: "s" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: infinite-unit-is-not-scalar with exact comparison

This case expects the runner comparison to reject this transport expectation; numeric tolerance cannot change storage kinds or units.

### Case description

```yaml
gesBlock: case
id: infinite-unit-is-not-scalar-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "Infinity", unit: "m" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Number.binary64", value: "Infinity" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: infinity-signs-differ with exact comparison

This case expects the runner comparison to reject this transport expectation; numeric tolerance cannot change storage kinds or units.

### Case description

```yaml
gesBlock: case
id: infinity-signs-differ-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "Infinity", unit: "m" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "-Infinity", unit: "m" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: nested-list-storage with exact comparison

This case expects the runner comparison to reject this transport expectation; numeric tolerance cannot change storage kinds or units.

### Case description

```yaml
gesBlock: case
id: nested-list-storage-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":List", items: [{ type: ":Number.int64", value: "1" }] }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":List", items: [{ type: ":Number.binary64", value: "1" }] }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: nested-map-infinite-unit with exact comparison

This case expects the runner comparison to reject this transport expectation; numeric tolerance cannot change storage kinds or units.

### Case description

```yaml
gesBlock: case
id: nested-map-infinite-unit-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Map", entries: [{ key: nested, value: { type: ":Quantity.binary64", value: "Infinity", unit: "m" } }] }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Map", entries: [{ key: nested, value: { type: ":Quantity.binary64", value: "Infinity", unit: "s" } }] }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: same-int64 with ulp comparison

This case expects the runner comparison to accept matching values.

### Case description

```yaml
gesBlock: case
id: same-int64-ulp
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Number.int64", value: "1" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Number.int64", value: "1" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: true
```

---

## Test: different-int64 with ulp comparison

This case expects the runner comparison to reject this transport expectation; numeric tolerance cannot change storage kinds or units.

### Case description

```yaml
gesBlock: case
id: different-int64-ulp
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Number.int64", value: "1" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Number.int64", value: "2" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: binary64-is-not-int64 with ulp comparison

This case expects the runner comparison to reject this transport expectation; numeric tolerance cannot change storage kinds or units.

### Case description

```yaml
gesBlock: case
id: binary64-is-not-int64-ulp
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Number.int64", value: "1" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Number.binary64", value: "1" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: quantity-binary64-is-not-int64 with ulp comparison

This case expects the runner comparison to reject this transport expectation; numeric tolerance cannot change storage kinds or units.

### Case description

```yaml
gesBlock: case
id: quantity-binary64-is-not-int64-ulp
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.int64", value: "1", unit: "m" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "1", unit: "m" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: same-fraction with ulp comparison

This case expects the runner comparison to accept matching values.

### Case description

```yaml
gesBlock: case
id: same-fraction-ulp
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Number.binary64", value: "1.5" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Number.binary64", value: "1.5" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: true
```

---

## Test: same-infinite-unit with ulp comparison

This case expects the runner comparison to accept matching values.

### Case description

```yaml
gesBlock: case
id: same-infinite-unit-ulp
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "Infinity", unit: "m" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "Infinity", unit: "m" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: true
```

---

## Test: infinite-units-differ with ulp comparison

This case expects the runner comparison to reject this transport expectation; numeric tolerance cannot change storage kinds or units.

### Case description

```yaml
gesBlock: case
id: infinite-units-differ-ulp
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "Infinity", unit: "m" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "Infinity", unit: "s" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: negative-infinite-units-differ with ulp comparison

This case expects the runner comparison to reject this transport expectation; numeric tolerance cannot change storage kinds or units.

### Case description

```yaml
gesBlock: case
id: negative-infinite-units-differ-ulp
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "-Infinity", unit: "m" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "-Infinity", unit: "s" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: infinite-unit-is-not-scalar with ulp comparison

This case expects the runner comparison to reject this transport expectation; numeric tolerance cannot change storage kinds or units.

### Case description

```yaml
gesBlock: case
id: infinite-unit-is-not-scalar-ulp
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "Infinity", unit: "m" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Number.binary64", value: "Infinity" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: infinity-signs-differ with ulp comparison

This case expects the runner comparison to reject this transport expectation; numeric tolerance cannot change storage kinds or units.

### Case description

```yaml
gesBlock: case
id: infinity-signs-differ-ulp
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "Infinity", unit: "m" }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Quantity.binary64", value: "-Infinity", unit: "m" }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: nested-list-storage with ulp comparison

This case expects the runner comparison to reject this transport expectation; numeric tolerance cannot change storage kinds or units.

### Case description

```yaml
gesBlock: case
id: nested-list-storage-ulp
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":List", items: [{ type: ":Number.int64", value: "1" }] }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":List", items: [{ type: ":Number.binary64", value: "1" }] }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: nested-map-infinite-unit with ulp comparison

This case expects the runner comparison to reject this transport expectation; numeric tolerance cannot change storage kinds or units.

### Case description

```yaml
gesBlock: case
id: nested-map-infinite-unit-ulp
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: { type: ":Map", entries: [{ key: nested, value: { type: ":Quantity.binary64", value: "Infinity", unit: "m" } }] }
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: { type: ":Map", entries: [{ key: nested, value: { type: ":Quantity.binary64", value: "Infinity", unit: "s" } }] }
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```
