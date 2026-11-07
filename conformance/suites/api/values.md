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

---

## Test: lenient parsing keeps a Handler form with a doubled underscore as Text

This public API case parses a literal whose name breaks the segment rule: it constructs nothing.

### Case description

```yaml
gesBlock: case
id: literal-invalid-underscore-name-1
kind: valueApi
level: atomic
valueApi:
  literal:
    text: ":Handler(Unit__Fire(value))"
    mode: lenient
```

### Expectation

```yaml
gesBlock: expect
literal:
  value: { type: ":Text", value: ":Handler(Unit__Fire(value))" }
```

---

## Test: strict parsing does not recognize a Handler form with a doubled underscore

This public API case parses a literal whose name breaks the segment rule: it constructs nothing.

### Case description

```yaml
gesBlock: case
id: literal-invalid-underscore-name-2
kind: valueApi
level: atomic
valueApi:
  literal:
    text: ":Handler(Unit__Fire(value))"
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  unrecognized: true
```

---

## Test: strict parsing does not recognize a Message form with a digit segment

This public API case parses a literal whose name breaks the segment rule: it constructs nothing.

### Case description

```yaml
gesBlock: case
id: literal-invalid-underscore-name-3
kind: valueApi
level: atomic
valueApi:
  literal:
    text: ":Message(Unit_1())"
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  unrecognized: true
```

---

## Test: a strict Record form with an invalid type name yields Nothing

This public API case parses a literal whose name breaks the segment rule: it constructs nothing.

### Case description

```yaml
gesBlock: case
id: literal-invalid-underscore-name-4
kind: valueApi
level: atomic
valueApi:
  literal:
    text: ":Record(\"Unit__Weapon\", [a: 1])"
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  value: { type: ":Nothing" }
```

---

## Test: a lenient Record form with an invalid type name yields Nothing

This public API case parses a complete `:Record` form whose Text is no type name: the form is recognized and its value is Nothing.

### Case description

```yaml
gesBlock: case
id: literal-invalid-underscore-record-lenient
kind: valueApi
level: atomic
valueApi:
  literal:
    text: ":Record(\"Unit__Weapon\", [a: 1])"
    mode: lenient
```

### Expectation

```yaml
gesBlock: expect
literal:
  value: { type: ":Nothing" }
```

---

## Test: lenient parsing keeps a Vector with a labeled gap as Text

This public API case parses a Vector whose labels skip `y`: the form is not recognized.

### Case description

```yaml
gesBlock: case
id: literal-spatial-gap-lenient
kind: valueApi
level: atomic
valueApi:
  literal:
    text: ":Vector(x: 1, z: 2)"
    mode: lenient
```

### Expectation

```yaml
gesBlock: expect
literal:
  value: { type: ":Text", value: ":Vector(x: 1, z: 2)" }
```

---

## Test: strict parsing does not recognize a Vector with a labeled gap

This public API case parses a Vector whose labels skip `y`: the form is not recognized.

### Case description

```yaml
gesBlock: case
id: literal-spatial-gap-strict
kind: valueApi
level: atomic
valueApi:
  literal:
    text: ":Vector(x: 1, z: 2)"
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  unrecognized: true
```

---

## Test: lenient parsing keeps a Vector of Boolean literals as Text

This public API case parses a Vector whose components are `true` and `false`: the form is not recognized.

### Case description

```yaml
gesBlock: case
id: literal-spatial-boolean-lenient
kind: valueApi
level: atomic
valueApi:
  literal:
    text: ":Vector(true, false, true)"
    mode: lenient
```

### Expectation

```yaml
gesBlock: expect
literal:
  value: { type: ":Text", value: ":Vector(true, false, true)" }
```

---

## Test: strict parsing does not recognize a Vector of Boolean literals

This public API case parses a Vector whose components are `true` and `false`: the form is not recognized.

### Case description

```yaml
gesBlock: case
id: literal-spatial-boolean-strict
kind: valueApi
level: atomic
valueApi:
  literal:
    text: ":Vector(true, false, true)"
    mode: strict
```

### Expectation

```yaml
gesBlock: expect
literal:
  unrecognized: true
```

---

## Test: a Map from ordered entries keeps the last duplicate in canonical order

This public API case creates a Map through the ordered-entries factory and traverses it in canonical key order.

### Case description

```yaml
gesBlock: case
id: access-map-entries-duplicates
kind: valueApi
level: atomic
valueApi:
  access:
    fromEntries: true
    value:
      type: ":Map"
      entries:
        - { key: b, value: { type: ":Number.int64", value: "1" } }
        - { key: a, value: { type: ":Number.int64", value: "2" } }
        - { key: "𐀀", value: { type: ":Number.int64", value: "3" } }
        - { key: b, value: { type: ":Number.int64", value: "4" } }
    members: [[b]]
```

### Expectation

```yaml
gesBlock: expect
access:
  normalized:
    type: ":Map"
    entries:
      - { key: a, value: { type: ":Number.int64", value: "2" } }
      - { key: b, value: { type: ":Number.int64", value: "4" } }
      - { key: "𐀀", value: { type: ":Number.int64", value: "3" } }
  members:
    - { type: ":Number.int64", value: "4" }
  traversal:
    - { key: a, value: { type: ":Number.int64", value: "2" } }
    - { key: b, value: { type: ":Number.int64", value: "4" } }
    - { key: "𐀀", value: { type: ":Number.int64", value: "3" } }
```

---

## Test: a Record from ordered fields keeps the last duplicate in canonical order

This public API case creates a Record through the ordered-entries factory.

### Case description

```yaml
gesBlock: case
id: access-record-entries-duplicates
kind: valueApi
level: atomic
valueApi:
  access:
    fromEntries: true
    value:
      type: ":Hit"
      entries:
        - { key: b, value: { type: ":Number.int64", value: "1" } }
        - { key: a, value: { type: ":Number.int64", value: "2" } }
        - { key: b, value: { type: ":Number.int64", value: "3" } }
    members: [[a], [b]]
```

### Expectation

```yaml
gesBlock: expect
access:
  normalized:
    type: ":Hit"
    entries:
      - { key: a, value: { type: ":Number.int64", value: "2" } }
      - { key: b, value: { type: ":Number.int64", value: "3" } }
  members:
    - { type: ":Number.int64", value: "2" }
    - { type: ":Number.int64", value: "3" }
  traversal:
    - { key: a, value: { type: ":Number.int64", value: "2" } }
    - { key: b, value: { type: ":Number.int64", value: "3" } }
```

---

## Test: a Record from parallel fields keeps the last duplicate

This public API case gives the parallel Record factory a duplicate name, as the parallel Map factory allows.

### Case description

```yaml
gesBlock: case
id: record-parallel-duplicates
kind: valueApi
level: atomic
valueApi:
  value:
    type: ":Hit"
    entries:
      - { key: b, value: { type: ":Number.int64", value: "1" } }
      - { key: a, value: { type: ":Number.int64", value: "2" } }
      - { key: b, value: { type: ":Number.int64", value: "3" } }
```

### Expectation

```yaml
gesBlock: expect
value:
  normalized:
    type: ":Hit"
    entries:
      - { key: a, value: { type: ":Number.int64", value: "2" } }
      - { key: b, value: { type: ":Number.int64", value: "3" } }
  length: 2
```

---

## Test: an empty Map traverses nothing and has no members

This public API case reads an empty Map created from no entries.

### Case description

```yaml
gesBlock: case
id: access-empty-map
kind: valueApi
level: atomic
valueApi:
  access:
    fromEntries: true
    value: { type: ":Map", entries: [] }
    members: [[a]]
```

### Expectation

```yaml
gesBlock: expect
access:
  normalized: { type: ":Map", entries: [] }
  members:
    - { type: ":Nothing" }
  traversal: []
```

---

## Test: members chain through Records and Maps and a missing name gives Nothing

This public API case reads fields and entries by name, chained, including a missing name, a stored Nothing and a name below a scalar.

### Case description

```yaml
gesBlock: case
id: access-members-chain
kind: valueApi
level: atomic
valueApi:
  access:
    value:
      type: ":Unit"
      entries:
        - key: hp
          value: { type: ":Number.int64", value: "5" }
        - key: gone
          value: { type: ":Nothing" }
        - key: stats
          value:
            type: ":Map"
            entries:
              - { key: level, value: { type: ":Number.int64", value: "7" } }
        - key: position
          value: { type: ":Point", x: "1.5", y: "2.5", z: "3.5", unit: ":meter" }
    members: [[hp], [gone], [missing], [stats, level], [stats, missing], [position, x], [missing, x], [hp, x]]
```

### Expectation

```yaml
gesBlock: expect
access:
  members:
    - { type: ":Number.int64", value: "5" }
    - { type: ":Nothing" }
    - { type: ":Nothing" }
    - { type: ":Number.int64", value: "7" }
    - { type: ":Nothing" }
    - { type: ":Quantity.binary64", value: "1.5", unit: ":meter" }
    - { type: ":Nothing" }
    - { type: ":Nothing" }
```

---

## Test: Vector members are its coordinates

This public API case reads the coordinates of a Vector, which keep its unit and are exact integers when integral, like every Number, and names that are no coordinates.

### Case description

```yaml
gesBlock: case
id: access-vector-coordinates
kind: valueApi
level: atomic
valueApi:
  access:
    value: { type: ":Vector", x: "1", y: "2.5", z: "0", unit: ":meter" }
    members: [[x], [y], [z], [w], [X], [""]]
```

### Expectation

```yaml
gesBlock: expect
access:
  members:
    - { type: ":Quantity.int64", value: "1", unit: ":meter" }
    - { type: ":Quantity.binary64", value: "2.5", unit: ":meter" }
    - { type: ":Quantity.int64", value: "0", unit: ":meter" }
    - { type: ":Nothing" }
    - { type: ":Nothing" }
    - { type: ":Nothing" }
  traversal: null
```

---

## Test: Point members are its coordinates

This public API case reads the coordinates of a Point, which keep its unit and are exact integers when integral, like every Number, and names that are no coordinates.

### Case description

```yaml
gesBlock: case
id: access-point-coordinates
kind: valueApi
level: atomic
valueApi:
  access:
    value: { type: ":Point", x: "1", y: "2.5", z: "0", unit: ":meter" }
    members: [[x], [y], [z], [w], [X], [""]]
```

### Expectation

```yaml
gesBlock: expect
access:
  members:
    - { type: ":Quantity.int64", value: "1", unit: ":meter" }
    - { type: ":Quantity.binary64", value: "2.5", unit: ":meter" }
    - { type: ":Quantity.int64", value: "0", unit: ":meter" }
    - { type: ":Nothing" }
    - { type: ":Nothing" }
    - { type: ":Nothing" }
  traversal: null
```

---

## Test: a Vector without a unit has plain numbers as coordinates

This public API case reads a coordinate of a Vector without a unit.

### Case description

```yaml
gesBlock: case
id: access-vector-without-unit
kind: valueApi
level: atomic
valueApi:
  access:
    value: { type: ":Vector", x: "1", y: "2", z: "3" }
    members: [[y]]
```

### Expectation

```yaml
gesBlock: expect
access:
  members:
    - { type: ":Number.int64", value: "2" }
```

---

## Test: Nothing has no members

This public API case reads names from Nothing, which give Nothing without an error.

### Case description

```yaml
gesBlock: case
id: access-other-nothing
kind: valueApi
level: atomic
valueApi:
  access:
    value: { type: ":Nothing" }
    members: [[name], [x], ["1"]]
```

### Expectation

```yaml
gesBlock: expect
access:
  members:
    - { type: ":Nothing" }
    - { type: ":Nothing" }
    - { type: ":Nothing" }
  traversal: null
```

---

## Test: a Number has no members

This public API case reads names from a Number, which give Nothing without an error.

### Case description

```yaml
gesBlock: case
id: access-other-number
kind: valueApi
level: atomic
valueApi:
  access:
    value: { type: ":Number.int64", value: "1" }
    members: [[name], [x], ["1"]]
```

### Expectation

```yaml
gesBlock: expect
access:
  members:
    - { type: ":Nothing" }
    - { type: ":Nothing" }
    - { type: ":Nothing" }
  traversal: null
```

---

## Test: a Text has no members

This public API case reads names from a Text, which give Nothing without an error.

### Case description

```yaml
gesBlock: case
id: access-other-text
kind: valueApi
level: atomic
valueApi:
  access:
    value: { type: ":Text", value: "x" }
    members: [[name], [x], ["1"]]
```

### Expectation

```yaml
gesBlock: expect
access:
  members:
    - { type: ":Nothing" }
    - { type: ":Nothing" }
    - { type: ":Nothing" }
  traversal: null
```

---

## Test: a List has no members

This public API case reads names from a List, which give Nothing without an error.

### Case description

```yaml
gesBlock: case
id: access-other-list
kind: valueApi
level: atomic
valueApi:
  access:
    value: { type: ":List", items: [{ type: ":Number.int64", value: "1" }] }
    members: [[name], [x], ["1"]]
```

### Expectation

```yaml
gesBlock: expect
access:
  members:
    - { type: ":Nothing" }
    - { type: ":Nothing" }
    - { type: ":Nothing" }
  traversal: null
```

---

## Test: a Message has no members

This public API case reads names from a Message, which give Nothing without an error; a Message exposes no members through this read.

### Case description

```yaml
gesBlock: case
id: access-other-message
kind: valueApi
level: atomic
valueApi:
  access:
    value: { type: ":Message", message: { name: "Foo", args: [], tags: [] } }
    members: [[name], [x], ["1"]]
```

### Expectation

```yaml
gesBlock: expect
access:
  members:
    - { type: ":Nothing" }
    - { type: ":Nothing" }
    - { type: ":Nothing" }
  traversal: null
```

---

## Test: an external value has no members and its getters stay unread

This public API case reads a name an external value declares: the result is Nothing and no field getter runs.

### Case description

```yaml
gesBlock: case
id: access-external
kind: valueApi
level: atomic
valueApi:
  access:
    external: counting
    members: [[x], [missing]]
```

### Expectation

```yaml
gesBlock: expect
access:
  members:
    - { type: ":Nothing" }
    - { type: ":Nothing" }
  traversal: null
  asMapPresent: false
  hostObject: true
  externalFieldReads: 0
```

---

## Test: reading one field of an external value runs only its getter each time

This public API case reads a declared field twice and an undeclared name; the probe counts its reads, so nothing is kept between them.

### Case description

```yaml
gesBlock: case
id: access-external-read-field
kind: valueApi
level: atomic
valueApi:
  access:
    external: counting
    readFields: [x, x, missing]
```

### Expectation

```yaml
gesBlock: expect
access:
  readFields:
    - { type: ":Number.int64", value: "1" }
    - { type: ":Number.int64", value: "2" }
    - null
  externalFieldReads: 2
```

---

## Test: materializing an external value runs its getters on every call

This public API case materializes an external value twice; the second map sees the changed field.

### Case description

```yaml
gesBlock: case
id: access-external-materialized
kind: valueApi
level: atomic
valueApi:
  access:
    external: counting
    materialize: 2
```

### Expectation

```yaml
gesBlock: expect
access:
  materialized:
    - - { key: x, value: { type: ":Number.int64", value: "1" } }
    - - { key: x, value: { type: ":Number.int64", value: "2" } }
  asMapPresent: false
  externalFieldReads: 2
```

---

## Test: stored values materialize to their stored fields and have no field reads

This public API case materializes a Record and reads a field that only an external value could provide.

### Case description

```yaml
gesBlock: case
id: access-stored-materialized
kind: valueApi
level: atomic
valueApi:
  access:
    value:
      type: ":Hit"
      entries:
        - { key: a, value: { type: ":Number.int64", value: "1" } }
    readFields: [a]
    materialize: 1
```

### Expectation

```yaml
gesBlock: expect
access:
  materialized:
    - - { key: a, value: { type: ":Number.int64", value: "1" } }
  asMapPresent: true
  hostObject: false
  readFields:
    - null
```

---

## Test: a scalar materializes to no map

This public API case materializes a Number, which has no entries.

### Case description

```yaml
gesBlock: case
id: access-scalar-materialized
kind: valueApi
level: atomic
valueApi:
  access:
    value: { type: ":Number.int64", value: "1" }
    materialize: 1
```

### Expectation

```yaml
gesBlock: expect
access:
  materialized:
    - null
  asMapPresent: false
```

---

## Test: reading a field of an external value applies its declared type

This public API case reads a field declared `Float<Watt>` whose getter returns the integer 3, directly and materialized.

### Case description

```yaml
gesBlock: case
id: access-external-declared-type
kind: valueApi
level: atomic
valueApi:
  access:
    external: typed
    readFields: [power]
    materialize: 1
```

### Expectation

```yaml
gesBlock: expect
access:
  readFields:
    - { type: ":Quantity.int64", value: "3", unit: ":watt" }
  materialized:
    - - { key: power, value: { type: ":Quantity.int64", value: "3", unit: ":watt" } }
  externalFieldReads: 2
```

---

## Test: a failing getter of an external value propagates through the field reads

This public API case reads a field whose getter fails; the member read stays Nothing without running it, and the two reads that run it report the failure.

### Case description

```yaml
gesBlock: case
id: access-external-getter-failure
kind: valueApi
level: atomic
valueApi:
  access:
    external: failing
    members: [[x]]
    readFields: [x]
    materialize: 1
```

### Expectation

```yaml
gesBlock: expect
access:
  members:
    - { type: ":Nothing" }
  readFields:
    - { failure: runtime.externalFieldAccessFailed }
  materialized:
    - { failure: runtime.externalFieldAccessFailed }
  externalFieldReads: 2
```

---

## Test: a deliberate fault of a getter keeps its code through the field reads

This public API case reads a field whose getter raises a deliberate fault; both reads that run it report the fault's own code, not a runtime code.

### Case description

```yaml
gesBlock: case
id: access-external-getter-fault
kind: valueApi
level: atomic
valueApi:
  access:
    external: faulting
    members: [[x]]
    readFields: [x]
    materialize: 1
```

### Expectation

```yaml
gesBlock: expect
access:
  members:
    - { type: ":Nothing" }
  readFields:
    - { failure: probe.fault }
  materialized:
    - { failure: probe.fault }
  externalFieldReads: 2
```

---

## Test: materializing runs the getters in declaration order

This public API case materializes an external value whose fields `z` and then `a` both fail; the first declared field decides the failure, not the first in key order.

### Case description

```yaml
gesBlock: case
id: access-external-first-declared-failure
kind: valueApi
level: atomic
valueApi:
  access:
    external: ordered
    readFields: [a, z]
    materialize: 1
```

### Expectation

```yaml
gesBlock: expect
access:
  readFields:
    - { failure: probe.a }
    - { failure: probe.z }
  materialized:
    - { failure: probe.z }
  externalFieldReads: 3
```

---

## Test: materializing leaves out a field without a value

This public API case materializes an external value declared `c`, `b`, `a`, whose field `b` has no value; the map has canonical key order, and every getter ran.

### Case description

```yaml
gesBlock: case
id: access-external-absent-field
kind: valueApi
level: atomic
valueApi:
  access:
    external: sparse
    readFields: [b]
    materialize: 1
```

### Expectation

```yaml
gesBlock: expect
access:
  readFields:
    - null
  materialized:
    - - { key: a, value: { type: ":Number.int64", value: "3" } }
      - { key: c, value: { type: ":Number.int64", value: "1" } }
  length: 3
  externalFieldReads: 4
```

---

## Test: the length of an external value runs no getter

This public API case reads the length of an external value whose getter fails; it counts the declared field and runs nothing.

### Case description

```yaml
gesBlock: case
id: access-external-length-runs-no-getter
kind: valueApi
level: atomic
valueApi:
  access:
    external: failing
```

### Expectation

```yaml
gesBlock: expect
access:
  length: 1
  externalFieldReads: 0
```

---

## Test: JSON exports an external value as a Record through its getters

This public API case serializes an external value; it runs the getter and writes a Record of the same type name.

### Case description

```yaml
gesBlock: case
id: access-external-json
kind: valueApi
level: atomic
valueApi:
  access:
    external: counting
    toJson: true
```

### Expectation

```yaml
gesBlock: expect
access:
  json: "{\"value\":{\"entries\":[{\"key\":\"x\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"1\"}}],\"name\":\"AccessProbe\",\"type\":\"Record\"},\"version\":1}"
  externalFieldReads: 1
```

---

## Test: a failing getter propagates through JSON

This public API case serializes an external value whose getter fails; the serialization reports the runtime failure, not a format error.

### Case description

```yaml
gesBlock: case
id: access-external-json-getter-failure
kind: valueApi
level: atomic
valueApi:
  access:
    external: failing
    toJson: true
```

### Expectation

```yaml
gesBlock: expect
access:
  json: { failure: runtime.externalFieldAccessFailed }
  externalFieldReads: 1
```

---

## Test: a deliberate fault of a getter keeps its code through JSON

This public API case serializes an external value whose getter raises a deliberate fault; the serialization reports its own code.

### Case description

```yaml
gesBlock: case
id: access-external-json-getter-fault
kind: valueApi
level: atomic
valueApi:
  access:
    external: faulting
    toJson: true
```

### Expectation

```yaml
gesBlock: expect
access:
  json: { failure: probe.fault }
  externalFieldReads: 1
```

---

## Test: a failing getter propagates through message JSON

This public API case serializes a message with an external argument whose getter fails; the serialization reports the runtime failure, not a format error.

### Case description

```yaml
gesBlock: case
id: access-external-message-json-getter-failure
kind: valueApi
level: atomic
valueApi:
  access:
    external: failing
    toMessageJson: true
```

### Expectation

```yaml
gesBlock: expect
access:
  messageJson: { failure: runtime.externalFieldAccessFailed }
  externalFieldReads: 1
```

---

## Test: a deliberate fault of a getter keeps its code through message JSON

This public API case serializes a message with an external argument whose getter raises a deliberate fault; the serialization reports its own code.

### Case description

```yaml
gesBlock: case
id: access-external-message-json-getter-fault
kind: valueApi
level: atomic
valueApi:
  access:
    external: faulting
    toMessageJson: true
```

### Expectation

```yaml
gesBlock: expect
access:
  messageJson: { failure: probe.fault }
  externalFieldReads: 1
```
