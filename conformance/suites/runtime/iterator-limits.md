---
formatVersion: 1
suiteId: runtime.iterator-limits
title: Iterator and generated-collection limits
kind: scriptApi
level: scenario
categories: [conformance]
tags: [runtime-limits, iterators, collections]
---

# Iterator and generated-collection limits

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies the host's iterator and incremental collection limits at exact boundaries, across frames, and after handler cleanup. It covers retained values, map keys, distinct keys, ordering buffers, group buckets, and present `nothing` items.

---

## Test: Generated list empty

This case verifies: An empty source produces an empty result without consuming the collection limit.

### Case description

```yaml
gesBlock: case
id: "generated-list-empty"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:select item => item]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items: []
    local:
      - name: "Done"
        args:
          - name: "result"
            value:
              type: ":List"
              items: []
    runtimeLimits:
      exclude:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Generated list below

This case verifies: A projection below the collection limit completes normally.

### Case description

```yaml
gesBlock: case
id: "generated-list-below"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:select item => item]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
    local:
      - name: "Done"
        args:
          - name: "result"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
    runtimeLimits:
      exclude:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Generated list exact

This case verifies: A projection exactly at the collection limit completes normally.

### Case description

```yaml
gesBlock: case
id: "generated-list-exact"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:select item => item]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
              - type: ":Number.int64"
                value: "2"
    local:
      - name: "Done"
        args:
          - name: "result"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
    runtimeLimits:
      exclude:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Generated list exceeded

This case verifies: The first excess result stops the handler without emitting a partial collection.

### Case description

```yaml
gesBlock: case
id: "generated-list-exceeded"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:select item => item]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
              - type: ":Number.int64"
                value: "2"
              - type: ":Number.int64"
                value: "3"
    local: []
    runtimeLimits:
      include:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Generated list nothing exact

This case verifies: Retained nothing values are valid results at the exact collection limit.

### Case description

```yaml
gesBlock: case
id: "generated-list-nothing-exact"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:select item => item]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Nothing"
              - type: ":Nothing"
    local:
      - name: "Done"
        args:
          - name: "result"
            value:
              type: ":List"
              items:
                - type: ":Nothing"
                - type: ":Nothing"
    runtimeLimits:
      exclude:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Generated list nothing exceeded

This case verifies: Retained nothing values count toward the collection limit.

### Case description

```yaml
gesBlock: case
id: "generated-list-nothing-exceeded"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:select item => item]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Nothing"
              - type: ":Nothing"
              - type: ":Nothing"
    local: []
    runtimeLimits:
      include:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Collection limit across frames exact

This case verifies that pausing between opcodes preserves the size of the active projection.

### Case description

```yaml
gesBlock: case
id: "generated-frames-exact"
runtimeLimits:
  maxGeneratedCollectionItems: 2
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(items) {
  let result be items[:select item => item]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | frames | 1 |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
              - type: ":Number.int64"
                value: "2"
    local:
      - name: "Done"
        args:
          - name: "result"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
    runtimeLimits:
      exclude:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
    paused: true
```

---

## Test: Collection limit across frames exceeded

This case verifies that pausing between opcodes preserves the size of the active projection.

### Case description

```yaml
gesBlock: case
id: "generated-frames-exceeded"
runtimeLimits:
  maxGeneratedCollectionItems: 2
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(items) {
  let result be items[:select item => item]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | frames | 1 |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
              - type: ":Number.int64"
                value: "2"
              - type: ":Number.int64"
                value: "3"
    local: []
    runtimeLimits:
      include:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
    paused: true
```

---

## Test: Filtering counts retained items

This case verifies that rejected source items do not grow the result collection.

### Case description

```yaml
gesBlock: case
id: "generated-filter-retained-items"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:filter item where item > 2]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
              - type: ":Number.int64"
                value: "2"
              - type: ":Number.int64"
                value: "3"
              - type: ":Number.int64"
                value: "4"
    local:
      - name: "Done"
        args:
          - name: "result"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "3"
                - type: ":Number.int64"
                  value: "4"
    runtimeLimits:
      exclude:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Filtering exceeds the collection limit

This case verifies that a filter cannot append a third accepted item to a result limited to two.

### Case description

```yaml
gesBlock: case
id: "generated-filter-exceeded"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:filter item where true]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
              - type: ":Number.int64"
                value: "2"
              - type: ":Number.int64"
                value: "3"
    local: []
    runtimeLimits:
      include:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Independent collection sizes

This case verifies that sequential projections each have their own collection size limit.

### Case description

```yaml
gesBlock: case
id: "generated-independent-collections"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let first be items[:select item => item]
  let second be items[:select item => item * 10]
  emit Done(first: first, second: second)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
              - type: ":Number.int64"
                value: "2"
    local:
      - name: "Done"
        args:
          - name: "first"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
          - name: "second"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "10"
                - type: ":Number.int64"
                  value: "20"
    runtimeLimits:
      exclude:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Collection limit for map new key exceeded

This case verifies the retained-item boundary for this selector, including its key and bucket semantics.

### Case description

```yaml
gesBlock: case
id: "generated-map-new-key-exceeded"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:map item by item.key => item.value]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Map"
                entries:
                  - key: "key"
                    value:
                      type: ":Text"
                      value: "a"
                  - key: "value"
                    value:
                      type: ":Number.int64"
                      value: "1"
              - type: ":Map"
                entries:
                  - key: "key"
                    value:
                      type: ":Text"
                      value: "b"
                  - key: "value"
                    value:
                      type: ":Number.int64"
                      value: "2"
              - type: ":Map"
                entries:
                  - key: "key"
                    value:
                      type: ":Text"
                      value: "c"
                  - key: "value"
                    value:
                      type: ":Number.int64"
                      value: "3"
    local: []
    runtimeLimits:
      include:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Collection limit for map overwrite and skipped keys

This case verifies the retained-item boundary for this selector, including its key and bucket semantics.

### Case description

```yaml
gesBlock: case
id: "generated-map-overwrite-and-skipped-keys"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:map item by item.key => item.value]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Map"
                entries:
                  - key: "key"
                    value:
                      type: ":Text"
                      value: "a"
                  - key: "value"
                    value:
                      type: ":Number.int64"
                      value: "1"
              - type: ":Map"
                entries:
                  - key: "key"
                    value:
                      type: ":Text"
                      value: "b"
                  - key: "value"
                    value:
                      type: ":Nothing"
              - type: ":Map"
                entries:
                  - key: "key"
                    value:
                      type: ":Text"
                      value: "a"
                  - key: "value"
                    value:
                      type: ":Number.int64"
                      value: "3"
              - type: ":Map"
                entries:
                  - key: "key"
                    value:
                      type: ":Text"
                      value: ""
                  - key: "value"
                    value:
                      type: ":Number.int64"
                      value: "4"
              - type: ":Map"
                entries:
                  - key: "key"
                    value:
                      type: ":Nothing"
                  - key: "value"
                    value:
                      type: ":Number.int64"
                      value: "5"
    local:
      - name: "Done"
        args:
          - name: "result"
            value:
              type: ":Map"
              entries:
                - key: "a"
                  value:
                    type: ":Number.int64"
                    value: "3"
                - key: "b"
                  value:
                    type: ":Nothing"
    runtimeLimits:
      exclude:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Collection limit for distinct new key exceeded

This case verifies the retained-item boundary for this selector, including its key and bucket semantics.

### Case description

```yaml
gesBlock: case
id: "generated-distinct-new-key-exceeded"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:distinct by item => item]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
              - type: ":Number.int64"
                value: "2"
              - type: ":Number.int64"
                value: "3"
    local: []
    runtimeLimits:
      include:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Collection limit for distinct duplicates at limit

This case verifies the retained-item boundary for this selector, including its key and bucket semantics.

### Case description

```yaml
gesBlock: case
id: "generated-distinct-duplicates-at-limit"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:distinct by item => item]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
              - type: ":Number.int64"
                value: "2"
              - type: ":Number.int64"
                value: "1"
              - type: ":Number.int64"
                value: "2"
    local:
      - name: "Done"
        args:
          - name: "result"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
    runtimeLimits:
      exclude:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Collection limit for distinct nothing at limit

This case verifies the retained-item boundary for this selector, including its key and bucket semantics.

### Case description

```yaml
gesBlock: case
id: "generated-distinct-nothing-at-limit"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:distinct by item => item]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Nothing"
              - type: ":Number.int64"
                value: "1"
              - type: ":Nothing"
              - type: ":Number.int64"
                value: "1"
    local:
      - name: "Done"
        args:
          - name: "result"
            value:
              type: ":List"
              items:
                - type: ":Nothing"
                - type: ":Number.int64"
                  value: "1"
    runtimeLimits:
      exclude:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Collection limit for order exceeded

This case verifies the retained-item boundary for this selector, including its key and bucket semantics.

### Case description

```yaml
gesBlock: case
id: "generated-order-exceeded"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:order by item => item ascending]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "3"
              - type: ":Number.int64"
                value: "1"
              - type: ":Number.int64"
                value: "2"
    local: []
    runtimeLimits:
      include:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Collection limit for order duplicates exceeded

This case verifies the retained-item boundary for this selector, including its key and bucket semantics.

### Case description

```yaml
gesBlock: case
id: "generated-order-duplicates-exceeded"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:order by item => item ascending]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
              - type: ":Number.int64"
                value: "1"
              - type: ":Number.int64"
                value: "1"
    local: []
    runtimeLimits:
      include:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Collection limit for order exact

This case verifies the retained-item boundary for this selector, including its key and bucket semantics.

### Case description

```yaml
gesBlock: case
id: "generated-order-exact"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:order by item => item ascending]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "2"
              - type: ":Number.int64"
                value: "1"
    local:
      - name: "Done"
        args:
          - name: "result"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "2"
    runtimeLimits:
      exclude:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Collection limit for group count exceeded

This case verifies the retained-item boundary for this selector, including its key and bucket semantics.

### Case description

```yaml
gesBlock: case
id: "generated-group-count-exceeded"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:group by item => item]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Text"
                value: "a"
              - type: ":Text"
                value: "b"
              - type: ":Text"
                value: "c"
    local: []
    runtimeLimits:
      include:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Collection limit for group bucket exceeded

This case verifies the retained-item boundary for this selector, including its key and bucket semantics.

### Case description

```yaml
gesBlock: case
id: "generated-group-bucket-exceeded"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:group by item => item]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Text"
                value: "a"
              - type: ":Text"
                value: "a"
              - type: ":Text"
                value: "a"
    local: []
    runtimeLimits:
      include:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Collection limit for group independent buckets

This case verifies the retained-item boundary for this selector, including its key and bucket semantics.

### Case description

```yaml
gesBlock: case
id: "generated-group-independent-buckets"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:group by item => item]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Text"
                value: "a"
              - type: ":Text"
                value: "b"
              - type: ":Text"
                value: "a"
              - type: ":Text"
                value: "b"
    local:
      - name: "Done"
        args:
          - name: "result"
            value:
              type: ":Map"
              entries:
                - key: "a"
                  value:
                    type: ":List"
                    items:
                      - type: ":Text"
                        value: "a"
                      - type: ":Text"
                        value: "a"
                - key: "b"
                  value:
                    type: ":List"
                    items:
                      - type: ":Text"
                        value: "b"
                      - type: ":Text"
                        value: "b"
    runtimeLimits:
      exclude:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Collection limit for group nothing bucket exceeded

This case verifies the retained-item boundary for this selector, including its key and bucket semantics.

### Case description

```yaml
gesBlock: case
id: "generated-group-nothing-bucket-exceeded"
runtimeLimits:
  maxGeneratedCollectionItems: 2
```

### Source code under test

```ges
on Start(items) {
  let result be items[:group by item => 'a']
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Nothing"
              - type: ":Nothing"
              - type: ":Nothing"
    local: []
    runtimeLimits:
      include:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Generated range failure cleans up and preserves queued work

This scenario verifies one collection-limit observation, cleanup of a seeded scope, and subsequent queued dispatch on the same host.

### Case description

```yaml
gesBlock: case
id: "generated-range-cleanup-and-reuse"
runtimeLimits:
  maxGeneratedCollectionItems: 2
compile:
  binaryRoundTrip: true
random:
  sequence:
    - "42"
```

### Source code under test

```ges
on Start() {
  emit Deferred()
  random with 777 {
    let result be :List[:select item from 1 to 3 => random from 1 to 100]
    emit Partial(result: result)
  }
  emit Unexpected()
}
on Deferred() { emit Done(value: random from 1 to 100) }
on Check() { emit Checked() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| limited | Start | completion |  |
| resume | Check | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  limited:
    local:
      - name: "Deferred"
    runtimeLimits:
      include:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
    trace:
      - event: "dispatchStarted"
        message:
          name: "Start"
        signatureId: "Start()"
      - event: "emit"
        message:
          name: "Deferred"
        accepted: true
      - event: "runtimeLimit"
        runtimeLimit:
          name: "MaxGeneratedCollectionItems"
          limit: 2
      - event: "dispatchCompleted"
        message:
          name: "Start"
        signatureId: "Start()"
  resume:
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "42"
      - name: "Checked"
    runtimeLimits:
      exclude:
        - name: "MaxGeneratedCollectionItems"
          limit: 2
```

---

## Test: Loop limit nothing exact

This case verifies that successful advances count independently of the item value, while exhaustion consumes no iteration.

### Case description

```yaml
gesBlock: case
id: "loop-nothing-exact"
runtimeLimits:
  maxLoopIterations: 1
```

### Source code under test

```ges
on Start(items) {
  for item in items { emit Item(value: item) }
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Nothing"
    local:
      - name: "Item"
        args:
          - name: "value"
            value:
              type: ":Nothing"
      - name: "Done"
    runtimeLimits:
      exclude:
        - name: "MaxLoopIterations"
          limit: 1
```

---

## Test: Loop limit nothing exceeded

This case verifies that successful advances count independently of the item value, while exhaustion consumes no iteration.

### Case description

```yaml
gesBlock: case
id: "loop-nothing-exceeded"
runtimeLimits:
  maxLoopIterations: 1
```

### Source code under test

```ges
on Start(items) {
  for item in items { emit Item(value: item) }
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Nothing"
              - type: ":Nothing"
              - type: ":Nothing"
    local:
      - name: "Item"
        args:
          - name: "value"
            value:
              type: ":Nothing"
    runtimeLimits:
      include:
        - name: "MaxLoopIterations"
          limit: 1
```

---

## Test: Loop limit mixed exceeded

This case verifies that successful advances count independently of the item value, while exhaustion consumes no iteration.

### Case description

```yaml
gesBlock: case
id: "loop-mixed-exceeded"
runtimeLimits:
  maxLoopIterations: 2
```

### Source code under test

```ges
on Start(items) {
  for item in items { emit Item(value: item) }
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
              - type: ":Nothing"
              - type: ":Number.int64"
                value: "3"
    local:
      - name: "Item"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      - name: "Item"
        args:
          - name: "value"
            value:
              type: ":Nothing"
    runtimeLimits:
      include:
        - name: "MaxLoopIterations"
          limit: 2
```

---

## Test: Loop limit empty

This case verifies that successful advances count independently of the item value, while exhaustion consumes no iteration.

### Case description

```yaml
gesBlock: case
id: "loop-empty"
runtimeLimits:
  maxLoopIterations: 1
```

### Source code under test

```ges
on Start(items) {
  for item in items { emit Item(value: item) }
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items: []
    local:
      - name: "Done"
    runtimeLimits:
      exclude:
        - name: "MaxLoopIterations"
          limit: 1
```

---

## Test: Loop limit non iterable

This case verifies that successful advances count independently of the item value, while exhaustion consumes no iteration.

### Case description

```yaml
gesBlock: case
id: "loop-non-iterable"
runtimeLimits:
  maxLoopIterations: 1
```

### Source code under test

```ges
on Start(items) {
  for item in items { emit Item(value: item) }
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":Nothing"
    local:
      - name: "Done"
    runtimeLimits:
      exclude:
        - name: "MaxLoopIterations"
          limit: 1
```

---

## Test: Loop limit frames exact

This case verifies that successful advances count independently of the item value, while exhaustion consumes no iteration.

### Case description

```yaml
gesBlock: case
id: "loop-frames-exact"
runtimeLimits:
  maxLoopIterations: 2
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(items) {
  for item in items { emit Item(value: item) }
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | frames | 1 |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Nothing"
              - type: ":Nothing"
    local:
      - name: "Item"
        args:
          - name: "value"
            value:
              type: ":Nothing"
      - name: "Item"
        args:
          - name: "value"
            value:
              type: ":Nothing"
      - name: "Done"
    runtimeLimits:
      exclude:
        - name: "MaxLoopIterations"
          limit: 2
    paused: true
```

---

## Test: Loop limit frames exceeded

This case verifies that successful advances count independently of the item value, while exhaustion consumes no iteration.

### Case description

```yaml
gesBlock: case
id: "loop-frames-exceeded"
runtimeLimits:
  maxLoopIterations: 2
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start(items) {
  for item in items { emit Item(value: item) }
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | frames | 1 |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Nothing"
              - type: ":Nothing"
              - type: ":Nothing"
    local:
      - name: "Item"
        args:
          - name: "value"
            value:
              type: ":Nothing"
      - name: "Item"
        args:
          - name: "value"
            value:
              type: ":Nothing"
    runtimeLimits:
      include:
        - name: "MaxLoopIterations"
          limit: 2
    paused: true
```

---

## Test: Empty and invalid sources after an exhausted iteration allowance

This case verifies that an empty or non-iterable source consumes no iteration even after another loop used the exact allowance.

### Case description

```yaml
gesBlock: case
id: "loop-empty-after-exact-budget"
runtimeLimits:
  maxLoopIterations: 1
```

### Source code under test

```ges
on Start(items) {
  for item in items { emit Item(value: item) }
  for emptyItem in [] { emit Unexpected() }
  for invalidItem in nothing { emit Unexpected() }
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Nothing"
    local:
      - name: "Item"
        args:
          - name: "value"
            value:
              type: ":Nothing"
      - name: "Done"
    runtimeLimits:
      exclude:
        - name: "MaxLoopIterations"
          limit: 1
```

---

## Test: Rejected nothing items still consume loop iterations

This case verifies that iterator advances count even when a filter discards every item and its generated result stays empty.

### Case description

```yaml
gesBlock: case
id: "loop-filter-rejected-nothing"
runtimeLimits:
  maxLoopIterations: 1
  maxGeneratedCollectionItems: 1
```

### Source code under test

```ges
on Start(items) {
  let result be items[:filter item where false]
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Nothing"
              - type: ":Nothing"
    local: []
    runtimeLimits:
      include:
        - name: "MaxLoopIterations"
          limit: 1
      exclude:
        - name: "MaxGeneratedCollectionItems"
          limit: 1
```

---

## Test: Nested loops share the handler iteration budget

This case verifies that nothing advances in outer and inner loops contribute to the same counter.

### Case description

```yaml
gesBlock: case
id: "loop-nested-shared-budget"
runtimeLimits:
  maxLoopIterations: 3
```

### Source code under test

```ges
on Start(items) {
  for outer in items {
    for inner in items { emit Item(value: inner) }
  }
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Nothing"
              - type: ":Nothing"
    local:
      - name: "Item"
        args:
          - name: "value"
            value:
              type: ":Nothing"
      - name: "Item"
        args:
          - name: "value"
            value:
              type: ":Nothing"
    runtimeLimits:
      include:
        - name: "MaxLoopIterations"
          limit: 3
```

---

## Test: Sequential loops share the handler iteration budget

This case verifies that starting another loop in the same handler does not reset the count of nothing advances.

### Case description

```yaml
gesBlock: case
id: "loop-sequential-shared-budget"
runtimeLimits:
  maxLoopIterations: 1
```

### Source code under test

```ges
on Start(items) {
  for first in items { emit Item(value: first) }
  for second in items { emit Unexpected() }
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Nothing"
    local:
      - name: "Item"
        args:
          - name: "value"
            value:
              type: ":Nothing"
    runtimeLimits:
      include:
        - name: "MaxLoopIterations"
          limit: 1
```

---

## Test: Nothing iteration budgets reset for each handler

This scenario verifies that two handlers each receive the full iteration budget for the same message.

### Case description

```yaml
gesBlock: case
id: "loop-reset-per-handler"
runtimeLimits:
  maxLoopIterations: 1
sources:
  - name: "first.ges"
    program: "first"
  - name: "second.ges"
    program: "second"
```

### Source code under test

```ges
on Start(items) {
  for item in items { emit Item(value: item) }
  emit Done()
}
```

```ges
on Start(items) {
  for item in items { emit Item(value: item) }
  emit Done()
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "items"
          value:
            type: ":List"
            items:
              - type: ":Nothing"
    local:
      - name: "Item"
        args:
          - name: "value"
            value:
              type: ":Nothing"
      - name: "Done"
      - name: "Item"
        args:
          - name: "value"
            value:
              type: ":Nothing"
      - name: "Done"
    runtimeLimits:
      exclude:
        - name: "MaxLoopIterations"
          limit: 1
```

---

## Test: Nothing loop failure cleans up and resets the next handler

This scenario verifies one loop-limit observation, cleanup of a seeded scope, and a fresh iteration budget for the next message.

### Case description

```yaml
gesBlock: case
id: "loop-nothing-cleanup-and-reuse"
runtimeLimits:
  maxLoopIterations: 1
random:
  sequence:
    - "42"
```

### Source code under test

```ges
on Start() {
  random with 777 {
    for item in [nothing, nothing] {
      let consumed be random from 1 to 100
    }
  }
  emit Unexpected()
}
on Check() {
  for item in [nothing] { emit Done(value: random from 1 to 100) }
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| limited | Start | completion |  |
| check | Check | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  limited:
    runtimeLimits:
      include:
        - name: "MaxLoopIterations"
          limit: 1
    trace:
      - event: "dispatchStarted"
        message:
          name: "Start"
        signatureId: "Start()"
      - event: "runtimeLimit"
        runtimeLimit:
          name: "MaxLoopIterations"
          limit: 1
      - event: "dispatchCompleted"
        message:
          name: "Start"
        signatureId: "Start()"
  check:
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "42"
    runtimeLimits:
      exclude:
        - name: "MaxLoopIterations"
          limit: 1
```
