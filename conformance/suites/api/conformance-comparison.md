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

---

## Test: range-int64-is-not-binary64 with exact comparison

This case rejects a Binary64 range expectation for an Int64 range even when all three numeric fields are equal.

### Case description

```yaml
gesBlock: case
id: range-int64-is-not-binary64-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.int64", "from": "1", "to": "3", "step": "1"}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.binary64", "from": "1", "to": "3", "step": "1"}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: range-binary64-is-not-int64 with exact comparison

This case rejects an Int64 range expectation for a Binary64 range even when all three numeric fields are equal.

### Case description

```yaml
gesBlock: case
id: range-binary64-is-not-int64-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.binary64", "from": "1", "to": "3", "step": "1"}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.int64", "from": "1", "to": "3", "step": "1"}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: large-int64-range-from-differs with exact comparison

This case rejects distinct Int64 lower bounds above 2^53 that would become equal if rounded through Binary64.

### Case description

```yaml
gesBlock: case
id: large-int64-range-from-differs-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.int64", "from": "9007199254740992", "to": "9007199254740996", "step": "1"}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.int64", "from": "9007199254740993", "to": "9007199254740996", "step": "1"}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: large-int64-range-to-differs with exact comparison

This case rejects distinct Int64 upper bounds above 2^53 that would become equal if rounded through Binary64.

### Case description

```yaml
gesBlock: case
id: large-int64-range-to-differs-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.int64", "from": "9007199254740990", "to": "9007199254740992", "step": "1"}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.int64", "from": "9007199254740990", "to": "9007199254740993", "step": "1"}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: large-int64-range-step-differs with exact comparison

This case rejects distinct Int64 steps above 2^53 that would become equal if rounded through Binary64.

### Case description

```yaml
gesBlock: case
id: large-int64-range-step-differs-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.int64", "from": "0", "to": "9223372036854775807", "step": "9007199254740992"}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.int64", "from": "0", "to": "9223372036854775807", "step": "9007199254740993"}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: same-large-int64-range with exact comparison

This positive control accepts identical Int64 range fields whose exact values cannot all be represented in Binary64.

### Case description

```yaml
gesBlock: case
id: same-large-int64-range-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.int64", "from": "9007199254740993", "to": "9007199254740997", "step": "2"}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.int64", "from": "9007199254740993", "to": "9007199254740997", "step": "2"}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: true
```

---

## Test: nested-record-storage with exact comparison

This case rejects a Binary64 expectation for an Int64 record field, proving that record comparison preserves nested transport storage.

### Case description

```yaml
gesBlock: case
id: nested-record-storage-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: {"type": ":Sample", "entries": [{"key": "value", "value": {"type": ":Number.int64", "value": "1"}}]}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Sample", "entries": [{"key": "value", "value": {"type": ":Number.binary64", "value": "1"}}]}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: nested-message-infinite-unit with exact comparison

This case rejects different units on an infinite argument inside a nested message.

### Case description

```yaml
gesBlock: case
id: nested-message-infinite-unit-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: {"type": ":Message", "message": {"name": "Nested", "args": [{"name": "value", "value": {"type": ":Quantity.binary64", "value": "Infinity", "unit": "m"}}]}}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Message", "message": {"name": "Nested", "args": [{"name": "value", "value": {"type": ":Quantity.binary64", "value": "Infinity", "unit": "s"}}]}}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: scalar-nan-is-nothing with exact comparison

This positive control preserves the documented NaN spelling for invalid mathematics, which compares equal to nothing.

### Case description

```yaml
gesBlock: case
id: scalar-nan-is-nothing-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: {"type": ":Nothing"}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Number.binary64", "value": "NaN"}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: true
```

---

## Test: map-last-entry-wins with exact comparison

This positive control compares duplicate map keys using only their last entry; an overwritten value must not affect comparison.

### Case description

```yaml
gesBlock: case
id: map-last-entry-wins-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: {"type": ":Map", "entries": [{"key": "value", "value": {"type": ":Number.int64", "value": "1"}}]}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Map", "entries": [{"key": "value", "value": {"type": ":Number.binary64", "value": "1"}}, {"key": "value", "value": {"type": ":Number.int64", "value": "1"}}]}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: true
```

---

## Test: binary64-tolerance with exact comparison

This case compares finite Binary64 values one ULP apart: exact mode rejects the difference and ULP mode accepts it.

### Case description

```yaml
gesBlock: case
id: binary64-tolerance-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: {"type": ":Number.binary64", "value": "1.5"}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Number.binary64", "value": "1.5000000000000002"}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: range-binary64-tolerance with exact comparison

This case compares Binary64 range lower bounds one ULP apart: exact mode rejects the difference and ULP mode accepts it.

### Case description

```yaml
gesBlock: case
id: range-binary64-tolerance-exact
comparison:
  binary64:
    mode: exact
messageApi:
  signature: { name: Done, parameters: [value] }
  message:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.binary64", "from": "1.5", "to": "2.5", "step": "0.5"}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.binary64", "from": "1.5000000000000002", "to": "2.5", "step": "0.5"}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: range-int64-is-not-binary64 with ulp comparison

This case rejects a Binary64 range expectation for an Int64 range even when all three numeric fields are equal.

### Case description

```yaml
gesBlock: case
id: range-int64-is-not-binary64-ulp
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
        value: {"type": ":Range.int64", "from": "1", "to": "3", "step": "1"}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.binary64", "from": "1", "to": "3", "step": "1"}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: range-binary64-is-not-int64 with ulp comparison

This case rejects an Int64 range expectation for a Binary64 range even when all three numeric fields are equal.

### Case description

```yaml
gesBlock: case
id: range-binary64-is-not-int64-ulp
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
        value: {"type": ":Range.binary64", "from": "1", "to": "3", "step": "1"}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.int64", "from": "1", "to": "3", "step": "1"}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: large-int64-range-from-differs with ulp comparison

This case rejects distinct Int64 lower bounds above 2^53 that would become equal if rounded through Binary64.

### Case description

```yaml
gesBlock: case
id: large-int64-range-from-differs-ulp
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
        value: {"type": ":Range.int64", "from": "9007199254740992", "to": "9007199254740996", "step": "1"}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.int64", "from": "9007199254740993", "to": "9007199254740996", "step": "1"}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: large-int64-range-to-differs with ulp comparison

This case rejects distinct Int64 upper bounds above 2^53 that would become equal if rounded through Binary64.

### Case description

```yaml
gesBlock: case
id: large-int64-range-to-differs-ulp
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
        value: {"type": ":Range.int64", "from": "9007199254740990", "to": "9007199254740992", "step": "1"}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.int64", "from": "9007199254740990", "to": "9007199254740993", "step": "1"}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: large-int64-range-step-differs with ulp comparison

This case rejects distinct Int64 steps above 2^53 that would become equal if rounded through Binary64.

### Case description

```yaml
gesBlock: case
id: large-int64-range-step-differs-ulp
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
        value: {"type": ":Range.int64", "from": "0", "to": "9223372036854775807", "step": "9007199254740992"}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.int64", "from": "0", "to": "9223372036854775807", "step": "9007199254740993"}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: same-large-int64-range with ulp comparison

This positive control accepts identical Int64 range fields whose exact values cannot all be represented in Binary64.

### Case description

```yaml
gesBlock: case
id: same-large-int64-range-ulp
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
        value: {"type": ":Range.int64", "from": "9007199254740993", "to": "9007199254740997", "step": "2"}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.int64", "from": "9007199254740993", "to": "9007199254740997", "step": "2"}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: true
```

---

## Test: nested-record-storage with ulp comparison

This case rejects a Binary64 expectation for an Int64 record field, proving that record comparison preserves nested transport storage.

### Case description

```yaml
gesBlock: case
id: nested-record-storage-ulp
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
        value: {"type": ":Sample", "entries": [{"key": "value", "value": {"type": ":Number.int64", "value": "1"}}]}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Sample", "entries": [{"key": "value", "value": {"type": ":Number.binary64", "value": "1"}}]}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: nested-message-infinite-unit with ulp comparison

This case rejects different units on an infinite argument inside a nested message.

### Case description

```yaml
gesBlock: case
id: nested-message-infinite-unit-ulp
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
        value: {"type": ":Message", "message": {"name": "Nested", "args": [{"name": "value", "value": {"type": ":Quantity.binary64", "value": "Infinity", "unit": "m"}}]}}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Message", "message": {"name": "Nested", "args": [{"name": "value", "value": {"type": ":Quantity.binary64", "value": "Infinity", "unit": "s"}}]}}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: false
```

---

## Test: scalar-nan-is-nothing with ulp comparison

This positive control preserves the documented NaN spelling for invalid mathematics, which compares equal to nothing.

### Case description

```yaml
gesBlock: case
id: scalar-nan-is-nothing-ulp
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
        value: {"type": ":Nothing"}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Number.binary64", "value": "NaN"}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: true
```

---

## Test: map-last-entry-wins with ulp comparison

This positive control compares duplicate map keys using only their last entry; an overwritten value must not affect comparison.

### Case description

```yaml
gesBlock: case
id: map-last-entry-wins-ulp
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
        value: {"type": ":Map", "entries": [{"key": "value", "value": {"type": ":Number.int64", "value": "1"}}]}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Map", "entries": [{"key": "value", "value": {"type": ":Number.binary64", "value": "1"}}, {"key": "value", "value": {"type": ":Number.int64", "value": "1"}}]}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: true
```

---

## Test: binary64-tolerance with ulp comparison

This case compares finite Binary64 values one ULP apart: exact mode rejects the difference and ULP mode accepts it.

### Case description

```yaml
gesBlock: case
id: binary64-tolerance-ulp
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
        value: {"type": ":Number.binary64", "value": "1.5"}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Number.binary64", "value": "1.5000000000000002"}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: true
```

---

## Test: range-binary64-tolerance with ulp comparison

This case compares Binary64 range lower bounds one ULP apart: exact mode rejects the difference and ULP mode accepts it.

### Case description

```yaml
gesBlock: case
id: range-binary64-tolerance-ulp
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
        value: {"type": ":Range.binary64", "from": "1.5", "to": "2.5", "step": "0.5"}
  compareConformanceMessage:
    name: Done
    args:
      - name: value
        value: {"type": ":Range.binary64", "from": "1.5000000000000002", "to": "2.5", "step": "0.5"}
```

### Expectation

```yaml
gesBlock: expect
message:
  conformanceEquals: true
```
