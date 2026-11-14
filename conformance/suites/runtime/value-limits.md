---
formatVersion: 1
suiteId: runtime.value-limits
title: Value nesting and Range work limits
kind: scriptApi
level: scenario
categories: [conformance]
tags: [runtime-limits, collections, ranges]
---

# Value nesting and Range work limits

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies the nesting depth bound of values a handler constructs and the bounds of operations that visit or materialize Range items without a loop.

---

## Test: A List exactly at the depth limit is allowed

This case verifies: A value whose nesting equals MaxValueDepth is constructed and emitted normally.

### Case description

```yaml
gesBlock: case
id: "value-depth-at-limit"
runtimeLimits:
  maxValueDepth: 3
```

### Source code under test

```ges
on Start {
  let value be [[[1]]]
  emit Done(value: value)
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
      args: []
    local:
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":List"
              items:
                - type: ":List"
                  items:
                    - type: ":List"
                      items:
                        - type: ":Number.int64"
                          value: "1"
    runtimeLimits:
      exclude:
        - name: "MaxValueDepth"
          limit: 3
```

---

## Test: A List one level deeper than the limit stops the handler

This case verifies: Constructing a List one level deeper than MaxValueDepth stops the handler before its next statement; earlier effects remain.

### Case description

```yaml
gesBlock: case
id: "value-depth-list-exceeded"
runtimeLimits:
  maxValueDepth: 3
```

### Source code under test

```ges
on Start {
  emit Before()
  let value be [[[[1]]]]
  emit Unexpected(value: value)
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
      args: []
    local:
      - name: "Before"
        args: []
    runtimeLimits:
      include:
        - name: "MaxValueDepth"
          limit: 3
```

---

## Test: Inserting into a List one level deeper than the limit stops the handler

This case verifies: Adding a value to a List with + builds a List whose depth is checked like any other construction.

### Case description

```yaml
gesBlock: case
id: "value-depth-list-insertion"
runtimeLimits:
  maxValueDepth: 3
```

### Source code under test

```ges
on Start {
  emit Before()
  let value be [] + [[[1]]]
  emit Unexpected(value: value)
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
      args: []
    local:
      - name: "Before"
        args: []
    runtimeLimits:
      include:
        - name: "MaxValueDepth"
          limit: 3
```

---

## Test: Zipping Lists one level deeper than the limit stops the handler

This case verifies: zip builds Maps of the items of two Lists, whose depth is checked like any other construction.

### Case description

```yaml
gesBlock: case
id: "value-depth-zip"
runtimeLimits:
  maxValueDepth: 3
```

### Source code under test

```ges
on Start {
  emit Before()
  let value be [[[1]]] zip [[[1]]]
  emit Unexpected(value: value)
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
      args: []
    local:
      - name: "Before"
        args: []
    runtimeLimits:
      include:
        - name: "MaxValueDepth"
          limit: 3
```

---

## Test: Maps count toward the nesting depth

This case verifies: A Map is one level deeper than its deepest value, so a Map of a Map of a List exceeds a limit of 2.

### Case description

```yaml
gesBlock: case
id: "value-depth-map-exceeded"
runtimeLimits:
  maxValueDepth: 2
```

### Source code under test

```ges
on Start {
  let inner be [a: 1]
  emit Inner(value: inner)
  let outer be [b: [c: [1]]]
  emit Unexpected(value: outer)
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
      args: []
    local:
      - name: "Inner"
        args:
          - name: "value"
            value:
              type: ":Map"
              entries:
                - key: "a"
                  value:
                    type: ":Number.int64"
                    value: "1"
    runtimeLimits:
      include:
        - name: "MaxValueDepth"
          limit: 2
```

---

## Test: A fold that wraps its accumulator stops at the default depth

This case verifies: Wrapping an accumulator in a new List on every step stops the handler with the default MaxValueDepth of 64 instead of building an unbounded nesting.

### Case description

```yaml
gesBlock: case
id: "value-depth-fold-default"
```

### Source code under test

```ges
on Start {
  let shallow be (from 1 to 63)[:fold acc be [], value => [acc]]
  emit Shallow()
  let deep be (from 1 to 9000)[:fold acc be [], value => [acc]]
  emit Unexpected()
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
      args: []
    local:
      - name: "Shallow"
        args: []
    runtimeLimits:
      include:
        - name: "MaxValueDepth"
          limit: 64
```

---

## Test: Received values are not checked until a handler nests them

This case verifies: A received argument deeper than MaxValueDepth is passed on unchanged, while wrapping it in a new List stops the handler.

### Case description

```yaml
gesBlock: case
id: "value-depth-received-argument"
runtimeLimits:
  maxValueDepth: 1
```

### Source code under test

```ges
on Start(items) {
  emit Seen(value: items)
  let wrapped be [items]
  emit Unexpected(value: wrapped)
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
              - type: ":List"
                items: []
    local:
      - name: "Seen"
        args:
          - name: "value"
            value:
              type: ":List"
              items:
                - type: ":List"
                  items: []
    runtimeLimits:
      include:
        - name: "MaxValueDepth"
          limit: 1
```

---

## Test: A raised depth limit allows deeper values

This case verifies: With MaxValueDepth raised to 200, a fold may nest values deeper than the default limit.

### Case description

```yaml
gesBlock: case
id: "value-depth-raised"
runtimeLimits:
  maxValueDepth: 200
```

### Source code under test

```ges
on Start {
  let deep be (from 1 to 100)[:fold acc be [], value => [acc]]
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
      args: []
    local:
      - name: "Done"
        args: []
    runtimeLimits:
      exclude:
        - name: "MaxValueDepth"
          limit: 200
```

---

## Test: Shuffling a Range longer than MaxRangeItems yields an empty List

This case verifies: Shuffle materializes a Range, so a Range longer than MaxRangeItems reports the limit and shuffles to an empty List, as the List cast does, while the handler continues.

### Case description

```yaml
gesBlock: case
id: "range-shuffle-over-limit"
runtimeLimits:
  maxRangeItems: 5
```

### Source code under test

```ges
on Start {
  let fits be (from 1 to 5)[:shuffle]
  let over be (from 1 to 6)[:shuffle]
  emit Done(fits: fits[:count], over: over[:count])
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
      args: []
    local:
      - name: "Done"
        args:
          - name: "fits"
            value:
              type: ":Number.int64"
              value: "5"
          - name: "over"
            value:
              type: ":Number.int64"
              value: "0"
    runtimeLimits:
      include:
        - name: "MaxRangeItems"
          limit: 5
```

---

## Test: Choosing at random from a long Range does not materialize it

This case verifies: Choosing items at random from a Range far longer than MaxRangeItems draws positions among the items not chosen yet, without a limit observation.

### Case description

```yaml
gesBlock: case
id: "range-choose-random-long"
runtimeLimits:
  maxRangeItems: 5
random:
  sequence: ["1", "0", "2"]
```

### Source code under test

```ges
on Start {
  let picked be (from 1 to 2000000000)[:choose 3 at random]
  emit Done(picked: picked)
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
      args: []
    local:
      - name: "Done"
        args:
          - name: "picked"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "2"
                - type: ":Number.int64"
                  value: "1"
                - type: ":Number.int64"
                  value: "5"
    runtimeLimits:
      exclude:
        - name: "MaxRangeItems"
          limit: 5
```

---

## Test: Integer Ranges decide has any and has all without visiting items

This case verifies: HasAny and HasAll over integer Ranges answer from the Range bounds, so a Range of a billion items stays within a loop limit of 2.

### Case description

```yaml
gesBlock: case
id: "range-has-integer-closed-form"
runtimeLimits:
  maxLoopIterations: 2
```

### Source code under test

```ges
on Start {
  let long be from 1 to 1000000000
  let zero be from -5 to 5 step 5
  let only be from 0 to 0
  let allLong be asm {
    HasAll allLong, long
  }
  let anyLong be asm {
    HasAny anyLong, long
  }
  let allZero be asm {
    HasAll allZero, zero
  }
  let anyOnly be asm {
    HasAny anyOnly, only
  }
  emit Done(allLong: allLong, anyLong: anyLong, allZero: allZero, anyOnly: anyOnly)
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
      args: []
    local:
      - name: "Done"
        args:
          - name: "allLong"
            value:
              type: ":Boolean"
              value: true
          - name: "anyLong"
            value:
              type: ":Boolean"
              value: true
          - name: "allZero"
            value:
              type: ":Boolean"
              value: false
          - name: "anyOnly"
            value:
              type: ":Boolean"
              value: false
    runtimeLimits:
      exclude:
        - name: "MaxLoopIterations"
          limit: 2
```

---

## Test: Has all over a binary64 Range charges the loop budget

This case verifies: HasAll visits the items of a binary64 Range one by one and charges each against MaxLoopIterations, so a long Range stops the handler.

### Case description

```yaml
gesBlock: case
id: "range-has-all-float-charged"
runtimeLimits:
  maxLoopIterations: 10
```

### Source code under test

```ges
on Start {
  let short be from 0.5 to 9.5
  let long be from 0.5 to 100.5
  let allShort be asm {
    HasAll allShort, short
  }
  emit Short(value: allShort)
  let allLong be asm {
    HasAll allLong, long
  }
  emit Unexpected(value: allLong)
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
      args: []
    local:
      - name: "Short"
        args:
          - name: "value"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      include:
        - name: "MaxLoopIterations"
          limit: 10
```

---

## Test: Contains any with a Range of candidates charges the loop budget

This case verifies: Each candidate of a Range searched by `:contains any` charges MaxLoopIterations, so a long candidate Range without a match stops the handler instead of visiting every item.

### Case description

```yaml
gesBlock: case
id: "range-contains-any-charged"
runtimeLimits:
  maxLoopIterations: 10
```

### Source code under test

```ges
on Start {
  let values be [1, 2, 3]
  emit Short(value: values[:contains any (from 3 to 5)])
  emit Unexpected(value: values[:contains any (from 10 to 1000000000)])
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
      args: []
    local:
      - name: "Short"
        args:
          - name: "value"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      include:
        - name: "MaxLoopIterations"
          limit: 10
```

---

## Test: Two integer Ranges compare their boundaries without visiting items

This case verifies: `starts with` and `ends with` between integer Ranges answer from the first or last terms and the steps, so billion-item Ranges stay within a loop limit of 2.

### Case description

```yaml
gesBlock: case
id: "range-affix-integer-closed-form"
runtimeLimits:
  maxLoopIterations: 2
```

### Source code under test

```ges
on Start {
  let long be from 1 to 1000000000
  emit Done(prefix: long starts with (from 1 to 999999999), suffix: long ends with (from 2 to 1000000000), single: long ends with (from 1000000000 to 1000000000), stepped: long starts with (from 1 to 999999999 step 2), longer: (from 2 to 9) ends with long)
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
      args: []
    local:
      - name: "Done"
        args:
          - name: "prefix"
            value:
              type: ":Boolean"
              value: true
          - name: "suffix"
            value:
              type: ":Boolean"
              value: true
          - name: "single"
            value:
              type: ":Boolean"
              value: true
          - name: "stepped"
            value:
              type: ":Boolean"
              value: false
          - name: "longer"
            value:
              type: ":Boolean"
              value: false
    runtimeLimits:
      exclude:
        - name: "MaxLoopIterations"
          limit: 2
```

---

## Test: Comparing two binary64 Ranges charges the loop budget

This case verifies: `starts with` between binary64 Ranges compares item pairs one by one and charges each against MaxLoopIterations.

### Case description

```yaml
gesBlock: case
id: "range-affix-float-charged"
runtimeLimits:
  maxLoopIterations: 10
```

### Source code under test

```ges
on Start {
  let long be from 0.5 to 100.5
  emit Short(value: long starts with (from 0.5 to 3.5))
  emit Unexpected(value: long starts with (from 0.5 to 99.5))
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
      args: []
    local:
      - name: "Short"
        args:
          - name: "value"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      include:
        - name: "MaxLoopIterations"
          limit: 10
```
