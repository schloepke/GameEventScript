---
formatVersion: 1
suiteId: api.schema-computed-spatial
title: Computed spatial fields of GES schemas
kind: schemaApi
level: atomic
categories: [conformance]
tags: [portable-api, schemas]
---

# Computed spatial fields of GES schemas

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies computed fields of the spatial types: their constructors, parts and arithmetic, the values decoding
derives with the rules of the virtual machine, the canonical form and the binding errors.


---

## Test: the extent and bounds of a map

A computed vector reads a nested record; a computed box reads the vector's axes, later numbers read both, and `abs` gives the vector's length.

### Case description

```yaml
gesBlock: case
id: decode-map-extent
schemaApi:
  sources:
    - "schema :Arena as {\n  columns: :Number integer between 1 and 1000\n  rows: :Number integer between 1 and 1000\n  spacing: :Quantity(m) between 0m and 10m step 1cm\n}\nschema :Ground as {\n  arena: :Arena\n  size: :Vector(m) computed by\n    :Vector(x: (arena.columns - 1) * arena.spacing, y: (arena.rows - 1) * arena.spacing, z: 0m)\n  bounds: :Box(m) computed by :Box(lower: :Point(x: 0m, y: 0m, z: 0m), upper: :Point(x: size.x, y: size.y, z: 0m))\n  area: :Quantity(m²) computed by size.x * size.y\n  diagonal: :Quantity(m) computed by abs size\n  middle: :Point(m) computed by bounds.center\n}\n"
  root: "Ground"
  encode:
    type: ":Ground"
    entries:
      - key: "arena"
        value:
          type: ":Arena"
          entries:
            - key: "columns"
              value:
                type: ":Number.int64"
                value: "4"
            - key: "rows"
              value:
                type: ":Number.int64"
                value: "5"
            - key: "spacing"
              value:
                type: ":Quantity.binary64"
                value: "1.5"
                unit: ":meter"
  decode: "03106009"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "03106009"
  decoded:
    value:
      type: ":Ground"
      entries:
        - key: "area"
          value:
            type: ":Quantity.int64"
            value: "27"
            unit: ":squareMeter"
        - key: "arena"
          value:
            type: ":Arena"
            entries:
              - key: "columns"
                value:
                  type: ":Number.int64"
                  value: "4"
              - key: "rows"
                value:
                  type: ":Number.int64"
                  value: "5"
              - key: "spacing"
                value:
                  type: ":Quantity.binary64"
                  value: "1.5"
                  unit: ":meter"
        - key: "bounds"
          value:
            type: ":Box"
            lower:
              type: ":Point"
              x: "0"
              y: "0"
              z: "0"
            upper:
              type: ":Point"
              x: "4.5"
              y: "6"
              z: "0"
            unit: ":meter"
        - key: "diagonal"
          value:
            type: ":Quantity.binary64"
            value: "7.5"
            unit: ":meter"
        - key: "middle"
          value:
            type: ":Point"
            x: "2.25"
            y: "3"
            z: "0"
            unit: ":meter"
        - key: "size"
          value:
            type: ":Vector"
            x: "4.5"
            y: "6"
            z: "0"
            unit: ":meter"
    byteCount: 4
```

---

## Test: components, omitted axes and positional forms

Labeled components are a run without gaps, positional ones fill x, y and z in order, and an omitted component is zero in the unit of the others; an integer component becomes binary64.

### Case description

```yaml
gesBlock: case
id: vector-components
schemaApi:
  sources:
    - "schema :Body as {\n  x: :Quantity(m) between -1000m and 1000m step 1cm\n  y: :Quantity(m) between -1000m and 1000m step 1cm\n  count: :Number integer between -9007199254740993 and 9007199254740993\n  zero: :Number integer between 0 and 1\n  a: :Vector(m) computed by :Vector(x: x, y: y)\n  b: :Point(m) computed by :Point(y: x, z: y)\n  c: :Vector(m) computed by :Vector(x, y, 1m)\n  d: :Vector computed by :Vector()\n  e: :Vector computed by :Vector(count, 1)\n  f: :Point computed by :Point(x: count)\n}\n"
  root: "Body"
  encode:
    type: ":Body"
    entries:
      - key: "count"
        value:
          type: ":Number.int64"
          value: "9007199254740993"
      - key: "x"
        value:
          type: ":Quantity.binary64"
          value: "1.5"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.binary64"
          value: "-2.25"
          unit: ":meter"
      - key: "zero"
        value:
          type: ":Number.int64"
          value: "0"
  decode: "3687fd162600000000000004"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "3687fd162600000000000004"
  decoded:
    value:
      type: ":Body"
      entries:
        - key: "a"
          value:
            type: ":Vector"
            x: "1.5"
            y: "-2.25"
            z: "0"
            unit: ":meter"
        - key: "b"
          value:
            type: ":Point"
            x: "0"
            y: "1.5"
            z: "-2.25"
            unit: ":meter"
        - key: "c"
          value:
            type: ":Vector"
            x: "1.5"
            y: "-2.25"
            z: "1"
            unit: ":meter"
        - key: "count"
          value:
            type: ":Number.int64"
            value: "9007199254740993"
        - key: "d"
          value:
            type: ":Vector"
            x: "0"
            y: "0"
            z: "0"
        - key: "e"
          value:
            type: ":Vector"
            x: "9007199254740992"
            y: "1"
            z: "0"
        - key: "f"
          value:
            type: ":Point"
            x: "9007199254740992"
            y: "0"
            z: "0"
        - key: "x"
          value:
            type: ":Quantity.binary64"
            value: "1.5"
            unit: ":meter"
        - key: "y"
          value:
            type: ":Quantity.binary64"
            value: "-2.25"
            unit: ":meter"
        - key: "zero"
          value:
            type: ":Number.int64"
            value: "0"
    byteCount: 12
```

---

## Test: a zero component is positive

A Vector or Point holds zero as +0, as the virtual machine stores it, also where a product or negation gives -0.

### Case description

```yaml
gesBlock: case
id: vector-negative-zero
schemaApi:
  sources:
    - "schema :Body as {\n  x: :Quantity(m) between -1000m and 1000m step 1cm\n  y: :Quantity(m) between -1000m and 1000m step 1cm\n  count: :Number integer between -9007199254740993 and 9007199254740993\n  zero: :Number integer between 0 and 1\n  a: :Vector(m) computed by :Vector(x: zero * 1m, y: y) * -1\n  b: :Vector(m) computed by -:Vector(x: zero * 1m, y: x)\n  c: :Number computed by (:Vector(x: zero, y: 1) * -1).x\n}\n"
  root: "Body"
  encode:
    type: ":Body"
    entries:
      - key: "count"
        value:
          type: ":Number.int64"
          value: "3"
      - key: "x"
        value:
          type: ":Quantity.binary64"
          value: "1.5"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.binary64"
          value: "-2.25"
          unit: ":meter"
      - key: "zero"
        value:
          type: ":Number.int64"
          value: "0"
  decode: "3687fd164600000000000002"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "3687fd164600000000000002"
  decoded:
    value:
      type: ":Body"
      entries:
        - key: "a"
          value:
            type: ":Vector"
            x: "0"
            y: "2.25"
            z: "0"
            unit: ":meter"
        - key: "b"
          value:
            type: ":Vector"
            x: "0"
            y: "-1.5"
            z: "0"
            unit: ":meter"
        - key: "c"
          value:
            type: ":Number.int64"
            value: "0"
        - key: "count"
          value:
            type: ":Number.int64"
            value: "3"
        - key: "x"
          value:
            type: ":Quantity.binary64"
            value: "1.5"
            unit: ":meter"
        - key: "y"
          value:
            type: ":Quantity.binary64"
            value: "-2.25"
            unit: ":meter"
        - key: "zero"
          value:
            type: ":Number.int64"
            value: "0"
    byteCount: 12
```

---

## Test: a NaN component makes the value nothing

A component of 0 / 0 makes a Vector or Point nothing, which an optional field leaves absent and a fallback replaces; an infinite component stays.

### Case description

```yaml
gesBlock: case
id: vector-nan-infinite
schemaApi:
  sources:
    - "schema :Body as {\n  x: :Quantity(m) between -1000m and 1000m step 1cm\n  y: :Quantity(m) between -1000m and 1000m step 1cm\n  count: :Number integer between -9007199254740993 and 9007199254740993\n  zero: :Number integer between 0 and 1\n  a: :Vector(m) optional computed by :Vector(x: x / zero * zero, y: y)\n  b: :Vector(m) computed by :Vector(x: x / zero, y: y)\n  c: :Vector(m) computed by :Vector(x: x / zero * zero, y: y) default :Vector(x: 1m, y: 1m)\n  d: :Boolean computed by :Point(x: zero / zero, y: 1) has value\n}\n"
  root: "Body"
  encode:
    type: ":Body"
    entries:
      - key: "count"
        value:
          type: ":Number.int64"
          value: "3"
      - key: "x"
        value:
          type: ":Quantity.binary64"
          value: "1.5"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.binary64"
          value: "-2.25"
          unit: ":meter"
      - key: "zero"
        value:
          type: ":Number.int64"
          value: "0"
  decode: "3687fd164600000000000002"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "3687fd164600000000000002"
  decoded:
    value:
      type: ":Body"
      entries:
        - key: "a"
          value:
            type: ":Nothing"
        - key: "b"
          value:
            type: ":Vector"
            x: "Infinity"
            y: "-2.25"
            z: "0"
            unit: ":meter"
        - key: "c"
          value:
            type: ":Vector"
            x: "1"
            y: "1"
            z: "0"
            unit: ":meter"
        - key: "count"
          value:
            type: ":Number.int64"
            value: "3"
        - key: "d"
          value:
            type: ":Boolean"
            value: "false"
        - key: "x"
          value:
            type: ":Quantity.binary64"
            value: "1.5"
            unit: ":meter"
        - key: "y"
          value:
            type: ":Quantity.binary64"
            value: "-2.25"
            unit: ":meter"
        - key: "zero"
          value:
            type: ":Number.int64"
            value: "0"
    byteCount: 12
```

---

## Test: vector and point arithmetic

Vectors add and subtract, a point moves by a vector, two points subtract to a vector, and a vector scales by a number, with the product unit, or a percentage.

### Case description

```yaml
gesBlock: case
id: vector-arithmetic
schemaApi:
  sources:
    - "schema :Body as {\n  x: :Quantity(m) between -1000m and 1000m step 1cm\n  y: :Quantity(m) between -1000m and 1000m step 1cm\n  count: :Number integer between -9007199254740993 and 9007199254740993\n  zero: :Number integer between 0 and 1\n  p: :Point(m) computed by :Point(x: x, y: y)\n  v: :Vector(m) computed by :Vector(x: 1m, y: 2m, z: 3m)\n  sum: :Vector(m) computed by v + v\n  difference: :Vector(m) computed by v - :Vector(x: x, y: y)\n  moved: :Point(m) computed by p + v\n  back: :Point(m) computed by p - v\n  between: :Vector(m) computed by p - :Point(x: 0.5m, y: 3m)\n  scaled: :Vector(m) computed by v * 2.5\n  left: :Vector(m) computed by 3 * v\n  ratio: :Vector(m) computed by v * 50%\n  speed: :Vector(m/s) computed by v / 2s\n  plain: :Vector(m) computed by :Vector(x: 1, y: 2) * 3m\n  shrunk: :Vector(m) computed by v / 4\n  negated: :Vector(m) computed by -v\n}\n"
  root: "Body"
  encode:
    type: ":Body"
    entries:
      - key: "count"
        value:
          type: ":Number.int64"
          value: "3"
      - key: "x"
        value:
          type: ":Quantity.binary64"
          value: "1.5"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.binary64"
          value: "-2.25"
          unit: ":meter"
      - key: "zero"
        value:
          type: ":Number.int64"
          value: "0"
  decode: "3687fd164600000000000002"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "3687fd164600000000000002"
  decoded:
    value:
      type: ":Body"
      entries:
        - key: "back"
          value:
            type: ":Point"
            x: "0.5"
            y: "-4.25"
            z: "-3"
            unit: ":meter"
        - key: "between"
          value:
            type: ":Vector"
            x: "1"
            y: "-5.25"
            z: "0"
            unit: ":meter"
        - key: "count"
          value:
            type: ":Number.int64"
            value: "3"
        - key: "difference"
          value:
            type: ":Vector"
            x: "-0.5"
            y: "4.25"
            z: "3"
            unit: ":meter"
        - key: "left"
          value:
            type: ":Vector"
            x: "3"
            y: "6"
            z: "9"
            unit: ":meter"
        - key: "moved"
          value:
            type: ":Point"
            x: "2.5"
            y: "-0.25"
            z: "3"
            unit: ":meter"
        - key: "negated"
          value:
            type: ":Vector"
            x: "-1"
            y: "-2"
            z: "-3"
            unit: ":meter"
        - key: "p"
          value:
            type: ":Point"
            x: "1.5"
            y: "-2.25"
            z: "0"
            unit: ":meter"
        - key: "plain"
          value:
            type: ":Vector"
            x: "3"
            y: "6"
            z: "0"
            unit: ":meter"
        - key: "ratio"
          value:
            type: ":Vector"
            x: "0.5"
            y: "1"
            z: "1.5"
            unit: ":meter"
        - key: "scaled"
          value:
            type: ":Vector"
            x: "2.5"
            y: "5"
            z: "7.5"
            unit: ":meter"
        - key: "shrunk"
          value:
            type: ":Vector"
            x: "0.25"
            y: "0.5"
            z: "0.75"
            unit: ":meter"
        - key: "speed"
          value:
            type: ":Vector"
            x: "0.5"
            y: "1"
            z: "1.5"
            unit: ":meterPerSecond"
        - key: "sum"
          value:
            type: ":Vector"
            x: "2"
            y: "4"
            z: "6"
            unit: ":meter"
        - key: "v"
          value:
            type: ":Vector"
            x: "1"
            y: "2"
            z: "3"
            unit: ":meter"
        - key: "x"
          value:
            type: ":Quantity.binary64"
            value: "1.5"
            unit: ":meter"
        - key: "y"
          value:
            type: ":Quantity.binary64"
            value: "-2.25"
            unit: ":meter"
        - key: "zero"
          value:
            type: ":Number.int64"
            value: "0"
    byteCount: 12
```

---

## Test: scaling by an infinite number or dividing by zero gives nothing

A scalar must be finite and a divisor finite and not zero, as the virtual machine requires.

### Case description

```yaml
gesBlock: case
id: vector-scalar-limits
schemaApi:
  sources:
    - "schema :Body as {\n  x: :Quantity(m) between -1000m and 1000m step 1cm\n  y: :Quantity(m) between -1000m and 1000m step 1cm\n  count: :Number integer between -9007199254740993 and 9007199254740993\n  zero: :Number integer between 0 and 1\n  v: :Vector(m) computed by :Vector(x: x, y: y)\n  infinite: :Vector(m) optional computed by v * (1 / zero)\n  divided: :Vector(m) optional computed by v / zero\n  tiny: :Vector(m) computed by v / 2 ^ 1000 / 2 ^ 1000\n}\n"
  root: "Body"
  encode:
    type: ":Body"
    entries:
      - key: "count"
        value:
          type: ":Number.int64"
          value: "3"
      - key: "x"
        value:
          type: ":Quantity.binary64"
          value: "1.5"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.binary64"
          value: "-2.25"
          unit: ":meter"
      - key: "zero"
        value:
          type: ":Number.int64"
          value: "0"
  decode: "3687fd164600000000000002"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "3687fd164600000000000002"
  decoded:
    value:
      type: ":Body"
      entries:
        - key: "count"
          value:
            type: ":Number.int64"
            value: "3"
        - key: "divided"
          value:
            type: ":Nothing"
        - key: "infinite"
          value:
            type: ":Nothing"
        - key: "tiny"
          value:
            type: ":Vector"
            x: "0"
            y: "0"
            z: "0"
            unit: ":meter"
        - key: "v"
          value:
            type: ":Vector"
            x: "1.5"
            y: "-2.25"
            z: "0"
            unit: ":meter"
        - key: "x"
          value:
            type: ":Quantity.binary64"
            value: "1.5"
            unit: ":meter"
        - key: "y"
          value:
            type: ":Quantity.binary64"
            value: "-2.25"
            unit: ":meter"
        - key: "zero"
          value:
            type: ":Number.int64"
            value: "0"
    byteCount: 12
```

---

## Test: the length of a vector

`abs` of a Vector is its length: the correctly rounded square root of the summed squares, in the vector's unit, an integer where it is whole.

### Case description

```yaml
gesBlock: case
id: vector-length
schemaApi:
  sources:
    - "schema :Body as {\n  x: :Quantity(m) between -1000m and 1000m step 1cm\n  y: :Quantity(m) between -1000m and 1000m step 1cm\n  count: :Number integer between -9007199254740993 and 9007199254740993\n  zero: :Number integer between 0 and 1\n  whole: :Quantity(m) computed by abs :Vector(x: 3m, y: 4m)\n  fraction: :Quantity(m) computed by abs :Vector(x: x, y: y, z: 1m)\n  large: :Number computed by abs :Vector(x: 2 ^ 600, y: 2 ^ 600)\n  axes: :Quantity(m) computed by :Vector(x: x, y: y).x + :Point(x: x, y: y).y\n}\n"
  root: "Body"
  encode:
    type: ":Body"
    entries:
      - key: "count"
        value:
          type: ":Number.int64"
          value: "3"
      - key: "x"
        value:
          type: ":Quantity.binary64"
          value: "1.5"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.binary64"
          value: "-2.25"
          unit: ":meter"
      - key: "zero"
        value:
          type: ":Number.int64"
          value: "0"
  decode: "3687fd164600000000000002"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "3687fd164600000000000002"
  decoded:
    value:
      type: ":Body"
      entries:
        - key: "axes"
          value:
            type: ":Quantity.binary64"
            value: "-0.75"
            unit: ":meter"
        - key: "count"
          value:
            type: ":Number.int64"
            value: "3"
        - key: "fraction"
          value:
            type: ":Quantity.binary64"
            value: "2.883140648667699"
            unit: ":meter"
        - key: "large"
          value:
            type: ":Number.binary64"
            value: "Infinity"
        - key: "whole"
          value:
            type: ":Quantity.int64"
            value: "5"
            unit: ":meter"
        - key: "x"
          value:
            type: ":Quantity.binary64"
            value: "1.5"
            unit: ":meter"
        - key: "y"
          value:
            type: ":Quantity.binary64"
            value: "-2.25"
            unit: ":meter"
        - key: "zero"
          value:
            type: ":Number.int64"
            value: "0"
    byteCount: 12
```

---

## Test: large components

Components are binary64, so an integer beyond 2^53 rounds, and a product of large components may overflow to infinity.

### Case description

```yaml
gesBlock: case
id: vector-large-components
schemaApi:
  sources:
    - "schema :Body as {\n  x: :Quantity(m) between -1000m and 1000m step 1cm\n  y: :Quantity(m) between -1000m and 1000m step 1cm\n  count: :Number integer between -9007199254740993 and 9007199254740993\n  zero: :Number integer between 0 and 1\n  big: :Vector computed by :Vector(x: count, y: count * 2)\n  huge: :Vector computed by :Vector(x: 2 ^ 1000, y: 1) * 2 ^ 1000\n}\n"
  root: "Body"
  encode:
    type: ":Body"
    entries:
      - key: "count"
        value:
          type: ":Number.int64"
          value: "9007199254740993"
      - key: "x"
        value:
          type: ":Quantity.binary64"
          value: "1.5"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.binary64"
          value: "-2.25"
          unit: ":meter"
      - key: "zero"
        value:
          type: ":Number.int64"
          value: "0"
  decode: "3687fd162600000000000004"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "3687fd162600000000000004"
  decoded:
    value:
      type: ":Body"
      entries:
        - key: "big"
          value:
            type: ":Vector"
            x: "9007199254740992"
            y: "1.8014398509481984e16"
            z: "0"
        - key: "count"
          value:
            type: ":Number.int64"
            value: "9007199254740993"
        - key: "huge"
          value:
            type: ":Vector"
            x: "Infinity"
            y: "1.0715086071862673e301"
            z: "0"
        - key: "x"
          value:
            type: ":Quantity.binary64"
            value: "1.5"
            unit: ":meter"
        - key: "y"
          value:
            type: ":Quantity.binary64"
            value: "-2.25"
            unit: ":meter"
        - key: "zero"
          value:
            type: ":Number.int64"
            value: "0"
    byteCount: 12
```

---

## Test: a box from corners or from a centre and size

`:Box(lower:, upper:)` orders swapped corners as the runtime does, `:Box(center:, size:)` spans half the size to each side, and the parts read the ordered corners, the centre and the size.

### Case description

```yaml
gesBlock: case
id: box-forms
schemaApi:
  sources:
    - "schema :Body as {\n  x: :Quantity(m) between -1000m and 1000m step 1cm\n  y: :Quantity(m) between -1000m and 1000m step 1cm\n  count: :Number integer between -9007199254740993 and 9007199254740993\n  zero: :Number integer between 0 and 1\n  swapped: :Box(m) computed by :Box(lower: :Point(x: 5m, y: 1m), upper: :Point(x: 2m, y: 3m))\n  around: :Box(m) computed by :Box(center: :Point(x: x, y: y), size: :Vector(x: 4m, y: -2m, z: 1m))\n  lower: :Point(m) computed by swapped.lower\n  upper: :Point(m) computed by swapped.upper\n  middle: :Point(m) computed by around.center\n  extent: :Vector(m) computed by around.size\n  width: :Quantity(m) computed by around.size.x\n}\n"
  root: "Body"
  encode:
    type: ":Body"
    entries:
      - key: "count"
        value:
          type: ":Number.int64"
          value: "3"
      - key: "x"
        value:
          type: ":Quantity.binary64"
          value: "1.5"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.binary64"
          value: "-2.25"
          unit: ":meter"
      - key: "zero"
        value:
          type: ":Number.int64"
          value: "0"
  decode: "3687fd164600000000000002"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "3687fd164600000000000002"
  decoded:
    value:
      type: ":Body"
      entries:
        - key: "around"
          value:
            type: ":Box"
            lower:
              type: ":Point"
              x: "-0.5"
              y: "-3.25"
              z: "-0.5"
            upper:
              type: ":Point"
              x: "3.5"
              y: "-1.25"
              z: "0.5"
            unit: ":meter"
        - key: "count"
          value:
            type: ":Number.int64"
            value: "3"
        - key: "extent"
          value:
            type: ":Vector"
            x: "4"
            y: "2"
            z: "1"
            unit: ":meter"
        - key: "lower"
          value:
            type: ":Point"
            x: "2"
            y: "1"
            z: "0"
            unit: ":meter"
        - key: "middle"
          value:
            type: ":Point"
            x: "1.5"
            y: "-2.25"
            z: "0"
            unit: ":meter"
        - key: "swapped"
          value:
            type: ":Box"
            lower:
              type: ":Point"
              x: "2"
              y: "1"
              z: "0"
            upper:
              type: ":Point"
              x: "5"
              y: "3"
              z: "0"
            unit: ":meter"
        - key: "upper"
          value:
            type: ":Point"
            x: "5"
            y: "3"
            z: "0"
            unit: ":meter"
        - key: "width"
          value:
            type: ":Quantity.int64"
            value: "4"
            unit: ":meter"
        - key: "x"
          value:
            type: ":Quantity.binary64"
            value: "1.5"
            unit: ":meter"
        - key: "y"
          value:
            type: ":Quantity.binary64"
            value: "-2.25"
            unit: ":meter"
        - key: "zero"
          value:
            type: ":Number.int64"
            value: "0"
    byteCount: 12
```

---

## Test: a disc around a centre or the origin

A disc keeps its radius's magnitude; a radius of nothing makes it nothing, and its parts read the centre and radius.

### Case description

```yaml
gesBlock: case
id: disc-forms
schemaApi:
  sources:
    - "schema :Body as {\n  x: :Quantity(m) between -1000m and 1000m step 1cm\n  y: :Quantity(m) between -1000m and 1000m step 1cm\n  count: :Number integer between -9007199254740993 and 9007199254740993\n  zero: :Number integer between 0 and 1\n  zone: :Disc(m) computed by :Disc(center: :Point(x: x, y: y), radius: -2.5m)\n  ring: :Disc(m) computed by :Disc(radius: 3m)\n  lost: :Disc(m) optional computed by :Disc(radius: x / zero * zero)\n  center: :Point(m) computed by zone.center\n  radius: :Quantity(m) computed by zone.radius\n}\n"
  root: "Body"
  encode:
    type: ":Body"
    entries:
      - key: "count"
        value:
          type: ":Number.int64"
          value: "3"
      - key: "x"
        value:
          type: ":Quantity.binary64"
          value: "1.5"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.binary64"
          value: "-2.25"
          unit: ":meter"
      - key: "zero"
        value:
          type: ":Number.int64"
          value: "0"
  decode: "3687fd164600000000000002"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "3687fd164600000000000002"
  decoded:
    value:
      type: ":Body"
      entries:
        - key: "center"
          value:
            type: ":Point"
            x: "1.5"
            y: "-2.25"
            z: "0"
            unit: ":meter"
        - key: "count"
          value:
            type: ":Number.int64"
            value: "3"
        - key: "lost"
          value:
            type: ":Nothing"
        - key: "radius"
          value:
            type: ":Quantity.binary64"
            value: "2.5"
            unit: ":meter"
        - key: "ring"
          value:
            type: ":Disc"
            center:
              type: ":Point"
              x: "0"
              y: "0"
              z: "0"
            radius: "3"
            unit: ":meter"
        - key: "x"
          value:
            type: ":Quantity.binary64"
            value: "1.5"
            unit: ":meter"
        - key: "y"
          value:
            type: ":Quantity.binary64"
            value: "-2.25"
            unit: ":meter"
        - key: "zero"
          value:
            type: ":Number.int64"
            value: "0"
        - key: "zone"
          value:
            type: ":Disc"
            center:
              type: ":Point"
              x: "1.5"
              y: "-2.25"
              z: "0"
            radius: "2.5"
            unit: ":meter"
    byteCount: 12
```

---

## Test: a polygon from points, a list field or a projection

`:Poly(points:)` takes a list of points, a list field of Points or a projection of a list field with `:select`; `points[:count]` counts them. A point off the plane z = 0 stays, as the runtime keeps it.

### Case description

```yaml
gesBlock: case
id: poly-forms
schemaApi:
  sources:
    - "schema :Post as {\n  x: :Quantity(m) between 0m and 100m step 1m\n  y: :Quantity(m) between 0m and 100m step 1m\n}\nschema :Path as {\n  marks: :List(:Point(m) {\n    x between 0m and 100m step 1m\n    y between 0m and 100m step 1m\n  }) max 4\n  posts: :List(:Post) max 4\n  triangle: :Poly(m) computed by :Poly(points: [:Point(x: 0m, y: 0m), :Point(x: 4m, y: 0m), :Point(x: 0m, y: 3m, z: 1m)])\n  marked: :Poly(m) computed by :Poly(points: marks)\n  fence: :Poly(m) computed by :Poly(points: posts[:select post => :Point(x: post.x, y: post.y)])\n  count: :Number computed by fence.points[:count]\n}\n"
  root: "Path"
  encode:
    type: ":Path"
    entries:
      - key: "marks"
        value:
          type: ":List"
          items:
            - type: ":Point"
              x: "1"
              y: "2"
              z: "0"
              unit: ":meter"
            - type: ":Point"
              x: "3"
              y: "4"
              z: "0"
              unit: ":meter"
      - key: "posts"
        value:
          type: ":List"
          items:
            - type: ":Post"
              entries:
                - key: "x"
                  value:
                    type: ":Quantity.int64"
                    value: "5"
                    unit: ":meter"
                - key: "y"
                  value:
                    type: ":Quantity.int64"
                    value: "6"
                    unit: ":meter"
            - type: ":Post"
              entries:
                - key: "x"
                  value:
                    type: ":Quantity.int64"
                    value: "7"
                    unit: ":meter"
                - key: "y"
                  value:
                    type: ":Quantity.int64"
                    value: "8"
                    unit: ":meter"
  decode: "0a080604150c0704"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0a080604150c0704"
  decoded:
    value:
      type: ":Path"
      entries:
        - key: "count"
          value:
            type: ":Number.int64"
            value: "2"
        - key: "fence"
          value:
            type: ":Poly"
            points:
              - type: ":Point"
                x: "5"
                y: "6"
                z: "0"
              - type: ":Point"
                x: "7"
                y: "8"
                z: "0"
            unit: ":meter"
        - key: "marked"
          value:
            type: ":Poly"
            points:
              - type: ":Point"
                x: "1"
                y: "2"
                z: "0"
              - type: ":Point"
                x: "3"
                y: "4"
                z: "0"
            unit: ":meter"
        - key: "marks"
          value:
            type: ":List"
            items:
              - type: ":Point"
                x: "1"
                y: "2"
                z: "0"
                unit: ":meter"
              - type: ":Point"
                x: "3"
                y: "4"
                z: "0"
                unit: ":meter"
        - key: "posts"
          value:
            type: ":List"
            items:
              - type: ":Post"
                entries:
                  - key: "x"
                    value:
                      type: ":Quantity.int64"
                      value: "5"
                      unit: ":meter"
                  - key: "y"
                    value:
                      type: ":Quantity.int64"
                      value: "6"
                      unit: ":meter"
              - type: ":Post"
                entries:
                  - key: "x"
                    value:
                      type: ":Quantity.int64"
                      value: "7"
                      unit: ":meter"
                  - key: "y"
                    value:
                      type: ":Quantity.int64"
                      value: "8"
                      unit: ":meter"
        - key: "triangle"
          value:
            type: ":Poly"
            points:
              - type: ":Point"
                x: "0"
                y: "0"
                z: "0"
              - type: ":Point"
                x: "4"
                y: "0"
                z: "0"
              - type: ":Point"
                x: "0"
                y: "3"
                z: "1"
            unit: ":meter"
    byteCount: 8
```

---

## Test: a polygon over an empty list

An empty list field gives a polygon without points, and a nothing point makes the polygon nothing.

### Case description

```yaml
gesBlock: case
id: poly-empty
schemaApi:
  sources:
    - "schema :Post as {\n  x: :Quantity(m) between 0m and 100m step 1m\n  y: :Quantity(m) between 0m and 100m step 1m\n}\nschema :Path as {\n  marks: :List(:Point(m) {\n    x between 0m and 100m step 1m\n    y between 0m and 100m step 1m\n  }) max 4\n  posts: :List(:Post) max 4\n  marked: :Poly(m) computed by :Poly(points: marks)\n  fence: :Poly(m) computed by :Poly(points: posts[:select post => :Point(x: post.x, y: post.y)])\n  count: :Number computed by marked.points[:count]\n  lost: :Poly(m) optional computed by :Poly(points: [:Point(x: 0m, y: 0m), :Point(x: 0m / 0, y: 0m)])\n}\n"
  root: "Path"
  encode:
    type: ":Path"
    entries:
      - key: "marks"
        value:
          type: ":List"
          items: []
      - key: "posts"
        value:
          type: ":List"
          items: []
  decode: "00"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "00"
  decoded:
    value:
      type: ":Path"
      entries:
        - key: "count"
          value:
            type: ":Number.int64"
            value: "0"
        - key: "fence"
          value:
            type: ":Poly"
            points: []
            unit: ":meter"
        - key: "lost"
          value:
            type: ":Nothing"
        - key: "marked"
          value:
            type: ":Poly"
            points: []
            unit: ":meter"
        - key: "marks"
          value:
            type: ":List"
            items: []
        - key: "posts"
          value:
            type: ":List"
            items: []
    byteCount: 1
```

---

## Test: an optional spatial field read with a guard

Reading an optional spatial field gives nothing where it is absent; an optional computed field is then absent, and a guard or fallback keeps a required one.

### Case description

```yaml
gesBlock: case
id: optional-spatial
schemaApi:
  sources:
    - "schema :Drift as {\n  origin: :Point(m) {\n    x between -10m and 10m step 1m\n    y between -10m and 10m step 1m\n  }\n  drift: :Vector(m) {\n    x between -10m and 10m step 1m\n  } optional\n  shifted: :Point(m) optional computed by origin + drift\n  moved: :Point(m) computed by origin + drift default origin\n  drifting: :Boolean computed by drift has value\n  far: :Point(m) computed by origin + drift when drift has value, otherwise origin\n}\n"
  root: "Drift"
  encode:
    type: ":Drift"
    entries:
      - key: "origin"
        value:
          type: ":Point"
          x: "2"
          y: "-3"
          z: "0"
          unit: ":meter"
  decode: "ec00"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "ec00"
  decoded:
    value:
      type: ":Drift"
      entries:
        - key: "drift"
          value:
            type: ":Nothing"
        - key: "drifting"
          value:
            type: ":Boolean"
            value: "false"
        - key: "far"
          value:
            type: ":Point"
            x: "2"
            y: "-3"
            z: "0"
            unit: ":meter"
        - key: "moved"
          value:
            type: ":Point"
            x: "2"
            y: "-3"
            z: "0"
            unit: ":meter"
        - key: "origin"
          value:
            type: ":Point"
            x: "2"
            y: "-3"
            z: "0"
            unit: ":meter"
        - key: "shifted"
          value:
            type: ":Nothing"
    byteCount: 2
```

---

## Test: the components of a vector share one unit

A component of another unit is `schema.unitMismatch`.

### Case description

```yaml
gesBlock: case
id: error-unit-axis
schemaApi:
  sources:
    - "schema :S as {\n  a: :Quantity(m) between 0m and 1m step 1cm\n  v: :Vector(m) computed by :Vector(x: a, y: 1s)\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 3
    column: 46
```

---

## Test: the vector's unit is the field's

A vector of another unit than the field's is `schema.unitMismatch`.

### Case description

```yaml
gesBlock: case
id: error-unit-field
schemaApi:
  sources:
    - "schema :S as {\n  a: :Quantity(m) between 0m and 1m step 1cm\n  v: :Vector(s) computed by :Vector(x: a, y: a)\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 3
    column: 29
```

---

## Test: the corners of a box share one unit

Corners of different units are `schema.unitMismatch`.

### Case description

```yaml
gesBlock: case
id: error-unit-box
schemaApi:
  sources:
    - "schema :S as {\n  a: :Box(m) computed by :Box(lower: :Point(x: 0m, y: 0m), upper: :Point(x: 1s, y: 1s))\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 2
    column: 67
```

---

## Test: the radius of a disc has the centre's unit

A radius of another unit is `schema.unitMismatch`.

### Case description

```yaml
gesBlock: case
id: error-unit-disc
schemaApi:
  sources:
    - "schema :S as {\n  a: :Disc(m) computed by :Disc(center: :Point(x: 0m, y: 0m), radius: 2s)\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 2
    column: 71
```

---

## Test: the points of a polygon share one unit

Points of different units are `schema.unitMismatch`.

### Case description

```yaml
gesBlock: case
id: error-unit-poly
schemaApi:
  sources:
    - "schema :S as {\n  a: :Poly(m) computed by :Poly(points: [:Point(x: 0m, y: 0m), :Point(x: 1s, y: 0s)])\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 2
    column: 64
```

---

## Test: vectors of different units do not add

`schema.unitMismatch`.

### Case description

```yaml
gesBlock: case
id: error-unit-sum
schemaApi:
  sources:
    - "schema :S as {\n  a: :Vector(m) computed by :Vector(x: 1m, y: 1m) + :Vector(x: 1s, y: 1s)\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 2
    column: 29
```

---

## Test: labeled components without gaps

`:Vector(x:, z:)` leaves out y, which is `schema.invalidExpression`.

### Case description

```yaml
gesBlock: case
id: error-axis-gap
schemaApi:
  sources:
    - "schema :S as {\n  v: :Vector(m) computed by :Vector(x: 1m, z: 2m)\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidExpression"
    line: 2
    column: 47
```

---

## Test: a point does not scale

A point times a number is `schema.invalidExpression`.

### Case description

```yaml
gesBlock: case
id: error-point-scale
schemaApi:
  sources:
    - "schema :S as {\n  p: :Point(m) computed by :Point(x: 1m, y: 1m) * 2\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidExpression"
    line: 2
    column: 28
```

---

## Test: a point has no negation

`-point` is `schema.invalidExpression`.

### Case description

```yaml
gesBlock: case
id: error-point-negate
schemaApi:
  sources:
    - "schema :S as {\n  p: :Point(m) computed by -:Point(x: 1m, y: 1m)\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidExpression"
    line: 2
    column: 28
```

---

## Test: spatial values are not compared

`=` of two vectors is `schema.invalidExpression`.

### Case description

```yaml
gesBlock: case
id: error-spatial-compare
schemaApi:
  sources:
    - "schema :S as {\n  b: :Boolean computed by :Vector(x: 1, y: 1) = :Vector(x: 1, y: 1)\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidExpression"
    line: 2
    column: 27
```

---

## Test: a box takes two known forms

`:Box(lower:)` alone is `schema.invalidExpression`.

### Case description

```yaml
gesBlock: case
id: error-box-labels
schemaApi:
  sources:
    - "schema :S as {\n  a: :Box(m) computed by :Box(lower: :Point(x: 0m, y: 0m))\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidExpression"
    line: 2
    column: 26
```

---

## Test: a vector has the parts x, y and z

`.w` is `schema.unknownName`.

### Case description

```yaml
gesBlock: case
id: error-unknown-part
schemaApi:
  sources:
    - "schema :S as {\n  a: :Number computed by :Vector(x: 1, y: 2).w\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unknownName"
    line: 2
    column: 46
```

---

## Test: the points of a polygon take only a count

`shape.points` alone is `schema.invalidExpression`.

### Case description

```yaml
gesBlock: case
id: error-points-alone
schemaApi:
  sources:
    - "schema :S as {\n  a: :Poly(m) computed by :Poly(points: [:Point(x: 0m, y: 0m)])\n  b: :Number computed by a.points\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidExpression"
    line: 3
    column: 28
```

---

## Test: a computed spatial field has no axis block

A computed Vector with axes is `schema.invalidExpression`.

### Case description

```yaml
gesBlock: case
id: error-axes-block
schemaApi:
  sources:
    - "schema :S as {\n  v: :Vector(m) {\n    x between 0m and 1m step 1cm\n  } computed by :Vector(x: 0m, y: 0m)\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidExpression"
    line: 2
    column: 3
```

---

## Test: a required spatial field that may be absent

Moving by an optional vector without a guard is `schema.computedMayBeAbsent`.

### Case description

```yaml
gesBlock: case
id: error-computed-absent
schemaApi:
  sources:
    - "schema :S as {\n  p: :Point(m) {\n    x between 0m and 1m step 1cm\n  }\n  d: :Vector(m) {\n    x between 0m and 1m step 1cm\n  } optional\n  q: :Point(m) computed by p + d\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.computedMayBeAbsent"
    line: 8
    column: 28
```

---

## Test: a projection gives the points of a polygon

`[:select]` outside `:Poly` is `schema.invalidExpression`.

### Case description

```yaml
gesBlock: case
id: error-select-alone
schemaApi:
  sources:
    - "schema :P as {\n  x: :Number between 0 and 1 step 1\n}\nschema :S as {\n  l: :List(:P) max 2\n  n: :Number computed by l[:select p => p.x][:count]\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidExpression"
    line: 6
    column: 27
```

---

## Test: spatial computed fields belong to the hash

Constructors, parts and spatial arithmetic have a prefix form in the canonical text; an omitted component is the integer 0.

### Case description

```yaml
gesBlock: case
id: canonical-spatial
schemaApi:
  sources:
    - "schema :S as {\n  a: :Quantity(m) between 0m and 1m step 1cm\n  v: :Vector(m) computed by :Vector(x: a, y: a) * 2\n  b: :Box(m) computed by :Box(center: :Point(x: a, y: 0m), size: v)\n  w: :Quantity(m) computed by b.size.x\n}\n"
  root: "S"
  bind: false
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema S\nfield a Quantity(meter) between 0e0 1e0 step 1e-2\ncomputed v Vector(meter) (multiply (vector (field a) (field a) (quantity meter 0e0)) (number 2e0))\ncomputed b Box(meter) (boxAround (point (field a) (quantity meter 0e0) (quantity meter 0e0)) (field v))\ncomputed w Quantity(meter) (part x (part size (field b)))\n"
  hash: "166b69167cbd2fa8"
```
