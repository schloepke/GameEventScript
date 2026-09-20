---
formatVersion: 1
suiteId: runtime.atomic.external-spatial-units
title: "External spatial units"
categories: [conformance]
---

# External spatial units

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite checks unit preservation at constructor argument, field access, and map materialization boundaries.

---

## Test: fields preserve units without a declared unit override

This case keeps Vector and Point units when a descriptor declares only the spatial kind, both for constructor inputs and independently supplied field values.

### Case description

```yaml
gesBlock: case
id: fields
kind: scriptApi
level: scenario
requires:
  core: [external-types]
sources:
  - name: fields.ges
    program: main
```

### Source code under test

```ges
on Start {
  let probe be :SpatialProbe(vector: :Vector(7°, 8°, 9°), point: :Point(10m, 11m, 12m))
  emit Done(vector: probe.vector, point: probe.point, storedVector: probe.storedVector, storedPoint: probe.storedPoint)
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
    input: { args: [] }
    local:
      - name: Done
        args:
          - name: vector
            value: { type: ":Vector", x: "7", y: "8", z: "9", unit: ":degree" }
          - name: point
            value: { type: ":Point", x: "10", y: "11", z: "12", unit: ":meter" }
          - name: storedVector
            value: { type: ":Vector", x: "1", y: "2", z: "3", unit: ":meter" }
          - name: storedPoint
            value: { type: ":Point", x: "4", y: "5", z: "6", unit: ":second" }
```

---

## Test: map preserve units without a declared unit override

This case keeps Vector and Point units when a descriptor declares only the spatial kind, both for constructor inputs and independently supplied field values.

### Case description

```yaml
gesBlock: case
id: map
kind: scriptApi
level: scenario
requires:
  core: [external-types]
sources:
  - name: map.ges
    program: main
```

### Source code under test

```ges
on Start {
  let probe be :SpatialProbe(vector: :Vector(7°, 8°, 9°), point: :Point(10m, 11m, 12m))
  emit Done(value: probe as :Map)
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
    input: { args: [] }
    local:
      - name: Done
        args:
          - name: value
            value:
              type: ":Map"
              entries:
                - key: vector
                  value: { type: ":Vector", x: "7", y: "8", z: "9", unit: ":degree" }
                - key: point
                  value: { type: ":Point", x: "10", y: "11", z: "12", unit: ":meter" }
                - key: storedVector
                  value: { type: ":Vector", x: "1", y: "2", z: "3", unit: ":meter" }
                - key: storedPoint
                  value: { type: ":Point", x: "4", y: "5", z: "6", unit: ":second" }
```
