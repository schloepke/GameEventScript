---
formatVersion: 1
suiteId: "api.values"
title: "Portable Value API"
categories: [conformance]
tags: [portable-api]
---

# Portable Value API

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies the language-neutral value model, including kinds, units, ordering, equality, normalization, and defensive copies.

---

## Test: primitive values expose their canonical representation

This public API case exercises “primitive values expose their canonical representation” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: primitives
kind: valueApi
level: atomic
valueApi:
  value: { type: ":Number.int64", value: "42" }
  equalTo: { type: ":Number.int64", value: "42" }
  notEqualTo: { type: ":Number.int64", value: "43" }
```

### Expectation

```yaml
gesBlock: expect
value:
  normalized: { type: ":Number.int64", value: "42" }
  isNumeric: true
  hasValue: true
  isNothing: false
  hasUnit: false
  asBoolean: true
  equal: true
  equalHash: true
  notEqual: true
```

---

## Test: measured floats retain Binary64 value and unit

This public API case exercises “measured floats retain Binary64 value and unit” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: measured-float
kind: valueApi
level: atomic
valueApi:
  value: { type: ":Quantity.binary64", value: "12.5", unit: ":meter" }
```

### Expectation

```yaml
gesBlock: expect
value:
  normalized: { type: ":Quantity.binary64", value: "12.5", unit: ":meter" }
  isNumeric: true
  hasValue: true
  hasUnit: true
  asBoolean: true
```

---

## Test: percentages retain their distinct kind

This public API case exercises “percentages retain their distinct kind” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: percentage
kind: valueApi
level: atomic
valueApi:
  value: { type: ":Percentage", value: "0.25" }
```

### Expectation

```yaml
gesBlock: expect
value:
  normalized: { type: ":Percentage", value: "0.25" }
  isNumeric: true
  hasValue: true
  hasUnit: false
```

---

## Test: vectors expose components and units

This public API case exercises “vectors expose components and units” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: vector
kind: valueApi
level: atomic
valueApi:
  value: { type: ":Vector", x: "3", y: "4", z: "5", unit: ":meter" }
```

### Expectation

```yaml
gesBlock: expect
value:
  normalized: { type: ":Vector", x: "3", y: "4", z: "5", unit: ":meter" }
  hasValue: true
  hasUnit: true
  asBoolean: true
```

---

## Test: points expose components and units

This public API case exercises “points expose components and units” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: point
kind: valueApi
level: atomic
valueApi:
  value: { type: ":Point", x: "1", y: "2", z: "3", unit: ":second" }
```

### Expectation

```yaml
gesBlock: expect
value:
  normalized: { type: ":Point", x: "1", y: "2", z: "3", unit: ":second" }
  hasValue: true
  hasUnit: true
  asBoolean: true
```

---

## Test: text retains Unicode scalar content

This public API case exercises “text retains Unicode scalar content” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: text
kind: valueApi
level: atomic
valueApi:
  value: { type: ":Text", value: "hello 😀" }
```

### Expectation

```yaml
gesBlock: expect
value:
  normalized: { type: ":Text", value: "hello 😀" }
  isNumeric: false
  hasValue: true
  isNothing: false
```

---

## Test: tag equality compares canonical names

This public API case verifies equality and inequality between valid canonical tag names.

### Case description

```yaml
gesBlock: case
id: tag-case-sensitive
kind: valueApi
level: atomic
valueApi:
  value: { type: ":Tag", value: "ready" }
  equalTo: { type: ":Tag", value: "ready" }
  notEqualTo: { type: ":Tag", value: "waiting" }
```

### Expectation

```yaml
gesBlock: expect
value:
  normalized: { type: ":Tag", value: "ready" }
  isNumeric: false
  hasValue: true
  equal: true
  equalHash: true
  notEqual: true
```

---

## Test: NaN canonicalizes to nothing

This public API case exercises “NaN canonicalizes to nothing” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: nan-is-nothing
kind: valueApi
level: atomic
valueApi:
  value: { type: ":Number.binary64", value: "NaN" }
```

### Expectation

```yaml
gesBlock: expect
value:
  normalized: { type: ":Nothing" }
  isNumeric: false
  hasValue: false
  isNothing: true
  hasUnit: false
  asBoolean: false
```

---

## Test: lists defensively copy their source items

This public API case exercises “lists defensively copy their source items” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: list-defensive-copy
kind: valueApi
level: atomic
valueApi:
  mutateSourceAfterCreate: true
  value:
    type: ":List"
    items:
      - { type: ":Number.int64", value: "1" }
      - { type: ":Text", value: "two" }
```

### Expectation

```yaml
gesBlock: expect
value:
  normalized:
    type: ":List"
    items:
      - { type: ":Number.int64", value: "1" }
      - { type: ":Text", value: "two" }
  length: 2
  hasValue: true
```

---

## Test: dice defensively copy and normalize rolls

This public API case exercises “dice defensively copy and normalize rolls” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: dice-defensive-copy
kind: valueApi
level: atomic
valueApi:
  mutateSourceAfterCreate: true
  value: { type: ":Dice", rolls: [3, 6, 1] }
```

### Expectation

```yaml
gesBlock: expect
value:
  normalized: { type: ":Dice", rolls: [6, 3, 1] }
  length: 3
  hasValue: true
```

---

## Test: maps use scalar ordering duplicate last wins and source arrays are copied

This public API case exercises “maps use scalar ordering duplicate last wins and source arrays are copied” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: map-ordering-and-copy
kind: valueApi
level: atomic
valueApi:
  mutateSourceAfterCreate: true
  value:
    type: ":Map"
    entries:
      - key: "𐀀"
        value: { type: ":Number.int64", value: "4" }
      - key: same
        value: { type: ":Number.int64", value: "1" }
      - key: ""
        value: { type: ":Number.int64", value: "3" }
      - key: same
        value: { type: ":Number.int64", value: "2" }
```

### Expectation

```yaml
gesBlock: expect
value:
  normalized:
    type: ":Map"
    entries:
      - key: same
        value: { type: ":Number.int64", value: "2" }
      - key: ""
        value: { type: ":Number.int64", value: "3" }
      - key: "𐀀"
        value: { type: ":Number.int64", value: "4" }
  length: 3
  hasValue: true
```

---

## Test: records expose type and sorted fields

This public API case exercises “records expose type and sorted fields” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: record
kind: valueApi
level: atomic
valueApi:
  mutateSourceAfterCreate: true
  value:
    type: ":Unit"
    entries:
      - key: hp
        value: { type: ":Number.int64", value: "10" }
      - key: _hidden
        value: { type: ":Text", value: secret }
```

### Expectation

```yaml
gesBlock: expect
value:
  normalized:
    type: ":Unit"
    entries:
      - key: _hidden
        value: { type: ":Text", value: secret }
      - key: hp
        value: { type: ":Number.int64", value: "10" }
  length: 2
  customTypeName: Unit
  hasValue: true
```

---

## Test: integer ranges preserve integer storage

This public API case exercises “integer ranges preserve integer storage” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: integer-range
kind: valueApi
level: atomic
valueApi:
  value: { type: ":Range.int64", from: "1", to: "5", step: "2" }
```

### Expectation

```yaml
gesBlock: expect
value:
  normalized: { type: ":Range.int64", from: "1", to: "5", step: "2" }
  hasValue: true
```

---

## Test: float ranges preserve Binary64 storage

This public API case exercises “float ranges preserve Binary64 storage” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: float-range
kind: valueApi
level: atomic
valueApi:
  value: { type: ":Range.binary64", from: "1.5", to: "2.5", step: "0.5" }
```

### Expectation

```yaml
gesBlock: expect
value:
  normalized: { type: ":Range.binary64", from: "1.5", to: "2.5", step: "0.5" }
  hasValue: true
```

---

## Test: message values retain message equality

This public API case exercises “message values retain message equality” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: message-value
kind: valueApi
level: atomic
valueApi:
  value:
    type: ":Message"
    message:
      name: Ping
      args:
        - name: amount
          value: { type: ":Number.int64", value: "7" }
  equalTo:
    type: ":Message"
    message:
      name: Ping
      args:
        - name: amount
          value: { type: ":Number.int64", value: "7" }
```

### Expectation

```yaml
gesBlock: expect
value:
  normalized:
    type: ":Message"
    message:
      name: Ping
      args:
        - name: amount
          value: { type: ":Number.int64", value: "7" }
  equal: true
  equalHash: true
  hasValue: true
```

---

## Test: lenient literal parsing recognizes a quantity

This public API case parses the literal input with `ParseLiteral` and verifies the recognized value, the strict rejection, or the reported limit.

### Case description

```yaml
gesBlock: case
id: literal-lenient-quantity
kind: valueApi
level: atomic
valueApi:
  literal:
    text: "150kW"
    mode: lenient
```

### Expectation

```yaml
gesBlock: expect
literal:
  value: { type: ":Quantity.int64", value: "150000", unit: ":watt" }
```

---

## Test: lenient literal parsing keeps a bare word as Text

This public API case parses the literal input with `ParseLiteral` and verifies the recognized value, the strict rejection, or the reported limit.

### Case description

```yaml
gesBlock: case
id: literal-lenient-bare-word
kind: valueApi
level: atomic
valueApi:
  literal:
    text: "standard"
    mode: lenient
```

### Expectation

```yaml
gesBlock: expect
literal:
  value: { type: ":Text", value: "standard" }
```

---

## Test: lenient literal parsing keeps malformed syntax as unchanged Text

This public API case parses the literal input with `ParseLiteral` and verifies the recognized value, the strict rejection, or the reported limit.

### Case description

```yaml
gesBlock: case
id: literal-lenient-malformed
kind: valueApi
level: atomic
valueApi:
  literal:
    text: "[1, 2"
    mode: lenient
```

### Expectation

```yaml
gesBlock: expect
literal:
  value: { type: ":Text", value: "[1, 2" }
```

---

## Test: lenient literal parsing keeps a Program Record constructor as Text

This public API case parses the literal input with `ParseLiteral` and verifies the recognized value, the strict rejection, or the reported limit.

### Case description

```yaml
gesBlock: case
id: literal-lenient-program-record
kind: valueApi
level: atomic
valueApi:
  literal:
    text: ":Hit(5)"
    mode: lenient
```

### Expectation

```yaml
gesBlock: expect
literal:
  value: { type: ":Text", value: ":Hit(5)" }
```

---

## Test: strict literal parsing recognizes a quantity

This public API case parses the literal input with `ParseLiteralStrict` and verifies the recognized value, the strict rejection, or the reported limit.

### Case description

```yaml
gesBlock: case
id: literal-strict-quantity
kind: valueApi
level: atomic
valueApi:
  literal:
    text: "150kW"
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  value: { type: ":Quantity.int64", value: "150000", unit: ":watt" }
```

---

## Test: strict literal parsing rejects an unquoted word

This public API case parses the literal input with `ParseLiteralStrict` and verifies the recognized value, the strict rejection, or the reported limit.

### Case description

```yaml
gesBlock: case
id: literal-strict-bare-word
kind: valueApi
level: atomic
valueApi:
  literal:
    text: "standard"
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  unrecognized: true
```

---

## Test: strict literal parsing decodes quoted Text

This public API case parses the literal input with `ParseLiteralStrict` and verifies the recognized value, the strict rejection, or the reported limit.

### Case description

```yaml
gesBlock: case
id: literal-strict-quoted-text
kind: valueApi
level: atomic
valueApi:
  literal:
    text: "'standard'"
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  value: { type: ":Text", value: "standard" }
```

---

## Test: strict literal parsing recognizes a Tag

This public API case parses the literal input with `ParseLiteralStrict` and verifies the recognized value, the strict rejection, or the reported limit.

### Case description

```yaml
gesBlock: case
id: literal-strict-tag
kind: valueApi
level: atomic
valueApi:
  literal:
    text: "#standard"
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  value: { type: ":Tag", value: "standard" }
```

---

## Test: strict literal parsing rejects malformed syntax

This public API case parses the literal input with `ParseLiteralStrict` and verifies the recognized value, the strict rejection, or the reported limit.

### Case description

```yaml
gesBlock: case
id: literal-strict-malformed
kind: valueApi
level: atomic
valueApi:
  literal:
    text: "[1, 2"
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  unrecognized: true
```

---

## Test: strict literal parsing rejects empty input

This public API case parses the literal input with `ParseLiteralStrict` and verifies the recognized value, the strict rejection, or the reported limit.

### Case description

```yaml
gesBlock: case
id: literal-strict-empty
kind: valueApi
level: atomic
valueApi:
  literal:
    text: ""
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  unrecognized: true
```

---

## Test: strict literal parsing recognizes the nothing literal

This public API case parses the literal input with `ParseLiteralStrict` and verifies the recognized value, the strict rejection, or the reported limit.

### Case description

```yaml
gesBlock: case
id: literal-strict-nothing
kind: valueApi
level: atomic
valueApi:
  literal:
    text: "nothing"
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  value: { type: ":Nothing" }
```

---

## Test: strict literal parsing accepts whitespace around a literal

This public API case parses the literal input with `ParseLiteralStrict` and verifies the recognized value, the strict rejection, or the reported limit.

### Case description

```yaml
gesBlock: case
id: literal-strict-surrounding-whitespace
kind: valueApi
level: atomic
valueApi:
  literal:
    text: "  [1]\n"
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  value: { type: ":List", items: [{ type: ":Number.int64", value: "1" }] }
```

---

## Test: strict literal parsing recognizes a Map

This public API case parses the literal input with `ParseLiteralStrict` and verifies the recognized value, the strict rejection, or the reported limit.

### Case description

```yaml
gesBlock: case
id: literal-strict-map
kind: valueApi
level: atomic
valueApi:
  literal:
    text: "[energy: 150kW, mode: #standard]"
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  value:
    type: ":Map"
    entries:
      - key: energy
        value: { type: ":Quantity.int64", value: "150000", unit: ":watt" }
      - key: mode
        value: { type: ":Tag", value: "standard" }
```

---

## Test: strict literal parsing reconstructs Record data

This public API case parses the literal input with `ParseLiteralStrict` and verifies the recognized value, the strict rejection, or the reported limit.

### Case description

```yaml
gesBlock: case
id: literal-strict-record-data
kind: valueApi
level: atomic
valueApi:
  literal:
    text: ":Record(\"Hit\", [damage: 5])"
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  value:
    type: ":Hit"
    entries:
      - key: damage
        value: { type: ":Number.int64", value: "5" }
```

---

## Test: strict literal parsing rejects a Program Record constructor without a Program

This public API case parses the literal input with `ParseLiteralStrict` and verifies the recognized value, the strict rejection, or the reported limit.

### Case description

```yaml
gesBlock: case
id: literal-strict-program-record
kind: valueApi
level: atomic
valueApi:
  literal:
    text: ":Hit(5)"
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  unrecognized: true
```

---

## Test: literal parsing outside a host reports the depth limit

This public API case parses the literal input with `ParseLiteral` and verifies the recognized value, the strict rejection, or the reported limit.

### Case description

```yaml
gesBlock: case
id: literal-depth-limit
kind: valueApi
level: atomic
valueApi:
  literal:
    text: "[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]"
    mode: lenient
```

### Expectation

```yaml
gesBlock: expect
literal:
  limit: { name: "MaxLiteralDepth", limit: 64 }
```

---

## Test: literal parsing outside a host reports the item limit

This public API case parses a List of 65,537 items with `ParseLiteral`; the item past MaxLiteralItems reports the limit instead of returning Text.

### Case description

```yaml
gesBlock: case
id: literal-items-limit
kind: valueApi
level: atomic
valueApi:
  literal:
    text:
      prefix: "["
      repeat: "0,"
      count: 65536
      suffix: "0]"
    mode: lenient
```

### Expectation

```yaml
gesBlock: expect
literal:
  limit: { name: "MaxLiteralItems", limit: 65536 }
```

---

## Test: strict literal parsing outside a host reports the input scalar limit

This public API case parses whitespace one scalar longer than MaxLiteralInputScalars with `ParseLiteralStrict`; the input length is checked before recognition, so the limit is reported instead of absence.

### Case description

```yaml
gesBlock: case
id: literal-strict-input-scalars-limit
kind: valueApi
level: atomic
valueApi:
  literal:
    text:
      repeat: " "
      count: 1048577
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  limit: { name: "MaxLiteralInputScalars", limit: 1048576 }
```

---

## Test: literal parsing outside a host requires the type name directly after its colon

This public API case parses a typed form with whitespace after its colon with `ParseLiteralStrict`; as in source, the form is not recognized.

### Case description

```yaml
gesBlock: case
id: literal-strict-type-name-after-colon
kind: valueApi
level: atomic
valueApi:
  literal:
    text: ": Number(5)"
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  unrecognized: true
```

---

## Test: strict literal parsing outside a host reports the depth limit

This public API case parses the literal input with `ParseLiteralStrict` and verifies the recognized value, the strict rejection, or the reported limit.

### Case description

```yaml
gesBlock: case
id: literal-strict-depth-limit
kind: valueApi
level: atomic
valueApi:
  literal:
    text: "[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]"
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  limit: { name: "MaxLiteralDepth", limit: 64 }
```

---

## Test: literal parsing outside a host bounds Range materialization by the default MaxRangeItems

This public API case parses the literal input with `ParseLiteralStrict` and verifies the recognized value, the strict rejection, or the reported limit.

### Case description

```yaml
gesBlock: case
id: literal-range-materialization-limit
kind: valueApi
level: atomic
valueApi:
  literal:
    text: ":List(:Range(1, 10001))"
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  limit: { name: "MaxRangeItems", limit: 10000 }
```

---

## Test: literal parsing outside a host bounds Range materialization in total

This public API case parses two Range materializations of 6,000 items each with `ParseLiteralStrict`; each stays below the default MaxRangeItems, but their total exceeds it.

### Case description

```yaml
gesBlock: case
id: literal-range-materialization-total-limit
kind: valueApi
level: atomic
valueApi:
  literal:
    text: "[:List(:Range(1, 6000)), :List(:Range(1, 6000))]"
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  limit: { name: "MaxRangeItems", limit: 10000 }
```
