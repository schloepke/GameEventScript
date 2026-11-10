---
formatVersion: 1
suiteId: "runtime.geometry"
title: "RuntimeGeometry"
categories: [conformance]
---

# RuntimeGeometry

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies the standard geometry library: the external types `:Box` and `:Poly` and the functions
`:geometry.*`, which compute as the native `GesBox` and `GesPoly` of both runtimes.

---

## Test: box operations

A box orders its corners and keeps their unit; its operations follow the native GesBox.

### Case description

```yaml
gesBlock: case
id: geometry-box
kind: scriptApi
level: scenario
sources:
  - name: "geometry-box.ges"
    program: main
```

### Source code under test

```ges
on Start() {
  let box be :Box(lower: :Point(10m, 0m), upper: :Point(0m, 4m))
  let other be :Box(lower: :Point(8m, 2m), upper: :Point(12m, 6m))
  let far be :Box(center: :Point(20m, 20m), size: :Vector(2m, -4m))
  emit Done(lower: box.lower, upper: box.upper, size: :geometry.size(box), center: :geometry.center(box), inside: :geometry.contains(box, :Point(5m, 4m)), outside: :geometry.contains(box, :Point(5m, 5m)), overlap: :geometry.intersects(box, other), apart: :geometry.intersects(box, far), union: :geometry.union(box, other).upper, inset: :geometry.inset(box, by: 1m).lower, crossed: :geometry.inset(box, by: 3m), nearest: :geometry.nearest(box, :Point(12m, -1m)), farMin: far.lower, farMax: far.upper)
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
          - name: "lower"
            value:
              type: ":Point"
              x: "0"
              y: "0"
              z: "0"
              unit: ":meter"
          - name: "upper"
            value:
              type: ":Point"
              x: "10"
              y: "4"
              z: "0"
              unit: ":meter"
          - name: "size"
            value:
              type: ":Vector"
              x: "10"
              y: "4"
              z: "0"
              unit: ":meter"
          - name: "center"
            value:
              type: ":Point"
              x: "5"
              y: "2"
              z: "0"
              unit: ":meter"
          - name: "inside"
            value:
              type: ":Boolean"
              value: true
          - name: "outside"
            value:
              type: ":Boolean"
              value: false
          - name: "overlap"
            value:
              type: ":Boolean"
              value: true
          - name: "apart"
            value:
              type: ":Boolean"
              value: false
          - name: "union"
            value:
              type: ":Point"
              x: "12"
              y: "6"
              z: "0"
              unit: ":meter"
          - name: "inset"
            value:
              type: ":Point"
              x: "1"
              y: "1"
              z: "0"
              unit: ":meter"
          - name: "crossed"
            value:
              type: ":Nothing"
          - name: "nearest"
            value:
              type: ":Point"
              x: "10"
              y: "0"
              z: "0"
              unit: ":meter"
          - name: "farMin"
            value:
              type: ":Point"
              x: "19"
              y: "18"
              z: "0"
              unit: ":meter"
          - name: "farMax"
            value:
              type: ":Point"
              x: "21"
              y: "22"
              z: "0"
              unit: ":meter"
```

---

## Test: box units and kinds

Corners of another unit or a value that is no Point construct nothing, and an operation with another unit gives nothing.

### Case description

```yaml
gesBlock: case
id: geometry-box-units
kind: scriptApi
level: scenario
sources:
  - name: "geometry-box-units.ges"
    program: main
```

### Source code under test

```ges
on Start() {
  let box be :Box(lower: :Point(0m, 0m), upper: :Point(4m, 4m))
  emit Done(mixed: :Box(lower: :Point(0m, 0m), upper: :Point(1, 1)), text: :Box(lower: :Point(0m, 0m), upper: 'corner'), unitless: :geometry.contains(box, :Point(1, 1)), seconds: :geometry.inset(box, by: 1s), plain: :geometry.size(:Box(lower: :Point(1, 2), upper: :Point(3, 5))), notBox: :geometry.size(:Point(1m, 1m)))
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
          - name: "mixed"
            value:
              type: ":Nothing"
          - name: "text"
            value:
              type: ":Nothing"
          - name: "unitless"
            value:
              type: ":Nothing"
          - name: "seconds"
            value:
              type: ":Nothing"
          - name: "plain"
            value:
              type: ":Vector"
              x: "2"
              y: "3"
              z: "0"
          - name: "notBox"
            value:
              type: ":Nothing"
```

---

## Test: a 3D box

A box with depth contains, insets and finds the nearest point on all three axes.

### Case description

```yaml
gesBlock: case
id: geometry-box-3d
kind: scriptApi
level: scenario
sources:
  - name: "geometry-box-3d.ges"
    program: main
```

### Source code under test

```ges
on Start() {
  let box be :Box(lower: :Point(0, 0, 0), upper: :Point(4, 4, 4))
  emit Done(inside: :geometry.contains(box, :Point(1, 1, 3)), above: :geometry.contains(box, :Point(1, 1, 5)), inset: :geometry.inset(box, by: 1).upper, nearest: :geometry.nearest(box, :Point(5, -1, 2)), flat: :geometry.contains(:Box(lower: :Point(0, 0), upper: :Point(4, 4)), :Point(1, 1, 1)))
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
          - name: "inside"
            value:
              type: ":Boolean"
              value: true
          - name: "above"
            value:
              type: ":Boolean"
              value: false
          - name: "inset"
            value:
              type: ":Point"
              x: "3"
              y: "3"
              z: "3"
          - name: "nearest"
            value:
              type: ":Point"
              x: "4"
              y: "0"
              z: "2"
          - name: "flat"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: polygon operations

A counterclockwise square has a positive area; its outline counts as inside, and distance is to the outline.

### Case description

```yaml
gesBlock: case
id: geometry-poly
kind: scriptApi
level: scenario
sources:
  - name: "geometry-poly.ges"
    program: main
```

### Source code under test

```ges
on Start() {
  let square be :Poly(points: [:Point(0m, 0m), :Point(4m, 0m), :Point(4m, 4m), :Point(0m, 4m)])
  let turned be :Poly(points: [:Point(0m, 0m), :Point(0m, 4m), :Point(4m, 4m), :Point(4m, 0m)])
  let near be :Poly(points: [:Point(3m, 3m), :Point(6m, 3m), :Point(6m, 6m)])
  let far be :Poly(points: [:Point(10m, 10m), :Point(12m, 10m), :Point(11m, 12m)])
  let inner be :Poly(points: [:Point(1m, 1m), :Point(2m, 1m), :Point(2m, 2m)])
  emit Done(area: :geometry.area(square), clockwise: :geometry.area(turned), centroid: :geometry.centroid(square), bounds: :geometry.bounds(near).upper, inside: :geometry.contains(square, :Point(2m, 2m)), edge: :geometry.contains(square, :Point(4m, 2m)), outside: :geometry.contains(square, :Point(5m, 5m)), center: :geometry.distance(square, :Point(2m, 1m)), away: :geometry.distance(square, :Point(7m, 8m)), crossing: :geometry.intersects(square, near), apart: :geometry.intersects(square, far), within: :geometry.intersects(square, inner), first: square.points[1])
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
          - name: "area"
            value:
              type: ":Number.int64"
              value: "16"
          - name: "clockwise"
            value:
              type: ":Number.int64"
              value: "-16"
          - name: "centroid"
            value:
              type: ":Point"
              x: "2"
              y: "2"
              z: "0"
              unit: ":meter"
          - name: "bounds"
            value:
              type: ":Point"
              x: "6"
              y: "6"
              z: "0"
              unit: ":meter"
          - name: "inside"
            value:
              type: ":Boolean"
              value: true
          - name: "edge"
            value:
              type: ":Boolean"
              value: true
          - name: "outside"
            value:
              type: ":Boolean"
              value: false
          - name: "center"
            value:
              type: ":Quantity.int64"
              value: "1"
              unit: ":meter"
          - name: "away"
            value:
              type: ":Quantity.int64"
              value: "5"
              unit: ":meter"
          - name: "crossing"
            value:
              type: ":Boolean"
              value: true
          - name: "apart"
            value:
              type: ":Boolean"
              value: false
          - name: "within"
            value:
              type: ":Boolean"
              value: true
          - name: "first"
            value:
              type: ":Point"
              x: "0"
              y: "0"
              z: "0"
              unit: ":meter"
```

---

## Test: invalid polygons

Fewer than three points, a crossing outline, a repeated point, no area and depth make every operation nothing; other kinds construct nothing.

### Case description

```yaml
gesBlock: case
id: geometry-poly-invalid
kind: scriptApi
level: scenario
sources:
  - name: "geometry-poly-invalid.ges"
    program: main
```

### Source code under test

```ges
on Start() {
  let two be :Poly(points: [:Point(0, 0), :Point(4, 0)])
  let bowtie be :Poly(points: [:Point(0, 0), :Point(4, 4), :Point(4, 0), :Point(0, 4)])
  let repeated be :Poly(points: [:Point(0, 0), :Point(4, 0), :Point(4, 0), :Point(0, 4)])
  let line be :Poly(points: [:Point(0, 0), :Point(2, 0), :Point(4, 0)])
  let deep be :Poly(points: [:Point(0, 0, 1), :Point(4, 0, 1), :Point(0, 4, 1)])
  let square be :Poly(points: [:Point(0, 0), :Point(4, 0), :Point(4, 4), :Point(0, 4)])
  emit Done(two: :geometry.area(two), bowtie: :geometry.contains(bowtie, :Point(1, 2)), repeated: :geometry.centroid(repeated), line: :geometry.bounds(line), deep: :geometry.area(deep), meets: :geometry.intersects(square, bowtie), mixed: :Poly(points: [:Point(0m, 0m), :Point(1, 0), :Point(0, 1)]), notPoints: :Poly(points: [1, 2, 3]), count: two.points[:count])
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
          - name: "two"
            value:
              type: ":Nothing"
          - name: "bowtie"
            value:
              type: ":Nothing"
          - name: "repeated"
            value:
              type: ":Nothing"
          - name: "line"
            value:
              type: ":Nothing"
          - name: "deep"
            value:
              type: ":Nothing"
          - name: "meets"
            value:
              type: ":Nothing"
          - name: "mixed"
            value:
              type: ":Nothing"
          - name: "notPoints"
            value:
              type: ":Nothing"
          - name: "count"
            value:
              type: ":Number.int64"
              value: "2"
```

---

## Test: a box meets a polygon

A 2D box intersects a polygon as the polygon of its outline; bounds of a box is the box.

### Case description

```yaml
gesBlock: case
id: geometry-box-poly
kind: scriptApi
level: scenario
sources:
  - name: "geometry-box-poly.ges"
    program: main
```

### Source code under test

```ges
on Start() {
  let box be :Box(lower: :Point(0, 0), upper: :Point(2, 2))
  let triangle be :Poly(points: [:Point(1, 1), :Point(5, 1), :Point(5, 5)])
  let far be :Poly(points: [:Point(7, 7), :Point(9, 7), :Point(9, 9)])
  emit Done(touch: :geometry.intersects(box, triangle), reverse: :geometry.intersects(triangle, box), apart: :geometry.intersects(box, far), same: :geometry.bounds(box).upper, deep: :geometry.intersects(:Box(lower: :Point(0, 0, 0), upper: :Point(2, 2, 2)), triangle))
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
          - name: "touch"
            value:
              type: ":Boolean"
              value: true
          - name: "reverse"
            value:
              type: ":Boolean"
              value: true
          - name: "apart"
            value:
              type: ":Boolean"
              value: false
          - name: "same"
            value:
              type: ":Point"
              x: "2"
              y: "2"
              z: "0"
          - name: "deep"
            value:
              type: ":Nothing"
```
