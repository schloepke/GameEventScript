---
formatVersion: 1
suiteId: "runtime.computed-spatial-records"
title: "Spatial computed fields of script records"
categories: [conformance]
---

# Spatial computed fields of script records

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite runs the computed spatial fields of `api.schema-computed-spatial` as fields of script records on the virtual
machine, with the geometry library's `:Box`, `:Disc` and `:Poly`. Each case expects exactly the values that decoding the
schema case derives, so schemas and scripts compute the same bits.


---

## Test: the extent and bounds of a map in a record

The computed fields of the schema case, as script record fields.

### Case description

```yaml
gesBlock: case
id: record-decode-map-extent
kind: scriptApi
level: scenario
sources:
  - name: "record-decode-map-extent.ges"
    program: main
```

### Source code under test

```ges
record :Arena as { columns: :Number, rows: :Number, spacing: :Quantity(m) }
record :Ground as {
  arena: :Arena,
  size: :Vector computed by :Vector(x: (arena.columns - 1) * arena.spacing, y: (arena.rows - 1) * arena.spacing, z: 0m),
  bounds: :Box computed by :Box(lower: :Point(x: 0m, y: 0m, z: 0m), upper: :Point(x: size.x, y: size.y, z: 0m)),
  area: :Quantity(m²) computed by size.x * size.y,
  diagonal: :Quantity(m) computed by abs size,
  middle: :Point computed by bounds.center
}
on Start() {
  let value be :Ground(arena: :Arena(columns: 4, rows: 5, spacing: 1.5m))
  emit Done(size: value.size, bounds: value.bounds, area: value.area, diagonal: value.diagonal, middle: value.middle)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: "expect"
steps:
  step-0001:
    local:
      - name: "Done"
        args:
          - name: "size"
            value:
              type: ":Vector"
              x: "4.5"
              y: "6"
              z: "0"
              unit: ":meter"
          - name: "bounds"
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
          - name: "area"
            value:
              type: ":Quantity.int64"
              value: "27"
              unit: ":squareMeter"
          - name: "diagonal"
            value:
              type: ":Quantity.binary64"
              value: "7.5"
              unit: ":meter"
          - name: "middle"
            value:
              type: ":Point"
              x: "2.25"
              y: "3"
              z: "0"
              unit: ":meter"
```

---

## Test: components in a record

Labeled, positional and omitted components, and an integer beyond 2^53.

### Case description

```yaml
gesBlock: case
id: record-vector-components
kind: scriptApi
level: scenario
sources:
  - name: "record-vector-components.ges"
    program: main
```

### Source code under test

```ges
record :Body as {
  x: :Quantity(m),
  y: :Quantity(m),
  count: :Number,
  zero: :Number,
  a: :Vector computed by :Vector(x: x, y: y),
  b: :Point computed by :Point(y: x, z: y),
  c: :Vector computed by :Vector(x, y, 1m),
  d: :Vector computed by :Vector(),
  e: :Vector computed by :Vector(count, 1),
  f: :Point computed by :Point(x: count)
}
on Start() {
  let value be :Body(x: 1.5m, y: -2.25m, count: 9007199254740993, zero: 0)
  emit Done(a: value.a, b: value.b, c: value.c, d: value.d, e: value.e, f: value.f)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: "expect"
steps:
  step-0001:
    local:
      - name: "Done"
        args:
          - name: "a"
            value:
              type: ":Vector"
              x: "1.5"
              y: "-2.25"
              z: "0"
              unit: ":meter"
          - name: "b"
            value:
              type: ":Point"
              x: "0"
              y: "1.5"
              z: "-2.25"
              unit: ":meter"
          - name: "c"
            value:
              type: ":Vector"
              x: "1.5"
              y: "-2.25"
              z: "1"
              unit: ":meter"
          - name: "d"
            value:
              type: ":Vector"
              x: "0"
              y: "0"
              z: "0"
          - name: "e"
            value:
              type: ":Vector"
              x: "9007199254740992"
              y: "1"
              z: "0"
          - name: "f"
            value:
              type: ":Point"
              x: "9007199254740992"
              y: "0"
              z: "0"
```

---

## Test: a zero component is positive in a record

Products and negations that give -0 store +0.

### Case description

```yaml
gesBlock: case
id: record-vector-negative-zero
kind: scriptApi
level: scenario
sources:
  - name: "record-vector-negative-zero.ges"
    program: main
```

### Source code under test

```ges
record :Body as {
  x: :Quantity(m),
  y: :Quantity(m),
  count: :Number,
  zero: :Number,
  a: :Vector computed by :Vector(x: zero * 1m, y: y) * -1,
  b: :Vector computed by -:Vector(x: zero * 1m, y: x),
  c: :Number computed by (:Vector(x: zero, y: 1) * -1).x
}
on Start() {
  let value be :Body(x: 1.5m, y: -2.25m, count: 3, zero: 0)
  emit Done(a: value.a, b: value.b, c: value.c)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: "expect"
steps:
  step-0001:
    local:
      - name: "Done"
        args:
          - name: "a"
            value:
              type: ":Vector"
              x: "0"
              y: "2.25"
              z: "0"
              unit: ":meter"
          - name: "b"
            value:
              type: ":Vector"
              x: "0"
              y: "-1.5"
              z: "0"
              unit: ":meter"
          - name: "c"
            value:
              type: ":Number.int64"
              value: "0"
```

---

## Test: a NaN component in a record

0 / 0 makes the value nothing; an infinite component stays.

### Case description

```yaml
gesBlock: case
id: record-vector-nan-infinite
kind: scriptApi
level: scenario
sources:
  - name: "record-vector-nan-infinite.ges"
    program: main
```

### Source code under test

```ges
record :Body as {
  x: :Quantity(m),
  y: :Quantity(m),
  count: :Number,
  zero: :Number,
  a: :Vector computed by :Vector(x: x / zero * zero, y: y),
  b: :Vector computed by :Vector(x: x / zero, y: y),
  c: :Vector computed by :Vector(x: x / zero * zero, y: y) default :Vector(x: 1m, y: 1m),
  d: :Boolean computed by :Point(x: zero / zero, y: 1) has value
}
on Start() {
  let value be :Body(x: 1.5m, y: -2.25m, count: 3, zero: 0)
  emit Done(a: value.a, b: value.b, c: value.c, d: value.d)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: "expect"
steps:
  step-0001:
    local:
      - name: "Done"
        args:
          - name: "a"
            value:
              type: ":Nothing"
          - name: "b"
            value:
              type: ":Vector"
              x: "Infinity"
              y: "-2.25"
              z: "0"
              unit: ":meter"
          - name: "c"
            value:
              type: ":Vector"
              x: "1"
              y: "1"
              z: "0"
              unit: ":meter"
          - name: "d"
            value:
              type: ":Boolean"
              value: "false"
```

---

## Test: vector and point arithmetic in a record

The arithmetic of the schema case.

### Case description

```yaml
gesBlock: case
id: record-vector-arithmetic
kind: scriptApi
level: scenario
sources:
  - name: "record-vector-arithmetic.ges"
    program: main
```

### Source code under test

```ges
record :Body as {
  x: :Quantity(m),
  y: :Quantity(m),
  count: :Number,
  zero: :Number,
  p: :Point computed by :Point(x: x, y: y),
  v: :Vector computed by :Vector(x: 1m, y: 2m, z: 3m),
  sum: :Vector computed by v + v,
  difference: :Vector computed by v - :Vector(x: x, y: y),
  moved: :Point computed by p + v,
  back: :Point computed by p - v,
  between: :Vector computed by p - :Point(x: 0.5m, y: 3m),
  scaled: :Vector computed by v * 2.5,
  left: :Vector computed by 3 * v,
  ratio: :Vector computed by v * 50%,
  speed: :Vector computed by v / 2s,
  plain: :Vector computed by :Vector(x: 1, y: 2) * 3m,
  shrunk: :Vector computed by v / 4,
  negated: :Vector computed by -v
}
on Start() {
  let value be :Body(x: 1.5m, y: -2.25m, count: 3, zero: 0)
  emit Done(p: value.p, v: value.v, sum: value.sum, difference: value.difference, moved: value.moved, back: value.back, between: value.between, scaled: value.scaled, left: value.left, ratio: value.ratio, speed: value.speed, plain: value.plain, shrunk: value.shrunk, negated: value.negated)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: "expect"
steps:
  step-0001:
    local:
      - name: "Done"
        args:
          - name: "p"
            value:
              type: ":Point"
              x: "1.5"
              y: "-2.25"
              z: "0"
              unit: ":meter"
          - name: "v"
            value:
              type: ":Vector"
              x: "1"
              y: "2"
              z: "3"
              unit: ":meter"
          - name: "sum"
            value:
              type: ":Vector"
              x: "2"
              y: "4"
              z: "6"
              unit: ":meter"
          - name: "difference"
            value:
              type: ":Vector"
              x: "-0.5"
              y: "4.25"
              z: "3"
              unit: ":meter"
          - name: "moved"
            value:
              type: ":Point"
              x: "2.5"
              y: "-0.25"
              z: "3"
              unit: ":meter"
          - name: "back"
            value:
              type: ":Point"
              x: "0.5"
              y: "-4.25"
              z: "-3"
              unit: ":meter"
          - name: "between"
            value:
              type: ":Vector"
              x: "1"
              y: "-5.25"
              z: "0"
              unit: ":meter"
          - name: "scaled"
            value:
              type: ":Vector"
              x: "2.5"
              y: "5"
              z: "7.5"
              unit: ":meter"
          - name: "left"
            value:
              type: ":Vector"
              x: "3"
              y: "6"
              z: "9"
              unit: ":meter"
          - name: "ratio"
            value:
              type: ":Vector"
              x: "0.5"
              y: "1"
              z: "1.5"
              unit: ":meter"
          - name: "speed"
            value:
              type: ":Vector"
              x: "0.5"
              y: "1"
              z: "1.5"
              unit: ":meterPerSecond"
          - name: "plain"
            value:
              type: ":Vector"
              x: "3"
              y: "6"
              z: "0"
              unit: ":meter"
          - name: "shrunk"
            value:
              type: ":Vector"
              x: "0.25"
              y: "0.5"
              z: "0.75"
              unit: ":meter"
          - name: "negated"
            value:
              type: ":Vector"
              x: "-1"
              y: "-2"
              z: "-3"
              unit: ":meter"
```

---

## Test: scalar limits in a record

An infinite scalar or a zero divisor gives nothing; underflow gives +0.

### Case description

```yaml
gesBlock: case
id: record-vector-scalar-limits
kind: scriptApi
level: scenario
sources:
  - name: "record-vector-scalar-limits.ges"
    program: main
```

### Source code under test

```ges
record :Body as {
  x: :Quantity(m),
  y: :Quantity(m),
  count: :Number,
  zero: :Number,
  v: :Vector computed by :Vector(x: x, y: y),
  infinite: :Vector computed by v * (1 / zero),
  divided: :Vector computed by v / zero,
  tiny: :Vector computed by v / 2 ^ 1000 / 2 ^ 1000
}
on Start() {
  let value be :Body(x: 1.5m, y: -2.25m, count: 3, zero: 0)
  emit Done(v: value.v, infinite: value.infinite, divided: value.divided, tiny: value.tiny)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: "expect"
steps:
  step-0001:
    local:
      - name: "Done"
        args:
          - name: "v"
            value:
              type: ":Vector"
              x: "1.5"
              y: "-2.25"
              z: "0"
              unit: ":meter"
          - name: "infinite"
            value:
              type: ":Nothing"
          - name: "divided"
            value:
              type: ":Nothing"
          - name: "tiny"
            value:
              type: ":Vector"
              x: "0"
              y: "0"
              z: "0"
              unit: ":meter"
```

---

## Test: the length of a vector in a record

`abs` of a Vector, as the schema case computes it.

### Case description

```yaml
gesBlock: case
id: record-vector-length
kind: scriptApi
level: scenario
sources:
  - name: "record-vector-length.ges"
    program: main
```

### Source code under test

```ges
record :Body as {
  x: :Quantity(m),
  y: :Quantity(m),
  count: :Number,
  zero: :Number,
  whole: :Quantity(m) computed by abs :Vector(x: 3m, y: 4m),
  fraction: :Quantity(m) computed by abs :Vector(x: x, y: y, z: 1m),
  large: :Number computed by abs :Vector(x: 2 ^ 600, y: 2 ^ 600),
  axes: :Quantity(m) computed by :Vector(x: x, y: y).x + :Point(x: x, y: y).y
}
on Start() {
  let value be :Body(x: 1.5m, y: -2.25m, count: 3, zero: 0)
  emit Done(whole: value.whole, fraction: value.fraction, large: value.large, axes: value.axes)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: "expect"
steps:
  step-0001:
    local:
      - name: "Done"
        args:
          - name: "whole"
            value:
              type: ":Quantity.int64"
              value: "5"
              unit: ":meter"
          - name: "fraction"
            value:
              type: ":Quantity.binary64"
              value: "2.883140648667699"
              unit: ":meter"
          - name: "large"
            value:
              type: ":Number.binary64"
              value: "Infinity"
          - name: "axes"
            value:
              type: ":Quantity.binary64"
              value: "-0.75"
              unit: ":meter"
```

---

## Test: large components in a record

Binary64 components round and overflow as in the schema case.

### Case description

```yaml
gesBlock: case
id: record-vector-large-components
kind: scriptApi
level: scenario
sources:
  - name: "record-vector-large-components.ges"
    program: main
```

### Source code under test

```ges
record :Body as {
  x: :Quantity(m),
  y: :Quantity(m),
  count: :Number,
  zero: :Number,
  big: :Vector computed by :Vector(x: count, y: count * 2),
  huge: :Vector computed by :Vector(x: 2 ^ 1000, y: 1) * 2 ^ 1000
}
on Start() {
  let value be :Body(x: 1.5m, y: -2.25m, count: 9007199254740993, zero: 0)
  emit Done(big: value.big, huge: value.huge)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: "expect"
steps:
  step-0001:
    local:
      - name: "Done"
        args:
          - name: "big"
            value:
              type: ":Vector"
              x: "9007199254740992"
              y: "1.8014398509481984e16"
              z: "0"
          - name: "huge"
            value:
              type: ":Vector"
              x: "Infinity"
              y: "1.0715086071862673e301"
              z: "0"
```

---

## Test: boxes in a record

The box forms and parts of the schema case.

### Case description

```yaml
gesBlock: case
id: record-box-forms
kind: scriptApi
level: scenario
sources:
  - name: "record-box-forms.ges"
    program: main
```

### Source code under test

```ges
record :Body as {
  x: :Quantity(m),
  y: :Quantity(m),
  count: :Number,
  zero: :Number,
  swapped: :Box computed by :Box(lower: :Point(x: 5m, y: 1m), upper: :Point(x: 2m, y: 3m)),
  around: :Box computed by :Box(center: :Point(x: x, y: y), size: :Vector(x: 4m, y: -2m, z: 1m)),
  lower: :Point computed by swapped.lower,
  upper: :Point computed by swapped.upper,
  middle: :Point computed by around.center,
  extent: :Vector computed by around.size,
  width: :Quantity(m) computed by around.size.x
}
on Start() {
  let value be :Body(x: 1.5m, y: -2.25m, count: 3, zero: 0)
  emit Done(swapped: value.swapped, around: value.around, lower: value.lower, upper: value.upper, middle: value.middle, extent: value.extent, width: value.width)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: "expect"
steps:
  step-0001:
    local:
      - name: "Done"
        args:
          - name: "swapped"
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
          - name: "around"
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
          - name: "lower"
            value:
              type: ":Point"
              x: "2"
              y: "1"
              z: "0"
              unit: ":meter"
          - name: "upper"
            value:
              type: ":Point"
              x: "5"
              y: "3"
              z: "0"
              unit: ":meter"
          - name: "middle"
            value:
              type: ":Point"
              x: "1.5"
              y: "-2.25"
              z: "0"
              unit: ":meter"
          - name: "extent"
            value:
              type: ":Vector"
              x: "4"
              y: "2"
              z: "1"
              unit: ":meter"
          - name: "width"
            value:
              type: ":Quantity.int64"
              value: "4"
              unit: ":meter"
```

---

## Test: discs in a record

The disc forms and parts of the schema case.

### Case description

```yaml
gesBlock: case
id: record-disc-forms
kind: scriptApi
level: scenario
sources:
  - name: "record-disc-forms.ges"
    program: main
```

### Source code under test

```ges
record :Body as {
  x: :Quantity(m),
  y: :Quantity(m),
  count: :Number,
  zero: :Number,
  zone: :Disc computed by :Disc(center: :Point(x: x, y: y), radius: -2.5m),
  ring: :Disc computed by :Disc(radius: 3m),
  lost: :Disc computed by :Disc(radius: x / zero * zero),
  center: :Point computed by zone.center,
  radius: :Quantity(m) computed by zone.radius
}
on Start() {
  let value be :Body(x: 1.5m, y: -2.25m, count: 3, zero: 0)
  emit Done(zone: value.zone, ring: value.ring, lost: value.lost, center: value.center, radius: value.radius)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: "expect"
steps:
  step-0001:
    local:
      - name: "Done"
        args:
          - name: "zone"
            value:
              type: ":Disc"
              center:
                type: ":Point"
                x: "1.5"
                y: "-2.25"
                z: "0"
              radius: "2.5"
              unit: ":meter"
          - name: "ring"
            value:
              type: ":Disc"
              center:
                type: ":Point"
                x: "0"
                y: "0"
                z: "0"
              radius: "3"
              unit: ":meter"
          - name: "lost"
            value:
              type: ":Nothing"
          - name: "center"
            value:
              type: ":Point"
              x: "1.5"
              y: "-2.25"
              z: "0"
              unit: ":meter"
          - name: "radius"
            value:
              type: ":Quantity.binary64"
              value: "2.5"
              unit: ":meter"
```

---

## Test: polygons in a record

The polygon forms of the schema case.

### Case description

```yaml
gesBlock: case
id: record-poly-forms
kind: scriptApi
level: scenario
sources:
  - name: "record-poly-forms.ges"
    program: main
```

### Source code under test

```ges
record :Post as { x: :Quantity(m), y: :Quantity(m) }
record :Path as {
  marks: :List,
  posts: :List,
  triangle: :Poly computed by :Poly(points: [:Point(x: 0m, y: 0m), :Point(x: 4m, y: 0m), :Point(x: 0m, y: 3m, z: 1m)]),
  marked: :Poly computed by :Poly(points: marks),
  fence: :Poly computed by :Poly(points: posts[:select post => :Point(x: post.x, y: post.y)]),
  count: :Number computed by fence.points[:count]
}
on Start() {
  let value be :Path(marks: [:Point(x: 1m, y: 2m), :Point(x: 3m, y: 4m)], posts: [:Post(x: 5m, y: 6m), :Post(x: 7m, y: 8m)])
  emit Done(triangle: value.triangle, marked: value.marked, fence: value.fence, count: value.count)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: "expect"
steps:
  step-0001:
    local:
      - name: "Done"
        args:
          - name: "triangle"
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
          - name: "marked"
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
          - name: "fence"
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
          - name: "count"
            value:
              type: ":Number.int64"
              value: "2"
```

---

## Test: polygons over empty lists in a record

Empty lists give polygons without points, which have no unit in a script, unlike a schema field of a declared unit; a nothing point makes the polygon nothing.

### Case description

```yaml
gesBlock: case
id: record-poly-empty
kind: scriptApi
level: scenario
sources:
  - name: "record-poly-empty.ges"
    program: main
```

### Source code under test

```ges
record :Post as { x: :Quantity(m), y: :Quantity(m) }
record :Path as {
  marks: :List,
  posts: :List,
  marked: :Poly computed by :Poly(points: marks),
  fence: :Poly computed by :Poly(points: posts[:select post => :Point(x: post.x, y: post.y)]),
  count: :Number computed by marked.points[:count],
  lost: :Poly computed by :Poly(points: [:Point(x: 0m, y: 0m), :Point(x: 0m / 0, y: 0m)])
}
on Start() {
  let value be :Path(marks: [], posts: [])
  emit Done(marked: value.marked, fence: value.fence, count: value.count, lost: value.lost)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: "expect"
steps:
  step-0001:
    local:
      - name: "Done"
        args:
          - name: "marked"
            value:
              type: ":Poly"
              points: []
          - name: "fence"
            value:
              type: ":Poly"
              points: []
          - name: "count"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "lost"
            value:
              type: ":Nothing"
```

---

## Test: an absent vector in a record

An absent field reads as nothing; guards and fallbacks keep a value.

### Case description

```yaml
gesBlock: case
id: record-optional-spatial
kind: scriptApi
level: scenario
sources:
  - name: "record-optional-spatial.ges"
    program: main
```

### Source code under test

```ges
record :Drift as {
  origin: :Point,
  drift: :Vector,
  shifted: :Point computed by origin + drift,
  moved: :Point computed by origin + drift default origin,
  drifting: :Boolean computed by drift has value,
  far: :Point computed by origin + drift when drift has value, otherwise origin
}
on Start() {
  let value be :Drift(origin: :Point(x: 2m, y: -3m))
  emit Done(shifted: value.shifted, moved: value.moved, drifting: value.drifting, far: value.far)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: "expect"
steps:
  step-0001:
    local:
      - name: "Done"
        args:
          - name: "shifted"
            value:
              type: ":Nothing"
          - name: "moved"
            value:
              type: ":Point"
              x: "2"
              y: "-3"
              z: "0"
              unit: ":meter"
          - name: "drifting"
            value:
              type: ":Boolean"
              value: "false"
          - name: "far"
            value:
              type: ":Point"
              x: "2"
              y: "-3"
              z: "0"
              unit: ":meter"
```
